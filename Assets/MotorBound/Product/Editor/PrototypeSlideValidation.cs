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
    // All controls act through the production input driver. The runner never sets moving-car velocity or yaw.
    public static class PrototypeSlideValidation
    {
        private const float InputSeconds = 1f / 60f;
        private const float PhysicsSeconds = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;

        [MenuItem("MotorBound/Validate Slide Mechanics")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run slide validation outside Play mode.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified scenes before slide validation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousMode = UnityEngine.Physics.simulationMode;
            var previousGravity = UnityEngine.Physics.gravity;
            var previousIterations = UnityEngine.Physics.defaultSolverIterations;
            var previousVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations;
            var report = new SlideReport { GeneratedUtc = DateTime.UtcNow.ToString("o"), UnityVersion = Application.unityVersion };
            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);
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
                    foreach (var speed in new[] { 30f, 50f, 70f })
                        foreach (var direction in new[] { -1f, 1f })
                            foreach (var assist in new[] { DriverAssistMode.Sport, DriverAssistMode.Off })
                                RunCase(configuration, speed, direction, assist, InputPolicy.VirtualDriver, report);
                    foreach (var speed in new[] { 50f, 70f })
                        foreach (var direction in new[] { -1f, 1f })
                            RunCase(configuration, speed, direction, DriverAssistMode.Off, InputPolicy.PowerOversteer, report);
                    foreach (var speed in new[] { 50f, 70f })
                        foreach (var assist in new[] { DriverAssistMode.Road, DriverAssistMode.Sport })
                            RunCase(configuration, speed, 1f, assist, InputPolicy.IdenticalOpenLoop, report);
                }
                foreach (var direction in new[] { -1f, 1f })
                    foreach (var assist in new[] { DriverAssistMode.Sport, DriverAssistMode.Off })
                        RunCase(stock, 50f, direction, assist, InputPolicy.KeyboardDutyCycle, report);
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
                foreach (var scene in setup) savedScene |= !string.IsNullOrEmpty(scene.path);
                if (savedScene) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                report.Passed = report.Failures.Count == 0;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts"));
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "slide-validation.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                var traces = new TraceReport { GeneratedUtc = report.GeneratedUtc };
                foreach (var result in report.Cases)
                    traces.Cases.Add(new CaseTrace { Name = result.Name, Samples = result.Trace });
                File.WriteAllText(Path.Combine(directory, "slide-validation-traces.json"), JsonUtility.ToJson(traces));
                Debug.Log("MotorBound slide validation: " + path + "; passed=" + report.Passed
                          + "; physical slide outcomes=" + report.PhysicalSlideOutcomes + "/" + report.Cases.Count);
            }
            if (!report.Passed)
                throw new InvalidOperationException("Slide validation failed: " + string.Join(" | ", report.Failures));
        }

        private static void RunCase(PrototypeGarageConfiguration configuration, float speedKmh, float direction,
            DriverAssistMode mode, InputPolicy policy, SlideReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var road = new GameObject("Slide validation dry asphalt", typeof(BoxCollider), typeof(SurfaceGrip));
            road.transform.position = new Vector3(0f, -0.12f, 0f);
            road.GetComponent<BoxCollider>().size = new Vector3(3000f, 0.24f, 3000f);
            road.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Slide validation Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, 0f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(configuration.Vehicle);
            controller.AssistMode = DriverAssistMode.Road;
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var body = car.GetComponent<Rigidbody>();
            var pivot = car.transform.Find("Front Left Visual");
            UnityEngine.Physics.SyncTransforms();
            for (var frame = 0; frame < 120; frame++) Advance(driver, controller, body, default(VehicleInputState));
            var accelerationFrames = 0;
            while (body.velocity.magnitude * 3.6f < speedKmh && accelerationFrames < 1800)
            {
                Advance(driver, controller, body, new VehicleInputState(1f, 0f, 0f, 0f));
                accelerationFrames++;
            }
            controller.AssistMode = mode;
            var trails = car.AddComponent<PrototypeSkidTrails>();
            trails.Configure(controller);
            var result = new SlideCase
            {
                Name = configuration.WheelPackageName + " / " + speedKmh + " km/h / "
                       + (direction > 0f ? "right" : "left") + " / " + mode + " / " + policy,
                WheelPackageKey = configuration.WheelPackageKey,
                AssistMode = mode.ToString(),
                InputPolicy = policy.ToString(),
                RearDifferentialType = configuration.Vehicle.RearDifferential.Type.ToString(),
                CommandsIdenticalAcrossAssistModes = policy == InputPolicy.IdenticalOpenLoop,
                RequestedSpeedKilometersPerHour = speedKmh,
                EntrySpeedKilometersPerHour = body.velocity.magnitude * 3.6f,
                Direction = direction,
                AuthoredMaximumSteeringDegrees = (float)configuration.Vehicle.MaximumSteeringAngleDegrees,
                StartPositionWorld = body.position
            };
            report.Cases.Add(result);
            Check(result.EntrySpeedKilometersPerHour >= speedKmh && result.EntrySpeedKilometersPerHour <= speedKmh + 1f,
                result.Name + ": engine and contact solver must reach the requested rolling entry speed.", report);
            var context = new SamplingContext { PreviousYawDegrees = body.rotation.eulerAngles.y };

            // Brake-pulse profile comparisons are identical; power-only cases establish a moderate turn first.
            var powerOnly = policy == InputPolicy.PowerOversteer;
            var initiationFrames = powerOnly ? 108 : 39;
            var initiation = new PhaseResult { Name = "Initiate", DurationSeconds = initiationFrames * InputSeconds };
            result.Phases.Add(initiation);
            for (var frame = 0; frame < initiationFrames; frame++)
            {
                var handbrake = frame >= 24 ? 1f : 0f;
                var requested = new VehicleInputState(frame < 24 ? 0.4f : 0.8f, 0f,
                    direction * (frame < 24 ? 0.2f : 0.3f), handbrake);
                if (powerOnly)
                    requested = new VehicleInputState(frame < 36 ? 0.25f : 1f, 0f, direction * 0.1f, 0f);
                else if (policy == InputPolicy.KeyboardDutyCycle)
                    requested = new VehicleInputState(1f, 0f,
                        frame < 6 || (frame >= 18 && frame < 33) ? direction : 0f, handbrake);
                SampleFrame(driver, controller, body, pivot, trails, requested, initiation, result, context, frame, false);
                if (MeasuredPhysicalBreakaway(controller, body, direction))
                {
                    RecordBreakaway(result, context, body);
                    if (powerOnly) break;
                }
            }
            initiation.DurationSeconds = initiation.FrameCount * InputSeconds;
            result.BodySideslipAtHandbrakeReleaseDegrees = FullSideslipDegrees(body);
            result.YawAtHandbrakeReleaseDegreesPerSecond = YawRate(body);
            if (!powerOnly && policy != InputPolicy.IdenticalOpenLoop && !result.InitiatedPhysicalSlide)
            {
                var followThrough = new PhaseResult { Name = "Initiate follow-through", DurationSeconds = 0.85f };
                result.Phases.Add(followThrough);
                for (var frame = 0; frame < 51; frame++)
                {
                    var requested = HoldInput(driver, body, result, policy, frame);
                    SampleFrame(driver, controller, body, pivot, trails, requested, followThrough, result, context, frame, false);
                    if (!MeasuredPhysicalBreakaway(controller, body, direction)) continue;
                    RecordBreakaway(result, context, body);
                    break;
                }
                followThrough.DurationSeconds = followThrough.FrameCount * InputSeconds;
            }

            var hold = new PhaseResult { Name = "Hold", DurationSeconds = 3f };
            result.Phases.Add(hold);
            for (var frame = 0; frame < 180; frame++)
            {
                var requested = HoldInput(driver, body, result, policy, frame);
                if (policy == InputPolicy.IdenticalOpenLoop)
                    requested = new VehicleInputState(0.8f, 0f, -direction * 0.12f, 0f);
                SampleFrame(driver, controller, body, pivot, trails, requested,
                    hold, result, context, frame, true);
            }
            result.HasPoweredSlideContinuation = result.PoweredRearSlipDuringControlledSlideSeconds >= 0.2f
                                                  && hold.PeakRearPositiveLongitudinalSlipRatio > 0.03f;
            result.SustainedPhysicalSlide = result.LongestControlledSlideSeconds >= 1f && result.HasPoweredSlideContinuation;

            var recovery = new PhaseResult { Name = "Recover", DurationSeconds = 3f };
            result.Phases.Add(recovery);
            for (var frame = 0; frame < 180; frame++)
            {
                var beta = FullSideslipDegrees(body);
                var steering = Mathf.Abs(beta) > 1f
                    ? Mathf.Clamp(beta / result.AuthoredMaximumSteeringDegrees, -0.9f, 0.9f) : 0f;
                var throttle = 0.05f;
                if (policy == InputPolicy.IdenticalOpenLoop) steering = 0f;
                if (policy == InputPolicy.KeyboardDutyCycle)
                {
                    steering = DigitalSteeringRequest(driver, steering);
                    throttle = 0f;
                }
                SampleFrame(driver, controller, body, pivot, trails, new VehicleInputState(throttle, 0f, steering, 0f),
                    recovery, result, context, frame, false);
            }
            result.ExitSpeedKilometersPerHour = body.velocity.magnitude * 3.6f;
            result.ExitBodySideslipDegrees = FullSideslipDegrees(body);
            result.ExitYawRateDegreesPerSecond = YawRate(body);
            result.ExitMeanRearLongitudinalSlipRatio = (Mathf.Abs(controller.Telemetry.Wheels[2].SlipRatio)
                                                      + Mathf.Abs(controller.Telemetry.Wheels[3].SlipRatio)) * 0.5f;
            result.RecoveredPhysicalSlide = Mathf.Abs(result.ExitBodySideslipDegrees) < 3f
                                            && Mathf.Abs(result.ExitYawRateDegreesPerSecond) < 5f
                                            && result.ExitMeanRearLongitudinalSlipRatio < 0.1f
                                            && result.ExitSpeedKilometersPerHour > result.EntrySpeedKilometersPerHour * 0.5f;
            result.DemonstratesPhysicalSlide = result.InitiatedPhysicalSlide && result.SustainedPhysicalSlide
                                               && result.HoldCountersteeringFrames > 0 && result.HoldAppliedCountersteeringFrames > 0
                                               && result.RecoveredPhysicalSlide;
            result.RequiresPhysicalSlideAcceptance = ((policy == InputPolicy.VirtualDriver || policy == InputPolicy.PowerOversteer)
                                                      && speedKmh >= 50f) || policy == InputPolicy.KeyboardDutyCycle;
            if (!result.InitiatedPhysicalSlide) result.OutcomeFailures.Add("No simultaneous 5-degree body sideslip and rear sliding contact during initiation/follow-through.");
            if (result.LongestControlledSlideSeconds < 1f) result.OutcomeFailures.Add("No continuous one-second controlled physical slide window.");
            if (!result.HasPoweredSlideContinuation) result.OutcomeFailures.Add("No measured 0.2-second powered rear-slip continuation during controlled sliding.");
            if (result.HoldCountersteeringFrames == 0) result.OutcomeFailures.Add("No requested countersteering during hold.");
            if (result.HoldAppliedCountersteeringFrames == 0) result.OutcomeFailures.Add("No physically applied countersteering during hold.");
            if (!result.RecoveredPhysicalSlide) result.OutcomeFailures.Add("Recovery did not restore rolling travel, bounded yaw/sideslip and at least half entry speed.");
            if (result.DemonstratesPhysicalSlide) report.PhysicalSlideOutcomes++;
            result.TravelDistanceMeters = Vector3.Distance(result.StartPositionWorld, body.position);
            result.SkidMarkSegmentCount = trails.SegmentCount;
            foreach (var phase in result.Phases)
            {
                phase.MeanDeliveredDriveTorqueScale = (float)(phase.TorqueScaleSum / Math.Max(1, phase.FrameCount));
                phase.TractionControlActiveFraction = phase.TractionControlActiveFrames / (float)Math.Max(1, phase.FrameCount);
            }
            // Exploration checks safety and force bounds. Outcome flags are deliberately not silently treated as success.
            Check(result.MinimumUprightDot > 0.15f && result.MinimumHeightMeters > -0.5f && result.MaximumHeightMeters < 4f,
                result.Name + ": slide maneuver must remain upright with bounded height.", report);
            Check(result.RoadContactWheelSamples > 0 && result.UnknownContactWheelSamples == 0,
                result.Name + ": slide contact must occur only on the validation road.", report);
            Check(result.MaximumDeliveredForceEnvelopeUtilization <= 1.001f,
                result.Name + ": delivered tire force must respect the current load and grip envelope.", report);
            Check(hold.MaximumHandbrakeCommand == 0f && recovery.MaximumHandbrakeCommand == 0f,
                result.Name + ": sustained sliding and recovery must follow actual handbrake release.", report);
            if (powerOnly)
                Check(initiation.MaximumHandbrakeCommand == 0f,
                    result.Name + ": power-oversteer initiation must occur without any handbrake command.", report);
            if (result.RequiresPhysicalSlideAcceptance)
                Check(result.DemonstratesPhysicalSlide,
                    result.Name + ": must physically initiate, sustain a controlled slide for at least one second, countersteer and recover.", report);
        }

        private static VehicleInputState HoldInput(PrototypeInputDriver driver, Rigidbody body, SlideCase result, InputPolicy policy, int frame)
        {
            var beta = FullSideslipDegrees(body);
            var speed = body.velocity.magnitude;
            // This is an automated driver commanding real steering/pedal actuators, not a yaw actuator.
            // Keep a finite cornering rate rather than simply aligning the front tires with travel and straightening the slide.
            var desiredYaw = result.Direction * Mathf.Clamp(7f / Mathf.Max(speed, 6f) * Mathf.Rad2Deg, 15f, 40f);
            var steering = beta / result.AuthoredMaximumSteeringDegrees
                           + result.Direction * (0.16f + (15f - Mathf.Abs(beta)) * 0.01f)
                           + (desiredYaw - YawRate(body)) * 0.003f;
            steering = Mathf.Clamp(steering, -0.9f, 0.9f);
            // Breakaway can accelerate the car before the driver catches it. Regulate that measured
            // operating point instead of lifting to recover the earlier approach speed during the hold.
            var holdSpeed = Mathf.Max(result.EntrySpeedKilometersPerHour, result.SpeedAtPhysicalBreakawayKilometersPerHour);
            var throttle = Mathf.Clamp(0.7f + (15f - Mathf.Abs(beta)) * 0.015f
                                      + (holdSpeed / 3.6f - speed) * 0.05f, 0.35f, 1f);
            if (policy == InputPolicy.KeyboardDutyCycle)
            {
                steering = DigitalSteeringRequest(driver, steering);
                var onFrames = Mathf.Clamp(Mathf.RoundToInt(throttle * 10f), 0, 10);
                throttle = frame % 10 < onFrames ? 1f : 0f;
            }
            return new VehicleInputState(throttle, 0f, steering, 0f);
        }

        private static float DigitalSteeringRequest(PrototypeInputDriver driver, float desiredSteering)
        {
            if (Mathf.Abs(desiredSteering) < 0.02f) return 0f;
            if (driver.SteeringInput * desiredSteering > 0f
                && Mathf.Abs(driver.SteeringInput) > Mathf.Abs(desiredSteering) + 0.05f) return 0f;
            return Mathf.Sign(desiredSteering);
        }

        private static bool MeasuredPhysicalBreakaway(RaycastVehicleController controller, Rigidbody body, float direction)
        {
            var beta = FullSideslipDegrees(body);
            if (Mathf.Abs(beta) < 5f || beta * direction >= 0f) return false;
            var wheels = controller.Telemetry.Wheels;
            return (wheels[2].Grounded && wheels[2].IsSliding) || (wheels[3].Grounded && wheels[3].IsSliding);
        }

        private static void RecordBreakaway(SlideCase result, SamplingContext context, Rigidbody body)
        {
            if (result.InitiatedPhysicalSlide) return;
            result.InitiatedPhysicalSlide = true;
            result.FirstPhysicalBreakawaySeconds = context.ElapsedSeconds;
            result.BodySideslipAtPhysicalBreakawayDegrees = FullSideslipDegrees(body);
            result.SpeedAtPhysicalBreakawayKilometersPerHour = body.velocity.magnitude * 3.6f;
        }

        private static void Advance(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body, VehicleInputState requested)
        {
            driver.ApplyInput(requested, InputSeconds);
            var steps = Mathf.RoundToInt(InputSeconds / PhysicsSeconds);
            for (var step = 0; step < steps; step++)
            {
                controller.SimulateStep(PhysicsSeconds);
                UnityEngine.Physics.Simulate(PhysicsSeconds);
                if (!Finite(body.position) || !Finite(body.velocity) || !Finite(body.angularVelocity)
                    || body.velocity.magnitude > 120f || body.angularVelocity.magnitude > 30f)
                    throw new InvalidOperationException("Slide mechanics produced nonfinite or unbounded motion.");
            }
        }

        private static void SampleFrame(PrototypeInputDriver driver, RaycastVehicleController controller, Rigidbody body,
            Transform pivot, PrototypeSkidTrails trails, VehicleInputState requested, PhaseResult phase,
            SlideCase result, SamplingContext context, int phaseFrame, bool measureHold)
        {
            Advance(driver, controller, body, requested);
            trails.SampleContacts();
            var telemetry = controller.Telemetry;
            var beta = FullSideslipDegrees(body);
            var yaw = body.rotation.eulerAngles.y;
            var speed = body.velocity.magnitude;
            var yawRate = YawRate(body);
            var angle = Mathf.DeltaAngle(0f, pivot.localEulerAngles.y);
            context.ElapsedSeconds += InputSeconds;
            result.AccumulatedHeadingDegrees += Mathf.DeltaAngle(context.PreviousYawDegrees, yaw);
            context.PreviousYawDegrees = yaw;
            result.MinimumUprightDot = Mathf.Min(result.MinimumUprightDot, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
            result.MinimumHeightMeters = Mathf.Min(result.MinimumHeightMeters, body.position.y);
            result.MaximumHeightMeters = Mathf.Max(result.MaximumHeightMeters, body.position.y);
            phase.FrameCount++;
            phase.PeakBodySideslipDegrees = Mathf.Max(phase.PeakBodySideslipDegrees, Mathf.Abs(beta));
            phase.PeakYawRateDegreesPerSecond = Mathf.Max(phase.PeakYawRateDegreesPerSecond, Mathf.Abs(yawRate));
            phase.MinimumSpeedKilometersPerHour = Mathf.Min(phase.MinimumSpeedKilometersPerHour, speed * 3.6f);
            phase.MaximumHandbrakeCommand = Mathf.Max(phase.MaximumHandbrakeCommand, telemetry.Input.Handbrake);
            phase.TorqueScaleSum += telemetry.DeliveredDriveTorqueScale;
            if (telemetry.TractionControlActive) phase.TractionControlActiveFrames++;
            var rearSliding = false;
            var poweredRearSlip = false;
            for (var index = 0; index < telemetry.Wheels.Length; index++)
            {
                var wheel = telemetry.Wheels[index];
                if (!wheel.Grounded) continue;
                result.RoadContactWheelSamples++;
                if (wheel.SurfaceName != "Dry asphalt") result.UnknownContactWheelSamples++;
                var tire = controller.Definition.Tire;
                var limit = wheel.NormalLoadNewtons * tire.PeakDryFrictionCoefficient * wheel.SurfaceGripMultiplier
                            * Math.Pow(Math.Max(wheel.NormalLoadNewtons / tire.ReferenceLoadNewtons, 0.05d), tire.LoadSensitivityExponent);
                var magnitude = Math.Sqrt(wheel.LongitudinalForceNewtons * wheel.LongitudinalForceNewtons
                                          + wheel.LateralForceNewtons * wheel.LateralForceNewtons);
                if (limit > 0.01d)
                    result.MaximumDeliveredForceEnvelopeUtilization = Mathf.Max(result.MaximumDeliveredForceEnvelopeUtilization, (float)(magnitude / limit));
                if (index < 2)
                {
                    if (wheel.IsSliding) phase.FrontSlidingWheelSamples++;
                    phase.PeakFrontSlipAngleDegrees = Mathf.Max(phase.PeakFrontSlipAngleDegrees, Mathf.Abs(wheel.SlipAngleDegrees));
                }
                else
                {
                    if (wheel.IsSliding) phase.RearSlidingWheelSamples++;
                    phase.PeakRearSlipAngleDegrees = Mathf.Max(phase.PeakRearSlipAngleDegrees, Mathf.Abs(wheel.SlipAngleDegrees));
                    phase.PeakRearPositiveLongitudinalSlipRatio = Mathf.Max(phase.PeakRearPositiveLongitudinalSlipRatio, wheel.SlipRatio);
                    rearSliding |= wheel.IsSliding && wheel.NormalLoadNewtons > 20f;
                    poweredRearSlip |= wheel.IsSliding && wheel.NormalLoadNewtons > 20f
                                       && wheel.SlipRatio > 0.03f && wheel.DriveTorqueNewtonMeters > 0f;
                }
            }
            if (measureHold)
            {
                if (requested.Steering * result.Direction < 0f) result.HoldCountersteeringFrames++;
                if (telemetry.Input.Steering * result.Direction < 0f) result.HoldAppliedCountersteeringFrames++;
                var controlled = Mathf.Abs(beta) >= 5f && Mathf.Abs(beta) <= 35f && beta * result.Direction < 0f
                                 && speed * 3.6f > result.EntrySpeedKilometersPerHour * 0.5f
                                 && telemetry.GroundedWheelCount >= 3 && rearSliding
                                 && yawRate * result.Direction > 2f && Mathf.Abs(yawRate) < 150f;
                if (controlled)
                {
                    context.ControlledFrames++;
                    result.ControlledSlideTotalSeconds += InputSeconds;
                    if (poweredRearSlip) result.PoweredRearSlipDuringControlledSlideSeconds += InputSeconds;
                    result.LongestControlledSlideSeconds = Mathf.Max(result.LongestControlledSlideSeconds, context.ControlledFrames * InputSeconds);
                }
                else context.ControlledFrames = 0;
            }
            if (phaseFrame % 3 != 0) return;
            var trace = new TraceSample
            {
                Seconds = context.ElapsedSeconds, Phase = phase.Name, AssistPhase = telemetry.AssistPhase.ToString(),
                RequestedThrottle = requested.Throttle, RequestedSteering = requested.Steering,
                ActualThrottle = telemetry.Input.Throttle, ActualSteering = telemetry.Input.Steering,
                Handbrake = telemetry.Input.Handbrake, AppliedSteeringDegrees = angle,
                SpeedKilometersPerHour = speed * 3.6f, BodySideslipDegrees = beta, YawRateDegreesPerSecond = yawRate,
                ForwardGear = telemetry.ForwardGear, EngineSpeedRpm = telemetry.EngineSpeedRpm,
                AccumulatedHeadingDegrees = result.AccumulatedHeadingDegrees,
                TractionControlActive = telemetry.TractionControlActive, DeliveredDriveTorqueScale = telemetry.DeliveredDriveTorqueScale,
                FrontDifferentialTransferTorqueNewtonMeters = telemetry.FrontDifferentialTransferTorqueNewtonMeters,
                RearDifferentialTransferTorqueNewtonMeters = telemetry.RearDifferentialTransferTorqueNewtonMeters,
                Wheels = new WheelSample[telemetry.Wheels.Length]
            };
            for (var index = 0; index < telemetry.Wheels.Length; index++)
            {
                var wheel = telemetry.Wheels[index];
                trace.Wheels[index] = new WheelSample
                {
                    Grounded = wheel.Grounded, NormalLoadNewtons = wheel.NormalLoadNewtons,
                    SlipRatio = wheel.SlipRatio, SlipAngleDegrees = wheel.SlipAngleDegrees,
                    AngularSpeedRadiansPerSecond = wheel.AngularSpeedRadiansPerSecond, DriveTorqueNewtonMeters = wheel.DriveTorqueNewtonMeters,
                    LongitudinalForceNewtons = wheel.LongitudinalForceNewtons, LateralForceNewtons = wheel.LateralForceNewtons,
                    Sliding = wheel.IsSliding
                };
            }
            result.Trace.Add(trace);
        }

        private static float FullSideslipDegrees(Rigidbody body)
        {
            var local = Quaternion.Inverse(body.rotation) * body.velocity;
            return local.sqrMagnitude > 0.01f ? Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg : 0f;
        }
        private static float YawRate(Rigidbody body) => Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static void Check(bool condition, string message, SlideReport report)
        {
            report.Assertions++;
            if (!condition) report.Failures.Add(message);
        }
        private enum InputPolicy { VirtualDriver, IdenticalOpenLoop, KeyboardDutyCycle, PowerOversteer }
        private sealed class SamplingContext { public float ElapsedSeconds; public float PreviousYawDegrees; public int ControlledFrames; }

        [Serializable]
        private sealed class SlideReport
        {
            public bool Passed;
            public bool Exploratory = false;
            public string GeneratedUtc;
            public string UnityVersion;
            public int Assertions;
            public int PhysicalSlideOutcomes;
            public string Scope = "60 Hz production input driver / 360 Hz native physics. Launch uses Road for matched rolling states. VirtualDriver is automated bounded sideslip/yaw/pedal feedback; PowerOversteer initiates through engine torque with no handbrake and hands control over at measured physical breakaway; KeyboardDutyCycle requests only 0/1 throttle and -1/0/1 steering. IdenticalOpenLoop uses the same commands in Road and Sport. Stock/touring 50/70 virtual-driver and power-only cases plus all four stock 50 keyboard cases require full physical-slide outcomes. Sustained sliding counts physical sideslip/contact/yaw continuity through pedal feathering, with powered rear slip measured separately. Diagnostic cases retain explicit outcome failures. Full 20 Hz traces are saved separately to slide-validation-traces.json.";
            public List<SlideCase> Cases = new List<SlideCase>();
            public List<string> Failures = new List<string>();
        }
        [Serializable]
        private sealed class SlideCase
        {
            public string Name;
            public string WheelPackageKey;
            public string AssistMode;
            public string InputPolicy;
            public string RearDifferentialType;
            public bool CommandsIdenticalAcrossAssistModes;
            public float RequestedSpeedKilometersPerHour;
            public float EntrySpeedKilometersPerHour;
            public float Direction;
            public float AuthoredMaximumSteeringDegrees;
            public Vector3 StartPositionWorld;
            public float BodySideslipAtHandbrakeReleaseDegrees;
            public float YawAtHandbrakeReleaseDegreesPerSecond;
            public bool InitiatedPhysicalSlide;
            public bool SustainedPhysicalSlide;
            public bool HasPoweredSlideContinuation;
            public bool RecoveredPhysicalSlide;
            public bool DemonstratesPhysicalSlide;
            public bool RequiresPhysicalSlideAcceptance;
            public float FirstPhysicalBreakawaySeconds;
            public float BodySideslipAtPhysicalBreakawayDegrees;
            public float SpeedAtPhysicalBreakawayKilometersPerHour;
            public float LongestControlledSlideSeconds;
            public float ControlledSlideTotalSeconds;
            public float PoweredRearSlipDuringControlledSlideSeconds;
            public int HoldCountersteeringFrames;
            public int HoldAppliedCountersteeringFrames;
            public float ExitSpeedKilometersPerHour;
            public float ExitBodySideslipDegrees;
            public float ExitYawRateDegreesPerSecond;
            public float ExitMeanRearLongitudinalSlipRatio;
            public float TravelDistanceMeters;
            public float AccumulatedHeadingDegrees;
            public int SkidMarkSegmentCount;
            public int RoadContactWheelSamples;
            public int UnknownContactWheelSamples;
            public float MaximumDeliveredForceEnvelopeUtilization;
            public float MinimumUprightDot = 1f;
            public float MinimumHeightMeters = float.MaxValue;
            public float MaximumHeightMeters = float.MinValue;
            public List<PhaseResult> Phases = new List<PhaseResult>();
            public List<string> OutcomeFailures = new List<string>();
            [NonSerialized] public List<TraceSample> Trace = new List<TraceSample>();
        }
        [Serializable]
        private sealed class PhaseResult
        {
            public string Name;
            public float DurationSeconds;
            public int FrameCount;
            public float PeakBodySideslipDegrees;
            public float PeakYawRateDegreesPerSecond;
            public float MinimumSpeedKilometersPerHour = float.MaxValue;
            public float MaximumHandbrakeCommand;
            public float PeakFrontSlipAngleDegrees;
            public float PeakRearSlipAngleDegrees;
            public float PeakRearPositiveLongitudinalSlipRatio;
            public int FrontSlidingWheelSamples;
            public int RearSlidingWheelSamples;
            public int TractionControlActiveFrames;
            public float TractionControlActiveFraction;
            public float MeanDeliveredDriveTorqueScale;
            [NonSerialized] public double TorqueScaleSum;
        }
        [Serializable]
        private sealed class TraceSample
        {
            public float Seconds;
            public string Phase;
            public string AssistPhase;
            public float RequestedThrottle;
            public float RequestedSteering;
            public float ActualThrottle;
            public float ActualSteering;
            public float Handbrake;
            public float AppliedSteeringDegrees;
            public float SpeedKilometersPerHour;
            public float BodySideslipDegrees;
            public float YawRateDegreesPerSecond;
            public int ForwardGear;
            public float EngineSpeedRpm;
            public float AccumulatedHeadingDegrees;
            public bool TractionControlActive;
            public float DeliveredDriveTorqueScale;
            public float FrontDifferentialTransferTorqueNewtonMeters;
            public float RearDifferentialTransferTorqueNewtonMeters;
            public WheelSample[] Wheels;
        }
        [Serializable]
        private sealed class WheelSample
        {
            public bool Grounded;
            public float NormalLoadNewtons;
            public float SlipRatio;
            public float SlipAngleDegrees;
            public float AngularSpeedRadiansPerSecond;
            public float DriveTorqueNewtonMeters;
            public float LongitudinalForceNewtons;
            public float LateralForceNewtons;
            public bool Sliding;
        }
        [Serializable]
        private sealed class TraceReport
        {
            public string GeneratedUtc;
            public List<CaseTrace> Cases = new List<CaseTrace>();
        }
        [Serializable]
        private sealed class CaseTrace
        {
            public string Name;
            public List<TraceSample> Samples;
        }
    }
}
