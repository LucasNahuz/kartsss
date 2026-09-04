using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.PowerUps;
using VortexKarts.Race;
using VortexKarts.Utils;

namespace VortexKarts.UI
{
    /// <summary>
    /// Minimal VR HUD: a dashboard panel on the kart (position, lap, speed, power-up, drift charge) and a
    /// floating notice ahead of the driver for countdown, laps, wrong way and messages. A rear-view mirror
    /// appears above the dashboard while "look back" is held.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private RaceManager race;
        private KartController kart;
        private RaceProgressTracker tracker;
        private PowerUpInventory inventory;

        private Text posText, posTotalText, lapText, speedText, itemText, itemCharges, timeText;
        private Image itemIcon, driftFill, boostFill;
        private Text noticeText, subNoticeText;
        private Image noticeBg;
        private float noticeUntil;
        private bool wrongWay;
        private Camera rearCamera;
        private RenderTexture rearTexture;
        private GameObject mirror;

        public static RaceHUD Create(RaceManager raceManager)
        {
            var go = new GameObject("RaceHUD");
            var hud = go.AddComponent<RaceHUD>();
            hud.race = raceManager;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            kart = race.PlayerKart;
            if (kart == null) return;
            tracker = kart.GetComponent<RaceProgressTracker>();
            inventory = kart.GetComponent<PowerUpInventory>();

            BuildDashboard();
            BuildNotice();
            BuildMirror();

            GameEvents.OnCountdownTick += OnCountdown;
            GameEvents.OnLapCompleted += OnLap;
            GameEvents.OnKartWrongWay += OnWrongWay;
            GameEvents.OnPositionChanged += OnPosition;
            GameEvents.OnKartFinished += OnFinished;
            GameEvents.OnPowerUpCollected += OnPickup;
            race.OnStartResult += OnStartResult;
            race.OnRaceMessage += ShowMessage;
            if (inventory != null) inventory.OnChanged += OnItemChanged;
            OnItemChanged(inventory != null ? inventory.Current : null, inventory != null ? inventory.ChargesLeft : 0);
        }

        private void OnDestroy()
        {
            GameEvents.OnCountdownTick -= OnCountdown;
            GameEvents.OnLapCompleted -= OnLap;
            GameEvents.OnKartWrongWay -= OnWrongWay;
            GameEvents.OnPositionChanged -= OnPosition;
            GameEvents.OnKartFinished -= OnFinished;
            GameEvents.OnPowerUpCollected -= OnPickup;
            if (race != null)
            {
                race.OnStartResult -= OnStartResult;
                race.OnRaceMessage -= ShowMessage;
            }
            if (inventory != null) inventory.OnChanged -= OnItemChanged;
            if (rearTexture != null) rearTexture.Release();
        }

        private void BuildDashboard()
        {
            var canvas = UIFactory.CreateWorldCanvas("DashboardCanvas", kart.DashboardAnchor, Vector3.zero, Quaternion.identity, new Vector2(900f, 400f), 0.00062f);
            var t = canvas.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(900f, 400f), new Color(0.03f, 0.04f, 0.08f, 0.85f));
            UIFactory.Panel(t, new Vector2(0f, 195f), new Vector2(900f, 8f), UIFactory.Accent);

