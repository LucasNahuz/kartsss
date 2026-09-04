using UnityEngine;

namespace VortexKarts.Data
{
    public enum HelmetStyle
    {
        Round = 0,
        Visor = 1,
        Crest = 2,
        Antenna = 3
    }

    /// <summary>
    /// A stylised pilot: colours, helmet shape and which AI personality drives them when they are CPU controlled.
    /// All eight pilots share the same procedural rig.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex Karts/Pilot Data", fileName = "PilotData")]
    public class PilotData : ScriptableObject
    {
        public string id = "vex";
        public string displayName = "Vex";
        [TextArea] public string bio = "";
        public int sortOrder = 0;

        [Header("Look")]
        public Color primaryColor = new Color(0.9f, 0.2f, 0.2f);
        public Color secondaryColor = new Color(1f, 0.9f, 0.3f);
        public Color skinColor = new Color(0.95f, 0.75f, 0.6f);
        public HelmetStyle helmet = HelmetStyle.Round;

        [Header("Defaults")]
        [Tooltip("AIPersonality id used when this pilot is CPU controlled.")]
        public string personalityId = "balanced";
        [Tooltip("KartStats id this pilot drives by default.")]
        public string defaultKartId = "balanced";
    }
}
