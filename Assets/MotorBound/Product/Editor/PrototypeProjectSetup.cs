using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotorBound.Editor
{
    public static class PrototypeProjectSetup
    {
        private const string SceneDirectory = "Assets/MotorBound/Product/Scenes";
        private const string ScenePath = SceneDirectory + "/VehicleDynamicsPrototype.unity";
        private const string PrototypeVersion = "0.2.4";

        [MenuItem("MotorBound/Configure Prototype Project")]
        public static void ConfigurePrototypeProject()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MotorBound/Product/Scenes"));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("Runtime Prototype Bootstrap");
            marker.transform.position = Vector3.zero;
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "GDS-G";
            PlayerSettings.productName = "MotorBound Online - Vehicle Dynamics Prototype";
            PlayerSettings.bundleVersion = PrototypeVersion;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MotorBound prototype project configured. Scene: " + ScenePath);
        }

        [MenuItem("MotorBound/Build Windows Prototype")]
        public static void BuildWindowsPrototype()
        {
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
            BuildWindowsPrototypeAt(outputDirectory);
        }

        // Keep a player's running build untouched while delivering the next playtest.
        [MenuItem("MotorBound/Build Versioned Windows Prototype")]
        public static void BuildVersionedWindowsPrototype()
        {
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Builds/Windows", PrototypeVersion));
            BuildWindowsPrototypeAt(outputDirectory);
        }

        private static void BuildWindowsPrototypeAt(string outputDirectory)
        {
            ConfigurePrototypeProject();
            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outputDirectory, "MotorBoundVehiclePrototype.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Prototype build failed with " + report.summary.totalErrors + " error(s). Result: " + report.summary.result);
            }

            Debug.Log(
                "MotorBound Windows prototype built successfully: "
                + options.locationPathName
                + " ("
                + report.summary.totalSize
                + " bytes)");
        }
    }
}
