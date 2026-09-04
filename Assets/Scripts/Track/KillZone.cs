using UnityEngine;
using VortexKarts.Kart;

namespace VortexKarts.Track
{
    /// <summary>Trigger volume under jump gaps and pits: entering it respawns the kart.</summary>
    public class KillZone : MonoBehaviour
    {
        public static KillZone Create(Transform parent, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject("KillZone");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.layer = 2;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return go.AddComponent<KillZone>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var respawn = rb.GetComponent<RespawnController>();
            if (respawn != null) respawn.RequestRespawn("kill zone");
        }
    }
}
