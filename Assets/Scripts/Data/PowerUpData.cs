using UnityEngine;

namespace VortexKarts.Data
{
    /// <summary>All power-up behaviours the game knows. Adding one: add a value, a PowerUpBase subclass, a PowerUpData asset.</summary>
    public enum PowerUpKind
    {
        None = 0,
        Turbo = 1,
        HomingMissile = 2,
        StraightShot = 3,
        Mine = 4,
        Shield = 5,
        EmpPulse = 6,
        OilSlick = 7,
        TripleShot = 8,
        TripleTurbo = 9,
        MegaBoost = 10
    }

    /// <summary>
    /// Data for one power-up: presentation, charges, tuning and position-weighted drop chances.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex Karts/Power-Up Data", fileName = "PowerUpData")]
    public class PowerUpData : ScriptableObject
    {
        [Header("Identity")]
        public string id = "turbo";
        public string displayName = "Turbo";
        [TextArea] public string description = "";
        public PowerUpKind kind = PowerUpKind.Turbo;
        public int sortOrder = 0;

        [Header("Behaviour")]
        [Tooltip("How many times the item can be used before it is consumed.")]
        public int charges = 1;
        [Tooltip("Duration in seconds for timed effects (boost, shield, stun).")]
        public float duration = 2f;
        [Tooltip("Generic strength: speed multiplier for boosts, radius for pulses, speed for projectiles.")]
        public float magnitude = 1.25f;
        [Tooltip("Secondary parameter: bounce count for projectiles, slow factor for hazards.")]
        public float secondary = 0f;
        public bool aiCanUse = true;

        [Header("Presentation")]
        public Color iconColor = new Color(1f, 0.6f, 0.1f);
        [Tooltip("Short label shown on the dashboard when icons are not available.")]
        public string shortLabel = "TURBO";

        [Header("Drop weights by position tier (0 = never)")]
        [Tooltip("Weight when the kart is in 1st place.")]
        public float weightLeader = 1f;
        [Tooltip("Weight for 2nd-3rd place.")]
        public float weightFront = 1f;
        [Tooltip("Weight for the middle of the pack.")]
        public float weightMid = 1f;
        [Tooltip("Weight for the last positions.")]
        public float weightBack = 1f;

        /// <summary>Interpolated weight for a normalised position (0 = leader, 1 = last).</summary>
        public float GetWeight(float positionFraction)
        {
            positionFraction = Mathf.Clamp01(positionFraction);
            // Tiers sit at 0, 0.3, 0.65, 1.
            if (positionFraction < 0.3f)
            {
                return Mathf.Lerp(weightLeader, weightFront, positionFraction / 0.3f);
            }
            if (positionFraction < 0.65f)
            {
                return Mathf.Lerp(weightFront, weightMid, (positionFraction - 0.3f) / 0.35f);
            }
            return Mathf.Lerp(weightMid, weightBack, (positionFraction - 0.65f) / 0.35f);
        }
    }
}
