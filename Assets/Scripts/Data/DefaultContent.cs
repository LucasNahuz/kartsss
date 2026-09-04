using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Data
{
    /// <summary>
    /// Single source of the default game content. The editor setup tool writes these into ScriptableObject
    /// assets under Resources/Data so designers can tune them in the Inspector; at runtime the same
    /// definitions act as a fallback when the assets are missing.
    /// </summary>
    public static class DefaultContent
    {
        // ------------------------------------------------------------------ Karts

        public static List<KartStats> CreateKarts()
        {
            var list = new List<KartStats>();

            var balanced = ScriptableObject.CreateInstance<KartStats>();
            balanced.id = "balanced";
            balanced.displayName = "Vector";
            balanced.description = "Equilibrado en todo. El punto de partida ideal.";
            balanced.sortOrder = 0;
            balanced.maxSpeed = 32f;
            balanced.acceleration = 14f;
            balanced.handling = 0.5f;
            balanced.driftControl = 0.5f;
            balanced.weight = 160f;
            balanced.boostPower = 1f;
            balanced.offRoadPenalty = 0.55f;
            balanced.bodyColor = new Color(0.9f, 0.3f, 0.2f);
            balanced.accentColor = new Color(0.95f, 0.85f, 0.2f);
            list.Add(balanced);

            var speed = ScriptableObject.CreateInstance<KartStats>();
            speed.id = "speed";
            speed.displayName = "Rayo";
            speed.description = "Velocidad punta superior a cambio de una dirección más pesada.";
            speed.sortOrder = 1;
            speed.maxSpeed = 35f;
            speed.acceleration = 12f;
            speed.handling = 0.35f;
            speed.driftControl = 0.45f;
            speed.weight = 150f;
            speed.boostPower = 1.05f;
            speed.offRoadPenalty = 0.5f;
            speed.bodyColor = new Color(0.2f, 0.5f, 1f);
            speed.accentColor = new Color(1f, 1f, 1f);
            speed.bodyScale = new Vector3(0.95f, 0.9f, 1.1f);
            list.Add(speed);

            var handling = ScriptableObject.CreateInstance<KartStats>();
            handling.id = "handling";
            handling.displayName = "Ágil";
            handling.description = "Gira como un insecto y derrapa con precisión. Menos velocidad punta.";
            handling.sortOrder = 2;
            handling.maxSpeed = 30.5f;
            handling.acceleration = 15.5f;
            handling.handling = 0.8f;
            handling.driftControl = 0.75f;
            handling.weight = 130f;
            handling.boostPower = 1f;
            handling.offRoadPenalty = 0.62f;
            handling.bodyColor = new Color(0.3f, 0.85f, 0.4f);
            handling.accentColor = new Color(0.1f, 0.2f, 0.2f);
            handling.bodyScale = new Vector3(0.9f, 0.95f, 0.92f);
            list.Add(handling);

            var heavy = ScriptableObject.CreateInstance<KartStats>();
            heavy.id = "heavy";
            heavy.displayName = "Titán";
            heavy.description = "Pesado y difícil de mover. Empuja a los rivales y aguanta los golpes.";
            heavy.sortOrder = 3;
            heavy.maxSpeed = 33f;
            heavy.acceleration = 11f;
            heavy.handling = 0.3f;
            heavy.driftControl = 0.4f;
            heavy.weight = 240f;
            heavy.boostPower = 1.1f;
            heavy.offRoadPenalty = 0.5f;
            heavy.bodyColor = new Color(0.45f, 0.45f, 0.5f);
            heavy.accentColor = new Color(1f, 0.5f, 0.1f);
            heavy.bodyScale = new Vector3(1.12f, 1.1f, 1.08f);
            list.Add(heavy);

            return list;
        }

        // ------------------------------------------------------------------ Power-ups

        private static PowerUpData PowerUp(string id, string name, PowerUpKind kind, string label, Color color,
            int charges, float duration, float magnitude, float secondary,
            float wLeader, float wFront, float wMid, float wBack, int order, string description)
        {
            var p = ScriptableObject.CreateInstance<PowerUpData>();
            p.id = id;
            p.displayName = name;
            p.kind = kind;
            p.shortLabel = label;
            p.iconColor = color;
            p.charges = charges;
            p.duration = duration;
            p.magnitude = magnitude;
            p.secondary = secondary;
            p.weightLeader = wLeader;
            p.weightFront = wFront;
            p.weightMid = wMid;
            p.weightBack = wBack;
            p.sortOrder = order;
            p.description = description;
            return p;
        }

        public static List<PowerUpData> CreatePowerUps()
        {
            var list = new List<PowerUpData>
            {
                PowerUp("turbo", "Turbo", PowerUpKind.Turbo, "TURBO", new Color(1f, 0.55f, 0.1f),
                    1, 2.2f, 1.28f, 0f, 3f, 4f, 4f, 3f, 0,
                    "Aumento fuerte de velocidad durante unos segundos."),
                PowerUp("homing", "Misil Rastreador", PowerUpKind.HomingMissile, "MISIL", new Color(1f, 0.25f, 0.2f),
                    1, 6f, 46f, 0f, 0.5f, 3f, 4f, 3f, 1,
                    "Persigue al rival de adelante. Se puede esquivar con obstáculos, escudos o maniobras."),
                PowerUp("bolt", "Bólido", PowerUpKind.StraightShot, "BÓLIDO", new Color(0.2f, 0.9f, 1f),
                    1, 4f, 52f, 3f, 3f, 4f, 3f, 2f, 2,
                    "Proyectil recto que rebota hasta tres veces contra las paredes."),
                PowerUp("mine", "Mina Magnética", PowerUpKind.Mine, "MINA", new Color(0.9f, 0.9f, 0.2f),
                    1, 25f, 1.2f, 0.5f, 4f, 3f, 2f, 1f, 3,
                    "Se deja atrás. Quien la toca pierde el control y velocidad."),
                PowerUp("shield", "Escudo Prisma", PowerUpKind.Shield, "ESCUDO", new Color(0.4f, 0.7f, 1f),
                    1, 7f, 1f, 0f, 5f, 3f, 2f, 1f, 4,
                    "Bloquea un impacto."),
                PowerUp("emp", "Pulso EMP", PowerUpKind.EmpPulse, "EMP", new Color(0.7f, 0.4f, 1f),
                    1, 2.5f, 22f, 0f, 1f, 2f, 3f, 3f, 5,
                    "Aturde a los rivales cercanos: menos aceleración y sin power-ups por unos segundos."),
                PowerUp("slime", "Mancha Slime", PowerUpKind.OilSlick, "SLIME", new Color(0.5f, 1f, 0.3f),
                    1, 15f, 2.5f, 4f, 4f, 3f, 2f, 1f, 6,
                    "Charco resbaladizo. Quien lo pisa pierde adherencia."),
                PowerUp("triple_bolt", "Triple Bólido", PowerUpKind.TripleShot, "x3 BÓLIDO", new Color(0.2f, 0.9f, 1f),
                    3, 4f, 52f, 2f, 0f, 1f, 3f, 4f, 7,
                    "Tres bólidos independientes."),
                PowerUp("triple_turbo", "Triple Turbo", PowerUpKind.TripleTurbo, "x3 TURBO", new Color(1f, 0.75f, 0.2f),
                    3, 1.2f, 1.22f, 0f, 0f, 1f, 3f, 4f, 8,
                    "Tres pequeños turbos."),
                PowerUp("mega", "Mega Boost", PowerUpKind.MegaBoost, "MEGA", new Color(1f, 0.3f, 0.9f),
                    1, 5f, 1.45f, 0f, 0f, 0f, 1f, 5f, 9,
                    "Velocidad extrema durante varios segundos y resistencia a impactos. Para remontar.")
            };
            return list;
        }

        // ------------------------------------------------------------------ AI

        private static AIPersonality Personality(string id, string name, int order, float aggression, float precision,
            float powerUpUsage, float overtake, float risk, float drift, float defend, float variance, float reaction,
            string description)
        {
            var p = ScriptableObject.CreateInstance<AIPersonality>();
            p.id = id;
            p.displayName = name;
            p.sortOrder = order;
            p.aggression = aggression;
            p.precision = precision;
            p.powerUpUsage = powerUpUsage;
            p.overtakeTendency = overtake;
            p.riskTaking = risk;
            p.driftSkill = drift;
            p.defendTendency = defend;
            p.lineVariance = variance;
            p.reactionTime = reaction;
            p.description = description;
            return p;
        }

        public static List<AIPersonality> CreatePersonalities()
        {
            return new List<AIPersonality>
            {
                Personality("aggressive", "Aggressive", 0, 0.9f, 0.55f, 0.85f, 0.9f, 0.8f, 0.6f, 0.4f, 0.35f, 0.18f,
                    "Busca el contacto, adelanta donde no hay hueco y dispara todo lo que tiene."),
                Personality("balanced", "Balanced", 1, 0.5f, 0.6f, 0.5f, 0.55f, 0.5f, 0.55f, 0.5f, 0.25f, 0.25f,
                    "Piloto completo sin excesos."),
                Personality("technical", "Technical", 2, 0.3f, 0.9f, 0.5f, 0.5f, 0.35f, 0.9f, 0.5f, 0.1f, 0.2f,
                    "Líneas limpias y derrapes perfectos. Evita el contacto."),
                Personality("defensive", "Defensive", 3, 0.35f, 0.7f, 0.6f, 0.3f, 0.25f, 0.5f, 0.9f, 0.15f, 0.25f,
                    "Protege su posición y guarda los power-ups para defenderse."),
                Personality("chaotic", "Chaotic", 4, 0.7f, 0.35f, 0.9f, 0.7f, 0.95f, 0.45f, 0.2f, 0.6f, 0.3f,
                    "Impredecible: atajos arriesgados, líneas raras y power-ups sin criterio.")
            };
        }

        private static DifficultySettings Difficulty(string id, string name, int order, float topSpeed, float accel,
            float corner, float precisionMul, float mistakeChance, float mistakeMag, float drift, float powerUp,
            float shortcut, float lookAhead, float rubber, string description)
        {
            var d = ScriptableObject.CreateInstance<DifficultySettings>();
            d.id = id;
            d.displayName = name;
            d.sortOrder = order;
            d.topSpeedFactor = topSpeed;
            d.accelerationFactor = accel;
            d.cornerSpeedFactor = corner;
            d.precisionMultiplier = precisionMul;
            d.mistakeChancePerSecond = mistakeChance;
            d.mistakeMagnitude = mistakeMag;
            d.driftUsage = drift;
            d.powerUpAggression = powerUp;
            d.shortcutUsage = shortcut;
            d.lookAheadTime = lookAhead;
            d.rubberBandStrength = rubber;
            d.description = description;
            return d;
        }

        public static List<DifficultySettings> CreateDifficulties()
        {
            return new List<DifficultySettings>
            {
                Difficulty("easy", "Fácil", 0, 0.86f, 0.85f, 0.78f, 0.6f, 0.2f, 0.5f, 0.25f, 0.4f, 0.2f, 0.7f, 0.02f,
                    "CPU más lenta, comete errores y usa pocos power-ups ofensivos."),
                Difficulty("normal", "Normal", 1, 0.96f, 0.95f, 0.9f, 0.8f, 0.08f, 0.35f, 0.6f, 0.7f, 0.5f, 0.9f, 0.03f,
                    "La experiencia principal."),
                Difficulty("hard", "Difícil", 2, 1.0f, 1.0f, 0.97f, 1.0f, 0.02f, 0.2f, 0.95f, 1.0f, 0.8f, 1.1f, 0.035f,
                    "Buenas líneas, derrapes constantes y power-ups usados con intención.")
            };
        }

        // ------------------------------------------------------------------ Pilots

        private static PilotData Pilot(string id, string name, int order, Color primary, Color secondary, Color skin,
            HelmetStyle helmet, string personality, string kart, string bio)
        {
            var p = ScriptableObject.CreateInstance<PilotData>();
            p.id = id;
            p.displayName = name;
            p.sortOrder = order;
            p.primaryColor = primary;
            p.secondaryColor = secondary;
            p.skinColor = skin;
            p.helmet = helmet;
            p.personalityId = personality;
            p.defaultKartId = kart;
            p.bio = bio;
            return p;
        }

        public static List<PilotData> CreatePilots()
        {
            return new List<PilotData>
            {
                Pilot("vex", "Vex", 0, new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.9f, 0.3f), new Color(0.95f, 0.75f, 0.6f),
                    HelmetStyle.Round, "aggressive", "speed", "Ex mensajero del Metro Neón. Frena poco, insulta mucho."),
                Pilot("lumi", "Lumi", 1, new Color(0.2f, 0.9f, 1f), new Color(1f, 1f, 1f), new Color(0.8f, 0.65f, 0.55f),
                    HelmetStyle.Visor, "technical", "handling", "Ingeniera del Sky Lab. Cada curva es una ecuación."),
                Pilot("brogan", "Brogan", 2, new Color(0.2f, 0.6f, 0.3f), new Color(1f, 0.55f, 0.1f), new Color(0.6f, 0.4f, 0.3f),
                    HelmetStyle.Crest, "defensive", "heavy", "Minero del Cañón Solar. Nadie lo pasa por dentro."),
                Pilot("zippy", "Zippy", 3, new Color(1f, 0.3f, 0.85f), new Color(0.6f, 1f, 0.3f), new Color(0.95f, 0.8f, 0.7f),
                    HelmetStyle.Antenna, "chaotic", "balanced", "Nadie sabe de dónde salió. Tampoco él."),
                Pilot("nova", "Nova", 4, new Color(0.5f, 0.25f, 0.9f), new Color(1f, 0.85f, 0.3f), new Color(0.45f, 0.3f, 0.25f),
                    HelmetStyle.Visor, "balanced", "speed", "Campeona en tres ligas. Sonríe hasta cuando pierde."),
                Pilot("tako", "Tako", 5, new Color(1f, 0.5f, 0.1f), new Color(0.15f, 0.35f, 0.9f), new Color(0.9f, 0.7f, 0.55f),
                    HelmetStyle.Round, "aggressive", "heavy", "Cocinero reconvertido. Usa el kart como sartén."),
                Pilot("sable", "Sable", 6, new Color(0.12f, 0.12f, 0.15f), new Color(0.9f, 0.1f, 0.2f), new Color(0.7f, 0.55f, 0.45f),
                    HelmetStyle.Crest, "technical", "balanced", "Silenciosa, precisa, letal en los derrapes largos."),
                Pilot("pip", "Pip", 7, new Color(1f, 0.9f, 0.2f), new Color(0.1f, 0.7f, 0.65f), new Color(0.98f, 0.85f, 0.75f),
                    HelmetStyle.Antenna, "chaotic", "handling", "La más joven del circuito. Cero miedo, cero frenos.")
            };
        }

        // ------------------------------------------------------------------ Tracks

        private static TrackControlPoint P(float x, float y, float z, float width, TrackPointFlags flags = TrackPointFlags.None)
        {
            return new TrackControlPoint(x, y, z, width, flags);
        }

        private static BoostPadDefinition Pad(int segment, float t, float lateral, float width = 4f)
        {
            return new BoostPadDefinition { placement = new TrackPlacement(segment, t, lateral), width = width, length = 6f };
        }

        private static PowerUpRowDefinition Row(int segment, float t, int count = 4)
        {
            return new PowerUpRowDefinition { placement = new TrackPlacement(segment, t, 0f), count = count };
        }

        private static HazardDefinition Hazard(HazardType type, int segment, float t, float lateral, float size, float strength, float range)
        {
            return new HazardDefinition
            {
                type = type,
                placement = new TrackPlacement(segment, t, lateral),
                size = size,
                strength = strength,
                range = range
            };
        }

        public static List<TrackData> CreateTracks()
        {
            return new List<TrackData> { CreateNeonMetro(), CreateSolarCanyon(), CreateSkyLab() };
        }

        public static TrackData CreateNeonMetro()
        {
            var t = ScriptableObject.CreateInstance<TrackData>();
            t.id = "neon_metro";
            t.displayName = "Metro Neón";
            t.sceneName = "Track_NeonCity";
            t.theme = TrackTheme.NeonMetro;
            t.difficultyLabel = "FÁCIL / MEDIA";
            t.description = "Ciudad futurista de noche. Autopista elevada, túnel y un callejón secreto.";
            t.sortOrder = 0;
            t.laps = 3;
            t.expectedLapSeconds = 70f;
            t.killY = -25f;
            t.hasGroundPlane = true;
            t.groundPlaneY = -0.6f;

            t.skyColor = new Color(0.03f, 0.03f, 0.1f);
            t.horizonColor = new Color(0.25f, 0.08f, 0.4f);
            t.fogColor = new Color(0.06f, 0.04f, 0.16f);
            t.fogDensity = 0.0035f;
            t.sunColor = new Color(0.6f, 0.65f, 1f);
            t.sunIntensity = 0.5f;
            t.sunDirection = new Vector3(40f, -30f, 0f);
            t.ambientColor = new Color(0.2f, 0.2f, 0.4f);
            t.roadColor = new Color(0.16f, 0.16f, 0.2f);
            t.roadEdgeColor = new Color(0.1f, 0.9f, 1f);
            t.wallColor = new Color(0.2f, 0.2f, 0.32f);
            t.accentColor = new Color(1f, 0.2f, 0.8f);
            t.groundColor = new Color(0.08f, 0.08f, 0.12f);

            var f = TrackPointFlags.None;
            t.controlPoints = new List<TrackControlPoint>
            {
                P(0f, 0f, -60f, 16f),                                   // 0 start / finish
                P(0f, 0f, 40f, 16f),                                    // 1
                P(0f, 0f, 150f, 16f),                                   // 2
                P(20f, 0f, 215f, 15f),                                  // 3
                P(65f, 0f, 260f, 15f),                                  // 4
                P(135f, 0f, 280f, 16f),                                 // 5
                P(215f, 0f, 272f, 16f),                                 // 6
                P(275f, 0f, 240f, 14f, TrackPointFlags.DriftZone),      // 7
                P(305f, 0f, 185f, 14f, TrackPointFlags.DriftZone),      // 8
                P(315f, 3f, 125f, 14f),                                 // 9 climb
                P(310f, 9f, 60f, 13f),                                  // 10
                P(300f, 12f, -5f, 13f, TrackPointFlags.BoostZone),      // 11 elevated highway
                P(300f, 12f, -75f, 13f),                                // 12 shortcut entry
                P(292f, 10f, -145f, 13f),                               // 13
                P(268f, 5f, -205f, 13f),                                // 14
                P(228f, 0f, -255f, 14f),                                // 15
                P(170f, 0f, -285f, 14f),                                // 16 shortcut exit
                P(110f, 0f, -298f, 14f, TrackPointFlags.Tunnel),        // 17 tunnel
                P(45f, 0f, -300f, 14f, TrackPointFlags.Tunnel),         // 18 tunnel
                P(-25f, 0f, -298f, 14f),                                // 19 tunnel exit
                P(-85f, 2.5f, -290f, 14f),                              // 20 ramp
                P(-125f, 3.5f, -285f, 14f, TrackPointFlags.JumpGap),    // 21 jump
                P(-175f, 0f, -278f, 14f),                               // 22 landing
                P(-225f, 0f, -255f, 13f, TrackPointFlags.DriftZone),    // 23 hairpin
                P(-255f, 0f, -210f, 13f, TrackPointFlags.DriftZone),    // 24
                P(-245f, 0f, -160f, 13f, TrackPointFlags.DriftZone),    // 25
                P(-205f, 0f, -130f, 14f),                               // 26
                P(-150f, 0f, -130f, 15f, TrackPointFlags.BoostZone),    // 27
                P(-95f, 0f, -135f, 15f),                                // 28
                P(-48f, 0f, -122f, 15f),                                // 29
                P(-14f, 0f, -95f, 16f)                                  // 30
            };

            t.shortcuts = new List<ShortcutDefinition>
            {
                new ShortcutDefinition
                {
                    name = "Callejón",
                    entryPointIndex = 12,
                    exitPointIndex = 16,
                    width = 7f,
                    wallsEnabled = true,
                    isTunnel = false,
                    aiPreference = 0.5f,
                    aiSpeedFraction = 0.72f,
                    waypoints = new List<Vector3>
                    {
                        new Vector3(285f, 11f, -100f),
                        new Vector3(250f, 7.5f, -140f),
                        new Vector3(215f, 3.5f, -190f),
                        new Vector3(190f, 0.5f, -245f)
                    }
                }
            };

            t.boostPads = new List<BoostPadDefinition>
            {
                Pad(11, 0.35f, -3f), Pad(11, 0.35f, 3f),
                Pad(27, 0.4f, 0f, 8f)
            };

            t.powerUpRows = new List<PowerUpRowDefinition>
            {
                Row(1, 0.5f), Row(5, 0.3f), Row(10, 0.5f), Row(15, 0.5f), Row(19, 0.35f), Row(26, 0.5f)
            };

            t.hazards = new List<HazardDefinition>
            {
                Hazard(HazardType.MovingBarrier, 18, 0.5f, 0f, 4f, 1.2f, 6f),
                Hazard(HazardType.SlipperyZone, 24, 0.5f, 3f, 5f, 0.35f, 8f)
            };

            t.tips = new List<string>
            {
                "Los derrapes largos generan un turbo más poderoso.",
                "El callejón junto a la autopista elevada es más corto pero muy estrecho.",
                "Acelerá justo cuando aparece GO para salir con turbo.",
                "Las plataformas azules de la autopista dan un impulso gratis."
            };
            return t;
        }

        public static TrackData CreateSolarCanyon()
        {
            var t = ScriptableObject.CreateInstance<TrackData>();
            t.id = "solar_canyon";
            t.displayName = "Cañón Solar";
            t.sceneName = "Track_SolarCanyon";
            t.theme = TrackTheme.SolarCanyon;
            t.difficultyLabel = "MEDIA";
            t.description = "Desierto de rocas gigantes. Puentes sin barandas, una mina abandonada y curvas para derrapar.";
            t.sortOrder = 1;
            t.laps = 3;
            t.expectedLapSeconds = 88f;
            t.killY = -25f;
            t.hasGroundPlane = true;
            t.groundPlaneY = -0.6f;

            t.skyColor = new Color(0.55f, 0.75f, 1f);
            t.horizonColor = new Color(1f, 0.75f, 0.5f);
            t.fogColor = new Color(0.9f, 0.7f, 0.5f);
            t.fogDensity = 0.0012f;
            t.sunColor = new Color(1f, 0.93f, 0.8f);
            t.sunIntensity = 1.2f;
            t.sunDirection = new Vector3(50f, -40f, 0f);
            t.ambientColor = new Color(0.55f, 0.45f, 0.4f);
            t.roadColor = new Color(0.42f, 0.33f, 0.27f);
            t.roadEdgeColor = new Color(1f, 0.85f, 0.3f);
            t.wallColor = new Color(0.7f, 0.4f, 0.25f);
            t.accentColor = new Color(1f, 0.5f, 0.1f);
            t.groundColor = new Color(0.85f, 0.7f, 0.45f);

            var D = TrackPointFlags.DriftZone;
            t.controlPoints = new List<TrackControlPoint>
            {
                P(0f, 0f, -80f, 13f),                       // 0
                P(0f, 0f, 20f, 13f),                        // 1
                P(5f, 0f, 110f, 13f),                       // 2
                P(35f, 0f, 180f, 12f, D),                   // 3
                P(95f, 2f, 215f, 12f),                      // 4
                P(160f, 5f, 205f, 12f),                     // 5
                P(200f, 8f, 150f, 12f, D),                  // 6
                P(205f, 12f, 85f, 12f),                     // 7
                P(240f, 15f, 30f, 11f),                     // 8
                P(300f, 15f, 10f, 10f, TrackPointFlags.NoWalls),   // 9 bridge
                P(360f, 15f, 20f, 10f, TrackPointFlags.NoWalls),   // 10 bridge
                P(410f, 14f, 60f, 11f),                     // 11
                P(430f, 12f, 130f, 11f, D),                 // 12
                P(405f, 9f, 195f, 11f, D),                  // 13
                P(350f, 6f, 230f, 12f),                     // 14
                P(290f, 4f, 265f, 12f),                     // 15
                P(250f, 4f, 330f, 12f),                     // 16 ramp
                P(235f, 7f, 385f, 12f, TrackPointFlags.JumpGap),   // 17 jump over the ravine
                P(225f, 0f, 440f, 13f),                     // 18 landing
                P(215f, 0f, 500f, 13f, TrackPointFlags.BoostZone), // 19
                P(170f, 0f, 550f, 12f, D),                  // 20
                P(100f, 0f, 560f, 12f),                     // 21
                P(40f, 0f, 530f, 12f, D),                   // 22
                P(5f, 0f, 470f, 12f),                       // 23
                P(-40f, 0f, 420f, 12f),                     // 24
                P(-110f, 0f, 400f, 12f, D),                 // 25
                P(-165f, 2f, 350f, 12f),                    // 26
                P(-185f, 6f, 280f, 11f),                    // 27 mine shortcut entry
                P(-165f, 10f, 215f, 11f, D),                // 28
                P(-110f, 12f, 180f, 11f),                   // 29
                P(-60f, 12f, 140f, 11f, TrackPointFlags.NoWallLeft), // 30 ledge
                P(-40f, 12f, 80f, 11f, TrackPointFlags.NoWallLeft),  // 31 ledge
                P(-50f, 8f, 20f, 12f),                      // 32 mine shortcut exit
                P(-60f, 3f, -40f, 12f),                     // 33
                P(-55f, 0f, -100f, 12f, D),                 // 34
                P(-50f, 0f, -150f, 13f, D),                 // 35 hairpin
                P(-20f, 0f, -170f, 13f, D),                 // 36
                P(5f, 0f, -140f, 13f)                       // 37
            };

            t.shortcuts = new List<ShortcutDefinition>
            {
                new ShortcutDefinition
                {
                    name = "Mina abandonada",
                    entryPointIndex = 27,
                    exitPointIndex = 32,
                    width = 6.5f,
                    wallsEnabled = true,
                    isTunnel = true,
                    aiPreference = 0.55f,
                    aiSpeedFraction = 0.7f,
                    waypoints = new List<Vector3>
                    {
                        new Vector3(-165f, 5f, 215f),
                        new Vector3(-130f, 5f, 140f),
                        new Vector3(-95f, 6f, 70f)
                    }
                }
            };

            t.boostPads = new List<BoostPadDefinition>
            {
                Pad(19, 0.3f, 0f, 7f),
                Pad(9, 0.25f, 0f, 6f)
            };

            t.powerUpRows = new List<PowerUpRowDefinition>
            {
                Row(1, 0.5f), Row(5, 0.5f), Row(11, 0.5f), Row(15, 0.5f), Row(19, 0.6f), Row(24, 0.5f), Row(29, 0.5f), Row(33, 0.3f)
            };

            t.hazards = new List<HazardDefinition>
            {
                Hazard(HazardType.MovingBarrier, 23, 0.5f, 0f, 4f, 1f, 5f),
                Hazard(HazardType.SlowZone, 14, 0.5f, -3f, 6f, 0.6f, 10f)
            };

            t.tips = new List<string>
            {
                "Los puentes no tienen barandas: cuidado con los derrapes largos.",
                "La mina abandonada acorta el circuito, pero es angosta y oscura.",
                "La arena frena mucho. Un turbo ignora la penalización del terreno.",
                "Aterrizar bien después de un salto da un pequeño impulso."
            };
            return t;
        }

        public static TrackData CreateSkyLab()
        {
            var t = ScriptableObject.CreateInstance<TrackData>();
            t.id = "sky_lab";
            t.displayName = "Sky Lab";
            t.sceneName = "Track_SkyLab";
            t.theme = TrackTheme.SkyLab;
            t.difficultyLabel = "MEDIA / ALTA";
            t.description = "Laboratorio flotante sobre las nubes. Plataformas sin bordes, un tubo de gravedad y turbinas.";
            t.sortOrder = 2;
            t.laps = 3;
            t.expectedLapSeconds = 78f;
            t.killY = -30f;
            t.hasGroundPlane = false;
            t.groundPlaneY = -60f;

            t.skyColor = new Color(0.35f, 0.6f, 1f);
            t.horizonColor = new Color(0.8f, 0.9f, 1f);
            t.fogColor = new Color(0.75f, 0.85f, 1f);
            t.fogDensity = 0.0015f;
            t.sunColor = new Color(1f, 1f, 1f);
            t.sunIntensity = 1.1f;
            t.sunDirection = new Vector3(55f, 20f, 0f);
            t.ambientColor = new Color(0.6f, 0.7f, 0.85f);
            t.roadColor = new Color(0.85f, 0.88f, 0.92f);
            t.roadEdgeColor = new Color(0.2f, 0.6f, 1f);
            t.wallColor = new Color(0.75f, 0.8f, 0.9f);
            t.accentColor = new Color(0.3f, 1f, 0.8f);
            t.groundColor = new Color(0.9f, 0.95f, 1f);

            var D = TrackPointFlags.DriftZone;
            var NW = TrackPointFlags.NoWalls;
            t.controlPoints = new List<TrackControlPoint>
            {
                P(0f, 0f, -70f, 12f),                       // 0
                P(0f, 2f, 30f, 12f),                        // 1
                P(0f, 5f, 130f, 12f, TrackPointFlags.GlassRoad), // 2
                P(-25f, 8f, 200f, 11f, D),                  // 3
                P(-80f, 11f, 240f, 11f, NW),                // 4
                P(-150f, 14f, 235f, 11f, NW),               // 5
                P(-200f, 17f, 185f, 11f),                   // 6
                P(-210f, 20f, 115f, 10f, NW | D),           // 7
                P(-180f, 23f, 55f, 10f, NW),                // 8
                P(-120f, 26f, 30f, 11f, TrackPointFlags.BoostZone), // 9
                P(-60f, 28f, 35f, 11f, TrackPointFlags.JumpGap),    // 10 jump over the void
                P(10f, 26f, 60f, 12f),                      // 11
                P(80f, 24f, 100f, 12f, TrackPointFlags.Tunnel),     // 12 gravity tube
                P(150f, 22f, 120f, 12f, TrackPointFlags.Tunnel),    // 13
                P(220f, 20f, 110f, 12f),                    // 14 shortcut entry
                P(275f, 18f, 60f, 11f, NW | D),             // 15
                P(285f, 15f, -10f, 11f, NW),                // 16
                P(270f, 11f, -80f, 11f, D),                 // 17
                P(235f, 8f, -135f, 11f),                    // 18 shortcut exit
                P(185f, 8f, -165f, 11f, TrackPointFlags.BoostZone), // 19
                P(120f, 7f, -185f, 11f),                    // 20
                P(55f, 6f, -205f, 11f, NW | D),             // 21
                P(0f, 4f, -240f, 11f, NW | TrackPointFlags.GlassRoad), // 22
                P(-60f, 3f, -250f, 11f),                    // 23
                P(-120f, 2f, -235f, 11f, D),                // 24
                P(-175f, 1f, -210f, 10f, NW | D),           // 25
                P(-200f, 0f, -160f, 10f, NW),               // 26
                P(-185f, 0f, -110f, 11f),                   // 27
                P(-140f, 0f, -92f, 11f, TrackPointFlags.BoostZone), // 28
                P(-85f, 0f, -100f, 11f),                    // 29
                P(-42f, 0f, -108f, 11f),                    // 30
                P(-12f, 0f, -95f, 12f)                      // 31
            };

            t.shortcuts = new List<ShortcutDefinition>
            {
                new ShortcutDefinition
                {
                    name = "Pasarela de servicio",
                    entryPointIndex = 14,
                    exitPointIndex = 18,
                    width = 6f,
                    wallsEnabled = false,
                    isTunnel = false,
                    aiPreference = 0.45f,
                    aiSpeedFraction = 0.8f,
                    waypoints = new List<Vector3>
                    {
                        new Vector3(240f, 17f, 50f),
                        new Vector3(245f, 13f, -20f),
                        new Vector3(240f, 10f, -90f)
                    }
                }
            };

            t.boostPads = new List<BoostPadDefinition>
            {
                Pad(9, 0.6f, 0f, 8f),
                Pad(19, 0.3f, -2.5f), Pad(19, 0.3f, 2.5f),
                Pad(28, 0.4f, 0f, 7f)
            };

            t.powerUpRows = new List<PowerUpRowDefinition>
            {
                Row(1, 0.5f), Row(5, 0.5f), Row(11, 0.5f), Row(14, 0.3f), Row(18, 0.5f), Row(23, 0.5f), Row(28, 0.6f)
            };

            t.hazards = new List<HazardDefinition>
            {
                Hazard(HazardType.MovingBarrier, 13, 0.5f, 0f, 4f, 1.4f, 6f),
                Hazard(HazardType.Fan, 20, 0.5f, 0f, 10f, 6f, 16f)
            };

            t.tips = new List<string>
            {
                "Muchas plataformas no tienen barandas. Si te caés, reaparecés en el último checkpoint.",
                "La pasarela de servicio es un atajo sin barandas: sólo para pilotos seguros.",
                "Las turbinas empujan el kart de costado. Compensá con la dirección.",
                "Tomá impulso en la plataforma azul antes del gran salto."
            };
            return t;
        }
    }
}
