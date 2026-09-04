using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using VortexKarts.Utils;

namespace VortexKarts.Core
{
    /// <summary>Per-frame driving input consumed by KartController (player or AI).</summary>
    public struct KartInputState
    {
        public float Steer;          // -1..1
        public float Throttle;       // 0..1
        public float Brake;          // 0..1
        public bool Drift;           // held
        public bool DriftPressed;    // this frame
        public bool UsePowerUp;      // pressed this frame
        public bool LookBack;        // held

        public static KartInputState Empty => new KartInputState();
    }

    /// <summary>
    /// Builds the Input System action asset in code (no .inputactions file to keep in sync), applies
    /// deadzones / sensitivity curves, drives haptics and handles rebinding + device disconnection.
    /// Persistent singleton owned by GameManager.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        public const string MapKart = "Kart";
        public const string ActionSteer = "Steer";
        public const string ActionThrottle = "Throttle";
        public const string ActionBrake = "Brake";
        public const string ActionDrift = "Drift";
        public const string ActionUsePowerUp = "UsePowerUp";
        public const string ActionLookBack = "LookBack";
        public const string ActionPause = "Pause";
        public const string ActionRecenter = "Recenter";

        public InputActionAsset Asset { get; private set; }
        public InputActionMap KartMap { get; private set; }

        private InputAction steer, throttle, brake, drift, usePowerUp, lookBack, pause, recenter;

        public event Action OnPausePressed;
        public event Action OnRecenterPressed;

        public bool GamepadConnected { get; private set; }
        public bool KartInputEnabled { get; private set; } = true;

        /// <summary>Optional external steering source (VR virtual wheel). Returns null when not holding the wheel.</summary>
        public Func<float?> ExternalSteerProvider;

        private const float Deadzone = 0.12f;

        // Haptics
        private float rumbleLow, rumbleHigh, rumbleEndTime;
        private bool rumbling;
        private InputActionRebindingExtensions.RebindingOperation rebindOp;

        /// <summary>Rebindable gamepad bindings shown in the controls menu.</summary>
        public struct RebindEntry
        {
            public string ActionName;
            public int BindingIndex;
            public string Label;
        }

        public static InputManager EnsureExists(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("InputManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<InputManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildActions();
            InputSystem.onDeviceChange += HandleDeviceChange;
            GamepadConnected = Gamepad.current != null;
        }

        private void OnDestroy()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            if (rebindOp != null)
            {
                rebindOp.Dispose();
                rebindOp = null;
            }
            StopRumble();
            if (Instance == this) Instance = null;
        }

        private void BuildActions()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "VortexKartsInput";
            KartMap = Asset.AddActionMap(MapKart);

            // Binding index 0 of every action is the gamepad binding (used by the rebind UI).
            steer = KartMap.AddAction(ActionSteer, InputActionType.Value, "<Gamepad>/leftStick/x");
            steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            steer.AddBinding("<XRController>{LeftHand}/thumbstick/x");

            throttle = KartMap.AddAction(ActionThrottle, InputActionType.Value, "<Gamepad>/rightTrigger");
            throttle.AddBinding("<Keyboard>/w");
            throttle.AddBinding("<Keyboard>/upArrow");
            throttle.AddBinding("<XRController>{RightHand}/trigger");

            brake = KartMap.AddAction(ActionBrake, InputActionType.Value, "<Gamepad>/leftTrigger");
            brake.AddBinding("<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow");
            brake.AddBinding("<XRController>{LeftHand}/trigger");

            drift = KartMap.AddAction(ActionDrift, InputActionType.Button, "<Gamepad>/buttonSouth");
            drift.AddBinding("<Keyboard>/space");
            drift.AddBinding("<XRController>{RightHand}/secondaryButton");

            usePowerUp = KartMap.AddAction(ActionUsePowerUp, InputActionType.Button, "<Gamepad>/rightShoulder");
            usePowerUp.AddBinding("<Keyboard>/leftShift");
            usePowerUp.AddBinding("<Keyboard>/e");
            usePowerUp.AddBinding("<XRController>{RightHand}/primaryButton");

            lookBack = KartMap.AddAction(ActionLookBack, InputActionType.Button, "<Gamepad>/leftShoulder");
            lookBack.AddBinding("<Gamepad>/buttonEast");
            lookBack.AddBinding("<Keyboard>/q");
            lookBack.AddBinding("<XRController>{LeftHand}/primaryButton");

