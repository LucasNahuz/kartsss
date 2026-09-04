using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Builds particle systems in code so no prefab assets are needed. All systems use the shared
    /// unlit particle material and fade alpha/size over life.
    /// </summary>
    public static class VfxFactory
    {
        public static ParticleSystem CreateStream(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Color color, float size, float speed, float lifetime, float rate, float coneAngle = 18f, int maxParticles = 150)
        {
            var ps = CreateBase(name, parent, localPosition, localRotation, color, size, speed, lifetime, maxParticles);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.08f;
            ps.Play();
            return ps;
        }

        public static ParticleSystem CreateBurst(string name, Transform parent, Vector3 localPosition, Color color, float size,
            float speed, float lifetime, int maxParticles = 80, float sphereRadius = 0.3f)
        {
            var ps = CreateBase(name, parent, localPosition, Quaternion.identity, color, size, speed, lifetime, maxParticles);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = sphereRadius;
            ps.Play();
            return ps;
        }

        private static ParticleSystem CreateBase(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Color color, float size, float speed, float lifetime, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MaterialLibrary.Particle(Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;
            return ps;
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
        }

        public static void SetColor(ParticleSystem ps, Color color)
        {
            if (ps == null) return;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }

        public static void SetSpeed(ParticleSystem ps, float speed)
        {
            if (ps == null) return;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
        }
    }
}
