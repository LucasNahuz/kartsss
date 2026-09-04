using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Kart;
using VortexKarts.Track;
using VortexKarts.Utils;

namespace VortexKarts.Race
{
    /// <summary>
    /// Per-kart lap, checkpoint and progress bookkeeping. Positions are derived from TotalProgress, which
    /// combines completed laps (validated through ordered checkpoints) with the continuous distance along
    /// the road, so a kart cannot gain a lap by driving backwards over the finish line or by leaving the track.
    /// </summary>
    public class RaceProgressTracker : MonoBehaviour
    {
        private KartController kart;
        private TrackRuntime track;

        public int Lap { get; private set; }               // completed laps
        public int NextCheckpoint { get; private set; }
        public int LastCheckpointIndex { get; private set; }
        public float DistanceAlong { get; private set; }
        public float Lateral { get; private set; }
        public TrackNode CurrentNode { get; private set; }
        public int NodeHint { get; private set; } = -1;
        public bool InShortcut { get; private set; }
        public int ShortcutIndex { get; private set; } = -1;
        public bool WrongWay { get; private set; }
        public bool Finished { get; private set; }
        public bool HasStarted { get; private set; }
        public int Position { get; set; }
        public float TotalProgress { get; private set; }
        public float LapProgress01 => track != null && track.Length > 0f ? DistanceAlong / track.Length : 0f;
        public List<float> LapTimes { get; } = new List<float>();
        public float CurrentLapStart { get; private set; }
        public float BestLap { get; private set; } = -1f;
        public float FinishTime { get; private set; } = -1f;
        public int TotalLaps { get; set; } = 3;
        /// <summary>Checkpoints up to (and including) this index may be skipped (shortcut in use).</summary>
        public int SkipAllowedUntil { get; private set; } = -1;

        private int shortcutHint = -1;
        private float wrongWayTimer;
        private float lastCheckpointTime;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        private void Start()
        {
            track = TrackRuntime.Instance;
            ResetProgress();
        }

        public void ResetProgress()
        {
            track = TrackRuntime.Instance;
            Lap = 0;
            NextCheckpoint = 0;
            LastCheckpointIndex = 0;
            HasStarted = false;
            Finished = false;
            FinishTime = -1f;
            LapTimes.Clear();
            BestLap = -1f;
            InShortcut = false;
            ShortcutIndex = -1;
            SkipAllowedUntil = -1;
            NodeHint = -1;
            WrongWay = false;
            wrongWayTimer = 0f;
            if (track != null && kart != null)
            {
                int hint = -1;
                float lat;
                TrackNode node;
                DistanceAlong = track.ProjectOntoMain(kart.Position, ref hint, out lat, out node);
                NodeHint = hint;
                CurrentNode = node;
                Lateral = lat;
            }
            RecomputeTotalProgress();
        }

        private void Update()
        {
            if (track == null)
            {
                track = TrackRuntime.Instance;
                if (track == null) return;
            }
            if (kart == null) return;

            float lat;
            TrackNode node;
            if (InShortcut)
            {
                int hint = shortcutHint;
                DistanceAlong = track.ProjectOntoShortcut(ShortcutIndex, kart.Position, ref hint, out lat, out node);
                shortcutHint = hint;
                // Fell far away from the shortcut (e.g. off a ledge): go back to main-road tracking.
                if (node != null && (kart.Position - node.Position).sqrMagnitude > 30f * 30f)
                {
                    InShortcut = false;
                    ShortcutIndex = -1;
                }
            }
            else
            {
                int hint = NodeHint;
                DistanceAlong = track.ProjectOntoMain(kart.Position, ref hint, out lat, out node);
                NodeHint = hint;
            }
            Lateral = lat;
            CurrentNode = node;

            UpdateWrongWay();
            RecomputeTotalProgress();
        }

        private void UpdateWrongWay()
        {
            if (CurrentNode == null || Finished || !HasStarted)
            {
                SetWrongWay(false);
                return;
            }
            Vector3 vel = kart.Velocity;
            float speed = vel.magnitude;
            bool goingBack = speed > 4f && Vector3.Dot(vel.normalized, CurrentNode.Forward) < -0.45f && kart.IsGrounded;
            if (goingBack) wrongWayTimer += Time.deltaTime;
            else wrongWayTimer = Mathf.Max(0f, wrongWayTimer - Time.deltaTime * 2f);
            SetWrongWay(wrongWayTimer > 1.0f);
        }

        private void SetWrongWay(bool value)
        {
            if (WrongWay == value) return;
            WrongWay = value;
            GameEvents.RaiseKartWrongWay(kart, value);
        }

        private void RecomputeTotalProgress()
        {
            if (track == null || track.Length <= 0f) return;
            float L = track.Length;
            float d = DistanceAlong;
            // Keep progress continuous around the finish line even if the trigger fires slightly early/late.
            if (HasStarted)
            {
                if (NextCheckpoint == 1 && d > L * 0.9f) d -= L;          // counted the lap, still physically before the line
                else if (NextCheckpoint == 0 && d < L * 0.1f && Lap > 0) d += L; // physically past the line, trigger not yet hit
                else if (NextCheckpoint == 0 && d < L * 0.1f && Lap == 0 && LapTimes.Count == 0 && lastCheckpointTime > 0f) d += L;
            }
            float lapBase = HasStarted ? Lap * L : -L;
            if (!HasStarted && d < L * 0.5f) lapBase = 0f; // spawned just past the line (debug placements)
            TotalProgress = lapBase + d;
        }

        // ------------------------------------------------------------------ Checkpoints

