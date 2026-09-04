using UnityEngine;
using VortexKarts.Utils;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Procedurally animated pilot: arms follow the steering wheel grips, head looks into the turn,
    /// body reacts to impacts and there are celebration / defeat poses for the results screen.
    /// </summary>
    public class PilotRig : MonoBehaviour
    {
        public Transform Head;
        public Transform HeadPivot;
        public Transform Torso;
        public Transform LeftArm;
        public Transform RightArm;
        public Transform LeftHand;
        public Transform RightHand;
        public Transform LeftGrip;
        public Transform RightGrip;
        public Vector3 LeftShoulder;
        public Vector3 RightShoulder;
        public Renderer[] HeadRenderers = new Renderer[0];

        public enum Mood { Racing, Celebrate, Defeat }
        public Mood CurrentMood = Mood.Racing;

        private float steerLean;
        private float jolt;
        private float moodTime;
        private KartController kart;

        public void Bind(KartController controller)
        {
            kart = controller;
        }

        public void SetHeadVisible(bool visible)
        {
            for (int i = 0; i < HeadRenderers.Length; i++)
            {
                if (HeadRenderers[i] != null) HeadRenderers[i].enabled = visible;
            }
        }

        public void Jolt(float strength)
        {
            jolt = Mathf.Max(jolt, Mathf.Clamp01(strength));
        }

        public void SetMood(Mood mood)
        {
            if (CurrentMood == mood) return;
            CurrentMood = mood;
            moodTime = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            moodTime += dt;
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 2.5f);

            float steer = kart != null ? kart.EffectiveSteer : 0f;
            steerLean = MathUtil.Damp(steerLean, steer, 8f, dt);

            switch (CurrentMood)
            {
                case Mood.Racing:
                    AnimateRacing(dt);
                    break;
                case Mood.Celebrate:
                    AnimateCelebrate();
                    break;
                case Mood.Defeat:
                    AnimateDefeat();
                    break;
            }
        }

        private void AnimateRacing(float dt)
        {
            if (Torso != null)
            {
                float roll = -steerLean * 6f;
                float pitch = -jolt * 14f;
                Torso.localRotation = Quaternion.Euler(pitch, 0f, roll);
            }
            if (HeadPivot != null)
            {
                float yaw = steerLean * 18f;
                float bob = Mathf.Sin(Time.time * 9f) * jolt * 4f;
                HeadPivot.localRotation = Quaternion.Euler(-jolt * 10f + bob, yaw, -steerLean * 4f);
            }
            PlaceArm(LeftArm, LeftHand, LeftShoulder, LeftGrip);
            PlaceArm(RightArm, RightHand, RightShoulder, RightGrip);
        }

        private void AnimateCelebrate()
        {
            float t = moodTime;
            if (Torso != null) Torso.localRotation = Quaternion.Euler(-8f + Mathf.Sin(t * 6f) * 4f, 0f, 0f);
            if (HeadPivot != null) HeadPivot.localRotation = Quaternion.Euler(-15f + Mathf.Sin(t * 6f) * 6f, Mathf.Sin(t * 3f) * 20f, 0f);
            // Arms pumped upwards.
            Vector3 lTarget = LeftShoulder + new Vector3(-0.15f, 0.55f + Mathf.Sin(t * 6f) * 0.06f, 0.05f);
            Vector3 rTarget = RightShoulder + new Vector3(0.15f, 0.55f + Mathf.Cos(t * 6f) * 0.06f, 0.05f);
            PlaceArmTo(LeftArm, LeftHand, LeftShoulder, lTarget);
            PlaceArmTo(RightArm, RightHand, RightShoulder, rTarget);
        }

        private void AnimateDefeat()
        {
            if (Torso != null) Torso.localRotation = Quaternion.Euler(18f, 0f, 0f);
            if (HeadPivot != null) HeadPivot.localRotation = Quaternion.Euler(35f, Mathf.Sin(moodTime * 1.5f) * 10f, 0f);
            PlaceArm(LeftArm, LeftHand, LeftShoulder, LeftGrip);
            PlaceArm(RightArm, RightHand, RightShoulder, RightGrip);
        }

        private void PlaceArm(Transform arm, Transform hand, Vector3 shoulderLocal, Transform grip)
        {
            if (arm == null || grip == null) return;
            Vector3 target = transform.InverseTransformPoint(grip.position);
            PlaceArmTo(arm, hand, shoulderLocal, target);
        }

        private void PlaceArmTo(Transform arm, Transform hand, Vector3 shoulderLocal, Vector3 targetLocal)
        {
            if (arm == null) return;
            Vector3 dir = targetLocal - shoulderLocal;
            float len = dir.magnitude;
            if (len < 0.001f) return;
            arm.localPosition = (shoulderLocal + targetLocal) * 0.5f;
            arm.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            Vector3 s = arm.localScale;
            arm.localScale = new Vector3(s.x, len * 0.5f, s.z); // capsule primitive is 2 units tall
            if (hand != null) hand.localPosition = targetLocal;
        }
    }
}
