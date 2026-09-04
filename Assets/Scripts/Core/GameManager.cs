using UnityEngine;
using VortexKarts.Audio;
using VortexKarts.Data;
using VortexKarts.VR;

namespace VortexKarts.Core
{
    /// <summary>
    /// Persistent root of the game. Owns the other persistent managers, the current race setup and
    /// the settings-apply pipeline. Any scene can call EnsureExists() so pressing Play inside a track
    /// scene works without going through Bootstrap.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string SceneBootstrap = "Bootstrap";
        public const string SceneMainMenu = "MainMenu";
        public const string SceneLoading = "Loading";

        public static GameManager Instance { get; private set; }

        public RaceSetup Setup { get; private set; } = new RaceSetup();
        public GameSettings Settings => SaveManager.Settings;
        public SceneLoader Loader { get; private set; }

        /// <summary>True while a race scene is paused (Time.timeScale = 0).</summary>
        public bool IsPaused { get; private set; }

        public static GameManager EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("GameManager");
            return go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            SaveManager.LoadSettings();
            SaveManager.LoadRecords();
            GameDatabase.EnsureLoaded();
            Setup.DifficultyIndex = Settings.difficultyIndex;
            Setup.Validate();

            InputManager.EnsureExists(transform);
            AudioManager.EnsureExists(transform);
            VRManager.EnsureExists(transform);
            Loader = GetComponent<SceneLoader>();
            if (Loader == null) Loader = gameObject.AddComponent<SceneLoader>();

            ApplySettings();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Pushes the current settings to every system and persists them.</summary>
        public void ApplySettings()
        {
            var s = Settings;
            s.ClampAll();
            Setup.DifficultyIndex = s.difficultyIndex;
            GraphicsSettingsApplier.Apply(s);
            if (AudioManager.Instance != null) AudioManager.Instance.ApplySettings(s);
            if (VRManager.Instance != null) VRManager.Instance.ApplySettings(s);
            GameEvents.RaiseSettingsApplied(s);
            SaveManager.SaveSettings();
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (paused && InputManager.Instance != null) InputManager.Instance.StopRumble();
            GameEvents.RaisePauseChanged(paused);
        }

        // ------------------------------------------------------------------ Flow

        public void OnBootstrapScene()
        {
            Loader.LoadScene(SceneMainMenu, false);
        }

        public void StartRace()
        {
            Setup.Validate();
            var track = Setup.Track;
            if (track == null)
            {
                Debug.LogError("[GameManager] No track selected.");
                return;
            }
            SetPaused(false);
            Loader.LoadScene(track.sceneName, true);
        }

        public void RestartRace()
        {
            var track = Setup.Track;
            if (track == null) return;
            SetPaused(false);
            Loader.LoadScene(track.sceneName, false);
        }

        public void ReturnToMenu()
        {
            SetPaused(false);
            Loader.LoadScene(SceneMainMenu, false);
        }

        public void QuitGame()
        {
            SaveManager.SaveSettings();
            SaveManager.SaveRecords();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationQuit()
        {
            SaveManager.SaveSettings();
            SaveManager.SaveRecords();
        }
    }
}
