using System;
using System.Collections.Generic;
using System.IO;
using MotorBound.Product;
using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MotorBound.Editor
{
    // Actual Unity Rigidbody/raycast smoke checks. These are broad regression bounds, not handling calibration.
    public static class PrototypeDrivingValidation
    {
        private const float StepSeconds = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;

        [MenuItem("MotorBound/Validate Driving Physics")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Run driving validation outside Play mode.");
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before running driving validation.");
                }
            }

            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var priorSimulationMode = UnityEngine.Physics.simulationMode;
            var priorGravity = UnityEngine.Physics.gravity;
            var priorSolverIterations = UnityEngine.Physics.defaultSolverIterations;
            var priorVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations;
            var report = new DrivingReport
            {
                UnityVersion = Application.unityVersion,
                SimulationHertz = RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz,
                GeneratedUtc = DateTime.UtcNow.ToString("o")
            };

            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);
                UnityEngine.Physics.defaultSolverIterations = 12;
                UnityEngine.Physics.defaultSolverVelocityIterations = 4;
                var stock = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState());
                var touringState = PrototypeGarageCatalog.CreateNewState();
                if (!PrototypeGarageCatalog.TryInstallCompanionKit(touringState, out var kitMessage))
                {
                    throw new InvalidOperationException("The authored touring package could not be installed: " + kitMessage);
                }
                if (!PrototypeGarageCatalog.TryInstallWheelPackage(touringState, PrototypeGarageCatalog.TouringKey, out var packageMessage))
                {
                    throw new InvalidOperationException("The authored touring package could not be installed: " + packageMessage);
                }

                var touring = PrototypeGarageCatalog.Compile(touringState);
                Check(touring.Vehicle.MassKilograms > stock.Vehicle.MassKilograms
                      && touring.Vehicle.Tire.UnloadedRadiusMeters > stock.Vehicle.Tire.UnloadedRadiusMeters
                      && touring.Vehicle.Tire.SectionWidthMeters > stock.Vehicle.Tire.SectionWidthMeters,
                    "Compiled touring assembly must change mass and tire dimensions from stock.", report);
                foreach (var configuration in new[] { stock, touring })
                {
                    var dry = RunManeuver(configuration, "Dry acceleration and braking", false, false, false, report);
                    var wet = RunManeuver(configuration, "Wet acceleration and braking", true, false, false, report);
                    RunManeuver(configuration, "Dry steering", false, false, true, report);
                    RunManeuver(configuration, "Rough road", false, true, false, report);
                    Check(wet.MinimumObservedGrip < dry.MinimumObservedGrip, configuration.WheelPackageName + ": wet contact must report less grip than dry contact.", report);
                    Check(wet.MaximumObservedWaterFilmMillimeters >= 2.49f, configuration.WheelPackageName + ": wet contact must sample the configured 2.5 mm water film.", report);
                    foreach (var accelerationSeconds in new[] { 2f, 4f })
                    {
                        RunSteeringRelease(configuration, accelerationSeconds, -1f, report);
                        RunSteeringRelease(configuration, accelerationSeconds, 1f, report);
                    }
                }
            }
            catch (Exception exception)
            {
                report.Failures.Add(exception.ToString());
            }
            finally
            {
                UnityEngine.Physics.simulationMode = priorSimulationMode;
                UnityEngine.Physics.gravity = priorGravity;
                UnityEngine.Physics.defaultSolverIterations = priorSolverIterations;
                UnityEngine.Physics.defaultSolverVelocityIterations = priorVelocityIterations;
                var hasSavedScene = false;
                foreach (var setup in sceneSetup)
                {
                    hasSavedScene |= !string.IsNullOrEmpty(setup.path);
                }

                if (hasSavedScene)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                report.Passed = report.Failures.Count == 0;
                var artifactDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts"));
                Directory.CreateDirectory(artifactDirectory);
                var reportPath = Path.Combine(artifactDirectory, "driving-validation.json");
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log("MotorBound driving validation: " + reportPath + "; passed=" + report.Passed);
            }

            if (!report.Passed)
            {
                throw new InvalidOperationException("Driving smoke validation failed: " + string.Join(" | ", report.Failures));
            }
        }

        private static ManeuverResult RunManeuver(PrototypeGarageConfiguration configuration, string name, bool wet, bool rough, bool steer, DrivingReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Validation road", typeof(BoxCollider), typeof(SurfaceGrip));
            ground.transform.position = new Vector3(0f, -0.12f, 40f);
            ground.GetComponent<BoxCollider>().size = new Vector3(180f, 0.24f, 480f);
            ground.GetComponent<SurfaceGrip>().Configure(wet ? 0.82f : 1f, wet ? "Wet asphalt" : "Dry asphalt", wet ? 2.5f : 0f, wet ? 0.08f : 0f);
            if (rough)
            {
                var strip = new GameObject("Rough road", typeof(SurfaceGrip));
                strip.GetComponent<SurfaceGrip>().Configure(0.96f, "Rough asphalt", 0f, 0.65f);
                for (var index = 0; index < 18; index++)
                {
                    var height = index % 3 == 0 ? 0.034f : 0.022f;
                    var ridge = new GameObject("Measured roughness ridge " + index, typeof(BoxCollider));
                    ridge.transform.SetParent(strip.transform, false);
                    ridge.transform.position = new Vector3(index % 2 == 0 ? -0.7f : 0.65f, height * 0.5f, 2f + (index * 2.7f));
                    ridge.GetComponent<BoxCollider>().size = new Vector3(7.2f, height, 0.34f);
                }
            }

            var car = new GameObject("Validation Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, -8f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            ValidateReconfiguration(controller, configuration, report);
            var body = car.GetComponent<Rigidbody>();
            UnityEngine.Physics.SyncTransforms();
            name = configuration.WheelPackageName + " / " + name;
            var result = new ManeuverResult
            {
                Name = name,
                WheelPackageKey = configuration.WheelPackageKey,
                MassKilograms = body.mass,
                TireRadiusMeters = (float)configuration.Vehicle.Tire.UnloadedRadiusMeters,
                TireSectionWidthMeters = (float)configuration.Vehicle.Tire.SectionWidthMeters,
                NominalCenterOfMassHeightMeters = (float)configuration.Vehicle.CenterOfMassHeightMeters,
                MinimumObservedGrip = float.MaxValue
            };
            report.Maneuvers.Add(result);
            Advance(controller, body, 2f, new VehicleInputState(0f, 0f, 0f, 0f), null);
            var startPosition = body.position;
            Advance(controller, body, rough ? 7f : 4f, new VehicleInputState(rough ? 0.45f : 0.8f, 0f, 0f, 0f), result);
            result.SpeedAfterAccelerationMetersPerSecond = body.velocity.magnitude;
            result.ForwardDistanceMeters = body.position.z - startPosition.z;
            Check(result.SpeedAfterAccelerationMetersPerSecond > 2f, name + ": throttle must produce forward travel above 2 m/s.", report);
            Check(result.ForwardDistanceMeters > 2f, name + ": throttle must move forward at least 2 m.", report);

            if (steer)
            {
                var initialYaw = body.rotation.eulerAngles.y;
                var initialX = body.position.x;
                Advance(controller, body, 1.5f, new VehicleInputState(0.2f, 0f, 0.12f, 0f), result);
                result.SteeringYawChangeDegrees = Mathf.Abs(Mathf.DeltaAngle(initialYaw, body.rotation.eulerAngles.y));
                result.SteeringLateralDisplacementMeters = Mathf.Abs(body.position.x - initialX);
                Check(result.SteeringYawChangeDegrees > 1f && result.SteeringLateralDisplacementMeters > 0.1f,
                    name + ": steering must change heading and lateral position.", report);
            }

            result.SpeedBeforeBrakingMetersPerSecond = body.velocity.magnitude;
            Advance(controller, body, 4f, new VehicleInputState(0f, 1f, 0f, 0f), result);
            result.SpeedAfterBrakingMetersPerSecond = body.velocity.magnitude;
            Check(result.SpeedAfterBrakingMetersPerSecond < result.SpeedBeforeBrakingMetersPerSecond * 0.75f,
                name + ": four seconds of service braking must reduce speed by at least 25%.", report);
            Check(result.GroundedSamples > 0, name + ": tire contact must occur.", report);
            Check(result.UnknownSurfaceContactSamples == 0, name + ": tire rays must contact the road, never the vehicle's own body.", report);
            Check(result.MinimumUprightDot > 0.15f && result.MaximumHeightMeters < 4f && result.MinimumHeightMeters > -0.5f,
                name + ": car must stay upright and within bounded height.", report);
            if (rough)
            {
                Check(result.RoughContactSamples > 0, "Rough-road maneuver must cross a measured ridge.", report);
                Check(result.MaximumSuspensionCompressionMeters - result.MinimumSuspensionCompressionMeters > 0.003f,
                    "Rough-road suspension must respond to uneven geometry.", report);
            }

            return result;
        }

        private static void RunSteeringRelease(PrototypeGarageConfiguration configuration, float accelerationSeconds, float direction, DrivingReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Steering release validation road", typeof(BoxCollider), typeof(SurfaceGrip));
            ground.transform.position = new Vector3(0f, -0.12f, 40f);
            ground.GetComponent<BoxCollider>().size = new Vector3(180f, 0.24f, 480f);
            ground.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Steering release Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, -8f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var body = car.GetComponent<Rigidbody>();
            UnityEngine.Physics.SyncTransforms();
            var result = new ManeuverResult
            {
                Name = configuration.WheelPackageName + " / " + (accelerationSeconds < 3f ? "Moderate" : "Faster")
                       + " speed " + (direction < 0f ? "left" : "right") + " steering release",
                IsSteeringReleaseScenario = true,
                WheelPackageKey = configuration.WheelPackageKey,
                MassKilograms = body.mass,
                TireRadiusMeters = (float)configuration.Vehicle.Tire.UnloadedRadiusMeters,
                TireSectionWidthMeters = (float)configuration.Vehicle.Tire.SectionWidthMeters,
                NominalCenterOfMassHeightMeters = (float)configuration.Vehicle.CenterOfMassHeightMeters,
                MinimumObservedGrip = float.MaxValue,
                InputCenteringSeconds = -1f
            };
            report.Maneuvers.Add(result);
            Advance(controller, body, 2f, new VehicleInputState(0f, 0f, 0f, 0f), null);
            var startPosition = body.position;
            AdvanceKeyboard(driver, controller, body, accelerationSeconds, new VehicleInputState(0.8f, 0f, 0f, 0f), result);
            result.SpeedAfterAccelerationMetersPerSecond = body.velocity.magnitude;
            result.ForwardDistanceMeters = body.position.z - startPosition.z;
            AdvanceKeyboard(driver, controller, body, 0.35f, new VehicleInputState(0.2f, 0f, direction, 0f), result);
            result.PeakSteeringInput = Mathf.Abs(controller.Telemetry.Input.Steering);
            result.SpeedAtSteeringReleaseMetersPerSecond = body.velocity.magnitude;
            result.YawRateAtSteeringReleaseDegreesPerSecond = YawRateDegreesPerSecond(body);
            Check(result.PeakSteeringInput >= 0.99f, result.Name + ": holding a steering key must reach full steering before release.", report);

            const float renderStepSeconds = 1f / 60f;
            var physicsStepsPerFrame = Mathf.RoundToInt(renderStepSeconds / StepSeconds);
            var previousYaw = body.rotation.eulerAngles.y;
            for (var frame = 0; frame < 120; frame++)
            {
                driver.ApplyInput(new VehicleInputState(0.2f, 0f, 0f, 0f), renderStepSeconds);
                for (var step = 0; step < physicsStepsPerFrame; step++)
                {
                    AdvancePhysicsStep(controller, body, result);
                    var currentYaw = body.rotation.eulerAngles.y;
                    result.AdditionalHeadingAfterSteeringReleaseDegrees += Mathf.DeltaAngle(previousYaw, currentYaw);
                    previousYaw = currentYaw;
                }

                var elapsedSeconds = (frame + 1) * renderStepSeconds;
                var actualSteering = controller.Telemetry.Input.Steering;
                if (result.InputCenteringSeconds < 0f && Mathf.Abs(actualSteering) <= 0.0001f)
                {
                    result.InputCenteringSeconds = elapsedSeconds;
                }

                if (actualSteering * direction < -0.0001f)
                {
                    result.SteeringReversalSamples++;
                }

                if (frame == 8) result.YawRateAfterRelease150MillisecondsDegreesPerSecond = YawRateDegreesPerSecond(body);
                if (frame == 29) result.YawRateAfterRelease500MillisecondsDegreesPerSecond = YawRateDegreesPerSecond(body);
                if (frame == 59) result.YawRateAfterRelease1SecondDegreesPerSecond = YawRateDegreesPerSecond(body);
            }

            result.YawRateAfterRelease2SecondsDegreesPerSecond = YawRateDegreesPerSecond(body);
            Check(result.InputCenteringSeconds >= 0f && result.InputCenteringSeconds <= 0.10001f,
                result.Name + ": released steering must center within 100 ms at 60 Hz input sampling.", report);
            Check(result.SteeringReversalSamples == 0, result.Name + ": release must never command an opposite steering direction.", report);
            Check(Mathf.Abs(result.YawRateAfterRelease500MillisecondsDegreesPerSecond) < 1f,
                result.Name + ": continued turning must settle below 1 degree per second within 500 ms without braking.", report);
            Check(Mathf.Abs(result.YawRateAfterRelease1SecondDegreesPerSecond) < 1f,
                result.Name + ": heading must remain settled one second after steering release.", report);
            Check(result.GroundedSamples > 0 && result.UnknownSurfaceContactSamples == 0,
                result.Name + ": release maneuver must maintain valid road contact.", report);
            Check(result.MinimumUprightDot > 0.15f && result.MaximumHeightMeters < 4f && result.MinimumHeightMeters > -0.5f,
                result.Name + ": release maneuver must remain upright and within bounded height.", report);
        }

        private static float YawRateDegreesPerSecond(Rigidbody body)
        {
            return Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
        }

        private static void AdvanceKeyboard(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body,
            float seconds, VehicleInputState requestedInput, ManeuverResult result)
        {
            const float renderStepSeconds = 1f / 60f;
            var physicsStepsPerFrame = Mathf.RoundToInt(renderStepSeconds / StepSeconds);
            var frames = Mathf.RoundToInt(seconds / renderStepSeconds);
            for (var frame = 0; frame < frames; frame++)
            {
                driver.ApplyInput(requestedInput, renderStepSeconds);
                for (var step = 0; step < physicsStepsPerFrame; step++)
                {
                    AdvancePhysicsStep(controller, body, result);
                }
            }
        }

        private static void Advance(RaycastVehicleController controller, Rigidbody body, float seconds, VehicleInputState input, ManeuverResult result)
        {
            controller.SetInput(input);
            var steps = Mathf.RoundToInt(seconds / StepSeconds);
            for (var step = 0; step < steps; step++)
            {
                AdvancePhysicsStep(controller, body, result);
            }
        }

        private static void AdvancePhysicsStep(RaycastVehicleController controller, Rigidbody body, ManeuverResult result)
        {
            var startTicks = Stopwatch.GetTimestamp();
            controller.SimulateStep(StepSeconds);
            UnityEngine.Physics.Simulate(StepSeconds);
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            var speed = body.velocity.magnitude;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed > 120f || body.angularVelocity.magnitude > 30f
                || !IsFinite(body.position) || !IsFinite(body.angularVelocity))
            {
                throw new InvalidOperationException("Physics produced nonfinite or unbounded motion.");
            }

            if (result == null)
            {
                return;
            }

            result.PhysicsSteps++;
            result.TotalSimulationTicks += elapsedTicks;
            result.MeanControllerAndPhysicsStepMicroseconds = result.TotalSimulationTicks * 1000000d / Stopwatch.Frequency / result.PhysicsSteps;
            result.MaximumSpeedMetersPerSecond = Mathf.Max(result.MaximumSpeedMetersPerSecond, speed);
            result.MaximumHeightMeters = Mathf.Max(result.MaximumHeightMeters, body.position.y);
            result.MinimumHeightMeters = Mathf.Min(result.MinimumHeightMeters, body.position.y);
            result.MinimumUprightDot = Mathf.Min(result.MinimumUprightDot, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
            foreach (var wheel in controller.Telemetry.Wheels)
            {
                if (!wheel.Grounded)
                {
                    continue;
                }

                result.GroundedSamples++;
                result.RoughContactSamples += wheel.SurfaceName == "Rough asphalt" ? 1 : 0;
                result.UnknownSurfaceContactSamples += wheel.SurfaceName != "Dry asphalt" && wheel.SurfaceName != "Wet asphalt"
                                                       && wheel.SurfaceName != "Rough asphalt" ? 1 : 0;
                result.MinimumObservedGrip = Mathf.Min(result.MinimumObservedGrip, wheel.SurfaceGripMultiplier);
                result.MaximumObservedWaterFilmMillimeters = Mathf.Max(result.MaximumObservedWaterFilmMillimeters, wheel.WaterFilmDepthMillimeters);
                result.MinimumSuspensionCompressionMeters = Mathf.Min(result.MinimumSuspensionCompressionMeters, wheel.SuspensionCompressionMeters);
                result.MaximumSuspensionCompressionMeters = Mathf.Max(result.MaximumSuspensionCompressionMeters, wheel.SuspensionCompressionMeters);
            }
        }

        private static void ValidateReconfiguration(RaycastVehicleController controller, PrototypeGarageConfiguration requestedConfiguration, DrivingReport report)
        {
            var body = controller.GetComponent<Rigidbody>();
            var meshes = WheelMeshes(controller);
            var initialChildCount = controller.transform.childCount;
            var firstMesh = meshes[0].sharedMesh;
            var firstMaterial = meshes[0].GetComponent<Renderer>().sharedMaterial;
            var stock = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle;
            var touringState = PrototypeGarageCatalog.CreateNewState();
            PrototypeGarageCatalog.TryInstallCompanionKit(touringState, out _);
            PrototypeGarageCatalog.TryInstallWheelPackage(touringState, PrototypeGarageCatalog.TouringKey, out _);
            var wider = PrototypeGarageCatalog.Compile(touringState).Vehicle;
            body.velocity = new Vector3(0f, 0f, 9f);
            body.angularVelocity = Vector3.up;
            controller.SetInput(new VehicleInputState(1f, 0f, 0.5f, 0f));
            controller.Configure(wider);
            controller.Configure(wider);
            meshes = WheelMeshes(controller);
            Check(meshes.Count == 4 && controller.transform.childCount == initialChildCount, "Reconfiguration must reuse exactly four wheel visuals.", report);
            foreach (var filter in meshes)
            {
                Check(filter.sharedMesh == firstMesh && filter.GetComponent<Renderer>().sharedMaterial == firstMaterial,
                    "Reconfiguration must reuse the owned tire mesh and rubber material.", report);
                Check(filter.transform.localScale == Vector3.one && filter.transform.parent.localScale == Vector3.one,
                    "Wheel dimensions must be baked into geometry at unit transform scale.", report);
                var size = filter.sharedMesh.bounds.size;
                Check(Mathf.Abs(size.x - (float)(wider.Tire.UnloadedRadiusMeters * 2d)) < 0.0001f
                      && Mathf.Abs(size.y - (float)wider.Tire.SectionWidthMeters) < 0.0001f,
                    "Wheel mesh must match selected tire radius and section width.", report);
            }

            Check(Mathf.Abs(body.mass - (float)wider.MassKilograms) < 0.0001f,
                "Controller mass must equal the touring installed-manifest mass.", report);
            CheckCenterOfMassDatum(controller, report);
            Check(body.velocity == Vector3.zero && body.angularVelocity == Vector3.zero
                  && controller.Telemetry.ForwardGear == 1 && controller.Telemetry.Input.Throttle == 0f,
                "Reconfiguration must reset drivetrain, controls and physical motion.", report);
            controller.SimulationPaused = true;
            controller.SetInput(new VehicleInputState(1f, 0f, 0f, 0f));
            controller.SimulateStep(StepSeconds);
            Check(controller.Telemetry.EngineSpeedRpm == (float)wider.Engine.IdleSpeedRpm,
                "Paused vehicle simulation must not advance the engine.", report);
            controller.SimulationPaused = false;
            controller.Configure(stock);
            var stockSize = firstMesh.bounds.size;
            Check(Mathf.Abs(stockSize.x - (float)(stock.Tire.UnloadedRadiusMeters * 2d)) < 0.0001f
                  && Mathf.Abs(stockSize.y - (float)stock.Tire.SectionWidthMeters) < 0.0001f
                  && Mathf.Abs(body.mass - (float)stock.MassKilograms) < 0.0001f,
                "Returning to stock must restore tire geometry and installed-manifest mass.", report);
            CheckCenterOfMassDatum(controller, report);
            controller.Configure(requestedConfiguration.Vehicle);
        }

        private static void CheckCenterOfMassDatum(RaycastVehicleController controller, DrivingReport report)
        {
            var definition = controller.Definition;
            var suspensionLength = Mathf.Clamp((float)(definition.Suspension.RestLengthMeters
                    - definition.MassKilograms * 9.80665d / (4d * definition.Suspension.SpringRateNewtonsPerMeter)),
                0f, (float)(definition.Suspension.RestLengthMeters + definition.Suspension.TravelMeters));
            var nominalDatum = (float)definition.Tire.UnloadedRadiusMeters + suspensionLength;
            Check(Mathf.Abs(nominalDatum + controller.GetComponent<Rigidbody>().centerOfMass.y
                            - (float)definition.CenterOfMassHeightMeters) < 0.0001f,
                "Rigidbody CG at nominal loaded ride height must match the above-ground definition height.", report);
        }

        private static List<MeshFilter> WheelMeshes(RaycastVehicleController controller)
        {
            var result = new List<MeshFilter>();
            foreach (var filter in controller.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name == "Tire" && filter.transform.parent.parent == controller.transform)
                {
                    result.Add(filter);
                }
            }

            return result;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                   && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void Check(bool condition, string message, DrivingReport report)
        {
            report.Assertions++;
            if (!condition)
            {
                report.Failures.Add(message);
            }
        }

        [Serializable]
        private sealed class DrivingReport
        {
            public bool Passed;
            public string GeneratedUtc;
            public string UnityVersion;
            public int SimulationHertz;
            public int Assertions;
            public string TimingScope = "Mean controller + Unity Physics.Simulate time on this machine; no rendering, UI or hardware performance guarantee.";
            public List<ManeuverResult> Maneuvers = new List<ManeuverResult>();
            public List<string> Failures = new List<string>();
        }

        [Serializable]
        private sealed class ManeuverResult
        {
            public string Name;
            public string WheelPackageKey;
            public float MassKilograms;
            public float TireRadiusMeters;
            public float TireSectionWidthMeters;
            public float NominalCenterOfMassHeightMeters;
            public int PhysicsSteps;
            public int GroundedSamples;
            public int RoughContactSamples;
            public int UnknownSurfaceContactSamples;
            public float SpeedAfterAccelerationMetersPerSecond;
            public float SpeedBeforeBrakingMetersPerSecond;
            public float SpeedAfterBrakingMetersPerSecond;
            public float ForwardDistanceMeters;
            public float MaximumSpeedMetersPerSecond;
            public float SteeringYawChangeDegrees;
            public float SteeringLateralDisplacementMeters;
            public bool IsSteeringReleaseScenario;
            public float PeakSteeringInput;
            public float SpeedAtSteeringReleaseMetersPerSecond;
            public float InputCenteringSeconds;
            public int SteeringReversalSamples;
            public float YawRateAtSteeringReleaseDegreesPerSecond;
            public float YawRateAfterRelease150MillisecondsDegreesPerSecond;
            public float YawRateAfterRelease500MillisecondsDegreesPerSecond;
            public float YawRateAfterRelease1SecondDegreesPerSecond;
            public float YawRateAfterRelease2SecondsDegreesPerSecond;
            public float AdditionalHeadingAfterSteeringReleaseDegrees;
            public float MinimumObservedGrip;
            public float MaximumObservedWaterFilmMillimeters;
            public float MinimumSuspensionCompressionMeters = float.MaxValue;
            public float MaximumSuspensionCompressionMeters;
            public float MinimumUprightDot = 1f;
            public float MinimumHeightMeters = float.MaxValue;
            public float MaximumHeightMeters = float.MinValue;
            public double MeanControllerAndPhysicsStepMicroseconds;
            [NonSerialized] public long TotalSimulationTicks;
        }
    }
}
