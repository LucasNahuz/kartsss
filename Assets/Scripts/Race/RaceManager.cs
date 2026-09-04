using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VortexKarts.AI;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Track;
using VortexKarts.VR;

namespace VortexKarts.Race
{
    public enum RaceState
    {
        Setup = 0,
        Countdown = 1,
        Racing = 2,
        Finished = 3
    }

    public class RaceResultEntry
    {
        public KartController Kart;
        public int Position;
        public float TotalTime;
        public float BestLap;
        public bool Finished;
        public bool IsPlayer;
    }

    public class RaceResults
    {
        public List<RaceResultEntry> Entries = new List<RaceResultEntry>();
        public RaceResultEntry Player;
        public bool NewRecord;
    }

    /// <summary>
    /// Owns one race: spawns the field, runs the countdown (with perfect-start detection), tracks time,
    /// pauses, detects the finish and builds the results. Scene-scoped; created by TrackSceneController.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public RaceState State { get; private set; } = RaceState.Setup;
        public List<KartController> Karts { get; } = new List<KartController>();
        public KartController PlayerKart { get; private set; }
        public int TotalLaps { get; private set; } = 3;
        public float RaceTime { get; private set; }
        public TrackRuntime Track { get; private set; }
        public TrackData TrackData { get; private set; }
        public DifficultySettings Difficulty { get; private set; }
        public PositionManager Positions { get; private set; }
        public RaceResults Results { get; private set; }
        public bool IsPaused => GameManager.Instance != null && GameManager.Instance.IsPaused;
        public bool AIEnabled { get; set; } = true;
        public int CountdownValue { get; private set; } = -1;

        public event Action<RaceState> OnStateChanged;
        public event Action<RaceResults> OnResultsReady;
        /// <summary>-1 jump start penalty, 0 normal, 1 perfect start.</summary>
        public event Action<int> OnStartResult;
        public event Action<string> OnRaceMessage;

