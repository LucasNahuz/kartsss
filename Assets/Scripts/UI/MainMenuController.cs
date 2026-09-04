using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VortexKarts.Audio;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.Utils;
using VortexKarts.VR;

namespace VortexKarts.UI
{
    /// <summary>
    /// Main menu: the player sits in a showcase kart on a small platform and the menu floats ahead.
    /// Flow: JUGAR → circuito → dificultad → kart/piloto → carrera. CONFIGURACIÓN. SALIR.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private Canvas canvas;
        private Transform root;
        private GameObject rootPanel, trackPanel, difficultyPanel, kartPanel;
        private SettingsMenu settings;
        private Transform seatAnchor;
        private KartController showcaseKart;
        private Transform showcaseParent;
        private int trackIndex, difficultyIndex, kartIndex, pilotIndex;
        private Text kartName, kartDesc, kartStats, pilotName, pilotBio, trackInfo;
        private Selectable firstRoot, firstTrack, firstDifficulty, firstKart;

        private void Start()
        {
            var gm = GameManager.EnsureExists();
            GameDatabase.EnsureLoaded();
            UIFactory.EnsureEventSystem();

            var setup = gm.Setup;
            trackIndex = IndexOfTrack(setup.TrackId);
            difficultyIndex = Mathf.Clamp(setup.DifficultyIndex, 0, GameDatabase.Difficulties.Count - 1);
            kartIndex = IndexOfKart(setup.KartId);
            pilotIndex = IndexOfPilot(setup.PilotId);

            BuildEnvironment();
            BuildShowcaseKart();
            BuildUI();
            ShowRoot();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
        }

        private int IndexOfTrack(string id)
        {
            for (int i = 0; i < GameDatabase.Tracks.Count; i++) if (GameDatabase.Tracks[i].id == id) return i;
            return 0;
        }

        private int IndexOfKart(string id)
        {
            for (int i = 0; i < GameDatabase.Karts.Count; i++) if (GameDatabase.Karts[i].id == id) return i;
            return 0;
        }

        private int IndexOfPilot(string id)
        {
            for (int i = 0; i < GameDatabase.Pilots.Count; i++) if (GameDatabase.Pilots[i].id == id) return i;
            return 0;
        }

        // ------------------------------------------------------------------ Environment