            pause = KartMap.AddAction(ActionPause, InputActionType.Button, "<Gamepad>/start");
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<XRController>{LeftHand}/menu");

            recenter = KartMap.AddAction(ActionRecenter, InputActionType.Button, "<Gamepad>/select");
            recenter.AddBinding("<Keyboard>/r");
            recenter.AddBinding("<XRController>{LeftHand}/secondaryButton");

            pause.performed += _ => OnPausePressed?.Invoke();
            recenter.performed += _ => OnRecenterPressed?.Invoke();

            LoadBindingOverrides();
            KartMap.Enable();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;
            switch (change)
            {
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    GamepadConnected = Gamepad.current != null && Gamepad.current != device;
                    if (!GamepadConnected) GameEvents.RaiseGamepadDisconnected();
                    break;
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    if (!GamepadConnected) GameEvents.RaiseGamepadReconnected();
                    GamepadConnected = true;
                    break;
            }
        }

        public void SetKartInputEnabled(bool enabled)
        {
            KartInputEnabled = enabled;
        }

        // ------------------------------------------------------------------ Reading

        /// <summary>Reads and processes the player's driving input for this frame.</summary>
        public KartInputState ReadKartInput()
        {
            var state = KartInputState.Empty;
            if (!KartInputEnabled || KartMap == null) return state;

            var settings = SaveManager.Settings;

            float rawSteer = steer.ReadValue<float>();
            float? external = ExternalSteerProvider != null ? ExternalSteerProvider() : null;
            if (external.HasValue) rawSteer = external.Value;

            float dz = MathUtil.ApplyDeadzone(rawSteer, Deadzone);
            // Sensitivity 0.5 -> soft (exponent 1.7); 1.0 -> 1.25; 1.5 -> 0.85 (twitchy).
            float exponent = Mathf.Lerp(1.7f, 0.85f, Mathf.InverseLerp(0.5f, 1.5f, settings.steeringSensitivity));
            float curved = MathUtil.ApplyCurve(dz, exponent);
            float scale = Mathf.Lerp(0.85f, 1.15f, Mathf.InverseLerp(0.5f, 1.5f, settings.steeringSensitivity));
            state.Steer = Mathf.Clamp(curved * scale, -1f, 1f);

            state.Throttle = Mathf.Clamp01(MathUtil.ApplyDeadzone(throttle.ReadValue<float>(), 0.05f));
            state.Brake = Mathf.Clamp01(MathUtil.ApplyDeadzone(brake.ReadValue<float>(), 0.05f));
            if (settings.autoAccelerate)
            {
                state.Throttle = Mathf.Max(state.Throttle, 1f - state.Brake);
            }

            state.Drift = drift.IsPressed();
            state.DriftPressed = drift.WasPressedThisFrame();
            state.UsePowerUp = usePowerUp.WasPressedThisFrame();
            state.LookBack = lookBack.IsPressed();
            return state;
        }

        public bool AnyKartButtonPressedThisFrame()
        {
            return drift.WasPressedThisFrame() || usePowerUp.WasPressedThisFrame() ||
                   throttle.WasPressedThisFrame() || pause.WasPressedThisFrame();
        }

        public bool ThrottleHeld => throttle != null && throttle.ReadValue<float>() > 0.3f;

        // ------------------------------------------------------------------ Haptics

        /// <summary>Plays rumble on gamepad and XR controllers, scaled by the vibration settings.</summary>
        public void Rumble(float low, float high, float duration)
        {
            var settings = SaveManager.Settings;
            if (!settings.vibration || settings.vibrationIntensity <= 0.001f) return;
            float intensity = settings.vibrationIntensity;
            low = Mathf.Clamp01(low * intensity);
            high = Mathf.Clamp01(high * intensity);
            if (low <= 0.001f && high <= 0.001f) return;

            // Keep the stronger of overlapping requests.
            float end = Time.unscaledTime + Mathf.Max(0.02f, duration);
            if (!rumbling || low > rumbleLow || high > rumbleHigh || end > rumbleEndTime)
            {
                rumbleLow = rumbling ? Mathf.Max(rumbleLow, low) : low;
                rumbleHigh = rumbling ? Mathf.Max(rumbleHigh, high) : high;
                rumbleEndTime = Mathf.Max(rumbleEndTime, end);
            }
            rumbling = true;
            ApplyRumble();

            float amplitude = Mathf.Clamp01(Mathf.Max(low, high));
            SendXrHaptic(XRNode.LeftHand, amplitude * 0.8f, duration);
            SendXrHaptic(XRNode.RightHand, amplitude, duration);
        }

        private static void SendXrHaptic(XRNode node, float amplitude, float duration)
        {
            try
            {
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid) return;
                HapticCapabilities caps;
                if (device.TryGetHapticCapabilities(out caps) && caps.supportsImpulse)
                {
                    device.SendHapticImpulse(0, amplitude, Mathf.Clamp(duration, 0.01f, 1.5f));
                }
            }
            catch (Exception)
            {
                // XR subsystem not running; ignore.
            }
        }

        private void ApplyRumble()
        {
            var pad = Gamepad.current;
            if (pad != null) pad.SetMotorSpeeds(rumbleLow, rumbleHigh);
        }

        public void StopRumble()
        {
            rumbling = false;
            rumbleLow = rumbleHigh = 0f;
            var pad = Gamepad.current;
            if (pad != null) pad.SetMotorSpeeds(0f, 0f);
        }

        private void Update()
        {
            if (rumbling && Time.unscaledTime >= rumbleEndTime)
            {
                StopRumble();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StopRumble();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) StopRumble();
        }

        // ------------------------------------------------------------------ Rebinding

        public List<RebindEntry> GetRebindableEntries()
        {
            return new List<RebindEntry>
            {
                new RebindEntry { ActionName = ActionThrottle, BindingIndex = 0, Label = "Acelerar" },
                new RebindEntry { ActionName = ActionBrake, BindingIndex = 0, Label = "Frenar / Reversa" },
                new RebindEntry { ActionName = ActionDrift, BindingIndex = 0, Label = "Derrape / Salto" },
                new RebindEntry { ActionName = ActionUsePowerUp, BindingIndex = 0, Label = "Usar power-up" },
                new RebindEntry { ActionName = ActionLookBack, BindingIndex = 0, Label = "Mirar atrás" },
                new RebindEntry { ActionName = ActionPause, BindingIndex = 0, Label = "Pausa" },
                new RebindEntry { ActionName = ActionRecenter, BindingIndex = 0, Label = "Recentrar VR" }
            };
        }

        public string GetBindingDisplay(string actionName, int bindingIndex)
        {
            var action = KartMap.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "-";
            try
            {
                return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
            catch (Exception)
            {
                return action.bindings[bindingIndex].effectivePath;
            }
        }

        public bool IsRebinding => rebindOp != null;

        /// <summary>Waits for the next gamepad control and assigns it. Callback receives success.</summary>
        public void StartRebind(string actionName, int bindingIndex, Action<bool> onDone)
        {
            var action = KartMap.FindAction(actionName);
            if (action == null || rebindOp != null)
            {
                onDone?.Invoke(false);
                return;
            }
            action.Disable();
            rebindOp = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Gamepad>")
                .WithControlsExcluding("<Gamepad>/leftStick")
                .WithControlsExcluding("<Gamepad>/rightStick")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    op.Dispose();
                    rebindOp = null;
                    action.Enable();
                    SaveBindingOverrides();
                    onDone?.Invoke(true);
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    rebindOp = null;
                    action.Enable();
                    onDone?.Invoke(false);
                });
            rebindOp.Start();
        }

        public void CancelRebind()
        {
            if (rebindOp != null) rebindOp.Cancel();
        }

        public void ResetBindings()
        {
            KartMap.RemoveAllBindingOverrides();
            SaveBindingOverrides();
        }

        private void SaveBindingOverrides()
        {
            try
            {
                SaveManager.Settings.bindingOverridesJson = Asset.SaveBindingOverridesAsJson();
                SaveManager.SaveSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[InputManager] Could not save binding overrides: " + e.Message);
            }
        }

        private void LoadBindingOverrides()
        {
            string json = SaveManager.Settings.bindingOverridesJson;
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                Asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[InputManager] Could not load binding overrides: " + e.Message);
            }
        }
    }
}
