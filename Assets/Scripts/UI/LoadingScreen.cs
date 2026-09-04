using UnityEngine;
using UnityEngine.UI;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Utils;
using VortexKarts.VR;

namespace VortexKarts.UI
{
    /// <summary>Light loading scene: track name, difficulty and a gameplay tip while the circuit generates.</summary>
    public class LoadingScreen : MonoBehaviour
    {
        private Text dots;
        private float timer;

        private void Start()
        {
            var gm = GameManager.EnsureExists();
            var track = gm.Setup.Track;

            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.4f);
            RenderSettings.skybox = null;
            if (VRManager.Instance != null) VRManager.Instance.SetBackgroundColor(new Color(0.02f, 0.02f, 0.05f));

            var anchor = new GameObject("LoadingSeat").transform;
            anchor.position = new Vector3(0f, 1.2f, 0f);
            if (VRManager.Instance != null) VRManager.Instance.AttachToStaticAnchor(anchor);

            var canvas = UIFactory.CreateWorldCanvas("LoadingCanvas", anchor, new Vector3(0f, 0f, 2.5f), Quaternion.identity, new Vector2(1600f, 800f), 0.0013f);
            var t = canvas.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(1500f, 700f), UIFactory.Bg);
            string name = track != null ? track.displayName.ToUpperInvariant() : "CARGANDO";
            UIFactory.Label(t, name, new Vector2(0f, 200f), new Vector2(1400f, 120f), 96, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            string diff = track != null ? track.difficultyLabel : "";
            var difficulty = gm.Setup.Difficulty;
            if (difficulty != null) diff += "   ·   CPU: " + difficulty.displayName.ToUpperInvariant();
            UIFactory.Label(t, diff, new Vector2(0f, 110f), new Vector2(1400f, 50f), 32, UIFactory.TextDim);

            string tip = "Los derrapes largos generan un turbo más poderoso.";
            if (track != null && track.tips != null && track.tips.Count > 0) tip = track.tips[Random.Range(0, track.tips.Count)];
            UIFactory.Label(t, "TIP", new Vector2(0f, 0f), new Vector2(400f, 40f), 28, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Label(t, tip, new Vector2(0f, -70f), new Vector2(1300f, 100f), 34, UIFactory.TextColor);
            dots = UIFactory.Label(t, "CARGANDO", new Vector2(0f, -260f), new Vector2(600f, 60f), 34, UIFactory.TextDim);

            // A slowly spinning prism so the scene is not static.
            var spinner = PrimitiveFactory.Box("Spinner", anchor, new Vector3(0f, -0.9f, 2.4f), Vector3.one * 0.35f,
                MaterialLibrary.Emissive(Color.black, UIFactory.Accent, 2f), false, Quaternion.Euler(45f, 0f, 45f));
            spinner.AddComponent<SimpleSpinner>();
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (dots != null)
            {
                int n = (int)(timer * 3f) % 4;
                dots.text = "CARGANDO" + new string('.', n);
            }
        }
    }

    public class SimpleSpinner : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(30f * Time.unscaledDeltaTime, 80f * Time.unscaledDeltaTime, 20f * Time.unscaledDeltaTime, Space.World);
        }
    }
}
