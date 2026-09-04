using UnityEngine;
using VortexKarts.Core;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Drift state machine. Hold the drift button while steering at speed to start sliding; the slide
    /// charges through three levels and releasing pays out a boost. Steering into the drift tightens it,
    /// steering against it opens the line, but the direction never flips.
    /// </summary>
    public class DriftController : MonoBehaviour
    {
        public const int MaxLevel = 3;

        private const float MinSpeedFractionToStart = 0.42f;
        private const float MinSpeedFractionToKeep = 0.25f;
        private const float MinSteerToStart = 0.25f;
        private const float MaxAirTimeWhileDrifting = 0.8f;

        private KartController kart;

        public bool IsDrifting { get; private set; }
        /// <summary>+1 = drifting to the right, -1 = left.</summary>
        public int Direction { get; private set; }
        public int Level { get; private set; }
        public float Charge { get; private set; }
        public float DriftTime { get; private set; }
        /// <summary>0..1 progress towards the next level (for HUD / particle intensity).</summary>
        public float LevelProgress { get; private set; }
        /// <summary>How much the driver steers into the drift right now (0..1).</summary>
        public float SteerInto { get; private set; }

        private float airTime;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        /// <summary>Called from KartController.FixedUpdate before the physics step.</summary>
        public void Tick(KartInputState input, bool controlsLocked, float dt)
        {
            if (kart == null || kart.Stats == null) return;

            if (controlsLocked)
            {
                if (IsDrifting) Cancel();
                return;
            }

            float speedFrac = kart.ForwardSpeed / Mathf.Max(1f, kart.Stats.maxSpeed);

            if (!IsDrifting)
            {
                bool canStart = input.Drift && kart.IsGrounded && speedFrac > MinSpeedFractionToStart &&
                                Mathf.Abs(input.Steer) > MinSteerToStart;
                if (canStart) BeginDrift(input.Steer > 0f ? 1 : -1);
                return;
            }

            // Active drift.
            if (!input.Drift)
            {
                Release();
                return;
            }
            if (speedFrac < MinSpeedFractionToKeep)
            {
                Cancel();
                return;
            }

            if (kart.IsGrounded) airTime = 0f;
            else
            {
                airTime += dt;
                if (airTime > MaxAirTimeWhileDrifting)
                {
                    Cancel();
                    return;
                }
            }

            DriftTime += dt;
            SteerInto = Mathf.Clamp01(input.Steer * Direction);
            float chargeRate = 0.7f + 0.6f * SteerInto;
            if (kart.IsGrounded) Charge += dt * chargeRate;

            int newLevel = 0;
            for (int l = MaxLevel; l >= 1; l--)
            {
                if (Charge >= kart.Stats.GetDriftChargeTime(l))
                {
                    newLevel = l;
                    break;
                }
            }
            if (newLevel < MaxLevel)
            {
                float prev = newLevel == 0 ? 0f : kart.Stats.GetDriftChargeTime(newLevel);
                float next = kart.Stats.GetDriftChargeTime(newLevel + 1);
                LevelProgress = Mathf.InverseLerp(prev, next, Charge);
            }
            else LevelProgress = 1f;

            if (newLevel != Level)
            {
                Level = newLevel;
                GameEvents.RaiseDriftLevelChanged(kart, Level);
            }
        }

        private void BeginDrift(int direction)
        {
            IsDrifting = true;
            Direction = direction;
            Level = 0;
            Charge = 0f;
            DriftTime = 0f;
            LevelProgress = 0f;
            airTime = 0f;
            GameEvents.RaiseDriftLevelChanged(kart, 0);
        }

        /// <summary>Yaw rate while drifting, degrees per second.</summary>
        public float GetYawRate(float steer, float baseRate)
        {
            float into = Mathf.Clamp(steer * Direction, -1f, 1f);
            // Into the drift: up to +50% tighter. Against: down to 35% of the base rate.
            float factor = into >= 0f ? Mathf.Lerp(0.85f, 1.5f, into) : Mathf.Lerp(0.85f, 0.35f, -into);
            return Direction * baseRate * kart.Stats.driftSteerBonus * factor;
        }

        private void Release()
        {
            int level = Level;
            EndDrift();
            if (level <= 0) return;
            float mul, dur;
            switch (level)
            {
                case 1: mul = 1.16f; dur = 0.65f; break;
                case 2: mul = 1.24f; dur = 1.0f; break;
                default: mul = 1.32f; dur = 1.5f; break;
            }
            kart.Boost.AddBoost(BoostSource.Drift, mul, dur);
            GameEvents.RaiseDriftReleased(kart, level);
        }

        public void Cancel()
        {
            if (!IsDrifting) return;
            EndDrift();
            GameEvents.RaiseDriftReleased(kart, 0);
        }

        private void EndDrift()
        {
            IsDrifting = false;
            Level = 0;
            Charge = 0f;
            LevelProgress = 0f;
            SteerInto = 0f;
            GameEvents.RaiseDriftLevelChanged(kart, -1);
        }
    }
}
