using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;
using NUnit.Framework;
using UnityEngine;

namespace MotorBound.Product.Tests
{
    public sealed class PrototypeGarageIntegrationTests
    {
        private readonly List<GameObject> ownedVehicles = new List<GameObject>();
        private string testDirectory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "MotorBoundGarageTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            savePath = Path.Combine(testDirectory, "garage.json");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var vehicle in ownedVehicles)
            {
                if (vehicle != null) UnityEngine.Object.DestroyImmediate(vehicle);
            }
            ownedVehicles.Clear();

            // Only files created in this test's unique directory are eligible for cleanup.
            if (testDirectory != null && Directory.Exists(testDirectory))
            {
                foreach (var file in Directory.GetFiles(testDirectory)) File.Delete(file);
                Directory.Delete(testDirectory);
            }
        }

        [TestCase(-1f, 30)]
        [TestCase(1f, 30)]
        [TestCase(-1f, 60)]
        [TestCase(1f, 60)]
        [TestCase(-1f, 144)]
        [TestCase(1f, 144)]
        public void KeyboardSteering_ReleaseCentersWithinOneTenthSecond(float direction, int inputFrequency)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            var step = 1f / inputFrequency;
            for (var frame = 0; frame < inputFrequency; frame++)
                driver.ApplyInput(new VehicleInputState(0f, 0f, direction, 0f), step);
            Assert.That(driver.SteeringInput, Is.EqualTo(direction));

