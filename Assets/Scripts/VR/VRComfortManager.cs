using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Kart;
using VortexKarts.Utils;

namespace VortexKarts.VR
{
    /// <summary>
    /// Decides how the seat's motion reaches the player's head:
    /// - Horizon stabilisation: the rig follows the kart's yaw fully, but pitch/roll only partially (tilt setting).
    /// - Reduced camera motion: additional smoothing of yaw.
    /// - Comfort vignette: darkens the periphery during fast turns / strong lateral acceleration.
    /// - Camera shake: OFF / LOW / MEDIUM, applied as a tiny positional offset of the seat, never rotation.
    /// The headset pose itself is never altered.
    /// </summary>
    public class VRComfortManager : MonoBehaviour
    {
        private VRManager vr;
        private Transform seat;
        private KartController kart;
        private GameSettings settings;

        private Quaternion currentRotation = Quaternion.identity;
        private float lastYaw;
        private float yawRate;
        private Vector3 lastVelocity;
        private float vignetteAlpha;
        private Material vignetteMaterial;
        private GameObject vignetteQuad;
        private Vector3 shakeOffset;
        private float shakeAmplitude;
        private float shakeSeed;

        public float VignetteAlpha => vignetteAlpha;

        public void Initialize(VRManager manager)
        {
            vr = manager;
            settings = SaveManager.Settings;
            BuildVignette();
            GameEvents.OnKartHit += OnKartHit;
            GameEvents.OnKartLanded += OnKartLanded;
            shakeSeed = Random.value * 100f;
        }

        private void OnDestroy()
        {
            GameEvents.OnKartHit -= OnKartHit;
            GameEvents.OnKartLanded -= OnKartLanded;
        }

        private void BuildVignette()
        {
            var tex = MaterialLibrary.CreateRadialGradient(256, 0.45f, 1.0f);
            vignetteMaterial = MaterialLibrary.UnlitTransparent(new Color(0f, 0f, 0f, 0f), tex);
            vignetteMaterial.renderQueue = 4000;
            vignetteQuad = PrimitiveFactory.Quad("ComfortVignette", vr.Head, new Vector3(0f, 0f, 0.3f), new Vector2(1.5f, 1.5f), vignetteMaterial);
            vignetteQuad.layer = 2;
            PrimitiveFactory.SetShadowCasting(vignetteQuad, false, false);
            vignetteQuad.SetActive(false);
        }

        public void ApplySettings(GameSettings s)
        {
            settings = s;
        }

        public void SetSeat(Transform seatAnchor, KartController controller)
        {
            seat = seatAnchor;
            kart = controller;
            if (seat != null)
            {
                currentRotation = Quaternion.Euler(0f, seat.eulerAngles.y, 0f);
                lastYaw = seat.eulerAngles.y;
                vr.Rig.SetPositionAndRotation(seat.position, currentRotation);
            }
            lastVelocity = Vector3.zero;
            shakeAmplitude = 0f;
        }

        private void OnKartHit(KartController k, KartHitKind kind, float strength)
        {
            if (kart == null || k != kart || settings == null || settings.cameraShake == 0) return;
            float scale = settings.cameraShake == 1 ? 0.012f : 0.022f;
            shakeAmplitude = Mathf.Max(shakeAmplitude, strength * scale);
        }

        private void OnKartLanded(KartController k, float airTime)
        {
            if (kart == null || k != kart || settings == null || settings.cameraShake == 0) return;
            if (airTime < 0.3f) return;
            float scale = settings.cameraShake == 1 ? 0.008f : 0.015f;
            shakeAmplitude = Mathf.Max(shakeAmplitude, Mathf.Clamp01(airTime) * scale);
        }

        private void LateUpdate()
        {
            if (seat == null || vr == null || vr.Rig == null) return;
            if (settings == null) settings = SaveManager.Settings;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // ---- Rotation
            Vector3 seatEuler = seat.rotation.eulerAngles;
            float yaw = seatEuler.y;
            float pitch = MathUtil.WrapAngle180(seatEuler.x);
            float roll = MathUtil.WrapAngle180(seatEuler.z);

            float tilt = settings.kartTiltIntensity;
            Quaternion target;
            if (settings.horizonStabilization)
            {
                float p = Mathf.Clamp(pitch * tilt * 0.6f, -8f, 8f);
                float r = Mathf.Clamp(roll * tilt * 0.4f, -6f, 6f);
                target = Quaternion.Euler(p, yaw, r);
            }
            else
            {
                target = Quaternion.Euler(Mathf.Clamp(pitch, -25f, 25f), yaw, Mathf.Clamp(roll, -20f, 20f));
            }
            float lambda = settings.reducedCameraMotion ? 9f : 30f;
            currentRotation = MathUtil.Damp(currentRotation, target, lambda, dt);
            // Yaw must never lag so far that the kart body visibly rotates under the player: clamp error.
            float yawError = MathUtil.WrapAngle180(yaw - currentRotation.eulerAngles.y);
            if (Mathf.Abs(yawError) > 12f)
            {
                var e = currentRotation.eulerAngles;
                currentRotation = Quaternion.Euler(e.x, yaw - Mathf.Sign(yawError) * 12f, e.z);
            }

            yawRate = Mathf.Abs(MathUtil.WrapAngle180(yaw - lastYaw)) / dt;
            lastYaw = yaw;

            // ---- Shake (positional only, tiny)
            shakeAmplitude = Mathf.MoveTowards(shakeAmplitude, 0f, dt * 0.08f);
            if (shakeAmplitude > 0.0005f)
            {
                float t = Time.unscaledTime * 28f + shakeSeed;
                shakeOffset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (shakeAmplitude * 2f);
            }
            else shakeOffset = Vector3.zero;

            vr.Rig.SetPositionAndRotation(seat.position + currentRotation * shakeOffset, currentRotation);

            // ---- Vignette
            float targetAlpha = 0f;
            if (settings.comfortVignette && kart != null)
            {
                Vector3 vel = kart.Velocity;
                Vector3 accel = (vel - lastVelocity) / dt;
                lastVelocity = vel;
                float lateral = Mathf.Abs(Vector3.Dot(accel, kart.Right));
                float fromYaw = Mathf.Clamp01(yawRate / 140f);
                float fromAccel = Mathf.Clamp01(lateral / 18f);
                float fromBoost = kart.Boost.Intensity * 0.35f;
                float fromSpin = kart.Status.IsSpinning ? 0.6f : 0f;
                targetAlpha = Mathf.Clamp01(Mathf.Max(fromYaw * 0.7f, fromAccel * 0.6f, fromBoost, fromSpin)) * settings.vignetteIntensity;
            }
            vignetteAlpha = MathUtil.Damp(vignetteAlpha, targetAlpha, targetAlpha > vignetteAlpha ? 10f : 4f, dt);
            bool show = vignetteAlpha > 0.01f;
            if (vignetteQuad.activeSelf != show) vignetteQuad.SetActive(show);
            if (show)
            {
                var c = new Color(0f, 0f, 0f, vignetteAlpha);
                if (vignetteMaterial.HasProperty("_BaseColor")) vignetteMaterial.SetColor("_BaseColor", c);
                else vignetteMaterial.color = c;
            }
        }
    }
}
