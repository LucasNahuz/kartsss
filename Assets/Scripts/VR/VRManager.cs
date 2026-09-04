using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using VortexKarts.Core;
using VortexKarts.Kart;

namespace VortexKarts.VR
{
    /// <summary>
    /// Owns the persistent camera rig. Head pose comes exclusively from the headset (TrackedPoseDriver);
    /// the rig is parented to whatever seat the player occupies (kart cockpit or menu chair) and the
    /// comfort manager decides how much of the seat's motion reaches the head.
    /// Works without a headset too (flat mode): the camera simply sits at the seat anchor.
    /// </summary>
    public class VRManager : MonoBehaviour
    {
        public static VRManager Instance { get; private set; }

        public Transform Rig { get; private set; }
        public Transform CameraOffset { get; private set; }
        public Transform Head { get; private set; }
        public Camera MainCamera { get; private set; }
        public VRFader Fader { get; private set; }
        public VRComfortManager Comfort { get; private set; }
        public VRWheelInput Wheel { get; private set; }
        public bool IsVRActive => XRSettings.isDeviceActive;
        public bool HeadTracked { get; private set; } = true;

        private TrackedPoseDriver poseDriver;
        private InputAction headPosAction, headRotAction;
        private float trackingCheckTimer;
        private bool originModeSet;

        public static VRManager EnsureExists(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("VRManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<VRManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildRig();
            if (InputManager.Instance != null) InputManager.Instance.OnRecenterPressed += Recenter;
            StartCoroutine(InitialFadeIn());
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null) InputManager.Instance.OnRecenterPressed -= Recenter;
            if (headPosAction != null) headPosAction.Disable();
            if (headRotAction != null) headRotAction.Disable();
            if (Instance == this) Instance = null;
        }

        private IEnumerator InitialFadeIn()
        {
            yield return null;
            yield return null;
            if (Fader != null && GameManager.Instance != null && !GameManager.Instance.Loader.IsLoading)
            {
                yield return Fader.FadeTo(0f, 0.6f);
            }
        }

        private void BuildRig()
        {
            // Remove any stray camera left in a scene (e.g. default Main Camera in an editor test scene).
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.gameObject != gameObject) cam.gameObject.SetActive(false);
            }

            Rig = new GameObject("XRRig").transform;
            Rig.SetParent(transform, false);
            CameraOffset = new GameObject("CameraOffset").transform;
            CameraOffset.SetParent(Rig, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(CameraOffset, false);
            Head = camGo.transform;
            MainCamera = camGo.AddComponent<Camera>();
            MainCamera.nearClipPlane = 0.05f;
            MainCamera.farClipPlane = 1600f;
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            MainCamera.fieldOfView = 70f;
            MainCamera.allowHDR = false;
            MainCamera.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            var urpData = MainCamera.GetUniversalAdditionalCameraData();
            if (urpData != null)
            {
                urpData.renderPostProcessing = false;
                urpData.antialiasing = AntialiasingMode.None;
                urpData.allowXRRendering = true;
            }

            // Head tracking through the Input System: position/rotation from the HMD only.
            headPosAction = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition");
            headRotAction = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            headPosAction.Enable();
            headRotAction.Enable();
            poseDriver = camGo.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(headPosAction);
            poseDriver.rotationInput = new InputActionProperty(headRotAction);
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            Fader = VRFader.Create(Head);
            Comfort = Rig.gameObject.AddComponent<VRComfortManager>();
            Comfort.Initialize(this);
            Wheel = Rig.gameObject.AddComponent<VRWheelInput>();
            Wheel.Initialize(this);
        }

        private void Update()
        {
            if (!originModeSet && IsVRActive)
            {
                TrySetTrackingOrigin();
            }
            trackingCheckTimer -= Time.unscaledDeltaTime;
            if (trackingCheckTimer <= 0f)
            {
                trackingCheckTimer = 0.25f;
                CheckTracking();
            }
        }

        private void TrySetTrackingOrigin()
        {
            try
            {
                var subsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                for (int i = 0; i < subsystems.Count; i++)
                {
                    if (!subsystems[i].running) continue;
                    // Device (head-relative) origin is ideal for a seated experience; fall back to floor.
                    if (!subsystems[i].TrySetTrackingOriginMode(TrackingOriginModeFlags.Device))
                    {
                        subsystems[i].TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                    }
                    subsystems[i].TryRecenter();
                    originModeSet = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VR] Could not set tracking origin: " + e.Message);
                originModeSet = true;
            }
            if (originModeSet) StartCoroutine(RecenterNextFrame());
        }

        private void CheckTracking()
        {
            bool tracked = true;
            if (IsVRActive)
            {
                try
                {
                    var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                    bool value;
                    if (head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out value)) tracked = value;
                }
                catch (System.Exception)
                {
                    tracked = true;
                }
            }
            if (tracked != HeadTracked)
            {
                HeadTracked = tracked;
                GameEvents.RaiseHeadTrackingChanged(tracked);
            }
        }

        // ------------------------------------------------------------------ Seats

        /// <summary>Puts the player into a kart cockpit.</summary>
        public void AttachToSeat(Transform seatAnchor, KartController kart)
        {
            Comfort.SetSeat(seatAnchor, kart);
            Wheel.SetKart(kart);
            StartCoroutine(RecenterNextFrame());
        }

        /// <summary>Puts the player on a static anchor (menu, loading, results).</summary>
        public void AttachToStaticAnchor(Transform anchor)
        {
            Comfort.SetSeat(anchor, null);
            Wheel.SetKart(null);
            StartCoroutine(RecenterNextFrame());
        }

        public void Detach()
        {
            Comfort.SetSeat(null, null);
            Wheel.SetKart(null);
        }

        private IEnumerator RecenterNextFrame()
        {
            yield return null;
            Recenter();
        }

        /// <summary>
        /// Moves the camera offset so the current head pose lands exactly on the seat anchor, facing forward.
        /// Only yaw is corrected; pitch/roll always come from the headset.
        /// </summary>
        public void Recenter()
        {
            if (Rig == null || CameraOffset == null || Head == null) return;
            var s = SaveManager.Settings;
            Vector3 seatOffset = new Vector3(0f, s.seatHeightOffset, s.seatForwardOffset);

            if (!IsVRActive)
            {
                CameraOffset.localRotation = Quaternion.identity;
                CameraOffset.localPosition = seatOffset;
                Head.localPosition = Vector3.zero;
                Head.localRotation = Quaternion.identity;
                return;
            }

            // Head pose relative to the offset (tracking space).
            Vector3 headLocalPos = Head.localPosition;
            float headYaw = Head.localRotation.eulerAngles.y;
            Quaternion yawFix = Quaternion.Euler(0f, -headYaw, 0f);
            CameraOffset.localRotation = yawFix;
            CameraOffset.localPosition = -(yawFix * headLocalPos) + seatOffset;
        }

        public void SetBackgroundColor(Color c)
        {
            if (MainCamera != null) MainCamera.backgroundColor = c;
        }

        public void ApplySettings(GameSettings s)
        {
            if (Comfort != null) Comfort.ApplySettings(s);
            if (Wheel != null) Wheel.ApplySettings(s);
            GraphicsSettingsApplier.ApplyToCamera(MainCamera, s);
            // Seat offsets changed: re-apply without changing yaw.
            if (CameraOffset != null && !IsVRActive)
            {
                CameraOffset.localPosition = new Vector3(0f, s.seatHeightOffset, s.seatForwardOffset);
            }
        }
    }
}
