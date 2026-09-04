using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Utils;

namespace VortexKarts.Track
{
    /// <summary>
    /// Theme-specific greybox dressing: skyline blocks and holograms for Metro Neón, rock formations and
    /// supports for Cañón Solar, floating platforms, rings and clouds for Sky Lab. Deterministic per track.
    /// </summary>
    public static class TrackDecorator
    {
        public static void Decorate(TrackData data, TrackRuntime runtime, Transform parent)
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(data.id.GetHashCode());
            switch (data.theme)
            {
                case TrackTheme.NeonMetro: DecorateNeon(data, runtime, root, rng); break;
                case TrackTheme.SolarCanyon: DecorateCanyon(data, runtime, root, rng); break;
                case TrackTheme.SkyLab: DecorateSkyLab(data, runtime, root, rng); break;
            }
            MarkStatic(root.gameObject);
        }

        private static void MarkStatic(GameObject go)
        {
            go.isStatic = true;
            for (int i = 0; i < go.transform.childCount; i++) MarkStatic(go.transform.GetChild(i).gameObject);
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>True when the world point is far enough from every node of the track (avoid props on the road).</summary>
        private static bool IsClearOfRoad(TrackRuntime runtime, Vector3 p, float clearance)
        {
            var nodes = runtime.Nodes;
            float c2 = clearance * clearance;
            for (int i = 0; i < nodes.Count; i += 2)
            {
                Vector3 d = nodes[i].Position - p;
                d.y = 0f;
                if (d.sqrMagnitude < (nodes[i].Width * 0.5f + clearance) * (nodes[i].Width * 0.5f + clearance)) return false;
            }
            for (int s = 0; s < runtime.ShortcutNodes.Count; s++)
            {
                var list = runtime.ShortcutNodes[s];
                for (int i = 0; i < list.Count; i += 2)
                {
                    Vector3 d = list[i].Position - p;
                    d.y = 0f;
                    if (d.sqrMagnitude < c2 + 16f) return false;
                }
            }
            return true;
        }

        private static void AddSupports(TrackRuntime runtime, Transform root, Material mat, float groundY, float minHeight, float every)
        {
            var nodes = runtime.Nodes;
            float next = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Distance < next || n.IsGap) continue;
                next = n.Distance + every;
                float h = n.Position.y - groundY;
                if (h < minHeight) continue;
                Vector3 center = n.Position - Vector3.up * (h * 0.5f + 0.4f);
                PrimitiveFactory.Box("Support", root, center, new Vector3(n.Width * 0.35f, h, 1.6f), mat, false,
                    Quaternion.LookRotation(MathUtil.FlatNormalized(n.Forward), Vector3.up));
            }
        }

        // ------------------------------------------------------------------ Neon Metro

        private static void DecorateNeon(TrackData data, TrackRuntime runtime, Transform root, System.Random rng)
        {
            var nodes = runtime.Nodes;
            var buildingMats = new[]
            {
                MaterialLibrary.Lit(new Color(0.08f, 0.08f, 0.12f), 0.4f, 0.2f),
                MaterialLibrary.Lit(new Color(0.12f, 0.1f, 0.18f), 0.5f, 0.3f),
                MaterialLibrary.Lit(new Color(0.06f, 0.09f, 0.12f), 0.3f, 0.1f)
            };
            var neonMats = new[]
            {
                MaterialLibrary.Emissive(Color.black, new Color(0.1f, 0.9f, 1f), 2.5f),
                MaterialLibrary.Emissive(Color.black, new Color(1f, 0.2f, 0.8f), 2.5f),
                MaterialLibrary.Emissive(Color.black, new Color(1f, 0.8f, 0.2f), 2.5f),
                MaterialLibrary.Emissive(Color.black, new Color(0.5f, 0.3f, 1f), 2.5f)
            };
            var supportMat = MaterialLibrary.Lit(new Color(0.18f, 0.18f, 0.22f), 0.3f, 0.3f);
            AddSupports(runtime, root, supportMat, data.groundPlaneY, 2.5f, 28f);

            // Skyline blocks on both sides.
            for (int i = 0; i < nodes.Count; i += 6)
            {
                var n = nodes[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    float dist = n.Width * 0.5f + Range(rng, 7f, 26f);
                    Vector3 p = n.Position + n.Right * (side * dist);
                    p.y = data.groundPlaneY;
                    if (!IsClearOfRoad(runtime, p, 5f)) continue;
                    float w = Range(rng, 7f, 16f), d = Range(rng, 7f, 16f), h = Range(rng, 10f, 45f);
                    var b = PrimitiveFactory.Box("Building", root, p + Vector3.up * (h * 0.5f), new Vector3(w, h, d),
                        buildingMats[rng.Next(buildingMats.Length)], true);
                    b.layer = Layers.Wall;
                    // Window stripes.
                    int stripes = Mathf.Max(1, (int)(h / 6f));
                    var neon = neonMats[rng.Next(neonMats.Length)];
                    for (int s = 0; s < stripes; s++)
                    {
                        float y = 3f + s * 6f;
                        if (y > h - 2f) break;
                        var strip = PrimitiveFactory.Box("Neon", b.transform, new Vector3(0f, (y - h * 0.5f) / h, 0f), new Vector3(1.02f, 0.4f / h, 1.02f), neon);
                        PrimitiveFactory.SetShadowCasting(strip, false, false);
                    }
                    if (rng.NextDouble() < 0.35)
                    {
                        // Rooftop holographic sign.
                        var sign = PrimitiveFactory.Box("Sign", root, p + Vector3.up * (h + 2.5f), new Vector3(w * 0.8f, 3f, 0.3f),
                            neonMats[rng.Next(neonMats.Length)]);
                        sign.transform.rotation = Quaternion.LookRotation(-n.Right * side, Vector3.up);
                        PrimitiveFactory.SetShadowCasting(sign, false, false);
                    }
                }
            }

            // Street lamps along the edges.
            var lampMat = MaterialLibrary.Lit(new Color(0.3f, 0.3f, 0.35f), 0.5f, 0.6f);
            var lampGlow = MaterialLibrary.Emissive(Color.white, new Color(0.7f, 0.9f, 1f), 3f);
            for (int i = 0; i < nodes.Count; i += 10)
            {
                var n = nodes[i];
                if (n.IsGap || n.Has(TrackPointFlags.Tunnel)) continue;
                int side = (i / 10) % 2 == 0 ? -1 : 1;
                if ((side < 0 && n.Has(TrackPointFlags.NoWallLeft)) || (side > 0 && n.Has(TrackPointFlags.NoWallRight))) continue;
                Vector3 basePos = n.Position + n.Right * (side * (n.Width * 0.5f + 1.2f));
                PrimitiveFactory.Cylinder("Lamp", root, basePos + Vector3.up * 3f, 0.2f, 6f, lampMat);
                var glow = PrimitiveFactory.Box("LampGlow", root, basePos + Vector3.up * 6.1f - n.Right * (side * 1.2f), new Vector3(0.5f, 0.2f, 2.4f), lampGlow);
                glow.transform.rotation = Quaternion.LookRotation(n.Right * side, Vector3.up);
                PrimitiveFactory.SetShadowCasting(glow, false, false);
            }

            // Decorative traffic on the elevated highway (moving boxes far from the road).
            var traffic = new GameObject("AmbientTraffic");
            traffic.transform.SetParent(root, false);
            var mover = traffic.AddComponent<AmbientTraffic>();
            mover.Build(runtime, neonMats[0], neonMats[1], rng);
        }

        // ------------------------------------------------------------------ Solar Canyon

        private static void DecorateCanyon(TrackData data, TrackRuntime runtime, Transform root, System.Random rng)
        {
            var nodes = runtime.Nodes;
            var rockMats = new[]
            {
                MaterialLibrary.Lit(new Color(0.75f, 0.42f, 0.25f), 0.1f, 0f),
                MaterialLibrary.Lit(new Color(0.85f, 0.55f, 0.3f), 0.1f, 0f),
                MaterialLibrary.Lit(new Color(0.6f, 0.32f, 0.22f), 0.1f, 0f)
            };
            var woodMat = MaterialLibrary.Lit(new Color(0.45f, 0.3f, 0.18f), 0.2f, 0f);
            AddSupports(runtime, root, woodMat, data.groundPlaneY, 2.5f, 18f);

            // Rock formations.
            for (int i = 0; i < nodes.Count; i += 5)
            {
                var n = nodes[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rng.NextDouble() < 0.3) continue;
                    float dist = n.Width * 0.5f + Range(rng, 6f, 40f);
                    Vector3 p = n.Position + n.Right * (side * dist);
                    p.y = data.groundPlaneY;
                    if (!IsClearOfRoad(runtime, p, 4f)) continue;
                    float w = Range(rng, 6f, 22f), h = Range(rng, 6f, 38f);
                    var mat = rockMats[rng.Next(rockMats.Length)];
                    var rock = PrimitiveFactory.Cylinder("Rock", root, p + Vector3.up * (h * 0.5f), w, h, mat, true,
                        Quaternion.Euler(Range(rng, -4f, 4f), Range(rng, 0f, 360f), Range(rng, -4f, 4f)));
                    rock.layer = Layers.Wall;
                    var cap = PrimitiveFactory.Cylinder("RockCap", root, p + Vector3.up * (h + 1f), w * 1.15f, 2.5f, rockMats[(rng.Next(rockMats.Length))]);
                    PrimitiveFactory.SetShadowCasting(cap, true, true);
                }
            }

            // Cacti / dry bushes near the track edges.
            var cactusMat = MaterialLibrary.Lit(new Color(0.3f, 0.55f, 0.25f), 0.2f, 0f);
            for (int i = 0; i < nodes.Count; i += 9)
            {
                var n = nodes[i];
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                Vector3 p = n.Position + n.Right * (side * (n.Width * 0.5f + Range(rng, 3f, 7f)));
                p.y = data.groundPlaneY + 0.4f;
                if (Mathf.Abs(n.Position.y - data.groundPlaneY) > 2f) continue;
                float h = Range(rng, 1.5f, 3.5f);
                PrimitiveFactory.Capsule("Cactus", root, p + Vector3.up * (h * 0.5f), 0.6f, h, cactusMat);
                PrimitiveFactory.Capsule("CactusArm", root, p + Vector3.up * (h * 0.6f) + Vector3.right * 0.5f, 0.35f, h * 0.5f, cactusMat,
                    Quaternion.Euler(0f, 0f, -60f));
            }

            // Distant mesas.
            for (int i = 0; i < 18; i++)
            {
                float angle = i / 18f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(rng, 450f, 700f) + new Vector3(100f, data.groundPlaneY, 200f);
                float w = Range(rng, 60f, 160f), h = Range(rng, 40f, 120f);
                PrimitiveFactory.Cylinder("Mesa", root, p + Vector3.up * (h * 0.5f), w, h, rockMats[rng.Next(rockMats.Length)]);
            }
        }

        // ------------------------------------------------------------------ Sky Lab

        private static void DecorateSkyLab(TrackData data, TrackRuntime runtime, Transform root, System.Random rng)
        {
            var nodes = runtime.Nodes;
            var panelMat = MaterialLibrary.Lit(new Color(0.75f, 0.8f, 0.9f), 0.7f, 0.6f);
            var glowMat = MaterialLibrary.Emissive(Color.black, data.accentColor, 2f);
            var cloudMat = MaterialLibrary.Lit(new Color(0.95f, 0.97f, 1f), 0.05f, 0f);

            // Under-platform machinery and glow strips.
            for (int i = 0; i < nodes.Count; i += 4)
            {
                var n = nodes[i];
                if (n.IsGap) continue;
                var box = PrimitiveFactory.Box("Underside", root, n.Position - Vector3.up * 1.6f, new Vector3(n.Width * 0.8f, 1.8f, data.nodeSpacing * 4f),
                    panelMat, false, Quaternion.LookRotation(MathUtil.FlatNormalized(n.Forward), Vector3.up));
                if (i % 12 == 0)
                {
                    var glow = PrimitiveFactory.Box("UnderGlow", root, n.Position - Vector3.up * 2.7f, new Vector3(n.Width * 0.5f, 0.2f, 2f), glowMat);
                    PrimitiveFactory.SetShadowCasting(glow, false, false);
                }
            }

            // Antigravity-looking rings around tunnel sections and pylons near the road.
            var ringMat = MaterialLibrary.Emissive(new Color(0.2f, 0.3f, 0.5f), data.accentColor, 1.5f);
            var spinner = new GameObject("RingSpinner");
            spinner.transform.SetParent(root, false);
            var rings = spinner.AddComponent<RingSpinner>();
            for (int i = 0; i < nodes.Count; i += 7)
            {
                var n = nodes[i];
                if (!n.Has(TrackPointFlags.Tunnel)) continue;
                var ring = new GameObject("Ring").transform;
                ring.SetParent(spinner.transform, false);
                ring.position = n.Position + Vector3.up * (data.tunnelHeight * 0.5f);
                ring.rotation = Quaternion.LookRotation(n.Forward, Vector3.up);
                float radius = n.Width * 0.5f + 2.5f;
                for (int s = 0; s < 8; s++)
                {
                    float a = s / 8f * Mathf.PI * 2f;
                    var seg = PrimitiveFactory.Box("RingSeg", ring, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f),
                        new Vector3(1.2f, 1.2f, 0.6f), ringMat, false, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg));
                    PrimitiveFactory.SetShadowCasting(seg, false, false);
                }
                rings.Rings.Add(ring);
            }

            // Floating lab modules around the track.
            for (int i = 0; i < nodes.Count; i += 11)
            {
                var n = nodes[i];
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                float dist = n.Width * 0.5f + Range(rng, 18f, 60f);
                Vector3 p = n.Position + n.Right * (side * dist) + Vector3.up * Range(rng, -12f, 14f);
                if (!IsClearOfRoad(runtime, p, 8f)) continue;
                float w = Range(rng, 6f, 18f), h = Range(rng, 3f, 9f);
                var module = PrimitiveFactory.Box("LabModule", root, p, new Vector3(w, h, w * 0.7f), panelMat, true);
                module.layer = Layers.Wall;
                var strip = PrimitiveFactory.Box("ModuleGlow", root, p, new Vector3(w * 1.02f, 0.3f, w * 0.7f * 1.02f), glowMat);
                PrimitiveFactory.SetShadowCasting(strip, false, false);
                if (rng.NextDouble() < 0.5)
                {
                    PrimitiveFactory.Cylinder("Antenna", root, p + Vector3.up * (h * 0.5f + 2f), 0.3f, 4f, panelMat);
                }
            }

            // Cloud layer far below.
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = new Vector3(Range(rng, -450f, 550f), Range(rng, -55f, -35f), Range(rng, -500f, 450f));
                float w = Range(rng, 40f, 120f);
                var cloud = PrimitiveFactory.Cylinder("Cloud", root, p, w, Range(rng, 4f, 9f), cloudMat);
                PrimitiveFactory.SetShadowCasting(cloud, false, false);
            }
        }
    }

    /// <summary>Slowly rotates decorative rings (Sky Lab gravity tube).</summary>
    public class RingSpinner : MonoBehaviour
    {
        public List<Transform> Rings = new List<Transform>();

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < Rings.Count; i++)
            {
                if (Rings[i] != null) Rings[i].Rotate(0f, 0f, (i % 2 == 0 ? 40f : -55f) * dt, Space.Self);
            }
        }
    }

    /// <summary>Decorative vehicles gliding on lanes far outside the track (Metro Neón ambience).</summary>
    public class AmbientTraffic : MonoBehaviour
    {
        private readonly List<Transform> cars = new List<Transform>();
        private readonly List<float> speeds = new List<float>();
        private readonly List<float> offsets = new List<float>();
        private TrackRuntime track;

        public void Build(TrackRuntime runtime, Material a, Material b, System.Random rng)
        {
            track = runtime;
            for (int i = 0; i < 14; i++)
            {
                var car = PrimitiveFactory.Box("AmbientCar", transform, Vector3.zero, new Vector3(1.8f, 1f, 4f), i % 2 == 0 ? a : b);
                PrimitiveFactory.SetShadowCasting(car, false, false);
                cars.Add(car.transform);
                speeds.Add(14f + (float)rng.NextDouble() * 10f);
                offsets.Add((float)rng.NextDouble() * runtime.Length);
            }
        }

        private void Update()
        {
            if (track == null || track.Nodes.Count == 0) return;
            for (int i = 0; i < cars.Count; i++)
            {
                offsets[i] = MathUtil.Wrap(offsets[i] + speeds[i] * Time.deltaTime, track.Length);
                var n = track.GetNodeAtDistance(offsets[i]);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 p = n.Position + n.Right * (side * (n.Width * 0.5f + 34f)) + Vector3.up * 18f;
                cars[i].position = p;
                cars[i].rotation = Quaternion.LookRotation(n.Forward * -side, Vector3.up);
            }
        }
    }
}
