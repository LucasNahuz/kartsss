using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VortexKarts.Core;
using VortexKarts.Race;
using VortexKarts.VR;

namespace VortexKarts.UI
{
    /// <summary>
    /// In-race pause menu. The world freezes (timeScale 0) but head tracking keeps running because the
    /// VR rig updates with unscaled time. The panel floats in front of the seat.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private RaceManager race;
        private Canvas canvas;
        private GameObject rootPanel;
        private SettingsMenu settings;
        private Selectable first;
        private Text messageText;

        public static PauseMenu Create(RaceManager raceManager)
        {
            var go = new GameObject("PauseMenu");
            var pm = go.AddComponent<PauseMenu>();
            pm.race = raceManager;
            pm.Build();
            return pm;
        }

        private void Build()
        {
            UIFactory.EnsureEventSystem();
            Transform parent = VRManager.Instance != null ? VRManager.Instance.Rig : null;
            canvas = UIFactory.CreateWorldCanvas("PauseCanvas", parent, new Vector3(0f, -0.1f, 2.1f), Quaternion.identity, new Vector2(1600f, 1000f), 0.0011f);
            var root = canvas.transform;

            rootPanel = new GameObject("Root");
            rootPanel.transform.SetParent(root, false);
            var rt = rootPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1600f, 1000f);
            var t = rootPanel.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(820f, 720f), UIFactory.Bg);
            UIFactory.Label(t, "PAUSA", new Vector2(0f, 280f), new Vector2(800f, 90f), 70, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            messageText = UIFactory.Label(t, "", new Vector2(0f, 215f), new Vector2(800f, 40f), 26, UIFactory.Warn);
            var resume = UIFactory.Button(t, "CONTINUAR", new Vector2(0f, 120f), new Vector2(560f, 92f), () => race.SetPaused(false), 42);
            var restart = UIFactory.Button(t, "REINICIAR CARRERA", new Vector2(0f, 5f), new Vector2(560f, 92f), () => race.Restart(), 40);
            var config = UIFactory.Button(t, "CONFIGURACIÓN", new Vector2(0f, -110f), new Vector2(560f, 92f), ShowSettings, 40);
            var exit = UIFactory.Button(t, "SALIR AL MENÚ", new Vector2(0f, -225f), new Vector2(560f, 92f), () => race.ExitToMenu(), 40);
            UIFactory.LinkVertical(new List<Selectable> { resume, restart, config, exit });
            first = resume;

            settings = SettingsMenu.Build(root, ShowRoot);
            settings.Hide();
            canvas.gameObject.SetActive(false);

            GameEvents.OnPauseChanged += OnPauseChanged;
            race.OnRaceMessage += OnMessage;
        }

        private void OnDestroy()
        {
            GameEvents.OnPauseChanged -= OnPauseChanged;
            if (race != null) race.OnRaceMessage -= OnMessage;
        }

        private void OnMessage(string msg)
        {
            if (messageText != null && (msg.Contains("CONTROL") || msg.Contains("TRACKING"))) messageText.text = msg;
        }

        private void OnPauseChanged(bool paused)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(paused);
            if (paused)
            {
                if (!race.WasAutoPaused && messageText != null) messageText.text = "";
                if (InputManager.Instance != null) InputManager.Instance.SetKartInputEnabled(false);
                ShowRoot();
            }
            else
            {
                if (InputManager.Instance != null) InputManager.Instance.SetKartInputEnabled(true);
                UIFactory.SetCancelHandler(null);
            }
        }

        private void ShowRoot()
        {
            settings.Hide();
            rootPanel.SetActive(true);
            UIFactory.SetCancelHandler(() => race.SetPaused(false));
            UIFactory.Select(first);
        }

        private void ShowSettings()
        {
            rootPanel.SetActive(false);
            settings.Show();
        }
    }
}
