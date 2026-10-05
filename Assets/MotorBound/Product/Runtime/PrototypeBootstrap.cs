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

            Time.fixedDeltaTime = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;
            Time.maximumDeltaTime = 0.1f;
            UnityEngine.Physics.defaultSolverIterations = 12;
            UnityEngine.Physics.defaultSolverVelocityIterations = 4;
            UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);

            CreateLighting();
            CreateTestFacility();
            CreateWorkshop();
            var vehicle = CreateReferenceVehicle();
            CreateCamera(vehicle.transform);
            CreateHud(vehicle);
        }

        private static RaycastVehicleController CreateReferenceVehicle()
        {
            var root = new GameObject("Kiyora Aven Club Prototype");
            root.transform.SetPositionAndRotation(PrototypeGarageSession.BayCenter + Vector3.up * 0.8f, Quaternion.identity);

            root.AddComponent<Rigidbody>();
            AddReferenceBody(root);

            var controller = root.AddComponent<RaycastVehicleController>();
            var input = root.AddComponent<PrototypeInputDriver>();
            input.Configure(controller);
            var garage = root.AddComponent<PrototypeGarageSession>();
            garage.Initialize(controller, input);
            root.AddComponent<PrototypeSkidTrails>().Configure(controller);
            var workshopPanel = new GameObject("Workshop controls").AddComponent<PrototypeGaragePanel>();
            workshopPanel.Configure(garage);
            return controller;
        }

        // Shared by the playable scene and native driving validation. Geometry uses metres,
        // with dimensions baked into meshes and unit transforms throughout the assembly.
        public static void AddReferenceBody(GameObject root)
        {
            var reference = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            var datumHeight = (float)(reference.Tire.UnloadedRadiusMeters + reference.Suspension.RestLengthMeters
                - reference.MassKilograms * 9.80665d / (4d * reference.Suspension.SpringRateNewtonsPerMeter));
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.32f - datumHeight, 0f);
            collider.size = new Vector3(1.73f, 0.38f, 3.96f);
            var cabinCollider = root.AddComponent<BoxCollider>();
            cabinCollider.center = new Vector3(0f, 0.875f - datumHeight, -0.38f);
            cabinCollider.size = new Vector3(1.42f, 0.73f, 1.35f);

            var bodyMaterial = CreateMaterial("Aven body", new Color(0.08f, 0.34f, 0.58f), 0.65f, 0.35f);
            var glassMaterial = CreateMaterial("Aven glass", new Color(0.06f, 0.1f, 0.14f), 0.85f, 0.55f);
            // Leave visible wheel openings in the blockout instead of burying tires in one solid box.
            CreateVisualBox(root.transform, "Chassis spine", new Vector3(0f, 0.32f - datumHeight, 0f), new Vector3(1.20f, 0.38f, 3.96f), bodyMaterial);
            CreateVisualBox(root.transform, "Central side panels", new Vector3(0f, 0.32f - datumHeight, 0f), new Vector3(1.73f, 0.38f, 1.54f), bodyMaterial);
            CreateVisualBox(root.transform, "Front bumper", new Vector3(0f, 0.32f - datumHeight, 1.795f), new Vector3(1.73f, 0.38f, 0.37f), bodyMaterial);
            CreateVisualBox(root.transform, "Rear bumper", new Vector3(0f, 0.32f - datumHeight, -1.795f), new Vector3(1.73f, 0.38f, 0.37f), bodyMaterial);
            CreateVisualBox(root.transform, "Hood", new Vector3(0f, 0.60f - datumHeight, 0.83f), new Vector3(1.58f, 0.18f, 1.25f), bodyMaterial);
            CreateVisualBox(root.transform, "Cabin", new Vector3(0f, 0.875f - datumHeight, -0.38f), new Vector3(1.42f, 0.73f, 1.35f), glassMaterial);
            CreateVisualBox(root.transform, "Left mirror", new Vector3(-0.91f, 0.86f - datumHeight, 0.15f), new Vector3(0.09f, 0.09f, 0.16f), bodyMaterial);
            CreateVisualBox(root.transform, "Right mirror", new Vector3(0.91f, 0.86f - datumHeight, 0.15f), new Vector3(0.09f, 0.09f, 0.16f), bodyMaterial);

        }

        private static void CreateWorkshop()
        {
            var workshop = new GameObject("Measured prototype workshop");
            var wall = CreateMaterial("Workshop walls", new Color(0.23f, 0.29f, 0.32f), 0.1f, 0.2f);
            var stripe = CreateMaterial("Workshop bay lines", new Color(0.28f, 0.83f, 0.72f), 0f, 0.1f);
            var profile = ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile();
            var openingWidth = (float)profile.PhysicalClearWidthMeters;
            var openingHeight = (float)profile.PhysicalClearHeightMeters;
            const float fullWidth = 7f;
            var sideWidth = (fullWidth - openingWidth) * 0.5f;
            CreateBox(workshop.transform, "Left entrance pier", new Vector3(-(openingWidth + sideWidth) * 0.5f, 1.6f, -115f), new Vector3(sideWidth, 3.2f, 0.3f), wall, true);
            CreateBox(workshop.transform, "Right entrance pier", new Vector3((openingWidth + sideWidth) * 0.5f, 1.6f, -115f), new Vector3(sideWidth, 3.2f, 0.3f), wall, true);
            CreateBox(workshop.transform, "Measured 2.10m lintel", new Vector3(0f, openingHeight + 0.55f, -115f), new Vector3(openingWidth, 1.1f, 0.3f), wall, true);
            CreateBox(workshop.transform, "West workshop wall", new Vector3(-3.5f, 1.6f, -121f), new Vector3(0.2f, 3.2f, 12f), wall, true);
            CreateBox(workshop.transform, "East workshop wall", new Vector3(3.5f, 1.6f, -121f), new Vector3(0.2f, 3.2f, 12f), wall, true);
            CreateBox(workshop.transform, "Rear workshop wall", new Vector3(0f, 1.6f, -127f), new Vector3(7.2f, 3.2f, 0.2f), wall, true);
            CreateBox(workshop.transform, "Bay left line", new Vector3(-1.3f, 0.012f, -121f), new Vector3(0.07f, 0.01f, 5.5f), stripe, false);
            CreateBox(workshop.transform, "Bay right line", new Vector3(1.3f, 0.012f, -121f), new Vector3(0.07f, 0.01f, 5.5f), stripe, false);
            CreateBox(workshop.transform, "Bay stop line", new Vector3(0f, 0.012f, -123.75f), new Vector3(2.6f, 0.01f, 0.07f), stripe, false);
            var sign = new GameObject("Posted workshop clearance");
            sign.transform.SetParent(workshop.transform, false);
            sign.transform.localPosition = new Vector3(0f, 2.72f, -114.8f);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var label = sign.AddComponent<TextMesh>();
            label.text = "WORKSHOP  /  2.00 m MAX\nPark in bay, stop, press G";
            label.fontSize = 64;
            label.characterSize = 0.035f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            var insideSign = Object.Instantiate(sign, workshop.transform);
            insideSign.name = "Posted workshop clearance inside";
            insideSign.transform.localPosition = new Vector3(0f, 2.72f, -115.2f);
            insideSign.transform.localRotation = Quaternion.identity;
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
            wet.AddComponent<SurfaceGrip>().Configure(0.82f, "2.5 mm water film", 2.5f, 0.08f);

            CreateRoughRoad(environment.transform, asphaltMaterial);

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

        private static void CreateRoughRoad(Transform parent, Material material)
        {
            var roughRoad = new GameObject("Rough-road durability strip");
            roughRoad.transform.SetParent(parent, false);
            roughRoad.AddComponent<SurfaceGrip>().Configure(0.96f, "Rough asphalt", 0f, 0.65f);
            CreateBox(roughRoad.transform, "Rough asphalt base", new Vector3(-22f, 0.008f, 225f), new Vector3(9f, 0.016f, 52f), material, true);

            for (var index = 0; index < 18; index++)
            {
                var height = index % 3 == 0 ? 0.034f : 0.022f;
                var lateralOffset = index % 2 == 0 ? -0.7f : 0.65f;
                CreateBox(
                    roughRoad.transform,
                    "Measured roughness ridge " + (index + 1),
                    new Vector3(-22f + lateralOffset, height * 0.5f, 202f + (index * 2.7f)),
                    new Vector3(7.2f, height, 0.34f),
                    material,
                    true);
            }
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
            return PrototypeGeometry.Box(parent, name, position, scale, material, keepCollider);
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
