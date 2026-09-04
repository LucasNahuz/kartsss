using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VortexKarts.Core;
using VortexKarts.Data;
using VortexKarts.VR;

namespace VortexKarts.UI
{
    /// <summary>
    /// Settings panel shared by the main menu and the pause menu. Every control writes straight into
    /// GameSettings and applies immediately through GameManager.ApplySettings().
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private const float Width = 1400f;
        private const float RowHeight = 82f;

        private Action onBack;
        private OptionSelector categorySelector;
        private readonly List<GameObject> panes = new List<GameObject>();
        private readonly List<List<Selectable>> paneSelectables = new List<List<Selectable>>();
        private readonly List<Action> paneRefreshers = new List<Action>();
        private Button backButton;
        private Button resetButton;
        private bool suppress;

        private GameSettings S => SaveManager.Settings;

        public static SettingsMenu Build(Transform root, Action onBack)
        {
            var go = new GameObject("SettingsMenu");
            go.transform.SetParent(root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1600f, 1000f);
            var menu = go.AddComponent<SettingsMenu>();
            menu.onBack = onBack;
            menu.BuildUI();
            return menu;
        }

        private void Apply()
        {
            if (suppress) return;
            if (GameManager.Instance != null) GameManager.Instance.ApplySettings();
            else SaveManager.SaveSettings();
        }

