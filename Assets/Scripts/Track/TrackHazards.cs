using UnityEngine;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.Track
{
    /// <summary>Barrier sliding across the road. Karts bounce off it (wall) and spin out if they hit it fast.</summary>
    public class MovingBarrier : MonoBehaviour
    {
        public float Range = 6f;
        public float Speed = 1f;
        private Vector3 startLocal;
        private float phase;
        private Rigidbody body;

        public static MovingBarrier Create(Transform parent, TrackNode node, float lateral, float size, float speed, float range, Color color)
        {
            var go = new GameObject("MovingBarrier");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(node.PointAtLateral(lateral) + node.Up * 0.75f, Quaternion.LookRotation(node.Forward, node.Up));
            go.layer = Layers.Wall;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size, 1.5f, 0.6f);
            var mesh = PrimitiveFactory.Box("Beam", go.transform, Vector3.zero, new Vector3(size, 0.5f, 0.5f),
                MaterialLibrary.Emissive(color * 0.6f, color, 1.5f));
            PrimitiveFactory.Box("Post", go.transform, new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.7f, 0.3f),
                MaterialLibrary.Lit(new Color(0.2f, 0.2f, 0.22f)));
            for (int i = 0; i < 3; i++)
            {
                float x = -size * 0.35f + i * size * 0.35f;
                PrimitiveFactory.Box("Stripe", go.transform, new Vector3(x, 0.26f, 0f), new Vector3(0.3f, 0.04f, 0.52f),
                    MaterialLibrary.Emissive(Color.black, Color.white, 1.2f));
            }
            var mb = go.AddComponent<MovingBarrier>();
            mb.Range = range;
            mb.Speed = speed;
            mb.body = rb;
            mb.startLocal = go.transform.position;
            mb.phase = Random.value * 6.28f;
            return mb;
        }

        private void FixedUpdate()
        {
            phase += Time.fixedDeltaTime * Speed;
            float offset = Mathf.Sin(phase) * Range * 0.5f;
            Vector3 target = startLocal + transform.right * offset;
            if (body != null) body.MovePosition(target);
            else transform.position = target;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var kart = collision.rigidbody != null ? collision.rigidbody.GetComponent<KartController>() : null;
            if (kart == null) return;
            float closing = collision.relativeVelocity.magnitude;
            if (closing > 9f) kart.Status.TryHit(KartHitKind.Hazard, Mathf.Clamp01(closing / 25f), transform.position);
            else kart.ScaleSpeed(0.6f);
        }
    }

    /// <summary>Turbine that pushes karts sideways while they drive through its volume.</summary>
    public class FanZone : MonoBehaviour
    {
        public Vector3 PushDirection = Vector3.right;
        public float Strength = 6f;
        private Transform blades;

        public static FanZone Create(Transform parent, TrackNode node, float lateral, float size, float strength, float length, Color color)
        {
            var go = new GameObject("FanZone");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(node.PointAtLateral(lateral) + node.Up * 1.5f, Quaternion.LookRotation(node.Forward, node.Up));
            go.layer = 2;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(node.Width + 2f, 4f, length);

            // Direction alternates so the player has to correct both ways along a fan corridor.
            bool pushRight = Mathf.Sin(node.Distance * 0.05f) >= 0f;
            var fan = go.AddComponent<FanZone>();
            fan.PushDirection = pushRight ? Vector3.right : Vector3.left;
            fan.Strength = strength;

            // Turbine housing on the side the wind comes from.
            float side = pushRight ? -1f : 1f;
            Vector3 housingPos = new Vector3(side * (node.Width * 0.5f + 2.5f), 1.2f, 0f);
            var housing = PrimitiveFactory.Cylinder("Turbine", go.transform, housingPos, size * 0.6f, 1.6f,
                MaterialLibrary.Lit(new Color(0.75f, 0.78f, 0.85f), 0.6f, 0.5f), false, Quaternion.Euler(0f, 0f, 90f));
            fan.blades = new GameObject("Blades").transform;
            fan.blades.SetParent(go.transform, false);
            fan.blades.localPosition = housingPos + new Vector3(-side * 0.85f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                PrimitiveFactory.Box("Blade", fan.blades, Vector3.zero, new Vector3(0.1f, size * 0.5f, 0.35f),
                    MaterialLibrary.Emissive(color * 0.4f, color, 1f), false, Quaternion.Euler(i * 45f, 0f, 0f));
            }
            PrimitiveFactory.SetShadowCasting(housing, true, true);
            return fan;
        }

        private void Update()
        {
            if (blades != null) blades.Rotate(Time.deltaTime * 540f, 0f, 0f, Space.Self);
        }

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var kart = rb.GetComponent<KartController>();
            if (kart == null || kart.Status.HasArmor) return;
            Vector3 push = transform.TransformDirection(PushDirection) * Strength * Time.deltaTime;
            kart.AddVelocity(push);
        }
    }
}
