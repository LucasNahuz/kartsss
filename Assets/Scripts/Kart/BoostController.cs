using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Central boost stack. Drift, power-ups, pads, landings and the race start all feed into it.
    /// Multipliers do not add up: the strongest active boost wins, with a small bonus per extra source
    /// so chaining (drift into pad) still feels rewarding without exploding.
    /// </summary>
    public class BoostController : MonoBehaviour
    {
        private class ActiveBoost
        {
            public BoostSource Source;
            public float SpeedMultiplier;
            public float AccelMultiplier;
            public float EndTime;
            public float Duration;
        }

        private readonly List<ActiveBoost> active = new List<ActiveBoost>(8);
        private KartController kart;

        public float MaxSpeedMultiplier { get; private set; } = 1f;
        public float AccelMultiplier { get; private set; } = 1f;
        public bool IsBoosting => active.Count > 0;
        /// <summary>0..1 value for FX; peaks with strong boosts and fades near the end.</summary>
        public float Intensity { get; private set; }
        public BoostSource StrongestSource { get; private set; }

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        /// <summary>
        /// Adds a boost. speedMultiplier is relative to top speed (1.25 = +25%). The kart's boostPower stat
        /// scales the bonus part of the multiplier.
        /// </summary>
        public void AddBoost(BoostSource source, float speedMultiplier, float duration, float accelMultiplier = 1.8f)
        {
            if (duration <= 0f) return;
            float power = kart != null && kart.Stats != null ? kart.Stats.boostPower : 1f;
            float bonus = (speedMultiplier - 1f) * power;
            var b = new ActiveBoost
            {
                Source = source,
                SpeedMultiplier = 1f + Mathf.Max(0f, bonus),
                AccelMultiplier = Mathf.Max(1f, accelMultiplier),
                Duration = duration,
                EndTime = Time.time + duration
            };
            active.Add(b);
            Recompute();
            GameEvents.RaiseBoostStarted(kart, source, bonus);
        }

        public void ClearAll()
        {
            active.Clear();
            Recompute();
        }

        public bool HasSource(BoostSource source)
        {
            for (int i = 0; i < active.Count; i++) if (active[i].Source == source) return true;
            return false;
        }

        private void Update()
        {
            bool changed = false;
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (now >= active[i].EndTime)
                {
                    active.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed || active.Count > 0) Recompute();
        }

        private void Recompute()
        {
            if (active.Count == 0)
            {
                MaxSpeedMultiplier = 1f;
                AccelMultiplier = 1f;
                Intensity = 0f;
                return;
            }
            float bestSpeed = 1f, bestAccel = 1f, intensity = 0f;
            BoostSource strongest = active[0].Source;
            float now = Time.time;
            for (int i = 0; i < active.Count; i++)
            {
                var b = active[i];
                if (b.SpeedMultiplier > bestSpeed)
                {
                    bestSpeed = b.SpeedMultiplier;
                    strongest = b.Source;
                }
                bestAccel = Mathf.Max(bestAccel, b.AccelMultiplier);
                float remaining = Mathf.Clamp01((b.EndTime - now) / Mathf.Max(0.01f, b.Duration));
                float fade = remaining < 0.25f ? remaining / 0.25f : 1f;
                intensity = Mathf.Max(intensity, Mathf.Clamp01((b.SpeedMultiplier - 1f) * 2.5f) * fade);
            }
            // Chaining bonus: +2% per additional source, capped.
            float chain = Mathf.Min(0.06f, (active.Count - 1) * 0.02f);
            MaxSpeedMultiplier = bestSpeed + chain;
            AccelMultiplier = bestAccel;
            Intensity = intensity;
            StrongestSource = strongest;
        }
    }
}
