using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VortexKarts.Core
{
    /// <summary>
    /// Applies the graphics block of GameSettings to URP and the active camera.
    /// Shadows on the sun light and particle density are handled by the track builder / kart feedback,
    /// which listen to GameEvents.OnSettingsApplied.
    /// </summary>
    public static class GraphicsSettingsApplier
    {
        public static float ShadowDistanceFor(int shadows)
        {
            switch (shadows)
            {
                case 0: return 0f;
                case 1: return 45f;
                default: return 90f;
            }
        }

        public static int MsaaFor(int antiAliasing)
        {
            switch (antiAliasing)
            {
                case 0: return 1;
                case 1: return 2;
                default: return 4;
            }
        }

        public static void Apply(GameSettings s)
        {
            if (s == null) return;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = Mathf.Clamp(s.renderScale, 0.5f, 1.5f);
                urp.shadowDistance = ShadowDistanceFor(s.shadows);
                urp.msaaSampleCount = MsaaFor(s.antiAliasing);
                urp.supportsHDR = false;
            }
            else
            {
                Debug.LogWarning("[Graphics] No URP asset assigned in Graphics settings. Run 'Vortex Karts > Setup Project'.");
            }

            ApplyToCamera(Camera.main, s);
        }

        public static void ApplyToCamera(Camera cam, GameSettings s)
        {
            if (cam == null || s == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = false; // post FX are too costly for VR; effects are handled with particles.
            data.antialiasing = AntialiasingMode.None; // MSAA from the pipeline asset instead.
            data.renderShadows = s.shadows > 0;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.allowXRRendering = true;
        }
    }
}