        private RaceSetup setup;
        private float playerThrottleHeldSince = -1f;
        private bool playerFinishedHandled;
        private bool autoPausedByDevice;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (InputManager.Instance != null) InputManager.Instance.OnPausePressed -= HandlePausePressed;
            GameEvents.OnKartFinished -= HandleKartFinished;
            GameEvents.OnGamepadDisconnected -= HandleGamepadLost;
            GameEvents.OnGamepadReconnected -= HandleGamepadBack;
            GameEvents.OnHeadTrackingChanged -= HandleTracking;
            if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
        }

        public void Initialize(RaceSetup raceSetup, TrackRuntime track)
        {
            setup = raceSetup;
            Track = track;
            TrackData = track.Data;
            TotalLaps = setup.Laps;
            Difficulty = setup.Difficulty;
            Positions = gameObject.AddComponent<PositionManager>();

            SpawnField();
            Positions.SetKarts(Karts);

            if (InputManager.Instance != null) InputManager.Instance.OnPausePressed += HandlePausePressed;
            GameEvents.OnKartFinished += HandleKartFinished;
            GameEvents.OnGamepadDisconnected += HandleGamepadLost;
            GameEvents.OnGamepadReconnected += HandleGamepadBack;
            GameEvents.OnHeadTrackingChanged += HandleTracking;

            StartCoroutine(CountdownRoutine());
        }

        // ------------------------------------------------------------------ Spawning

        private void SpawnField()
        {
            var pilots = new List<PilotData>(GameDatabase.Pilots);
            var playerPilot = setup.Pilot;
            pilots.RemoveAll(p => p == playerPilot);

            // Deterministic-ish shuffle of CPU pilots so each race has a different grid.
            var rng = new System.Random(Environment.TickCount);
            for (int i = pilots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pilots[i];
                pilots[i] = pilots[j];
                pilots[j] = tmp;
            }

            int total = Mathf.Clamp(setup.TotalRacers, 2, 8);
            // Player starts at the back of the grid (arcade convention: you overtake, you don't defend from lap 1).
            for (int slot = 0; slot < total; slot++)
            {
                bool isPlayer = slot == total - 1;
                Vector3 pos;
                Quaternion rot;
                Track.GetGridSlot(slot, out pos, out rot);

                KartController kart;
                if (isPlayer)
                {
                    kart = KartFactory.CreateKart(setup.Kart, playerPilot, true, pos, rot, slot);
                    PlayerKart = kart;
                }
                else
                {
                    int pi = (slot) % Mathf.Max(1, pilots.Count);
                    var pilot = pilots.Count > 0 ? pilots[pi] : null;
                    var stats = pilot != null ? GameDatabase.GetKart(pilot.defaultKartId) : GameDatabase.Karts[0];
                    kart = KartFactory.CreateKart(stats, pilot, false, pos, rot, slot);
                    var ai = kart.gameObject.AddComponent<AIKartController>();
                    var personality = pilot != null ? GameDatabase.GetPersonality(pilot.personalityId) : GameDatabase.Personalities[0];
                    ai.Setup(personality, Difficulty, rng.Next());
                }
                kart.InputLocked = true;
                var tracker = kart.GetComponent<RaceProgressTracker>();
                if (tracker != null) tracker.TotalLaps = TotalLaps;
                Karts.Add(kart);
            }

            if (PlayerKart != null && VRManager.Instance != null)
            {
                VRManager.Instance.AttachToSeat(PlayerKart.SeatAnchor, PlayerKart);
            }
        }

        // ------------------------------------------------------------------ Countdown

        private IEnumerator CountdownRoutine()
        {
            SetState(RaceState.Countdown);
            yield return new WaitForSeconds(1.2f);
            for (int i = 3; i >= 1; i--)
            {
                CountdownValue = i;
                GameEvents.RaiseCountdownTick(i);
                yield return new WaitForSeconds(1f);
            }
            CountdownValue = 0;
            GameEvents.RaiseCountdownTick(0);
            RaceTime = 0f;
            SetState(RaceState.Racing);
            GameEvents.RaiseRaceStarted();

            // Perfect start / jump start evaluation.
            int result = 0;
            if (PlayerKart != null)
            {
                float held = playerThrottleHeldSince >= 0f ? Time.time - playerThrottleHeldSince : -1f;
                float window = Difficulty != null ? Difficulty.perfectStartWindow : 0.35f;
                if (held >= 0f && held <= window + 0.2f)
                {
                    result = 1;
                    PlayerKart.Boost.AddBoost(BoostSource.Start, 1.22f, 1.1f, 2.2f);
                }
                else if (held > 1.1f)
                {
                    result = -1;
                    StartCoroutine(JumpStartPenalty(PlayerKart));
                }
            }
            for (int i = 0; i < Karts.Count; i++)
            {
                if (Karts[i] != PlayerKart || result != -1) Karts[i].InputLocked = false;
            }
            // CPU start boosts by skill.
            for (int i = 0; i < Karts.Count; i++)
            {
                var ai = Karts[i].GetComponent<AIKartController>();
                if (ai != null && ai.RollStartBoost()) Karts[i].Boost.AddBoost(BoostSource.Start, 1.18f, 0.9f, 2f);
            }
            OnStartResult?.Invoke(result);
            CountdownValue = -1;
        }

        private IEnumerator JumpStartPenalty(KartController kart)
        {
            kart.InputLocked = true;
            OnRaceMessage?.Invoke("¡SALIDA ANTICIPADA!");
            yield return new WaitForSeconds(0.9f);
            kart.InputLocked = false;
        }

        private void Update()
        {
            if (State == RaceState.Countdown && PlayerKart != null)
            {
                bool throttle = PlayerKart.Input.Throttle > 0.5f;
                if (throttle && playerThrottleHeldSince < 0f) playerThrottleHeldSince = Time.time;
                if (!throttle) playerThrottleHeldSince = -1f;
            }
            if (State == RaceState.Racing || State == RaceState.Finished)
            {
                RaceTime += Time.deltaTime;
            }
        }

        private void SetState(RaceState s)
        {
            if (State == s) return;
            State = s;
            OnStateChanged?.Invoke(s);
        }

        // ------------------------------------------------------------------ Finish

        private void HandleKartFinished(KartController kart)
        {
            if (kart != PlayerKart || playerFinishedHandled) return;
            playerFinishedHandled = true;
            StartCoroutine(FinishRoutine());
        }

        private IEnumerator FinishRoutine()
        {
            if (PlayerKart != null && PlayerKart.Visuals != null && PlayerKart.Visuals.Pilot != null)
            {
                var tracker = PlayerKart.GetComponent<RaceProgressTracker>();
                bool podium = tracker != null && tracker.Position <= 3;
                PlayerKart.Visuals.Pilot.SetMood(podium ? PilotRig.Mood.Celebrate : PilotRig.Mood.Defeat);
            }
            yield return new WaitForSeconds(2.2f);
            BuildResults();
            SetState(RaceState.Finished);
            GameEvents.RaiseRaceFinished();
            OnResultsReady?.Invoke(Results);
        }

        private void BuildResults()
        {
            Positions.Recompute(true);
            var results = new RaceResults();
            var ordered = Positions.Ordered;
            for (int i = 0; i < ordered.Count; i++)
            {
                var t = ordered[i];
                var kart = t.GetComponent<KartController>();
                var entry = new RaceResultEntry
                {
                    Kart = kart,
                    Position = i + 1,
                    Finished = t.Finished,
                    TotalTime = t.Finished ? t.FinishTime : -1f,
                    BestLap = t.BestLap,
                    IsPlayer = kart == PlayerKart
                };
                if (!t.Finished)
                {
                    // Estimate a finish time for CPU still on track so the board is complete.
                    float remaining = Mathf.Max(0f, TotalLaps * Track.Length - t.TotalProgress);
                    float avgSpeed = Mathf.Max(8f, kart.Stats.maxSpeed * 0.8f);
                    entry.TotalTime = RaceTime + remaining / avgSpeed;
                }
                results.Entries.Add(entry);
                if (entry.IsPlayer) results.Player = entry;
                if (kart != PlayerKart && kart.Visuals != null && kart.Visuals.Pilot != null)
                {
                    kart.Visuals.Pilot.SetMood(entry.Position <= 3 ? PilotRig.Mood.Celebrate : PilotRig.Mood.Racing);
                }
            }
            if (results.Player != null && TrackData != null)
            {
                results.NewRecord = SaveManager.RecordRaceResult(TrackData.id, results.Player.BestLap, results.Player.TotalTime, results.Player.Position);
            }
            Results = results;
        }

        // ------------------------------------------------------------------ Pause & flow

        private void HandlePausePressed()
        {
            if (State == RaceState.Setup) return;
            if (GameManager.Instance != null && GameManager.Instance.Loader != null && GameManager.Instance.Loader.IsLoading) return;
            TogglePause();
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            if (GameManager.Instance == null) return;
            autoPausedByDevice = false;
            GameManager.Instance.SetPaused(paused);
        }

        private void HandleGamepadLost()
        {
            if (State == RaceState.Racing || State == RaceState.Countdown)
            {
                if (!IsPaused)
                {
                    autoPausedByDevice = true;
                    GameManager.Instance.SetPaused(true);
                }
                OnRaceMessage?.Invoke("CONTROL DESCONECTADO");
            }
        }

        private void HandleGamepadBack()
        {
            OnRaceMessage?.Invoke("CONTROL CONECTADO");
        }

        private void HandleTracking(bool tracked)
        {
            if (tracked) return;
            if ((State == RaceState.Racing || State == RaceState.Countdown) && !IsPaused)
            {
                autoPausedByDevice = true;
                GameManager.Instance.SetPaused(true);
                OnRaceMessage?.Invoke("TRACKING PERDIDO");
            }
        }

        public bool WasAutoPaused => autoPausedByDevice;

        public void Restart()
        {
            GameManager.Instance.RestartRace();
        }

        public void ExitToMenu()
        {
            GameManager.Instance.ReturnToMenu();
        }

        // ------------------------------------------------------------------ Debug helpers

        public void DebugSetPlayerLap(int lap)
        {
            var t = PlayerKart != null ? PlayerKart.GetComponent<RaceProgressTracker>() : null;
            if (t == null) return;
            t.DebugSetLap(lap);
        }

        public void DebugTeleportPlayerToPosition(int position)
        {
            if (PlayerKart == null) return;
            var target = Positions.GetAt(Mathf.Clamp(position, 1, Karts.Count));
            if (target == null) return;
            var tk = target.GetComponent<KartController>();
            if (tk == PlayerKart) return;
            var pt = PlayerKart.GetComponent<RaceProgressTracker>();
            Vector3 pos = tk.Position + tk.Forward * 6f - Vector3.up * KartController.Radius;
            PlayerKart.TeleportTo(pos, Quaternion.LookRotation(tk.Forward, Vector3.up));
            if (pt != null) pt.DebugSetLap(target.Lap, target.NextCheckpoint, target.HasStarted);
        }

        public void DebugSetAIEnabled(bool enabled)
        {
            AIEnabled = enabled;
            for (int i = 0; i < Karts.Count; i++)
            {
                var ai = Karts[i].GetComponent<AIKartController>();
                if (ai != null) ai.enabled = enabled;
                if (!enabled && Karts[i] != PlayerKart) Karts[i].SetInput(KartInputState.Empty);
            }
        }
    }
}
