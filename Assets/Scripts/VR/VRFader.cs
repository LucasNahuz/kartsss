using System.Collections;
using UnityEngine;
using VortexKarts.Utils;

namespace VortexKarts.VR
{
    /// <summary>
    /// Black quad in front of the camera used for scene transitions and respawns. Uses unscaled time so it
    /// works while the game is paused.
    /// </summary>
    public class VRFader : MonoBehaviour
    {
        private Material material;
        private Renderer quadRenderer;
        private float alpha;
        private Coroutine running;

        public float Alpha => alpha;
        public bool IsOpaque => alpha >= 0.999f;

        public static VRFader Create(Transform cameraTransform)
        {
            var mat = MaterialLibrary.UnlitTransparent(new Color(0f, 0f, 0f, 1f), MaterialLibrary.WhiteTexture);
            mat.renderQueue = 4001;
            var quad = PrimitiveFactory.Quad("Fader", cameraTransform, new Vector3(0f, 0f, 0.25f), new Vector2(4f, 4f), mat);
            quad.layer = 2;
            PrimitiveFactory.SetShadowCasting(quad, false, false);
            var fader = quad.AddComponent<VRFader>();
            fader.material = mat;
            fader.quadRenderer = quad.GetComponent<Renderer>();
            fader.SetAlpha(1f); // start black; SceneLoader / bootstrap fades in
            return fader;
        }

        public void SetAlpha(float a)
        {
            alpha = Mathf.Clamp01(a);
            var c = new Color(0f, 0f, 0f, alpha);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
            else material.color = c;
            if (quadRenderer != null) quadRenderer.enabled = alpha > 0.002f;
        }

        /// <summary>Fades to the target alpha; yield on it to wait.</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeRoutine(target, duration));
            yield return running;
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float start = alpha;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(target);
            running = null;
        }
    }
}
