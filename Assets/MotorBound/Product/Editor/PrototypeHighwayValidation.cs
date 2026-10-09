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
    // Open-loop highway inputs, not a virtual stability driver. Entry wheel speed comes from the live powertrain.
    public static class PrototypeHighwayValidation
    {
        private const float InputStep = 1f / 60f;
        private const float PhysicsStep = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;
        private const float MetersPerSecondPerMph = 0.44704f;
        private const float Gravity = 9.80665f;

        [MenuItem("MotorBound/Validate Highway Steering")]
        public static void Run()
        {
            RunReport("highway-validation.json");
        }

        public static void RunBaseline()
        {
            RunReport("highway-baseline-025.json");
        }

        private static void RunReport(string reportFileName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run highway validation outside Play mode.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified scenes before highway validation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousMode = UnityEngine.Physics.simulationMode;
            var previousGravity = UnityEngine.Physics.gravity;
            var previousIterations = UnityEngine.Physics.defaultSolverIterations;
            var previousVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations;
            var report = new HighwayReport { GeneratedUtc = DateTime.UtcNow.ToString("o"), UnityVersion = Application.unityVersion };
            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                UnityEngine.Physics.gravity = new Vector3(0f, -Gravity, 0f);
                UnityEngine.Physics.defaultSolverIterations = 12;
                UnityEngine.Physics.defaultSolverVelocityIterations = 4;
                var stock = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState());
                var touringState = PrototypeGarageCatalog.CreateNewState();
                if (!PrototypeGarageCatalog.TryInstallCompanionKit(touringState, out var kitMessage))
                    throw new InvalidOperationException(kitMessage);
                if (!PrototypeGarageCatalog.TryInstallWheelPackage(touringState, PrototypeGarageCatalog.TouringKey, out var wheelMessage))
                    throw new InvalidOperationException(wheelMessage);
                var touring = PrototypeGarageCatalog.Compile(touringState);
                foreach (var configuration in new[] { stock, touring })
                {
                    var speeds = configuration == stock ? new[] { 50f, 70f, 90f } : new[] { 70f };
                    foreach (var speed in speeds)
                        foreach (var mode in new[] { DriverAssistMode.Sport, DriverAssistMode.Off })
                            foreach (var direction in new[] { -1f, 1f })
                                foreach (var maneuver in new[] { Maneuver.Gentle, Maneuver.Sharp, Maneuver.Reversal })
                                    RunCase(configuration, speed, direction, mode, maneuver, Pedal.Coast, report);
                    foreach (var direction in new[] { -1f, 1f })
                        foreach (var pedal in new[] { Pedal.Power, Pedal.Lift, Pedal.Brake })
                            RunCase(configuration, 70f, direction, DriverAssistMode.Off, Maneuver.Sharp, pedal, report);
                    RunCase(configuration, 70f, 1f, DriverAssistMode.Road, Maneuver.Sharp, Pedal.Coast, report);
                    RunCase(configuration, 70f, 1f, DriverAssistMode.Sport, Maneuver.Sharp, Pedal.Power, report);
                }
                // Explicit unit comparison: historical 70 km/h is only 43.496 mph, not the user's 70 mph.
                foreach (var maneuver in new[] { Maneuver.Gentle, Maneuver.Sharp })
                    RunCase(stock, 70f / 1.609344f, 1f, DriverAssistMode.Off, maneuver, Pedal.Coast, report);
                Check(report.UnassistedSevereReversalOutcomes > 0,
                    "At least one unassisted 70 mph reversal must demonstrate simultaneous loaded rear sliding / body sideslip and a large sideslip response; every sharp turn need not spin.", report);
            }
            catch (Exception exception) { report.Failures.Add(exception.ToString()); }
            finally
            {
                UnityEngine.Physics.simulationMode = previousMode;
                UnityEngine.Physics.gravity = previousGravity;
                UnityEngine.Physics.defaultSolverIterations = previousIterations;
                UnityEngine.Physics.defaultSolverVelocityIterations = previousVelocityIterations;
                var savedScene = false;
                foreach (var scene in setup) savedScene |= !string.IsNullOrEmpty(scene.path);
                if (savedScene) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                report.Passed = report.Failures.Count == 0;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts"));
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, reportFileName);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log("MotorBound highway validation: " + path + "; cases=" + report.Cases.Count + "; passed=" + report.Passed);
            }
            if (!report.Passed)
                throw new InvalidOperationException("Highway validation failed: " + string.Join(" | ", report.Failures));
        }

        private static void RunCase(PrototypeGarageConfiguration configuration, float speedMph, float direction,
            DriverAssistMode mode, Maneuver maneuver, Pedal pedal, HighwayReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var road = new GameObject("Highway validation dry asphalt", typeof(BoxCollider), typeof(SurfaceGrip));
            road.transform.position = new Vector3(0f, -0.12f, 0f);
            road.GetComponent<BoxCollider>().size = new Vector3(6000f, 0.24f, 6000f);
            road.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Highway validation Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, 0f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            controller.AssistMode = DriverAssistMode.Road;
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var body = car.GetComponent<Rigidbody>();
            var steeringPivot = car.transform.Find("Front Left Visual");
            UnityEngine.Physics.SyncTransforms();
            for (var frame = 0; frame < 120; frame++) Advance(driver, controller, body, default(VehicleInputState));
            var accelerationFrames = 0;
            while (body.velocity.magnitude < speedMph * MetersPerSecondPerMph && accelerationFrames < 3600)
            {
                Advance(driver, controller, body, new VehicleInputState(1f, 0f, 0f, 0f));
                accelerationFrames++;
            }
            // A coast case is already off the accelerator; a lift case releases W exactly as steering starts.
            controller.AssistMode = mode;
            for (var frame = 0; frame < 21; frame++)
                Advance(driver, controller, body, new VehicleInputState(pedal == Pedal.Coast ? 0f : 1f, 0f, 0f, 0f));
            var entrySpeed = body.velocity.magnitude;
            var maximumAngle = (float)configuration.Vehicle.MaximumSteeringAngleDegrees;
            var gentleAngle = Mathf.Atan(0.3f * Gravity * (float)configuration.Vehicle.WheelbaseMeters
                                        / Mathf.Max(entrySpeed * entrySpeed, 1f)) * Mathf.Rad2Deg;
            var command = direction * (maneuver == Maneuver.Gentle ? gentleAngle / maximumAngle : 1f);
            var result = new HighwayCase
            {
                Name = configuration.WheelPackageName + " / " + speedMph + " mph / " + (direction > 0f ? "right" : "left")
                       + " / " + mode + " / " + maneuver + " / " + pedal,
                WheelPackageKey = configuration.WheelPackageKey,
                AssistMode = mode.ToString(), Maneuver = maneuver.ToString(), Pedal = pedal.ToString(),
                RequestedSpeedMilesPerHour = speedMph, RequestedSpeedKilometersPerHour = speedMph * 1.609344f,
                EntrySpeedMilesPerHour = entrySpeed / MetersPerSecondPerMph, EntrySpeedKilometersPerHour = entrySpeed * 3.6f,
                AccelerationSeconds = accelerationFrames * InputStep,
                AuthoredMaximumSteeringDegrees = maximumAngle, FirstRequestedSteering = command,
                GentleTargetGeometricLateralAccelerationG = maneuver == Maneuver.Gentle ? 0.3f : 0f,
                EntryCommandGeometricLateralAccelerationG = entrySpeed * entrySpeed * Mathf.Tan(command * maximumAngle * Mathf.Deg2Rad)
                                                           / (float)configuration.Vehicle.WheelbaseMeters / Gravity,
                EntryCommandKinematicYawDegreesPerSecond = entrySpeed * Mathf.Tan(command * maximumAngle * Mathf.Deg2Rad)
                                                          / (float)configuration.Vehicle.WheelbaseMeters * Mathf.Rad2Deg,
                StartPositionWorld = body.position
            };
            report.Cases.Add(result);
            Check(result.EntrySpeedMilesPerHour >= speedMph * 0.98f && result.EntrySpeedMilesPerHour <= speedMph * 1.05f,
                result.Name + ": actual powertrain must reach a physically rolling highway entry speed.", report);
            var trails = car.AddComponent<PrototypeSkidTrails>();
            trails.Configure(controller);
            var context = new Context { PreviousYaw = body.rotation.eulerAngles.y, PreviousVelocity = body.velocity };
            var turnFrames = maneuver == Maneuver.Reversal ? 84 : 72;
            for (var frame = 0; frame < turnFrames; frame++)
            {
                var steering = maneuver == Maneuver.Reversal && frame >= 36 ? -command : command;
                var input = new VehicleInputState(pedal == Pedal.Power ? 1f : 0f, pedal == Pedal.Brake ? 1f : 0f, steering, 0f);
                Sample(driver, controller, body, steeringPivot, trails, input, result, context, true);
                if (frame == 17 || frame == 29 || (maneuver == Maneuver.Reversal && frame == 71))
                    result.Snapshots.Add(Snapshot(controller, body, steeringPivot, input, context.ElapsedSeconds,
                        frame == 17 ? "300 ms" : frame == 29 ? "500 ms" : "600 ms after reversal"));
                if (frame == 29)
                {
                    result.ActualSteeringAfter500Milliseconds = controller.Telemetry.Input.Steering;
                    result.WheelAngleAfter500MillisecondsDegrees = Mathf.DeltaAngle(0f, steeringPivot.localEulerAngles.y);
                }
            }
            result.TurnSeconds = turnFrames * InputStep;
            result.TurnHeadingChangeDegrees = result.AccumulatedHeadingChangeDegrees;
            result.TurnFinalActualSteering = controller.Telemetry.Input.Steering;
            result.Snapshots.Add(Snapshot(controller, body, steeringPivot, controller.Telemetry.Input, context.ElapsedSeconds, "Steering release"));
            var releaseHeading = result.AccumulatedHeadingChangeDegrees;
            for (var frame = 0; frame < 120; frame++)
            {
                var input = default(VehicleInputState);
                Sample(driver, controller, body, steeringPivot, trails, input, result, context, false);
                if (frame == 5) result.ActualSteeringAfter100MillisecondsRelease = controller.Telemetry.Input.Steering;
            }
            result.Snapshots.Add(Snapshot(controller, body, steeringPivot, default(VehicleInputState), context.ElapsedSeconds, "Two seconds after release"));
            result.AdditionalHeadingAfterReleaseDegrees = result.AccumulatedHeadingChangeDegrees - releaseHeading;
            result.ExitSpeedMilesPerHour = body.velocity.magnitude / MetersPerSecondPerMph;
            result.ExitBodySideslipDegrees = Sideslip(body);
            result.ExitYawRateDegreesPerSecond = YawRate(body);
            result.SkidMarkSegmentCount = trails.SegmentCount;
            result.TravelDistanceMeters = Vector3.Distance(body.position, result.StartPositionWorld);
            result.LateralDisplacementMeters = body.position.x - result.StartPositionWorld.x;
            result.MeanFrontSlipAngleDuringTurnDegrees = (float)(context.FrontSlipSum / Math.Max(1, context.FrontSlipCount));
            result.MeanRearSlipAngleDuringTurnDegrees = (float)(context.RearSlipSum / Math.Max(1, context.RearSlipCount));
            result.TractionControlActiveFraction = context.TractionControlFrames / (float)Math.Max(1, context.FrameCount);
            result.MeanDeliveredDriveTorqueScale = (float)(context.TorqueScaleSum / Math.Max(1, context.FrameCount));
            foreach (var wheel in result.Wheels)
            {
                wheel.MeanNormalLoadNewtons = (float)(wheel.NormalLoadSum / Math.Max(1, context.FrameCount));
                wheel.MeanForceEnvelopeUtilization = (float)(wheel.UtilizationSum / Math.Max(1, wheel.LoadedSamples));
            }
            result.Classification = Classify(result);
            if (result.FrontSlidingDuringTurnSeconds > 0f) report.CasesWithFrontSliding++;
            if (result.RearSlidingDuringTurnSeconds > 0f) report.CasesWithRearSliding++;
            if (result.PeakBodySideslipDuringTurnDegrees >= 45f) report.CasesWithLargeBodySideslipDuringTurn++;
            if (result.PeakBodySideslipDegrees >= 45f) report.CasesWithLargeBodySideslipOverall++;
            if (mode == DriverAssistMode.Off && maneuver == Maneuver.Reversal && speedMph == 70f
                && result.RearSlidingWithBodySideslipDuringTurnSeconds >= 0.1f && result.PeakBodySideslipDegrees >= 15f)
                report.UnassistedSevereReversalOutcomes++;
            Check(Mathf.Abs(result.ActualSteeringAfter500Milliseconds - command) < 0.001f
                  && Mathf.Abs(result.WheelAngleAfter500MillisecondsDegrees - command * maximumAngle) < 0.05f,
                result.Name + ": held input must reach its complete authored angle at highway speed by 500 ms.", report);
            if (maneuver == Maneuver.Reversal)
                Check(Mathf.Abs(result.TurnFinalActualSteering + command) < 0.001f,
                    result.Name + ": opposite held key must reach full opposite lock after unwinding.", report);
            Check(Mathf.Abs(result.ActualSteeringAfter100MillisecondsRelease) < 0.0001f,
                result.Name + ": releasing steering must physically center input within 100 ms.", report);
            Check(result.MaximumDeliveredForceEnvelopeUtilization <= 1.001f,
                result.Name + ": tire force must remain inside its authored combined mu/load envelope.", report);
            Check(result.MinimumHeightMeters > -0.8f && result.MaximumHeightMeters < 10f && context.FrameCount > 0,
                result.Name + ": finite physics must remain on the bounded highway test surface.", report);
            if (configuration.WheelPackageKey == PrototypeGarageCatalog.StockRoadKey && speedMph == 70f
                && direction > 0f && mode == DriverAssistMode.Off && maneuver == Maneuver.Sharp && pedal == Pedal.Coast)
                Check(result.FrontSlidingDuringTurnSeconds >= 0.1f,
                    result.Name + ": a full-lock highway input must measurably exceed loaded front-tire grip, not remain an unexplained wide arc.", report);
            if (maneuver == Maneuver.Gentle)
            {
                Check(result.PeakBodySideslipDuringTurnDegrees < 3f,
                    result.Name + ": a genuine 0.3g gentle input must not produce large body sideslip.", report);
                Check(result.LongestContinuousFrontSlidingDuringTurnSeconds <= 0.05f
                      && result.LongestContinuousRearSlidingDuringTurnSeconds <= 0.05f,
                    result.Name + ": loaded tires must stay sub-peak during a genuine 0.3g gentle turn.", report);
            }
            // These are baseline safety/observability guards, not an assertion that every sharp turn must spin.
        }

        private static void Sample(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body,
            Transform pivot, PrototypeSkidTrails trails, VehicleInputState input, HighwayCase result, Context context, bool turning)
        {
            Advance(driver, controller, body, input);
            trails.SampleContacts();
            var telemetry = controller.Telemetry;
            context.FrameCount++;
            context.ElapsedSeconds += InputStep;
            context.TorqueScaleSum += telemetry.DeliveredDriveTorqueScale;
            if (telemetry.TractionControlActive) context.TractionControlFrames++;
            var angle = Mathf.DeltaAngle(0f, pivot.localEulerAngles.y);
            var speed = body.velocity.magnitude;
            var beta = Sideslip(body);
            var yawRate = YawRate(body);
            var deltaHeading = Mathf.DeltaAngle(context.PreviousYaw, body.rotation.eulerAngles.y);
            result.AccumulatedHeadingChangeDegrees += deltaHeading;
            result.AccumulatedAbsoluteHeadingChangeDegrees += Mathf.Abs(deltaHeading);
            context.PreviousYaw = body.rotation.eulerAngles.y;
            var acceleration = Quaternion.Inverse(body.rotation) * ((body.velocity - context.PreviousVelocity) / InputStep);
            context.PreviousVelocity = body.velocity;
            var lateralG = acceleration.x / Gravity;
            var geometricG = speed * speed * Mathf.Tan(angle * Mathf.Deg2Rad) / (float)controller.Definition.WheelbaseMeters / Gravity;
            var geometricYaw = speed * Mathf.Tan(angle * Mathf.Deg2Rad) / (float)controller.Definition.WheelbaseMeters * Mathf.Rad2Deg;
            if (turning)
            {
                result.KinematicDemandedHeadingDuringTurnDegrees += geometricYaw * InputStep;
                result.PeakGeometricLateralAccelerationDuringTurnG = Mathf.Max(result.PeakGeometricLateralAccelerationDuringTurnG, Mathf.Abs(geometricG));
                result.PeakBodySideslipDuringTurnDegrees = Mathf.Max(result.PeakBodySideslipDuringTurnDegrees, Mathf.Abs(beta));
                result.PeakYawRateDuringTurnDegreesPerSecond = Mathf.Max(result.PeakYawRateDuringTurnDegreesPerSecond, Mathf.Abs(yawRate));
                result.PeakLateralAccelerationDuringTurnG = Mathf.Max(result.PeakLateralAccelerationDuringTurnG, Mathf.Abs(lateralG));
            }
            result.PeakBodySideslipDegrees = Mathf.Max(result.PeakBodySideslipDegrees, Mathf.Abs(beta));
            result.PeakYawRateDegreesPerSecond = Mathf.Max(result.PeakYawRateDegreesPerSecond, Mathf.Abs(yawRate));
            result.PeakRollDegrees = Mathf.Max(result.PeakRollDegrees, Mathf.Abs(Mathf.DeltaAngle(0f, body.rotation.eulerAngles.z)));
            result.MinimumUprightDot = Mathf.Min(result.MinimumUprightDot, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
            result.MinimumHeightMeters = Mathf.Min(result.MinimumHeightMeters, body.position.y);
            result.MaximumHeightMeters = Mathf.Max(result.MaximumHeightMeters, body.position.y);
            result.MinimumGroundedWheelCount = Math.Min(result.MinimumGroundedWheelCount, telemetry.GroundedWheelCount);
            var frontSliding = false;
            var rearSliding = false;
            var frontNormal = 0f;
            var rearNormal = 0f;
            foreach (var wheel in telemetry.Wheels)
            {
                if (!wheel.Grounded) continue;
                if (wheel.SurfaceName != "Dry asphalt") throw new InvalidOperationException("Unexpected highway contact surface.");
            }
            for (var index = 0; index < 4; index++)
            {
                var wheel = telemetry.Wheels[index];
                var metrics = result.Wheels[index];
                var normal = wheel.Grounded ? wheel.NormalLoadNewtons : 0f;
                metrics.MinimumNormalLoadNewtons = Mathf.Min(metrics.MinimumNormalLoadNewtons, normal);
                metrics.MaximumNormalLoadNewtons = Mathf.Max(metrics.MaximumNormalLoadNewtons, normal);
                metrics.NormalLoadSum += normal;
                if (index < 2) frontNormal += normal;
                else rearNormal += normal;
                if (!wheel.Grounded || normal <= 20f) continue;
                metrics.LoadedSamples++;
                metrics.PeakSlipAngleDegrees = Mathf.Max(metrics.PeakSlipAngleDegrees, Mathf.Abs(wheel.SlipAngleDegrees));
                metrics.PeakAbsoluteLongitudinalSlipRatio = Mathf.Max(metrics.PeakAbsoluteLongitudinalSlipRatio, Mathf.Abs(wheel.SlipRatio));
                metrics.PeakPositiveLongitudinalSlipRatio = Mathf.Max(metrics.PeakPositiveLongitudinalSlipRatio, wheel.SlipRatio);
                metrics.PeakSlipDemandRatio = Mathf.Max(metrics.PeakSlipDemandRatio, wheel.SlipDemandRatio);
                var tire = controller.Definition.Tire;
                var limit = normal * tire.PeakDryFrictionCoefficient * wheel.SurfaceGripMultiplier
                            * Math.Pow(Math.Max(normal / tire.ReferenceLoadNewtons, 0.05d), tire.LoadSensitivityExponent);
                var magnitude = Math.Sqrt(wheel.LongitudinalForceNewtons * wheel.LongitudinalForceNewtons
                                          + wheel.LateralForceNewtons * wheel.LateralForceNewtons);
                var utilization = limit > 0.01d ? (float)(magnitude / limit) : 0f;
                metrics.PeakForceEnvelopeUtilization = Mathf.Max(metrics.PeakForceEnvelopeUtilization, utilization);
                metrics.UtilizationSum += utilization;
                result.MaximumDeliveredForceEnvelopeUtilization = Mathf.Max(result.MaximumDeliveredForceEnvelopeUtilization, utilization);
                if (wheel.IsSliding) metrics.SlidingSeconds += InputStep;
                if (!turning) continue;
                if (index < 2)
                {
                    context.FrontSlipSum += Mathf.Abs(wheel.SlipAngleDegrees);
                    context.FrontSlipCount++;
                    frontSliding |= wheel.IsSliding;
                    result.PeakFrontSlipAngleDuringTurnDegrees = Mathf.Max(result.PeakFrontSlipAngleDuringTurnDegrees, Mathf.Abs(wheel.SlipAngleDegrees));
                }
                else
                {
                    context.RearSlipSum += Mathf.Abs(wheel.SlipAngleDegrees);
                    context.RearSlipCount++;
                    rearSliding |= wheel.IsSliding;
                    result.PeakRearSlipAngleDuringTurnDegrees = Mathf.Max(result.PeakRearSlipAngleDuringTurnDegrees, Mathf.Abs(wheel.SlipAngleDegrees));
                }
            }
            result.MinimumFrontAxleLoadNewtons = Mathf.Min(result.MinimumFrontAxleLoadNewtons, frontNormal);
            result.MinimumRearAxleLoadNewtons = Mathf.Min(result.MinimumRearAxleLoadNewtons, rearNormal);
            if (!turning) return;
            if (frontSliding) result.FrontSlidingDuringTurnSeconds += InputStep;
            if (rearSliding) result.RearSlidingDuringTurnSeconds += InputStep;
            if (rearSliding && Mathf.Abs(beta) >= 5f) result.RearSlidingWithBodySideslipDuringTurnSeconds += InputStep;
            context.FrontSlidingRun = frontSliding ? context.FrontSlidingRun + 1 : 0;
            context.RearSlidingRun = rearSliding ? context.RearSlidingRun + 1 : 0;
            result.LongestContinuousFrontSlidingDuringTurnSeconds = Mathf.Max(result.LongestContinuousFrontSlidingDuringTurnSeconds, context.FrontSlidingRun * InputStep);
            result.LongestContinuousRearSlidingDuringTurnSeconds = Mathf.Max(result.LongestContinuousRearSlidingDuringTurnSeconds, context.RearSlidingRun * InputStep);
        }

        private static HighwaySnapshot Snapshot(RaycastVehicleController controller, Rigidbody body, Transform pivot,
            VehicleInputState requested, float seconds, string label)
        {
            var telemetry = controller.Telemetry;
            var wheels = telemetry.Wheels;
            return new HighwaySnapshot
            {
                Label = label, Seconds = seconds, RequestedSteering = requested.Steering,
                ActualSteering = telemetry.Input.Steering, WheelAngleDegrees = Mathf.DeltaAngle(0f, pivot.localEulerAngles.y),
                RequestedThrottle = requested.Throttle, RequestedBrake = requested.Brake,
                SpeedMilesPerHour = body.velocity.magnitude / MetersPerSecondPerMph,
                BodySideslipDegrees = Sideslip(body), YawRateDegreesPerSecond = YawRate(body),
                LocalAccelerationMetersPerSecondSquared = telemetry.LocalAccelerationMetersPerSecondSquared,
                FrontLeftNormalLoadNewtons = wheels[0].NormalLoadNewtons, FrontRightNormalLoadNewtons = wheels[1].NormalLoadNewtons,
                RearLeftNormalLoadNewtons = wheels[2].NormalLoadNewtons, RearRightNormalLoadNewtons = wheels[3].NormalLoadNewtons,
                FrontLeftSlipAngleDegrees = wheels[0].SlipAngleDegrees, FrontRightSlipAngleDegrees = wheels[1].SlipAngleDegrees,
                RearLeftSlipAngleDegrees = wheels[2].SlipAngleDegrees, RearRightSlipAngleDegrees = wheels[3].SlipAngleDegrees,
                TractionControlActive = telemetry.TractionControlActive, DeliveredDriveTorqueScale = telemetry.DeliveredDriveTorqueScale,
                AssistPhase = telemetry.AssistPhase.ToString()
            };
        }

        private static string Classify(HighwayCase result)
        {
            if (result.PeakBodySideslipDegrees >= 45f)
                return result.PeakBodySideslipDuringTurnDegrees >= 45f ? "Large body sideslip / spin risk during steering" : "Large body sideslip / spin risk after steering release";
            if (result.RearSlidingWithBodySideslipDuringTurnSeconds >= 0.15f)
                return result.FrontSlidingDuringTurnSeconds >= 0.15f ? "Combined front/rear skid with simultaneous loaded rear sliding and body sideslip" : "Loaded rear sliding with simultaneous body sideslip";
            if (result.FrontSlidingDuringTurnSeconds >= 0.15f
                && result.MeanFrontSlipAngleDuringTurnDegrees > result.MeanRearSlipAngleDuringTurnDegrees + 3f)
                return "Front-tire skid / understeer; wide arc does not mean retained front grip";
            if (result.FrontSlidingDuringTurnSeconds > 0f || result.RearSlidingDuringTurnSeconds > 0f)
                return "Brief or mixed axle tire saturation";
            return "Sub-peak grip cornering";
        }

        private static void Advance(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body, VehicleInputState input)
        {
            driver.ApplyInput(input, InputStep);
            for (var step = 0; step < 6; step++)
            {
                controller.SimulateStep(PhysicsStep);
                UnityEngine.Physics.Simulate(PhysicsStep);
                if (!Finite(body.position) || !Finite(body.velocity) || !Finite(body.angularVelocity)
                    || body.velocity.magnitude > 120f || body.angularVelocity.magnitude > 30f)
                    throw new InvalidOperationException("Highway physics exceeded finite, bounded solver limits.");
            }
        }
        private static float Sideslip(Rigidbody body)
        {
            var local = Quaternion.Inverse(body.rotation) * body.velocity;
            return local.sqrMagnitude > 0.01f ? Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg : 0f;
        }
        private static float YawRate(Rigidbody body) => Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static void Check(bool condition, string message, HighwayReport report)
        {
            report.Assertions++;
            if (!condition) report.Failures.Add(message);
        }
        private enum Maneuver { Gentle, Sharp, Reversal }
        private enum Pedal { Coast, Power, Lift, Brake }
        private sealed class Context
        {
            public float PreviousYaw, ElapsedSeconds;
            public Vector3 PreviousVelocity;
            public int FrameCount, FrontSlidingRun, RearSlidingRun, FrontSlipCount, RearSlipCount, TractionControlFrames;
            public double FrontSlipSum, RearSlipSum, TorqueScaleSum;
        }
        [Serializable] private sealed class HighwayReport
        {
            public string GeneratedUtc, UnityVersion;
            public int ReportScopeVersion = 2;
            public int SimulationHertz = RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;
            public int InputHertz = 60;
            public string EntryMethod = "Natural engine acceleration in Road mode; then selected assist and 350 ms pedal preconditioning. No moving velocity/yaw assignment.";
            public string AcceptanceScope = "Entry speed, full authored steering, prompt centering, finite physics, combined tire force envelope, plus stable sub-peak 0.3g gentle turns. Sharp cases have no universal rear-spin or stability expectation.";
            public string SlidingDefinition = "At least one grounded axle wheel with >20 N normal load and WheelTelemetry.IsSliding at each 60 Hz observation. Continuous durations are consecutive observations.";
            public string MechanicalVariantLimitation = "Current authoring shares tire and suspension across axles; no rear-only grip or roll-bias variant is fabricated.";
            public bool Passed = false;
            public int Assertions, CasesWithFrontSliding, CasesWithRearSliding, CasesWithLargeBodySideslipDuringTurn, CasesWithLargeBodySideslipOverall;
            public int UnassistedSevereReversalOutcomes;
            public List<HighwayCase> Cases = new List<HighwayCase>();
            public List<string> Failures = new List<string>();
        }
        [Serializable] private sealed class HighwayCase
        {
            public string Name, WheelPackageKey, AssistMode, Maneuver, Pedal, Classification;
            public float RequestedSpeedMilesPerHour, RequestedSpeedKilometersPerHour, EntrySpeedMilesPerHour, EntrySpeedKilometersPerHour;
            public float AccelerationSeconds, AuthoredMaximumSteeringDegrees, FirstRequestedSteering, GentleTargetGeometricLateralAccelerationG;
            public float EntryCommandGeometricLateralAccelerationG, EntryCommandKinematicYawDegreesPerSecond;
            public float ActualSteeringAfter500Milliseconds, WheelAngleAfter500MillisecondsDegrees, TurnFinalActualSteering;
            public float ActualSteeringAfter100MillisecondsRelease, TurnSeconds, TurnHeadingChangeDegrees, KinematicDemandedHeadingDuringTurnDegrees;
            public float AccumulatedHeadingChangeDegrees, AccumulatedAbsoluteHeadingChangeDegrees, AdditionalHeadingAfterReleaseDegrees;
            public float PeakGeometricLateralAccelerationDuringTurnG, PeakBodySideslipDuringTurnDegrees, PeakYawRateDuringTurnDegreesPerSecond;
            public float PeakLateralAccelerationDuringTurnG, PeakBodySideslipDegrees, PeakYawRateDegreesPerSecond, PeakRollDegrees;
            public float PeakFrontSlipAngleDuringTurnDegrees, PeakRearSlipAngleDuringTurnDegrees;
            public float MeanFrontSlipAngleDuringTurnDegrees, MeanRearSlipAngleDuringTurnDegrees;
            public float FrontSlidingDuringTurnSeconds, RearSlidingDuringTurnSeconds;
            public float RearSlidingWithBodySideslipDuringTurnSeconds;
            public float LongestContinuousFrontSlidingDuringTurnSeconds, LongestContinuousRearSlidingDuringTurnSeconds;
            public float MaximumDeliveredForceEnvelopeUtilization, TractionControlActiveFraction, MeanDeliveredDriveTorqueScale;
            public float MinimumUprightDot = 1f;
            public float MinimumHeightMeters = float.MaxValue, MaximumHeightMeters = float.MinValue;
            public float MinimumFrontAxleLoadNewtons = float.MaxValue, MinimumRearAxleLoadNewtons = float.MaxValue;
            public int MinimumGroundedWheelCount = 4;
            public float ExitSpeedMilesPerHour, ExitBodySideslipDegrees, ExitYawRateDegreesPerSecond;
            public float TravelDistanceMeters, LateralDisplacementMeters;
            public int SkidMarkSegmentCount;
            public Vector3 StartPositionWorld;
            public WheelMetrics[] Wheels = { new WheelMetrics { Name = "Front Left" }, new WheelMetrics { Name = "Front Right" },
                new WheelMetrics { Name = "Rear Left" }, new WheelMetrics { Name = "Rear Right" } };
            public List<HighwaySnapshot> Snapshots = new List<HighwaySnapshot>();
        }
        [Serializable] private sealed class WheelMetrics
        {
            public string Name;
            public float MinimumNormalLoadNewtons = float.MaxValue;
            public float MaximumNormalLoadNewtons, MeanNormalLoadNewtons, PeakSlipAngleDegrees;
            public float PeakAbsoluteLongitudinalSlipRatio, PeakPositiveLongitudinalSlipRatio, PeakSlipDemandRatio;
            public float PeakForceEnvelopeUtilization, MeanForceEnvelopeUtilization, SlidingSeconds;
            [NonSerialized] public double NormalLoadSum, UtilizationSum;
            [NonSerialized] public int LoadedSamples;
        }
        [Serializable] private sealed class HighwaySnapshot
        {
            public string Label, AssistPhase;
            public float Seconds, RequestedSteering, ActualSteering, WheelAngleDegrees, RequestedThrottle, RequestedBrake;
            public float SpeedMilesPerHour, BodySideslipDegrees, YawRateDegreesPerSecond;
            public Vector3 LocalAccelerationMetersPerSecondSquared;
            public float FrontLeftNormalLoadNewtons, FrontRightNormalLoadNewtons, RearLeftNormalLoadNewtons, RearRightNormalLoadNewtons;
            public float FrontLeftSlipAngleDegrees, FrontRightSlipAngleDegrees, RearLeftSlipAngleDegrees, RearRightSlipAngleDegrees;
            public bool TractionControlActive;
            public float DeliveredDriveTorqueScale;
        }
    }
}
