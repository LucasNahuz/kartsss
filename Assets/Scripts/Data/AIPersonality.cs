using UnityEngine;

namespace VortexKarts.Data
{
    /// <summary>
    /// Behaviour profile of a CPU driver. Every value is 0..1 and scales AI decisions; difficulty layers on top.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex Karts/AI Personality", fileName = "AIPersonality")]
    public class AIPersonality : ScriptableObject
    {
        public string id = "balanced";
        public string displayName = "Balanced";
        [TextArea] public string description = "";
        public int sortOrder = 0;

        [Range(0f, 1f), Tooltip("Willingness to bump rivals and take tight gaps.")]
        public float aggression = 0.5f;
        [Range(0f, 1f), Tooltip("How closely the racing line is followed.")]
        public float precision = 0.5f;
        [Range(0f, 1f), Tooltip("How eagerly power-ups are fired.")]
        public float powerUpUsage = 0.5f;
        [Range(0f, 1f), Tooltip("Probability of attempting an overtake when close behind someone.")]
        public float overtakeTendency = 0.5f;
        [Range(0f, 1f), Tooltip("Taking shortcuts, late braking, drifting on the limit.")]
        public float riskTaking = 0.5f;
        [Range(0f, 1f), Tooltip("Quality and frequency of drifts.")]
        public float driftSkill = 0.5f;
        [Range(0f, 1f), Tooltip("Blocks the kart behind when it is close.")]
        public float defendTendency = 0.5f;
        [Range(0f, 1f), Tooltip("Random wobble in the chosen line. Chaotic drivers wander.")]
        public float lineVariance = 0.3f;
        [Tooltip("Seconds of delay before reacting to new situations.")]
        public float reactionTime = 0.25f;
    }
}