            var elapsed = 0f;
            while (driver.SteeringInput != 0f && elapsed < 1f)
            {
                driver.ApplyInput(default(VehicleInputState), step);
                elapsed += step;
                Assert.That(driver.SteeringInput * direction, Is.InRange(0f, 1f), "Centering must never countersteer.");
            }
            Assert.That(elapsed, Is.LessThanOrEqualTo(0.10001f));
        }

        [Test]
        public void KeyboardSteering_PressRemainsProgressiveAndGarageClearsSteering()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            driver.ApplyInput(new VehicleInputState(0f, 0f, 1f, 0f), 0.1f);
            Assert.That(driver.SteeringInput, Is.EqualTo(0.2f).Within(0.00001f));
            driver.DrivingEnabled = false;
            driver.ApplyInput(new VehicleInputState(1f, 0f, 1f, 0f), 0.1f);
            Assert.That(driver.SteeringInput, Is.Zero);
            driver.DrivingEnabled = true;
            driver.ApplyInput(default(VehicleInputState), 0.1f);
            Assert.That(driver.SteeringInput, Is.Zero);
        }

        [TestCase(-1f, 30)]
        [TestCase(1f, 30)]
        [TestCase(-1f, 60)]
        [TestCase(1f, 60)]
        [TestCase(-1f, 144)]
        [TestCase(1f, 144)]
        public void KeyboardSteering_HeldKeyReachesFullRangeInHalfSecond(float direction, int inputFrequency)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            for (var frame = 0; frame < inputFrequency / 2; frame++)
                driver.ApplyInput(new VehicleInputState(1f, 0f, direction, 0f), 1f / inputFrequency);
            Assert.That(driver.SteeringInput, Is.EqualTo(direction).Within(0.00001f));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void KeyboardSteering_CountersteerUnwindsOldTurnPromptly(float direction)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            driver.ApplyInput(new VehicleInputState(0f, 0f, direction, 0f), 1f);
            driver.ApplyInput(new VehicleInputState(0f, 0f, -direction, 0f), 0.1f);
            Assert.That(driver.SteeringInput * direction, Is.LessThan(0f),
                "Countersteering must not retain the old turn for the slower half-second press ramp.");
            Assert.That(Mathf.Abs(driver.SteeringInput), Is.EqualTo(1f / 30f).Within(0.00001f));
        }

        [Test]
        public void RoadTractionControl_IsVisibleSwitchableAndRetainedDuringReconfiguration()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var configuration = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState());
            controller.Configure(configuration.Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            Assert.That(controller.RoadTractionControlEnabled, Is.True);
            driver.DrivingEnabled = false;
            driver.ToggleRoadTractionControl();
            Assert.That(controller.RoadTractionControlEnabled, Is.True, "Garage controls must not change driving preferences.");
            driver.DrivingEnabled = true;
            driver.ToggleRoadTractionControl();
            Assert.That(controller.RoadTractionControlEnabled, Is.False);
            controller.Configure(configuration.Vehicle);
            Assert.That(controller.RoadTractionControlEnabled, Is.False);
            driver.ToggleRoadTractionControl();
            Assert.That(controller.RoadTractionControlEnabled, Is.True);
        }

        [Test]
        public void DriverAssistModes_DefaultSportCycleExplicitlyAndSurviveGarageReconfiguration()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var configuration = PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState());
            controller.Configure(configuration.Vehicle);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Sport));
            driver.DrivingEnabled = false;
            driver.CycleDriverAssistMode();
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Sport));
            driver.DrivingEnabled = true;
            driver.CycleDriverAssistMode();
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Off));
            controller.Configure(configuration.Vehicle);
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Off));
            driver.CycleDriverAssistMode();
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Road));
            driver.CycleDriverAssistMode();
            Assert.That(controller.AssistMode, Is.EqualTo(DriverAssistMode.Sport));
        }

        [TestCase(DriveLayout.FrontWheelDrive)]
        [TestCase(DriveLayout.RearWheelDrive)]
        [TestCase(DriveLayout.AllWheelDrive)]
        public void CoupledAxles_DeliverPropulsionOnlyToTheDeclaredDrivenWheels(DriveLayout layout)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.DriveLayout = layout;
            controller.Configure(definition);
            controller.AssistMode = DriverAssistMode.Off;
            controller.SetInput(new VehicleInputState(1f, 0f, 0f, 0f));
            controller.SimulateStep(1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz);
            var actual = controller.Telemetry;
            var engineTorque = definition.Engine.EvaluateFullLoadTorqueNewtonMeters(actual.EngineSpeedRpm);
            var requestedTotal = engineTorque * definition.Transmission.ForwardGearRatios[actual.ForwardGear - 1]
                * definition.Transmission.FinalDriveRatio * definition.Transmission.Efficiency;
            Assert.That(actual.Wheels.Sum(wheel => wheel.DriveTorqueNewtonMeters),
                Is.EqualTo(requestedTotal).Within(0.001d), "Internal differential transfer cannot create axle torque.");
            for (var index = 0; index < actual.Wheels.Length; index++)
            {
                var driven = layout == DriveLayout.AllWheelDrive
                    || (layout == DriveLayout.FrontWheelDrive && index < 2)
                    || (layout == DriveLayout.RearWheelDrive && index >= 2);
                Assert.That(actual.Wheels[index].DriveTorqueNewtonMeters > 0f, Is.EqualTo(driven));
                Assert.That(actual.Wheels[index].AngularSpeedRadiansPerSecond > 0f, Is.EqualTo(driven));
            }
        }

        [Test]
        public void ManualGearControls_AreGarageGuardedAndPersistModeAcrossReset()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            controller.Configure(definition);
            var driver = vehicle.GetComponent<PrototypeInputDriver>();
            driver.DrivingEnabled = false;
            Assert.That(driver.ShiftUp() || driver.ShiftDown() || driver.ToggleReverse() || driver.ToggleTransmissionMode(), Is.False);
            Assert.That(controller.SelectedGear, Is.EqualTo(1));
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Automatic));
            driver.DrivingEnabled = true;
            Assert.That(driver.ShiftUp(), Is.True);
            Assert.That(controller.SelectedGear, Is.EqualTo(2));
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Manual));
            controller.ResetMotion();
            Assert.That(controller.SelectedGear, Is.EqualTo(1));
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Manual));
            controller.Configure(definition);
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Manual));
            Assert.That(driver.ToggleTransmissionMode(), Is.True);
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Automatic));
        }

        [Test]
        public void ReverseRequestAtSpeed_DoesNotMutateGearOrMode()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype());
            vehicle.GetComponent<Rigidbody>().velocity = new Vector3(10f, 0f, 0f);
            Assert.That(vehicle.GetComponent<PrototypeInputDriver>().ToggleReverse(), Is.False);
            Assert.That(controller.SelectedGear, Is.EqualTo(1));
            Assert.That(controller.TransmissionMode, Is.EqualTo(DriveTransmissionMode.Automatic));
            Assert.That(controller.GearSelectionMessage, Does.Contain("Stop"));
        }

        [TestCase(DriveLayout.FrontWheelDrive)]
        [TestCase(DriveLayout.RearWheelDrive)]
        [TestCase(DriveLayout.AllWheelDrive)]
        public void ReversePropulsionUsesNegativeTorqueAndNeutralDeliversNoNetDrive(DriveLayout layout)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.DriveLayout = layout;
            controller.Configure(definition);
            Assert.That(controller.TryToggleReverse(), Is.True);
            controller.SetInput(new VehicleInputState(1f, 0f, 0f, 0f));
            controller.SimulateStep(1f / 360f);
            var telemetry = controller.Telemetry;
            var expected = definition.Engine.EvaluateFullLoadTorqueNewtonMeters(telemetry.EngineSpeedRpm)
                * definition.Transmission.ReverseGearRatio * definition.Transmission.FinalDriveRatio * definition.Transmission.Efficiency;
            Assert.That(telemetry.Wheels.Sum(wheel => wheel.DriveTorqueNewtonMeters), Is.EqualTo(expected).Within(0.001d));
            Assert.That(telemetry.TractionControlActive, Is.False);
            Assert.That(controller.TryShiftGear(1), Is.True);
            Assert.That(controller.SelectedGear, Is.EqualTo(0));
            controller.SimulateStep(1f / 360f);
            Assert.That(controller.Telemetry.Wheels.Sum(wheel => wheel.DriveTorqueNewtonMeters), Is.EqualTo(0f).Within(0.001f));
            Assert.That(controller.Telemetry.EngineRevLimiterActive, Is.False);
        }

        [Test]
        public void ManualFirstGear_CutsPropulsionAtRedlineInsteadOfApplyingInfiniteTorque()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype());
            Assert.That(controller.ToggleTransmissionMode(), Is.True);
            controller.SetInput(new VehicleInputState(1f, 0f, 0f, 0f));
            for (var index = 0; index < 180; index++) controller.SimulateStep(1f / 360f);
            Assert.That(controller.SelectedGear, Is.EqualTo(1));
            Assert.That(controller.EngineRevLimiterActive, Is.True);
            Assert.That(controller.Telemetry.Wheels.Sum(wheel => wheel.DriveTorqueNewtonMeters), Is.EqualTo(0f).Within(0.001f));
            Assert.That(controller.TryShiftGear(1), Is.True);
            controller.SimulateStep(1f / 360f);
            Assert.That(controller.EngineRevLimiterActive, Is.False);
            Assert.That(controller.TryShiftGear(-1), Is.False);
            Assert.That(controller.SelectedGear, Is.EqualTo(2));
            Assert.That(controller.GearSelectionMessage, Does.Contain("redline"));
        }

        [TestCase(-1, "Gear R")]
        [TestCase(0, "Gear N")]
        [TestCase(6, "Gear 6")]
        public void HudGearLabels_AreExplicit(int gear, string expected)
        {
            Assert.That(PrototypeTelemetryHud.GearLabel(gear), Is.EqualTo(expected));
        }

        [Test]
        public void SaveLoad_RetainsAssemblyIdentityRevisionAndCompiledConfiguration()
        {
            var state = CreateTouringState();
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, state, out var saved), Is.True, saved);
            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var restored, out var loaded), Is.True, loaded);

            AssertSameAssembly(state, restored);
            var configuration = PrototypeGarageCatalog.Compile(restored);
            Assert.That(configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.TouringKey));
            Assert.That(configuration.HasCompanionKit, Is.True);
            Assert.That(configuration.Vehicle.MassKilograms, Is.EqualTo(state.Manifest.ComputeAuthoritativeMassKilograms()));
            Assert.That(configuration.Envelope.SourceManifestRevision, Is.EqualTo(state.Manifest.Revision));
            Assert.That(Directory.GetFiles(testDirectory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void CorruptSave_IsRefusedAndNeverOverwrittenByNewState()
        {
            var invalidBytes = System.Text.Encoding.UTF8.GetBytes("{ not valid JSON");
            File.WriteAllBytes(savePath, invalidBytes);

            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var restored, out _), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, PrototypeGarageCatalog.CreateNewState(), out _), Is.False);
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(invalidBytes));
            Assert.That(File.Exists(savePath + ".bak"), Is.False);
        }

        [Test]
        public void UnsupportedSchema_IsRefusedAndNeverOverwrittenByNewState()
        {
            var futureState = CreateTouringState();
            futureState.SchemaVersion = 999;
            File.WriteAllText(savePath, JsonUtility.ToJson(futureState, true));
            var originalBytes = File.ReadAllBytes(savePath);

            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var restored, out var message), Is.False);
            Assert.That(restored, Is.Null);
            StringAssert.Contains("schema", message.ToLowerInvariant());
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, PrototypeGarageCatalog.CreateNewState(), out _), Is.False);
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalBytes));
            Assert.That(File.Exists(savePath + ".bak"), Is.False);
        }

        [TestCase("SchemaVersion")]
        [TestCase("Revision")]
        [TestCase("PartDefinitionRevision")]
        public void MissingRequiredVersion_IsRefusedAndExistingBytesPreserved(string fieldName)
        {
            var json = JsonUtility.ToJson(CreateTouringState(), true);
            var field = new Regex("\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\\d+\\s*,?");
            Assert.That(field.IsMatch(json), Is.True, "The fixture must contain the requested version field.");
            File.WriteAllText(savePath, field.Replace(json, string.Empty, 1));
            var originalBytes = File.ReadAllBytes(savePath);

            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var restored, out _), Is.False,
                "An omitted required version must not silently acquire a supported default.");
            Assert.That(restored, Is.Null);
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, PrototypeGarageCatalog.CreateNewState(), out _), Is.False);
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(originalBytes));
        }

        [Test]
        public void ReplacingSave_PreservesPreviousValidAssemblyAsBackup()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, state, out var firstMessage), Is.True, firstMessage);
            var firstBytes = File.ReadAllBytes(savePath);
            var firstRevision = state.Manifest.Revision;
            InstallTouring(state);
            Assert.That(PrototypeGarageSaveStore.TrySave(savePath, state, out var secondMessage), Is.True, secondMessage);

            Assert.That(File.ReadAllBytes(savePath + ".bak"), Is.EqualTo(firstBytes));
            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath + ".bak", out var backup, out var backupMessage), Is.True, backupMessage);
            Assert.That(backup.Manifest.Revision, Is.EqualTo(firstRevision));
            Assert.That(PrototypeGarageCatalog.Compile(backup).WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.StockRoadKey));
            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var current, out var currentMessage), Is.True, currentMessage);
            AssertSameAssembly(state, current);
        }

        [Test]
        public void Reconfigure_ResizesExistingFourTiresAndUsesManifestMass()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var state = PrototypeGarageCatalog.CreateNewState();
            controller.Configure(PrototypeGarageCatalog.Compile(state).Vehicle);
            var firstTires = vehicle.GetComponentsInChildren<MeshFilter>();
            Assert.That(firstTires.Length, Is.EqualTo(4));
            var originalWidth = firstTires[0].sharedMesh.bounds.size.y;
            var firstIds = firstTires.Select(tire => tire.GetInstanceID()).ToArray();

            InstallTouring(state);
            var changed = PrototypeGarageCatalog.Compile(state);
            controller.Configure(changed.Vehicle);
            var updatedTires = vehicle.GetComponentsInChildren<MeshFilter>();

            Assert.That(updatedTires.Select(tire => tire.GetInstanceID()).ToArray(), Is.EqualTo(firstIds));
            Assert.That(vehicle.transform.childCount, Is.EqualTo(4));
            Assert.That(vehicle.GetComponent<Rigidbody>().mass, Is.EqualTo(changed.Vehicle.MassKilograms).Within(0.001d));
            foreach (var tire in updatedTires)
            {
                Assert.That(tire.sharedMesh.bounds.size.y, Is.GreaterThan(originalWidth));
                Assert.That(tire.sharedMesh.bounds.size.y, Is.EqualTo(changed.Vehicle.Tire.SectionWidthMeters).Within(0.00001d));
                Assert.That(tire.sharedMesh.bounds.size.x, Is.EqualTo(2d * changed.Vehicle.Tire.UnloadedRadiusMeters).Within(0.00001d));
                Assert.That(tire.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(tire.transform.parent.localScale, Is.EqualTo(Vector3.one));
            }
        }

        [Test]
        public void ProductionBodyGeometry_DoesNotBecomeItsOwnTireContactSurface()
        {
            var floor = new GameObject("Integration road", typeof(BoxCollider), typeof(SurfaceGrip));
            ownedVehicles.Add(floor);
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.GetComponent<BoxCollider>().size = new Vector3(20f, 0.2f, 20f);
            floor.GetComponent<SurfaceGrip>().Configure(1f, "Integration road");

            var vehicle = CreateVehicle();
            PrototypeBootstrap.AddReferenceBody(vehicle);
            vehicle.transform.position = new Vector3(0f, 0.65f, 0f);
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            controller.Configure(PrototypeGarageCatalog.Compile(PrototypeGarageCatalog.CreateNewState()).Vehicle);
            UnityEngine.Physics.SyncTransforms();
            controller.SimulateStep(1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz);

            Assert.That(controller.Telemetry.GroundedWheelCount, Is.EqualTo(4));
            foreach (var wheel in controller.Telemetry.Wheels)
            {
                Assert.That(wheel.SurfaceName, Is.EqualTo("Integration road"),
                    "Raycasts must skip the chassis beneath the suspension mounts and reach the road.");
                Assert.That(wheel.SuspensionCompressionMeters, Is.LessThan(0.02f),
                    "A self-contact would falsely report full suspension compression.");
            }
        }

        [TestCase(5f, 0f)]
        [TestCase(0.65f, 180f)]
        public void GarageEntry_RejectsAboveBayOrOverturnedVehicle(float heightMeters, float rollDegrees)
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var input = vehicle.GetComponent<PrototypeInputDriver>();
            var session = vehicle.AddComponent<PrototypeGarageSession>();
            session.Initialize(controller, input, savePath);
            session.LeaveGarage();
            vehicle.transform.SetPositionAndRotation(PrototypeGarageSession.BayCenter + Vector3.up * heightMeters,
                Quaternion.Euler(0f, 0f, rollDegrees));
            vehicle.GetComponent<Rigidbody>().velocity = Vector3.zero;

            session.TryEnterGarage();

            Assert.That(session.IsInGarage, Is.False, "Only an upright car at workshop floor height may enter.");
            Assert.That(input.DrivingEnabled, Is.True);
            Assert.That(controller.SimulationPaused, Is.False);
        }

        [Test]
        public void GarageDriveReturnSaveReload_UsesOneConsistentAssembly()
        {
            var vehicle = CreateVehicle();
            var controller = vehicle.GetComponent<RaycastVehicleController>();
            var input = vehicle.GetComponent<PrototypeInputDriver>();
            var body = vehicle.GetComponent<Rigidbody>();
            var session = vehicle.AddComponent<PrototypeGarageSession>();
            session.Initialize(controller, input, savePath);
            AssertParked(session, controller, input, body);

            session.SelectPackage(PrototypeGarageCatalog.TouringKey);
            Assert.That(session.Preview().CanInstall, Is.False, "The package needs its companion hardware.");
            session.InstallSelectedPackage();
            Assert.That(session.Configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.StockRoadKey));
            session.InstallCompanionKit();
            session.InstallSelectedPackage();
            Assert.That(session.Configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.TouringKey));
            Assert.That(session.Configuration.HasCompanionKit, Is.True);
            Assert.That(body.mass, Is.EqualTo(session.Configuration.Vehicle.MassKilograms).Within(0.001d));

            session.LeaveGarage();
            Assert.That(session.IsInGarage, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(controller.SimulationPaused, Is.False);
            Assert.That(input.DrivingEnabled, Is.True);
            session.Save();
            Assert.That(File.Exists(savePath), Is.False, "Driving must not replace the parked configuration save.");

            vehicle.transform.position = PrototypeGarageSession.BayCenter + Vector3.right * 10f;
            session.TryEnterGarage();
            Assert.That(session.IsInGarage, Is.False, "The workshop must reject a car outside the bay.");
            vehicle.transform.position = PrototypeGarageSession.BayCenter + Vector3.up;
            body.velocity = Vector3.forward;
            session.TryEnterGarage();
            Assert.That(session.IsInGarage, Is.False, "The workshop must reject a moving car.");
            body.velocity = Vector3.zero;
            session.TryEnterGarage();
            AssertParked(session, controller, input, body);
            session.Save();
            Assert.That(PrototypeGarageSaveStore.TryLoad(savePath, out var savedState, out var saveMessage), Is.True, saveMessage);

            session.SelectPackage(PrototypeGarageCatalog.StockRoadKey);
            session.InstallSelectedPackage();
            Assert.That(session.Configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.StockRoadKey));
            session.Reload();
            AssertSameAssembly(savedState, session.State);
            Assert.That(session.Configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.TouringKey));

            session.LeaveGarage();
            vehicle.transform.position = new Vector3(50f, 20f, 50f);
            session.RecoverToGarage();
            AssertParked(session, controller, input, body);
            AssertSameAssembly(savedState, session.State);

            var reopenedVehicle = CreateVehicle();
            var reopened = reopenedVehicle.AddComponent<PrototypeGarageSession>();
            reopened.Initialize(reopenedVehicle.GetComponent<RaycastVehicleController>(), reopenedVehicle.GetComponent<PrototypeInputDriver>(), savePath);
            AssertSameAssembly(savedState, reopened.State);
            Assert.That(reopened.Configuration.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.TouringKey));
            Assert.That(reopened.IsInGarage, Is.True);
        }

        private GameObject CreateVehicle()
        {
            var vehicle = new GameObject("Garage integration test vehicle", typeof(Rigidbody), typeof(RaycastVehicleController), typeof(PrototypeInputDriver));
            ownedVehicles.Add(vehicle);
            vehicle.GetComponent<PrototypeInputDriver>().Configure(vehicle.GetComponent<RaycastVehicleController>());
            return vehicle;
        }

        private static PrototypeGarageState CreateTouringState()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            InstallTouring(state);
            return state;
        }

        private static void InstallTouring(PrototypeGarageState state)
        {
            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out var kitMessage), Is.True, kitMessage);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out var wheelMessage), Is.True, wheelMessage);
        }

        private static void AssertSameAssembly(PrototypeGarageState expected, PrototypeGarageState actual)
        {
            Assert.That(actual.SchemaVersion, Is.EqualTo(expected.SchemaVersion));
            Assert.That(actual.Manifest.ManifestId, Is.EqualTo(expected.Manifest.ManifestId));
            Assert.That(actual.Manifest.Revision, Is.EqualTo(expected.Manifest.Revision));
            Assert.That(actual.Manifest.Parts.Select(part => part.InstanceId.Value).ToArray(),
                Is.EqualTo(expected.Manifest.Parts.Select(part => part.InstanceId.Value).ToArray()));
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        private static void AssertParked(PrototypeGarageSession session, RaycastVehicleController controller, PrototypeInputDriver input, Rigidbody body)
        {
            Assert.That(session.IsInGarage, Is.True);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(controller.SimulationPaused, Is.True);
            Assert.That(input.DrivingEnabled, Is.False);
            Assert.That(body.transform.position.x, Is.EqualTo(PrototypeGarageSession.BayCenter.x).Within(0.001f));
            Assert.That(body.transform.position.z, Is.EqualTo(PrototypeGarageSession.BayCenter.z).Within(0.001f));
        }
    }
}
