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
    // Native drivetrain checks use the production driver and genuine rolling wheel states.
    public static class PrototypeGearValidation
    {
        private const float InputStep = 1f / 60f;
        private const float PhysicsStep = 1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;

        [MenuItem("MotorBound/Validate Gear Selection")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run gear validation outside Play mode.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified scenes before gear validation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousMode = UnityEngine.Physics.simulationMode;
            var previousGravity = UnityEngine.Physics.gravity;
            var previousIterations = UnityEngine.Physics.defaultSolverIterations;
            var previousVelocityIterations = UnityEngine.Physics.defaultSolverVelocityIterations;
            var report = new GearReport { GeneratedUtc = DateTime.UtcNow.ToString("o"), UnityVersion = Application.unityVersion };
            try
            {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                UnityEngine.Physics.gravity = new Vector3(0f, -9.80665f, 0f);
                UnityEngine.Physics.defaultSolverIterations = 12;
                UnityEngine.Physics.defaultSolverVelocityIterations = 4;
                RunCase("Manual second persists; reset retains manual mode", ManualSecond, report);
                RunCase("Reverse propulsion and service braking; moving forward guard", Reverse, report);
                RunCase("Neutral has no net axle drive torque", Neutral, report);
                RunCase("Moving reverse selection rejects without gear or mode change", MovingReverseGuard, report);
                RunCase("Automatic transmission still upshifts", Automatic, report);
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
                var path = Path.Combine(directory, "gear-validation.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log("MotorBound gear validation: " + path + "; passed=" + report.Passed);
            }
            if (!report.Passed)
                throw new InvalidOperationException("Gear validation failed: " + string.Join(" | ", report.Failures));
        }

        private static void RunCase(string name, Action<Rig, GearCase, GearReport> test, GearReport report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var road = new GameObject("Gear validation dry asphalt", typeof(BoxCollider), typeof(SurfaceGrip));
            road.transform.position = new Vector3(0f, -0.12f, 0f);
            road.GetComponent<BoxCollider>().size = new Vector3(1000f, 0.24f, 1000f);
            road.GetComponent<SurfaceGrip>().Configure(1f, "Dry asphalt", 0f, 0f);
            var car = new GameObject("Gear validation Kiyora Aven", typeof(Rigidbody));
            car.transform.position = new Vector3(0f, 0.92f, 0f);
            PrototypeBootstrap.AddReferenceBody(car);
            var controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            controller.AssistMode = DriverAssistMode.Road;
            var driver = car.AddComponent<PrototypeInputDriver>();
            driver.Configure(controller);
            var rig = new Rig { Controller = controller, Driver = driver, Body = car.GetComponent<Rigidbody>() };
            UnityEngine.Physics.SyncTransforms();
            for (var frame = 0; frame < 60; frame++) Advance(rig, default(VehicleInputState));
            var result = new GearCase { Name = name, InitialSelectedGear = controller.SelectedGear, InitialMode = controller.TransmissionMode.ToString() };
            report.Cases.Add(result);
            var failuresBefore = report.Failures.Count;
            try { test(rig, result, report); }
            catch (Exception exception) { report.Failures.Add(name + ": " + exception); }
            result.FinalSelectedGear = controller.SelectedGear;
            result.FinalMode = controller.TransmissionMode.ToString();
            result.FinalSelectionMessage = controller.GearSelectionMessage;
            result.Passed = failuresBefore == report.Failures.Count;
        }

        private static void ManualSecond(Rig rig, GearCase result, GearReport report)
        {
            Check(rig.Controller.TransmissionMode == DriveTransmissionMode.Automatic && rig.Controller.SelectedGear == 1,
                result, "new vehicle starts in automatic first gear", report);
            Check(Action(rig, result, "Toggle manual", rig.Driver.ToggleTransmissionMode), result, "manual selection accepted", report);
            Check(Action(rig, result, "Shift up to second", rig.Driver.ShiftUp), result, "first-to-second shift accepted", report);
            Check(rig.Controller.TransmissionMode == DriveTransmissionMode.Manual && rig.Controller.SelectedGear == 2,
                result, "selected second gear is manual", report);
            var phase = Drive(rig, result, "Manual second under W", 480, new VehicleInputState(1f, 0f, 0f, 0f));
            Check(phase.MinimumSelectedGear == 2 && phase.MaximumSelectedGear == 2 && phase.ManualFrames == phase.Frames,
                result, "manual second persists under throttle without automatic shifting", report);
            Check(phase.EndForwardSpeedMetersPerSecond > 2f && phase.PeakPositiveRearAxleDriveTorqueNewtonMeters > 1f,
                result, "manual second actually drives the vehicle", report);
            // Reset behavior is explicitly exercised, not used to manufacture a rolling entry state.
            rig.Controller.ResetMotion();
            result.ResetSelectedGear = rig.Controller.SelectedGear;
            result.ResetMode = rig.Controller.TransmissionMode.ToString();
            Check(result.ResetSelectedGear == 1 && rig.Controller.TransmissionMode == DriveTransmissionMode.Manual,
                result, "motion reset returns to first and retains manual mode", report);
            Check(rig.Body.velocity.sqrMagnitude < 0.0001f && rig.Body.angularVelocity.sqrMagnitude < 0.0001f,
                result, "motion reset clears body movement", report);
        }

        private static void Reverse(Rig rig, GearCase result, GearReport report)
        {
            Check(Action(rig, result, "Select stopped reverse", rig.Driver.ToggleReverse), result, "reverse selection accepted while stopped", report);
            Check(rig.Controller.SelectedGear == -1, result, "reverse gear is selected", report);
            var reverse = Drive(rig, result, "Reverse W propulsion", 240, new VehicleInputState(1f, 0f, 0f, 0f));
            Check(reverse.EndForwardSpeedMetersPerSecond < -2f && reverse.ForwardDisplacementMeters < -2f,
                result, "W in reverse physically propels the vehicle backwards", report);
            Check(reverse.ReversePoweredContactFrames > 3 && reverse.MinimumRearAngularSpeedRadiansPerSecond < -0.1f,
                result, "loaded reverse drive has negative rear wheel speed, negative slip and negative axle torque together", report);
            var priorGear = rig.Controller.SelectedGear;
            var priorMode = rig.Controller.TransmissionMode;
            Check(!Action(rig, result, "Reject moving reverse-to-forward", rig.Driver.ToggleReverse),
                result, "moving reverse-to-forward direction change rejected", report);
            Check(rig.Controller.SelectedGear == priorGear && rig.Controller.TransmissionMode == priorMode,
                result, "rejected reverse-to-forward request changes neither gear nor mode", report);
            var brake = Drive(rig, result, "Service brake opposes reverse", 120, new VehicleInputState(0f, 1f, 0f, 0f));
            Check(Mathf.Abs(brake.EndForwardSpeedMetersPerSecond) < Mathf.Abs(brake.StartForwardSpeedMetersPerSecond) * 0.5f,
                result, "service braking reduces reverse travel speed by at least half", report);
            Check(brake.MeanSignedLongitudinalTireForceNewtons > 5f,
                result, "service-brake contact force is forward, opposing backwards travel", report);
        }

        private static void Neutral(Rig rig, GearCase result, GearReport report)
        {
            Check(Action(rig, result, "Shift down first-to-neutral", rig.Driver.ShiftDown), result, "neutral selection accepted", report);
            Check(rig.Controller.SelectedGear == 0, result, "neutral gear is selected", report);
            var phase = Drive(rig, result, "Neutral under W", 240, new VehicleInputState(1f, 0f, 0f, 0f));
            Check(phase.MinimumSelectedGear == 0 && phase.MaximumSelectedGear == 0, result, "neutral persists under throttle", report);
            Check(phase.PeakAbsoluteFrontAxleDriveTorqueNewtonMeters < 0.01f && phase.PeakAbsoluteRearAxleDriveTorqueNewtonMeters < 0.01f,
                result, "neutral transmits zero net drive torque to both axles (passive internal LSD transfers cancel)", report);
            Check(Mathf.Abs(phase.EndForwardSpeedMetersPerSecond) < 0.15f && Mathf.Abs(phase.ForwardDisplacementMeters) < 0.2f,
                result, "neutral throttle does not propel the vehicle", report);
        }

        private static void MovingReverseGuard(Rig rig, GearCase result, GearReport report)
        {
            var phase = Drive(rig, result, "Forward W before reverse request", 180, new VehicleInputState(1f, 0f, 0f, 0f));
            Check(phase.EndForwardSpeedMetersPerSecond > 2f, result, "direction-guard test starts genuinely moving forwards", report);
            var priorGear = rig.Controller.SelectedGear;
            var priorMode = rig.Controller.TransmissionMode;
            Check(!Action(rig, result, "Reject moving forward-to-reverse", rig.Driver.ToggleReverse),
                result, "reverse selection rejected while moving forwards", report);
            Check(rig.Controller.SelectedGear == priorGear && rig.Controller.TransmissionMode == priorMode,
                result, "rejected reverse request changes neither forward gear nor transmission mode", report);
        }

        private static void Automatic(Rig rig, GearCase result, GearReport report)
        {
            var phase = Drive(rig, result, "Automatic W acceleration", 720, new VehicleInputState(1f, 0f, 0f, 0f));
            Check(phase.MaximumSelectedGear > 1 && phase.AutomaticFrames == phase.Frames,
                result, "automatic mode still physically upshifts under sustained throttle", report);
            Check(phase.EndForwardSpeedMetersPerSecond > 10f,
                result, "automatic acceleration remains capable of normal road speed", report);
        }

        private static bool Action(Rig rig, GearCase result, string name, Func<bool> action)
        {
            var accepted = action();
            result.Actions.Add(new GearAction { Name = name, Accepted = accepted, SelectedGear = rig.Controller.SelectedGear,
                Mode = rig.Controller.TransmissionMode.ToString(), Message = rig.Controller.GearSelectionMessage,
                ForwardSpeedMetersPerSecond = ForwardSpeed(rig.Body) });
            return accepted;
        }

        private static GearPhase Drive(Rig rig, GearCase result, string name, int frames, VehicleInputState input)
        {
            var start = rig.Body.position;
            var phase = new GearPhase { Name = name, Frames = frames, Seconds = frames * InputStep,
                Throttle = input.Throttle, Brake = input.Brake, StartForwardSpeedMetersPerSecond = ForwardSpeed(rig.Body) };
            result.Phases.Add(phase);
            for (var frame = 0; frame < frames; frame++)
            {
                Advance(rig, input);
                var telemetry = rig.Controller.Telemetry;
                var wheels = telemetry.Wheels;
                var frontTorque = wheels[0].DriveTorqueNewtonMeters + wheels[1].DriveTorqueNewtonMeters;
                var rearTorque = wheels[2].DriveTorqueNewtonMeters + wheels[3].DriveTorqueNewtonMeters;
                phase.MinimumSelectedGear = Math.Min(phase.MinimumSelectedGear, rig.Controller.SelectedGear);
                phase.MaximumSelectedGear = Math.Max(phase.MaximumSelectedGear, rig.Controller.SelectedGear);
                if (rig.Controller.TransmissionMode == DriveTransmissionMode.Manual) phase.ManualFrames++;
                else phase.AutomaticFrames++;
                phase.PeakEngineSpeedRpm = Mathf.Max(phase.PeakEngineSpeedRpm, telemetry.EngineSpeedRpm);
                phase.PeakAbsoluteFrontAxleDriveTorqueNewtonMeters = Mathf.Max(phase.PeakAbsoluteFrontAxleDriveTorqueNewtonMeters, Mathf.Abs(frontTorque));
                phase.PeakAbsoluteRearAxleDriveTorqueNewtonMeters = Mathf.Max(phase.PeakAbsoluteRearAxleDriveTorqueNewtonMeters, Mathf.Abs(rearTorque));
                phase.PeakPositiveRearAxleDriveTorqueNewtonMeters = Mathf.Max(phase.PeakPositiveRearAxleDriveTorqueNewtonMeters, rearTorque);
                phase.MinimumRearAngularSpeedRadiansPerSecond = Mathf.Min(phase.MinimumRearAngularSpeedRadiansPerSecond,
                    Mathf.Min(wheels[2].AngularSpeedRadiansPerSecond, wheels[3].AngularSpeedRadiansPerSecond));
                phase.MinimumRearSlipRatio = Mathf.Min(phase.MinimumRearSlipRatio, Mathf.Min(wheels[2].SlipRatio, wheels[3].SlipRatio));
                if (wheels[2].Grounded && wheels[3].Grounded && wheels[2].NormalLoadNewtons > 20f && wheels[3].NormalLoadNewtons > 20f
                    && wheels[2].AngularSpeedRadiansPerSecond < -0.1f && wheels[3].AngularSpeedRadiansPerSecond < -0.1f
                    && wheels[2].SlipRatio < -0.001f && wheels[3].SlipRatio < -0.001f && rearTorque < -0.01f)
                    phase.ReversePoweredContactFrames++;
                foreach (var wheel in wheels)
                    phase.LongitudinalForceSum += wheel.LongitudinalForceNewtons;
                if (telemetry.GroundedWheelCount < 3) phase.FramesWithFewerThanThreeContacts++;
                if (frame == 0 || frame == frames / 2 || frame == frames - 1)
                    phase.Snapshots.Add(new GearSnapshot { Seconds = (frame + 1) * InputStep, SelectedGear = rig.Controller.SelectedGear,
                        Mode = rig.Controller.TransmissionMode.ToString(), ForwardSpeedMetersPerSecond = ForwardSpeed(rig.Body),
                        EngineSpeedRpm = telemetry.EngineSpeedRpm, RearAxleDriveTorqueNewtonMeters = rearTorque,
                        RearLeftAngularSpeedRadiansPerSecond = wheels[2].AngularSpeedRadiansPerSecond,
                        RearRightAngularSpeedRadiansPerSecond = wheels[3].AngularSpeedRadiansPerSecond,
                        RearLeftSlipRatio = wheels[2].SlipRatio, RearRightSlipRatio = wheels[3].SlipRatio });
            }
            phase.EndForwardSpeedMetersPerSecond = ForwardSpeed(rig.Body);
            phase.ForwardDisplacementMeters = rig.Body.position.z - start.z;
            phase.MeanSignedLongitudinalTireForceNewtons = (float)(phase.LongitudinalForceSum / frames);
            return phase;
        }

        private static void Advance(Rig rig, VehicleInputState input)
        {
            rig.Driver.ApplyInput(input, InputStep);
            for (var step = 0; step < 6; step++)
            {
                rig.Controller.SimulateStep(PhysicsStep);
                UnityEngine.Physics.Simulate(PhysicsStep);
                if (!Finite(rig.Body.position) || !Finite(rig.Body.velocity) || !Finite(rig.Body.angularVelocity)
                    || rig.Body.velocity.magnitude > 100f || rig.Body.angularVelocity.magnitude > 30f
                    || Mathf.Abs(rig.Body.position.x) > 450f || Mathf.Abs(rig.Body.position.z) > 450f)
                    throw new InvalidOperationException("Gear validation exceeded finite, bounded physics limits.");
            }
        }
        private static float ForwardSpeed(Rigidbody body) => (Quaternion.Inverse(body.rotation) * body.velocity).z;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static void Check(bool condition, GearCase result, string message, GearReport report)
        {
            report.Assertions++;
            if (!condition) report.Failures.Add(result.Name + ": " + message + ".");
        }
        private sealed class Rig { public RaycastVehicleController Controller; public PrototypeInputDriver Driver; public Rigidbody Body; }
        [Serializable] private sealed class GearReport
        {
            public string GeneratedUtc, UnityVersion;
            public int SimulationHertz = RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz;
            public int InputHertz = 60;
            public string Scope = "Five stock-vehicle native cases on dry asphalt, one-second settlement, production driver commands and natural acceleration. ResetMotion is tested explicitly, not used to manufacture rolling states.";
            public bool Passed = false;
            public int Assertions;
            public List<GearCase> Cases = new List<GearCase>();
            public List<string> Failures = new List<string>();
        }
        [Serializable] private sealed class GearCase
        {
            public string Name, InitialMode, FinalMode, FinalSelectionMessage, ResetMode;
            public int InitialSelectedGear, FinalSelectedGear, ResetSelectedGear;
            public bool Passed = false;
            public List<GearAction> Actions = new List<GearAction>();
            public List<GearPhase> Phases = new List<GearPhase>();
        }
        [Serializable] private sealed class GearAction
        {
            public string Name, Mode, Message;
            public bool Accepted;
            public int SelectedGear;
            public float ForwardSpeedMetersPerSecond;
        }
        [Serializable] private sealed class GearPhase
        {
            public string Name;
            public int Frames, ManualFrames, AutomaticFrames, ReversePoweredContactFrames, FramesWithFewerThanThreeContacts;
            public int MinimumSelectedGear = int.MaxValue, MaximumSelectedGear = int.MinValue;
            public float Seconds, Throttle, Brake, StartForwardSpeedMetersPerSecond, EndForwardSpeedMetersPerSecond, ForwardDisplacementMeters;
            public float PeakEngineSpeedRpm, PeakAbsoluteFrontAxleDriveTorqueNewtonMeters, PeakAbsoluteRearAxleDriveTorqueNewtonMeters;
            public float PeakPositiveRearAxleDriveTorqueNewtonMeters, MinimumRearAngularSpeedRadiansPerSecond, MinimumRearSlipRatio;
            public float MeanSignedLongitudinalTireForceNewtons;
            [NonSerialized] public double LongitudinalForceSum;
            public List<GearSnapshot> Snapshots = new List<GearSnapshot>();
        }
        [Serializable] private sealed class GearSnapshot
        {
            public string Mode;
            public int SelectedGear;
            public float Seconds, ForwardSpeedMetersPerSecond, EngineSpeedRpm, RearAxleDriveTorqueNewtonMeters;
            public float RearLeftAngularSpeedRadiansPerSecond, RearRightAngularSpeedRadiansPerSecond, RearLeftSlipRatio, RearRightSlipRatio;
        }
    }
}
