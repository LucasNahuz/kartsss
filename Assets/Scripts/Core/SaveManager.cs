using System;
using System.IO;
using UnityEngine;

namespace VortexKarts.Core
{
    /// <summary>
    /// JSON persistence for settings and records in Application.persistentDataPath.
    /// JSON was chosen over PlayerPrefs so the whole settings block can be inspected, backed up and versioned.
    /// </summary>
    public static class SaveManager
    {
        private const string SettingsFile = "settings.json";
        private const string RecordsFile = "records.json";

        private static GameSettings settings;
        private static RecordsData records;

        public static event Action<GameSettings> OnSettingsSaved;

        public static GameSettings Settings
        {
            get
            {
                if (settings == null) LoadSettings();
                return settings;
            }
        }

        public static RecordsData Records
        {
            get
            {
                if (records == null) LoadRecords();
                return records;
            }
        }

        private static string PathFor(string file)
        {
            return Path.Combine(Application.persistentDataPath, file);
        }

        public static void LoadSettings()
        {
            settings = LoadJson<GameSettings>(SettingsFile) ?? new GameSettings();
            settings.ClampAll();
        }

        public static void LoadRecords()
        {
            records = LoadJson<RecordsData>(RecordsFile) ?? new RecordsData();
        }

        public static void SaveSettings()
        {
            if (settings == null) return;
            settings.ClampAll();
            SaveJson(SettingsFile, settings);
            OnSettingsSaved?.Invoke(settings);
        }

        public static void SaveRecords()
        {
            if (records == null) return;
            SaveJson(RecordsFile, records);
        }

        public static void ResetSettingsToDefault()
        {
            string overrides = settings != null ? settings.bindingOverridesJson : "";
            settings = new GameSettings();
            settings.bindingOverridesJson = overrides;
            SaveSettings();
        }

        /// <summary>Stores race results; returns true when a new best lap or best race time was set.</summary>
        public static bool RecordRaceResult(string trackId, float bestLap, float totalTime, int position)
        {
            if (string.IsNullOrEmpty(trackId)) return false;
            var r = Records.GetOrCreate(trackId);
            bool improved = false;
            r.racesPlayed++;
            if (bestLap > 0f && (r.bestLap < 0f || bestLap < r.bestLap))
            {
                r.bestLap = bestLap;
                improved = true;
            }
            if (totalTime > 0f && (r.bestRace < 0f || totalTime < r.bestRace))
            {
                r.bestRace = totalTime;
                improved = true;
            }
            if (position > 0 && position < r.bestPosition) r.bestPosition = position;
            SaveRecords();
            return improved;
        }

        private static T LoadJson<T>(string file) where T : class
        {
            try
            {
                string path = PathFor(file);
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Failed to read " + file + ": " + e.Message);
                return null;
            }
        }

        private static void SaveJson<T>(string file, T data)
        {
            try
            {
                string path = PathFor(file);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Failed to write " + file + ": " + e.Message);
            }
        }
    }
}
