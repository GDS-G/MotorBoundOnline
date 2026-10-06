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

namespace MotorBound.Editor
{
    // Reproduce cornering through the live keyboard path, starting with physically rolling wheels.
    public static class PrototypeCorneringValidation
    {
        private const float PhysicsStep = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;
        private const float InputStep = 1f / 60f;
        private const float TurnDuration = 1.2f;

        [MenuItem("MotorBound/Validate Cornering Physics")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run cornering validation outside Play mode.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified scenes before running cornering validation.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousMode = UnityEngine.Physics.simulationMode;
            var previousGravity = UnityEngine.Physics.gravity;
            var previousIterations = UnityEngine.Physics.defaultSolverIterations;
            var previousVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations;
            var report = new CorneringReport
            {
                GeneratedUtc = DateTime.UtcNow.ToString("o"),
                UnityVersion = Application.unityVersion,
                SimulationHertz = RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz
            };
            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);
                UnityEngine.Physics.defaultSolverIterations = 12;
                UnityEngine.Physics.defaultSolverVelocityIterations = 4;
                var stock = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState());
                foreach (var speed in new[] { 30f, 45f, 50f, 55f, 70f })
                {
                    RunManeuver(stock, speed, 0.12f, 0.2f, 0f, "gentle", report);
                    RunManeuver(stock, speed, 1f, 0.2f, 0f, "hard", report);
                }
                foreach (var speed in new[] { 55f, 70f })
                {
                    RunManeuver(stock, speed, 1f, 0f, 0f, "coasting hard", report);
                    RunManeuver(stock, speed, 1f, speed == 55f ? 0.8f : 1f, 0f, "power hard", report);
                    RunManeuver(stock, speed, 1f, 0.2f, 1f, "handbrake hard", report);
                }
                RunManeuver(stock, 50f, -1f, 0.2f, 0f, "left hard mirror", report);
                var touringState = PrototypeGarageCatalog.CreateNewState();
                if (!PrototypeGarageCatalog.TryInstallCompanionKit(touringState, out var kitMessage)
                    || !PrototypeGarageCatalog.TryInstallWheelPackage(touringState, PrototypeGarageCatalog.TouringKey, out var packageMessage))
                    throw new InvalidOperationException("Cannot assemble touring wheels: " + kitMessage);
                var touring = PrototypeGarageCatalog.Compile(touringState);
                RunManeuver(touring, 55f, 0.12f, 0.2f, 0f, "gentle", report);
                RunManeuver(touring, 55f, 1f, 0.2f, 0f, "hard", report);
                RunManeuver(touring, 70f, 1f, 0.2f, 1f, "handbrake hard", report);
                RunManeuver(stock, 55f, 1f, 0.2f, 1f, "brief handbrake and countersteer recovery", report, true);
                RunManeuver(stock, 70f, 1f, 0.2f, 1f, "brief handbrake and countersteer recovery", report, true);
                foreach (var routineConfiguration in new[] { stock, touring })
                {
                    foreach (var speed in new[] { 30f, 50f, 70f })
                    {
                        RunRoutineManeuver(routineConfiguration, speed, "80 ms right key tap with throttle", 1f,
                            new[] { new InputSegment(0.08f, 1f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "150 ms left key tap with throttle", 1f,
                            new[] { new InputSegment(0.15f, -1f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "gentle 0.12 right with throttle", 1f,
                            new[] { new InputSegment(0.8f, 0.12f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "mild 0.06 right with throttle", 1f,
                            new[] { new InputSegment(0.8f, 0.06f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "gentle 0.20 left with throttle", 1f,
                            new[] { new InputSegment(0.8f, -0.2f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "150 ms right key tap coasting", 0f,
                            new[] { new InputSegment(0.15f, 1f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "keyboard right-left lane change with throttle", 1f,
                            new[] { new InputSegment(0.15f, 1f), new InputSegment(0.2f, 0f), new InputSegment(0.15f, -1f) }, report);
                        RunRoutineManeuver(routineConfiguration, speed, "gentle alternating slalom with throttle", 1f,
                            new[] { new InputSegment(0.4f, 0.12f), new InputSegment(0.8f, -0.12f), new InputSegment(0.4f, 0.12f) }, report);
                    }
                }
            }
            catch (Exception exception)
            {
                report.Failures.Add(exception.ToString());
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previousMode;
                UnityEngine.Physics.gravity = previousGravity;
                UnityEngine.Physics.defaultSolverIterations = previousIterations;
                UnityEngine.Physics.defaultSolverVelocityIterations = previousVelocityIterations;
                var savedScene = false;
                foreach (var item in setup) savedScene |= !string.IsNullOrEmpty(item.path);
                if (savedScene) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                report.Passed = report.Failures.Count == 0;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts"));
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "cornering-validation.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log("MotorBound cornering validation: " + path + "; passed=" + report.Passed);
            }
            if (!report.Passed)
                throw new InvalidOperationException("Cornering validation failed: " + string.Join(" | ", report.Failures));
        }

        private static void RunManeuver(PrototypeGarageConfiguration configuration, float targetSpeedKmh,
            float steering, float throttle, float handbrake, string label, CorneringReport report, bool recover = false)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Cornering dry asphalt", typeof(BoxCollider), typeof(SurfaceGrip));
            ground.transform.position = new Vector3(0f, -0.12f, 0f);
            ground.GetComponent<BoxCollider>().size = new Vector3(3000f, 0.24f, 3000f);
            ground.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Cornering Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, 0f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            controller.RoadTractionControlEnabled = false;
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var body = car.GetComponent<Rigidbody>();
            var steeringPivot = car.transform.Find("Front Left Visual");
            UnityEngine.Physics.SyncTransforms();

            // Accelerating through the normal engine and tire solver avoids a mismatched wheel spin state.
            for (var frame = 0; frame < 120; frame++)
                AdvanceFrame(driver, controller, body, default(VehicleInputState), null, 0f);
            var accelerationFrames = 0;
            while (body.velocity.magnitude * 3.6f < targetSpeedKmh && accelerationFrames < 60 * 30)
            {
                AdvanceFrame(driver, controller, body, new VehicleInputState(1f, 0f, 0f, 0f), null, 0f);
                accelerationFrames++;
            }

            var result = new ManeuverResult
            {
                Name = configuration.WheelPackageName + " / " + targetSpeedKmh + " km/h / " + label,
                WheelPackageKey = configuration.WheelPackageKey,
                RequestedSpeedKilometersPerHour = targetSpeedKmh,
                SpeedAtTurnKilometersPerHour = body.velocity.magnitude * 3.6f,
                AccelerationSeconds = accelerationFrames * InputStep,
                RequestedSteering = steering,
                RequestedThrottle = throttle,
                RequestedHandbrake = handbrake,
                IsRecoveryManeuver = recover,
                RoadTractionControlEnabled = controller.RoadTractionControlEnabled,
                SteeringHoldSeconds = recover ? 0.2f : TurnDuration,
                AuthoredMaximumSteeringDegrees = (float)configuration.Vehicle.MaximumSteeringAngleDegrees
            };
            report.Maneuvers.Add(result);
            // Start marks only after the straight acceleration so the count belongs to this corner.
            var skidTrails = car.AddComponent<PrototypeSkidTrails>();
            skidTrails.Configure(controller);
            Check(result.SpeedAtTurnKilometersPerHour >= targetSpeedKmh
                  && result.SpeedAtTurnKilometersPerHour <= targetSpeedKmh + 1f,
                result.Name + ": live drivetrain must reach requested initial speed within 1 km/h.", report);
            var yawStart = body.rotation.eulerAngles.y;
            var start = body.position;
            var previousYaw = yawStart;
            var turnInput = new VehicleInputState(throttle, 0f, steering, handbrake);
            var frames = Mathf.RoundToInt(result.SteeringHoldSeconds / InputStep);
            for (var frame = 0; frame < frames; frame++)
            {
                AdvanceFrame(driver, controller, body, turnInput, result, (frame + 1) * InputStep);
                skidTrails.SampleContacts();
                result.HeadingChangeDegrees += Mathf.DeltaAngle(previousYaw, body.rotation.eulerAngles.y);
                previousYaw = body.rotation.eulerAngles.y;
                result.PeakAppliedSteeringDegrees = Mathf.Max(result.PeakAppliedSteeringDegrees,
                    Mathf.Abs(Mathf.DeltaAngle(0f, steeringPivot.localEulerAngles.y)));
                if (frame == 17)
                {
                    result.SteeringInputAfter300Milliseconds = controller.Telemetry.Input.Steering;
                    result.AppliedSteeringDegreesAfter300Milliseconds = Mathf.DeltaAngle(0f, steeringPivot.localEulerAngles.y);
                    result.SpeedAfter300MillisecondsKilometersPerHour = body.velocity.magnitude * 3.6f;
                }
                if (frame == 29)
                {
                    result.SteeringInputAfter500Milliseconds = controller.Telemetry.Input.Steering;
                    result.AppliedSteeringDegreesAfter500Milliseconds = Mathf.DeltaAngle(0f, steeringPivot.localEulerAngles.y);
                }
            }
            result.SpeedAfterTurnKilometersPerHour = body.velocity.magnitude * 3.6f;
            result.SkidMarkSegmentCount = skidTrails.SegmentCount;
            result.SpeedLossKilometersPerHour = result.SpeedAtTurnKilometersPerHour - result.SpeedAfterTurnKilometersPerHour;
            result.LateralDisplacementMeters = body.position.x - start.x;
            result.TravelDistanceMeters = Vector3.Distance(start, body.position);
            result.FinalBodySideslipDegrees = SideslipDegrees(body);
            result.FinalYawRateDegreesPerSecond = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
            result.MeanFrontForceEnvelopeUtilization = (float)(result.FrontUtilizationSum / Math.Max(1, result.FrontGroundedSamples));
            result.MeanRearForceEnvelopeUtilization = (float)(result.RearUtilizationSum / Math.Max(1, result.RearGroundedSamples));
            result.MeanLateFrontSlipAngleDegrees = (float)(result.LateFrontSlipSum / Math.Max(1, result.LateFrontSamples));
            result.MeanLateRearSlipAngleDegrees = (float)(result.LateRearSlipSum / Math.Max(1, result.LateRearSamples));
            result.Classification = Classify(result);
            if (recover)
                RunRecovery(driver, controller, body, skidTrails, result, report);
            else
            {
                var expectedProgressiveSteering = Mathf.Sign(steering) * Mathf.Min(Mathf.Abs(steering), 0.6f);
                Check(Mathf.Abs(result.SteeringInputAfter300Milliseconds - expectedProgressiveSteering) < 0.0001f,
                    result.Name + ": keyboard steering must progress at two full-input units per second.", report);
                Check(Mathf.Abs(result.SteeringInputAfter500Milliseconds - steering) < 0.0001f,
                    result.Name + ": live keyboard response must reach requested steering without a speed limiter.", report);
                Check(Mathf.Abs(Mathf.Abs(result.AppliedSteeringDegreesAfter500Milliseconds)
                                - Mathf.Abs(steering) * result.AuthoredMaximumSteeringDegrees) < 0.001f,
                    result.Name + ": front wheel angle must preserve the authored steering range at speed.", report);
            }
            Check(result.FrontGroundedSamples > 0 && result.RearGroundedSamples > 0
                  && result.UnknownSurfaceSamples == 0,
                result.Name + ": both axles must contact only the validation road.", report);
            Check(result.MinimumUprightDot > 0.15f && result.MinimumHeightMeters > -0.5f
                  && result.MaximumHeightMeters < 4f,
                result.Name + ": car must remain upright with bounded suspension height.", report);
            Check(result.PeakFrontForceEnvelopeUtilization <= 1.001f && result.PeakRearForceEnvelopeUtilization <= 1.001f,
                result.Name + ": delivered tire forces must remain within the measured friction envelope.", report);
            if (Mathf.Abs(steering) >= 0.99f && result.FrontPostPeakSamples + result.RearPostPeakSamples > 0)
                Check(result.SkidMarkSegmentCount > 0,
                    result.Name + ": measured sliding tires must leave visible marks during the hard turn.", report);
            if (Mathf.Abs(steering) <= 0.12f && targetSpeedKmh <= 45f
                && result.FrontPostPeakSamples + result.RearPostPeakSamples == 0)
                Check(result.SkidMarkSegmentCount == 0,
                    result.Name + ": ordinary subpeak cornering must not leave sliding tire marks.", report);
        }

        private static void RunRecovery(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body,
            PrototypeSkidTrails skidTrails, ManeuverResult result, CorneringReport report)
        {
            result.BodySideslipAtHandbrakeReleaseDegrees = SideslipDegrees(body);
            result.YawRateAtHandbrakeReleaseDegreesPerSecond = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
            var previousYaw = body.rotation.eulerAngles.y;
            for (var frame = 0; frame < 120; frame++)
            {
                var sideslip = SideslipDegrees(body);
                // Aim the front tires toward actual travel, then ease them straight as the body aligns.
                var countersteering = Mathf.Abs(sideslip) > 1.5f
                    ? Mathf.Clamp(sideslip / result.AuthoredMaximumSteeringDegrees, -1f, 1f)
                    : 0f;
                if (Mathf.Abs(countersteering) > 0.0001f) result.CountersteeringFrames++;
                if (countersteering * result.RequestedSteering < 0f) result.OppositeSteeringFrames++;
                result.PeakRecoveryRequestedSteering = Mathf.Max(result.PeakRecoveryRequestedSteering, Mathf.Abs(countersteering));
                AdvanceFrame(driver, controller, body, new VehicleInputState(0.2f, 0f, countersteering, 0f), result,
                    result.SteeringHoldSeconds + (frame + 1) * InputStep);
                skidTrails.SampleContacts();
                result.HeadingChangeDuringRecoveryDegrees += Mathf.DeltaAngle(previousYaw, body.rotation.eulerAngles.y);
                previousYaw = body.rotation.eulerAngles.y;
                result.PeakRecoveryYawRateDegreesPerSecond = Mathf.Max(result.PeakRecoveryYawRateDegreesPerSecond,
                    Mathf.Abs(Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg));
                result.MaximumHandbrakeInputAfterRelease = Mathf.Max(result.MaximumHandbrakeInputAfterRelease,
                    controller.Telemetry.Input.Handbrake);
                for (var wheelIndex = 2; wheelIndex < controller.Telemetry.Wheels.Length; wheelIndex++)
                {
                    var wheel = controller.Telemetry.Wheels[wheelIndex];
                    if (wheel.Grounded && wheel.NormalLoadNewtons > 20f && frame >= 60)
                        result.PeakRearLongitudinalSlipRatioLastRecoverySecond = Mathf.Max(
                            result.PeakRearLongitudinalSlipRatioLastRecoverySecond,
                            Mathf.Abs(wheel.SlipRatio));
                }
            }
            result.BodySideslipAfterRecoveryDegrees = SideslipDegrees(body);
            result.YawRateAfterRecoveryDegreesPerSecond = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
            result.SpeedAfterRecoveryKilometersPerHour = body.velocity.magnitude * 3.6f;
            result.SkidMarkSegmentCount = skidTrails.SegmentCount;
            result.MeanLateFrontSlipAngleDegrees = (float)(result.LateFrontSlipSum / Math.Max(1, result.LateFrontSamples));
            result.MeanLateRearSlipAngleDegrees = (float)(result.LateRearSlipSum / Math.Max(1, result.LateRearSamples));
            result.MeanFrontForceEnvelopeUtilization = (float)(result.FrontUtilizationSum / Math.Max(1, result.FrontGroundedSamples));
            result.MeanRearForceEnvelopeUtilization = (float)(result.RearUtilizationSum / Math.Max(1, result.RearGroundedSamples));
            result.Classification = Classify(result);
            result.FinalRearLongitudinalSlipRatio = (Mathf.Abs(controller.Telemetry.Wheels[2].SlipRatio)
                                                    + Mathf.Abs(controller.Telemetry.Wheels[3].SlipRatio)) * 0.5f;
            Check(result.MaximumHandbrakeInputAfterRelease == 0f,
                result.Name + ": releasing the handbrake must remove rear brake command on every recovery frame.", report);
            // Observe recoverability without requiring yaw to vanish after a physical skid.
            Check(result.PeakRecoveryYawRateDegreesPerSecond < 360f,
                result.Name + ": brief handbrake release and countersteer must avoid an excessive spin.", report);
            Check(Mathf.Abs(result.YawRateAfterRecoveryDegreesPerSecond) < 5f
                  && Mathf.Abs(result.BodySideslipAfterRecoveryDegrees) < 3f,
                result.Name + ": countersteer must settle heading and sideslip after the brief handbrake skid.", report);
            Check(result.OppositeSteeringFrames > 0,
                result.Name + ": recovery must include opposite steering toward the measured travel direction.", report);
            Check(result.FinalRearLongitudinalSlipRatio < 0.1f
                  && result.PeakRearLongitudinalSlipRatioLastRecoverySecond < 0.1f,
                result.Name + ": rear tires must resume rolling after the handbrake is released.", report);
            Check(result.SpeedAfterRecoveryKilometersPerHour > result.SpeedAtTurnKilometersPerHour * 0.5f,
                result.Name + ": recovery must preserve forward travel rather than settle by stopping the car.", report);
        }

        private static void RunRoutineManeuver(PrototypeGarageConfiguration configuration, float targetSpeedKmh,
            string label, float throttle, InputSegment[] segments, CorneringReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Routine handling dry asphalt", typeof(BoxCollider), typeof(SurfaceGrip));
            ground.transform.position = new Vector3(0f, -0.12f, 0f);
            ground.GetComponent<BoxCollider>().size = new Vector3(3000f, 0.24f, 3000f);
            ground.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Routine handling Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, 0f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            controller.RoadTractionControlEnabled = true;
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var body = car.GetComponent<Rigidbody>();
            var steeringPivot = car.transform.Find("Front Left Visual");
            UnityEngine.Physics.SyncTransforms();
            for (var frame = 0; frame < 120; frame++)
                AdvanceFrame(driver, controller, body, default(VehicleInputState), null, 0f);
            var accelerationFrames = 0;
            while (body.velocity.magnitude * 3.6f < targetSpeedKmh && accelerationFrames < 60 * 30)
            {
                AdvanceFrame(driver, controller, body, new VehicleInputState(1f, 0f, 0f, 0f), null, 0f);
                accelerationFrames++;
            }
            var result = new ManeuverResult
            {
                Name = configuration.WheelPackageName + " / " + targetSpeedKmh + " km/h / " + label,
                WheelPackageKey = configuration.WheelPackageKey,
                RequestedSpeedKilometersPerHour = targetSpeedKmh,
                SpeedAtTurnKilometersPerHour = body.velocity.magnitude * 3.6f,
                AccelerationSeconds = accelerationFrames * InputStep,
                RequestedThrottle = throttle,
                AuthoredMaximumSteeringDegrees = (float)configuration.Vehicle.MaximumSteeringAngleDegrees,
                IsRoutineHandlingManeuver = true,
                RoadTractionControlEnabled = controller.RoadTractionControlEnabled,
                RoutineInputSegments = segments,
                InputCenteringAfterFinalReleaseSeconds = -1f,
                YawSettlingAfterFinalReleaseSeconds = -1f
            };
            report.RoutineHandling.Add(result);
            var trails = car.AddComponent<PrototypeSkidTrails>();
            trails.Configure(controller);
            var start = body.position;
            var previousYaw = body.rotation.eulerAngles.y;
            var elapsedFrames = 0;
            foreach (var segment in segments)
            {
                var segmentFrames = Mathf.RoundToInt(segment.Seconds / InputStep);
                result.SteeringHoldSeconds += segmentFrames * InputStep;
                result.RequestedSteering = Mathf.Max(result.RequestedSteering, Mathf.Abs(segment.Steering));
                for (var frame = 0; frame < segmentFrames; frame++)
                {
                    elapsedFrames++;
                    AdvanceFrame(driver, controller, body, new VehicleInputState(throttle, 0f, segment.Steering, 0f),
                        result, elapsedFrames * InputStep);
                    trails.SampleContacts();
                    RecordHeadingAndSteering(body, steeringPivot, result, ref previousYaw);
                }
            }
            result.SpeedAtFinalSteeringReleaseKilometersPerHour = body.velocity.magnitude * 3.6f;
            result.BodySideslipAtFinalSteeringReleaseDegrees = SideslipDegrees(body);
            result.YawRateAtFinalSteeringReleaseDegreesPerSecond = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
            var finalReleaseYaw = body.rotation.eulerAngles.y;
            var settledFrames = 0;
            for (var frame = 0; frame < 90; frame++)
            {
                elapsedFrames++;
                AdvanceFrame(driver, controller, body, new VehicleInputState(throttle, 0f, 0f, 0f), result,
                    elapsedFrames * InputStep);
                trails.SampleContacts();
                var yawBeforeReleaseFrame = previousYaw;
                RecordHeadingAndSteering(body, steeringPivot, result, ref previousYaw);
                result.AccumulatedAdditionalHeadingAfterFinalReleaseDegrees += Mathf.DeltaAngle(yawBeforeReleaseFrame, previousYaw);
                var releaseSeconds = (frame + 1) * InputStep;
                var yawRate = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
                var sideslip = SideslipDegrees(body);
                if (result.InputCenteringAfterFinalReleaseSeconds < 0f
                    && Mathf.Abs(controller.Telemetry.Input.Steering) <= 0.0001f)
                    result.InputCenteringAfterFinalReleaseSeconds = releaseSeconds;
                if (Mathf.Abs(yawRate) < 1f && Mathf.Abs(sideslip) < 1f) settledFrames++;
                else settledFrames = 0;
                if (settledFrames >= 12 && result.YawSettlingAfterFinalReleaseSeconds < 0f)
                    result.YawSettlingAfterFinalReleaseSeconds = releaseSeconds - 11f * InputStep;
                result.PeakBodySideslipAfterFinalReleaseDegrees = Mathf.Max(result.PeakBodySideslipAfterFinalReleaseDegrees,
                    Mathf.Abs(sideslip));
                if (frame == 8) result.YawRateAfterFinalRelease150MillisecondsDegreesPerSecond = yawRate;
                if (frame == 29) result.YawRateAfterFinalRelease500MillisecondsDegreesPerSecond = yawRate;
                if (frame == 59) result.YawRateAfterFinalRelease1SecondDegreesPerSecond = yawRate;
            }
            result.AdditionalHeadingAfterFinalReleaseDegrees = Mathf.DeltaAngle(finalReleaseYaw, body.rotation.eulerAngles.y);
            result.SpeedAfterTurnKilometersPerHour = body.velocity.magnitude * 3.6f;
            result.SpeedLossKilometersPerHour = result.SpeedAtTurnKilometersPerHour - result.SpeedAfterTurnKilometersPerHour;
            result.LateralDisplacementMeters = body.position.x - start.x;
            result.TravelDistanceMeters = Vector3.Distance(start, body.position);
            result.FinalBodySideslipDegrees = SideslipDegrees(body);
            result.FinalYawRateDegreesPerSecond = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
            result.FinalLateralAccelerationMetersPerSecondSquared = controller.Telemetry.LocalAccelerationMetersPerSecondSquared.x;
            result.SkidMarkSegmentCount = trails.SegmentCount;
            result.MeanFrontForceEnvelopeUtilization = (float)(result.FrontUtilizationSum / Math.Max(1, result.FrontGroundedSamples));
            result.MeanRearForceEnvelopeUtilization = (float)(result.RearUtilizationSum / Math.Max(1, result.RearGroundedSamples));
            result.MeanLateFrontSlipAngleDegrees = (float)(result.LateFrontSlipSum / Math.Max(1, result.LateFrontSamples));
            result.MeanLateRearSlipAngleDegrees = (float)(result.LateRearSlipSum / Math.Max(1, result.LateRearSamples));
            result.Classification = Classify(result);
            Check(result.SpeedAtTurnKilometersPerHour >= targetSpeedKmh
                  && result.SpeedAtTurnKilometersPerHour <= targetSpeedKmh + 1f,
                result.Name + ": routine maneuver must start at the authored test speed.", report);
            Check(result.FrontGroundedSamples > 0 && result.RearGroundedSamples > 0 && result.UnknownSurfaceSamples == 0,
                result.Name + ": routine steering must maintain valid road contact.", report);
            Check(result.MinimumUprightDot > 0.15f && result.MinimumHeightMeters > -0.5f && result.MaximumHeightMeters < 4f,
                result.Name + ": routine steering must remain upright and within bounded height.", report);
            Check(result.PeakFrontForceEnvelopeUtilization <= 1.001f && result.PeakRearForceEnvelopeUtilization <= 1.001f,
                result.Name + ": routine delivered tire forces must stay inside the available grip envelope.", report);
            Check(result.InputCenteringAfterFinalReleaseSeconds >= 0f
                  && result.InputCenteringAfterFinalReleaseSeconds <= 0.10001f,
                result.Name + ": released steering must center within 100 milliseconds.", report);
            Check(result.PeakBodySideslipDegrees < 10f,
                result.Name + ": Road traction control must prevent a large unintended body slide during ordinary steering.", report);
            var smallCorrection = label.Contains("key tap") || label.Contains("lane change") || label.Contains("mild 0.06");
            if (smallCorrection)
                Check(Mathf.Abs(result.FinalYawRateDegreesPerSecond) < 5f
                      && Mathf.Abs(result.FinalBodySideslipDegrees) < 3f
                      && Mathf.Abs(result.YawRateAfterFinalRelease500MillisecondsDegreesPerSecond) < 6f,
                    result.Name + ": small steering corrections must settle without an unintended spin.", report);
            if (label.Contains("mild 0.06"))
                Check(result.YawSettlingAfterFinalReleaseSeconds >= 0f
                      && result.YawSettlingAfterFinalReleaseSeconds < 1.5f,
                    result.Name + ": mild steering must regain stable straight travel within 1.5 seconds of release.", report);
        }

        private static void RecordHeadingAndSteering(Rigidbody body, Transform steeringPivot, ManeuverResult result, ref float previousYaw)
        {
            result.HeadingChangeDegrees += Mathf.DeltaAngle(previousYaw, body.rotation.eulerAngles.y);
            previousYaw = body.rotation.eulerAngles.y;
            result.PeakAppliedSteeringDegrees = Mathf.Max(result.PeakAppliedSteeringDegrees,
                Mathf.Abs(Mathf.DeltaAngle(0f, steeringPivot.localEulerAngles.y)));
        }

        private static void AdvanceFrame(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body,
            VehicleInputState input, ManeuverResult result, float elapsedTurnSeconds)
        {
            driver.ApplyInput(input, InputStep);
            var substeps = Mathf.RoundToInt(InputStep / PhysicsStep);
            for (var index = 0; index < substeps; index++)
            {
                controller.SimulateStep(PhysicsStep);
                UnityEngine.Physics.Simulate(PhysicsStep);
                var speed = body.velocity.magnitude;
                if (!Finite(body.position) || !Finite(body.velocity) || !Finite(body.angularVelocity)
                    || speed > 120f || body.angularVelocity.magnitude > 30f)
                    throw new InvalidOperationException("Cornering produced nonfinite or unbounded physics.");
                if (result == null) continue;
                result.PhysicsSteps++;
                result.MaximumSpeedKilometersPerHour = Mathf.Max(result.MaximumSpeedKilometersPerHour, speed * 3.6f);
                result.MinimumUprightDot = Mathf.Min(result.MinimumUprightDot, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
                result.MinimumHeightMeters = Mathf.Min(result.MinimumHeightMeters, body.position.y);
                result.MaximumHeightMeters = Mathf.Max(result.MaximumHeightMeters, body.position.y);
                result.PeakBodySideslipDegrees = Mathf.Max(result.PeakBodySideslipDegrees, Mathf.Abs(SideslipDegrees(body)));
                result.PeakYawRateDegreesPerSecond = Mathf.Max(result.PeakYawRateDegreesPerSecond,
                    Mathf.Abs(Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg));
                result.PeakLateralAccelerationMetersPerSecondSquared = Mathf.Max(result.PeakLateralAccelerationMetersPerSecondSquared,
                    Mathf.Abs(controller.Telemetry.LocalAccelerationMetersPerSecondSquared.x));
                result.PeakSteeringInput = Mathf.Max(result.PeakSteeringInput, Mathf.Abs(controller.Telemetry.Input.Steering));
                var definition = controller.Definition;
                var lateSampleStart = result.IsRecoveryManeuver ? 1.9f : 0.9f;
                for (var wheelIndex = 0; wheelIndex < controller.Telemetry.Wheels.Length; wheelIndex++)
                {
                    var wheel = controller.Telemetry.Wheels[wheelIndex];
                    if (!wheel.Grounded) continue;
                    if (wheel.SurfaceName != "Dry asphalt") result.UnknownSurfaceSamples++;
                    var front = wheelIndex < 2;
                    var slip = Mathf.Abs(wheel.SlipAngleDegrees);
                    var maximumForce = wheel.NormalLoadNewtons * definition.Tire.PeakDryFrictionCoefficient
                                       * wheel.SurfaceGripMultiplier
                                       * Math.Pow(Math.Max(wheel.NormalLoadNewtons / definition.Tire.ReferenceLoadNewtons, 0.05d),
                                           definition.Tire.LoadSensitivityExponent);
                    var utilization = maximumForce > 0.01d
                        ? (float)(Math.Sqrt(wheel.LongitudinalForceNewtons * wheel.LongitudinalForceNewtons
                                          + wheel.LateralForceNewtons * wheel.LateralForceNewtons) / maximumForce)
                        : 0f;
                    if (front)
                    {
                        result.FrontGroundedSamples++;
                        result.PeakFrontSlipAngleDegrees = Mathf.Max(result.PeakFrontSlipAngleDegrees, slip);
                        result.PeakFrontLongitudinalSlipRatio = Mathf.Max(result.PeakFrontLongitudinalSlipRatio, Mathf.Abs(wheel.SlipRatio));
                        result.PeakFrontForceEnvelopeUtilization = Mathf.Max(result.PeakFrontForceEnvelopeUtilization, utilization);
                        result.FrontUtilizationSum += utilization;
                        result.PeakFrontSlipDemandRatio = Mathf.Max(result.PeakFrontSlipDemandRatio, wheel.SlipDemandRatio);
                        if (wheel.IsSliding) result.FrontPostPeakSamples++;
                        if (wheel.SlipDemandRatio > 1f) result.FrontDemandAboveLinearEnvelopeSamples++;
                        if (slip > 8f) result.FrontSlidingSamples++;
                        if (elapsedTurnSeconds >= lateSampleStart)
                        {
                            result.LateFrontSlipSum += slip;
                            result.LateFrontSamples++;
                        }
                    }
                    else
                    {
                        result.RearGroundedSamples++;
                        result.PeakRearSlipAngleDegrees = Mathf.Max(result.PeakRearSlipAngleDegrees, slip);
                        result.PeakRearLongitudinalSlipRatio = Mathf.Max(result.PeakRearLongitudinalSlipRatio, Mathf.Abs(wheel.SlipRatio));
                        result.PeakRearForceEnvelopeUtilization = Mathf.Max(result.PeakRearForceEnvelopeUtilization, utilization);
                        result.RearUtilizationSum += utilization;
                        result.PeakRearSlipDemandRatio = Mathf.Max(result.PeakRearSlipDemandRatio, wheel.SlipDemandRatio);
                        if (wheel.IsSliding) result.RearPostPeakSamples++;
                        if (wheel.SlipDemandRatio > 1f) result.RearDemandAboveLinearEnvelopeSamples++;
                        if (slip > 8f) result.RearSlidingSamples++;
                        if (elapsedTurnSeconds >= lateSampleStart)
                        {
                            result.LateRearSlipSum += slip;
                            result.LateRearSamples++;
                        }
                    }
                }
            }
        }

        private static float SideslipDegrees(Rigidbody body)
        {
            var local = Quaternion.Inverse(body.rotation) * body.velocity;
            return Mathf.Atan2(local.x, Mathf.Max(Mathf.Abs(local.z), 0.1f)) * Mathf.Rad2Deg;
        }

        private static string Classify(ManeuverResult result)
        {
            // These labels describe measurements, not a requirement that every hard turn must drift.
            if (result.MeanLateRearSlipAngleDegrees >= 8f && result.PeakBodySideslipDegrees >= 6f)
                return result.MeanLateFrontSlipAngleDegrees >= 8f ? "Both axles sliding" : "Rear sliding";
            if (result.MeanLateFrontSlipAngleDegrees >= 8f
                && result.MeanLateFrontSlipAngleDegrees > result.MeanLateRearSlipAngleDegrees * 1.5f)
                return "Front push / understeer";
            return "Within grip / transient slip";
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                   && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void Check(bool condition, string message, CorneringReport report)
        {
            report.Assertions++;
            if (!condition) report.Failures.Add(message);
        }

        [Serializable]
        private sealed class CorneringReport
        {
            public bool Passed;
            public string GeneratedUtc;
            public string UnityVersion;
            public int SimulationHertz;
            public int Assertions;
            public bool RoutineRoadTractionControlEnabled = true;
            public string Scope = "Live 60 Hz keyboard input; manual 360 Hz Unity physics; stock/touring on dry asphalt. Historical Maneuvers explicitly disable Road traction control; RoutineHandling explicitly enables Road traction control. Slip and front-push labels are descriptive, not a drift requirement.";
            public List<ManeuverResult> Maneuvers = new List<ManeuverResult>();
            public List<ManeuverResult> RoutineHandling = new List<ManeuverResult>();
            public List<string> Failures = new List<string>();
        }

        [Serializable]
        private sealed class ManeuverResult
        {
            public string Name;
            public string WheelPackageKey;
            public string Classification;
            public float RequestedSpeedKilometersPerHour;
            public float SpeedAtTurnKilometersPerHour;
            public float AccelerationSeconds;
            public float RequestedSteering;
            public float RequestedThrottle;
            public float RequestedHandbrake;
            public bool IsRecoveryManeuver;
            public bool IsRoutineHandlingManeuver;
            public bool RoadTractionControlEnabled;
            public InputSegment[] RoutineInputSegments;
            public float SteeringHoldSeconds;
            public float AuthoredMaximumSteeringDegrees;
            public float SteeringInputAfter300Milliseconds;
            public float AppliedSteeringDegreesAfter300Milliseconds;
            public float SteeringInputAfter500Milliseconds;
            public float AppliedSteeringDegreesAfter500Milliseconds;
            public float PeakSteeringInput;
            public float PeakAppliedSteeringDegrees;
            public float SpeedAfter300MillisecondsKilometersPerHour;
            public float SpeedAfterTurnKilometersPerHour;
            public float SpeedLossKilometersPerHour;
            public float MaximumSpeedKilometersPerHour;
            public float HeadingChangeDegrees;
            public float LateralDisplacementMeters;
            public float TravelDistanceMeters;
            public float PeakFrontSlipAngleDegrees;
            public float PeakRearSlipAngleDegrees;
            public float MeanLateFrontSlipAngleDegrees;
            public float MeanLateRearSlipAngleDegrees;
            public float PeakFrontLongitudinalSlipRatio;
            public float PeakRearLongitudinalSlipRatio;
            public float PeakFrontSlipDemandRatio;
            public float PeakRearSlipDemandRatio;
            public float PeakBodySideslipDegrees;
            public float FinalBodySideslipDegrees;
            public float PeakYawRateDegreesPerSecond;
            public float FinalYawRateDegreesPerSecond;
            public float FinalLateralAccelerationMetersPerSecondSquared;
            public float SpeedAtFinalSteeringReleaseKilometersPerHour;
            public float BodySideslipAtFinalSteeringReleaseDegrees;
            public float YawRateAtFinalSteeringReleaseDegreesPerSecond;
            public float InputCenteringAfterFinalReleaseSeconds;
            public float YawSettlingAfterFinalReleaseSeconds;
            public float PeakBodySideslipAfterFinalReleaseDegrees;
            public float YawRateAfterFinalRelease150MillisecondsDegreesPerSecond;
            public float YawRateAfterFinalRelease500MillisecondsDegreesPerSecond;
            public float YawRateAfterFinalRelease1SecondDegreesPerSecond;
            public float AdditionalHeadingAfterFinalReleaseDegrees;
            public float AccumulatedAdditionalHeadingAfterFinalReleaseDegrees;
            public float PeakLateralAccelerationMetersPerSecondSquared;
            public float PeakFrontForceEnvelopeUtilization;
            public float PeakRearForceEnvelopeUtilization;
            public float MeanFrontForceEnvelopeUtilization;
            public float MeanRearForceEnvelopeUtilization;
            public float MinimumUprightDot = 1f;
            public float MinimumHeightMeters = float.MaxValue;
            public float MaximumHeightMeters = float.MinValue;
            public int PhysicsSteps;
            public int FrontGroundedSamples;
            public int RearGroundedSamples;
            public int FrontSlidingSamples;
            public int RearSlidingSamples;
            public int FrontPostPeakSamples;
            public int RearPostPeakSamples;
            public int FrontDemandAboveLinearEnvelopeSamples;
            public int RearDemandAboveLinearEnvelopeSamples;
            public int SkidMarkSegmentCount;
            public float BodySideslipAtHandbrakeReleaseDegrees;
            public float YawRateAtHandbrakeReleaseDegreesPerSecond;
            public float BodySideslipAfterRecoveryDegrees;
            public float YawRateAfterRecoveryDegreesPerSecond;
            public float PeakRecoveryYawRateDegreesPerSecond;
            public float HeadingChangeDuringRecoveryDegrees;
            public float SpeedAfterRecoveryKilometersPerHour;
            public float MaximumHandbrakeInputAfterRelease;
            public float PeakRecoveryRequestedSteering;
            public float PeakRearLongitudinalSlipRatioLastRecoverySecond;
            public float FinalRearLongitudinalSlipRatio;
            public int CountersteeringFrames;
            public int OppositeSteeringFrames;
            public int UnknownSurfaceSamples;
            [NonSerialized] public double FrontUtilizationSum;
            [NonSerialized] public double RearUtilizationSum;
            [NonSerialized] public double LateFrontSlipSum;
            [NonSerialized] public double LateRearSlipSum;
            [NonSerialized] public int LateFrontSamples;
            [NonSerialized] public int LateRearSamples;
        }

        [Serializable]
        private sealed class InputSegment
        {
            public float Seconds;
            public float Steering;

            public InputSegment(float seconds, float steering)
            {
                Seconds = seconds;
                Steering = steering;
            }
        }
    }
}
