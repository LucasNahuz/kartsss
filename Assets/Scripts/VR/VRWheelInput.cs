using UnityEngine;
using UnityEngine.XR;
using VortexKarts.Core;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.VR
{
    /// <summary>
    /// Optional motion-controller steering: grip near the virtual wheel to hold it, rotate the hands around
    /// the wheel axis to steer. Gamepad stays the recommended mode; when no hand is gripping, steering
    /// falls back to the regular actions (thumbstick / gamepad). Also renders simple hand markers.
    /// </summary>
    public class VRWheelInput : MonoBehaviour
    {
        private const float GrabDistance = 0.28f;
        private const float MaxWheelAngle = 95f;
        private const float DeadzoneDegrees = 3f;

        private VRManager vr;
        private KartController kart;
        private bool enabledMode;
        private Transform leftHand, rightHand;
        private Renderer leftRenderer, rightRenderer;
        private Material handIdle, handGrab;
        private bool leftGrabbing, rightGrabbing;
        private float leftGrabAngle, rightGrabAngle;
        private float wheelAngle;
        private float smoothedSteer;

        public bool IsHoldingWheel => leftGrabbing || rightGrabbing;
        public float WheelAngle => wheelAngle;

        public void Initialize(VRManager manager)
        {
            vr = manager;
            handIdle = MaterialLibrary.UnlitTransparent(new Color(0.6f, 0.9f, 1f, 0.55f));
            handGrab = MaterialLibrary.UnlitTransparent(new Color(1f, 0.6f, 0.2f, 0.8f));
            var l = PrimitiveFactory.Sphere("HandL", vr.CameraOffset, Vector3.zero, 0.07f, handIdle);
            var r = PrimitiveFactory.Sphere("HandR", vr.CameraOffset, Vector3.zero, 0.07f, handIdle);
            PrimitiveFactory.SetShadowCasting(l, false, false);
            PrimitiveFactory.SetShadowCasting(r, false, false);
            leftHand = l.transform;
            rightHand = r.transform;
            leftRenderer = l.GetComponent<Renderer>();
            rightRenderer = r.GetComponent<Renderer>();
            ApplySettings(SaveManager.Settings);
            if (InputManager.Instance != null) InputManager.Instance.ExternalSteerProvider = ProvideSteer;
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null && InputManager.Instance.ExternalSteerProvider == ProvideSteer)
            {
                InputManager.Instance.ExternalSteerProvider = null;
            }
        }

        public void ApplySettings(GameSettings s)
        {
            enabledMode = s.inputMode == (int)InputMode.VRMotionControllers;
            bool show = enabledMode && vr != null && vr.IsVRActive;
            if (leftRenderer != null) leftRenderer.enabled = show;
            if (rightRenderer != null) rightRenderer.enabled = show;
            if (!enabledMode)
            {
                leftGrabbing = rightGrabbing = false;
                wheelAngle = 0f;
            }
        }

        public void SetKart(KartController controller)
        {
            kart = controller;
            leftGrabbing = rightGrabbing = false;
            wheelAngle = 0f;
        }

        private float? ProvideSteer()
        {
            if (!enabledMode || kart == null || vr == null || !vr.IsVRActive) return null;
            if (!IsHoldingWheel) return null;
            return smoothedSteer;
        }

        private void Update()
        {
            if (!enabledMode || vr == null || !vr.IsVRActive) return;
            UpdateHand(XRNode.LeftHand, leftHand, leftRenderer, ref leftGrabbing, ref leftGrabAngle);
            UpdateHand(XRNode.RightHand, rightHand, rightRenderer, ref rightGrabbing, ref rightGrabAngle);

            if (kart == null || kart.Visuals == null || kart.Visuals.SteeringWheel == null)
            {
                wheelAngle = 0f;
                smoothedSteer = 0f;
                return;
            }

            // Wheel angle = average delta of gripping hands since they grabbed.
            float sum = 0f;
            int count = 0;
            if (leftGrabbing) { sum += HandAngle(leftHand) - leftGrabAngle; count++; }
            if (rightGrabbing) { sum += HandAngle(rightHand) - rightGrabAngle; count++; }
            if (count > 0)
            {
                float delta = MathUtil.WrapAngle180(sum / count);
                wheelAngle = Mathf.Clamp(wheelAngle * 0.2f + delta * 0.8f, -MaxWheelAngle, MaxWheelAngle);
            }
            else
            {
                wheelAngle = MathUtil.Damp(wheelAngle, 0f, 6f, Time.deltaTime);
            }
            float a = Mathf.Abs(wheelAngle) < DeadzoneDegrees ? 0f : wheelAngle;
            float steer = Mathf.Clamp(a / MaxWheelAngle, -1f, 1f);
            // Ease the response so tiny hand tremor does not shake the kart.
            smoothedSteer = MathUtil.Damp(smoothedSteer, steer, 14f, Time.deltaTime);
        }

        private void UpdateHand(XRNode node, Transform hand, Renderer renderer, ref bool grabbing, ref float grabAngle)
        {
            InputDevice device;
            try
            {
                device = InputDevices.GetDeviceAtXRNode(node);
            }
            catch (System.Exception)
            {
                return;
            }
            if (!device.isValid)
            {
                if (renderer != null) renderer.enabled = false;
                grabbing = false;
                return;
            }
            Vector3 pos;
            Quaternion rot;
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out pos)) hand.localPosition = pos;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot)) hand.localRotation = rot;
            if (renderer != null) renderer.enabled = true;

            bool grip = false;
            float gripValue;
            if (device.TryGetFeatureValue(CommonUsages.grip, out gripValue)) grip = gripValue > 0.6f;
            else device.TryGetFeatureValue(CommonUsages.gripButton, out grip);

            bool nearWheel = kart != null && kart.Visuals != null && kart.Visuals.SteeringWheel != null &&
                             (hand.position - kart.Visuals.SteeringWheel.position).magnitude < GrabDistance;
            if (grip && !grabbing && nearWheel)
            {
                grabbing = true;
                grabAngle = HandAngle(hand) - wheelAngle;
                if (InputManager.Instance != null) InputManager.Instance.Rumble(0.1f, 0.3f, 0.05f);
            }
            else if (!grip && grabbing)
            {
                grabbing = false;
            }
            if (renderer != null) renderer.sharedMaterial = grabbing ? handGrab : handIdle;
        }

        /// <summary>Angle of the hand around the wheel axis, in the wheel's local plane.</summary>
        private float HandAngle(Transform hand)
        {
            var wheel = kart.Visuals.SteeringWheel;
            Vector3 local = wheel.InverseTransformPoint(hand.position);
            // Wheel primitive axis is local Y; the rim lies in the local XZ plane.
            return Mathf.Atan2(local.x, -local.z) * Mathf.Rad2Deg;
        }
    }
}
