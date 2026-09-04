using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VortexKarts.Core;

namespace VortexKarts.UI
{
    /// <summary>
    /// Selectable that cycles through options with left/right (gamepad/keyboard) or the arrow buttons (mouse).
    /// </summary>
    public class OptionSelector : Selectable, ISubmitHandler
    {
        public Text ValueText;
        public string[] Options = new string[0];
        public int Index;
        public UnityEvent<int> OnChanged = new UnityEvent<int>();

        public void SetOptions(string[] options, int index)
        {
            Options = options ?? new string[0];
            Index = Options.Length > 0 ? Mathf.Clamp(index, 0, Options.Length - 1) : 0;
            Refresh();
        }

        public void SetIndex(int index, bool notify)
        {
            if (Options.Length == 0) return;
            Index = Mathf.Clamp(index, 0, Options.Length - 1);
            Refresh();
            if (notify) OnChanged.Invoke(Index);
        }

        public void Next()
        {
            if (Options.Length == 0) return;
            Index = (Index + 1) % Options.Length;
            Refresh();
            GameEvents.RaiseUiClick("move");
            OnChanged.Invoke(Index);
        }

        public void Prev()
        {
            if (Options.Length == 0) return;
            Index = (Index - 1 + Options.Length) % Options.Length;
            Refresh();
            GameEvents.RaiseUiClick("move");
            OnChanged.Invoke(Index);
        }

        private void Refresh()
        {
            if (ValueText != null) ValueText.text = Options.Length > 0 ? Options[Index] : "-";
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
            {
                Prev();
                return;
            }
            if (eventData.moveDir == MoveDirection.Right)
            {
                Next();
                return;
            }
            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Next();
        }
    }

    /// <summary>Plays the UI click sound when a button is pressed.</summary>
    public class UIClickSound : MonoBehaviour, ISelectHandler
    {
        public void OnSelect(BaseEventData eventData)
        {
            GameEvents.RaiseUiClick("move");
        }
    }

