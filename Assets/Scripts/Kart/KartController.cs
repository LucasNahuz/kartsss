using System;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Track;
using VortexKarts.Utils;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Arcade kart controller built on a single sphere rigidbody.
    ///
    /// The sphere handles collisions and gravity; heading (yaw) and the "up" vector are simulated here and
    /// applied to a child Orientation transform, never to the rigidbody itself. Each physics step the old
    /// velocity is re-expressed in the new heading frame: the forward part is driven by throttle/brake and
    /// the lateral part decays with grip. Low grip (drift, slime) leaves lateral velocity alive, which is
    /// what makes the kart slide.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        public const float Gravity = 22f;
        public const float Radius = 0.6f;
        private const float GroundCheckExtra = 0.42f;
        private const float GroundedDownforce = 9f;
        private const float OverCapDeceleration = 14f;
        private const float AirSteerFactor = 0.3f;
        private const float SlipstreamDistance = 14f;

        [Header("Identity")]
        public KartStats Stats;
        public PilotData Pilot;
        public bool IsPlayer;
        public string DisplayName = "Kart";
        /// <summary>Slot in the race (0 = player when present).</summary>
        public int Index;

        [Header("Hierarchy (set by KartFactory)")]
        public Transform Orientation;
        public Transform VisualRoot;
        public Transform SeatAnchor;
        public Transform DashboardAnchor;
        public Transform[] ExhaustAnchors = new Transform[0];
        public Transform[] RearWheelAnchors = new Transform[0];

        public Rigidbody Body { get; private set; }
        public SphereCollider SphereCollider { get; private set; }
        public DriftController Drift { get; private set; }
        public BoostController Boost { get; private set; }
        public JumpController Jump { get; private set; }
        public KartStatusEffects Status { get; private set; }
        public RespawnController Respawn { get; private set; }
        public KartVisuals Visuals { get; private set; }

        // ---- Live state
        public KartInputState Input { get; private set; }
        /// <summary>Set by RaceManager during countdown / after finishing.</summary>
        public bool InputLocked { get; set; }
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public SurfaceType Surface { get; private set; } = SurfaceType.Road;
        public float SurfaceSpeedFactor { get; private set; } = 1f;
        public float SurfaceGripFactor { get; private set; } = 1f;
        public float ForwardSpeed { get; private set; }
        public float LateralSpeed { get; private set; }
        public float Speed => Body != null ? Body.linearVelocity.magnitude : 0f;
        public float SpeedFraction => Stats != null ? Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(1f, Stats.maxSpeed)) : 0f;
        public float CurrentMaxSpeed { get; private set; }
        public float Yaw { get; private set; }
        public Vector3 Forward { get; private set; } = Vector3.forward;
        public Vector3 Right { get; private set; } = Vector3.right;
        public Vector3 Up { get; private set; } = Vector3.up;
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public Vector3 Position => Body != null ? Body.position : transform.position;
        public Vector3 Velocity => Body != null ? Body.linearVelocity : Vector3.zero;
        public float EffectiveSteer { get; private set; }
        public float EffectiveThrottle { get; private set; }
        public bool IsReversing => ForwardSpeed < -0.5f;
        public float LastWallHitTime { get; private set; } = -10f;
        public float LastKartHitTime { get; private set; } = -10f;
        public bool HasFinished { get; set; }
        public float SlipstreamTime { get; private set; }

        public event Action<Collision, KartController> OnKartCollision;
        public event Action<Collision, float> OnWallCollision;

        private Vector3 upSmoothed = Vector3.up;
        private float pendingYawKick;
        private float pendingVerticalKick;
        private float lastSlipstreamBoost;
        private bool initialized;

        // ------------------------------------------------------------------ Setup

        public void Initialize(KartStats stats, PilotData pilot, bool isPlayer, string displayName)
        {
            Stats = stats;
            Pilot = pilot;
            IsPlayer = isPlayer;
            DisplayName = displayName;

            Body = GetComponent<Rigidbody>();
            SphereCollider = GetComponent<SphereCollider>();
            Drift = GetComponent<DriftController>() ?? gameObject.AddComponent<DriftController>();
            Boost = GetComponent<BoostController>() ?? gameObject.AddComponent<BoostController>();
            Jump = GetComponent<JumpController>() ?? gameObject.AddComponent<JumpController>();
            Status = GetComponent<KartStatusEffects>() ?? gameObject.AddComponent<KartStatusEffects>();
            Respawn = GetComponent<RespawnController>() ?? gameObject.AddComponent<RespawnController>();
            Visuals = GetComponent<KartVisuals>();

            Body.mass = stats.MassForPhysics;
            Body.useGravity = false;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.05f;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.maxAngularVelocity = 0f;

            if (Orientation == null)
            {
                var o = new GameObject("Orientation");
                o.transform.SetParent(transform, false);
                Orientation = o.transform;
            }

            Yaw = transform.eulerAngles.y;
            Orientation.rotation = Quaternion.Euler(0f, Yaw, 0f);
            UpdateFrame(Vector3.up);
            CurrentMaxSpeed = stats.maxSpeed;
            initialized = true;
        }

        private bool driftPressLatched;

        /// <summary>Drivers call this every Update. Presses are latched until the next physics step.</summary>
        public void SetInput(KartInputState state)
        {
            if (state.DriftPressed) driftPressLatched = true;
            Input = state;
        }

        // ------------------------------------------------------------------ External nudges

        /// <summary>Multiplies the whole horizontal velocity (hits, wall scrapes).</summary>
        public void ScaleSpeed(float factor)
        {
            if (Body == null) return;
            Vector3 v = Body.linearVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(v, Up);
            Vector3 vertical = v - horizontal;
            Body.linearVelocity = horizontal * Mathf.Clamp(factor, 0f, 2f) + vertical;
        }

        public void AddVerticalKick(float upVelocity)
        {
            pendingVerticalKick += upVelocity;
        }

        public void AddYawKick(float degrees)
        {
            pendingYawKick += degrees;
        }

        public void AddVelocity(Vector3 delta)
        {
            if (Body != null) Body.linearVelocity += delta;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (Body == null) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position + Vector3.up * Radius;
            transform.position = Body.position;
            Yaw = rotation.eulerAngles.y;
            upSmoothed = Vector3.up;
            UpdateFrame(Vector3.up);
            Orientation.rotation = Rotation;
            ForwardSpeed = 0f;
            LateralSpeed = 0f;
            pendingYawKick = 0f;
            pendingVerticalKick = 0f;
            if (Drift != null) Drift.Cancel();
            if (Boost != null) Boost.ClearAll();
            Physics.SyncTransforms();
        }

        public void StopCompletely()
        {
            if (Body == null) return;
            Body.linearVelocity = Vector3.zero;
            ForwardSpeed = 0f;
            LateralSpeed = 0f;
        }

        // ------------------------------------------------------------------ Physics

        private void FixedUpdate()
        {
            if (!initialized || Stats == null) return;
            float dt = Time.fixedDeltaTime;

            ReadGround();
            Jump.Tick(dt);

            bool locked = InputLocked || Status.ControlsLocked || HasFinished;
            KartInputState input = locked ? KartInputState.Empty : Input;
            input.DriftPressed = driftPressLatched && !locked;
            driftPressLatched = false;
            if (HasFinished)
            {
                // Coast to a gentle cruise after the finish line so the podium karts keep rolling.
                input.Throttle = ForwardSpeed < Stats.maxSpeed * 0.35f ? 0.6f : 0f;
            }

            if (input.DriftPressed && !locked) Jump.TryHop();
            Drift.Tick(input, locked, dt);

            // ---- Heading
            float speedFrac = SpeedFraction;
            float steer = input.Steer * Status.SteerMultiplier;
            float baseRate = Stats.GetSteerRate(speedFrac);
            float yawRate;
            if (Drift.IsDrifting)
            {
                yawRate = Drift.GetYawRate(steer, baseRate);
            }
            else
            {
                float speedGate = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 2.5f); // no pivoting when stopped
                yawRate = steer * baseRate * speedGate;
                if (ForwardSpeed < -0.5f) yawRate *= 0.8f;
            }
            if (!IsGrounded) yawRate *= AirSteerFactor;
            if (Status.IsSpinning)
            {
                // CPU karts spin for real. The player's heading only wobbles (VR comfort); the chassis
                // mesh spins visually instead (see KartVisuals).
                yawRate += IsPlayer ? Mathf.Sin(Time.time * 11f) * 45f : Status.SpinYawRate;
            }

            Yaw += yawRate * dt;
            if (Mathf.Abs(pendingYawKick) > 0.001f)
            {
                Yaw += pendingYawKick;
                pendingYawKick = 0f;
            }
            Yaw = MathUtil.WrapAngle180(Yaw);

            // Smooth the up vector towards the ground normal (airborne: back to world up).
            Vector3 targetUp = IsGrounded ? GroundNormal : Vector3.up;
            upSmoothed = Vector3.Slerp(upSmoothed, targetUp, 1f - Mathf.Exp(-(IsGrounded ? 14f : 3f) * dt)).normalized;
            UpdateFrame(upSmoothed);

            // ---- Re-express velocity in the new frame
            Vector3 vel = Body.linearVelocity;
            float fwdSpeed = Vector3.Dot(vel, Forward);
            float latSpeed = Vector3.Dot(vel, Right);
            float vertSpeed = Vector3.Dot(vel, Up);

            // ---- Speed cap
            bool ignoreSurfacePenalty = Boost.IsBoosting;
            float surface = ignoreSurfacePenalty ? Mathf.Max(SurfaceSpeedFactor, 1f) : SurfaceSpeedFactor;
            CurrentMaxSpeed = Stats.maxSpeed * Status.SpeedMultiplier * Boost.MaxSpeedMultiplier * surface;

            // ---- Longitudinal
            float throttle = Mathf.Clamp01(input.Throttle);
            float brake = Mathf.Clamp01(input.Brake);
            EffectiveThrottle = throttle;
            float accel = Stats.acceleration * Status.AccelMultiplier * Boost.AccelMultiplier;
            if (!IsGrounded) accel *= 0.25f;

            if (throttle > 0.01f && fwdSpeed < CurrentMaxSpeed)
            {
                float taper = 1f - 0.65f * Mathf.Clamp01(fwdSpeed / Mathf.Max(1f, CurrentMaxSpeed));
                if (fwdSpeed < 0f) taper = 1.3f; // recovering from reverse is quick
                fwdSpeed += accel * throttle * taper * dt;
                if (fwdSpeed > CurrentMaxSpeed) fwdSpeed = CurrentMaxSpeed;
            }
            if (brake > 0.01f)
            {
                if (fwdSpeed > 0.5f)
                {
                    fwdSpeed -= Stats.brakeDeceleration * brake * dt;
                    if (fwdSpeed < 0f) fwdSpeed = 0f;
                }
                else if (IsGrounded)
                {
                    fwdSpeed -= Stats.acceleration * 0.7f * brake * dt;
                    float reverseCap = -Stats.reverseMaxSpeed * Status.SpeedMultiplier;
                    if (fwdSpeed < reverseCap) fwdSpeed = reverseCap;
                }
            }
            if (throttle <= 0.01f && brake <= 0.01f && IsGrounded)
            {
                fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, Stats.coastDeceleration * dt);
            }
            if (fwdSpeed > CurrentMaxSpeed)
            {
                // Boost ended or entered off-road: bleed speed instead of snapping.
                float bleed = OverCapDeceleration * (Surface == SurfaceType.OffRoad && !ignoreSurfacePenalty ? 1.6f : 1f);
                fwdSpeed = Mathf.MoveTowards(fwdSpeed, CurrentMaxSpeed, bleed * dt);
            }

            // ---- Lateral grip
            float grip = Stats.GetGrip(Drift.IsDrifting) * Status.GripMultiplier * SurfaceGripFactor;
            if (!IsGrounded) grip = 0.4f;
            if (IsPlayer && SaveManager.Settings.drivingAssists && Mathf.Abs(input.Steer) < 0.05f && !Drift.IsDrifting) grip *= 1.35f;
            latSpeed *= Mathf.Exp(-grip * dt);
            if (Drift.IsDrifting && IsGrounded)
            {
                // Keep the slide alive: push outward proportionally to speed.
                float slide = -Drift.Direction * Mathf.Abs(fwdSpeed) * 0.28f;
                latSpeed = Mathf.MoveTowards(latSpeed, slide, 18f * dt);
            }

            // ---- Vertical
            if (pendingVerticalKick > 0.001f)
            {
                vertSpeed = Mathf.Max(vertSpeed, 0f) + pendingVerticalKick;
                pendingVerticalKick = 0f;
                IsGrounded = false;
            }

            vel = Forward * fwdSpeed + Right * latSpeed + Up * vertSpeed;
            vel += Vector3.down * Gravity * dt;
            if (IsGrounded) vel -= GroundNormal * GroundedDownforce * dt;

            Body.linearVelocity = vel;
            ForwardSpeed = fwdSpeed;
            LateralSpeed = latSpeed;
            EffectiveSteer = Drift.IsDrifting ? Mathf.Clamp(Drift.Direction * 0.6f + steer * 0.4f, -1f, 1f) : steer;

            TickSlipstream(dt);
        }

        private void UpdateFrame(Vector3 up)
        {
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, up);
            Rotation = tilt * Quaternion.Euler(0f, Yaw, 0f);
            Forward = Rotation * Vector3.forward;
            Right = Rotation * Vector3.right;
            Up = up;
        }

        private void ReadGround()
        {
            Vector3 origin = Body.position;
            RaycastHit hit;
            float dist = Radius + GroundCheckExtra;
            // Cast along the smoothed up so banked surfaces stay "grounded".
            bool grounded = Physics.Raycast(origin, -upSmoothed, out hit, dist, Layers.GroundMask, QueryTriggerInteraction.Ignore);
            if (!grounded)
            {
                grounded = Physics.Raycast(origin, Vector3.down, out hit, dist, Layers.GroundMask, QueryTriggerInteraction.Ignore);
            }
            IsGrounded = grounded;
            if (grounded)
            {
                GroundNormal = hit.normal;
                var surface = TrackSurface.Get(hit.collider);
                if (surface != null)
                {
                    Surface = surface.Type;
                    SurfaceSpeedFactor = surface.SpeedFactor;
                    SurfaceGripFactor = surface.GripFactor;
                }
                else
                {
                    Surface = SurfaceType.OffRoad;
                    SurfaceSpeedFactor = Stats.offRoadPenalty;
                    SurfaceGripFactor = 0.8f;
                }
                if (Surface == SurfaceType.OffRoad) SurfaceSpeedFactor = Mathf.Min(SurfaceSpeedFactor, Stats.offRoadPenalty);
            }
            else
            {
                Surface = SurfaceType.Road;
                SurfaceSpeedFactor = 1f;
                SurfaceGripFactor = 1f;
            }
        }

        private void TickSlipstream(float dt)
        {
            if (!IsGrounded || ForwardSpeed < Stats.maxSpeed * 0.6f)
            {
                SlipstreamTime = 0f;
                return;
            }
            RaycastHit hit;
            Vector3 origin = Body.position + Forward * (Radius + 0.1f);
            if (Physics.SphereCast(origin, 0.5f, Forward, out hit, SlipstreamDistance, Layers.KartMask, QueryTriggerInteraction.Ignore))
            {
                var other = hit.rigidbody != null ? hit.rigidbody.GetComponent<KartController>() : null;
                if (other != null && other != this && Vector3.Dot(other.Forward, Forward) > 0.8f)
                {
                    SlipstreamTime += dt;
                    if (SlipstreamTime > 1.0f && Time.time - lastSlipstreamBoost > 0.25f)
                    {
                        lastSlipstreamBoost = Time.time;
                        Boost.AddBoost(BoostSource.Slipstream, 1.07f, 0.35f, 1.2f);
                    }
                    return;
                }
            }
            SlipstreamTime = 0f;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            Orientation.rotation = Rotation;
        }

        // ------------------------------------------------------------------ Collisions

        private void OnCollisionEnter(Collision collision)
        {
            if (!initialized) return;
            var otherKart = collision.rigidbody != null ? collision.rigidbody.GetComponent<KartController>() : null;
            if (otherKart != null)
            {
                HandleKartCollision(collision, otherKart);
            }
            else
            {
                HandleWallCollision(collision);
            }
        }

        private void HandleKartCollision(Collision collision, KartController other)
        {
            Vector3 normal = collision.GetContact(0).normal; // points from other towards us
            Vector3 relVel = Velocity - other.Velocity;
            float closing = Mathf.Max(0f, -Vector3.Dot(relVel, normal));
            float myMass = Body.mass;
            float otherMass = other.Body.mass;
            float share = otherMass / (myMass + otherMass);
            float strength = Mathf.Clamp01(closing / 12f);

            // Extra arcade shove on top of the physics response. Armoured (mega boost) karts hit like trucks.
            float shove = closing * 0.35f * share * (other.Status.HasArmor ? 1.8f : 1f);
            Body.linearVelocity += normal * shove;

            if (Status.IsInvulnerable) return;
            if (other.Status.HasArmor && !Status.HasArmor && closing > 5f && !Status.HasShield)
            {
                Status.Apply(StatusEffect.SpinOut, 0.7f);
            }

            if (Time.time - LastKartHitTime > 0.25f && strength > 0.12f)
            {
                LastKartHitTime = Time.time;
                GameEvents.RaiseKartHit(this, KartHitKind.Kart, strength);
                OnKartCollision?.Invoke(collision, other);
                if (Drift.IsDrifting && strength > 0.5f) Drift.Cancel();
            }
        }

        private void HandleWallCollision(Collision collision)
        {
            var contact = collision.GetContact(0);
            Vector3 normal = contact.normal;
            // Ignore floor-ish contacts.
            if (Vector3.Dot(normal, Vector3.up) > 0.6f) return;

            Vector3 vel = Body.linearVelocity;
            float into = Mathf.Max(0f, -Vector3.Dot(collision.relativeVelocity * -1f, normal));
            float alignment = 1f - Mathf.Clamp01(Mathf.Abs(Vector3.Dot(normal, Forward))); // 1 = grazing, 0 = head-on
            float keep = Mathf.Lerp(0.42f, 0.93f, alignment);
            Vector3 horizontal = Vector3.ProjectOnPlane(vel, Up);
            Vector3 vertical = vel - horizontal;
            Body.linearVelocity = horizontal * keep + vertical;

            float strength = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(vel, normal)) / Mathf.Max(1f, Stats.maxSpeed));
            if (strength > 0.12f && Time.time - LastWallHitTime > 0.3f)
            {
                LastWallHitTime = Time.time;
                GameEvents.RaiseKartHit(this, KartHitKind.Wall, strength);
                OnWallCollision?.Invoke(collision, strength);
                if (Drift.IsDrifting && strength > 0.45f) Drift.Cancel();
            }
        }
    }
}
