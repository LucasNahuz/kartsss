using UnityEngine;
using UnityEngine.SceneManagement;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Debugging;
using VortexKarts.PowerUps;
using VortexKarts.Track;
using VortexKarts.UI;
using VortexKarts.Utils;
using VortexKarts.VR;

namespace VortexKarts.Race
{
    /// <summary>
    /// Entry point of a track scene: generates the circuit, environment, race, power-ups and UI.
    /// </summary>
    public class TrackSceneController : MonoBehaviour
    {
        public string TrackId = "";

        public TrackRuntime Track { get; private set; }
        public RaceManager Race { get; private set; }

        private void Start()
        {
            var gm = GameManager.EnsureExists();
            GameDatabase.EnsureLoaded();

            TrackData data = null;
            if (!string.IsNullOrEmpty(TrackId)) data = GameDatabase.GetTrack(TrackId);
            if (data == null) data = GameDatabase.GetTrackBySceneName(SceneManager.GetActiveScene().name);
            if (data == null && GameDatabase.Tracks.Count > 0) data = GameDatabase.Tracks[0];
            if (data == null || !data.IsValid())
            {
                Debug.LogError("[TrackScene] No valid TrackData found for this scene.");
                return;
            }
            gm.Setup.TrackId = data.id;
            gm.Setup.Validate();

            // Track geometry + runtime data.
            var trackRoot = new GameObject("Track");
            Track = trackRoot.AddComponent<TrackRuntime>();
            TrackBuilder.Build(data, trackRoot.transform, Track);
            ApplyEnvironment(data);

            // Pools + power-ups.
            var pools = ObjectPoolManager.Instance;
            pools.transform.SetParent(null);
            var pum = new GameObject("PowerUpManager").AddComponent<PowerUpManager>();
            pum.Initialize(Track);

            // Race.
            Race = new GameObject("RaceManager").AddComponent<RaceManager>();
            Race.Initialize(gm.Setup, Track);

            // UI.
            RaceHUD.Create(Race);
            PauseMenu.Create(Race);
            ResultsScreen.Create(Race);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugRacePanel.Create(Race);
#endif
            GameEvents.OnSettingsApplied += OnSettingsApplied;
            OnSettingsApplied(SaveManager.Settings);
        }

        private void OnDestroy()
        {
            GameEvents.OnSettingsApplied -= OnSettingsApplied;
        }

        private void OnSettingsApplied(GameSettings s)
        {
            if (Track != null && Track.Sun != null)
            {
                Track.Sun.shadows = s.shadows > 0 ? LightShadows.Soft : LightShadows.None;
                Track.Sun.shadowStrength = s.shadows == 2 ? 0.8f : 0.6f;
            }
            if (VRManager.Instance != null && VRManager.Instance.MainCamera != null)
            {
                GraphicsSettingsApplier.ApplyToCamera(VRManager.Instance.MainCamera, s);
            }
        }

        private void ApplyEnvironment(TrackData data)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = data.ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = data.fogDensity;
            RenderSettings.skybox = null;
            if (VRManager.Instance != null) VRManager.Instance.SetBackgroundColor(data.skyColor);
        }
    }
}
