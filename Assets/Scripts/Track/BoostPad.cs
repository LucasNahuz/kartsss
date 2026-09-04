using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.Track
{
    /// <summary>Glowing pad on the road that gives a boost to any kart driving over it.</summary>
    public class BoostPad : MonoBehaviour
    {
        public float BoostMultiplier = 1.28f;
        public float BoostDuration = 1.3f;

        private readonly Dictionary<KartController, float> lastTrigger = new Dictionary<KartController, float>();
        private Material glowMaterial;
        private Color glowColor;
        private Renderer[] stripes = new Renderer[0];
        private float phase;

        public static BoostPad Create(Transform parent, TrackNode node, float lateral, float length, float width, float multiplier,
            float duration, Color color)
        {
            var go = new GameObject("BoostPad");
            go.transform.SetParent(parent, false);
            Vector3 pos = node.PointAtLateral(lateral) + node.Up * 0.06f;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(node.Forward, node.Up));
            go.layer = 2;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, 2.5f, length);
            box.center = new Vector3(0f, 1.2f, 0f);

            var pad = go.AddComponent<BoostPad>();
            pad.BoostMultiplier = multiplier;
            pad.BoostDuration = duration;
            pad.glowColor = color;
            pad.glowMaterial = MaterialLibrary.Emissive(color * 0.5f, color, 2.5f);

            var basePlate = PrimitiveFactory.Box("Plate", go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.04f, length),
                MaterialLibrary.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0.3f));
            PrimitiveFactory.SetShadowCasting(basePlate, false, true);

            int stripeCount = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f));
            var list = new List<Renderer>();
            for (int i = 0; i < stripeCount; i++)
            {
                float z = -length * 0.5f + (i + 0.5f) * (length / stripeCount);
                var stripe = PrimitiveFactory.Box("Stripe", go.transform, new Vector3(0f, 0.05f, z),
                    new Vector3(width * 0.8f, 0.03f, length / stripeCount * 0.55f), pad.glowMaterial);
                PrimitiveFactory.SetShadowCasting(stripe, false, false);
                list.Add(stripe.GetComponent<Renderer>());
            }
            pad.stripes = list.ToArray();
            return pad;
        }

        private void Update()
        {
            // Animated chevrons: brightness runs along the pad.
            phase += Time.deltaTime * 3f;
            for (int i = 0; i < stripes.Length; i++)
            {
                float f = 0.5f + 0.5f * Mathf.Sin(phase - i * 0.9f);
                stripes[i].transform.localScale = new Vector3(stripes[i].transform.localScale.x, 0.03f + f * 0.03f, stripes[i].transform.localScale.z);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var kart = rb.GetComponent<KartController>();
            if (kart == null) return;
            float last;
            if (lastTrigger.TryGetValue(kart, out last) && Time.time - last < 1f) return;
            lastTrigger[kart] = Time.time;
            kart.Boost.AddBoost(BoostSource.Pad, BoostMultiplier, BoostDuration, 2f);
        }
    }
}
