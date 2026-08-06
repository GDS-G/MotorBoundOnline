using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotorBound.Product
{
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindObjectOfType<RaycastVehicleController>() != null)
            {
                return;
            }

            Time.fixedDeltaTime = 0.02f;
            Time.maximumDeltaTime = 0.1f;
            UnityEngine.Physics.defaultSolverIterations = 12;
            UnityEngine.Physics.defaultSolverVelocityIterations = 4;
            UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);

            CreateLighting();
            CreateTestFacility();
            var vehicle = CreateReferenceVehicle();
            CreateCamera(vehicle.transform);
            CreateHud(vehicle);
        }

        private static RaycastVehicleController CreateReferenceVehicle()
        {
            var root = new GameObject("Kiyora Aven Club Prototype");
            root.transform.SetPositionAndRotation(new Vector3(0f, 0.92f, -120f), Quaternion.identity);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1120f;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.02f, 0f);
            collider.size = new Vector3(1.62f, 0.48f, 3.75f);

            var bodyMaterial = CreateMaterial("Aven body", new Color(0.08f, 0.34f, 0.58f), 0.65f, 0.35f);
            var glassMaterial = CreateMaterial("Aven glass", new Color(0.06f, 0.1f, 0.14f), 0.85f, 0.55f);
            CreateVisualBox(root.transform, "Lower body", new Vector3(0f, 0f, 0f), new Vector3(1.68f, 0.46f, 3.8f), bodyMaterial);
            CreateVisualBox(root.transform, "Hood", new Vector3(0f, 0.32f, 0.83f), new Vector3(1.58f, 0.18f, 1.25f), bodyMaterial);
            CreateVisualBox(root.transform, "Cabin", new Vector3(0f, 0.47f, -0.38f), new Vector3(1.45f, 0.48f, 1.35f), glassMaterial);
            CreateVisualBox(root.transform, "Front bumper", new Vector3(0f, -0.05f, 1.94f), new Vector3(1.62f, 0.22f, 0.12f), bodyMaterial);
            CreateVisualBox(root.transform, "Rear bumper", new Vector3(0f, -0.05f, -1.94f), new Vector3(1.62f, 0.22f, 0.12f), bodyMaterial);

            var controller = root.AddComponent<RaycastVehicleController>();
            controller.Configure(ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype());
            var input = root.AddComponent<PrototypeInputDriver>();
            input.Configure(controller);
            return controller;
        }

        private static void CreateTestFacility()
        {
            var environment = new GameObject("Vehicle Dynamics Test Facility");
            var asphaltMaterial = CreateMaterial("Dry asphalt", new Color(0.13f, 0.145f, 0.16f), 0f, 0.05f);
            var wetMaterial = CreateMaterial("Wet asphalt", new Color(0.055f, 0.11f, 0.15f), 0.15f, 0.65f);
            var paintMaterial = CreateMaterial("Road paint", new Color(0.9f, 0.88f, 0.7f), 0f, 0.1f);
            var coneMaterial = CreateMaterial("Safety orange", new Color(1f, 0.28f, 0.03f), 0f, 0.1f);

            var ground = CreateBox(environment.transform, "Dry asphalt", new Vector3(0f, -0.12f, 80f), new Vector3(180f, 0.24f, 480f), asphaltMaterial, true);
            ground.AddComponent<SurfaceGrip>().Configure(1f, "Dry asphalt");

            var wet = CreateBox(environment.transform, "Wet handling pad", new Vector3(0f, 0.008f, 28f), new Vector3(16f, 0.016f, 42f), wetMaterial, true);
            wet.AddComponent<SurfaceGrip>().Configure(0.58f, "Wet asphalt");

            for (var z = -110; z <= 285; z += 12)
            {
                CreateBox(environment.transform, "Center line", new Vector3(0f, 0.012f, z), new Vector3(0.12f, 0.018f, 5f), paintMaterial, false);
            }

            for (var z = -110; z <= 285; z += 8)
            {
                CreateBox(environment.transform, "Left edge line", new Vector3(-5.5f, 0.011f, z), new Vector3(0.1f, 0.015f, 7.5f), paintMaterial, false);
                CreateBox(environment.transform, "Right edge line", new Vector3(5.5f, 0.011f, z), new Vector3(0.1f, 0.015f, 7.5f), paintMaterial, false);
            }

            CreateSkidpad(environment.transform, new Vector3(45f, 0f, 95f), 24f, coneMaterial);
            CreateSlalom(environment.transform, coneMaterial);

            CreateBox(environment.transform, "North barrier", new Vector3(0f, 0.5f, 319f), new Vector3(180f, 1f, 1f), coneMaterial, true);
            CreateBox(environment.transform, "West barrier", new Vector3(-89.5f, 0.5f, 80f), new Vector3(1f, 1f, 480f), coneMaterial, true);
            CreateBox(environment.transform, "East barrier", new Vector3(89.5f, 0.5f, 80f), new Vector3(1f, 1f, 480f), coneMaterial, true);
        }

        private static void CreateSkidpad(Transform parent, Vector3 center, float radius, Material material)
        {
            const int markerCount = 40;
            for (var index = 0; index < markerCount; index++)
            {
                var angle = (index / (float)markerCount) * Mathf.PI * 2f;
                var position = center + new Vector3(Mathf.Cos(angle) * radius, 0.24f, Mathf.Sin(angle) * radius);
                CreateBox(parent, "Skidpad marker", position, new Vector3(0.32f, 0.48f, 0.32f), material, true);
            }
        }

        private static void CreateSlalom(Transform parent, Material material)
        {
            for (var index = 0; index < 8; index++)
            {
                var x = index % 2 == 0 ? -2.5f : 2.5f;
                var z = 120f + (index * 13f);
                CreateBox(parent, "Slalom marker", new Vector3(x, 0.28f, z), new Vector3(0.38f, 0.56f, 0.38f), material, true);
            }
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.53f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.31f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.11f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.52f, 0.62f, 0.71f);
            RenderSettings.fogDensity = 0.0015f;

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        }

        private static void CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Prototype Chase Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 64f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 1200f;
            var chase = cameraObject.AddComponent<PrototypeChaseCamera>();
            chase.Configure(target);
        }

        private static void CreateHud(RaycastVehicleController vehicle)
        {
            var hudObject = new GameObject("Prototype Telemetry HUD");
            var hud = hudObject.AddComponent<PrototypeTelemetryHud>();
            hud.Configure(vehicle);
        }

        private static GameObject CreateVisualBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var result = CreateBox(parent, name, localPosition, localScale, material, false);
            result.transform.localPosition = localPosition;
            return result;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            var renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            var collider = box.GetComponent<Collider>();
            if (!keepCollider && collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            return box;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");
            var material = new Material(shader)
            {
                name = name,
                color = color
            };
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }
    }
}
