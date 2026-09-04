using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.Audio
{
    /// <summary>
    /// Engine and skid sounds for one kart. Engine pitch follows simulated RPM (speed + throttle),
    /// drift loop follows drift level. The player's engine is 2D; rivals are spatialised.
    /// </summary>
    public class VehicleAudio : MonoBehaviour
    {
        private KartController kart;
        private AudioSource engine;
        private AudioSource drift;
        private float rpm;
        private float driftVol;

        public void Build(KartController controller)
        {
            kart = controller;
            engine = CreateSource("EngineAudio", ProceduralAudio.Get("engine"));
            drift = CreateSource("DriftAudio", ProceduralAudio.Get("drift"));
            engine.Play();
            drift.Play();
            drift.volume = 0f;
        }

        private AudioSource CreateSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(kart.Orientation, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.8f);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = kart.IsPlayer ? 0.15f : 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 5f;
            src.maxDistance = 70f;
            src.dopplerLevel = kart.IsPlayer ? 0f : 0.35f;
            src.volume = 0f;
            return src;
        }

        private void Update()
        {
            if (kart == null || kart.Stats == null || engine == null) return;
            float dt = Time.deltaTime;
            float sfx = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;

            float speedFrac = Mathf.Clamp01(Mathf.Abs(kart.ForwardSpeed) / Mathf.Max(1f, kart.Stats.maxSpeed));
            float throttle = kart.EffectiveThrottle;
            bool revving = kart.InputLocked && kart.Input.Throttle > 0.3f;
            float targetRpm = Mathf.Clamp01(speedFrac * 0.8f + throttle * 0.2f);
            if (revving) targetRpm = 0.55f + Mathf.Sin(Time.time * 20f) * 0.05f;
            if (!kart.IsGrounded) targetRpm = Mathf.Max(targetRpm, 0.6f + throttle * 0.3f);
            rpm = MathUtil.Damp(rpm, targetRpm, throttle > 0.1f ? 3.5f : 5f, dt);

            float boost = kart.Boost.Intensity;
            engine.pitch = 0.55f + rpm * 1.15f + boost * 0.25f + (kart.Status.IsSpinning ? Mathf.Sin(Time.time * 25f) * 0.1f : 0f);
            float baseVol = kart.IsPlayer ? 0.32f : 0.42f;
            engine.volume = (baseVol + rpm * 0.2f) * sfx;

            float driftTarget = 0f;
            if (kart.Drift.IsDrifting && kart.IsGrounded) driftTarget = 0.25f + kart.Drift.Level * 0.12f;
            else if (kart.Status.Has(StatusEffect.Slippery) && kart.IsGrounded && kart.Speed > 5f) driftTarget = 0.2f;
            driftVol = MathUtil.Damp(driftVol, driftTarget, driftTarget > driftVol ? 12f : 6f, dt);
            drift.volume = driftVol * (kart.IsPlayer ? 0.7f : 0.5f) * sfx;
            drift.pitch = 0.9f + kart.Drift.Level * 0.12f + speedFrac * 0.2f;
        }
    }
}
