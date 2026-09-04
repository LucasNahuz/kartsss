using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VortexKarts.VR;

namespace VortexKarts.Core
{
    /// <summary>
    /// Async scene loading with a VR-safe fade. Optionally routes through the Loading scene so the
    /// player sees the track name and a tip while the heavy track generates.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public bool IsLoading { get; private set; }
        public string TargetScene { get; private set; }

        private const float FadeDuration = 0.35f;
        private const float MinLoadingScreenTime = 1.6f;

        public void LoadScene(string sceneName, bool viaLoadingScreen)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName, viaLoadingScreen));
        }

        private IEnumerator LoadRoutine(string sceneName, bool viaLoadingScreen)
        {
            IsLoading = true;
            TargetScene = sceneName;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            yield return Fade(1f);

            if (viaLoadingScreen && Application.CanStreamedLevelBeLoaded(GameManager.SceneLoading))
            {
                var loadingOp = SceneManager.LoadSceneAsync(GameManager.SceneLoading, LoadSceneMode.Single);
                while (loadingOp != null && !loadingOp.isDone) yield return null;
                yield return Fade(0f);
                float shown = 0f;
                // Give the loading screen a moment to build its UI before starting the heavy load.
                yield return null;
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError("[SceneLoader] Scene not found: " + sceneName);
                    IsLoading = false;
                    yield break;
                }
                op.allowSceneActivation = false;
                while (op.progress < 0.9f || shown < MinLoadingScreenTime)
                {
                    shown += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return Fade(1f);
                op.allowSceneActivation = true;
                while (!op.isDone) yield return null;
            }
            else
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError("[SceneLoader] Scene not found: " + sceneName);
                    yield return Fade(0f);
                    IsLoading = false;
                    yield break;
                }
                while (!op.isDone) yield return null;
            }

            // Let the new scene run its Awake/Start (track generation) before revealing it.
            yield return null;
            yield return null;
            yield return Fade(0f);
            IsLoading = false;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            var vr = VRManager.Instance;
            if (vr != null && vr.Fader != null)
            {
                yield return vr.Fader.FadeTo(targetAlpha, FadeDuration);
            }
        }
    }
}
