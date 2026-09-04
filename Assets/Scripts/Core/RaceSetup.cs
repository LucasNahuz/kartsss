using VortexKarts.Data;

namespace VortexKarts.Core
{
    /// <summary>
    /// What the player chose in the PLAY menu. Lives on GameManager and is consumed by the track scene.
    /// </summary>
    public class RaceSetup
    {
        public string TrackId = "neon_metro";
        public int DifficultyIndex = 1;
        public string KartId = "balanced";
        public string PilotId = "vex";
        public int TotalRacers = 8;
        public int LapsOverride = -1;

        public TrackData Track => GameDatabase.GetTrack(TrackId);
        public DifficultySettings Difficulty => GameDatabase.GetDifficulty(DifficultyIndex);
        public KartStats Kart => GameDatabase.GetKart(KartId);
        public PilotData Pilot => GameDatabase.GetPilot(PilotId);

        public int Laps
        {
            get
            {
                if (LapsOverride > 0) return LapsOverride;
                var t = Track;
                return t != null ? t.laps : 3;
            }
        }

        public void Validate()
        {
            if (GameDatabase.GetTrack(TrackId) == null && GameDatabase.Tracks.Count > 0) TrackId = GameDatabase.Tracks[0].id;
            if (GameDatabase.GetKart(KartId) == null && GameDatabase.Karts.Count > 0) KartId = GameDatabase.Karts[0].id;
            if (GameDatabase.GetPilot(PilotId) == null && GameDatabase.Pilots.Count > 0) PilotId = GameDatabase.Pilots[0].id;
            if (DifficultyIndex < 0) DifficultyIndex = 0;
            if (DifficultyIndex >= GameDatabase.Difficulties.Count) DifficultyIndex = GameDatabase.Difficulties.Count - 1;
            if (TotalRacers < 2) TotalRacers = 2;
            if (TotalRacers > 8) TotalRacers = 8;
        }
    }
}
