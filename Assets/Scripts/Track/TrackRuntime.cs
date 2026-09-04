using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Utils;

namespace VortexKarts.Track
{
    /// <summary>
    /// Runtime queries over the generated track: nodes, checkpoints, shortcuts, distance projection,
    /// start grid. Built once per race by TrackBuilder; one instance per track scene.
    /// </summary>
    public class TrackRuntime : MonoBehaviour
    {
        public static TrackRuntime Instance { get; private set; }

        public TrackData Data { get; private set; }
        public List<TrackNode> Nodes { get; private set; } = new List<TrackNode>();
        public List<List<TrackNode>> ShortcutNodes { get; private set; } = new List<List<TrackNode>>();
        public List<Checkpoint> Checkpoints { get; private set; } = new List<Checkpoint>();
        public float Length { get; private set; }
        public float KillY => Data != null ? Data.killY : -30f;
        public Transform GeometryRoot { get; set; }
        public Light Sun { get; set; }
        /// <summary>World positions where power-up boxes spawn (filled by TrackBuilder).</summary>
        public List<Vector3> PowerUpBoxPositions { get; } = new List<Vector3>();
        /// <summary>Distance along the main road of every control point (index = control point).</summary>
        public float[] ControlPointDistances { get; set; } = new float[0];

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Setup(TrackData data, List<TrackNode> nodes, List<List<TrackNode>> shortcuts, float length)
        {
            Data = data;
            Nodes = nodes;
            ShortcutNodes = shortcuts ?? new List<List<TrackNode>>();
            Length = length;
            Checkpoints.Clear();
        }

