using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Small math helpers shared by gameplay, AI and VR code.
    /// </summary>
    public static class MathUtil
    {
        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMax, inMin)) return outMin;
            float t = Mathf.InverseLerp(inMin, inMax, value);
            return Mathf.Lerp(outMin, outMax, t);
        }

        /// <summary>Frame-rate independent exponential smoothing. Higher lambda = faster.</summary>
        public static float Damp(float current, float target, float lambda, float deltaTime)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * deltaTime));
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float deltaTime)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-lambda * deltaTime));
        }

        public static Quaternion Damp(Quaternion current, Quaternion target, float lambda, float deltaTime)
        {
            return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-lambda * deltaTime));
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        public static Vector3 FlatNormalized(Vector3 v)
        {
            v.y = 0f;
            float m = v.magnitude;
            return m > 0.0001f ? v / m : Vector3.forward;
        }

        public static float SignedYawAngle(Vector3 from, Vector3 to)
        {
            return Vector3.SignedAngle(Flat(from), Flat(to), Vector3.up);
        }

        public static float ApplyDeadzone(float value, float deadzone)
        {
            float a = Mathf.Abs(value);
            if (a <= deadzone) return 0f;
            float scaled = (a - deadzone) / (1f - deadzone);
            return Mathf.Sign(value) * Mathf.Clamp01(scaled);
        }

        /// <summary>Response curve. exponent 1 = linear, 2 = soft center, 0.6 = twitchy.</summary>
        public static float ApplyCurve(float value, float exponent)
        {
            if (exponent <= 0f) return value;
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }

        public static float WrapAngle180(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }

        public static float Wrap(float value, float length)
        {
            if (length <= 0f) return 0f;
            value %= length;
            if (value < 0f) value += length;
            return value;
        }

        /// <summary>Shortest signed distance from a to b on a loop of the given length.</summary>
        public static float LoopDelta(float from, float to, float length)
        {
            float d = Wrap(to - from, length);
            if (d > length * 0.5f) d -= length;
            return d;
        }

        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--.---";
            int minutes = (int)(seconds / 60f);
            float rem = seconds - minutes * 60f;
            int secs = (int)rem;
            int millis = (int)((rem - secs) * 1000f);
            return string.Format("{0:00}:{1:00}.{2:000}", minutes, secs, millis);
        }

        public static string Ordinal(int position)
        {
            switch (position)
            {
                case 1: return "1º";
                case 2: return "2º";
                case 3: return "3º";
                default: return position + "º";
            }
        }
    }
}
