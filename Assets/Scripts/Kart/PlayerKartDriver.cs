using UnityEngine;
using VortexKarts.Core;

namespace VortexKarts.Kart
{
    /// <summary>Feeds the player's processed input into the kart every frame.</summary>
    public class PlayerKartDriver : MonoBehaviour
    {
        private KartController kart;

        private void Awake()
        {
            kart = GetComponent<KartController>();
        }

        private void Update()
        {
            if (kart == null || InputManager.Instance == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            kart.SetInput(InputManager.Instance.ReadKartInput());
        }
    }
}
