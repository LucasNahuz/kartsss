using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Layer lookups with fallbacks so the code works even if TagManager was not imported.
    /// Layer indices match ProjectSettings/TagManager.asset.
    /// </summary>
    public static class Layers
    {
        private static int kart = -1, track = -1, hazard = -1, powerUp = -1, wall = -1, projectile = -1;

        private static int Resolve(ref int cache, string name, int fallback)
        {
            if (cache >= 0) return cache;
            int l = LayerMask.NameToLayer(name);
            cache = l >= 0 ? l : fallback;
            return cache;
        }

        public static int Kart => Resolve(ref kart, "Kart", 6);
        public static int Track => Resolve(ref track, "Track", 7);
        public static int Hazard => Resolve(ref hazard, "Hazard", 8);
        public static int PowerUp => Resolve(ref powerUp, "PowerUp", 9);
        public static int Wall => Resolve(ref wall, "Wall", 10);
        public static int Projectile => Resolve(ref projectile, "Projectile", 11);

        public const int IgnoreRaycast = 2;

        /// <summary>Everything a kart can drive on or bump into (no karts, pickups, hazards, projectiles).</summary>
        public static int GroundMask => ~((1 << Kart) | (1 << PowerUp) | (1 << Hazard) | (1 << Projectile) | (1 << IgnoreRaycast));

        /// <summary>Solid geometry that stops projectiles.</summary>
        public static int SolidMask => (1 << Track) | (1 << Wall) | (1 << 0);

        public static int KartMask => 1 << Kart;
    }
}
