using System.IO;
using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;
using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGarageSession : MonoBehaviour
    {
        public static readonly Vector3 BayCenter = new Vector3(0f, 0f, -121f);
        private RaycastVehicleController controller;
        private PrototypeInputDriver inputDriver;
        private Rigidbody body;
        private PrototypeGarageState state;
        private string selectedPackageKey = PrototypeGarageCatalog.StockRoadKey;

        public bool IsInGarage { get; private set; }
        public string Message { get; private set; }
        public string SavePath { get; private set; }
        public PrototypeGarageConfiguration Configuration { get; private set; }
        public PrototypeGarageState State => state;
        public string SelectedPackageKey => selectedPackageKey;
        public DimensionalCompatibilityResult GarageAccess => DimensionalCompatibilityEvaluator.Evaluate(
            Configuration.Envelope, ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile());

        public void Initialize(RaycastVehicleController target, PrototypeInputDriver driver, string savePath = null)
        {
            controller = target;
            inputDriver = driver;
            body = target.GetComponent<Rigidbody>();
            SavePath = savePath ?? Path.Combine(Application.persistentDataPath, "garage-v1.json");
            if (!PrototypeGarageSaveStore.TryLoad(SavePath, out state, out var loadMessage))
            {
                state = PrototypeGarageCatalog.CreateNewState();
            }

            ApplyConfiguration();
            selectedPackageKey = Configuration.WheelPackageKey;
            ParkInGarage();
            Message = loadMessage + " Choose a wheel package or leave for a test drive.";
        }

        public void SelectPackage(string key)
        {
            selectedPackageKey = key;
            Message = Preview().Summary;
        }

        public PrototypeGarageProposal Preview()
        {
            return PrototypeGarageCatalog.PreviewWheelPackage(state, selectedPackageKey);
        }

        public void InstallSelectedPackage()
        {
            if (!IsInGarage) return;
            if (PrototypeGarageCatalog.TryInstallWheelPackage(state, selectedPackageKey, out var result))
            {
                ApplyConfiguration();
                ParkInGarage();
            }
            Message = result;
        }

        public void InstallCompanionKit()
        {
            if (!IsInGarage) return;
            if (PrototypeGarageCatalog.TryInstallCompanionKit(state, out var result))
            {
                ApplyConfiguration();
                ParkInGarage();
            }
            Message = result;
        }

        public void LeaveGarage()
        {
            if (!IsInGarage) return;
            var access = GarageAccess;
            if (!access.IsCompatible)
            {
                Message = access.Conflicts.Length > 0 ? access.Conflicts[0].Message : "Garage clearance is not verified.";
                return;
            }

            IsInGarage = false;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            controller.ResetMotion();
            controller.SimulationPaused = false;
            inputDriver.DrivingEnabled = true;
            Message = "Test drive: W/S throttle and brake, A/D steer. Return to the marked bay, stop, then press G.";
        }

        public void TryEnterGarage()
        {
            if (IsInGarage) return;
            var delta = controller.transform.position - BayCenter;
            if (Mathf.Abs(delta.x) > 1.25f || Mathf.Abs(delta.z) > 2f || delta.y < 0.1f || delta.y > 1.1f
                || Vector3.Dot(controller.transform.up, Vector3.up) < 0.85f || body.velocity.magnitude > 0.5f)
            {
                Message = "Park within the marked workshop bay and stop below 0.5 m/s before pressing G.";
                return;
            }
            if (!GarageAccess.IsCompatible)
            {
                Message = "This assembly cannot use the workshop: " + GarageAccess.Conflicts[0].Message;
                return;
            }
            ParkInGarage();
            Message = "Vehicle parked. Inspect, change parts, save, or reload your assembly.";
        }

        public void Save()
        {
            if (!IsInGarage) { Message = "Return to the workshop before saving."; return; }
            PrototypeGarageSaveStore.TrySave(SavePath, state, out var result);
            Message = result;
        }

        public void Reload()
        {
            if (!IsInGarage) { Message = "Return to the workshop before reloading."; return; }
            if (PrototypeGarageSaveStore.TryLoad(SavePath, out var loaded, out var result))
            {
                state = loaded;
                ApplyConfiguration();
                selectedPackageKey = Configuration.WheelPackageKey;
                ParkInGarage();
            }
            Message = result;
        }

        // Explicit prototype recovery control for an overturned or stranded test vehicle.
        public void RecoverToGarage()
        {
            ParkInGarage();
            Message = "Recovered to the test garage. Installed configuration retained.";
        }

        private void ApplyConfiguration()
        {
            Configuration = PrototypeGarageCatalog.Compile(state);
            // Unity cannot reset velocity on a kinematic body. Reconfigure while motion is stopped.
            body.isKinematic = false;
            controller.Configure(Configuration.Vehicle);
        }

        private void ParkInGarage()
        {
            inputDriver.DrivingEnabled = false;
            controller.SimulationPaused = true;
            body.isKinematic = false;
            controller.ResetMotion();
            var car = Configuration.Vehicle;
            var nominalHeight = car.Tire.UnloadedRadiusMeters + car.Suspension.RestLengthMeters
                - car.MassKilograms * 9.80665d / (4d * car.Suspension.SpringRateNewtonsPerMeter);
            controller.transform.SetPositionAndRotation(BayCenter + Vector3.up * (float)nominalHeight, Quaternion.identity);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            IsInGarage = true;
        }

        private void Update()
        {
            if (controller == null) return;
            if (Input.GetKeyDown(KeyCode.G)) TryEnterGarage();
            if (Input.GetKeyDown(KeyCode.T)) LeaveGarage();
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Reload();
            if (Input.GetKeyDown(KeyCode.F6)) RecoverToGarage();
        }
    }
}
