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
            PlayerSettings.bundleVersion = "0.2.1";
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
            ConfigurePrototypeProject();
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
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
