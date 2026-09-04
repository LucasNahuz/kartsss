using System;
using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Core
{
    public enum InputMode
    {
        Gamepad = 0,
        VRMotionControllers = 1
    }

    /// <summary>
    /// Every user-facing option. Serialised to JSON by SaveManager. Each field is read by a real system;
    /// there are no decorative options.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public int version = 1;

        // Gameplay
        public int difficultyIndex = 1;
        public bool vibration = true;
        [Range(0f, 1f)] public float vibrationIntensity = 1f;
        [Range(0.5f, 1.5f)] public float steeringSensitivity = 1f;
        public bool drivingAssists = true;
        public bool autoAccelerate = false;
        public int inputMode = (int)InputMode.Gamepad;

        // VR comfort
        public float seatHeightOffset = 0f;      // metres, -0.3 .. 0.3
        public float seatForwardOffset = 0f;     // metres, -0.3 .. 0.3
        public bool horizonStabilization = true;
        public bool comfortVignette = true;
        [Range(0f, 1f)] public float vignetteIntensity = 0.7f;
        public int cameraShake = 1;              // 0 off, 1 low, 2 medium
        [Range(0f, 1f)] public float kartTiltIntensity = 0.3f;
        public bool reducedCameraMotion = false;

        // Audio
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Range(0f, 1f)] public float musicVolume = 0.65f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        // Graphics
        public int qualityPreset = 1;            // 0 low, 1 medium, 2 high
        [Range(0.5f, 1.5f)] public float renderScale = 1f;
        public int shadows = 1;                  // 0 off, 1 low, 2 high
        public bool effects = true;
        public int antiAliasing = 1;             // 0 none, 1 MSAA 2x, 2 MSAA 4x

        // Controls
        public string bindingOverridesJson = "";

        public GameSettings Clone()
        {
            return (GameSettings)MemberwiseClone();
        }

        public void ClampAll()
        {
            difficultyIndex = Mathf.Clamp(difficultyIndex, 0, 2);
            vibrationIntensity = Mathf.Clamp01(vibrationIntensity);
            steeringSensitivity = Mathf.Clamp(steeringSensitivity, 0.5f, 1.5f);
            inputMode = Mathf.Clamp(inputMode, 0, 1);
            seatHeightOffset = Mathf.Clamp(seatHeightOffset, -0.3f, 0.3f);
            seatForwardOffset = Mathf.Clamp(seatForwardOffset, -0.3f, 0.3f);
            vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
            cameraShake = Mathf.Clamp(cameraShake, 0, 2);
            kartTiltIntensity = Mathf.Clamp01(kartTiltIntensity);
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            qualityPreset = Mathf.Clamp(qualityPreset, 0, 2);
            renderScale = Mathf.Clamp(renderScale, 0.5f, 1.5f);
            shadows = Mathf.Clamp(shadows, 0, 2);
            antiAliasing = Mathf.Clamp(antiAliasing, 0, 2);
        }

        /// <summary>Applies a preset to the individual graphics fields.</summary>
        public void ApplyQualityPreset(int preset)
        {
            qualityPreset = Mathf.Clamp(preset, 0, 2);
            switch (qualityPreset)
            {
                case 0:
                    renderScale = 0.85f;
                    shadows = 0;
                    effects = false;
                    antiAliasing = 0;
                    break;
                case 1:
                    renderScale = 1f;
                    shadows = 1;
                    effects = true;
                    antiAliasing = 1;
                    break;
                default:
                    renderScale = 1.2f;
                    shadows = 2;
                    effects = true;
                    antiAliasing = 2;
                    break;
            }
        }
    }

    [Serializable]
    public class TrackRecord
    {
        public string trackId;
        public float bestLap = -1f;
        public float bestRace = -1f;
        public int bestPosition = 99;
        public int racesPlayed = 0;
    }

    [Serializable]
    public class RecordsData
    {
        public List<TrackRecord> records = new List<TrackRecord>();

        public TrackRecord GetOrCreate(string trackId)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].trackId == trackId) return records[i];
            }
            var r = new TrackRecord { trackId = trackId };
            records.Add(r);
            return r;
        }

        public TrackRecord Get(string trackId)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].trackId == trackId) return records[i];
            }
            return null;
        }
    }
}
