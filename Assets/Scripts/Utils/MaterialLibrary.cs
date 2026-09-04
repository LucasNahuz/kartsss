using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Central place that hands out URP materials for procedurally generated geometry.
    /// Base materials live in Resources/Materials so the URP shaders (and the emission / transparent
    /// variants) are guaranteed to be included in builds. Every requested colour is cached so the
    /// renderer can batch and GPU-instance geometry that shares a material.
    /// </summary>
    public static class MaterialLibrary
    {
        private const string BaseLitPath = "Materials/Base_Lit";
        private const string BaseEmissivePath = "Materials/Base_Emissive";
        private const string BaseUnlitPath = "Materials/Base_Unlit";
        private const string BaseUnlitTransparentPath = "Materials/Base_UnlitTransparent";
        private const string BaseParticlePath = "Materials/Base_Particle";

        private static Material baseLit;
        private static Material baseEmissive;
        private static Material baseUnlit;
        private static Material baseUnlitTransparent;
        private static Material baseParticle;

        private static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private static Material LoadBase(ref Material slot, string resourcePath, string[] shaderFallbacks)
        {
            if (slot != null) return slot;
            slot = Resources.Load<Material>(resourcePath);
            if (slot != null && slot.shader != null && slot.shader.isSupported)
            {
                return slot;
            }
            for (int i = 0; i < shaderFallbacks.Length; i++)
            {
                var shader = Shader.Find(shaderFallbacks[i]);
                if (shader != null)
                {
                    slot = new Material(shader);
                    slot.name = "Runtime_" + resourcePath.Replace('/', '_');
                    return slot;
                }
            }
            Debug.LogError("[MaterialLibrary] Could not find any shader for " + resourcePath);
            slot = new Material(Shader.Find("Hidden/InternalErrorShader"));
            return slot;
        }

        private static Material BaseLit => LoadBase(ref baseLit, BaseLitPath,
            new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard" });

        private static Material BaseEmissive => LoadBase(ref baseEmissive, BaseEmissivePath,
            new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard" });

        private static Material BaseUnlit => LoadBase(ref baseUnlit, BaseUnlitPath,
            new[] { "Universal Render Pipeline/Unlit", "Unlit/Color" });

        private static Material BaseUnlitTransparent => LoadBase(ref baseUnlitTransparent, BaseUnlitTransparentPath,
            new[] { "Universal Render Pipeline/Unlit", "Unlit/Transparent" });

        private static Material BaseParticle => LoadBase(ref baseParticle, BaseParticlePath,
            new[] { "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit", "Particles/Standard Unlit" });

        private static long Key(int family, Color c, float a, float b)
        {
            // Quantise so near-identical requests share a material.
            int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            int bl = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            int al = Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f);
            int qa = Mathf.RoundToInt(Mathf.Clamp(a, 0f, 8f) * 31f);
            int qb = Mathf.RoundToInt(Mathf.Clamp(b, 0f, 8f) * 31f);
            long key = family;
            key = key * 256 + r;
            key = key * 256 + g;
            key = key * 256 + bl;
            key = key * 256 + al;
            key = key * 512 + qa;
            key = key * 512 + qb;
            return key;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            else m.color = c;
        }

        /// <summary>Opaque lit material.</summary>
        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            long key = Key(1, color, smoothness, metallic);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseLit);
            m.name = "Lit_" + ColorUtility.ToHtmlStringRGB(color);
            SetColor(m, color);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, metallic);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>Lit material with emission (neon signs, boost pads, rings).</summary>
        public static Material Emissive(Color baseColor, Color emission, float intensity = 2f)
        {
            long key = Key(2, baseColor, intensity, emission.r * 0.3f + emission.g * 0.6f + emission.b * 2f);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseEmissive);
            m.name = "Emissive_" + ColorUtility.ToHtmlStringRGB(emission);
            SetColor(m, baseColor);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, emission * intensity);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.5f);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>Flat unlit opaque material (UI-ish world elements, skyline silhouettes).</summary>
        public static Material Unlit(Color color)
        {
            long key = Key(3, color, 0f, 0f);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseUnlit);
            m.name = "Unlit_" + ColorUtility.ToHtmlStringRGB(color);
            SetColor(m, color);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>
        /// Unlit alpha-blended material. Not cached when a texture is supplied because the texture
        /// is unique per caller (fader, vignette, holograms).
        /// </summary>
        public static Material UnlitTransparent(Color color, Texture texture = null)
        {
            if (texture == null)
            {
                long key = Key(4, color, 0f, 0f);
                Material cached;
                if (cache.TryGetValue(key, out cached) && cached != null) return cached;
                var m = CreateTransparent(color, null);
                cache[key] = m;
                return m;
            }
            return CreateTransparent(color, texture);
        }

        private static Material CreateTransparent(Color color, Texture texture)
        {
            var m = new Material(BaseUnlitTransparent);
            m.name = "UnlitTransparent_" + ColorUtility.ToHtmlStringRGBA(color);
            SetColor(m, color);
            // In case the base asset was replaced by a Shader.Find fallback, force the transparent setup.
            ConfigureUrpTransparent(m);
            if (texture != null && m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, texture);
            else if (texture != null) m.mainTexture = texture;
            return m;
        }

        /// <summary>Material for particle systems (additive-looking, unlit).</summary>
        public static Material Particle(Color color)
        {
            long key = Key(5, color, 0f, 0f);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseParticle);
            m.name = "Particle_" + ColorUtility.ToHtmlStringRGBA(color);
            SetColor(m, color);
            ConfigureUrpTransparent(m);
            cache[key] = m;
            return m;
        }

        /// <summary>Sets the properties URP Lit/Unlit shaders use to switch to alpha blending.</summary>
        public static void ConfigureUrpTransparent(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Radial gradient texture used by the comfort vignette (transparent centre, opaque edge).</summary>
        public static Texture2D CreateRadialGradient(int size, float innerRadius, float outerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.InverseLerp(innerRadius, outerRadius, d);
                    a = MathUtil.SmoothStep01(a);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Solid 1x1 white texture, handy for UI images and quads.</summary>
        public static Texture2D WhiteTexture => Texture2D.whiteTexture;

        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}