    /// <summary>
    /// Builds world-space uGUI in code with a consistent look. Every control is navigable with a gamepad
    /// (explicit vertical navigation) and clickable with a mouse in flat mode.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Bg = new Color(0.05f, 0.06f, 0.11f, 0.94f);
        public static readonly Color PanelColor = new Color(0.1f, 0.12f, 0.2f, 0.9f);
        public static readonly Color Accent = new Color(0.1f, 0.9f, 1f, 1f);
        public static readonly Color Accent2 = new Color(1f, 0.25f, 0.85f, 1f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color TextDim = new Color(0.7f, 0.75f, 0.85f, 1f);
        public static readonly Color Warn = new Color(1f, 0.35f, 0.3f, 1f);
        public static readonly Color Good = new Color(0.4f, 1f, 0.5f, 1f);
        public static readonly Color ButtonNormal = new Color(0.14f, 0.17f, 0.28f, 1f);
        public static readonly Color ButtonHighlight = new Color(0.15f, 0.55f, 0.85f, 1f);
        public static readonly Color ButtonPressed = new Color(0.1f, 0.9f, 1f, 1f);

        private static Font font;
        private static Action cancelCallback;
        private static bool cancelHooked;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { font = null; }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { font = null; }
                }
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Helvetica", "Liberation Sans" }, 32);
                return font;
            }
        }

        // ------------------------------------------------------------------ Event system

        public static InputSystemUIInputModule UIModule { get; private set; }

        public static void EnsureEventSystem()
        {
            var es = EventSystem.current;
            if (es == null)
            {
                es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            }
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
                cancelHooked = false; // new module per scene: re-hook the cancel action
                UIModule = go.AddComponent<InputSystemUIInputModule>();
                try { UIModule.AssignDefaultActions(); } catch (Exception e) { Debug.LogWarning("[UI] Default UI actions: " + e.Message); }
                UIModule.moveRepeatDelay = 0.35f;
                UIModule.moveRepeatRate = 0.12f;
            }
            else if (UIModule == null)
            {
                cancelHooked = false;
                UIModule = es.GetComponent<InputSystemUIInputModule>();
                if (UIModule == null)
                {
                    UIModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                    try { UIModule.AssignDefaultActions(); } catch (Exception) { }
                }
            }
            HookCancel();
        }

        private static void HookCancel()
        {
            if (cancelHooked || UIModule == null || UIModule.cancel == null || UIModule.cancel.action == null) return;
            UIModule.cancel.action.performed += _ => cancelCallback?.Invoke();
            cancelHooked = true;
        }

        /// <summary>Sets what the "back" button (B / Escape) does for the currently visible menu.</summary>
        public static void SetCancelHandler(Action handler)
        {
            cancelCallback = handler;
        }

        public static void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        // ------------------------------------------------------------------ Canvas & layout

        public static Canvas CreateWorldCanvas(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Vector2 sizePixels, float unitsPerPixel)
        {
            var go = new GameObject(name);
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = Vector3.one * unitsPerPixel;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            go.AddComponent<GraphicRaycaster>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = sizePixels;
            return canvas;
        }

        private static RectTransform MakeRect(GameObject go, Transform parent, Vector2 pos, Vector2 size)
        {
            go.layer = 5;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Panel(Transform parent, Vector2 pos, Vector2 size, Color color, string name = "Panel")
        {
            var go = new GameObject(name);
            var rt = MakeRect(go, parent, pos, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        public static Text Label(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label");
            MakeRect(go, parent, pos, size);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 40)
        {
            var go = new GameObject("Button_" + label);
            MakeRect(go, parent, pos, size);
            var img = go.AddComponent<Image>();
            img.color = ButtonNormal;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var text = Label(go.transform, label, Vector2.zero, size - new Vector2(20f, 8f), fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (onClick != null) btn.onClick.AddListener(onClick);
            btn.onClick.AddListener(() => GameEvents.RaiseUiClick("click"));
            go.AddComponent<UIClickSound>();
            return btn;
        }

        public static Text ButtonText(Button b)
        {
            return b != null ? b.GetComponentInChildren<Text>() : null;
        }

        public static Slider Slider(Transform parent, Vector2 pos, Vector2 size, float min, float max, float value,
            UnityAction<float> onChanged, bool wholeNumbers = false)
        {
            var go = new GameObject("Slider");
            MakeRect(go, parent, pos, size);
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.14f, 1f);
            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = bgImg;

            var fillArea = new GameObject("FillArea");
            var faRt = MakeRect(fillArea, go.transform, Vector2.zero, size);
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;
            var fill = new GameObject("Fill");
            var fillRt = MakeRect(fill, fillArea.transform, Vector2.zero, Vector2.zero);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = Accent;
            fillImg.raycastTarget = false;
            slider.fillRect = fillRt;

            var handleArea = new GameObject("HandleArea");
            var haRt = MakeRect(handleArea, go.transform, Vector2.zero, size);
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.sizeDelta = new Vector2(-size.y, 0f);
            var handle = new GameObject("Handle");
            var hRt = MakeRect(handle, handleArea.transform, Vector2.zero, new Vector2(size.y * 1.2f, size.y * 1.4f));
            var hImg = handle.AddComponent<Image>();
            hImg.color = Color.white;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;

            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = Accent2;
            slider.colors = colors;

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            go.AddComponent<UIClickSound>();
            return slider;
        }

        public static Toggle Toggle(Transform parent, Vector2 pos, Vector2 size, bool value, UnityAction<bool> onChanged)
        {
            var go = new GameObject("Toggle");
            MakeRect(go, parent, pos, size);
            var bg = go.AddComponent<Image>();
            bg.color = ButtonNormal;
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            var check = new GameObject("Check");
            var cRt = MakeRect(check, go.transform, Vector2.zero, size - new Vector2(16f, 16f));
            var cImg = check.AddComponent<Image>();
            cImg.color = Accent;
            cImg.raycastTarget = false;
            toggle.graphic = cImg;
            var colors = toggle.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            toggle.colors = colors;
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
            toggle.onValueChanged.AddListener(_ => GameEvents.RaiseUiClick("click"));
            go.AddComponent<UIClickSound>();
            return toggle;
        }

        public static OptionSelector Selector(Transform parent, Vector2 pos, Vector2 size, string[] options, int index,
            UnityAction<int> onChanged)
        {
            var go = new GameObject("Selector");
            MakeRect(go, parent, pos, size);
            var bg = go.AddComponent<Image>();
            bg.color = ButtonNormal;
            var sel = go.AddComponent<OptionSelector>();
            sel.targetGraphic = bg;
            var colors = sel.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            sel.colors = colors;

            float arrowW = size.y;
            var left = Button(go.transform, "<", new Vector2(-size.x * 0.5f + arrowW * 0.5f, 0f), new Vector2(arrowW, size.y), sel.Prev, 34);
            var right = Button(go.transform, ">", new Vector2(size.x * 0.5f - arrowW * 0.5f, 0f), new Vector2(arrowW, size.y), sel.Next, 34);
            // Arrows are mouse helpers only; keep gamepad focus on the selector itself.
            var nav = left.navigation; nav.mode = Navigation.Mode.None; left.navigation = nav; right.navigation = nav;
            sel.ValueText = Label(go.transform, "", Vector2.zero, new Vector2(size.x - arrowW * 2f - 10f, size.y), Mathf.RoundToInt(size.y * 0.5f), TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            sel.SetOptions(options, index);
            if (onChanged != null) sel.OnChanged.AddListener(onChanged);
            go.AddComponent<UIClickSound>();
            return sel;
        }

        /// <summary>Label on the left, control on the right. Returns the control's anchored position.</summary>
        public static Vector2 RowPositions(float y, float totalWidth, out Vector2 labelPos, out Vector2 labelSize, out Vector2 controlSize)
        {
            float labelW = totalWidth * 0.45f;
            float controlW = totalWidth * 0.5f;
            labelPos = new Vector2(-totalWidth * 0.5f + labelW * 0.5f, y);
            labelSize = new Vector2(labelW, 70f);
            controlSize = new Vector2(controlW, 60f);
            return new Vector2(totalWidth * 0.5f - controlW * 0.5f, y);
        }

        public static void LinkVertical(IList<Selectable> items, bool wrap = true)
        {
            if (items == null) return;
            int n = items.Count;
            for (int i = 0; i < n; i++)
            {
                if (items[i] == null) continue;
                var nav = items[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? items[i - 1] : (wrap && n > 1 ? items[n - 1] : null);
                nav.selectOnDown = i < n - 1 ? items[i + 1] : (wrap && n > 1 ? items[0] : null);
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                items[i].navigation = nav;
            }
        }

        public static void SetInteractableAll(Transform root, bool interactable)
        {
            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++) selectables[i].interactable = interactable;
        }
    }
}
