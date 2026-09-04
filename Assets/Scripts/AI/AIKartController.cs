using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.PowerUps;
using VortexKarts.Race;
using VortexKarts.Track;
using VortexKarts.Utils;

namespace VortexKarts.AI
{
    /// <summary>
    /// CPU driver. Follows the racing line with a personal lateral offset, brakes for corners using the
    /// nodes' recommended speeds, drifts through drift zones, overtakes, defends, avoids hazards and walls,
    /// takes shortcuts, recovers when stuck or facing the wrong way and fires power-ups with intent.
    /// Difficulty improves behaviour quality; speed factors stay close to the player's.
    /// </summary>
    public class AIKartController : MonoBehaviour
    {
        private enum Mode { Racing, Overtake, Defend, Recover }

        private KartController kart;
        private RaceProgressTracker tracker;
        private PowerUpInventory inventory;
        private AIPersonality personality;
        private DifficultySettings difficulty;
        private System.Random rng;
        private TrackRuntime track;

        private Mode mode = Mode.Racing;
        private float lateralTarget;
        private float lateralCurrent;
        private float decisionTimer;
        private float noisePhase;
        private float mistakeTimer;
        private float mistakeSteer;
        private float recoverTimer;
        private float stuckTimer;
        private float driftHoldTimer;
        private bool wantsDrift;
        private int driftDir;
        private int plannedShortcut = -1;
        private int shortcutDecidedLap = -1;
        private float itemHoldTimer;
        private float reactionTimer;
        private float skill;
        private Vector3 lastProgressPosition;
        private float noProgressTimer;
        private float offRoadTimer;

        public AIPersonality Personality => personality;
        public bool WantsDrift => wantsDrift;
        public string DebugState => mode + (wantsDrift ? "/drift" : "") + (plannedShortcut >= 0 ? "/sc" + plannedShortcut : "") +
                                    " lat=" + lateralTarget.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        public void Setup(AIPersonality p, DifficultySettings d, int seed)
        {
            personality = p;
            difficulty = d;
            rng = new System.Random(seed);
            noisePhase = (float)rng.NextDouble() * 100f;
            skill = Mathf.Clamp01((p != null ? p.precision : 0.5f) * (d != null ? d.precisionMultiplier : 0.8f));
        }

        private void Awake()
        {
            kart = GetComponent<KartController>();
            tracker = GetComponent<RaceProgressTracker>();
            inventory = GetComponent<PowerUpInventory>();
        }

        private void Start()
        {
            track = TrackRuntime.Instance;
            if (personality == null) personality = GameDatabase.Personalities[0];
            if (difficulty == null) difficulty = GameDatabase.GetDifficulty(1);
            if (rng == null) rng = new System.Random(GetInstanceID());
        }

        public bool RollStartBoost()
        {
            if (rng == null) rng = new System.Random(GetInstanceID());
            return rng.NextDouble() < 0.25 + 0.55 * skill;
        }

        private float Rand()
        {
            return (float)rng.NextDouble();
        }

        private void Update()
        {
            if (kart == null || kart.Stats == null || track == null || tracker == null) return;
            var race = RaceManager.Instance;
            if (race != null && race.State != RaceState.Racing && race.State != RaceState.Finished)
            {
                var idle = KartInputState.Empty;
                // Rev during countdown for flavour, no movement (InputLocked handles it).
                idle.Throttle = race.State == RaceState.Countdown && race.CountdownValue >= 0 && race.CountdownValue <= 1 ? 1f : 0f;
                kart.SetInput(idle);
                return;
            }
            float dt = Time.deltaTime;
            var node = tracker.CurrentNode ?? track.GetClosestNode(kart.Position);
            if (node == null) return;

            // Safety net: a long time off the road (fell to the ground far below) -> respawn.
            if (kart.Surface == SurfaceType.OffRoad && kart.IsGrounded && !kart.HasFinished) offRoadTimer += dt;
            else offRoadTimer = 0f;
            if (offRoadTimer > 10f && !kart.Respawn.IsRespawning)
            {
                offRoadTimer = 0f;
                plannedShortcut = -1;
                mode = Mode.Racing;
                kart.Respawn.RequestRespawn("ai off-road too long");
                return;
            }

            // Safety net: no real progress for a while (wedged between geometry) -> respawn.
            if ((kart.Position - lastProgressPosition).sqrMagnitude > 6f * 6f)
            {
                lastProgressPosition = kart.Position;
                noProgressTimer = 0f;
            }
            else
            {
                noProgressTimer += dt;
                if (noProgressTimer > 8f && !kart.Respawn.IsRespawning)
                {
                    noProgressTimer = 0f;
                    lastProgressPosition = kart.Position;
                    plannedShortcut = -1;
                    mode = Mode.Racing;
                    kart.Respawn.RequestRespawn("ai no progress");
                    return;
                }
            }

            decisionTimer -= dt;
            reactionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                decisionTimer = 0.35f + Rand() * 0.2f;
                Decide(node);
            }

