using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.Track
{
    /// <summary>
    /// Turns a TrackData definition into geometry, colliders, AI nodes, checkpoints, triggers and props.
    /// Everything is generated at load so scenes stay empty and tracks can be tuned in the Inspector.
    /// </summary>
    public static class TrackBuilder
    {
        private const float LateralAccelForSpeed = 12f;   // m/s^2 the AI trusts in corners
        private const float MaxReferenceSpeed = 36f;
        private const float BrakingDecel = 18f;
        private const float KickerLength = 9f;
        private const int NodesPerMeshChunk = 48;
        private const float EdgeStripeWidth = 0.45f;

        public static void Build(TrackData data, Transform root, TrackRuntime runtime)
        {
            if (data == null || !data.IsValid())
            {
                Debug.LogError("[TrackBuilder] Invalid track data.");
                return;
            }

            var geometry = new GameObject("Geometry").transform;
            geometry.SetParent(root, false);
            runtime.GeometryRoot = geometry;

            // ---- Main spline & nodes
            var positions = new List<Vector3>();
            var widths = new List<float>();
            for (int i = 0; i < data.controlPoints.Count; i++)
            {
                positions.Add(data.controlPoints[i].position);
                widths.Add(Mathf.Max(6f, data.controlPoints[i].width));
            }
            var spline = new TrackSpline(positions, widths, true);
            var samples = spline.SampleByDistance(Mathf.Max(2f, data.nodeSpacing));
            float length = spline.EstimateLength();

            var nodes = new List<TrackNode>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var node = new TrackNode
                {
                    Index = i,
                    Position = s.Position,
                    Forward = s.Tangent,
                    Width = s.Width,
                    Distance = s.Distance,
                    Segment = s.Segment,
                    T = s.T,
                    Flags = data.GetPoint(s.Segment).flags
                };
                nodes.Add(node);
            }

            // Control point distances (first node of each segment).
            var cpDistances = new float[data.controlPoints.Count];
            for (int s = 0; s < cpDistances.Length; s++) cpDistances[s] = -1f;
            for (int i = 0; i < nodes.Count; i++)
            {
                int seg = nodes[i].Segment;
                if (seg >= 0 && seg < cpDistances.Length && cpDistances[seg] < 0f) cpDistances[seg] = nodes[i].Distance;
            }
            for (int s = 0; s < cpDistances.Length; s++)
            {
                if (cpDistances[s] < 0f) cpDistances[s] = s > 0 ? cpDistances[s - 1] : 0f;
            }
            runtime.ControlPointDistances = cpDistances;

            ApplyGapsAndKickers(data, nodes, cpDistances, length);
            ComputeFrames(nodes, true);
            ComputeCurvatureAndSpeed(nodes, true, data.nodeSpacing, 1f);

            // ---- Shortcuts
            var shortcutNodes = new List<List<TrackNode>>();
            for (int sc = 0; sc < data.shortcuts.Count; sc++)
            {
                var def = data.shortcuts[sc];
                var list = BuildShortcutNodes(data, def, sc, cpDistances, length);
                shortcutNodes.Add(list);
            }
            CarveShortcutJunctions(nodes, shortcutNodes, length);

            runtime.Setup(data, nodes, shortcutNodes, length);

            // ---- Geometry
            var roadMat = MaterialLibrary.Lit(data.roadColor, 0.25f, 0f);
            var glassMat = MaterialLibrary.UnlitTransparent(new Color(data.roadColor.r, data.roadColor.g, data.roadColor.b, 0.5f));
            var edgeMat = MaterialLibrary.Emissive(data.roadEdgeColor * 0.4f, data.roadEdgeColor, 1.8f);
            var wallMat = MaterialLibrary.Lit(data.wallColor, 0.3f, 0.1f);
            var tunnelMat = MaterialLibrary.Lit(data.wallColor * 0.7f, 0.2f, 0f);

            BuildRoad(nodes, true, geometry, data, roadMat, glassMat, edgeMat, wallMat, tunnelMat, "Road");
            for (int sc = 0; sc < shortcutNodes.Count; sc++)
            {
                var def = data.shortcuts[sc];
                BuildShortcutRoad(shortcutNodes[sc], def, geometry, data, roadMat, edgeMat, wallMat, tunnelMat);
            }

            if (data.hasGroundPlane) BuildGroundPlane(data, nodes, geometry);
            BuildKillZones(data, nodes, cpDistances, length, geometry);
            if (data.hasGroundPlane) BuildChasmKillZones(data, nodes, geometry);

            // ---- Gameplay objects
            var gameplay = new GameObject("Gameplay").transform;
            gameplay.SetParent(root, false);
            BuildCheckpoints(data, runtime, nodes, gameplay);
            BuildShortcutTriggers(data, runtime, nodes, shortcutNodes, cpDistances, gameplay);
            BuildBoostPads(data, runtime, gameplay);
            BuildPowerUpRows(data, runtime);
            BuildHazards(data, runtime, gameplay);

            // ---- Environment
            BuildSun(data, root, runtime);
            TrackDecorator.Decorate(data, runtime, geometry);

            StaticBatchingUtility.Combine(geometry.gameObject);
        }

        // ------------------------------------------------------------------ Nodes

        private static void ApplyGapsAndKickers(TrackData data, List<TrackNode> nodes, float[] cpDistances, float length)
        {
            for (int s = 0; s < data.controlPoints.Count; s++)
            {
                if (!data.controlPoints[s].Has(TrackPointFlags.JumpGap)) continue;
                float gapStart = cpDistances[s];
                float gapEnd = gapStart + data.gapLength;
                for (int i = 0; i < nodes.Count; i++)
                {
                    float d = nodes[i].Distance;
                    float rel = MathUtil.LoopDelta(gapStart, d, length);
                    if (rel >= 0f && rel < data.gapLength)
                    {
                        nodes[i].IsGap = true;
                    }
                    else if (rel < 0f && rel > -KickerLength)
                    {
                        float t = 1f + rel / KickerLength; // 0 at kicker start, 1 at the edge
                        nodes[i].Position += Vector3.up * (data.kickerHeight * MathUtil.SmoothStep01(t));
                    }
                }
            }
        }

        private static void ComputeFrames(List<TrackNode> nodes, bool closed)
        {
            int n = nodes.Count;
            for (int i = 0; i < n; i++)
            {
                var node = nodes[i];
                Vector3 fwd;
                if (closed)
                {
                    fwd = nodes[(i + 1) % n].Position - nodes[(i - 1 + n) % n].Position;
                }
                else
                {
                    int a = Mathf.Max(0, i - 1), b = Mathf.Min(n - 1, i + 1);
                    fwd = nodes[b].Position - nodes[a].Position;
                }
                if (fwd.sqrMagnitude < 1e-4f) fwd = node.Forward;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
                Vector3 up = Vector3.Cross(fwd, right).normalized;
                node.Forward = fwd;
                node.Right = right;
                node.Up = up;
            }
        }

        private static void ComputeCurvatureAndSpeed(List<TrackNode> nodes, bool closed, float spacing, float speedScale)
        {
            int n = nodes.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 f0, f1;
                if (closed)
                {
                    f0 = nodes[(i - 1 + n) % n].Forward;
                    f1 = nodes[(i + 1) % n].Forward;
                }
                else
                {
                    f0 = nodes[Mathf.Max(0, i - 1)].Forward;
                    f1 = nodes[Mathf.Min(n - 1, i + 1)].Forward;
                }
                float angle = Vector3.SignedAngle(MathUtil.Flat(f0), MathUtil.Flat(f1), Vector3.up) * Mathf.Deg2Rad;
                float curvature = angle / Mathf.Max(0.1f, 2f * spacing);
                nodes[i].Curvature = curvature;
                float absC = Mathf.Abs(curvature);
                float v = absC > 1e-4f ? Mathf.Sqrt(LateralAccelForSpeed / absC) : MaxReferenceSpeed;
                nodes[i].RecommendedSpeed = Mathf.Clamp(v, 11f, MaxReferenceSpeed) * speedScale;
            }
            // Smooth curvature a little (3-tap) and apply braking constraint backwards.
            var smooth = new float[n];
            for (int i = 0; i < n; i++)
            {
                float a = nodes[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)].Curvature;
                float b = nodes[i].Curvature;
                float c = nodes[closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1)].Curvature;
                smooth[i] = (a + 2f * b + c) * 0.25f;
            }
            for (int i = 0; i < n; i++) nodes[i].Curvature = smooth[i];

            int passes = closed ? 2 : 1;
            for (int p = 0; p < passes; p++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    var next = nodes[closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1)];
                    float allowed = Mathf.Sqrt(next.RecommendedSpeed * next.RecommendedSpeed + 2f * BrakingDecel * spacing);
                    if (nodes[i].RecommendedSpeed > allowed) nodes[i].RecommendedSpeed = allowed;
                }
            }
        }

        /// <summary>
        /// Shortcuts start and end on the main road's centre line. Open the main wall on the side the
        /// shortcut leaves/joins, and do not build shortcut geometry while it still overlaps the main road
        /// (otherwise its walls would cut across the main lane and block everybody).
        /// </summary>
        private static void CarveShortcutJunctions(List<TrackNode> mainNodes, List<List<TrackNode>> shortcuts, float length)
        {
            for (int sc = 0; sc < shortcuts.Count; sc++)
            {
                var list = shortcuts[sc];
                if (list.Count < 4) continue;

                // Nodes overlapping the main road: no mesh, no walls.
                for (int i = 0; i < list.Count; i++)
                {
                    var n = list[i];
                    TrackNode closest = null;
                    float best = float.MaxValue;
                    for (int m = 0; m < mainNodes.Count; m += 1)
                    {
                        float d = (mainNodes[m].Position - n.Position).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            closest = mainNodes[m];
                        }
                    }
                    if (closest == null) continue;
                    Vector3 delta = n.Position - closest.Position;
                    float signedLateral = Vector3.Dot(delta, closest.Right);
                    float lateral = Mathf.Abs(signedLateral);
                    float vertical = Mathf.Abs(Vector3.Dot(delta, closest.Up));
                    if (vertical >= 3.5f) continue;
                    if (lateral + n.Width * 0.5f < closest.Width * 0.5f + 0.3f)
                    {
                        // Shortcut lane entirely inside the main lane: no geometry at all (the main road is the floor).
                        n.SkipMesh = true;
                        n.Flags |= TrackPointFlags.NoWalls;
                        n.Position.y = closest.Position.y;
                    }
                    else if (lateral < closest.Width * 0.5f + n.Width * 0.5f + 0.5f)
                    {
                        // Partially overlapping: keep the floor but make it coplanar with the main road (no lip), drop walls.
                        n.Flags |= TrackPointFlags.NoWalls;
                        float blend = Mathf.InverseLerp(closest.Width * 0.5f + n.Width * 0.5f + 0.5f, closest.Width * 0.5f - n.Width * 0.5f, lateral);
                        n.Position.y = Mathf.Lerp(n.Position.y, closest.Position.y, Mathf.Clamp01(blend));
                    }
                    else if (lateral < closest.Width * 0.5f + n.Width * 0.5f + 2.5f)
                    {
                        // Running alongside the main road: drop the walls facing each other so there is no funnel.
                        bool shortcutOnRight = signedLateral > 0f;
                        n.Flags |= shortcutOnRight ? TrackPointFlags.NoWallLeft : TrackPointFlags.NoWallRight;
                        TrackPointFlags mainFlag = shortcutOnRight ? TrackPointFlags.NoWallRight : TrackPointFlags.NoWallLeft;
                        for (int k = -2; k <= 2; k++)
                        {
                            int idx = closest.Index + k;
                            idx %= mainNodes.Count;
                            if (idx < 0) idx += mainNodes.Count;
                            mainNodes[idx].Flags |= mainFlag;
                        }
                    }
                }

                // Heights changed near the junctions: refresh the shortcut frames.
                ComputeFrames(list, false);

                // Main road: open the wall on the branch side around entry and exit.
                OpenMainWall(mainNodes, list[0], list[Mathf.Min(4, list.Count - 1)], length, -10f, 26f);
                OpenMainWall(mainNodes, list[list.Count - 1], list[Mathf.Max(0, list.Count - 5)], length, -26f, 10f);
            }
        }

        private static void OpenMainWall(List<TrackNode> mainNodes, TrackNode junction, TrackNode towards, float length, float from, float to)
        {
            float junctionDistance = junction.MainDistanceEquivalent;
            // Find the main node at the junction to know which side the shortcut is on.
            TrackNode mainAt = null;
            float best = float.MaxValue;
            for (int m = 0; m < mainNodes.Count; m++)
            {
                float d = (mainNodes[m].Position - junction.Position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    mainAt = mainNodes[m];
                }
            }
            if (mainAt == null) return;
            float side = Vector3.Dot(towards.Position - junction.Position, mainAt.Right);
            TrackPointFlags flag = side >= 0f ? TrackPointFlags.NoWallRight : TrackPointFlags.NoWallLeft;
            for (int m = 0; m < mainNodes.Count; m++)
            {
                float rel = MathUtil.LoopDelta(junctionDistance, mainNodes[m].Distance, length);
                if (rel >= from && rel <= to) mainNodes[m].Flags |= flag;
            }
        }

        private static List<TrackNode> BuildShortcutNodes(TrackData data, ShortcutDefinition def, int index, float[] cpDistances, float length)
        {
            var pts = new List<Vector3>();
            var ws = new List<float>();
            var entry = data.GetPoint(def.entryPointIndex);
            var exit = data.GetPoint(def.exitPointIndex);
            pts.Add(entry.position);
            ws.Add(entry.width);
            for (int i = 0; i < def.waypoints.Count; i++)
            {
                pts.Add(def.waypoints[i]);
                ws.Add(def.width);
            }
            pts.Add(exit.position);
            ws.Add(exit.width);

            var spline = new TrackSpline(pts, ws, false);
            var samples = spline.SampleByDistance(Mathf.Max(2f, data.nodeSpacing));
            float entryD = cpDistances[Mathf.Clamp(def.entryPointIndex, 0, cpDistances.Length - 1)];
            float exitD = cpDistances[Mathf.Clamp(def.exitPointIndex, 0, cpDistances.Length - 1)];
            if (exitD < entryD) exitD += length;
            float scLength = samples.Count > 0 ? samples[samples.Count - 1].Distance : 1f;

            var list = new List<TrackNode>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                float f = scLength > 0f ? s.Distance / scLength : 0f;
                var node = new TrackNode
                {
                    Index = i,
                    Position = s.Position,
                    Forward = s.Tangent,
                    Width = i == 0 || i == samples.Count - 1 ? s.Width : def.width,
                    Distance = s.Distance,
                    Segment = s.Segment,
                    T = s.T,
                    IsShortcut = true,
                    ShortcutIndex = index,
                    MainDistanceEquivalent = MathUtil.Wrap(Mathf.Lerp(entryD, exitD, f), length),
                    Flags = def.isTunnel ? TrackPointFlags.Tunnel : TrackPointFlags.None
                };
                if (!def.wallsEnabled) node.Flags |= TrackPointFlags.NoWalls;
                node.Flags |= TrackPointFlags.NoOvertake;
                list.Add(node);
            }
            ComputeFrames(list, false);
            ComputeCurvatureAndSpeed(list, false, data.nodeSpacing, def.aiSpeedFraction / 0.9f);
            return list;
        }

        // ------------------------------------------------------------------ Road geometry

        private static void BuildRoad(List<TrackNode> nodes, bool closed, Transform parent, TrackData data, Material roadMat,
            Material glassMat, Material edgeMat, Material wallMat, Material tunnelMat, string name)
        {
            int n = nodes.Count;
            int segCount = closed ? n : n - 1;
            for (int start = 0; start < segCount; start += NodesPerMeshChunk)
            {
                int end = Mathf.Min(segCount, start + NodesPerMeshChunk);
                var road = new MeshBuilder();
                var glass = new MeshBuilder();
                var edges = new MeshBuilder();
                var walls = new MeshBuilder();
                var tunnel = new MeshBuilder();
                bool anyGlass = false;

                for (int i = start; i < end; i++)
                {
                    var a = nodes[i];
                    var b = nodes[closed ? (i + 1) % n : i + 1];
                    if (a.IsGap || b.IsGap || a.SkipMesh || b.SkipMesh) continue;

                    float va = a.Distance / 10f, vb = b.Distance / 10f;
                    if (closed && i == n - 1) vb = va + data.nodeSpacing / 10f;

                    bool isGlass = a.Has(TrackPointFlags.GlassRoad);
                    var target = isGlass ? glass : road;
                    if (isGlass) anyGlass = true;
                    target.AddStripSegment(a.LeftEdge, a.RightEdge, b.LeftEdge, b.RightEdge, va, vb, a.Up);
                    // Underside so the road is visible from below (jumps, elevated sections).
                    Vector3 drop = -a.Up * 0.5f;
                    target.AddStripSegment(a.RightEdge + drop, a.LeftEdge + drop, b.RightEdge + drop, b.LeftEdge + drop, va, vb, -a.Up);
                    // Side skirts.
                    target.AddStripSegment(a.LeftEdge + drop, a.LeftEdge, b.LeftEdge + drop, b.LeftEdge, va, vb, -a.Right);
                    target.AddStripSegment(a.RightEdge, a.RightEdge + drop, b.RightEdge, b.RightEdge + drop, va, vb, a.Right);

                    // Edge stripes.
                    Vector3 lift = a.Up * 0.02f;
                    edges.AddStripSegment(a.LeftEdge + lift, a.LeftEdge + a.Right * EdgeStripeWidth + lift,
                        b.LeftEdge + lift, b.LeftEdge + b.Right * EdgeStripeWidth + lift, va, vb, a.Up);
                    edges.AddStripSegment(a.RightEdge - a.Right * EdgeStripeWidth + lift, a.RightEdge + lift,
                        b.RightEdge - b.Right * EdgeStripeWidth + lift, b.RightEdge + lift, va, vb, a.Up);

                    bool isTunnel = a.Has(TrackPointFlags.Tunnel);
                    float wallH = isTunnel ? data.tunnelHeight : data.wallHeight;
                    bool leftWall = isTunnel || !a.Has(TrackPointFlags.NoWallLeft);
                    bool rightWall = isTunnel || !a.Has(TrackPointFlags.NoWallRight);
                    var wallTarget = isTunnel ? tunnel : walls;
                    if (leftWall) AddWall(wallTarget, a.LeftEdge, b.LeftEdge, a.Up, b.Up, -a.Right, wallH, va, vb);
                    if (rightWall) AddWall(wallTarget, a.RightEdge, b.RightEdge, a.Up, b.Up, a.Right, wallH, va, vb);
                    if (isTunnel)
                    {
                        Vector3 ha = a.Up * data.tunnelHeight, hb = b.Up * data.tunnelHeight;
                        tunnel.AddStripSegment(a.RightEdge + ha - a.Right * 0.4f, a.LeftEdge + ha + a.Right * 0.4f,
                            b.RightEdge + hb - b.Right * 0.4f, b.LeftEdge + hb + b.Right * 0.4f, va, vb, -a.Up);
                        // Outer shell so the tunnel reads as a solid from outside.
                        tunnel.AddStripSegment(a.LeftEdge + ha + a.Up * 0.6f, a.RightEdge + ha + a.Up * 0.6f,
                            b.LeftEdge + hb + b.Up * 0.6f, b.RightEdge + hb + b.Up * 0.6f, va, vb, a.Up);
                    }
                }

                string suffix = "_" + (start / NodesPerMeshChunk);
                CreateMeshObject(parent, name + suffix, road, roadMat, true, Layers.Track, SurfaceType.Road, 1f, 1f);
                if (anyGlass) CreateMeshObject(parent, name + "_Glass" + suffix, glass, glassMat, true, Layers.Track, SurfaceType.Road, 1f, 1f);
                CreateMeshObject(parent, name + "_Edges" + suffix, edges, edgeMat, false, Layers.Track, SurfaceType.Road, 1f, 1f);
                CreateMeshObject(parent, name + "_Walls" + suffix, walls, wallMat, true, Layers.Wall, SurfaceType.Road, 1f, 1f);
                CreateMeshObject(parent, name + "_Tunnel" + suffix, tunnel, tunnelMat, true, Layers.Wall, SurfaceType.Road, 1f, 1f);
            }
        }

        private static void BuildShortcutRoad(List<TrackNode> nodes, ShortcutDefinition def, Transform parent, TrackData data,
            Material roadMat, Material edgeMat, Material wallMat, Material tunnelMat)
        {
            var scRoot = new GameObject("Shortcut_" + def.name).transform;
            scRoot.SetParent(parent, false);
            var glassMat = MaterialLibrary.UnlitTransparent(new Color(data.roadColor.r, data.roadColor.g, data.roadColor.b, 0.5f));
            BuildRoad(nodes, false, scRoot, data, roadMat, glassMat, edgeMat, wallMat, tunnelMat, "ShortcutRoad");
        }

        private static void AddWall(MeshBuilder mb, Vector3 baseA, Vector3 baseB, Vector3 upA, Vector3 upB, Vector3 outward,
            float height, float va, float vb)
        {
            Vector3 topA = baseA + upA * height, topB = baseB + upB * height;
            Vector3 thick = outward * 0.35f;
            // Inner face (towards road).
            mb.AddStripSegment(baseA, topA, baseB, topB, va, vb, -outward);
            // Outer face.
            mb.AddStripSegment(topA + thick, baseA + thick - upA * 0.5f, topB + thick, baseB + thick - upB * 0.5f, va, vb, outward);
            // Top.
            mb.AddStripSegment(topA, topA + thick, topB, topB + thick, va, vb, upA);
        }

        private static GameObject CreateMeshObject(Transform parent, string name, MeshBuilder mb, Material mat, bool collider, int layer,
            SurfaceType surface, float speedFactor, float gripFactor)
        {
            if (mb.VertexCount == 0) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            go.isStatic = true;
            var mesh = mb.Build(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                TrackSurface.Configure(go, surface, speedFactor, gripFactor);
            }
            return go;
        }

        private static void BuildGroundPlane(TrackData data, List<TrackNode> nodes, Transform parent)
        {
            Bounds b = new Bounds(nodes[0].Position, Vector3.zero);
            for (int i = 1; i < nodes.Count; i++) b.Encapsulate(nodes[i].Position);
            for (int i = 0; i < data.shortcuts.Count; i++)
                for (int w = 0; w < data.shortcuts[i].waypoints.Count; w++) b.Encapsulate(data.shortcuts[i].waypoints[w]);
            float margin = 400f;
            var go = PrimitiveFactory.Box("Ground", parent, new Vector3(b.center.x, data.groundPlaneY - 0.5f, b.center.z),
                new Vector3(b.size.x + margin * 2f, 1f, b.size.z + margin * 2f), MaterialLibrary.Lit(data.groundColor, 0.05f, 0f), true);
            go.layer = Layers.Track;
            go.isStatic = true;
            PrimitiveFactory.SetShadowCasting(go, false, true);
            TrackSurface.Configure(go, SurfaceType.OffRoad, 0.5f, 0.75f);
        }

        private static void BuildKillZones(TrackData data, List<TrackNode> nodes, float[] cpDistances, float length, Transform parent)
        {
            for (int s = 0; s < data.controlPoints.Count; s++)
            {
                if (!data.controlPoints[s].Has(TrackPointFlags.JumpGap)) continue;
                float gapStart = cpDistances[s];
                // Find the node at the gap start and its width.
                TrackNode startNode = null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].IsGap)
                    {
                        float rel = MathUtil.LoopDelta(gapStart, nodes[i].Distance, length);
                        if (rel >= 0f && rel < data.nodeSpacing * 1.5f)
                        {
                            startNode = nodes[i];
                            break;
                        }
                    }
                }
                if (startNode == null) continue;
                Vector3 center = startNode.Position + startNode.Forward * (data.gapLength * 0.5f) - Vector3.up * 4.5f;
                KillZone.Create(parent, center, Quaternion.LookRotation(MathUtil.FlatNormalized(startNode.Forward), Vector3.up),
                    new Vector3(startNode.Width + 30f, 6f, data.gapLength + 14f));
            }
        }

        /// <summary>
        /// Elevated sections without walls (bridges, ledges) drop onto the off-road plane far below. Driving on
        /// from down there is hopeless, so the fall respawns the kart at the last checkpoint instead.
        /// </summary>
        private static void BuildChasmKillZones(TrackData data, List<TrackNode> nodes, Transform parent)
        {
            float next = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Distance < next) continue;
                bool open = n.Has(TrackPointFlags.NoWallLeft) || n.Has(TrackPointFlags.NoWallRight);
                float height = n.Position.y - data.groundPlaneY;
                if (!open || height < 6f) continue;
                next = n.Distance + 18f;
                // Low and narrow enough not to touch roads passing nearby at ground level or shortcuts above it.
                Vector3 center = new Vector3(n.Position.x, data.groundPlaneY + 2.5f, n.Position.z);
                var rot = Quaternion.LookRotation(MathUtil.FlatNormalized(n.Forward), Vector3.up);
                KillZone.Create(parent, center, rot, new Vector3(n.Width + 30f, 6f, 24f));
            }
        }

        // ------------------------------------------------------------------ Gameplay objects

        private static void BuildCheckpoints(TrackData data, TrackRuntime runtime, List<TrackNode> nodes, Transform parent)
        {
            float spacing = Mathf.Max(40f, data.checkpointSpacing);
            int count = Mathf.Max(3, Mathf.RoundToInt(runtime.Length / spacing));
            float step = runtime.Length / count;
            for (int c = 0; c < count; c++)
            {
                var node = runtime.GetNodeAtDistance(c * step);
                int guard = 0;
                while (node != null && node.IsGap && guard++ < 30) node = runtime.GetNode(node.Index + 1);
                if (node == null) continue;
                if (c == 0) node = nodes[0];
                var cp = Checkpoint.Create(parent, c, node);
                runtime.RegisterCheckpoint(cp);
            }
            BuildStartGate(data, nodes[0], parent);
        }

        private static void BuildStartGate(TrackData data, TrackNode node, Transform parent)
        {
            var gate = new GameObject("StartGate").transform;
            gate.SetParent(parent, false);
            var pillarMat = MaterialLibrary.Lit(data.wallColor, 0.4f, 0.3f);
            var bannerMat = MaterialLibrary.Emissive(data.accentColor * 0.3f, data.accentColor, 2f);
            float half = node.Width * 0.5f + 1.2f;
            float h = 7f;
            PrimitiveFactory.Box("PillarL", gate, node.LeftEdge - node.Right * 1.2f + Vector3.up * (h * 0.5f), new Vector3(0.8f, h, 0.8f), pillarMat, true);
            PrimitiveFactory.Box("PillarR", gate, node.RightEdge + node.Right * 1.2f + Vector3.up * (h * 0.5f), new Vector3(0.8f, h, 0.8f), pillarMat, true);
            var banner = PrimitiveFactory.Box("Banner", gate, node.Position + Vector3.up * (h - 0.6f), new Vector3(half * 2f, 1.2f, 0.4f), bannerMat);
            banner.transform.rotation = Quaternion.LookRotation(MathUtil.FlatNormalized(node.Forward), Vector3.up);
            // Checkered strip on the road.
            var lineMat = MaterialLibrary.Emissive(Color.white * 0.8f, Color.white, 0.6f);
            var darkMat = MaterialLibrary.Lit(new Color(0.05f, 0.05f, 0.06f));
            int squares = Mathf.Max(4, Mathf.RoundToInt(node.Width / 1.5f));
            float sq = node.Width / squares;
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < squares; i++)
                {
                    bool white = (i + row) % 2 == 0;
                    Vector3 p = node.LeftEdge + node.Right * (sq * (i + 0.5f)) + node.Forward * (row * sq - sq * 0.5f) + node.Up * 0.03f;
                    var s = PrimitiveFactory.Box("Sq", gate, p, new Vector3(sq, 0.02f, sq), white ? lineMat : darkMat);
                    s.transform.rotation = Quaternion.LookRotation(node.Forward, node.Up);
                    PrimitiveFactory.SetShadowCasting(s, false, false);
                }
            }
        }

        private static void BuildShortcutTriggers(TrackData data, TrackRuntime runtime, List<TrackNode> nodes,
            List<List<TrackNode>> shortcutNodes, float[] cpDistances, Transform parent)
        {
            for (int sc = 0; sc < data.shortcuts.Count; sc++)
            {
                var def = data.shortcuts[sc];
                var list = shortcutNodes[sc];
                if (list.Count < 3) continue;
                // Entry gate a little into the shortcut so touching it means committing to the route.
                var entryNode = list[Mathf.Min(3, list.Count - 1)];
                var exitNode = list[Mathf.Max(0, list.Count - 4)];
                float exitD = cpDistances[Mathf.Clamp(def.exitPointIndex, 0, cpDistances.Length - 1)];
                int exitCp = (runtime.CheckpointIndexAtDistance(exitD) + 1) % Mathf.Max(1, runtime.Checkpoints.Count);
                ShortcutTrigger.Create(parent, entryNode, sc, false, exitCp, def.width);
                ShortcutTrigger.Create(parent, exitNode, sc, true, exitCp, def.width);
            }
        }

        private static TrackNode NodeAtPlacement(TrackRuntime runtime, TrackPlacement placement)
        {
            var d = runtime.ControlPointDistances;
            if (d.Length == 0) return runtime.GetNode(0);
            int s = Mathf.Clamp(placement.segmentIndex, 0, d.Length - 1);
            float start = d[s];
            float end = s + 1 < d.Length ? d[s + 1] : runtime.Length;
            float dist = Mathf.Lerp(start, end, Mathf.Clamp01(placement.t));
            var node = runtime.GetNodeAtDistance(dist);
            int guard = 0;
            while (node != null && node.IsGap && guard++ < 30) node = runtime.GetNode(node.Index + 1);
            return node;
        }

        private static void BuildBoostPads(TrackData data, TrackRuntime runtime, Transform parent)
        {
            for (int i = 0; i < data.boostPads.Count; i++)
            {
                var def = data.boostPads[i];
                var node = NodeAtPlacement(runtime, def.placement);
                if (node == null) continue;
                float lateral = Mathf.Clamp(def.placement.lateralOffset, -node.Width * 0.5f + def.width * 0.5f, node.Width * 0.5f - def.width * 0.5f);
                BoostPad.Create(parent, node, lateral, def.length, def.width, def.boostMultiplier, def.boostDuration, data.roadEdgeColor);
            }
        }

        private static void BuildPowerUpRows(TrackData data, TrackRuntime runtime)
        {
            runtime.PowerUpBoxPositions.Clear();
            for (int i = 0; i < data.powerUpRows.Count; i++)
            {
                var def = data.powerUpRows[i];
                var node = NodeAtPlacement(runtime, def.placement);
                if (node == null) continue;
                int count = Mathf.Clamp(def.count, 1, 6);
                float usable = node.Width * 0.72f;
                for (int c = 0; c < count; c++)
                {
                    float lateral = count == 1 ? 0f : -usable * 0.5f + usable * c / (count - 1);
                    runtime.PowerUpBoxPositions.Add(node.PointAtLateral(lateral) + node.Up * 1.1f);
                }
            }
        }

        private static void BuildHazards(TrackData data, TrackRuntime runtime, Transform parent)
        {
            for (int i = 0; i < data.hazards.Count; i++)
            {
                var def = data.hazards[i];
                var node = NodeAtPlacement(runtime, def.placement);
                if (node == null) continue;
                switch (def.type)
                {
                    case HazardType.MovingBarrier:
                        MovingBarrier.Create(parent, node, def.placement.lateralOffset, def.size, def.strength, def.range, data.accentColor);
                        break;
                    case HazardType.Fan:
                        FanZone.Create(parent, node, def.placement.lateralOffset, def.size, def.strength, def.range, data.accentColor);
                        break;
                    case HazardType.SlowZone:
                        CreateSurfaceZone(parent, node, def, SurfaceType.Slow, Mathf.Clamp(def.strength, 0.3f, 0.95f), 1f,
                            new Color(0.5f, 0.35f, 0.2f, 1f), data);
                        break;
                    case HazardType.SlipperyZone:
                        CreateSurfaceZone(parent, node, def, SurfaceType.Slippery, 1f, Mathf.Clamp(def.strength, 0.15f, 0.8f),
                            new Color(0.3f, 0.7f, 1f, 1f), data);
                        break;
                }
            }
        }

        private static void CreateSurfaceZone(Transform parent, TrackNode node, HazardDefinition def, SurfaceType type, float speed,
            float grip, Color tint, TrackData data)
        {
            var go = new GameObject(type + "Zone");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(node.PointAtLateral(def.placement.lateralOffset) + node.Up * 0.02f,
                Quaternion.LookRotation(node.Forward, node.Up));
            go.layer = Layers.Track;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(def.size, 0.05f, def.range);
            box.center = new Vector3(0f, 0.0f, 0f);
            TrackSurface.Configure(go, type, speed, grip);
            var visual = PrimitiveFactory.Box("Visual", go.transform, new Vector3(0f, 0.005f, 0f), new Vector3(def.size, 0.01f, def.range),
                MaterialLibrary.Emissive(tint * 0.5f, tint, 0.8f));
            PrimitiveFactory.SetShadowCasting(visual, false, false);
        }

        private static void BuildSun(TrackData data, Transform root, TrackRuntime runtime)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(root, false);
            go.transform.rotation = Quaternion.Euler(data.sunDirection);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = data.sunColor;
            light.intensity = data.sunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            runtime.Sun = light;
        }
    }
}
