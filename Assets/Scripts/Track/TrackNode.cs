using UnityEngine;
using VortexKarts.Data;

namespace VortexKarts.Track
{
    /// <summary>
    /// One sample of the racing line. Used by the AI (steering targets, speeds), the progress tracker
    /// (distance along the lap) and the mesh builder (cross sections).
    /// </summary>
    public class TrackNode
    {
        public int Index;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Right;
        public Vector3 Up = Vector3.up;
        public float Width;
        /// <summary>Distance from the start line along this node's own path.</summary>
        public float Distance;
        /// <summary>Signed curvature in 1/m. Positive = turning right.</summary>
        public float Curvature;
        /// <summary>Speed (m/s) a good driver would carry here.</summary>
        public float RecommendedSpeed;
        public TrackPointFlags Flags;
        /// <summary>No road under this node (jump gap).</summary>
        public bool IsGap;
        public int Segment;
        public float T;
        public bool IsShortcut;
        public int ShortcutIndex = -1;
        /// <summary>For shortcut nodes: equivalent progress distance on the main path.</summary>
        public float MainDistanceEquivalent;

        public Vector3 LeftEdge => Position - Right * (Width * 0.5f);
        public Vector3 RightEdge => Position + Right * (Width * 0.5f);

        public bool Has(TrackPointFlags f) => (Flags & f) != 0;

        public Vector3 PointAtLateral(float lateral)
        {
            return Position + Right * lateral;
        }
    }
}