        public void RegisterCheckpoint(Checkpoint cp)
        {
            Checkpoints.Add(cp);
            Checkpoints.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        // ------------------------------------------------------------------ Node queries

        public TrackNode GetNode(int index)
        {
            int n = Nodes.Count;
            if (n == 0) return null;
            index %= n;
            if (index < 0) index += n;
            return Nodes[index];
        }

        public TrackNode GetNodeAhead(TrackNode node, int steps)
        {
            if (node == null) return null;
            if (node.IsShortcut)
            {
                var list = ShortcutNodes[node.ShortcutIndex];
                int i = node.Index + steps;
                if (i < list.Count) return list[i];
                // Past the shortcut end: continue on the main road from the equivalent distance.
                var exitNode = GetNodeAtDistance(list[list.Count - 1].MainDistanceEquivalent);
                return GetNode(exitNode.Index + (i - list.Count) + 1);
            }
            return GetNode(node.Index + steps);
        }

        public TrackNode GetNodeAtDistance(float distance)
        {
            if (Nodes.Count == 0) return null;
            float d = MathUtil.Wrap(distance, Length);
            float spacing = Length / Nodes.Count;
            int idx = Mathf.RoundToInt(d / spacing);
            return GetNode(idx);
        }

        /// <summary>Position/rotation on the main road at a distance and lateral offset (used for grid + respawn).</summary>
        public void GetPoseAtDistance(float distance, float lateral, out Vector3 position, out Quaternion rotation)
        {
            var node = GetNodeAtDistance(distance);
            if (node == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }
            position = node.PointAtLateral(lateral);
            rotation = Quaternion.LookRotation(node.Forward, node.Up);
        }

        /// <summary>Closest main node; searches around the hint when provided.</summary>
        public TrackNode GetClosestNode(Vector3 position, int hint = -1, int searchRadius = 12)
        {
            int n = Nodes.Count;
            if (n == 0) return null;
            int best = -1;
            float bestDist = float.MaxValue;
            if (hint >= 0)
            {
                for (int o = -searchRadius; o <= searchRadius; o++)
                {
                    int i = hint + o;
                    i %= n;
                    if (i < 0) i += n;
                    float d = (Nodes[i].Position - position).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
                // If the closest hinted node is far away, fall back to a full search.
                if (bestDist < 40f * 40f) return Nodes[best];
            }
            best = 0;
            bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = (Nodes[i].Position - position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return Nodes[best];
        }

        /// <summary>
        /// Projects a world position onto the main road. Returns distance along the lap; outputs the lateral
        /// offset (metres right of centre) and the reference node. Uses the hint for speed.
        /// </summary>
        public float ProjectOntoMain(Vector3 position, ref int hint, out float lateral, out TrackNode node)
        {
            node = GetClosestNode(position, hint);
            if (node == null)
            {
                lateral = 0f;
                return 0f;
            }
            hint = node.Index;
            Vector3 delta = position - node.Position;
            float along = Vector3.Dot(delta, node.Forward);
            lateral = Vector3.Dot(delta, node.Right);
            return MathUtil.Wrap(node.Distance + along, Length);
        }

        /// <summary>Projects onto a shortcut path. Returns the main-road-equivalent distance.</summary>
        public float ProjectOntoShortcut(int shortcutIndex, Vector3 position, ref int hint, out float lateral, out TrackNode node)
        {
            lateral = 0f;
            node = null;
            if (shortcutIndex < 0 || shortcutIndex >= ShortcutNodes.Count) return 0f;
            var list = ShortcutNodes[shortcutIndex];
            if (list.Count == 0) return 0f;
            int best = 0;
            float bestDist = float.MaxValue;
            int from = hint >= 0 ? Mathf.Max(0, hint - 10) : 0;
            int to = hint >= 0 ? Mathf.Min(list.Count - 1, hint + 10) : list.Count - 1;
            for (int i = from; i <= to; i++)
            {
                float d = (list[i].Position - position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            node = list[best];
            hint = best;
            Vector3 delta = position - node.Position;
            lateral = Vector3.Dot(delta, node.Right);
            float along = Vector3.Dot(delta, node.Forward);
            float spacingEquivalent = list.Count > 1
                ? (list[list.Count - 1].MainDistanceEquivalent - list[0].MainDistanceEquivalent) / (list[list.Count - 1].Distance - list[0].Distance + 0.001f)
                : 1f;
            return MathUtil.Wrap(node.MainDistanceEquivalent + along * spacingEquivalent, Length);
        }

        /// <summary>Distance to the closest road edge at this node (negative = outside the road).</summary>
        public float DistanceToEdge(TrackNode node, float lateral)
        {
            return node.Width * 0.5f - Mathf.Abs(lateral);
        }

        // ------------------------------------------------------------------ Checkpoints

        public Checkpoint GetCheckpoint(int index)
        {
            int n = Checkpoints.Count;
            if (n == 0) return null;
            index %= n;
            if (index < 0) index += n;
            return Checkpoints[index];
        }

        /// <summary>Index of the last checkpoint at or before the distance.</summary>
        public int CheckpointIndexAtDistance(float distance)
        {
            int n = Checkpoints.Count;
            if (n == 0) return 0;
            float d = MathUtil.Wrap(distance, Length);
            int best = 0;
            for (int i = 0; i < n; i++)
            {
                if (Checkpoints[i].Distance <= d) best = i;
            }
            return best;
        }

        // ------------------------------------------------------------------ Grid

        /// <summary>Two columns behind the start line, leader first.</summary>
        public void GetGridSlot(int slot, out Vector3 position, out Quaternion rotation)
        {
            int row = slot / 2;
            float lateral = (slot % 2 == 0 ? -1f : 1f) * 2.6f;
            float distance = Length - 9f - row * 7f - (slot % 2) * 2.5f;
            GetPoseAtDistance(distance, lateral, out position, out rotation);
        }

        // ------------------------------------------------------------------ Debug

        private void OnDrawGizmosSelected()
        {
            if (Nodes == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var a = Nodes[i];
                var b = Nodes[(i + 1) % Nodes.Count];
                Gizmos.DrawLine(a.Position, b.Position);
            }
            Gizmos.color = Color.yellow;
            for (int s = 0; s < ShortcutNodes.Count; s++)
            {
                var list = ShortcutNodes[s];
                for (int i = 0; i < list.Count - 1; i++) Gizmos.DrawLine(list[i].Position, list[i + 1].Position);
            }
        }
    }
}