        private void BuildUI()
        {
            UIFactory.Panel(transform, Vector2.zero, new Vector2(1560f, 960f), UIFactory.Bg);
            UIFactory.Label(transform, "CONFIGURACIÓN", new Vector2(0f, 420f), new Vector2(1200f, 80f), 56, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

            var categories = new[] { "JUEGO", "VR", "AUDIO", "GRÁFICOS", "CONTROLES" };
            categorySelector = UIFactory.Selector(transform, new Vector2(0f, 330f), new Vector2(700f, 64f), categories, 0, ShowPane);

            BuildGameplayPane();
            BuildVRPane();
            BuildAudioPane();
            BuildGraphicsPane();
            BuildControlsPane();

            resetButton = UIFactory.Button(transform, "RESTABLECER", new Vector2(-330f, -420f), new Vector2(420f, 70f), () =>
            {
                SaveManager.ResetSettingsToDefault();
                Apply();
                RefreshAll();
            }, 34);
            backButton = UIFactory.Button(transform, "VOLVER", new Vector2(330f, -420f), new Vector2(420f, 70f), () => onBack?.Invoke(), 34);

            ShowPane(0);
        }

        private Transform NewPane(string name, out List<Selectable> selectables, out Action refresher)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -30f);
            rt.sizeDelta = new Vector2(Width, 640f);
            panes.Add(go);
            selectables = new List<Selectable>();
            paneSelectables.Add(selectables);
            refresher = null;
            paneRefreshers.Add(null);
            return go.transform;
        }

        private void SetRefresher(int paneIndex, Action refresher)
        {
            paneRefreshers[paneIndex] = refresher;
        }

        private float RowY(int row) => 250f - row * RowHeight;

        private void AddLabel(Transform pane, string text, int row)
        {
            Vector2 labelPos, labelSize, controlSize;
            UIFactory.RowPositions(RowY(row), Width, out labelPos, out labelSize, out controlSize);
            UIFactory.Label(pane, text, labelPos, labelSize, 34, UIFactory.TextDim, TextAnchor.MiddleLeft);
        }

        private Slider AddSlider(Transform pane, string label, int row, float min, float max, Func<float> get, Action<float> set, List<Selectable> list)
        {
            AddLabel(pane, label, row);
            Vector2 lp, ls, cs;
            Vector2 pos = UIFactory.RowPositions(RowY(row), Width, out lp, out ls, out cs);
            var slider = UIFactory.Slider(pane, pos, new Vector2(cs.x, 40f), min, max, get(), v => { set(v); Apply(); });
            list.Add(slider);
            return slider;
        }

        private Toggle AddToggle(Transform pane, string label, int row, Func<bool> get, Action<bool> set, List<Selectable> list)
        {
            AddLabel(pane, label, row);
            Vector2 lp, ls, cs;
            Vector2 pos = UIFactory.RowPositions(RowY(row), Width, out lp, out ls, out cs);
            var toggle = UIFactory.Toggle(pane, pos + new Vector2(-cs.x * 0.5f + 32f, 0f), new Vector2(60f, 60f), get(), v => { set(v); Apply(); });
            list.Add(toggle);
            return toggle;
        }

        private OptionSelector AddSelector(Transform pane, string label, int row, string[] options, Func<int> get, Action<int> set, List<Selectable> list)
        {
            AddLabel(pane, label, row);
            Vector2 lp, ls, cs;
            Vector2 pos = UIFactory.RowPositions(RowY(row), Width, out lp, out ls, out cs);
            var sel = UIFactory.Selector(pane, pos, cs, options, get(), i => { set(i); Apply(); });
            list.Add(sel);
            return sel;
        }

        // ------------------------------------------------------------------ Panes

        private void BuildGameplayPane()
        {
            List<Selectable> list;
            Action dummy;
            var pane = NewPane("Gameplay", out list, out dummy);
            var diffNames = new List<string>();
            foreach (var d in GameDatabase.Difficulties) diffNames.Add(d.displayName.ToUpperInvariant());

            var diff = AddSelector(pane, "Dificultad", 0, diffNames.ToArray(), () => S.difficultyIndex, v => S.difficultyIndex = v, list);
            var vib = AddToggle(pane, "Vibración", 1, () => S.vibration, v => S.vibration = v, list);
            var vibInt = AddSlider(pane, "Intensidad de vibración", 2, 0f, 1f, () => S.vibrationIntensity, v => S.vibrationIntensity = v, list);
            var sens = AddSlider(pane, "Sensibilidad de dirección", 3, 0.5f, 1.5f, () => S.steeringSensitivity, v => S.steeringSensitivity = v, list);
            var assists = AddToggle(pane, "Ayudas de conducción", 4, () => S.drivingAssists, v => S.drivingAssists = v, list);
            var auto = AddToggle(pane, "Auto-acelerar", 5, () => S.autoAccelerate, v => S.autoAccelerate = v, list);
            var mode = AddSelector(pane, "Modo de control", 6, new[] { "GAMEPAD", "CONTROLES VR (VOLANTE)" }, () => S.inputMode, v => S.inputMode = v, list);
            UIFactory.LinkVertical(list);
            SetRefresher(0, () =>
            {
                diff.SetIndex(S.difficultyIndex, false);
                vib.SetIsOnWithoutNotify(S.vibration);
                vibInt.SetValueWithoutNotify(S.vibrationIntensity);
                sens.SetValueWithoutNotify(S.steeringSensitivity);
                assists.SetIsOnWithoutNotify(S.drivingAssists);
                auto.SetIsOnWithoutNotify(S.autoAccelerate);
                mode.SetIndex(S.inputMode, false);
            });
        }

        private void BuildVRPane()
        {
            List<Selectable> list;
            Action dummy;
            var pane = NewPane("VR", out list, out dummy);

            AddLabel(pane, "Recentrar visor", 0);
            Vector2 lp, ls, cs;
            Vector2 pos = UIFactory.RowPositions(RowY(0), Width, out lp, out ls, out cs);
            var recenter = UIFactory.Button(pane, "RECENTRAR", pos, new Vector2(cs.x, 60f), () => { if (VRManager.Instance != null) VRManager.Instance.Recenter(); }, 30);
            list.Add(recenter);

            var height = AddSlider(pane, "Altura del asiento", 1, -0.3f, 0.3f, () => S.seatHeightOffset, v => { S.seatHeightOffset = v; }, list);
            var fwd = AddSlider(pane, "Asiento adelante / atrás", 2, -0.3f, 0.3f, () => S.seatForwardOffset, v => { S.seatForwardOffset = v; }, list);
            var horizon = AddToggle(pane, "Horizonte estabilizado", 3, () => S.horizonStabilization, v => S.horizonStabilization = v, list);
            var vig = AddToggle(pane, "Vignette de confort", 4, () => S.comfortVignette, v => S.comfortVignette = v, list);
            var vigInt = AddSlider(pane, "Intensidad del vignette", 5, 0f, 1f, () => S.vignetteIntensity, v => S.vignetteIntensity = v, list);
            var shake = AddSelector(pane, "Camera shake", 6, new[] { "OFF", "LOW", "MEDIUM" }, () => S.cameraShake, v => S.cameraShake = v, list);
            var tilt = AddSlider(pane, "Inclinación del kart en cámara", 7, 0f, 1f, () => S.kartTiltIntensity, v => S.kartTiltIntensity = v, list);
            UIFactory.LinkVertical(list);
            SetRefresher(1, () =>
            {
                height.SetValueWithoutNotify(S.seatHeightOffset);
                fwd.SetValueWithoutNotify(S.seatForwardOffset);
                horizon.SetIsOnWithoutNotify(S.horizonStabilization);
                vig.SetIsOnWithoutNotify(S.comfortVignette);
                vigInt.SetValueWithoutNotify(S.vignetteIntensity);
                shake.SetIndex(S.cameraShake, false);
                tilt.SetValueWithoutNotify(S.kartTiltIntensity);
            });
        }

        private void BuildAudioPane()
        {
            List<Selectable> list;
            Action dummy;
            var pane = NewPane("Audio", out list, out dummy);
            var master = AddSlider(pane, "Volumen general", 0, 0f, 1f, () => S.masterVolume, v => S.masterVolume = v, list);
            var music = AddSlider(pane, "Música", 1, 0f, 1f, () => S.musicVolume, v => S.musicVolume = v, list);
            var sfx = AddSlider(pane, "Efectos", 2, 0f, 1f, () => S.sfxVolume, v => S.sfxVolume = v, list);
            UIFactory.LinkVertical(list);
            SetRefresher(2, () =>
            {
                master.SetValueWithoutNotify(S.masterVolume);
                music.SetValueWithoutNotify(S.musicVolume);
                sfx.SetValueWithoutNotify(S.sfxVolume);
            });
        }

        private void BuildGraphicsPane()
        {
            List<Selectable> list;
            Action dummy;
            var pane = NewPane("Graphics", out list, out dummy);
            OptionSelector preset = null;
            Slider scale = null;
            OptionSelector shadows = null;
            Toggle effects = null;
            OptionSelector aa = null;

            preset = AddSelector(pane, "Preset de calidad", 0, new[] { "LOW", "MEDIUM", "HIGH" }, () => S.qualityPreset, v =>
            {
                S.ApplyQualityPreset(v);
                suppress = true;
                if (scale != null) scale.SetValueWithoutNotify(S.renderScale);
                if (shadows != null) shadows.SetIndex(S.shadows, false);
                if (effects != null) effects.SetIsOnWithoutNotify(S.effects);
                if (aa != null) aa.SetIndex(S.antiAliasing, false);
                suppress = false;
            }, list);
            scale = AddSlider(pane, "Escala de render", 1, 0.5f, 1.5f, () => S.renderScale, v => S.renderScale = v, list);
            shadows = AddSelector(pane, "Sombras", 2, new[] { "OFF", "LOW", "HIGH" }, () => S.shadows, v => S.shadows = v, list);
            effects = AddToggle(pane, "Efectos (partículas)", 3, () => S.effects, v => S.effects = v, list);
            aa = AddSelector(pane, "Anti-aliasing", 4, new[] { "NINGUNO", "MSAA 2x", "MSAA 4x" }, () => S.antiAliasing, v => S.antiAliasing = v, list);
            UIFactory.LinkVertical(list);
            SetRefresher(3, () =>
            {
                preset.SetIndex(S.qualityPreset, false);
                scale.SetValueWithoutNotify(S.renderScale);
                shadows.SetIndex(S.shadows, false);
                effects.SetIsOnWithoutNotify(S.effects);
                aa.SetIndex(S.antiAliasing, false);
            });
        }

        private void BuildControlsPane()
        {
            List<Selectable> list;
            Action dummy;
            var pane = NewPane("Controls", out list, out dummy);
            var im = InputManager.Instance;
            var buttons = new List<Button>();
            var entries = im != null ? im.GetRebindableEntries() : new List<InputManager.RebindEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                AddLabel(pane, e.Label, i);
                Vector2 lp, ls, cs;
                Vector2 pos = UIFactory.RowPositions(RowY(i), Width, out lp, out ls, out cs);
                Button b = null;
                b = UIFactory.Button(pane, im != null ? im.GetBindingDisplay(e.ActionName, e.BindingIndex) : "-", pos, new Vector2(cs.x, 60f), () =>
                {
                    if (im == null || im.IsRebinding) return;
                    UIFactory.ButtonText(b).text = "PRESIONÁ UN BOTÓN...";
                    UIFactory.SetInteractableAll(transform, false);
                    im.StartRebind(e.ActionName, e.BindingIndex, ok =>
                    {
                        UIFactory.SetInteractableAll(transform, true);
                        RefreshBindings(buttons, entries);
                        UIFactory.Select(b);
                    });
                }, 30);
                buttons.Add(b);
                list.Add(b);
            }
            int row = entries.Count;
            AddLabel(pane, "Dirección: stick izquierdo / A-D", row);
            Vector2 lp2, ls2, cs2;
            Vector2 pos2 = UIFactory.RowPositions(RowY(row), Width, out lp2, out ls2, out cs2);
            var reset = UIFactory.Button(pane, "RESTABLECER CONTROLES", pos2, new Vector2(cs2.x, 60f), () =>
            {
                if (im != null) im.ResetBindings();
                RefreshBindings(buttons, entries);
            }, 28);
            list.Add(reset);
            UIFactory.LinkVertical(list);
            SetRefresher(4, () => RefreshBindings(buttons, entries));
        }

        private void RefreshBindings(List<Button> buttons, List<InputManager.RebindEntry> entries)
        {
            var im = InputManager.Instance;
            for (int i = 0; i < buttons.Count && i < entries.Count; i++)
            {
                var t = UIFactory.ButtonText(buttons[i]);
                if (t != null) t.text = im != null ? im.GetBindingDisplay(entries[i].ActionName, entries[i].BindingIndex) : "-";
            }
        }

        // ------------------------------------------------------------------ Show / hide

        private void ShowPane(int index)
        {
            for (int i = 0; i < panes.Count; i++) panes[i].SetActive(i == index);
            if (index >= 0 && index < paneRefreshers.Count && paneRefreshers[index] != null) paneRefreshers[index]();
            // Navigation: selector -> first control ... last control -> reset/back.
            var first = index < paneSelectables.Count && paneSelectables[index].Count > 0 ? paneSelectables[index][0] : null;
            var last = index < paneSelectables.Count && paneSelectables[index].Count > 0 ? paneSelectables[index][paneSelectables[index].Count - 1] : null;
            var nav = categorySelector.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = first != null ? first : (Selectable)backButton;
            nav.selectOnUp = backButton;
            categorySelector.navigation = nav;
            if (first != null)
            {
                var fn = first.navigation; fn.selectOnUp = categorySelector; first.navigation = fn;
            }
            if (last != null)
            {
                var ln = last.navigation; ln.selectOnDown = backButton; last.navigation = ln;
            }
            var bn = backButton.navigation;
            bn.mode = Navigation.Mode.Explicit;
            bn.selectOnUp = last != null ? last : (Selectable)categorySelector;
            bn.selectOnDown = categorySelector;
            bn.selectOnLeft = resetButton;
            backButton.navigation = bn;
            var rn = resetButton.navigation;
            rn.mode = Navigation.Mode.Explicit;
            rn.selectOnUp = last != null ? last : (Selectable)categorySelector;
            rn.selectOnDown = categorySelector;
            rn.selectOnRight = backButton;
            resetButton.navigation = rn;
        }

        public void RefreshAll()
        {
            for (int i = 0; i < paneRefreshers.Count; i++) if (paneRefreshers[i] != null) paneRefreshers[i]();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshAll();
            UIFactory.SetCancelHandler(() => onBack?.Invoke());
            UIFactory.Select(categorySelector);
        }

        public void Hide()
        {
            if (InputManager.Instance != null && InputManager.Instance.IsRebinding) InputManager.Instance.CancelRebind();
            gameObject.SetActive(false);
        }
    }
}