        public void OnCheckpointTriggered(Checkpoint cp)
        {
            if (Finished || track == null || cp == null) return;
            int n = track.Checkpoints.Count;
            if (n == 0) return;

            bool forward = Vector3.Dot(kart.Velocity.sqrMagnitude > 0.5f ? kart.Velocity.normalized : kart.Forward, cp.Forward) >= 0f;

            if (!forward)
            {
                // Driving backwards through the checkpoint we most recently passed: roll back.
                if (cp.Index == LastCheckpointIndex && HasStarted)
                {
                    if (cp.IsFinishLine)
                    {
                        if (Lap > 0)
                        {
                            Lap--;
                            if (LapTimes.Count > 0)
                            {
                                CurrentLapStart -= LapTimes[LapTimes.Count - 1];
                                LapTimes.RemoveAt(LapTimes.Count - 1);
                            }
                        }
                        else
                        {
                            HasStarted = false;
                        }
                    }
                    NextCheckpoint = cp.Index;
                    LastCheckpointIndex = (cp.Index - 1 + n) % n;
                }
                return;
            }

            bool accepted = cp.Index == NextCheckpoint;
            if (!accepted && SkipAllowedUntil >= 0 && IsWithinSkipRange(cp.Index, n))
            {
                accepted = true;
            }
            if (!accepted) return;

            LastCheckpointIndex = cp.Index;
            NextCheckpoint = (cp.Index + 1) % n;
            lastCheckpointTime = Time.time;
            if (SkipAllowedUntil >= 0 && cp.Index == SkipAllowedUntil) SkipAllowedUntil = -1;

            if (cp.IsFinishLine)
            {
                if (!HasStarted)
                {
                    HasStarted = true;
                    CurrentLapStart = Time.time;
                }
                else
                {
                    float lapTime = Time.time - CurrentLapStart;
                    LapTimes.Add(lapTime);
                    if (BestLap < 0f || lapTime < BestLap) BestLap = lapTime;
                    Lap++;
                    CurrentLapStart = Time.time;
                    GameEvents.RaiseLapCompleted(kart, Lap, lapTime);
                    if (Lap >= TotalLaps)
                    {
                        Finished = true;
                        FinishTime = RaceManager.Instance != null ? RaceManager.Instance.RaceTime : Time.time;
                        kart.HasFinished = true;
                        GameEvents.RaiseKartFinished(kart);
                    }
                }
            }
            RecomputeTotalProgress();
        }

        private bool IsWithinSkipRange(int index, int n)
        {
            // Allowed indices: NextCheckpoint .. SkipAllowedUntil (wrapping).
            int steps = (SkipAllowedUntil - NextCheckpoint + n) % n;
            int offset = (index - NextCheckpoint + n) % n;
            return offset <= steps;
        }

        // ------------------------------------------------------------------ Shortcuts

        public void EnterShortcut(int shortcutIndex, int exitCheckpointIndex)
        {
            if (Finished) return;
            InShortcut = true;
            ShortcutIndex = shortcutIndex;
            shortcutHint = -1;
            SkipAllowedUntil = exitCheckpointIndex;
        }

        public void ExitShortcut(int shortcutIndex)
        {
            if (ShortcutIndex != shortcutIndex && InShortcut) return;
            InShortcut = false;
            ShortcutIndex = -1;
            NodeHint = -1;
        }

        // ------------------------------------------------------------------ Respawn

        public bool TryGetRespawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (track == null || track.Checkpoints.Count == 0 || track.Nodes.Count == 0) return false;
            var cp = track.GetCheckpoint(LastCheckpointIndex);
            float d = cp != null ? cp.Distance + 4f : 0f;
            if (!HasStarted)
            {
                // Before the start line: respawn on the grid area.
                d = track.Length - 12f;
            }
            var node = track.GetNodeAtDistance(d);
            int guard = 0;
            while (node != null && node.IsGap && guard++ < 20) node = track.GetNode(node.Index + 1);
            if (node == null) return false;
            position = node.Position;
            rotation = Quaternion.LookRotation(node.Forward, Vector3.up);
            return true;
        }

        public void OnRespawned()
        {
            InShortcut = false;
            ShortcutIndex = -1;
            NodeHint = -1;
            wrongWayTimer = 0f;
            SetWrongWay(false);
        }

        public float CurrentLapTime => HasStarted && !Finished ? Time.time - CurrentLapStart : 0f;

        // ------------------------------------------------------------------ Debug

        /// <summary>Development tool: jumps the lap counter (keeps checkpoint expectations sane).</summary>
        public void DebugSetLap(int lap)
        {
            Lap = Mathf.Clamp(lap, 0, Mathf.Max(0, TotalLaps - 1));
            HasStarted = true;
            if (CurrentLapStart <= 0f) CurrentLapStart = Time.time;
            Finished = false;
            RecomputeTotalProgress();
        }

        public void DebugSetLap(int lap, int nextCheckpoint, bool started)
        {
            Lap = Mathf.Clamp(lap, 0, Mathf.Max(0, TotalLaps - 1));
            NextCheckpoint = nextCheckpoint;
            LastCheckpointIndex = nextCheckpoint - 1;
            if (LastCheckpointIndex < 0 && track != null) LastCheckpointIndex = Mathf.Max(0, track.Checkpoints.Count - 1);
            HasStarted = started;
            if (started && CurrentLapStart <= 0f) CurrentLapStart = Time.time;
            InShortcut = false;
            ShortcutIndex = -1;
            NodeHint = -1;
            Finished = false;
            RecomputeTotalProgress();
        }
    }
}
