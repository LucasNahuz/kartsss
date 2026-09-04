using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;

namespace VortexKarts.Audio
{
    /// <summary>
    /// Persistent audio hub: category volumes (Master / Music / SFX), pooled one-shot sources with spatial
    /// audio for rivals, music per scene/theme and event-driven SFX for boosts, hits, power-ups, laps and UI.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int OneShotSources = 12;

        private readonly List<AudioSource> oneShots = new List<AudioSource>();
        private AudioSource musicSource;
        private float master = 0.9f, music = 0.65f, sfx = 1f;
        private int nextSource;

        public float SfxVolume => master * sfx;
        public float MusicVolume => master * music;

        public static AudioManager EnsureExists(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            for (int i = 0; i < OneShotSources; i++)
            {
                var src = new GameObject("OneShot_" + i).AddComponent<AudioSource>();
                src.transform.SetParent(transform, false);
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 4f;
                src.maxDistance = 90f;
                src.dopplerLevel = 0.2f;
                oneShots.Add(src);
            }
            musicSource = new GameObject("Music").AddComponent<AudioSource>();
            musicSource.transform.SetParent(transform, false);
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.ignoreListenerPause = true;

            GameEvents.OnBoostStarted += OnBoost;
            GameEvents.OnKartHit += OnHit;
            GameEvents.OnPowerUpCollected += OnPickup;
            GameEvents.OnPowerUpUsed += OnUse;
            GameEvents.OnShieldBlocked += OnShieldBlocked;
            GameEvents.OnLapCompleted += OnLap;
            GameEvents.OnCountdownTick += OnCountdown;
            GameEvents.OnKartRespawned += OnRespawn;
            GameEvents.OnKartLanded += OnLanded;
            GameEvents.OnDriftReleased += OnDriftReleased;
            GameEvents.OnKartWrongWay += OnWrongWay;
            GameEvents.OnKartFinished += OnFinished;
            GameEvents.OnUiClick += OnUiClick;
        }

        private void OnDestroy()
        {
            GameEvents.OnBoostStarted -= OnBoost;
            GameEvents.OnKartHit -= OnHit;
            GameEvents.OnPowerUpCollected -= OnPickup;
            GameEvents.OnPowerUpUsed -= OnUse;
            GameEvents.OnShieldBlocked -= OnShieldBlocked;
            GameEvents.OnLapCompleted -= OnLap;
            GameEvents.OnCountdownTick -= OnCountdown;
            GameEvents.OnKartRespawned -= OnRespawn;
            GameEvents.OnKartLanded -= OnLanded;
            GameEvents.OnDriftReleased -= OnDriftReleased;
            GameEvents.OnKartWrongWay -= OnWrongWay;
            GameEvents.OnKartFinished -= OnFinished;
            GameEvents.OnUiClick -= OnUiClick;
            if (Instance == this) Instance = null;
        }

        public void ApplySettings(GameSettings s)
        {
            master = s.masterVolume;
            music = s.musicVolume;
            sfx = s.sfxVolume;
            if (musicSource != null) musicSource.volume = MusicVolume * 0.6f;
            AudioListener.volume = 1f;
        }

        // ------------------------------------------------------------------ Playback

        public void Play2D(string clipName, float volume = 1f, float pitch = 1f)
        {
            var clip = ProceduralAudio.Get(clipName);
            if (clip == null) return;
            var src = NextSource();
            src.transform.position = Vector3.zero;
            src.spatialBlend = 0f;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume) * SfxVolume;
            src.clip = clip;
            src.Play();
        }

        public void PlayAt(string clipName, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var clip = ProceduralAudio.Get(clipName);
            if (clip == null) return;
            var src = NextSource();
            src.transform.position = position;
            src.spatialBlend = 1f;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(volume) * SfxVolume;
            src.clip = clip;
            src.Play();
        }

        /// <summary>Player sounds are 2D (stable in the headset); rivals are spatialised at their kart.</summary>
        private void PlayForKart(KartController kart, string clip, float volume, float pitch = 1f)
        {
            if (kart == null) return;
            if (kart.IsPlayer) Play2D(clip, volume, pitch);
            else PlayAt(clip, kart.Position, volume * 0.8f, pitch);
        }

        private AudioSource NextSource()
        {
            var src = oneShots[nextSource];
            nextSource = (nextSource + 1) % oneShots.Count;
            return src;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null) return;
            if (clip == null)
            {
                musicSource.Stop();
                return;
            }
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.volume = MusicVolume * 0.6f;
            musicSource.Play();
        }

        public void PlayMenuMusic() => PlayMusic(ProceduralAudio.Get("music_menu"));
        public void PlayTrackMusic(TrackTheme theme) => PlayMusic(ProceduralAudio.MusicFor(theme));
        public void StopMusic() => PlayMusic(null);

        // ------------------------------------------------------------------ Event handlers

        private void OnBoost(KartController k, BoostSource s, float strength)
        {
            if (s == BoostSource.Slipstream) return;
            string clip = s == BoostSource.MegaBoost ? "megaboost" : "boost";
            PlayForKart(k, clip, 0.5f + strength, 0.9f + strength * 0.3f);
        }

        private void OnHit(KartController k, KartHitKind kind, float strength)
        {
            switch (kind)
            {
                case KartHitKind.Wall: PlayForKart(k, "impact_soft", 0.3f + strength * 0.6f, 0.9f + strength * 0.2f); break;
                case KartHitKind.Kart: PlayForKart(k, "impact", 0.4f + strength * 0.5f); break;
                case KartHitKind.Emp: PlayForKart(k, "emp", 0.7f); break;
                case KartHitKind.OilSlick: PlayForKart(k, "drift_release", 0.4f, 0.6f); break;
                default: PlayForKart(k, "explosion", 0.8f); break;
            }
        }

        private void OnPickup(KartController k, PowerUpData d) => PlayForKart(k, "pickup", 0.6f);

        private void OnUse(KartController k, PowerUpData d)
        {
            if (d.kind == PowerUpKind.Shield) PlayForKart(k, "shield_on", 0.6f);
            else if (d.kind == PowerUpKind.Turbo || d.kind == PowerUpKind.TripleTurbo || d.kind == PowerUpKind.MegaBoost) return;
            else PlayForKart(k, "use", 0.6f);
        }

        private void OnShieldBlocked(KartController k) => PlayForKart(k, "shield_block", 0.8f);

        private void OnLap(KartController k, int lap, float time)
        {
            if (k.IsPlayer) Play2D("lap", 0.7f);
        }

        private void OnCountdown(int value)
        {
            if (value > 0) Play2D("countdown", 0.8f);
            else Play2D("go", 0.9f);
        }

        private void OnRespawn(KartController k)
        {
            if (k.IsPlayer) Play2D("respawn", 0.6f);
        }

        private void OnLanded(KartController k, float airTime)
        {
            if (airTime > 0.25f) PlayForKart(k, "land", Mathf.Clamp01(airTime) * 0.6f);
        }

        private void OnDriftReleased(KartController k, int level)
        {
            if (level > 0) PlayForKart(k, "drift_release", 0.4f + level * 0.15f, 0.8f + level * 0.15f);
        }

        private void OnWrongWay(KartController k, bool wrong)
        {
            if (k.IsPlayer && wrong) Play2D("wrongway", 0.6f);
        }

        private void OnFinished(KartController k)
        {
            if (k.IsPlayer) Play2D("finish", 0.9f);
        }

        private void OnUiClick(string id)
        {
            Play2D(id == "move" ? "ui_move" : "ui_click", 0.5f);
        }
    }
}
