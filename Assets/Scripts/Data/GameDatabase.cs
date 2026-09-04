using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Data
{
    /// <summary>
    /// Runtime access to every ScriptableObject collection. Assets are loaded from Resources/Data/*;
    /// if a collection is missing (fresh checkout before the editor generated the assets) the built-in
    /// defaults from DefaultContent are used so the game always boots.
    /// </summary>
    public static class GameDatabase
    {
        public const string TracksPath = "Data/Tracks";
        public const string KartsPath = "Data/Karts";
        public const string PowerUpsPath = "Data/PowerUps";
        public const string PersonalitiesPath = "Data/Personalities";
        public const string DifficultiesPath = "Data/Difficulties";
        public const string PilotsPath = "Data/Pilots";

        private static bool loaded;
        private static List<TrackData> tracks;
        private static List<KartStats> karts;
        private static List<PowerUpData> powerUps;
        private static List<AIPersonality> personalities;
        private static List<DifficultySettings> difficulties;
        private static List<PilotData> pilots;
        private static bool usedDefaults;

        public static bool UsedBuiltInDefaults
        {
            get { EnsureLoaded(); return usedDefaults; }
        }

        public static IReadOnlyList<TrackData> Tracks { get { EnsureLoaded(); return tracks; } }
        public static IReadOnlyList<KartStats> Karts { get { EnsureLoaded(); return karts; } }
        public static IReadOnlyList<PowerUpData> PowerUps { get { EnsureLoaded(); return powerUps; } }
        public static IReadOnlyList<AIPersonality> Personalities { get { EnsureLoaded(); return personalities; } }
        public static IReadOnlyList<DifficultySettings> Difficulties { get { EnsureLoaded(); return difficulties; } }
        public static IReadOnlyList<PilotData> Pilots { get { EnsureLoaded(); return pilots; } }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            usedDefaults = false;

            tracks = LoadOrDefault(TracksPath, DefaultContent.CreateTracks, t => t.sortOrder);
            karts = LoadOrDefault(KartsPath, DefaultContent.CreateKarts, k => k.sortOrder);
            powerUps = LoadOrDefault(PowerUpsPath, DefaultContent.CreatePowerUps, p => p.sortOrder);
            personalities = LoadOrDefault(PersonalitiesPath, DefaultContent.CreatePersonalities, p => p.sortOrder);
            difficulties = LoadOrDefault(DifficultiesPath, DefaultContent.CreateDifficulties, d => d.sortOrder);
            pilots = LoadOrDefault(PilotsPath, DefaultContent.CreatePilots, p => p.sortOrder);

            if (usedDefaults)
            {
                Debug.LogWarning("[GameDatabase] Some data assets were missing in Resources/Data. Built-in defaults are in use. " +
                                 "Run 'Vortex Karts > Setup Project' in the editor to generate editable assets.");
            }
        }

        /// <summary>Forces a reload (used by the editor after regenerating assets).</summary>
        public static void Invalidate()
        {
            loaded = false;
        }

        private static List<T> LoadOrDefault<T>(string path, System.Func<List<T>> fallback, System.Func<T, int> order)
            where T : ScriptableObject
        {
            var found = Resources.LoadAll<T>(path);
            List<T> list;
            if (found != null && found.Length > 0)
            {
                list = new List<T>(found);
            }
            else
            {
                list = fallback();
                usedDefaults = true;
            }
            list.Sort((a, b) => order(a).CompareTo(order(b)));
            return list;
        }

        public static TrackData GetTrack(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < tracks.Count; i++) if (tracks[i].id == id) return tracks[i];
            return tracks.Count > 0 ? tracks[0] : null;
        }

        public static TrackData GetTrackBySceneName(string sceneName)
        {
            EnsureLoaded();
            for (int i = 0; i < tracks.Count; i++) if (tracks[i].sceneName == sceneName) return tracks[i];
            return null;
        }

        public static KartStats GetKart(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < karts.Count; i++) if (karts[i].id == id) return karts[i];
            return karts.Count > 0 ? karts[0] : null;
        }

        public static PowerUpData GetPowerUp(PowerUpKind kind)
        {
            EnsureLoaded();
            for (int i = 0; i < powerUps.Count; i++) if (powerUps[i].kind == kind) return powerUps[i];
            return null;
        }

        public static PowerUpData GetPowerUp(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < powerUps.Count; i++) if (powerUps[i].id == id) return powerUps[i];
            return null;
        }

        public static AIPersonality GetPersonality(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < personalities.Count; i++) if (personalities[i].id == id) return personalities[i];
            return personalities.Count > 0 ? personalities[0] : null;
        }

        public static DifficultySettings GetDifficulty(int index)
        {
            EnsureLoaded();
            if (difficulties.Count == 0) return null;
            index = Mathf.Clamp(index, 0, difficulties.Count - 1);
            return difficulties[index];
        }

        public static DifficultySettings GetDifficulty(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < difficulties.Count; i++) if (difficulties[i].id == id) return difficulties[i];
            return difficulties.Count > 0 ? difficulties[Mathf.Min(1, difficulties.Count - 1)] : null;
        }

        public static PilotData GetPilot(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < pilots.Count; i++) if (pilots[i].id == id) return pilots[i];
            return pilots.Count > 0 ? pilots[0] : null;
        }
    }
}
