using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using VortexKarts.Data;

namespace VortexKarts.EditorTools
{
    /// <summary>
    /// One-click project configuration. Runs automatically the first time the project is opened and can be
    /// re-run from the menu: URP asset, XR Plug-in Management with OpenXR + controller profiles, layers,
    /// build scenes and the ScriptableObject data assets generated from DefaultContent.
    /// </summary>
    [InitializeOnLoad]
    public static class KartVRProjectSetup
    {
        private const string MenuRoot = "Vortex Karts/";
        private const string SettingsFolder = "Assets/Settings";
        private const string UrpAssetPath = "Assets/Settings/VortexKarts_URP.asset";
        private const string UrpRendererPath = "Assets/Settings/VortexKarts_URP_Renderer.asset";
        private const string XrFolder = "Assets/XR";
        private const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Loading.unity",
            "Assets/Scenes/Track_NeonCity.unity",
            "Assets/Scenes/Track_SolarCanyon.unity",
            "Assets/Scenes/Track_SkyLab.unity"
        };

        private static string AutoRunKey => "VortexKarts.SetupDone." + Application.dataPath.GetHashCode();

        static KartVRProjectSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorPrefs.GetBool(AutoRunKey, false)) return;
            EditorPrefs.SetBool(AutoRunKey, true);
            Debug.Log("[Vortex Karts] First launch detected: running project setup.");
            RunAll();
        }

        [MenuItem(MenuRoot + "Setup Project (all steps)", false, 0)]
        public static void RunAll()
        {
            try
            {
                ConfigureLayers();
                ConfigurePlayerSettings();
                ConfigureUrp();
                GenerateDataAssets();
                ConfigureBuildScenes();
                ConfigureXr();
                ValidateMaterials();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Vortex Karts] Project setup complete. Open Assets/Scenes/Bootstrap.unity and press Play.");
            }
            catch (Exception e)
            {
                Debug.LogError("[Vortex Karts] Setup failed: " + e);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ Layers & player settings

        [MenuItem(MenuRoot + "Configure Layers", false, 20)]
        public static void ConfigureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return;
            SetLayer(layers, 6, "Kart");
            SetLayer(layers, 7, "Track");
            SetLayer(layers, 8, "Hazard");
            SetLayer(layers, 9, "PowerUp");
            SetLayer(layers, 10, "Wall");
            SetLayer(layers, 11, "Projectile");
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Vortex Karts] Layers configured.");
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            if (index >= layers.arraySize) return;
            var element = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(element.stringValue) || element.stringValue == name) element.stringValue = name;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.companyName = "Nahuz Studio";
            PlayerSettings.productName = "Vortex Karts VR";
        }

        // ------------------------------------------------------------------ URP

        [MenuItem(MenuRoot + "Configure URP", false, 21)]
        public static void ConfigureUrp()
        {
            EnsureFolder(SettingsFolder);
            var existing = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (existing == null)
            {
                existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            }
            if (existing == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    rendererData.name = "VortexKarts_URP_Renderer";
                    AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
                }
                existing = UniversalRenderPipelineAsset.Create(rendererData);
                existing.name = "VortexKarts_URP";
                AssetDatabase.CreateAsset(existing, UrpAssetPath);
            }

            existing.renderScale = 1f;
            existing.msaaSampleCount = 2;
            existing.supportsHDR = false;
            existing.shadowDistance = 45f;
            existing.shadowCascadeCount = 2;
            existing.supportsCameraDepthTexture = false;
            existing.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(existing);

            GraphicsSettings.defaultRenderPipeline = existing;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = existing;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[Vortex Karts] URP asset assigned: " + AssetDatabase.GetAssetPath(existing));
        }

        // ------------------------------------------------------------------ XR

        [MenuItem(MenuRoot + "Configure XR (OpenXR)", false, 22)]
        public static void ConfigureXr()
        {
            try
            {
                EnsureFolder(XrFolder);
                XRGeneralSettingsPerBuildTarget perTarget;
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget);
                if (perTarget == null)
                {
                    perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
                    if (perTarget == null)
                    {
                        perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                        AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
                    }
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
                }

                var group = BuildTargetGroup.Standalone;
                var settings = perTarget.SettingsForBuildTarget(group);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                    settings.name = "Standalone Settings";
                    perTarget.SetSettingsForBuildTarget(group, settings);
                    AssetDatabase.AddObjectToAsset(settings, perTarget);
                }
                if (settings.AssignedSettings == null)
                {
                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    manager.name = "Standalone Providers";
                    settings.AssignedSettings = manager;
                    AssetDatabase.AddObjectToAsset(manager, perTarget);
                }
                settings.InitManagerOnStart = true;

                bool assigned = XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
                EditorUtility.SetDirty(perTarget);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("[Vortex Karts] OpenXR loader " + (assigned ? "assigned" : "already present / could not be assigned") + " for Standalone.");

                ConfigureOpenXrFeatures(group);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Vortex Karts] XR configuration needs a manual step (Project Settings > XR Plug-in Management > check OpenXR). Details: " + e.Message);
            }
        }

        private static void ConfigureOpenXrFeatures(BuildTargetGroup group)
        {
            try
            {
                UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null)
                {
                    Debug.LogWarning("[Vortex Karts] OpenXR settings not created yet. Open Project Settings > XR Plug-in Management > OpenXR once and re-run 'Configure XR'.");
                    return;
                }
                settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                int enabled = 0;
                foreach (var feature in settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRInteractionFeature>())
                {
                    if (feature is OculusTouchControllerProfile || feature is ValveIndexControllerProfile ||
                        feature is HTCViveControllerProfile || feature is MicrosoftMotionControllerProfile ||
                        feature is KHRSimpleControllerProfile || feature is MetaQuestTouchProControllerProfile)
                    {
                        feature.enabled = true;
                        enabled++;
                    }
                }
                EditorUtility.SetDirty(settings);
                Debug.Log("[Vortex Karts] OpenXR: single-pass instanced, " + enabled + " interaction profiles enabled.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Vortex Karts] OpenXR feature setup skipped: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ Data assets

        [MenuItem(MenuRoot + "Generate Data Assets (from DefaultContent)", false, 23)]
        public static void GenerateDataAssets()
        {
            int created = 0;
            created += CreateAssets(DefaultContent.CreateTracks(), "Assets/Resources/" + GameDatabase.TracksPath, t => t.id);
            created += CreateAssets(DefaultContent.CreateKarts(), "Assets/Resources/" + GameDatabase.KartsPath, k => k.id);
            created += CreateAssets(DefaultContent.CreatePowerUps(), "Assets/Resources/" + GameDatabase.PowerUpsPath, p => p.id);
            created += CreateAssets(DefaultContent.CreatePersonalities(), "Assets/Resources/" + GameDatabase.PersonalitiesPath, p => p.id);
            created += CreateAssets(DefaultContent.CreateDifficulties(), "Assets/Resources/" + GameDatabase.DifficultiesPath, d => d.id);
            created += CreateAssets(DefaultContent.CreatePilots(), "Assets/Resources/" + GameDatabase.PilotsPath, p => p.id);
            GameDatabase.Invalidate();
            Debug.Log("[Vortex Karts] Data assets ready (" + created + " created).");
        }

        private static int CreateAssets<T>(List<T> items, string folder, Func<T, string> idOf) where T : ScriptableObject
        {
            EnsureFolder(folder);
            int created = 0;
            for (int i = 0; i < items.Count; i++)
            {
                string path = folder + "/" + idOf(items[i]) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                {
                    UnityEngine.Object.DestroyImmediate(items[i]);
                    continue;
                }
                AssetDatabase.CreateAsset(items[i], path);
                created++;
            }
            return created;
        }

        // ------------------------------------------------------------------ Scenes

        [MenuItem(MenuRoot + "Configure Build Scenes", false, 24)]
        public static void ConfigureBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                if (File.Exists(ScenePaths[i])) list.Add(new EditorBuildSettingsScene(ScenePaths[i], true));
                else Debug.LogWarning("[Vortex Karts] Scene missing: " + ScenePaths[i]);
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Vortex Karts] Build scenes configured (" + list.Count + ").");
        }

        // ------------------------------------------------------------------ Materials

        private static void ValidateMaterials()
        {
            FixMaterial("Assets/Resources/Materials/Base_Lit.mat", "Universal Render Pipeline/Lit", false, false);
            FixMaterial("Assets/Resources/Materials/Base_Emissive.mat", "Universal Render Pipeline/Lit", true, false);
            FixMaterial("Assets/Resources/Materials/Base_Unlit.mat", "Universal Render Pipeline/Unlit", false, false);
            FixMaterial("Assets/Resources/Materials/Base_UnlitTransparent.mat", "Universal Render Pipeline/Unlit", false, true);
            FixMaterial("Assets/Resources/Materials/Base_Particle.mat", "Universal Render Pipeline/Particles/Unlit", false, true);
        }

        private static void FixMaterial(string path, string shaderName, bool emissive, bool transparent)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader == null || mat.shader.name.Contains("InternalError") || mat.shader.name != shaderName)
            {
                mat.shader = shader;
            }
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white);
            }
            if (transparent) VortexKarts.Utils.MaterialLibrary.ConfigureUrpTransparent(mat);
            EditorUtility.SetDirty(mat);
        }
    }
}
