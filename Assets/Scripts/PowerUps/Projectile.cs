using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Track;
using VortexKarts.Utils;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// Pooled projectile used by Bólido (straight, bouncing) and Misil Rastreador (homing).
    /// Moves kinematically with sphere casts so it never tunnels through walls at high speed.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        private const float HoverHeight = 0.55f;
        private const float HomingTurnRate = 150f;   // deg/s
        private const float OwnerImmunity = 0.35f;
        private const float BodyRadius = 0.35f;

        public KartController Owner { get; private set; }
        public KartController Target { get; private set; }
        public bool IsFlying { get; private set; }
        public Vector3 Direction { get; private set; } = Vector3.forward;
        public bool IsHoming { get; private set; }

        private PowerUpManager manager;
        private float speed;
        private float lifetime;
        private int bouncesLeft;
        private float launchTime;
        private Transform visual;
        private Renderer bodyRenderer;
        private ParticleSystem trail;
        private Color color;
        private int nodeHint = -1;

        public static GameObject CreatePooledInstance()
        {
            var go = new GameObject("Projectile");
            go.layer = Layers.Projectile;
            var p = go.AddComponent<Projectile>();
            p.visual = new GameObject("Visual").transform;
            p.visual.SetParent(go.transform, false);
            var body = PrimitiveFactory.Sphere("Body", p.visual, Vector3.zero, 0.7f, MaterialLibrary.Emissive(Color.white, Color.cyan, 2f));
            PrimitiveFactory.SetShadowCasting(body, false, false);
            p.bodyRenderer = body.GetComponent<Renderer>();
            var fin = PrimitiveFactory.Box("Fin", p.visual, new Vector3(0f, 0f, -0.35f), new Vector3(0.9f, 0.08f, 0.4f),
                MaterialLibrary.Lit(new Color(0.15f, 0.15f, 0.18f), 0.6f, 0.5f));
            PrimitiveFactory.SetShadowCasting(fin, false, false);
            p.trail = VfxFactory.CreateStream("Trail", go.transform, new Vector3(0f, 0f, -0.4f), Quaternion.Euler(0f, 180f, 0f),
                Color.cyan, 0.35f, 2f, 0.4f, 60f, 6f, 80);
            return go;
        }

        public void OnSpawned()
        {
            IsFlying = false;
        }

        public void OnDespawned()
        {
            IsFlying = false;
            Owner = null;
            Target = null;
            if (manager != null) manager.UnregisterProjectile(this);
        }

        public void Launch(PowerUpManager mgr, KartController owner, PowerUpData data, bool homing, KartController target)
        {
            manager = mgr;
            Owner = owner;
            Target = target;
            IsHoming = homing;
            speed = Mathf.Max(20f, data.magnitude) + Mathf.Max(0f, owner.ForwardSpeed) * 0.5f;
            lifetime = Mathf.Max(1f, data.duration);
            bouncesLeft = homing ? 0 : Mathf.RoundToInt(data.secondary);
            launchTime = Time.time;
            Direction = MathUtil.FlatNormalized(owner.Forward);
            transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
            nodeHint = -1;
            color = data.iconColor;
            bodyRenderer.sharedMaterial = MaterialLibrary.Emissive(color * 0.6f, color, 2.5f);
            VfxFactory.SetColor(trail, color);
            IsFlying = true;
        }

        private void FixedUpdate()
        {
            if (!IsFlying) return;
            float dt = Time.fixedDeltaTime;
            lifetime -= dt;
            if (lifetime <= 0f)
            {
                Explode(false);
                return;
            }

            Steer(dt);

            // Follow the ground height so it does not fly off ramps or sink into slopes.
            RaycastHit ground;
            Vector3 pos = transform.position;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out ground, 6f, Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                float targetY = ground.point.y + HoverHeight;
                pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-12f * dt));
            }

            float step = speed * dt;
            RaycastHit hit;
            int mask = Layers.SolidMask | Layers.KartMask | (1 << Layers.Wall);
            if (Physics.SphereCast(pos, BodyRadius, Direction, out hit, step + BodyRadius, mask, QueryTriggerInteraction.Ignore))
            {
                var kart = hit.rigidbody != null ? hit.rigidbody.GetComponent<KartController>() : null;
                if (kart != null)
                {
                    if (kart == Owner && Time.time - launchTime < OwnerImmunity)
                    {
                        // Pass through the launcher right after firing.
                    }
                    else
                    {
                        manager.HitKart(kart, KartHitKind.Projectile, 1f, transform.position, Owner);
                        Explode(true);
                        return;
                    }
                }
                else if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
                {
                    if (bouncesLeft > 0)
                    {
                        bouncesLeft--;
                        Vector3 n = MathUtil.FlatNormalized(hit.normal);
                        Direction = Vector3.Reflect(Direction, n).normalized;
                        Direction = MathUtil.FlatNormalized(Direction);
                        pos = hit.point + n * (BodyRadius + 0.05f);
                        pos.y = transform.position.y;
                        transform.position = pos;
                        transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
                        if (manager != null) manager.Burst(hit.point, color, 12);
                        return;
                    }
                    Explode(false);
                    return;
                }
            }

            pos += Direction * step;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
            visual.Rotate(0f, 0f, 720f * dt, Space.Self);
        }

        private void Steer(float dt)
        {
            if (!IsHoming) return;
            Vector3 aim;
            if (Target != null && !Target.HasFinished)
            {
                aim = Target.Position + Target.Velocity * 0.15f;
                // If the target is far or around a bend, route along the track first.
                var track = TrackRuntime.Instance;
                if (track != null)
                {
                    Vector3 toTarget = aim - transform.position;
                    var node = track.GetClosestNode(transform.position, nodeHint);
                    if (node != null)
                    {
                        nodeHint = node.Index;
                        var ahead = track.GetNode(node.Index + 5);
                        float dist = toTarget.magnitude;
                        bool lineOfSight = !Physics.Raycast(transform.position, toTarget.normalized, dist, Layers.SolidMask | (1 << Layers.Wall), QueryTriggerInteraction.Ignore);
                        if (!lineOfSight || dist > 60f) aim = ahead.Position + Vector3.up * HoverHeight;
                    }
                }
            }
            else
            {
                return;
            }
            Vector3 desired = MathUtil.FlatNormalized(aim - transform.position);
            float maxTurn = HomingTurnRate * dt;
            float angle = Vector3.SignedAngle(Direction, desired, Vector3.up);
            angle = Mathf.Clamp(angle, -maxTurn, maxTurn);
            Direction = Quaternion.Euler(0f, angle, 0f) * Direction;
        }

        public void Explode(bool hitKart)
        {
            if (!IsFlying) return;
            IsFlying = false;
            if (manager != null) manager.Burst(transform.position, color, hitKart ? 40 : 24);
            if (ObjectPoolManager.HasInstance) ObjectPoolManager.Instance.Despawn(gameObject);
        }
    }
}
