using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Race;
using VortexKarts.Track;
using VortexKarts.Utils;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// Scene-scoped hub for power-ups: kind → behaviour registry, pools for projectiles/hazards, pickup
    /// boxes, hit resolution and the hazard registry the AI uses to steer around mines and slime.
    /// </summary>
    public class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        public const string PoolProjectile = "Projectile";
        public const string PoolMine = "Mine";
        public const string PoolOilSlick = "OilSlick";
        public const string PoolBurst = "BurstVFX";

        private readonly Dictionary<PowerUpKind, PowerUpBase> behaviours = new Dictionary<PowerUpKind, PowerUpBase>();
        private readonly List<ITrackHazard> hazards = new List<ITrackHazard>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<PowerUpBox> boxes = new List<PowerUpBox>();
        private System.Random rng = new System.Random();
        private TrackRuntime track;

        public IReadOnlyList<ITrackHazard> Hazards => hazards;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IReadOnlyList<PowerUpBox> Boxes => boxes;

        private void Awake()
        {
            Instance = this;
            Register(new TurboPowerUp());
            Register(new TripleTurboPowerUp());
            Register(new MegaBoostPowerUp());
            Register(new HomingMissilePowerUp());
            Register(new StraightShotPowerUp());
            Register(new TripleShotPowerUp());
            Register(new MinePowerUp());
            Register(new OilSlickPowerUp());
            Register(new ShieldPowerUp());
            Register(new EmpPulsePowerUp());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Register(PowerUpBase b)
        {
            behaviours[b.Kind] = b;
        }

        public PowerUpBase GetBehaviour(PowerUpKind kind)
        {
            PowerUpBase b;
            return behaviours.TryGetValue(kind, out b) ? b : null;
        }

        public void Initialize(TrackRuntime trackRuntime)
        {
            track = trackRuntime;
            var pools = ObjectPoolManager.Instance;
            pools.RegisterPool(PoolProjectile, Projectile.CreatePooledInstance, 12);
            pools.RegisterPool(PoolMine, MineHazard.CreatePooledInstance, 8);
            pools.RegisterPool(PoolOilSlick, OilSlickHazard.CreatePooledInstance, 8);
            pools.RegisterPool(PoolBurst, CreateBurstVfx, 6);

            var boxRoot = new GameObject("PowerUpBoxes").transform;
            boxRoot.SetParent(transform, false);
            for (int i = 0; i < track.PowerUpBoxPositions.Count; i++)
            {
                boxes.Add(PowerUpBox.Create(this, boxRoot, track.PowerUpBoxPositions[i]));
            }
        }

        private static GameObject CreateBurstVfx()
        {
            var go = new GameObject("BurstVFX");
            var ps = VfxFactory.CreateBurst("Burst", go.transform, Vector3.zero, Color.white, 0.6f, 9f, 0.6f, 80, 0.5f);
            var auto = go.AddComponent<TimedDespawn>();
            auto.Lifetime = 1.2f;
            return go;
        }

        public void Burst(Vector3 position, Color color, int count)
        {
            if (!SaveManager.Settings.effects) count = Mathf.Max(6, count / 3);
            var go = ObjectPoolManager.Instance.Spawn(PoolBurst, position, Quaternion.identity);
            if (go == null) return;
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                VfxFactory.SetColor(ps, color);
                ps.Emit(count);
            }
        }

        // ------------------------------------------------------------------ Rolling & activation

        public PowerUpData Roll(KartController kart)
        {
            int total = RaceManager.Instance != null ? RaceManager.Instance.Karts.Count : 8;
            var tracker = kart.GetComponent<RaceProgressTracker>();
            int position = tracker != null && tracker.Position > 0 ? tracker.Position : total;
            var inventory = kart.GetComponent<PowerUpInventory>();
            var exclude = inventory != null && inventory.Current != null ? inventory.Current.kind : PowerUpKind.None;
            return PositionWeightedPowerUpTable.Roll(GameDatabase.PowerUps, position, total, rng, !kart.IsPlayer, exclude);
        }

        public bool Activate(KartController user, PowerUpData data)
        {
            if (user == null || data == null) return false;
            var behaviour = GetBehaviour(data.kind);
            if (behaviour == null)
            {
                Debug.LogWarning("[PowerUps] No behaviour registered for " + data.kind);
                return false;
            }
            return behaviour.Activate(user, data, this);
        }

        // ------------------------------------------------------------------ Spawning

        public KartController FindTargetAhead(KartController user)
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            var tracker = user.GetComponent<RaceProgressTracker>();
            if (tracker == null) return null;
            int myPos = tracker.Position;
            if (myPos <= 1) return null;
            var aheadTracker = rm.Positions.GetAt(myPos - 1);
            if (aheadTracker == null) return null;
            var target = aheadTracker.GetComponent<KartController>();
            if (target == null || target == user || target.HasFinished) return null;
            return target;
        }

        public Projectile SpawnProjectile(KartController owner, PowerUpData data, bool homing, KartController target)
        {
            Vector3 pos = owner.Position + owner.Forward * 2.2f + Vector3.up * 0.1f;
            var go = ObjectPoolManager.Instance.Spawn(PoolProjectile, pos, Quaternion.LookRotation(owner.Forward, Vector3.up));
            if (go == null) return null;
            var p = go.GetComponent<Projectile>();
            p.Launch(this, owner, data, homing, target);
            projectiles.Add(p);
            return p;
        }

        public void SpawnMine(KartController owner, PowerUpData data)
        {
            Vector3 pos = owner.Position - owner.Forward * 3.2f - Vector3.up * (KartController.Radius - 0.15f);
            var go = ObjectPoolManager.Instance.Spawn(PoolMine, pos, Quaternion.Euler(0f, owner.Yaw, 0f));
            if (go == null) return;
            go.GetComponent<MineHazard>().Arm(this, owner, data);
        }

        public void SpawnOilSlick(KartController owner, PowerUpData data)
        {
            Vector3 pos = owner.Position - owner.Forward * 3.5f - Vector3.up * (KartController.Radius - 0.08f);
            var go = ObjectPoolManager.Instance.Spawn(PoolOilSlick, pos, Quaternion.Euler(0f, owner.Yaw, 0f));
            if (go == null) return;
            go.GetComponent<OilSlickHazard>().Arm(this, owner, data);
        }

        public void EmpBurst(KartController user, PowerUpData data)
        {
            float radius = Mathf.Max(5f, data.magnitude);
            var rm = RaceManager.Instance;
            Burst(user.Position, new Color(0.7f, 0.4f, 1f), 60);
            var pulse = EmpPulseVisual.Create(user.Position, radius, new Color(0.7f, 0.4f, 1f));
            if (rm != null)
            {
                for (int i = 0; i < rm.Karts.Count; i++)
                {
                    var k = rm.Karts[i];
                    if (k == user) continue;
                    if ((k.Position - user.Position).sqrMagnitude <= radius * radius)
                    {
                        HitKart(k, KartHitKind.Emp, 1f, user.Position, user);
                    }
                }
            }
            // The pulse also fries projectiles in range.
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                if (p == null) { projectiles.RemoveAt(i); continue; }
                if ((p.transform.position - user.Position).sqrMagnitude <= radius * radius) p.Explode(false);
            }
        }

        // ------------------------------------------------------------------ Hits & registries

        public bool HitKart(KartController target, KartHitKind kind, float strength, Vector3 from, KartController attacker)
        {
            if (target == null) return false;
            bool applied = target.Status.TryHit(kind, strength, from);
            Color c = kind == KartHitKind.Emp ? new Color(0.7f, 0.4f, 1f) : new Color(1f, 0.6f, 0.2f);
            Burst(target.Position + Vector3.up * 0.4f, applied ? c : new Color(0.5f, 0.8f, 1f), applied ? 30 : 20);
            return applied;
        }

        public void RegisterHazard(ITrackHazard h)
        {
            if (!hazards.Contains(h)) hazards.Add(h);
        }

        public void UnregisterHazard(ITrackHazard h)
        {
            hazards.Remove(h);
        }

        public void UnregisterProjectile(Projectile p)
        {
            projectiles.Remove(p);
        }

        /// <summary>True when a projectile is heading for this kart and within the distance.</summary>
        public bool IsProjectileThreatening(KartController kart, float within)
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                if (p == null || !p.IsFlying || p.Owner == kart) continue;
                Vector3 to = kart.Position - p.transform.position;
                if (to.sqrMagnitude > within * within) continue;
                if (p.Target == kart) return true;
                if (Vector3.Dot(to.normalized, p.Direction) > 0.9f) return true;
            }
            return false;
        }
    }

    /// <summary>Returns a pooled object to its pool after a delay.</summary>
    public class TimedDespawn : MonoBehaviour, IPoolable
    {
        public float Lifetime = 1f;
        private float timer;

        public void OnSpawned()
        {
            timer = Lifetime;
        }

        public void OnDespawned() { }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f && ObjectPoolManager.HasInstance) ObjectPoolManager.Instance.Despawn(gameObject);
        }
    }

    /// <summary>Expanding translucent sphere for the EMP pulse. Self-destroys.</summary>
    public class EmpPulseVisual : MonoBehaviour
    {
        private float radius;
        private float t;
        private Material mat;
        private Color color;

        public static EmpPulseVisual Create(Vector3 position, float radius, Color color)
        {
            var go = PrimitiveFactory.Sphere("EmpPulse", null, Vector3.zero, 1f, MaterialLibrary.UnlitTransparent(new Color(color.r, color.g, color.b, 0.35f)));
            go.transform.position = position;
            PrimitiveFactory.SetShadowCasting(go, false, false);
            var v = go.AddComponent<EmpPulseVisual>();
            v.radius = radius;
            v.color = color;
            v.mat = go.GetComponent<Renderer>().material;
            return v;
        }

        private void Update()
        {
            t += Time.deltaTime * 2.2f;
            float s = Mathf.Lerp(1f, radius * 2f, Mathf.Clamp01(t));
            transform.localScale = Vector3.one * s;
            var c = new Color(color.r, color.g, color.b, Mathf.Lerp(0.35f, 0f, Mathf.Clamp01(t)));
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else mat.color = c;
            }
            if (t >= 1f)
            {
                if (mat != null) Destroy(mat);
                Destroy(gameObject);
            }
        }
    }
}