            var input = new KartInputState();
            if (mode == Mode.Recover)
            {
                Recover(node, ref input, dt);
            }
            else
            {
                Drive(node, ref input, dt);
            }
            UpdateMistakes(dt, ref input);
            UpdatePowerUps(node, ref input, dt);
            kart.SetInput(input);
        }

        // ------------------------------------------------------------------ Decisions

        private void Decide(TrackNode node)
        {
            var race = RaceManager.Instance;
            float halfWidth = node.Width * 0.5f - 1.6f;

            // Base racing line: cut towards the inside of the upcoming corner.
            var ahead = track.GetNodeAhead(node, 8);
            float curv = ahead != null ? ahead.Curvature : 0f;
            float inside = -Mathf.Sign(curv) * Mathf.Clamp(Mathf.Abs(curv) * 250f, 0f, halfWidth * 0.7f);
            // Wander noise scaled by personality.
            float noise = Mathf.Sin(Time.time * 0.6f + noisePhase) * personality.lineVariance * halfWidth * 0.6f;
            float target = inside * Mathf.Lerp(0.4f, 1f, skill) + noise;

            mode = Mode.Racing;
            KartController aheadKart = null, behindKart = null;
            float aheadDist = float.MaxValue, behindDist = float.MaxValue;
            if (race != null)
            {
                for (int i = 0; i < race.Karts.Count; i++)
                {
                    var other = race.Karts[i];
                    if (other == kart || other.HasFinished) continue;
                    Vector3 to = other.Position - kart.Position;
                    float along = Vector3.Dot(to, kart.Forward);
                    float side = Mathf.Abs(Vector3.Dot(to, kart.Right));
                    if (along > 0f && along < 22f && side < 6f && along < aheadDist)
                    {
                        aheadDist = along;
                        aheadKart = other;
                    }
                    if (along < 0f && along > -14f && side < 5f && -along < behindDist)
                    {
                        behindDist = -along;
                        behindKart = other;
                    }
                }
            }

            bool canOvertake = !node.Has(TrackPointFlags.NoOvertake) && !node.IsShortcut;
            if (aheadKart != null && canOvertake && Rand() < personality.overtakeTendency * Mathf.Lerp(0.5f, 1.2f, skill))
            {
                mode = Mode.Overtake;
                float otherLat = Vector3.Dot(aheadKart.Position - node.Position, node.Right);
                float room = 3.4f + personality.aggression * 0.5f;
                float side = otherLat > 0f ? -1f : 1f; // pass on the emptier side
                if (Mathf.Abs(otherLat + side * room) > halfWidth) side = -side;
                target = otherLat + side * room;
            }
            else if (behindKart != null && Rand() < personality.defendTendency)
            {
                mode = Mode.Defend;
                float otherLat = Vector3.Dot(behindKart.Position - node.Position, node.Right);
                target = Mathf.Lerp(target, otherLat, 0.7f);
            }

            // Hazards ahead (mines, slime): move away laterally.
            var pum = PowerUpManager.Instance;
            if (pum != null)
            {
                for (int i = 0; i < pum.Hazards.Count; i++)
                {
                    var h = pum.Hazards[i];
                    if (h == null || !h.IsActive) continue;
                    Vector3 to = h.Position - kart.Position;
                    float along = Vector3.Dot(to, kart.Forward);
                    if (along < 2f || along > 45f) continue;
                    float hazLat = Vector3.Dot(h.Position - node.Position, node.Right);
                    float delta = target - hazLat;
                    float need = h.Radius + 1.6f;
                    if (Mathf.Abs(delta) < need)
                    {
                        float side = delta >= 0f ? 1f : -1f;
                        if (Mathf.Abs(hazLat + side * need) > halfWidth) side = -side;
                        target = hazLat + side * need;
                    }
                }
            }

            // Shortcut planning: decide once per lap when approaching the entry.
            if (plannedShortcut < 0 && !tracker.InShortcut && track.ShortcutNodes.Count > 0 && !node.IsShortcut)
            {
                for (int sc = 0; sc < track.ShortcutNodes.Count; sc++)
                {
                    var list = track.ShortcutNodes[sc];
                    if (list.Count == 0) continue;
                    float entryD = list[0].MainDistanceEquivalent;
                    float delta = MathUtil.LoopDelta(tracker.DistanceAlong, entryD, track.Length);
                    if (delta > 0f && delta < 40f && shortcutDecidedLap != tracker.Lap * 10 + sc)
                    {
                        shortcutDecidedLap = tracker.Lap * 10 + sc;
                        var def = track.Data.shortcuts[sc];
                        float chance = difficulty.shortcutUsage * Mathf.Lerp(0.4f, 1.3f, personality.riskTaking) * def.aiPreference * 1.6f;
                        if (Rand() < chance) plannedShortcut = sc;
                    }
                }
            }
            if (plannedShortcut >= 0 && tracker.InShortcut) plannedShortcut = -1;

            lateralTarget = Mathf.Clamp(target, -halfWidth, halfWidth);
        }

