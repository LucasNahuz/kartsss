using System.Collections;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Race;
using VortexKarts.Track;
using VortexKarts.VR;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Detects falls, stuck karts and flipped karts and puts them back on the last safe checkpoint.
    /// The player gets a fade so the teleport is never visible in the headset.
    /// </summary>
    public class RespawnController : MonoBehaviour
    {
        private const float StuckSpeed = 0.8f;
        private const float StuckSeconds = 4f;
        private const float FlippedSeconds = 2f;
        private const float FallingSeconds = 4.5f;
        private const float ProtectionSeconds = 2f;
        private const float MinIntervalSeconds = 1.5f;

        private KartController kart;
        private RaceProgressTracker tracker;
        private float stuckTimer, flippedTimer, airTimer;
        private float lastRespawnTime = -10f;
        private bool respawning;

        public bool IsRespawning => respawning;
        public int RespawnCount { get; private set; }

        private void Awake()
        {
            kart = GetComponent<KartController>();
            tracker = GetComponent<RaceProgressTracker>();
        }

        private void Update()
        {
            if (kart == null || kart.Stats == null || respawning) return;
            if (kart.InputLocked && !kart.HasFinished) return; // countdown

            float dt = Time.deltaTime;
            var track = TrackRuntime.Instance;
            float killY = track != null ? track.KillY : -30f;

            if (kart.Position.y < killY)
            {
                RequestRespawn("fell off the world");
                return;
            }

            if (kart.IsGrounded) airTimer = 0f;
            else
            {
                airTimer += dt;
                if (airTimer > FallingSeconds)
                {
                    RequestRespawn("falling too long");
                    return;
                }
            }

            bool tryingToMove = kart.Input.Throttle > 0.5f || kart.Input.Brake > 0.5f;
            if (kart.Speed < StuckSpeed && tryingToMove && !kart.Status.ControlsLocked) stuckTimer += dt;
            else stuckTimer = 0f;
            if (stuckTimer > StuckSeconds)
            {
                RequestRespawn("stuck");
                return;
            }

            if (kart.Up.y < 0.2f) flippedTimer += dt;
            else flippedTimer = 0f;
            if (flippedTimer > FlippedSeconds)
            {
                RequestRespawn("flipped");
            }
        }

        /// <summary>Respawns at the last checkpoint. Safe to call from triggers (kill zones).</summary>
        public void RequestRespawn(string reason = null)
        {
            if (respawning || Time.time - lastRespawnTime < MinIntervalSeconds) return;
            lastRespawnTime = Time.time;
            stuckTimer = flippedTimer = airTimer = 0f;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            respawning = true;
            RespawnCount++;
            kart.Status.Apply(StatusEffect.Invulnerable, ProtectionSeconds + 0.6f);
            if (kart.Drift != null) kart.Drift.Cancel();

            VRFader fader = kart.IsPlayer && VRManager.Instance != null ? VRManager.Instance.Fader : null;
            if (fader != null) yield return fader.FadeTo(1f, 0.25f);
            else yield return new WaitForSeconds(0.35f);

            Vector3 pos;
            Quaternion rot;
            if (tracker == null || !tracker.TryGetRespawnPose(out pos, out rot))
            {
                var track = TrackRuntime.Instance;
                if (track != null) track.GetPoseAtDistance(0f, 0f, out pos, out rot);
                else
                {
                    pos = kart.Position + Vector3.up * 2f;
                    rot = Quaternion.Euler(0f, kart.Yaw, 0f);
                }
            }

            kart.TeleportTo(pos, rot);
            if (tracker != null) tracker.OnRespawned();
            GameEvents.RaiseKartRespawned(kart);

            // Hold a moment so physics settles before revealing.
            yield return new WaitForSeconds(0.15f);
            if (fader != null) yield return fader.FadeTo(0f, 0.3f);

            respawning = false;
        }
    }
}
