using System;
using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Data
{
    [Flags]
    public enum TrackPointFlags
    {
        None = 0,
        /// <summary>Road is removed for GapLength metres after this point; a kicker ramp is added before it.</summary>
        JumpGap = 1 << 0,
        NoWallLeft = 1 << 1,
        NoWallRight = 1 << 2,
        /// <summary>Segment starting at this point gets a ceiling and full-height walls.</summary>
        Tunnel = 1 << 3,
        /// <summary>Hint for the AI: good place to drift.</summary>
        DriftZone = 1 << 4,
        /// <summary>Hint for the AI: keep full throttle, boost pads nearby.</summary>
        BoostZone = 1 << 5,
        /// <summary>AI will not attempt overtakes here (narrow / dangerous).</summary>
        NoOvertake = 1 << 6,
        /// <summary>Road surface rendered semi-transparent (Sky Lab glass sections).</summary>
        GlassRoad = 1 << 7,
        NoWalls = NoWallLeft | NoWallRight
    }

    public enum TrackTheme
    {
        NeonMetro = 0,
        SolarCanyon = 1,
        SkyLab = 2
    }

    public enum HazardType
    {
        /// <summary>Barrier sliding sideways across part of the road.</summary>
        MovingBarrier = 0,
        /// <summary>Turbine pushing karts sideways while inside the volume.</summary>
        Fan = 1,
        /// <summary>Patch of road that caps speed.</summary>
        SlowZone = 2,
        /// <summary>Low-grip patch (ice, spilled coolant).</summary>
        SlipperyZone = 3
    }

    [Serializable]
    public class TrackControlPoint
    {
        public Vector3 position;
        public float width = 14f;
        public TrackPointFlags flags = TrackPointFlags.None;

        public TrackControlPoint() { }

        public TrackControlPoint(float x, float y, float z, float width, TrackPointFlags flags = TrackPointFlags.None)
        {
            position = new Vector3(x, y, z);
            this.width = width;
            this.flags = flags;
        }

        public bool Has(TrackPointFlags f) => (flags & f) != 0;
    }

    /// <summary>Position on the track expressed relative to a control point segment (robust to re-tuning).</summary>
    [Serializable]
    public struct TrackPlacement
    {
        public int segmentIndex;
        [Range(0f, 1f)] public float t;
        [Tooltip("Metres from the centre line. Negative = left.")]
        public float lateralOffset;

        public TrackPlacement(int segmentIndex, float t, float lateralOffset = 0f)
        {
            this.segmentIndex = segmentIndex;
            this.t = t;
            this.lateralOffset = lateralOffset;
        }
    }

    [Serializable]
    public class ShortcutDefinition
    {
        public string name = "Shortcut";
        [Tooltip("Control point index where the shortcut leaves the main road.")]
        public int entryPointIndex;
        [Tooltip("Control point index where the shortcut re-joins the main road.")]
        public int exitPointIndex;
        [Tooltip("Intermediate waypoints (world space). Entry/exit positions come from the main road.")]
        public List<Vector3> waypoints = new List<Vector3>();
        public float width = 7f;
        public bool wallsEnabled = true;
        public bool isTunnel = false;
        [Range(0f, 1f), Tooltip("How much the AI likes this route (personality risk taking scales it).")]
        public float aiPreference = 0.5f;
        [Tooltip("Speed recommended for the AI inside the shortcut, fraction of top speed.")]
        [Range(0.3f, 1f)] public float aiSpeedFraction = 0.75f;
    }

    [Serializable]
    public class BoostPadDefinition
    {
        public TrackPlacement placement;
        public float length = 6f;
        public float width = 4f;
        public float boostDuration = 1.3f;
        public float boostMultiplier = 1.28f;
    }

    [Serializable]
    public class PowerUpRowDefinition
    {
        public TrackPlacement placement;
        [Range(1, 6)] public int count = 4;
    }

    [Serializable]
    public class HazardDefinition
    {
        public HazardType type = HazardType.MovingBarrier;
        public TrackPlacement placement;
        public float size = 4f;
        [Tooltip("Barrier travel speed / fan push strength / slow factor depending on type.")]
        public float strength = 1f;
        [Tooltip("Barrier travel range in metres, fan volume length.")]
        public float range = 6f;
    }

    /// <summary>
    /// Complete description of a circuit. Geometry, AI data and gameplay objects are all generated from this at load.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex Karts/Track Data", fileName = "TrackData")]
    public class TrackData : ScriptableObject
    {
        [Header("Identity")]
        public string id = "neon_metro";
        public string displayName = "Metro Neón";
        public string sceneName = "Track_NeonCity";
        public TrackTheme theme = TrackTheme.NeonMetro;
        public string difficultyLabel = "FÁCIL / MEDIA";
        [TextArea] public string description = "";
        public int sortOrder = 0;
        public int laps = 3;
        public float expectedLapSeconds = 70f;

        [Header("Layout")]
        public List<TrackControlPoint> controlPoints = new List<TrackControlPoint>();
        public List<ShortcutDefinition> shortcuts = new List<ShortcutDefinition>();
        public List<BoostPadDefinition> boostPads = new List<BoostPadDefinition>();
        public List<PowerUpRowDefinition> powerUpRows = new List<PowerUpRowDefinition>();
        public List<HazardDefinition> hazards = new List<HazardDefinition>();

        [Header("Generation")]
        [Tooltip("Distance between AI/progress nodes.")]
        public float nodeSpacing = 4f;
        [Tooltip("Metres between checkpoints.")]
        public float checkpointSpacing = 110f;
        [Tooltip("Length of the hole created by JumpGap points.")]
        public float gapLength = 16f;
        [Tooltip("Height added by the kicker just before a JumpGap point.")]
        public float kickerHeight = 1.3f;
        public float wallHeight = 1.4f;
        public float tunnelHeight = 6.5f;
        [Tooltip("Karts below this world Y are respawned.")]
        public float killY = -25f;
        [Tooltip("Whether a large off-road ground plane exists under the whole track.")]
        public bool hasGroundPlane = true;
        public float groundPlaneY = -0.6f;

        [Header("Look")]
        public Color skyColor = new Color(0.05f, 0.05f, 0.15f);
        public Color horizonColor = new Color(0.2f, 0.1f, 0.35f);
        public Color fogColor = new Color(0.08f, 0.06f, 0.18f);
        public float fogDensity = 0.004f;
        public Color sunColor = new Color(0.7f, 0.75f, 1f);
        public float sunIntensity = 0.7f;
        public Vector3 sunDirection = new Vector3(40f, -30f, 0f);
        public Color ambientColor = new Color(0.25f, 0.25f, 0.4f);
        public Color roadColor = new Color(0.22f, 0.22f, 0.26f);
        public Color roadEdgeColor = new Color(0.1f, 0.9f, 1f);
        public Color wallColor = new Color(0.3f, 0.3f, 0.4f);
        public Color accentColor = new Color(1f, 0.2f, 0.8f);
        public Color groundColor = new Color(0.1f, 0.1f, 0.14f);

        [Header("Loading screen")]
        public List<string> tips = new List<string>();

        public int PointCount => controlPoints != null ? controlPoints.Count : 0;

        public TrackControlPoint GetPoint(int index)
        {
            int n = PointCount;
            if (n == 0) return null;
            index %= n;
            if (index < 0) index += n;
            return controlPoints[index];
        }

        public bool IsValid()
        {
            return controlPoints != null && controlPoints.Count >= 4;
        }
    }
}
