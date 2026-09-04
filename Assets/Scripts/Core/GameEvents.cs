using System;
using VortexKarts.Data;
using VortexKarts.Kart;

namespace VortexKarts.Core
{
    /// <summary>
    /// Global gameplay events. Audio, UI, haptics and VFX subscribe here so gameplay code never
    /// references presentation systems directly.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<KartController, BoostSource, float> OnBoostStarted;
        public static event Action<KartController, int> OnDriftLevelChanged;
        public static event Action<KartController, int> OnDriftReleased;
        public static event Action<KartController, PowerUpData> OnPowerUpCollected;
        public static event Action<KartController, PowerUpData> OnPowerUpUsed;
        public static event Action<KartController, KartHitKind, float> OnKartHit;
        public static event Action<KartController> OnShieldBlocked;
        public static event Action<KartController> OnKartRespawned;
        public static event Action<KartController, bool> OnKartWrongWay;
        public static event Action<KartController, int, float> OnLapCompleted;
        public static event Action<KartController, int> OnPositionChanged;
        public static event Action<KartController> OnKartFinished;
        public static event Action<KartController, float> OnKartLanded;
        public static event Action<int> OnCountdownTick;
        public static event Action OnRaceStarted;
        public static event Action OnRaceFinished;
        public static event Action<bool> OnPauseChanged;
        public static event Action OnGamepadDisconnected;
        public static event Action OnGamepadReconnected;
        public static event Action<bool> OnHeadTrackingChanged;
        public static event Action<GameSettings> OnSettingsApplied;
        public static event Action<string> OnUiClick;

        public static void RaiseBoostStarted(KartController k, BoostSource s, float strength) => OnBoostStarted?.Invoke(k, s, strength);
        public static void RaiseDriftLevelChanged(KartController k, int level) => OnDriftLevelChanged?.Invoke(k, level);
        public static void RaiseDriftReleased(KartController k, int level) => OnDriftReleased?.Invoke(k, level);
        public static void RaisePowerUpCollected(KartController k, PowerUpData d) => OnPowerUpCollected?.Invoke(k, d);
        public static void RaisePowerUpUsed(KartController k, PowerUpData d) => OnPowerUpUsed?.Invoke(k, d);
        public static void RaiseKartHit(KartController k, KartHitKind kind, float strength) => OnKartHit?.Invoke(k, kind, strength);
        public static void RaiseShieldBlocked(KartController k) => OnShieldBlocked?.Invoke(k);
        public static void RaiseKartRespawned(KartController k) => OnKartRespawned?.Invoke(k);
        public static void RaiseKartWrongWay(KartController k, bool wrong) => OnKartWrongWay?.Invoke(k, wrong);
        public static void RaiseLapCompleted(KartController k, int lap, float lapTime) => OnLapCompleted?.Invoke(k, lap, lapTime);
        public static void RaisePositionChanged(KartController k, int pos) => OnPositionChanged?.Invoke(k, pos);
        public static void RaiseKartFinished(KartController k) => OnKartFinished?.Invoke(k);
        public static void RaiseKartLanded(KartController k, float airTime) => OnKartLanded?.Invoke(k, airTime);
        public static void RaiseCountdownTick(int value) => OnCountdownTick?.Invoke(value);
        public static void RaiseRaceStarted() => OnRaceStarted?.Invoke();
        public static void RaiseRaceFinished() => OnRaceFinished?.Invoke();
        public static void RaisePauseChanged(bool paused) => OnPauseChanged?.Invoke(paused);
        public static void RaiseGamepadDisconnected() => OnGamepadDisconnected?.Invoke();
        public static void RaiseGamepadReconnected() => OnGamepadReconnected?.Invoke();
        public static void RaiseHeadTrackingChanged(bool tracked) => OnHeadTrackingChanged?.Invoke(tracked);
        public static void RaiseSettingsApplied(GameSettings s) => OnSettingsApplied?.Invoke(s);
        public static void RaiseUiClick(string id) => OnUiClick?.Invoke(id);
    }
}
