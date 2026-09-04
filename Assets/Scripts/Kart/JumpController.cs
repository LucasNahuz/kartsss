using UnityEngine;
using VortexKarts.Core;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Tracks air time, performs the small hop used to enter drifts and awards the landing boost.
    /// Landing boost: any clean landing after at least 0.45 s of flight gives a short push.
    /// </summary>
    public class JumpController : MonoBehaviour
    {
        private const float HopVelocity = 2.6f;
        private const float HopCooldown = 0.45f;
        private const float MinAirTimeForLandingBoost = 0.45f;

        private KartController kart;
        private float lastHopTime = -10f;
        private bool wasGrounded = true;

        public float AirTime { get; private set; }
        public float LastAirTime { get; private set; }
        public bool IsAirborne => AirTime > 0.05f;
        /// <summary>Seconds since the last landing (for visuals).</summary>
        public float TimeSinceLanding { get; private set; } = 10f;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        /// <summary>Called from KartController.FixedUpdate after the ground check.</summary>
        public void Tick(float dt)
        {
            TimeSinceLanding += dt;
            if (kart.IsGrounded)
            {
                if (!wasGrounded)
                {
                    LastAirTime = AirTime;
                    TimeSinceLanding = 0f;
                    OnLanded(AirTime);
                }
                AirTime = 0f;
            }
            else
            {
                AirTime += dt;
            }
            wasGrounded = kart.IsGrounded;
        }

        private void OnLanded(float airTime)
        {
            if (airTime >= MinAirTimeForLandingBoost && !kart.Status.ControlsLocked)
            {
                float mul = Mathf.Lerp(1.12f, 1.2f, Mathf.InverseLerp(0.45f, 1.5f, airTime));
                float dur = Mathf.Lerp(0.5f, 0.9f, Mathf.InverseLerp(0.45f, 1.5f, airTime));
                kart.Boost.AddBoost(BoostSource.Landing, mul, dur, 1.5f);
            }
            GameEvents.RaiseKartLanded(kart, airTime);
        }

        /// <summary>Small hop; returns true if performed.</summary>
        public bool TryHop()
        {
            if (!kart.IsGrounded || Time.time - lastHopTime < HopCooldown || kart.Status.ControlsLocked) return false;
            lastHopTime = Time.time;
            kart.AddVerticalKick(HopVelocity);
            return true;
        }
    }
}
