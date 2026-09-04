namespace VortexKarts.Kart
{
    /// <summary>Where a boost came from. Used for FX intensity and statistics.</summary>
    public enum BoostSource
    {
        Drift = 0,
        PowerUp = 1,
        Pad = 2,
        Landing = 3,
        Start = 4,
        Slipstream = 5,
        MegaBoost = 6
    }

    /// <summary>What hit a kart. Drives status effects and feedback.</summary>
    public enum KartHitKind
    {
        Projectile = 0,
        Mine = 1,
        OilSlick = 2,
        Emp = 3,
        Wall = 4,
        Kart = 5,
        Hazard = 6
    }

    /// <summary>Type of ground under the kart.</summary>
    public enum SurfaceType
    {
        Road = 0,
        OffRoad = 1,
        Slippery = 2,
        Slow = 3,
        Boost = 4
    }

    /// <summary>Timed status effects applied to a kart.</summary>
    public enum StatusEffect
    {
        SpinOut = 0,
        Slow = 1,
        Slippery = 2,
        Stunned = 3,
        Shield = 4,
        Invulnerable = 5,
        Armor = 6
    }
}
