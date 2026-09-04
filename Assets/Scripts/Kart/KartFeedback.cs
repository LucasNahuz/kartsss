using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Utils;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Particles and haptics for one kart. Listens to global events filtered to its own kart.
    /// Audio lives in VehicleAudio. Nothing here moves the VR camera.
    /// </summary>
    public class KartFeedback : MonoBehaviour
    {
        private KartController kart;
        private ParticleSystem[] driftFx = new ParticleSystem[0];
        private ParticleSystem[] boostFx = new ParticleSystem[0];
        private ParticleSystem impactFx;
        private ParticleSystem stunFx;
        private ParticleSystem slipFx;
        private GameObject shieldBubble;
        private GameObject armorGlow;
        private Material shieldMaterial;
        private bool effectsEnabled = true;
        private float driftRumbleTimer;

        private static readonly Color[] DriftColors =
        {
            new Color(0.85f, 0.85f, 0.85f, 0.6f), // level 0: light smoke
            new Color(0.2f, 0.9f, 1f, 0.9f),     // level 1: cyan
            new Color(1f, 0.6f, 0.15f, 0.95f),   // level 2: orange
            new Color(1f, 0.25f, 0.85f, 1f)      // level 3: magenta
        };

        public void Build(KartController controller)
        {
            kart = controller;
            effectsEnabled = SaveManager.Settings.effects;

            // Drift sparks at the rear wheels, pointing backwards and slightly outwards.
            driftFx = new ParticleSystem[kart.RearWheelAnchors.Length];
            for (int i = 0; i < kart.RearWheelAnchors.Length; i++)
            {
                var anchor = kart.RearWheelAnchors[i];
                driftFx[i] = VfxFactory.CreateStream("DriftFX", anchor, Vector3.zero, Quaternion.Euler(-10f, 180f, 0f),
                    DriftColors[0], 0.35f, 4f, 0.45f, 0f, 25f, 120);
            }

            boostFx = new ParticleSystem[kart.ExhaustAnchors.Length];
            for (int i = 0; i < kart.ExhaustAnchors.Length; i++)
            {
                var anchor = kart.ExhaustAnchors[i];
                boostFx[i] = VfxFactory.CreateStream("BoostFX", anchor, Vector3.zero, Quaternion.Euler(0f, 180f, 0f),
                    new Color(1f, 0.55f, 0.1f, 1f), 0.3f, 9f, 0.35f, 0f, 8f, 120);
            }

            impactFx = VfxFactory.CreateBurst("ImpactFX", kart.Orientation, new Vector3(0f, 0.2f, 0f),
                new Color(1f, 0.9f, 0.6f, 1f), 0.3f, 6f, 0.5f, 60, 0.4f);

            stunFx = VfxFactory.CreateStream("StunFX", kart.Orientation, new Vector3(0f, 0.9f, -0.2f), Quaternion.Euler(-90f, 0f, 0f),
                new Color(0.7f, 0.4f, 1f, 1f), 0.15f, 1.5f, 0.6f, 0f, 60f, 60);

            slipFx = VfxFactory.CreateStream("SlipFX", kart.Orientation, new Vector3(0f, -0.4f, -0.6f), Quaternion.Euler(-70f, 180f, 0f),
                new Color(0.5f, 1f, 0.3f, 0.8f), 0.3f, 2f, 0.5f, 0f, 40f, 60);

            // Shield bubble.
            shieldMaterial = MaterialLibrary.UnlitTransparent(new Color(0.4f, 0.7f, 1f, 0.28f));
            shieldBubble = PrimitiveFactory.Sphere("ShieldBubble", kart.Orientation, new Vector3(0f, 0.15f, 0f), 2.9f, shieldMaterial);
            PrimitiveFactory.SetShadowCasting(shieldBubble, false, false);
            shieldBubble.SetActive(false);

            armorGlow = PrimitiveFactory.Sphere("ArmorGlow", kart.Orientation, new Vector3(0f, 0.15f, 0f), 2.6f,
                MaterialLibrary.UnlitTransparent(new Color(1f, 0.3f, 0.9f, 0.18f)));
            PrimitiveFactory.SetShadowCasting(armorGlow, false, false);
            armorGlow.SetActive(false);

            GameEvents.OnDriftLevelChanged += OnDriftLevelChanged;
            GameEvents.OnDriftReleased += OnDriftReleased;
            GameEvents.OnBoostStarted += OnBoostStarted;
            GameEvents.OnKartHit += OnKartHit;
            GameEvents.OnKartLanded += OnKartLanded;
            GameEvents.OnPowerUpCollected += OnPowerUpCollected;
            GameEvents.OnShieldBlocked += OnShieldBlocked;
            GameEvents.OnSettingsApplied += OnSettingsApplied;
            kart.Status.OnEffectChanged += OnEffectChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnDriftLevelChanged -= OnDriftLevelChanged;
            GameEvents.OnDriftReleased -= OnDriftReleased;
            GameEvents.OnBoostStarted -= OnBoostStarted;
            GameEvents.OnKartHit -= OnKartHit;
            GameEvents.OnKartLanded -= OnKartLanded;
            GameEvents.OnPowerUpCollected -= OnPowerUpCollected;
            GameEvents.OnShieldBlocked -= OnShieldBlocked;
            GameEvents.OnSettingsApplied -= OnSettingsApplied;
            if (kart != null && kart.Status != null) kart.Status.OnEffectChanged -= OnEffectChanged;
        }

        private void OnSettingsApplied(GameSettings s)
        {
            effectsEnabled = s.effects;
        }

        private void Rumble(float low, float high, float duration)
        {
            if (kart == null || !kart.IsPlayer || InputManager.Instance == null) return;
            InputManager.Instance.Rumble(low, high, duration);
        }

        private void OnDriftLevelChanged(KartController k, int level)
        {
            if (k != kart) return;
            if (level >= 1) Rumble(0.25f, 0.6f, 0.12f);
        }

        private void OnDriftReleased(KartController k, int level)
        {
            if (k != kart) return;
            if (level > 0) Rumble(0.5f + level * 0.12f, 0.8f, 0.25f);
        }

        private void OnBoostStarted(KartController k, BoostSource source, float strength)
        {
            if (k != kart) return;
            if (source == BoostSource.Slipstream) return;
            Rumble(0.4f + strength, 0.7f, 0.2f + strength * 0.4f);
            if (effectsEnabled && impactFx != null && source != BoostSource.Landing)
            {
                VfxFactory.SetColor(impactFx, source == BoostSource.MegaBoost ? new Color(1f, 0.3f, 0.9f) : new Color(1f, 0.7f, 0.2f));
                impactFx.Emit(Mathf.RoundToInt(8 + strength * 30f));
            }
        }

        private void OnKartHit(KartController k, KartHitKind kind, float strength)
        {
            if (k != kart) return;
            switch (kind)
            {
                case KartHitKind.Wall:
                    Rumble(strength * 0.9f, strength * 0.5f, 0.12f + strength * 0.2f);
                    break;
                case KartHitKind.Kart:
                    Rumble(strength * 0.7f, strength * 0.9f, 0.15f);
                    break;
                case KartHitKind.OilSlick:
                    Rumble(0.2f, 0.5f, 0.3f);
                    break;
                case KartHitKind.Emp:
                    Rumble(0.15f, 0.9f, 0.6f);
                    break;
                default:
                    Rumble(1f, 1f, 0.35f);
                    break;
            }
            if (effectsEnabled && impactFx != null && (strength > 0.25f || kind == KartHitKind.Projectile || kind == KartHitKind.Mine))
            {
                VfxFactory.SetColor(impactFx, kind == KartHitKind.Wall ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.5f, 0.3f));
                impactFx.Emit(Mathf.RoundToInt(10 + strength * 35f));
            }
        }

        private void OnKartLanded(KartController k, float airTime)
        {
            if (k != kart) return;
            if (airTime > 0.25f) Rumble(Mathf.Clamp01(airTime * 0.6f), 0.3f, 0.12f);
        }

        private void OnPowerUpCollected(KartController k, Data.PowerUpData d)
        {
            if (k != kart) return;
            Rumble(0.15f, 0.45f, 0.1f);
        }

        private void OnShieldBlocked(KartController k)
        {
            if (k != kart) return;
            Rumble(0.5f, 0.9f, 0.2f);
            if (effectsEnabled && impactFx != null)
            {
                VfxFactory.SetColor(impactFx, new Color(0.5f, 0.8f, 1f));
                impactFx.Emit(30);
            }
        }

        private void OnEffectChanged(StatusEffect effect, bool active)
        {
            switch (effect)
            {
                case StatusEffect.Shield:
                    if (shieldBubble != null) shieldBubble.SetActive(active);
                    break;
                case StatusEffect.Armor:
                    if (armorGlow != null) armorGlow.SetActive(active);
                    break;
                case StatusEffect.Stunned:
                    VfxFactory.SetRate(stunFx, active && effectsEnabled ? 25f : 0f);
                    break;
                case StatusEffect.Slippery:
                    VfxFactory.SetRate(slipFx, active && effectsEnabled ? 20f : 0f);
                    break;
            }
        }

        private void Update()
        {
            if (kart == null || kart.Stats == null) return;
            float dt = Time.deltaTime;

            // Drift particles.
            bool drifting = kart.Drift.IsDrifting && kart.IsGrounded;
            int level = Mathf.Clamp(kart.Drift.Level, 0, 3);
            float rate = 0f;
            if (drifting && effectsEnabled)
            {
                rate = 25f + level * 30f + kart.Drift.LevelProgress * 15f;
            }
            else if (drifting)
            {
                rate = 15f;
            }
            Color driftColor = DriftColors[level];
            for (int i = 0; i < driftFx.Length; i++)
            {
                VfxFactory.SetRate(driftFx[i], rate);
                VfxFactory.SetColor(driftFx[i], driftColor);
            }

            // Continuous drift rumble for the player, growing with level.
            if (drifting && kart.IsPlayer)
            {
                driftRumbleTimer -= dt;
                if (driftRumbleTimer <= 0f)
                {
                    driftRumbleTimer = 0.1f;
                    Rumble(0.08f + level * 0.08f, 0.05f + level * 0.05f, 0.12f);
                }
            }

            // Boost flames.
            float intensity = kart.Boost.Intensity;
            float boostRate = intensity > 0.02f ? (effectsEnabled ? 40f + intensity * 90f : 25f) : 0f;
            Color boostColor = kart.Boost.StrongestSource == BoostSource.MegaBoost
                ? new Color(1f, 0.3f, 0.9f, 1f)
                : Color.Lerp(new Color(1f, 0.6f, 0.15f, 1f), new Color(0.4f, 0.8f, 1f, 1f), Mathf.Clamp01(intensity - 0.5f) * 2f);
            for (int i = 0; i < boostFx.Length; i++)
            {
                VfxFactory.SetRate(boostFx[i], boostRate);
                VfxFactory.SetColor(boostFx[i], boostColor);
                VfxFactory.SetSpeed(boostFx[i], 6f + intensity * 10f);
            }

            // Shield pulse.
            if (shieldBubble != null && shieldBubble.activeSelf && shieldMaterial != null)
            {
                float remaining = kart.Status.Remaining(StatusEffect.Shield);
                float pulse = 0.22f + Mathf.Sin(Time.time * 6f) * 0.05f;
                if (remaining < 1.5f) pulse *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
                var c = new Color(0.4f, 0.7f, 1f, pulse);
                if (shieldMaterial.HasProperty("_BaseColor")) shieldMaterial.SetColor("_BaseColor", c);
                else shieldMaterial.color = c;
            }
        }
    }
}
