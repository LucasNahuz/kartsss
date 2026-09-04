using System;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// The kart's item slot. One slot today; Slots is an array so a second slot can be enabled later
    /// without touching callers (HUD and AI read through the public accessors).
    /// </summary>
    public class PowerUpInventory : MonoBehaviour
    {
        public const int SlotCount = 1;

        private class Slot
        {
            public PowerUpData Data;
            public int Charges;
        }

        private readonly Slot[] slots = new Slot[SlotCount];
        private KartController kart;
        private float lastUseTime = -10f;
        private float heldSince;

        public event Action<PowerUpData, int> OnChanged;

        public PowerUpData Current => slots[0] != null ? slots[0].Data : null;
        public int ChargesLeft => slots[0] != null ? slots[0].Charges : 0;
        public bool HasItem => slots[0] != null && slots[0].Charges > 0;
        public float HeldSeconds => HasItem ? Time.time - heldSince : 0f;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        public bool CanPickUp => !HasItem;

        public void Give(PowerUpData data)
        {
            if (data == null) return;
            slots[0] = new Slot { Data = data, Charges = Mathf.Max(1, data.charges) };
            heldSince = Time.time;
            OnChanged?.Invoke(data, slots[0].Charges);
            GameEvents.RaisePowerUpCollected(kart, data);
        }

        public void Clear()
        {
            slots[0] = null;
            OnChanged?.Invoke(null, 0);
        }

        /// <summary>Uses one charge. Returns false when nothing was used.</summary>
        public bool TryUse()
        {
            if (!HasItem) return false;
            if (kart == null || kart.Status.IsStunned || kart.Status.ControlsLocked || kart.InputLocked || kart.HasFinished) return false;
            if (Time.time - lastUseTime < 0.25f) return false;
            var manager = PowerUpManager.Instance;
            if (manager == null) return false;

            var slot = slots[0];
            bool used = manager.Activate(kart, slot.Data);
            if (!used) return false;
            lastUseTime = Time.time;
            slot.Charges--;
            GameEvents.RaisePowerUpUsed(kart, slot.Data);
            if (slot.Charges <= 0)
            {
                slots[0] = null;
                OnChanged?.Invoke(null, 0);
            }
            else
            {
                OnChanged?.Invoke(slot.Data, slot.Charges);
            }
            return true;
        }

        private void LateUpdate()
        {
            if (kart != null && kart.Input.UsePowerUp) TryUse();
        }
    }
}
