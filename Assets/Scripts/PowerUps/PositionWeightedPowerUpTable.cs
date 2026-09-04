using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// Picks a power-up with probabilities that depend on race position: the leader mostly gets defensive
    /// or mild items, the back of the pack gets offensive and recovery items. Weights come from PowerUpData.
    /// </summary>
    public static class PositionWeightedPowerUpTable
    {
        public static PowerUpData Roll(IReadOnlyList<PowerUpData> table, int position, int totalRacers, System.Random rng,
            bool forAI = false, PowerUpKind exclude = PowerUpKind.None)
        {
            if (table == null || table.Count == 0) return null;
            float fraction = totalRacers <= 1 ? 0f : Mathf.Clamp01((position - 1) / (float)(totalRacers - 1));

            float total = 0f;
            var weights = new float[table.Count];
            for (int i = 0; i < table.Count; i++)
            {
                var p = table[i];
                float w = p.GetWeight(fraction);
                if (forAI && !p.aiCanUse) w = 0f;
                if (p.kind == exclude) w *= 0.25f;
                if (p.kind == PowerUpKind.None) w = 0f;
                weights[i] = Mathf.Max(0f, w);
                total += weights[i];
            }
            if (total <= 0f) return table[0];

            double r = (rng != null ? rng.NextDouble() : Random.value) * total;
            for (int i = 0; i < table.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) return table[i];
            }
            return table[table.Count - 1];
        }
    }
}
