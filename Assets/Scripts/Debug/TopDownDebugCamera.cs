using UnityEngine;
using UnityEngine.Rendering.Universal;
using VortexKarts.Kart;
using VortexKarts.Race;

namespace VortexKarts.Debugging
{
    /// <summary>
    /// Development aid (-topdown): a camera high above the player's kart rendering on top of the main view.
    /// Useful to check track generation and AI behaviour from automated runs. Never used in VR.
    /// </summary>
    public class TopDownDebugCamera : MonoBehaviour
    {
        public float Height = 90f;
        private Camera cam;
        private RaceManager race;

        public static TopDownDebugCamera Create(RaceManager raceManager, float height)
        {
            var go = new GameObject("TopDownDebugCamera");
            var c = go.AddComponent<TopDownDebugCamera>();
            c.race = raceManager;
            c.Height = height;
            c.cam = go.AddComponent<Camera>();
            c.cam.depth = 50f;
            c.cam.clearFlags = CameraClearFlags.SolidColor;
            c.cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            c.cam.fieldOfView = 70f;
            c.cam.nearClipPlane = 1f;
            c.cam.farClipPlane = 2000f;
            var data = c.cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.allowXRRendering = false;
                data.renderShadows = false;
                data.renderPostProcessing = false;
            }
            return c;
        }

        private void LateUpdate()
        {
            var kart = race != null ? race.PlayerKart : null;
            if (kart == null) return;
            Vector3 target = kart.Position;
            transform.position = target + Vector3.up * Height + Vector3.back * 0.01f;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }
    }
}
