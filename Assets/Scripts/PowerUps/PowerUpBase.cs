using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Kart;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// Behaviour of one power-up kind. Stateless: PowerUpManager keeps one instance per kind and calls
    /// Activate for every use. Adding a power-up = new subclass + new PowerUpKind + PowerUpData asset.
    /// </summary>
    public abstract class PowerUpBase
    {
        public abstract PowerUpKind Kind { get; }

        /// <summary>Performs the effect. Return false to refuse (charge is kept).</summary>
        public abstract bool Activate(KartController user, PowerUpData data, PowerUpManager manager);

        /// <summary>Whether firing now makes sense for the AI (used by AIKartController).</summary>
        public virtual bool IsOffensive => false;
    }

    public class TurboPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.Turbo;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            user.Boost.AddBoost(BoostSource.PowerUp, data.magnitude, data.duration, 2.2f);
            return true;
        }
    }

    public class TripleTurboPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.TripleTurbo;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            user.Boost.AddBoost(BoostSource.PowerUp, data.magnitude, data.duration, 2.2f);
            return true;
        }
    }

    public class MegaBoostPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.MegaBoost;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            user.Boost.AddBoost(BoostSource.MegaBoost, data.magnitude, data.duration, 2.6f);
            user.Status.Apply(StatusEffect.Armor, data.duration);
            return true;
        }
    }

    public class HomingMissilePowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.HomingMissile;
        public override bool IsOffensive => true;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            var target = manager.FindTargetAhead(user);
            manager.SpawnProjectile(user, data, true, target);
            return true;
        }
    }

    public class StraightShotPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.StraightShot;
        public override bool IsOffensive => true;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            manager.SpawnProjectile(user, data, false, null);
            return true;
        }
    }

    public class TripleShotPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.TripleShot;
        public override bool IsOffensive => true;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            manager.SpawnProjectile(user, data, false, null);
            return true;
        }
    }

    public class MinePowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.Mine;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            manager.SpawnMine(user, data);
            return true;
        }
    }

    public class OilSlickPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.OilSlick;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            manager.SpawnOilSlick(user, data);
            return true;
        }
    }

    public class ShieldPowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.Shield;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            user.Status.Apply(StatusEffect.Shield, data.duration);
            return true;
        }
    }

    public class EmpPulsePowerUp : PowerUpBase
    {
        public override PowerUpKind Kind => PowerUpKind.EmpPulse;
        public override bool IsOffensive => true;

        public override bool Activate(KartController user, PowerUpData data, PowerUpManager manager)
        {
            manager.EmpBurst(user, data);
            return true;
        }
    }
}