            // Position (left).
            UIFactory.Label(t, "POS", new Vector2(-300f, 140f), new Vector2(200f, 40f), 30, UIFactory.TextDim);
            posText = UIFactory.Label(t, "8", new Vector2(-320f, 40f), new Vector2(200f, 150f), 140, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            posTotalText = UIFactory.Label(t, "/8", new Vector2(-200f, 10f), new Vector2(120f, 60f), 44, UIFactory.TextDim, TextAnchor.MiddleLeft);

            // Lap (right).
            UIFactory.Label(t, "VUELTA", new Vector2(300f, 140f), new Vector2(200f, 40f), 30, UIFactory.TextDim);
            lapText = UIFactory.Label(t, "1/3", new Vector2(300f, 55f), new Vector2(260f, 120f), 96, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            timeText = UIFactory.Label(t, "00:00.000", new Vector2(300f, -30f), new Vector2(260f, 50f), 34, UIFactory.TextDim);

            // Speed (centre top).
            speedText = UIFactory.Label(t, "0", new Vector2(0f, 95f), new Vector2(300f, 120f), 100, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Label(t, "km/h", new Vector2(0f, 20f), new Vector2(200f, 40f), 28, UIFactory.TextDim);

            // Power-up (centre bottom).
            var slot = UIFactory.Panel(t, new Vector2(0f, -100f), new Vector2(420f, 130f), new Color(0.1f, 0.12f, 0.2f, 1f), "ItemSlot");
            itemIcon = UIFactory.Panel(slot, new Vector2(-150f, 0f), new Vector2(90f, 90f), Color.clear, "ItemIcon").GetComponent<Image>();
            itemText = UIFactory.Label(slot, "—", new Vector2(40f, 12f), new Vector2(280f, 60f), 40, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            itemCharges = UIFactory.Label(slot, "", new Vector2(40f, -38f), new Vector2(280f, 40f), 26, UIFactory.TextDim);

            // Drift charge bar (bottom left) and boost bar (bottom right).
            UIFactory.Panel(t, new Vector2(-300f, -120f), new Vector2(240f, 22f), new Color(0.1f, 0.12f, 0.2f, 1f));
            var driftRt = UIFactory.Panel(t, new Vector2(-300f, -120f), new Vector2(240f, 22f), UIFactory.Accent, "DriftFill");
            driftFill = driftRt.GetComponent<Image>();
            driftFill.type = Image.Type.Filled;
            driftFill.fillMethod = Image.FillMethod.Horizontal;
            driftFill.fillAmount = 0f;
            UIFactory.Label(t, "DERRAPE", new Vector2(-300f, -155f), new Vector2(240f, 30f), 22, UIFactory.TextDim);

            UIFactory.Panel(t, new Vector2(300f, -120f), new Vector2(240f, 22f), new Color(0.1f, 0.12f, 0.2f, 1f));
            var boostRt = UIFactory.Panel(t, new Vector2(300f, -120f), new Vector2(240f, 22f), new Color(1f, 0.55f, 0.1f), "BoostFill");
            boostFill = boostRt.GetComponent<Image>();
            boostFill.type = Image.Type.Filled;
            boostFill.fillMethod = Image.FillMethod.Horizontal;
            boostFill.fillAmount = 0f;
            UIFactory.Label(t, "TURBO", new Vector2(300f, -155f), new Vector2(240f, 30f), 22, UIFactory.TextDim);
        }

        private void BuildNotice()
        {
            var canvas = UIFactory.CreateWorldCanvas("NoticeCanvas", kart.SeatAnchor, new Vector3(0f, 0.6f, 3.2f), Quaternion.identity, new Vector2(1400f, 360f), 0.0014f);
            var t = canvas.transform;
            noticeBg = UIFactory.Panel(t, Vector2.zero, new Vector2(1400f, 360f), new Color(0.03f, 0.04f, 0.08f, 0.55f), "NoticeBg").GetComponent<Image>();
            noticeText = UIFactory.Label(t, "", new Vector2(0f, 40f), new Vector2(1380f, 220f), 190, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            subNoticeText = UIFactory.Label(t, "", new Vector2(0f, -120f), new Vector2(1380f, 80f), 54, UIFactory.TextColor);
            SetNoticeVisible(false);
        }

        private void BuildMirror()
        {
            rearTexture = new RenderTexture(384, 192, 16, RenderTextureFormat.Default);
            rearTexture.name = "RearMirror";
            var camGo = new GameObject("RearCamera");
            camGo.transform.SetParent(kart.Orientation, false);
            camGo.transform.localPosition = new Vector3(0f, 1.0f, -0.4f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            rearCamera = camGo.AddComponent<Camera>();
            rearCamera.fieldOfView = 58f;
            rearCamera.nearClipPlane = 0.5f;
            rearCamera.farClipPlane = 400f;
            rearCamera.targetTexture = rearTexture;
            // Not stereo: the URP camera data below disables XR rendering for this camera.
            rearCamera.clearFlags = CameraClearFlags.SolidColor;
            rearCamera.backgroundColor = RenderSettings.fogColor;
            rearCamera.allowMSAA = false;
            rearCamera.allowHDR = false;
            var data = rearCamera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.allowXRRendering = false;
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
            }
            rearCamera.enabled = false;

            var mat = new Material(MaterialLibrary.Unlit(Color.white));
            mat.name = "MirrorMat";
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", rearTexture);
            else mat.mainTexture = rearTexture;
            // Mirror the image horizontally like a real rear-view mirror.
            mat.mainTextureScale = new Vector2(-1f, 1f);
            mat.mainTextureOffset = new Vector2(1f, 0f);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }
            mirror = PrimitiveFactory.Quad("RearMirror", kart.SeatAnchor, new Vector3(0f, 0.28f, 0.95f), new Vector2(0.5f, 0.25f), mat);
            var frame = PrimitiveFactory.Box("MirrorFrame", mirror.transform, new Vector3(0f, 0f, 0.01f), new Vector3(1.06f, 1.12f, 0.5f),
                MaterialLibrary.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f, 0.4f));
            PrimitiveFactory.SetShadowCasting(mirror, false, false);
            mirror.SetActive(false);
        }

        // ------------------------------------------------------------------ Updates

        private void Update()
        {
            if (kart == null || tracker == null) return;

            posText.text = tracker.Position > 0 ? tracker.Position.ToString() : "-";
            posTotalText.text = "/" + race.Karts.Count;
            int lapShown = Mathf.Clamp(tracker.Lap + 1, 1, race.TotalLaps);
            lapText.text = lapShown + "/" + race.TotalLaps;
            timeText.text = MathUtil.FormatTime(race.State == RaceState.Racing || race.State == RaceState.Finished ? race.RaceTime : 0f);

            // Stylised km/h: a little faster than reality reads better in VR.
            float kmh = Mathf.Abs(kart.ForwardSpeed) * 3.6f * 1.45f;
            speedText.text = Mathf.RoundToInt(kmh).ToString();
            speedText.color = kart.Boost.IsBoosting ? new Color(1f, 0.7f, 0.2f) : UIFactory.TextColor;

            if (kart.Drift.IsDrifting)
            {
                int level = kart.Drift.Level;
                float fill = (level + kart.Drift.LevelProgress) / DriftController.MaxLevel;
                driftFill.fillAmount = Mathf.Clamp01(fill);
                driftFill.color = level >= 3 ? UIFactory.Accent2 : level >= 2 ? new Color(1f, 0.6f, 0.15f) : UIFactory.Accent;
            }
            else driftFill.fillAmount = Mathf.MoveTowards(driftFill.fillAmount, 0f, Time.deltaTime * 2f);
            boostFill.fillAmount = kart.Boost.Intensity;

            // Notice fade.
            if (Time.unscaledTime > noticeUntil && !wrongWay && race.State != RaceState.Countdown) SetNoticeVisible(false);
            if (wrongWay && noticeText != null)
            {
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                noticeText.color = blink ? UIFactory.Warn : new Color(1f, 0.6f, 0.5f);
            }

            // Rear mirror.
            bool look = kart.Input.LookBack && race.State != RaceState.Finished;
            if (mirror != null && mirror.activeSelf != look)
            {
                mirror.SetActive(look);
                rearCamera.enabled = look;
            }
        }

        private void SetNoticeVisible(bool visible)
        {
            if (noticeText == null) return;
            noticeText.gameObject.SetActive(visible);
            subNoticeText.gameObject.SetActive(visible);
            noticeBg.gameObject.SetActive(visible);
        }

        private void Notice(string main, string sub, Color color, float seconds)
        {
            if (noticeText == null) return;
            noticeText.text = main;
            noticeText.color = color;
            subNoticeText.text = sub;
            noticeUntil = Time.unscaledTime + seconds;
            SetNoticeVisible(true);
        }

        private void OnCountdown(int value)
        {
            if (value > 0) Notice(value.ToString(), "", UIFactory.TextColor, 1.2f);
            else Notice("¡GO!", "", UIFactory.Good, 1.0f);
        }

        private void OnStartResult(int result)
        {
            if (result == 1) Notice("¡SALIDA PERFECTA!", "", UIFactory.Good, 1.4f);
            else if (result == -1) Notice("SALIDA ANTICIPADA", "penalización", UIFactory.Warn, 1.4f);
        }

        private void OnLap(KartController k, int lap, float lapTime)
        {
            if (k != kart) return;
            if (lap >= race.TotalLaps) return;
            string main = lap == race.TotalLaps - 1 ? "¡ÚLTIMA VUELTA!" : "VUELTA " + (lap + 1);
            Notice(main, "vuelta " + MathUtil.FormatTime(lapTime), UIFactory.Accent, 2f);
        }

        private void OnWrongWay(KartController k, bool wrong)
        {
            if (k != kart) return;
            wrongWay = wrong;
            if (wrong) Notice("DIRECCIÓN INCORRECTA", "dá la vuelta", UIFactory.Warn, 999f);
            else
            {
                noticeUntil = 0f;
                SetNoticeVisible(false);
            }
        }

        private void OnPosition(KartController k, int pos)
        {
            if (k != kart || race.State != RaceState.Racing) return;
            if (Time.unscaledTime < noticeUntil && !wrongWay) return;
            Notice(MathUtil.Ordinal(pos), "", pos <= 3 ? UIFactory.Good : UIFactory.TextColor, 0.9f);
        }

        private void OnFinished(KartController k)
        {
            if (k != kart) return;
            wrongWay = false;
            Notice("¡META!", MathUtil.Ordinal(tracker.Position) + " puesto", UIFactory.Accent2, 4f);
        }

        private void OnPickup(KartController k, PowerUpData d)
        {
            // Item text handled by inventory event; nothing else here.
        }

        private void OnItemChanged(PowerUpData data, int charges)
        {
            if (itemText == null) return;
            if (data == null)
            {
                itemText.text = "—";
                itemCharges.text = "";
                itemIcon.color = Color.clear;
                return;
            }
            itemText.text = data.shortLabel;
            itemCharges.text = charges > 1 ? "x" + charges : (InputManager.Instance != null ? InputManager.Instance.GetBindingDisplay(InputManager.ActionUsePowerUp, 0) : "");
            itemIcon.color = data.iconColor;
        }

        public void ShowMessage(string msg)
        {
            Notice(msg, "", UIFactory.Warn, 2.5f);
        }
    }
}
