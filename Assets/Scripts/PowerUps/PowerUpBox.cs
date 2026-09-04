using UnityEngine;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.PowerUps
{
    /// <summary>
    /// Floating energy prism that grants a power-up. Hides for a few seconds after being collected and
    /// reappears with a pop. Multiple karts crossing in the same frame each get an item.
    /// </summary>
    public class PowerUpBox : MonoBehaviour
    {
        private const float RespawnSeconds = 5f;

        private PowerUpManager manager;
        private Transform visual;
        private Transform innerCore;
        private Material coreMat;
        private float hiddenUntil = -1f;
        private bool visible = true;
        private float phase;
        private float popScale = 1f;

        public bool IsAvailable => visible;

        public static PowerUpBox Create(PowerUpManager manager, Transform parent, Vector3 position)
        {
            var go = new GameObject("PowerUpBox");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = Layers.PowerUp;
            var box = go.AddComponent<PowerUpBox>();
            box.manager = manager;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.2f, 2.6f, 2.2f);

            box.visual = new GameObject("Visual").transform;
            box.visual.SetParent(go.transform, false);
            var shell = MaterialLibrary.UnlitTransparent(new Color(0.4f, 0.9f, 1f, 0.45f));
            var a = PrimitiveFactory.Box("ShellA", box.visual, Vector3.zero, Vector3.one * 1.1f, shell, false, Quaternion.Euler(45f, 0f, 45f));
            var b = PrimitiveFactory.Box("ShellB", box.visual, Vector3.zero, Vector3.one * 1.1f, shell, false, Quaternion.Euler(0f, 45f, 45f));
            PrimitiveFactory.SetShadowCasting(a, false, false);
            PrimitiveFactory.SetShadowCasting(b, false, false);
            box.coreMat = MaterialLibrary.Emissive(Color.white, new Color(1f, 0.5f, 0.9f), 2.5f);
            var core = PrimitiveFactory.Sphere("Core", box.visual, Vector3.zero, 0.5f, box.coreMat);
            PrimitiveFactory.SetShadowCasting(core, false, false);
            box.innerCore = core.transform;
            box.phase = Random.value * 6f;
            return box;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!visible)
            {
                if (Time.time >= hiddenUntil)
                {
                    visible = true;
                    visual.gameObject.SetActive(true);
                    popScale = 0.05f;
                    if (manager != null) manager.Burst(transform.position, new Color(0.5f, 0.9f, 1f), 12);
                }
                return;
            }
            phase += dt;
            popScale = Mathf.MoveTowards(popScale, 1f, dt * 3f);
            visual.localPosition = new Vector3(0f, Mathf.Sin(phase * 2f) * 0.18f, 0f);
            visual.localRotation = Quaternion.Euler(phase * 30f, phase * 70f, phase * 20f);
            visual.localScale = Vector3.one * popScale;
            if (innerCore != null) innerCore.localScale = Vector3.one * (0.5f + Mathf.Sin(phase * 5f) * 0.08f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!visible) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var kart = rb.GetComponent<KartController>();
            if (kart == null) return;
            var inventory = kart.GetComponent<PowerUpInventory>();
            if (inventory == null || !inventory.CanPickUp) return;

            var data = manager != null ? manager.Roll(kart) : null;
            if (data == null) return;
            inventory.Give(data);

            visible = false;
            hiddenUntil = Time.time + RespawnSeconds;
            visual.gameObject.SetActive(false);
            if (manager != null) manager.Burst(transform.position, data.iconColor, 22);
        }
    }
}
