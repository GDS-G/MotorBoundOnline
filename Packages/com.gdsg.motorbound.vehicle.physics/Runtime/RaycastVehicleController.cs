using System;
using MotorBound.Foundation;
using MotorBound.Vehicle.Core;
using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaycastVehicleController : MonoBehaviour
    {
        private const int WheelCount = 4;
        private const float AirDensityKilogramsPerCubicMeter = 1.225f;
        private const float MinimumSlipReferenceSpeedMetersPerSecond = 1.5f;

        private readonly WheelState[] wheels = new WheelState[WheelCount];
        private Rigidbody body;
        private VehicleDefinition definition;
        private VehicleInputState input;
        private Vector3 previousVelocity;
        private int forwardGear = 1;
        private float engineSpeedRpm;
        private bool configured;

        public VehicleTelemetry Telemetry { get; private set; }
        public VehicleDefinition Definition => definition;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (!configured)
            {
                Configure(ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype());
            }
        }

        public void Configure(VehicleDefinition vehicleDefinition)
        {
            definition = vehicleDefinition ?? throw new ArgumentNullException(nameof(vehicleDefinition));
            var validationIssues = definition.Validate();
            if (validationIssues.Count > 0)
            {
                throw new ArgumentException("Vehicle definition is invalid: " + validationIssues[0], nameof(vehicleDefinition));
            }

            body = body != null ? body : GetComponent<Rigidbody>();
            body.mass = (float)definition.MassKilograms;
            body.drag = 0f;
            body.angularDrag = 0.08f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0f, (float)-definition.CenterOfMassHeightMeters, (float)definition.CenterOfMassLongitudinalOffsetMeters);
            body.maxAngularVelocity = 20f;

            InitializeWheel(0, "Front Left", -(float)definition.FrontTrackMeters * 0.5f, (float)definition.WheelbaseMeters * 0.5f, true, false, true);
            InitializeWheel(1, "Front Right", (float)definition.FrontTrackMeters * 0.5f, (float)definition.WheelbaseMeters * 0.5f, true, false, false);
            InitializeWheel(2, "Rear Left", -(float)definition.RearTrackMeters * 0.5f, -(float)definition.WheelbaseMeters * 0.5f, false, true, true);
            InitializeWheel(3, "Rear Right", (float)definition.RearTrackMeters * 0.5f, -(float)definition.WheelbaseMeters * 0.5f, false, true, false);

            previousVelocity = body.velocity;
            engineSpeedRpm = (float)definition.Engine.IdleSpeedRpm;
            Telemetry = new VehicleTelemetry
            {
                VehicleName = definition.DisplayName,
                ForwardGear = forwardGear,
                EngineSpeedRpm = engineSpeedRpm,
                Wheels = new WheelTelemetry[WheelCount]
            };
            configured = true;
        }

        public void SetInput(VehicleInputState nextInput)
        {
            input = nextInput;
        }

        public void Recover()
        {
            var yaw = transform.eulerAngles.y;
            transform.SetPositionAndRotation(transform.position + (Vector3.up * 1.25f), Quaternion.Euler(0f, yaw, 0f));
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            foreach (var wheel in wheels)
            {
                wheel.AngularSpeedRadiansPerSecond = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (!configured)
            {
                return;
            }

            var fixedDeltaTime = Time.fixedDeltaTime;
            UpdatePowertrain();
            var drivenWheelCount = GetDrivenWheelCount();
            var gearRatio = (float)definition.Transmission.ForwardGearRatios[forwardGear - 1];
            var engineTorque = (float)definition.Engine.EvaluateFullLoadTorqueNewtonMeters(engineSpeedRpm) * input.Throttle;
            var engineBraking = body.velocity.sqrMagnitude > 0.25f
                ? (1f - input.Throttle) * (float)definition.Engine.EngineBrakingTorqueNewtonMeters
                : 0f;
            var driveTorquePerWheel = ((engineTorque - engineBraking) * gearRatio * (float)definition.Transmission.FinalDriveRatio * (float)definition.Transmission.Efficiency) / drivenWheelCount;

            var telemetryWheels = Telemetry.Wheels ?? new WheelTelemetry[WheelCount];
            for (var index = 0; index < wheels.Length; index++)
            {
                var wheel = wheels[index];
                telemetryWheels[index] = SimulateWheel(wheel, wheel.Driven ? driveTorquePerWheel : 0f, fixedDeltaTime);
            }

            ApplyAerodynamics();

            var accelerationWorld = (body.velocity - previousVelocity) / Mathf.Max(fixedDeltaTime, 0.0001f);
            previousVelocity = body.velocity;
            Telemetry = new VehicleTelemetry
            {
                VehicleName = definition.DisplayName,
                SpeedMetersPerSecond = body.velocity.magnitude,
                EngineSpeedRpm = engineSpeedRpm,
                ForwardGear = forwardGear,
                LocalAccelerationMetersPerSecondSquared = transform.InverseTransformDirection(accelerationWorld),
                Input = input,
                Wheels = telemetryWheels
            };
        }

        private WheelTelemetry SimulateWheel(WheelState wheel, float driveTorqueNewtonMeters, float deltaTimeSeconds)
        {
            var suspension = definition.Suspension;
            var tire = definition.Tire;
            var radius = (float)tire.UnloadedRadiusMeters;
            var rayOrigin = transform.TransformPoint(wheel.LocalMountPosition);
            var rayDirection = -transform.up;
            var maximumRayDistance = (float)(suspension.RestLengthMeters + suspension.TravelMeters + tire.UnloadedRadiusMeters);
            var steeringDegrees = wheel.Steered ? input.Steering * (float)definition.MaximumSteeringAngleDegrees : 0f;

            if (!UnityEngine.Physics.Raycast(rayOrigin, rayDirection, out var hit, maximumRayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                IntegrateFreeWheel(wheel, driveTorqueNewtonMeters, deltaTimeSeconds);
                UpdateWheelVisual(wheel, steeringDegrees, (float)(suspension.RestLengthMeters + suspension.TravelMeters));
                return new WheelTelemetry
                {
                    Grounded = false,
                    SurfaceGripMultiplier = 0f,
                    SurfaceName = "Airborne"
                };
            }

            var suspensionLength = Mathf.Clamp(hit.distance - radius, 0f, (float)(suspension.RestLengthMeters + suspension.TravelMeters));
            var compression = Mathf.Max(0f, (float)suspension.RestLengthMeters - suspensionLength);
            var pointVelocity = body.GetPointVelocity(hit.point);
            var suspensionVelocity = Vector3.Dot(pointVelocity, transform.up);
            var damperRate = suspensionVelocity < 0f
                ? (float)suspension.DamperCompressionNewtonsPerMeterPerSecond
                : (float)suspension.DamperReboundNewtonsPerMeterPerSecond;
            var normalLoad = Mathf.Max(0f, (compression * (float)suspension.SpringRateNewtonsPerMeter) - (suspensionVelocity * damperRate));

            var steerRotation = Quaternion.AngleAxis(steeringDegrees, transform.up);
            var wheelForward = Vector3.ProjectOnPlane(steerRotation * transform.forward, hit.normal).normalized;
            var wheelRight = Vector3.Cross(hit.normal, wheelForward).normalized;
            var longitudinalSpeed = Vector3.Dot(pointVelocity, wheelForward);
            var lateralSpeed = Vector3.Dot(pointVelocity, wheelRight);
            var wheelSurfaceSpeed = wheel.AngularSpeedRadiansPerSecond * radius;
            var slipReferenceSpeed = Mathf.Max(Mathf.Abs(longitudinalSpeed), MinimumSlipReferenceSpeedMetersPerSecond);
            var slipRatio = (wheelSurfaceSpeed - longitudinalSpeed) / slipReferenceSpeed;
            var slipAngle = Mathf.Atan2(lateralSpeed, Mathf.Max(Mathf.Abs(longitudinalSpeed), MinimumSlipReferenceSpeedMetersPerSecond));
            var surface = hit.collider.GetComponentInParent<SurfaceGrip>();
            var surfaceMultiplier = surface != null ? surface.GripMultiplier : 1f;
            var surfaceName = surface != null ? surface.SurfaceName : hit.collider.name;

            var forceResult = TireForceModel.Evaluate(new TireForceInput
            {
                NormalLoadNewtons = normalLoad,
                SlipRatio = slipRatio,
                SlipAngleRadians = slipAngle,
                PeakDryFrictionCoefficient = tire.PeakDryFrictionCoefficient,
                SurfaceGripMultiplier = surfaceMultiplier,
                LongitudinalSlipStiffnessNewtonPerRatio = tire.LongitudinalSlipStiffnessNewtonPerRatio,
                CorneringStiffnessNewtonPerRadian = tire.CorneringStiffnessNewtonPerRadian,
                ReferenceLoadNewtons = tire.ReferenceLoadNewtons,
                LoadSensitivityExponent = tire.LoadSensitivityExponent
            });

            var longitudinalForce = (float)forceResult.LongitudinalForceNewtons;
            if (Mathf.Abs(longitudinalSpeed) > 0.25f)
            {
                longitudinalForce -= Mathf.Sign(longitudinalSpeed) * (float)(tire.RollingResistanceCoefficient * normalLoad);
            }

            var lateralForce = (float)forceResult.LateralForceNewtons;
            var deliveredForceMagnitude = Mathf.Sqrt((longitudinalForce * longitudinalForce) + (lateralForce * lateralForce));
            var maximumCombinedForce = (float)forceResult.MaximumCombinedForceNewtons;
            if (deliveredForceMagnitude > maximumCombinedForce && deliveredForceMagnitude > 0f)
            {
                var gripScale = maximumCombinedForce / deliveredForceMagnitude;
                longitudinalForce *= gripScale;
                lateralForce *= gripScale;
            }

            body.AddForceAtPosition(
                (hit.normal * normalLoad)
                + (wheelForward * longitudinalForce)
                + (wheelRight * lateralForce),
                hit.point,
                ForceMode.Force);

            var serviceBias = wheel.Front
                ? (float)definition.Brakes.FrontBias * 0.5f
                : (1f - (float)definition.Brakes.FrontBias) * 0.5f;
            var serviceBrakeTorque = input.Brake * (float)definition.Brakes.MaximumServiceBrakeTorqueNewtonMeters * serviceBias;
            var handbrakeTorque = wheel.Front ? 0f : input.Handbrake * (float)definition.Brakes.MaximumHandbrakeTorqueNewtonMeters * 0.5f;
            IntegrateWheelAngularSpeed(
                wheel,
                driveTorqueNewtonMeters,
                longitudinalForce * radius,
                serviceBrakeTorque + handbrakeTorque,
                longitudinalSpeed,
                deltaTimeSeconds);

            UpdateWheelVisual(wheel, steeringDegrees, suspensionLength);
            return new WheelTelemetry
            {
                Grounded = true,
                SuspensionCompressionMeters = compression,
                NormalLoadNewtons = normalLoad,
                SlipRatio = slipRatio,
                SlipAngleDegrees = slipAngle * Mathf.Rad2Deg,
                LongitudinalForceNewtons = longitudinalForce,
                LateralForceNewtons = lateralForce,
                SurfaceGripMultiplier = surfaceMultiplier,
                SurfaceName = surfaceName
            };
        }

        private void UpdatePowertrain()
        {
            var drivenAngularSpeed = 0f;
            var count = 0;
            foreach (var wheel in wheels)
            {
                if (!wheel.Driven)
                {
                    continue;
                }

                drivenAngularSpeed += Mathf.Abs(wheel.AngularSpeedRadiansPerSecond);
                count++;
            }

            drivenAngularSpeed /= Mathf.Max(count, 1);
            var gearRatio = (float)definition.Transmission.ForwardGearRatios[forwardGear - 1];
            var coupledRpm = drivenAngularSpeed
                             * gearRatio
                             * (float)definition.Transmission.FinalDriveRatio
                             * (float)UnitConversion.RadiansPerSecondToRevolutionsPerMinute;
            var launchRpm = (float)definition.Engine.IdleSpeedRpm + (input.Throttle * 900f);
            engineSpeedRpm = Mathf.Clamp(Mathf.Max(coupledRpm, launchRpm), (float)definition.Engine.IdleSpeedRpm, (float)definition.Engine.RedlineSpeedRpm);

            if (engineSpeedRpm >= definition.Transmission.UpshiftSpeedRpm
                && forwardGear < definition.Transmission.ForwardGearRatios.Length)
            {
                forwardGear++;
            }
            else if (engineSpeedRpm <= definition.Transmission.DownshiftSpeedRpm && forwardGear > 1)
            {
                forwardGear--;
            }
        }

        private void ApplyAerodynamics()
        {
            var speed = body.velocity.magnitude;
            if (speed <= 0.01f)
            {
                return;
            }

            var dynamicPressure = 0.5f * AirDensityKilogramsPerCubicMeter * speed * speed;
            var dragMagnitude = dynamicPressure * (float)definition.AerodynamicDragCoefficient * (float)definition.FrontalAreaSquareMeters;
            var liftMagnitude = dynamicPressure * (float)definition.AerodynamicLiftCoefficient * (float)definition.FrontalAreaSquareMeters;
            body.AddForce((-body.velocity.normalized * dragMagnitude) + (transform.up * liftMagnitude), ForceMode.Force);
        }

        private void IntegrateFreeWheel(WheelState wheel, float driveTorqueNewtonMeters, float deltaTimeSeconds)
        {
            var brake = input.Brake * (float)definition.Brakes.MaximumServiceBrakeTorqueNewtonMeters * 0.25f;
            if (!wheel.Front)
            {
                brake += input.Handbrake * (float)definition.Brakes.MaximumHandbrakeTorqueNewtonMeters * 0.5f;
            }

            IntegrateWheelAngularSpeed(wheel, driveTorqueNewtonMeters, 0f, brake, 0f, deltaTimeSeconds);
        }

        private void IntegrateWheelAngularSpeed(
            WheelState wheel,
            float driveTorqueNewtonMeters,
            float reactionTorqueNewtonMeters,
            float brakeTorqueNewtonMeters,
            float longitudinalSpeedMetersPerSecond,
            float deltaTimeSeconds)
        {
            var priorAngularSpeed = wheel.AngularSpeedRadiansPerSecond;
            var direction = Mathf.Abs(priorAngularSpeed) > 0.1f
                ? Mathf.Sign(priorAngularSpeed)
                : Mathf.Sign(longitudinalSpeedMetersPerSecond);
            var brakeTorque = brakeTorqueNewtonMeters * direction;
            var netTorque = driveTorqueNewtonMeters - reactionTorqueNewtonMeters - brakeTorque;
            var angularAcceleration = netTorque / Mathf.Max((float)definition.Tire.RotationalInertiaKilogramMetersSquared, 0.01f);
            wheel.AngularSpeedRadiansPerSecond += angularAcceleration * deltaTimeSeconds;

            if (brakeTorqueNewtonMeters > 0f
                && Mathf.Abs(driveTorqueNewtonMeters) < brakeTorqueNewtonMeters
                && Mathf.Sign(priorAngularSpeed) != 0f
                && Mathf.Sign(priorAngularSpeed) != Mathf.Sign(wheel.AngularSpeedRadiansPerSecond))
            {
                wheel.AngularSpeedRadiansPerSecond = 0f;
            }

            wheel.AngularSpeedRadiansPerSecond = Mathf.Clamp(wheel.AngularSpeedRadiansPerSecond, -1000f, 1000f);
        }

        private int GetDrivenWheelCount()
        {
            var count = 0;
            foreach (var wheel in wheels)
            {
                if (wheel.Driven)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        private void InitializeWheel(int index, string wheelName, float lateral, float longitudinal, bool front, bool rearDriven, bool left)
        {
            var driven = definition.DriveLayout == DriveLayout.AllWheelDrive
                         || (definition.DriveLayout == DriveLayout.FrontWheelDrive && front)
                         || (definition.DriveLayout == DriveLayout.RearWheelDrive && rearDriven);
            var wheel = wheels[index] ?? new WheelState();
            wheel.Name = wheelName;
            wheel.LocalMountPosition = new Vector3(lateral, 0f, longitudinal);
            wheel.Front = front;
            wheel.Steered = front;
            wheel.Driven = driven;
            wheel.Left = left;
            if (wheel.VisualPivot == null)
            {
                CreateWheelVisual(wheel);
            }

            wheels[index] = wheel;
        }

        private void CreateWheelVisual(WheelState wheel)
        {
            var pivot = new GameObject(wheel.Name + " Visual").transform;
            pivot.SetParent(transform, false);
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Tire";
            cylinder.transform.SetParent(pivot, false);
            cylinder.transform.localScale = new Vector3(
                (float)definition.Tire.UnloadedRadiusMeters * 2f,
                0.12f,
                (float)definition.Tire.UnloadedRadiusMeters * 2f);
            cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.04f, 0.045f, 0.05f);
            }

            wheel.VisualPivot = pivot;
            wheel.VisualWheel = cylinder.transform;
        }

        private void UpdateWheelVisual(WheelState wheel, float steeringDegrees, float suspensionLengthMeters)
        {
            if (wheel.VisualPivot == null)
            {
                return;
            }

            wheel.VisualPivot.localPosition = wheel.LocalMountPosition + (Vector3.down * suspensionLengthMeters);
            wheel.VisualPivot.localRotation = Quaternion.Euler(0f, steeringDegrees, 0f);
            wheel.VisualSpinDegrees = Mathf.Repeat(
                wheel.VisualSpinDegrees + (wheel.AngularSpeedRadiansPerSecond * Mathf.Rad2Deg * Time.fixedDeltaTime),
                360f);
            wheel.VisualWheel.localRotation = Quaternion.Euler(0f, wheel.VisualSpinDegrees, 90f);
        }

        private sealed class WheelState
        {
            public string Name;
            public Vector3 LocalMountPosition;
            public bool Front;
            public bool Steered;
            public bool Driven;
            public bool Left;
            public float AngularSpeedRadiansPerSecond;
            public float VisualSpinDegrees;
            public Transform VisualPivot;
            public Transform VisualWheel;
        }
    }
}
