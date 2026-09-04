using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Utils;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Cosmetic motion layered on top of the physics: chassis lean, suspension bob, wheel spin and steer,
    /// steering-wheel rotation and the player's visual-only spin-out.
    /// </summary>
    public class KartVisuals : MonoBehaviour
    {
        public Transform Chassis;
        public Transform[] FrontWheelPivots = new Transform[0];
        public Transform[] WheelSpinners = new Transform[0];
        public Transform SteeringWheel;
        public Quaternion SteeringWheelBaseRotation = Quaternion.identity;
        public PilotRig Pilot;

        private KartController kart;
        private float leanRoll, leanPitch, bob, bobVelocity;
        private float wheelSpin;
        private float chassisSpin;
        private float steerVisual;
        private Vector3 lastVelocity;
        private float tiltIntensity = 1f;

        private const float WheelRadius = 0.26f;
        private const float MaxSteerWheelAngle = 110f;
        private const float MaxFrontWheelAngle = 28f;

        private void Awake()
        {
            kart = GetComponent<KartController>();
            GameEvents.OnSettingsApplied += OnSettings;
            GameEvents.OnKartLanded += OnLanded;
            GameEvents.OnKartHit += OnHit;
            OnSettings(SaveManager.Settings);
        }

        private void OnDestroy()
        {
            GameEvents.OnSettingsApplied -= OnSettings;
            GameEvents.OnKartLanded -= OnLanded;
            GameEvents.OnKartHit -= OnHit;
        }

        private void OnSettings(GameSettings s)
        {
            // For the player the chassis lean is what the VR seat follows only partially (see VRComfortManager);
            // the visual mesh itself can lean fully because the head does not move with it.
            tiltIntensity = 1f;
        }

        private void OnLanded(KartController k, float airTime)
        {
            if (k != kart) return;
            bobVelocity -= Mathf.Clamp(airTime, 0.2f, 1.2f) * 0.9f;
        }

        private void OnHit(KartController k, KartHitKind kind, float strength)
        {
            if (k != kart) return;
            if (Pilot != null) Pilot.Jolt(strength);
            bobVelocity -= strength * 0.5f;
        }

        private void LateUpdate()
        {
            if (kart == null || kart.Stats == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Acceleration estimate for lean.
            Vector3 vel = kart.Velocity;
            Vector3 accel = (vel - lastVelocity) / Mathf.Max(dt, 0.0001f);
            lastVelocity = vel;
            float lateralAccel = Vector3.Dot(accel, kart.Right);
            float forwardAccel = Vector3.Dot(accel, kart.Forward);

            steerVisual = MathUtil.Damp(steerVisual, kart.EffectiveSteer, 10f, dt);

            float targetRoll = Mathf.Clamp(-lateralAccel * 0.25f - steerVisual * 3f, -9f, 9f);
            if (kart.Drift.IsDrifting) targetRoll += -kart.Drift.Direction * 4f;
            float targetPitch = Mathf.Clamp(-forwardAccel * 0.28f, -5f, 5f);
            if (!kart.IsGrounded) targetPitch = Mathf.Clamp(-Vector3.Dot(vel, Vector3.up) * 0.6f, -10f, 10f);
            leanRoll = MathUtil.Damp(leanRoll, targetRoll * tiltIntensity, 7f, dt);
            leanPitch = MathUtil.Damp(leanPitch, targetPitch * tiltIntensity, 7f, dt);

            // Suspension bob (spring).
            float spring = 90f, damping = 9f;
            bobVelocity += (-bob * spring - bobVelocity * damping) * dt;
            bob += bobVelocity * dt;
            bob = Mathf.Clamp(bob, -0.12f, 0.08f);

            // Player-only visual spin-out.
            if (kart.IsPlayer && kart.Status.IsSpinning)
            {
                chassisSpin += kart.Status.SpinYawRate * dt;
            }
            else if (Mathf.Abs(chassisSpin) > 0.01f)
            {
                chassisSpin = MathUtil.WrapAngle180(chassisSpin);
                chassisSpin = MathUtil.Damp(chassisSpin, 0f, 10f, dt);
            }

            if (Chassis != null)
            {
                Chassis.localRotation = Quaternion.Euler(leanPitch, chassisSpin, leanRoll);
                Chassis.localPosition = new Vector3(0f, bob, 0f);
            }

            // Wheels.
            float distance = kart.ForwardSpeed * dt;
            wheelSpin += distance / WheelRadius * Mathf.Rad2Deg;
            wheelSpin %= 360f;
            for (int i = 0; i < WheelSpinners.Length; i++)
            {
                if (WheelSpinners[i] != null) WheelSpinners[i].localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
            }
            float frontAngle = steerVisual * MaxFrontWheelAngle;
            for (int i = 0; i < FrontWheelPivots.Length; i++)
            {
                if (FrontWheelPivots[i] != null) FrontWheelPivots[i].localRotation = Quaternion.Euler(0f, frontAngle, 0f);
            }

            if (SteeringWheel != null)
            {
                SteeringWheel.localRotation = SteeringWheelBaseRotation * Quaternion.Euler(0f, steerVisual * MaxSteerWheelAngle, 0f);
            }
        }
    }
}
