using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Timed status effects (spin-out, slow, slippery, stun, shield, invulnerability, armor) and the
    /// single entry point for "something hit this kart" so shield / invulnerability rules live in one place.
    /// </summary>
    public class KartStatusEffects : MonoBehaviour
    {
        private readonly Dictionary<StatusEffect, float> endTimes = new Dictionary<StatusEffect, float>();
        private readonly Dictionary<StatusEffect, float> startTimes = new Dictionary<StatusEffect, float>();
        private KartController kart;

        public float SpeedMultiplier { get; private set; } = 1f;
        public float AccelMultiplier { get; private set; } = 1f;
        public float GripMultiplier { get; private set; } = 1f;
        public float SteerMultiplier { get; private set; } = 1f;
        /// <summary>True while the kart cannot be controlled (spin-out).</summary>
        public bool ControlsLocked { get; private set; }
        /// <summary>Extra yaw applied while spinning out, degrees per second.</summary>
        public float SpinYawRate { get; private set; }
        public bool HasShield => Has(StatusEffect.Shield);
        public bool IsInvulnerable => Has(StatusEffect.Invulnerable);
        public bool HasArmor => Has(StatusEffect.Armor);
        public bool IsStunned => Has(StatusEffect.Stunned);
        public bool IsSpinning => Has(StatusEffect.SpinOut);

        public event System.Action<StatusEffect, bool> OnEffectChanged;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        public bool Has(StatusEffect effect)
        {
            float end;
            return endTimes.TryGetValue(effect, out end) && Time.time < end;
        }

        public float Remaining(StatusEffect effect)
        {
            float end;
            return endTimes.TryGetValue(effect, out end) ? Mathf.Max(0f, end - Time.time) : 0f;
        }

        public float Progress(StatusEffect effect)
        {
            float end, start;
            if (!endTimes.TryGetValue(effect, out end) || !startTimes.TryGetValue(effect, out start)) return 1f;
            float dur = Mathf.Max(0.001f, end - start);
            return Mathf.Clamp01((Time.time - start) / dur);
        }

        public void Apply(StatusEffect effect, float duration)
        {
            if (duration <= 0f) return;
            bool wasActive = Has(effect);
            float newEnd = Time.time + duration;
            float end;
            if (endTimes.TryGetValue(effect, out end) && end > newEnd && wasActive) return; // keep longer one
            endTimes[effect] = newEnd;
            startTimes[effect] = Time.time;
            Recompute();
            if (!wasActive) OnEffectChanged?.Invoke(effect, true);
        }

        public void Clear(StatusEffect effect)
        {
            if (!endTimes.ContainsKey(effect)) return;
            bool wasActive = Has(effect);
            endTimes.Remove(effect);
            startTimes.Remove(effect);
            Recompute();
            if (wasActive) OnEffectChanged?.Invoke(effect, false);
        }

        public void ClearAll()
        {
            var keys = new List<StatusEffect>(endTimes.Keys);
            for (int i = 0; i < keys.Count; i++) Clear(keys[i]);
        }

        private void Update()
        {
            // Detect expirations so listeners (FX) get the "off" notification.
            bool changed = false;
            var keys = new List<StatusEffect>(endTimes.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                if (Time.time >= endTimes[k])
                {
                    endTimes.Remove(k);
                    startTimes.Remove(k);
                    OnEffectChanged?.Invoke(k, false);
                    changed = true;
                }
            }
            if (changed || endTimes.Count > 0) Recompute();
        }

        private void Recompute()
        {
            SpeedMultiplier = 1f;
            AccelMultiplier = 1f;
            GripMultiplier = 1f;
            SteerMultiplier = 1f;
            ControlsLocked = false;
            SpinYawRate = 0f;

            if (Has(StatusEffect.SpinOut))
            {
                ControlsLocked = true;
                float p = Progress(StatusEffect.SpinOut);
                // Two full turns over the effect, easing out.
                SpinYawRate = Mathf.Lerp(720f, 90f, p);
                AccelMultiplier *= 0.2f;
            }
            if (Has(StatusEffect.Slow))
            {
                SpeedMultiplier *= 0.6f;
            }
            if (Has(StatusEffect.Slippery))
            {
                GripMultiplier *= 0.22f;
                SteerMultiplier *= 0.8f;
            }
            if (Has(StatusEffect.Stunned))
            {
                AccelMultiplier *= 0.4f;
                SpeedMultiplier *= 0.85f;
            }
        }

        /// <summary>
        /// Resolves an incoming hit. Returns true when the effect was applied, false if blocked
        /// (shield, invulnerability, mega-boost armor). Applies the standard consequences per hit kind.
        /// </summary>
        public bool TryHit(KartHitKind kind, float strength, Vector3 sourcePosition)
        {
            if (IsInvulnerable) return false;

            bool controlLoss = kind == KartHitKind.Projectile || kind == KartHitKind.Mine ||
                               kind == KartHitKind.OilSlick || kind == KartHitKind.Hazard || kind == KartHitKind.Emp;

            if (controlLoss && HasShield)
            {
                Clear(StatusEffect.Shield);
                GameEvents.RaiseShieldBlocked(kart);
                return false;
            }

            if (controlLoss && HasArmor)
            {
                // Mega boost shrugs off control loss; only a tiny speed dent.
                if (kart != null) kart.ScaleSpeed(0.9f);
                return false;
            }

            switch (kind)
            {
                case KartHitKind.Projectile:
                    Apply(StatusEffect.SpinOut, 1.2f * Mathf.Max(0.5f, strength));
                    if (kart != null) kart.ScaleSpeed(0.35f);
                    break;
                case KartHitKind.Mine:
                    Apply(StatusEffect.SpinOut, 1.2f * Mathf.Max(0.5f, strength));
                    Apply(StatusEffect.Slow, 2.2f);
                    if (kart != null)
                    {
                        kart.ScaleSpeed(0.3f);
                        kart.AddVerticalKick(3.5f);
                    }
                    break;
                case KartHitKind.OilSlick:
                    Apply(StatusEffect.Slippery, 2.5f * Mathf.Max(0.4f, strength));
                    if (kart != null) kart.AddYawKick(Random.value < 0.5f ? -28f : 28f);
                    break;
                case KartHitKind.Emp:
                    Apply(StatusEffect.Stunned, 2.5f * Mathf.Max(0.4f, strength));
                    break;
                case KartHitKind.Hazard:
                    Apply(StatusEffect.SpinOut, 0.8f);
                    if (kart != null) kart.ScaleSpeed(0.45f);
                    break;
                case KartHitKind.Wall:
                case KartHitKind.Kart:
                    // Physical hits are handled by KartController; nothing to apply here.
                    break;
            }
            if (kart != null && kart.Drift != null && controlLoss) kart.Drift.Cancel();
            GameEvents.RaiseKartHit(kart, kind, strength);
            return true;
        }
    }
}
