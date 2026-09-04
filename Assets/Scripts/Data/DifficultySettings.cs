using UnityEngine;

namespace VortexKarts.Data
{
    /// <summary>
    /// Difficulty layer applied on top of AI personalities. Harder levels improve behaviour
    /// (lines, drifting, item use) rather than just increasing speed.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex Karts/Difficulty Settings", fileName = "DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        public string id = "normal";
        public string displayName = "Normal";
        [TextArea] public string description = "";
        public int sortOrder = 1;

        [Header("Speed envelope (kept close to the player's)")]
        [Range(0.7f, 1.05f)] public float topSpeedFactor = 0.96f;
        [Range(0.7f, 1.1f)] public float accelerationFactor = 0.95f;
        [Range(0.6f, 1.05f), Tooltip("Fraction of the ideal corner speed the AI is willing to carry.")]
        public float cornerSpeedFactor = 0.9f;

        [Header("Behaviour quality")]
        [Range(0f, 1f), Tooltip("Multiplies personality precision.")]
        public float precisionMultiplier = 0.8f;
        [Tooltip("Probability per second of a small driving mistake.")]
        public float mistakeChancePerSecond = 0.08f;
        [Tooltip("Steering error magnitude when a mistake happens (0..1).")]
        public float mistakeMagnitude = 0.35f;
        [Range(0f, 1f), Tooltip("How often the AI drifts in drift zones.")]
        public float driftUsage = 0.6f;
        [Range(0f, 1f), Tooltip("Scales personality power-up aggression.")]
        public float powerUpAggression = 0.7f;
        [Range(0f, 1f), Tooltip("Probability to take a shortcut when personality allows it.")]
        public float shortcutUsage = 0.5f;
        [Tooltip("Look-ahead time in seconds used to pick the steering target.")]
        public float lookAheadTime = 0.9f;

        [Header("Rubber banding (kept nearly invisible)")]
        [Range(0f, 0.08f), Tooltip("Maximum +/- top-speed adjustment based on distance to the player.")]
        public float rubberBandStrength = 0.03f;
        [Tooltip("Distance (metres) at which rubber banding saturates.")]
        public float rubberBandDistance = 150f;

        [Header("Player assists")]
        [Tooltip("Player start boost window length in seconds (perfect start tolerance).")]
        public float perfectStartWindow = 0.35f;
    }
}
