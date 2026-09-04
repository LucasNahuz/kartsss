using UnityEngine;
using VortexKarts.Race;
using VortexKarts.UI;

namespace VortexKarts.Core
{
    public enum SceneKind
    {
        Bootstrap = 0,
        MainMenu = 1,
        Loading = 2,
        Track = 3
    }

    /// <summary>
    /// The only component that lives inside a saved scene file. Everything else (track geometry, karts,
    /// UI, VR rig) is generated at runtime, which keeps scene files tiny and merge-friendly.
    /// </summary>
    public class SceneEntry : MonoBehaviour
    {
        public SceneKind kind = SceneKind.Track;
        [Tooltip("TrackData id for Track scenes. Empty = match by scene name.")]
        public string trackId = "";

        private void Awake()
        {
            var gm = GameManager.EnsureExists();
            switch (kind)
            {
                case SceneKind.Bootstrap:
                    gm.OnBootstrapScene();
                    break;
                case SceneKind.MainMenu:
                    if (GetComponent<MainMenuController>() == null) gameObject.AddComponent<MainMenuController>();
                    break;
                case SceneKind.Loading:
                    if (GetComponent<LoadingScreen>() == null) gameObject.AddComponent<LoadingScreen>();
                    break;
                case SceneKind.Track:
                    var controller = GetComponent<TrackSceneController>();
                    if (controller == null) controller = gameObject.AddComponent<TrackSceneController>();
                    controller.TrackId = trackId;
                    break;
            }
        }
    }
}