        // ------------------------------------------------------------------ Driving

        private void Drive(TrackNode node, ref KartInputState input, float dt)
        {
            float speed = kart.ForwardSpeed;
            int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(6f, speed) * difficulty.lookAheadTime / Mathf.Max(1f, track.Data.nodeSpacing)), 2, 14);
            TrackNode targetNode = track.GetNodeAhead(node, steps);
            Vector3 targetPoint;

            lateralCurrent = Mathf.MoveTowards(lateralCurrent, lateralTarget, 5f * dt);
            if (plannedShortcut >= 0 && !tracker.InShortcut && plannedShortcut < track.ShortcutNodes.Count)
            {
                var list = track.ShortcutNodes[plannedShortcut];
                var aim = list[Mathf.Min(4, list.Count - 1)];
                targetPoint = aim.Position;
                Vector3 flat = targetPoint - kart.Position;
                flat.y = 0f;
                if (Vector3.Dot(flat, kart.Forward) < 0f) plannedShortcut = -1; // missed the entry
            }
            else
            {
                float lat = Mathf.Clamp(lateralCurrent, -targetNode.Width * 0.5f + 1.2f, targetNode.Width * 0.5f - 1.2f);
                targetPoint = targetNode.PointAtLateral(lat);
            }

            // Wall avoidance: if the closest edge is very near and we drift towards it, push back.
            float edge = track.DistanceToEdge(node, tracker.Lateral);
            if (edge < 1.2f && !node.Has(TrackPointFlags.NoWalls))
            {
                float sign = tracker.Lateral > 0f ? -1f : 1f;
                targetPoint += node.Right * (sign * 2.5f);
            }

            Vector3 toTarget = targetPoint - kart.Position;
            float angle = MathUtil.SignedYawAngle(kart.Forward, toTarget);
            float steerGain = Mathf.Lerp(28f, 40f, skill);
            float steer = Mathf.Clamp(angle / steerGain, -1f, 1f);

            // Throttle / brake from recommended speeds with difficulty and rubber banding.
            float cornerFactor = difficulty.cornerSpeedFactor * Mathf.Lerp(0.9f, 1.08f, personality.riskTaking);
            float desired = Mathf.Min(targetNode.RecommendedSpeed, track.GetNodeAhead(node, Mathf.Max(1, steps / 2)).RecommendedSpeed) * cornerFactor;
            if (node.IsGap || targetNode.IsGap || node.Has(TrackPointFlags.BoostZone)) desired = 999f;
            float cap = kart.Stats.maxSpeed * difficulty.topSpeedFactor * RubberBand();
            if (kart.HasFinished) cap = kart.Stats.maxSpeed * 0.45f; // parade lap
            desired = Mathf.Min(desired, cap);

            if (speed > desired * 1.06f && kart.IsGrounded)
            {
                input.Brake = Mathf.Clamp01((speed - desired) / 6f);
                input.Throttle = 0f;
            }
            else if (speed > cap)
            {
                input.Throttle = 0f;
            }
            else
            {
                input.Throttle = 1f;
            }
            // Acceleration handicap for lower difficulties: ease off when accelerating from low speed.
            if (difficulty.accelerationFactor < 0.99f && speed < kart.Stats.maxSpeed * 0.7f)
            {
                input.Throttle *= difficulty.accelerationFactor;
            }

            // Drifting.
            UpdateDrift(node, targetNode, steer, ref input, dt);
            input.Steer = Mathf.Clamp(steer, -1f, 1f);

            // Stuck detection → recover.
            if (speed < 1.5f && input.Throttle > 0.5f && !kart.Status.ControlsLocked) stuckTimer += dt;
            else stuckTimer = 0f;
            if (stuckTimer > 1.6f || tracker.WrongWay || (Mathf.Abs(angle) > 110f && speed < 8f))
            {
                mode = Mode.Recover;
                recoverTimer = 1.2f;
                stuckTimer = 0f;
            }
        }

        private void UpdateDrift(TrackNode node, TrackNode targetNode, float steer, ref KartInputState input, float dt)
        {
            float speedFrac = kart.SpeedFraction;
            if (!wantsDrift)
            {
                var ahead = track.GetNodeAhead(node, 5);
                float curv = ahead != null ? ahead.Curvature : 0f;
                bool driftZone = ahead != null && ahead.Has(TrackPointFlags.DriftZone);
                bool sharp = Mathf.Abs(curv) > (driftZone ? 0.016f : 0.022f);
                if (sharp && speedFrac > 0.55f && kart.IsGrounded && Mathf.Abs(steer) > 0.25f && reactionTimer <= 0f)
                {
                    float chance = difficulty.driftUsage * Mathf.Lerp(0.3f, 1.1f, personality.driftSkill);
                    if (Rand() < chance)
                    {
                        wantsDrift = true;
                        driftDir = curv > 0f ? 1 : -1;
                        driftHoldTimer = 0f;
                        input.DriftPressed = true;
                    }
                    reactionTimer = Mathf.Max(0.3f, personality.reactionTime * 2f);
                }
            }
            else
            {
                driftHoldTimer += dt;
                var ahead = track.GetNodeAhead(node, 4);
                float curv = ahead != null ? ahead.Curvature : 0f;
                bool stillTurning = Mathf.Sign(curv) == driftDir && Mathf.Abs(curv) > 0.004f;
                float maxHold = Mathf.Lerp(1.4f, 3.4f, personality.driftSkill);
                if (!stillTurning || driftHoldTimer > maxHold || speedFrac < 0.3f || kart.Status.ControlsLocked || !kart.Drift.IsDrifting && driftHoldTimer > 0.4f)
                {
                    wantsDrift = false;
                }
            }
            input.Drift = wantsDrift;
            if (wantsDrift && kart.Drift.IsDrifting)
            {
                // Modulate steering inside the drift: steer into it, loosen when pointing too far in.
                float headingErr = steer * driftDir;
                float into = Mathf.Clamp(0.75f + headingErr * 0.7f, -0.2f, 1f);
                input.Steer = into * driftDir;
            }
        }

        private float RubberBand()
        {
            var race = RaceManager.Instance;
            if (race == null || race.PlayerKart == null || difficulty.rubberBandStrength <= 0f) return 1f;
            var playerTracker = race.PlayerKart.GetComponent<RaceProgressTracker>();
            if (playerTracker == null) return 1f;
            float delta = playerTracker.TotalProgress - tracker.TotalProgress; // positive = player ahead
            float t = Mathf.Clamp(delta / Mathf.Max(20f, difficulty.rubberBandDistance), -1f, 1f);
            return 1f + t * difficulty.rubberBandStrength;
        }

        private void Recover(TrackNode node, ref KartInputState input, float dt)
        {
            recoverTimer -= dt;
            var targetNode = track.GetNodeAhead(node, 4);
            Vector3 to = targetNode.Position - kart.Position;
            float angle = MathUtil.SignedYawAngle(kart.Forward, to);
            wantsDrift = false;

            // Heading is controlled directly in this kart model: steering right always swings the nose right,
            // forwards or backwards. Alternate short reverse / forward pulses, always turning towards the target,
            // until the nose points roughly at the road again.
            float steerToTarget = Mathf.Clamp(angle / 25f, -1f, 1f);
            if (Mathf.Abs(steerToTarget) < 0.5f) steerToTarget = Mathf.Sign(angle == 0f ? 1f : angle) * 0.5f;
            bool reversePhase = Mathf.Repeat(recoverTimer, 2.4f) > 1.2f;
            if (Mathf.Abs(angle) > 50f)
            {
                if (reversePhase)
                {
                    input.Brake = 1f;
                    input.Throttle = 0f;
                }
                else
                {
                    input.Throttle = 0.8f;
                    input.Brake = 0f;
                }
                input.Steer = steerToTarget;
            }
            else
            {
                input.Throttle = 1f;
                input.Brake = 0f;
                input.Steer = Mathf.Clamp(angle / 30f, -1f, 1f);
            }
            if (Mathf.Abs(angle) < 40f && kart.ForwardSpeed > -1f)
            {
                mode = Mode.Racing;
                decisionTimer = 0f;
                wantsDrift = false;
            }
            else if (recoverTimer <= -6f)
            {
                // Still hopeless: let the respawn system handle it.
                kart.Respawn.RequestRespawn("ai gave up");
                mode = Mode.Racing;
            }
        }

        private void UpdateMistakes(float dt, ref KartInputState input)
        {
            mistakeTimer -= dt;
            if (mistakeTimer <= 0f)
            {
                if (Rand() < difficulty.mistakeChancePerSecond * dt)
                {
                    mistakeSteer = (Rand() < 0.5f ? -1f : 1f) * difficulty.mistakeMagnitude * Mathf.Lerp(1.2f, 0.5f, skill);
                    mistakeTimer = 0.45f;
                }
                else
                {
                    mistakeSteer = 0f;
                }
            }
            if (mistakeSteer != 0f && mode != Mode.Recover)
            {
                input.Steer = Mathf.Clamp(input.Steer + mistakeSteer, -1f, 1f);
            }
        }

        // ------------------------------------------------------------------ Power-ups

        private void UpdatePowerUps(TrackNode node, ref KartInputState input, float dt)
        {
            if (inventory == null || !inventory.HasItem || kart.Status.IsStunned) return;
            var data = inventory.Current;
            var race = RaceManager.Instance;
            var pum = PowerUpManager.Instance;
            itemHoldTimer += dt;

            float eagerness = personality.powerUpUsage * difficulty.powerUpAggression;
            bool fire = false;
            float curv = Mathf.Abs(node.Curvature);
            bool straight = curv < 0.005f && kart.IsGrounded;

            KartController closeAhead = null, closeBehind = null;
            float aheadDist = 999f, behindDist = 999f, aheadAngle = 180f;
            if (race != null)
            {
                for (int i = 0; i < race.Karts.Count; i++)
                {
                    var other = race.Karts[i];
                    if (other == kart || other.HasFinished) continue;
                    Vector3 to = other.Position - kart.Position;
                    float along = Vector3.Dot(to, kart.Forward);
                    float dist = to.magnitude;
                    float ang = Vector3.Angle(kart.Forward, to);
                    if (along > 0f && dist < aheadDist)
                    {
                        aheadDist = dist;
                        closeAhead = other;
                        aheadAngle = ang;
                    }
                    if (along < 0f && dist < behindDist)
                    {
                        behindDist = dist;
                        closeBehind = other;
                    }
                }
            }

            switch (data.kind)
            {
                case PowerUpKind.Turbo:
                case PowerUpKind.TripleTurbo:
                    fire = straight && !kart.Boost.IsBoosting && Rand() < 0.5f + eagerness * 0.5f;
                    break;
                case PowerUpKind.MegaBoost:
                    fire = straight || itemHoldTimer > 3f;
                    break;
                case PowerUpKind.StraightShot:
                case PowerUpKind.TripleShot:
                    fire = closeAhead != null && aheadDist < 40f && aheadAngle < 18f && Rand() < eagerness;
                    if (!fire && itemHoldTimer > 12f && Rand() < 0.2f) fire = true;
                    break;
                case PowerUpKind.HomingMissile:
                    fire = closeAhead != null && aheadDist < 90f && tracker.Position > 1 && Rand() < 0.4f + eagerness * 0.6f;
                    break;
                case PowerUpKind.Mine:
                case PowerUpKind.OilSlick:
                    fire = (closeBehind != null && behindDist < 18f && Rand() < 0.5f + eagerness * 0.5f) ||
                           (itemHoldTimer > 7f && curv > 0.01f && Rand() < 0.3f);
                    break;
                case PowerUpKind.Shield:
                    bool threatened = pum != null && pum.IsProjectileThreatening(kart, 30f);
                    fire = threatened || (closeBehind != null && behindDist < 8f && personality.defendTendency > 0.5f && Rand() < 0.3f) ||
                           itemHoldTimer > 9f;
                    break;
                case PowerUpKind.EmpPulse:
                    int near = 0;
                    if (race != null)
                    {
                        float r = data.magnitude * 0.85f;
                        for (int i = 0; i < race.Karts.Count; i++)
                        {
                            var o = race.Karts[i];
                            if (o != kart && (o.Position - kart.Position).sqrMagnitude < r * r) near++;
                        }
                    }
                    fire = near >= 1 && Rand() < 0.4f + eagerness * 0.6f;
                    break;
            }

            // Defensive personalities sit on items longer; chaotic ones fire early.
            if (fire && personality.defendTendency > 0.7f && itemHoldTimer < 1.5f && data.kind != PowerUpKind.Shield) fire = false;
            if (!fire && personality.riskTaking > 0.85f && itemHoldTimer > 2f && Rand() < 0.02f) fire = true;

            if (fire)
            {
                input.UsePowerUp = true;
                itemHoldTimer = 0f;
            }
        }
    }
}
