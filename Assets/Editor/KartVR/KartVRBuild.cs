using System.IO;
using UnityEditor;
using UnityEngine;

namespace VortexKarts.EditorTools
{
    /// <summary>
    /// Command-line friendly builds. Example:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod VortexKarts.EditorTools.KartVRBuild.BuildWindowsDev
    /// </summary>
    public static class KartVRBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Loading.unity",
            "Assets/Scenes/Track_NeonCity.unity",
            "Assets/Scenes/Track_SolarCanyon.unity",
            "Assets/Scenes/Track_SkyLab.unity"
        };

        [MenuItem("Vortex Karts/Build/Windows (Development)", false, 100)]
        public static void BuildWindowsDev()
        {
            Build("Builds/Dev/VortexKartsVR.exe", BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("Vortex Karts/Build/Windows (Release)", false, 101)]
        public static void BuildWindowsRelease()
        {
            Build("Builds/Release/VortexKartsVR.exe", BuildOptions.None);
        }

        /// <summary>
        /// In batch mode the OpenXR settings singleton is loaded lazily and the OpenXR build hook fails with
        /// "OpenXR Settings found in project but not yet loaded". Touching the assets first avoids that.
        /// </summary>
        private static void PreloadOpenXrSettings()
        {
            try
            {
                var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
                if (settings != null) Debug.Log("[Vortex Karts] OpenXR settings preloaded: " + settings.name);
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/XR" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    var objs = AssetDatabase.LoadAllAssetsAtPath(p);
                    Debug.Log("[Vortex Karts] Loaded XR asset " + p + " (" + objs.Length + " objects)");
                }
                var instance = UnityEngine.XR.OpenXR.OpenXRSettings.Instance;
                Debug.Log("[Vortex Karts] OpenXRSettings.Instance is " + (instance != null ? "ready" : "null"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Vortex Karts] OpenXR preload: " + e.Message);
            }
        }

        private static void Build(string path, BuildOptions options)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            PreloadOpenXrSettings();
            var report = BuildPipeline.BuildPlayer(Scenes, path, BuildTarget.StandaloneWindows64, options);
            var summary = report.summary;
            Debug.Log("[Vortex Karts] Build " + summary.result + " -> " + summary.outputPath + " (" + (summary.totalSize / (1024 * 1024)) + " MB, " +
                      summary.totalErrors + " errors, " + summary.totalWarnings + " warnings)");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
