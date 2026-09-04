using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.PowerUps
{
    /// <summary>Something on the road the AI should steer around.</summary>
    public interface ITrackHazard
    {
        Vector3 Position { get; }
        float Radius { get; }
        bool IsActive { get; }
        KartController Owner { get; }
    }

    /// <summary>Mina Magnética: dropped behind the kart, arms after a moment, spins out whoever touches it.</summary>
    public class MineHazard : MonoBehaviour, IPoolable, ITrackHazard
    {
        private const float TriggerRadius = 1.5f;

        public Vector3 Position => transform.position;
        public float Radius => 2.2f;
        public bool IsActive { get; private set; }
        public KartController Owner { get; private set; }

        private PowerUpManager manager;
        private float armedAt;
        private float ownerImmuneUntil;
        private float dieAt;
        private float strength;
        private Renderer light;
        private Material lightOn, lightOff;
        private readonly Collider[] hits = new Collider[8];

        public static GameObject CreatePooledInstance()
        {
            var go = new GameObject("Mine");
            go.layer = Layers.Hazard;
            var m = go.AddComponent<MineHazard>();
            var body = PrimitiveFactory.Cylinder("Body", go.transform, new Vector3(0f, 0.2f, 0f), 1.3f, 0.4f,
                MaterialLibrary.Lit(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.6f));
            m.lightOn = MaterialLibrary.Emissive(Color.black, new Color(1f, 0.85f, 0.2f), 3f);
            m.lightOff = MaterialLibrary.Lit(new Color(0.4f, 0.35f, 0.1f));
            var lamp = PrimitiveFactory.Sphere("Lamp", go.transform, new Vector3(0f, 0.5f, 0f), 0.4f, m.lightOn);
            m.light = lamp.GetComponent<Renderer>();
            PrimitiveFactory.SetShadowCasting(lamp, false, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                PrimitiveFactory.Box("Spike", go.transform, Quaternion.Euler(0f, a, 0f) * new Vector3(0.75f, 0.2f, 0f),
                    new Vector3(0.4f, 0.15f, 0.15f), MaterialLibrary.Lit(new Color(0.9f, 0.85f, 0.2f)), false, Quaternion.Euler(0f, a, 0f));
            }
            return go;
        }

        public void OnSpawned()
        {
            IsActive = false;
        }

        public void OnDespawned()
        {
            IsActive = false;
            if (manager != null) manager.UnregisterHazard(this);
            Owner = null;
        }

        public void Arm(PowerUpManager mgr, KartController owner, PowerUpData data)
        {
            manager = mgr;
            Owner = owner;
            armedAt = Time.time + 0.6f;
            ownerImmuneUntil = Time.time + 1.6f;
            dieAt = Time.time + Mathf.Max(5f, data.duration);
            strength = Mathf.Max(0.5f, data.magnitude);
            IsActive = true;
            manager.RegisterHazard(this);
        }

        private void FixedUpdate()
        {
            if (!IsActive) return;
            if (Time.time >= dieAt)
            {
                Detonate(null);
                return;
            }
            bool blink = Mathf.Repeat(Time.time, 0.6f) < 0.3f;
            light.sharedMaterial = blink ? lightOn : lightOff;
            if (Time.time < armedAt) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.4f, TriggerRadius, hits, Layers.KartMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                var kart = rb != null ? rb.GetComponent<KartController>() : null;
                if (kart == null) continue;
                if (kart == Owner && Time.time < ownerImmuneUntil) continue;
                Detonate(kart);
                return;
            }
        }

        private void Detonate(KartController victim)
        {
            if (!IsActive) return;
            IsActive = false;
            if (victim != null) manager.HitKart(victim, KartHitKind.Mine, strength, transform.position, Owner);
            manager.Burst(transform.position + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.3f), 45);
            if (ObjectPoolManager.HasInstance) ObjectPoolManager.Instance.Despawn(gameObject);
        }
    }

    /// <summary>Mancha Slime: slippery puddle. Karts driving through lose grip for a few seconds.</summary>
    public class OilSlickHazard : MonoBehaviour, IPoolable, ITrackHazard
    {
        public Vector3 Position => transform.position;
        public float Radius => radius;
        public bool IsActive { get; private set; }
        public KartController Owner { get; private set; }

        private PowerUpManager manager;
        private float radius = 3f;
        private float dieAt;
        private float ownerImmuneUntil;
        private float strength;
        private Transform disc;
        private readonly Dictionary<KartController, float> cooldown = new Dictionary<KartController, float>();
        private readonly Collider[] hits = new Collider[8];

        public static GameObject CreatePooledInstance()
        {
            var go = new GameObject("OilSlick");
            go.layer = Layers.Hazard;
            var o = go.AddComponent<OilSlickHazard>();
            var d = PrimitiveFactory.Cylinder("Disc", go.transform, new Vector3(0f, 0.03f, 0f), 1f, 0.06f,
                MaterialLibrary.Emissive(new Color(0.3f, 0.8f, 0.2f), new Color(0.5f, 1f, 0.3f), 0.8f));
            PrimitiveFactory.SetShadowCasting(d, false, false);
            o.disc = d.transform;
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var blob = PrimitiveFactory.Sphere("Blob", go.transform, new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), 0.35f,
                    MaterialLibrary.Emissive(new Color(0.3f, 0.8f, 0.2f), new Color(0.5f, 1f, 0.3f), 1.2f));
                PrimitiveFactory.SetShadowCasting(blob, false, false);
            }
            return go;
        }

        public void OnSpawned()
        {
            IsActive = false;
            cooldown.Clear();
        }

        public void OnDespawned()
        {
            IsActive = false;
            if (manager != null) manager.UnregisterHazard(this);
            Owner = null;
        }

        public void Arm(PowerUpManager mgr, KartController owner, PowerUpData data)
        {
            manager = mgr;
            Owner = owner;
            radius = Mathf.Max(2f, data.secondary);
            dieAt = Time.time + Mathf.Max(4f, data.duration);
            ownerImmuneUntil = Time.time + 1.5f;
            strength = Mathf.Max(0.4f, data.magnitude / 2.5f);
            disc.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            IsActive = true;
            manager.RegisterHazard(this);
        }

        private void FixedUpdate()
        {
            if (!IsActive) return;
            if (Time.time >= dieAt)
            {
                IsActive = false;
                if (ObjectPoolManager.HasInstance) ObjectPoolManager.Instance.Despawn(gameObject);
                return;
            }
            // Shrink during the last seconds so players can read that it is fading.
            float remaining = dieAt - Time.time;
            float scale = remaining < 2f ? Mathf.Lerp(0.2f, 1f, remaining / 2f) : 1f;
            disc.localScale = new Vector3(radius * 2f * scale, 0.03f, radius * 2f * scale);

            int count = Physics.OverlapSphereNonAlloc(transform.position, radius * scale, hits, Layers.KartMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                var kart = rb != null ? rb.GetComponent<KartController>() : null;
                if (kart == null || !kart.IsGrounded) continue;
                if (kart == Owner && Time.time < ownerImmuneUntil) continue;
                float last;
                if (cooldown.TryGetValue(kart, out last) && Time.time - last < 2f) continue;
                cooldown[kart] = Time.time;
                manager.HitKart(kart, KartHitKind.OilSlick, strength, transform.position, Owner);
            }
        }
    }
}