        private void BuildEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.4f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.12f);
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.skybox = null;
            if (VRManager.Instance != null) VRManager.Instance.SetBackgroundColor(new Color(0.03f, 0.03f, 0.09f));

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.8f, 0.85f, 1f);
            sun.intensity = 0.9f;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sun.shadows = LightShadows.Soft;

            var env = new GameObject("MenuEnvironment").transform;
            var floor = PrimitiveFactory.Cylinder("Platform", env, new Vector3(0f, -0.5f, 2f), 22f, 1f, MaterialLibrary.Lit(new Color(0.1f, 0.11f, 0.16f), 0.5f, 0.3f));
            PrimitiveFactory.SetShadowCasting(floor, false, true);
            // Slightly wider and lower than the platform so only a thin glowing rim shows.
            var ring = PrimitiveFactory.Cylinder("PlatformRing", env, new Vector3(0f, -0.09f, 2f), 22.6f, 0.05f, MaterialLibrary.Emissive(Color.black, UIFactory.Accent, 2f));
            PrimitiveFactory.SetShadowCasting(ring, false, false);
            var rng = new System.Random(42);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                float r = 30f + (float)rng.NextDouble() * 25f;
                float h = 8f + (float)rng.NextDouble() * 30f;
                var pillar = PrimitiveFactory.Box("Pillar", env, new Vector3(Mathf.Cos(a) * r, h * 0.5f - 1f, Mathf.Sin(a) * r + 2f),
                    new Vector3(3f + (float)rng.NextDouble() * 4f, h, 3f + (float)rng.NextDouble() * 4f),
                    MaterialLibrary.Lit(new Color(0.08f, 0.09f, 0.14f), 0.4f, 0.2f));
                var glow = PrimitiveFactory.Box("PillarGlow", pillar.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.02f, 0.02f, 1.02f),
                    MaterialLibrary.Emissive(Color.black, i % 2 == 0 ? UIFactory.Accent : UIFactory.Accent2, 2f));
                PrimitiveFactory.SetShadowCasting(glow, false, false);
            }

            // Title sign.
            var sign = PrimitiveFactory.Box("TitleSign", env, new Vector3(0f, 6.5f, 14f), new Vector3(10f, 2.2f, 0.3f),
                MaterialLibrary.Emissive(Color.black, UIFactory.Accent2, 1.5f));
            PrimitiveFactory.SetShadowCasting(sign, false, false);
            // Canvas forward must point away from the viewer (who looks along +Z) or the text renders mirrored.
            var titleCanvas = UIFactory.CreateWorldCanvas("TitleCanvas", env, new Vector3(0f, 6.5f, 13.8f), Quaternion.identity,
                new Vector2(1000f, 220f), 0.01f);
            UIFactory.Label(titleCanvas.transform, "VORTEX KARTS", Vector2.zero, new Vector2(1000f, 220f), 150, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildShowcaseKart()
        {
            if (showcaseParent == null)
            {
                showcaseParent = new GameObject("Showcase").transform;
            }
            if (showcaseKart != null) Destroy(showcaseKart.gameObject);

            var stats = GameDatabase.Karts[kartIndex];
            var pilot = GameDatabase.Pilots[pilotIndex];
            showcaseKart = KartFactory.CreateKart(stats, pilot, true, Vector3.zero, Quaternion.identity, 0);
            showcaseKart.transform.SetParent(showcaseParent, true);
            // Static display: no physics simulation, no driver, no engine audio.
            var driver = showcaseKart.GetComponent<PlayerKartDriver>();
            if (driver != null) Destroy(driver);
            showcaseKart.Body.isKinematic = true;
            showcaseKart.InputLocked = true;
            showcaseKart.enabled = false;
            foreach (var va in showcaseKart.GetComponentsInChildren<VehicleAudio>()) Destroy(va);
            foreach (var audio in showcaseKart.GetComponentsInChildren<AudioSource>()) Destroy(audio);

            seatAnchor = showcaseKart.SeatAnchor;
            if (VRManager.Instance != null) VRManager.Instance.AttachToStaticAnchor(seatAnchor);
            if (canvas != null) PositionCanvas();
        }

        private void PositionCanvas()
        {
            canvas.transform.SetParent(seatAnchor, false);
            canvas.transform.localPosition = new Vector3(0f, 0.05f, 2.3f);
            canvas.transform.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------ UI

        private void BuildUI()
        {
            canvas = UIFactory.CreateWorldCanvas("MenuCanvas", null, Vector3.zero, Quaternion.identity, new Vector2(1600f, 1000f), 0.0012f);
            root = canvas.transform;
            PositionCanvas();

            BuildRootPanel();
            BuildTrackPanel();
            BuildDifficultyPanel();
            BuildKartPanel();
            settings = SettingsMenu.Build(root, ShowRoot);
            settings.Hide();
        }

        private GameObject NewPanel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1600f, 1000f);
            return go;
        }

        private void BuildRootPanel()
        {
            rootPanel = NewPanel("Root");
            var t = rootPanel.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(900f, 760f), UIFactory.Bg);
            UIFactory.Label(t, "VORTEX KARTS", new Vector2(0f, 280f), new Vector2(880f, 110f), 84, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Label(t, "VR ARCADE RACING", new Vector2(0f, 205f), new Vector2(880f, 50f), 30, UIFactory.TextDim);
            var play = UIFactory.Button(t, "JUGAR", new Vector2(0f, 60f), new Vector2(600f, 110f), ShowTracks, 52);
            var config = UIFactory.Button(t, "CONFIGURACIÓN", new Vector2(0f, -80f), new Vector2(600f, 110f), ShowSettings, 46);
            var quit = UIFactory.Button(t, "SALIR", new Vector2(0f, -220f), new Vector2(600f, 110f), () => GameManager.Instance.QuitGame(), 46);
            UIFactory.LinkVertical(new List<Selectable> { play, config, quit });
            firstRoot = play;
            string hint = VRManager.Instance != null && VRManager.Instance.IsVRActive ? "Select / R: recentrar visor" : "Modo escritorio (sin visor detectado)";
            UIFactory.Label(t, hint, new Vector2(0f, -330f), new Vector2(880f, 40f), 24, UIFactory.TextDim);
        }

        private void BuildTrackPanel()
        {
            trackPanel = NewPanel("Tracks");
            var t = trackPanel.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(1500f, 900f), UIFactory.Bg);
            UIFactory.Label(t, "ELEGÍ UN CIRCUITO", new Vector2(0f, 390f), new Vector2(1400f, 80f), 56, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var buttons = new List<Selectable>();
            var tracks = GameDatabase.Tracks;
            float y = 270f;
            for (int i = 0; i < tracks.Count; i++)
            {
                int idx = i;
                var track = tracks[i];
                var b = UIFactory.Button(t, track.displayName.ToUpperInvariant() + "   ·   " + track.difficultyLabel, new Vector2(0f, y), new Vector2(1100f, 96f), () =>
                {
                    trackIndex = idx;
                    ShowDifficulty();
                }, 40);
                var sel = b.gameObject.AddComponent<TrackHoverInfo>();
                sel.Menu = this;
                sel.Index = idx;
                buttons.Add(b);
                y -= 115f;
            }
            trackInfo = UIFactory.Label(t, "", new Vector2(0f, -150f), new Vector2(1300f, 160f), 28, UIFactory.TextDim, TextAnchor.UpperCenter);
            var back = UIFactory.Button(t, "VOLVER", new Vector2(0f, -380f), new Vector2(400f, 76f), ShowRoot, 34);
            buttons.Add(back);
            UIFactory.LinkVertical(buttons);
            firstTrack = buttons[0];
        }

        public void ShowTrackInfo(int index)
        {
            if (trackInfo == null || index < 0 || index >= GameDatabase.Tracks.Count) return;
            var track = GameDatabase.Tracks[index];
            var record = SaveManager.Records.Get(track.id);
            string rec = record != null && record.bestLap > 0f
                ? "Mejor vuelta: " + MathUtil.FormatTime(record.bestLap) + "   ·   Mejor carrera: " + MathUtil.FormatTime(record.bestRace) + "   ·   Mejor puesto: " + MathUtil.Ordinal(record.bestPosition)
                : "Sin récords todavía.";
            trackInfo.text = track.description + "\n" + track.laps + " vueltas · ~" + Mathf.RoundToInt(track.expectedLapSeconds) + " s por vuelta\n" + rec;
        }

        private void BuildDifficultyPanel()
        {
            difficultyPanel = NewPanel("Difficulty");
            var t = difficultyPanel.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(1200f, 800f), UIFactory.Bg);
            UIFactory.Label(t, "DIFICULTAD", new Vector2(0f, 330f), new Vector2(1100f, 80f), 56, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var buttons = new List<Selectable>();
            float y = 200f;
            for (int i = 0; i < GameDatabase.Difficulties.Count; i++)
            {
                int idx = i;
                var d = GameDatabase.Difficulties[i];
                var b = UIFactory.Button(t, d.displayName.ToUpperInvariant(), new Vector2(0f, y), new Vector2(700f, 96f), () =>
                {
                    difficultyIndex = idx;
                    ShowKarts();
                }, 44);
                UIFactory.Label(t, d.description, new Vector2(0f, y - 62f), new Vector2(1000f, 40f), 24, UIFactory.TextDim);
                buttons.Add(b);
                y -= 150f;
            }
            var back = UIFactory.Button(t, "VOLVER", new Vector2(0f, -330f), new Vector2(400f, 76f), ShowTracks, 34);
            buttons.Add(back);
            UIFactory.LinkVertical(buttons);
            firstDifficulty = buttons[0];
        }

        private void BuildKartPanel()
        {
            kartPanel = NewPanel("Karts");
            var t = kartPanel.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(1500f, 900f), UIFactory.Bg);
            UIFactory.Label(t, "KART Y PILOTO", new Vector2(0f, 390f), new Vector2(1400f, 80f), 56, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

            var kartNames = new List<string>();
            foreach (var k in GameDatabase.Karts) kartNames.Add(k.displayName.ToUpperInvariant());
            var kartSel = UIFactory.Selector(t, new Vector2(0f, 280f), new Vector2(800f, 80f), kartNames.ToArray(), kartIndex, i =>
            {
                kartIndex = i;
                RefreshKartTexts();
                BuildShowcaseKart();
            });
            kartDesc = UIFactory.Label(t, "", new Vector2(0f, 210f), new Vector2(1300f, 50f), 26, UIFactory.TextDim);
            kartStats = UIFactory.Label(t, "", new Vector2(0f, 140f), new Vector2(1300f, 70f), 28, UIFactory.TextColor);

            var pilotNames = new List<string>();
            foreach (var p in GameDatabase.Pilots) pilotNames.Add(p.displayName.ToUpperInvariant());
            var pilotSel = UIFactory.Selector(t, new Vector2(0f, 20f), new Vector2(800f, 80f), pilotNames.ToArray(), pilotIndex, i =>
            {
                pilotIndex = i;
                RefreshKartTexts();
                BuildShowcaseKart();
            });
            pilotBio = UIFactory.Label(t, "", new Vector2(0f, -50f), new Vector2(1300f, 50f), 26, UIFactory.TextDim);

            var start = UIFactory.Button(t, "INICIAR CARRERA", new Vector2(0f, -200f), new Vector2(760f, 110f), StartRace, 50);
            var back = UIFactory.Button(t, "VOLVER", new Vector2(0f, -370f), new Vector2(400f, 76f), ShowDifficulty, 34);
            UIFactory.LinkVertical(new List<Selectable> { kartSel, pilotSel, start, back });
            firstKart = kartSel;
            RefreshKartTexts();
        }

        private void RefreshKartTexts()
        {
            var k = GameDatabase.Karts[kartIndex];
            var p = GameDatabase.Pilots[pilotIndex];
            if (kartDesc != null) kartDesc.text = k.description;
            if (kartStats != null)
            {
                kartStats.text = "Velocidad " + Bar(k.maxSpeed / 36f) + "   Aceleración " + Bar(k.acceleration / 16f) + "\nManejo " + Bar(k.handling) +
                                 "   Derrape " + Bar(k.driftControl) + "   Peso " + Bar(k.weight / 260f);
            }
            if (pilotBio != null) pilotBio.text = p.bio;
        }

        private static string Bar(float v)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(v * 5f), 1, 5);
            return new string('■', n) + new string('□', 5 - n);
        }

        // ------------------------------------------------------------------ Flow

        private void HideAll()
        {
            rootPanel.SetActive(false);
            trackPanel.SetActive(false);
            difficultyPanel.SetActive(false);
            kartPanel.SetActive(false);
            settings.Hide();
        }

        private void ShowRoot()
        {
            HideAll();
            rootPanel.SetActive(true);
            UIFactory.SetCancelHandler(null);
            UIFactory.Select(firstRoot);
        }

        private void ShowTracks()
        {
            HideAll();
            trackPanel.SetActive(true);
            ShowTrackInfo(trackIndex);
            UIFactory.SetCancelHandler(ShowRoot);
            UIFactory.Select(firstTrack);
        }

        private void ShowDifficulty()
        {
            HideAll();
            difficultyPanel.SetActive(true);
            UIFactory.SetCancelHandler(ShowTracks);
            UIFactory.Select(firstDifficulty);
        }

        private void ShowKarts()
        {
            HideAll();
            kartPanel.SetActive(true);
            UIFactory.SetCancelHandler(ShowDifficulty);
            UIFactory.Select(firstKart);
        }

        private void ShowSettings()
        {
            HideAll();
            settings.Show();
        }

        private void StartRace()
        {
            var gm = GameManager.Instance;
            gm.Setup.TrackId = GameDatabase.Tracks[trackIndex].id;
            gm.Setup.DifficultyIndex = difficultyIndex;
            gm.Settings.difficultyIndex = difficultyIndex;
            gm.Setup.KartId = GameDatabase.Karts[kartIndex].id;
            gm.Setup.PilotId = GameDatabase.Pilots[pilotIndex].id;
            gm.Setup.TotalRacers = 8;
            SaveManager.SaveSettings();
            UIFactory.SetCancelHandler(null);
            gm.StartRace();
        }

        private void OnDestroy()
        {
            UIFactory.SetCancelHandler(null);
        }
    }

    /// <summary>Updates the track description when a track button gets focus.</summary>
    public class TrackHoverInfo : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IPointerEnterHandler
    {
        public MainMenuController Menu;
        public int Index;

        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            if (Menu != null) Menu.ShowTrackInfo(Index);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (Menu != null) Menu.ShowTrackInfo(Index);
        }
    }
}
