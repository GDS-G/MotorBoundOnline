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
        public const int CriticalVehicleSimulationFrequencyHertz = 360;
        private const int WheelCount = 4;
        private const float AirDensityKilogramsPerCubicMeter = 1.225f;
        private const float MinimumSlipReferenceSpeedMetersPerSecond = 1.5f;

        private readonly WheelState[] wheels = new WheelState[WheelCount];
        private readonly TireForceInput[] tractionContacts = new TireForceInput[WheelCount];
        private readonly PreparedWheelStep[] preparedWheels = new PreparedWheelStep[WheelCount];
        private readonly VehicleAssistController assistController = new VehicleAssistController();
        private readonly TransmissionGearSelector gearSelector = new TransmissionGearSelector();
        private RaycastHit[] contactHits = new RaycastHit[8];
        private Rigidbody body;
        private VehicleDefinition definition;
        private VehicleInputState input;
        private Vector3 previousVelocity;
        private float engineSpeedRpm;
        private bool engineRevLimiterActive;
        private bool configured;
        private Mesh tireMesh;
        private Material tireMaterial;

        public VehicleTelemetry Telemetry { get; private set; }
        public VehicleDefinition Definition => definition;
        public bool SimulationPaused { get; set; }
        public DriverAssistMode AssistMode { get; set; } = DriverAssistMode.Sport;
        public int SelectedGear => gearSelector.SelectedGear;
        public DriveTransmissionMode TransmissionMode => gearSelector.Mode;
        public string GearSelectionMessage { get; private set; } = "Automatic forward gears; Q/E manual shift, R stopped direction selector.";
        public bool EngineRevLimiterActive => engineRevLimiterActive;
        // Compatibility for existing explicit Road/OFF validation callers.
        public bool RoadTractionControlEnabled
        {
            get => AssistMode != DriverAssistMode.Off;
            set => AssistMode = value ? DriverAssistMode.Road : DriverAssistMode.Off;
        }

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
            if (vehicleDefinition == null)
            {
                throw new ArgumentNullException(nameof(vehicleDefinition));
            }

            var validationIssues = vehicleDefinition.Validate();
            if (validationIssues.Count > 0)
            {
                throw new ArgumentException("Vehicle definition is invalid: " + validationIssues[0], nameof(vehicleDefinition));
            }

            definition = vehicleDefinition;
            body = body != null ? body : GetComponent<Rigidbody>();
            body.mass = (float)definition.MassKilograms;
            body.drag = 0f;
            body.angularDrag = 0.08f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // The definition measures CG above ground; the transform is the loaded suspension-mount datum.
            body.centerOfMass = new Vector3(0f, (float)definition.CenterOfMassHeightMeters - NominalRootHeightMeters(),
                (float)definition.CenterOfMassLongitudinalOffsetMeters);
            body.maxAngularVelocity = 20f;

            UpdateTireMesh();
            InitializeWheel(0, "Front Left", -(float)definition.FrontTrackMeters * 0.5f, (float)definition.WheelbaseMeters * 0.5f, true, false, true);
            InitializeWheel(1, "Front Right", (float)definition.FrontTrackMeters * 0.5f, (float)definition.WheelbaseMeters * 0.5f, true, false, false);
            InitializeWheel(2, "Rear Left", -(float)definition.RearTrackMeters * 0.5f, -(float)definition.WheelbaseMeters * 0.5f, false, true, true);
            InitializeWheel(3, "Rear Right", (float)definition.RearTrackMeters * 0.5f, -(float)definition.WheelbaseMeters * 0.5f, false, true, false);

            configured = true;
            ResetMotion();
        }

        public void ResetMotion()
        {
            body = body != null ? body : GetComponent<Rigidbody>();
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            previousVelocity = Vector3.zero;
            input = default(VehicleInputState);
            assistController.Reset();
            var retainedTransmissionMode = gearSelector.Mode;
            gearSelector.Reset();
            gearSelector.SetMode(retainedTransmissionMode);
            GearSelectionMessage = "First gear selected after motion reset.";
            engineSpeedRpm = definition != null ? (float)definition.Engine.IdleSpeedRpm : 0f;
            engineRevLimiterActive = false;
            foreach (var wheel in wheels)
            {
                if (wheel == null)
                {
                    continue;
                }

                wheel.AngularSpeedRadiansPerSecond = 0f;
                wheel.VisualSpinDegrees = 0f;
                UpdateWheelVisual(wheel, 0f, NominalSuspensionLengthMeters(), 0f);
            }

            Telemetry = new VehicleTelemetry
            {
                VehicleName = definition != null ? definition.DisplayName : string.Empty,
                ForwardGear = SelectedGear,
                TransmissionMode = TransmissionMode,
                EngineSpeedRpm = engineSpeedRpm,
                EngineRevLimiterActive = engineRevLimiterActive,
                SimulationFrequencyHertz = CriticalVehicleSimulationFrequencyHertz,
                RoadTractionControlEnabled = RoadTractionControlEnabled,
                AssistMode = AssistMode,
                AssistPhase = AssistMode == DriverAssistMode.Off ? DriverAssistPhase.Unassisted : DriverAssistPhase.RoadGrip,
                DeliveredDriveTorqueScale = 1f,
                Wheels = new WheelTelemetry[WheelCount]
            };
        }

        public void SetInput(VehicleInputState nextInput)
        {
            input = nextInput;
        }

        public bool TryShiftGear(int direction)
        {
            if (!configured || SimulationPaused) return false;
            var count = definition.Transmission.ForwardGearRatios.Length;
            var target = direction == 1 ? Mathf.Min(SelectedGear + 1, count)
                : direction == -1 ? Mathf.Max(SelectedGear - 1, -1) : SelectedGear;
            if (target > 0 && target < SelectedGear)
            {
                var predictedRpm = AverageDrivenWheelAngularSpeed()
                    * definition.Transmission.ForwardGearRatios[target - 1]
                    * definition.Transmission.FinalDriveRatio * UnitConversion.RadiansPerSecondToRevolutionsPerMinute;
                if (predictedRpm > definition.Engine.RedlineSpeedRpm)
                {
                    GearSelectionMessage = "Downshift rejected: selected gear would exceed engine redline.";
                    return false;
                }
            }
            return ApplyGearSelection(gearSelector.RequestShift(direction, count, body.velocity.magnitude));
        }

        public bool TryToggleReverse()
        {
            if (!configured || SimulationPaused) return false;
            return ApplyGearSelection(gearSelector.RequestReverseToggle(
                definition.Transmission.ForwardGearRatios.Length, body.velocity.magnitude));
        }

        public bool ToggleTransmissionMode()
        {
            if (!configured || SimulationPaused) return false;
            return ApplyGearSelection(gearSelector.ToggleMode());
        }

        private bool ApplyGearSelection(TransmissionGearSelectionResult result)
        {
            GearSelectionMessage = result.Message;
            return result.Accepted;
        }

        public void Recover()
        {
            var yaw = transform.eulerAngles.y;
            transform.SetPositionAndRotation(transform.position + (Vector3.up * 1.25f), Quaternion.Euler(0f, yaw, 0f));
            ResetMotion();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        // The Editor validation runner calls this once before each manual Physics.Simulate step.
        public void SimulateStep(float fixedDeltaTime)
        {
            if (!configured || SimulationPaused)
            {
                assistController.Reset();
                return;
            }

            if (float.IsNaN(fixedDeltaTime) || float.IsInfinity(fixedDeltaTime) || fixedDeltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            }

            UpdatePowertrain();
            var drivenWheelCount = GetDrivenWheelCount();
            var gearRatio = (float)gearSelector.GetSelectedGearRatio(definition.Transmission);
            var engineTorque = engineRevLimiterActive ? 0f
                : (float)definition.Engine.EvaluateFullLoadTorqueNewtonMeters(engineSpeedRpm) * input.Throttle;
            var engineBraking = body.velocity.sqrMagnitude > 0.25f
                ? (1f - input.Throttle) * (float)definition.Engine.EngineBrakingTorqueNewtonMeters
                : 0f;
            var carrierSpeed = AverageDrivenWheelAngularSpeed(true);
            var propulsionPerWheel = (float)(PowertrainTorqueModel.CalculateAxleTorque(engineTorque, 0d,
                gearRatio, definition.Transmission.FinalDriveRatio, definition.Transmission.Efficiency, carrierSpeed) / drivenWheelCount);
            var localVelocity = transform.InverseTransformDirection(body.velocity);
            var bodySideslip = localVelocity.x * localVelocity.x + localVelocity.z * localVelocity.z > 0.01f
                ? Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg : 0f;
            var assist = assistController.Evaluate(AssistMode, CreateAssistObservation(bodySideslip), fixedDeltaTime);
            // Limit propulsion only. Passive engine drag must remain dissipative and
            // untouched even if a spin back-drives the shaft opposite the selected gear.
            var tractionControl = EvaluateRoadTractionControl(propulsionPerWheel,
                assist.UseRoadTractionControl && SelectedGear > 0);
            var driveTorquePerWheel = (float)(PowertrainTorqueModel.CalculateAxleTorque(
                engineTorque * tractionControl.DeliveredDriveTorqueScale, engineBraking,
                gearRatio, definition.Transmission.FinalDriveRatio, definition.Transmission.Efficiency, carrierSpeed) / drivenWheelCount);

            var telemetryWheels = Telemetry.Wheels ?? new WheelTelemetry[WheelCount];
            for (var index = 0; index < wheels.Length; index++)
                preparedWheels[index] = PrepareWheelStep(wheels[index], fixedDeltaTime);
            var frontTransfer = SimulateAxle(0, definition.FrontDifferential, driveTorquePerWheel, telemetryWheels, fixedDeltaTime);
            var rearTransfer = SimulateAxle(2, definition.RearDifferential, driveTorquePerWheel, telemetryWheels, fixedDeltaTime);

            ApplyAerodynamics();

            var accelerationWorld = (body.velocity - previousVelocity) / Mathf.Max(fixedDeltaTime, 0.0001f);
            previousVelocity = body.velocity;
            Telemetry = new VehicleTelemetry
            {
                VehicleName = definition.DisplayName,
                SpeedMetersPerSecond = body.velocity.magnitude,
                EngineSpeedRpm = engineSpeedRpm,
                EngineRevLimiterActive = engineRevLimiterActive,
                ForwardGear = SelectedGear,
                TransmissionMode = TransmissionMode,
                SimulationFrequencyHertz = Mathf.RoundToInt(1f / Mathf.Max(fixedDeltaTime, 0.0001f)),
                LocalAccelerationMetersPerSecondSquared = transform.InverseTransformDirection(accelerationWorld),
                Input = input,
                RoadTractionControlEnabled = RoadTractionControlEnabled,
                TractionControlActive = tractionControl.Active,
                DeliveredDriveTorqueScale = (float)tractionControl.DeliveredDriveTorqueScale,
                AssistMode = AssistMode,
                AssistPhase = assist.Phase,
                BodySideslipDegrees = bodySideslip,
                FrontDifferentialTransferTorqueNewtonMeters = frontTransfer,
                RearDifferentialTransferTorqueNewtonMeters = rearTransfer,
                DifferentialTransferTorqueNewtonMeters = Mathf.Max(Mathf.Abs(frontTransfer), Mathf.Abs(rearTransfer)),
                Wheels = telemetryWheels
            };
        }

        private VehicleAssistObservation CreateAssistObservation(float bodySideslip)
        {
            var observation = new VehicleAssistObservation
            {
                SpeedMetersPerSecond = body.velocity.magnitude,
                BodySideslipDegrees = bodySideslip,
                YawRateDegreesPerSecond = Vector3.Dot(body.angularVelocity, transform.up) * Mathf.Rad2Deg,
                Throttle = input.Throttle,
                Brake = input.Brake,
                Steering = input.Steering,
                Handbrake = input.Handbrake
            };
            var previous = Telemetry.Wheels;
            if (previous == null) return observation;
            for (var index = 0; index < wheels.Length && index < previous.Length; index++)
            {
                if (!wheels[index].Driven || !previous[index].Grounded || previous[index].NormalLoadNewtons <= 20f) continue;
                observation.LoadedDrivenContactCount++;
                observation.HasSlidingDrivenContact |= previous[index].IsSliding;
            }
            return observation;
        }

        private RoadTractionControlResult EvaluateRoadTractionControl(float requestedWheelTorque, bool enabled)
        {
            var contactCount = 0;
            var previousContacts = Telemetry.Wheels;
            if (previousContacts != null)
            {
                var tire = definition.Tire;
                for (var index = 0; index < wheels.Length && index < previousContacts.Length; index++)
                {
                    var previous = previousContacts[index];
                    if (!wheels[index].Driven || !previous.Grounded || previous.NormalLoadNewtons <= 20f) continue;
                    tractionContacts[contactCount++] = new TireForceInput
                    {
                        NormalLoadNewtons = previous.NormalLoadNewtons,
                        SlipRatio = previous.SlipRatio,
                        SlipAngleRadians = previous.SlipAngleDegrees * Mathf.Deg2Rad,
                        SurfaceGripMultiplier = previous.SurfaceGripMultiplier,
                        PeakDryFrictionCoefficient = tire.PeakDryFrictionCoefficient,
                        SlidingGripRatio = tire.SlidingGripRatio,
                        LongitudinalSlipStiffnessNewtonPerRatio = tire.LongitudinalSlipStiffnessNewtonPerRatio,
                        CorneringStiffnessNewtonPerRadian = tire.CorneringStiffnessNewtonPerRadian,
                        ReferenceLoadNewtons = tire.ReferenceLoadNewtons,
                        LoadSensitivityExponent = tire.LoadSensitivityExponent
                    };
                }
            }

            return RoadTractionControl.Evaluate(new RoadTractionControlInput
            {
                Enabled = enabled,
                RequestedDriveTorquePerWheelNewtonMeters = requestedWheelTorque,
                HandbrakeInput = input.Handbrake,
                TireRadiusMeters = definition.Tire.UnloadedRadiusMeters,
                LoadedDrivenContacts = tractionContacts,
                ContactCount = contactCount
            });
        }

        private PreparedWheelStep PrepareWheelStep(WheelState wheel, float deltaTimeSeconds)
        {
            var suspension = definition.Suspension;
            var tire = definition.Tire;
            var radius = (float)tire.UnloadedRadiusMeters;
            var rayOrigin = transform.TransformPoint(wheel.LocalMountPosition);
            var rayDirection = -transform.up;
            var maximumRayDistance = (float)(suspension.RestLengthMeters + suspension.TravelMeters + tire.UnloadedRadiusMeters);
            var steeringDegrees = wheel.Steered ? input.Steering * (float)definition.MaximumSteeringAngleDegrees : 0f;
            var serviceBias = wheel.Front ? (float)definition.Brakes.FrontBias * 0.5f : (1f - (float)definition.Brakes.FrontBias) * 0.5f;
            var step = new PreparedWheelStep
            {
                SteeringDegrees = steeringDegrees,
                SuspensionLengthMeters = (float)(suspension.RestLengthMeters + suspension.TravelMeters),
                Rotation = new WheelContactInput
                {
                    AngularSpeedRadiansPerSecond = wheel.AngularSpeedRadiansPerSecond,
                    RadiusMeters = radius,
                    RotationalInertiaKilogramMetersSquared = tire.RotationalInertiaKilogramMetersSquared,
                    BrakeTorqueNewtonMeters = input.Brake * (float)definition.Brakes.MaximumServiceBrakeTorqueNewtonMeters * serviceBias
                        + (wheel.Front ? 0f : input.Handbrake * (float)definition.Brakes.MaximumHandbrakeTorqueNewtonMeters * 0.5f),
                    RollingResistanceCoefficient = tire.RollingResistanceCoefficient,
                    DeltaTimeSeconds = deltaTimeSeconds,
                    Tire = new TireForceInput
                    {
                        PeakDryFrictionCoefficient = tire.PeakDryFrictionCoefficient,
                        SlidingGripRatio = tire.SlidingGripRatio,
                        LongitudinalSlipStiffnessNewtonPerRatio = tire.LongitudinalSlipStiffnessNewtonPerRatio,
                        CorneringStiffnessNewtonPerRadian = tire.CorneringStiffnessNewtonPerRadian,
                        ReferenceLoadNewtons = tire.ReferenceLoadNewtons,
                        LoadSensitivityExponent = tire.LoadSensitivityExponent
                    }
                },
                Telemetry = new WheelTelemetry { SurfaceName = "Airborne" }
            };

            if (!TryGetWheelContact(wheel, rayOrigin, rayDirection, maximumRayDistance, steeringDegrees, out var contact))
                return step;

            var suspensionLength = Mathf.Clamp(contact.Distance - radius, 0f, (float)(suspension.RestLengthMeters + suspension.TravelMeters));
            var compression = Mathf.Max(0f, (float)suspension.RestLengthMeters - suspensionLength);
            var chassisPointVelocity = body.GetPointVelocity(contact.Point);
            var groundBody = contact.Collider.attachedRigidbody;
            var groundPointVelocity = groundBody != null ? groundBody.GetPointVelocity(contact.Point) : Vector3.zero;
            if (!SuspensionKinematics.TryCalculateLengthRate(chassisPointVelocity, groundPointVelocity,
                transform.up, contact.Normal, out var suspensionVelocity)) return step;
            var pointVelocity = chassisPointVelocity - groundPointVelocity;
            var damperRate = suspensionVelocity < 0f
                ? (float)suspension.DamperCompressionNewtonsPerMeterPerSecond
                : (float)suspension.DamperReboundNewtonsPerMeterPerSecond;
            var normalLoad = Mathf.Max(0f, (compression * (float)suspension.SpringRateNewtonsPerMeter) - (suspensionVelocity * damperRate));

            var steerRotation = Quaternion.AngleAxis(steeringDegrees, transform.up);
            var wheelForward = Vector3.ProjectOnPlane(steerRotation * transform.forward, contact.Normal).normalized;
            var wheelRight = Vector3.Cross(contact.Normal, wheelForward).normalized;
            var longitudinalSpeed = Vector3.Dot(pointVelocity, wheelForward);
            var lateralSpeed = Vector3.Dot(pointVelocity, wheelRight);
            var slipAngle = Mathf.Atan2(lateralSpeed, Mathf.Max(Mathf.Abs(longitudinalSpeed), MinimumSlipReferenceSpeedMetersPerSecond));
            var surface = contact.Collider.GetComponentInParent<SurfaceGrip>();
            var surfaceState = surface != null
                ? surface.Evaluate(Mathf.Abs(longitudinalSpeed), (float)tire.TreadWaterEvacuationFactor)
                : SurfaceConditionModel.Evaluate(new SurfaceConditionInput
                {
                    BaseGripMultiplier = 1d,
                    VehicleSpeedMetersPerSecond = Mathf.Abs(longitudinalSpeed),
                    TireWaterEvacuationFactor = tire.TreadWaterEvacuationFactor
                });
            var surfaceMultiplier = (float)surfaceState.EffectiveGripMultiplier;
            var surfaceName = surface != null ? surface.SurfaceName : contact.Collider.name;

            step.Rotation.LongitudinalSpeedMetersPerSecond = longitudinalSpeed;
            step.Rotation.Tire.NormalLoadNewtons = normalLoad;
            step.Rotation.Tire.SlipAngleRadians = slipAngle;
            step.Rotation.Tire.SurfaceGripMultiplier = surfaceMultiplier;
            step.SuspensionLengthMeters = suspensionLength;
            step.WheelForward = wheelForward;
            step.WheelRight = wheelRight;
            step.Telemetry = new WheelTelemetry
            {
                Grounded = true,
                ContactSampleCount = contact.SampleCount,
                SuspensionCompressionMeters = compression,
                NormalLoadNewtons = normalLoad,
                SlipAngleDegrees = slipAngle * Mathf.Rad2Deg,
                SurfaceGripMultiplier = surfaceMultiplier,
                WaterFilmDepthMillimeters = surface != null ? surface.WaterFilmDepthMillimeters : 0f,
                ContactPointWorld = contact.Point,
                ContactNormalWorld = contact.Normal,
                SurfaceName = surfaceName
            };
            return step;
        }

        private float SimulateAxle(int leftIndex, DifferentialDefinition differential, float driveTorquePerWheel,
            WheelTelemetry[] telemetry, float deltaTimeSeconds)
        {
            var left = preparedWheels[leftIndex];
            var right = preparedWheels[leftIndex + 1];
            if (wheels[leftIndex].Driven)
            {
                // RWD/FWD: entire drive torque goes to the driven axle. AWD: equal
                // center split, with physically independent front/rear axle couplings.
                var result = AxleContactSolver.Solve(new AxleContactInput
                {
                    Left = left.Rotation,
                    Right = right.Rotation,
                    AxleDriveTorqueNewtonMeters = driveTorquePerWheel * 2d,
                    Differential = differential
                });
                telemetry[leftIndex] = ApplyWheelStep(wheels[leftIndex], left, result.Left, result.LeftMeanDriveTorqueNewtonMeters, deltaTimeSeconds);
                telemetry[leftIndex + 1] = ApplyWheelStep(wheels[leftIndex + 1], right, result.Right, result.RightMeanDriveTorqueNewtonMeters, deltaTimeSeconds);
                return (float)result.MeanDifferentialTransferTorqueNewtonMeters;
            }
            telemetry[leftIndex] = ApplyWheelStep(wheels[leftIndex], left, WheelContactSolver.Solve(left.Rotation), 0d, deltaTimeSeconds);
            telemetry[leftIndex + 1] = ApplyWheelStep(wheels[leftIndex + 1], right, WheelContactSolver.Solve(right.Rotation), 0d, deltaTimeSeconds);
            return 0f;
        }

        private WheelTelemetry ApplyWheelStep(WheelState wheel, PreparedWheelStep prepared, WheelContactResult result,
            double meanDriveTorqueNewtonMeters, float deltaTimeSeconds)
        {
            wheel.AngularSpeedRadiansPerSecond = (float)result.AngularSpeedRadiansPerSecond;
            var telemetry = prepared.Telemetry;
            telemetry.AngularSpeedRadiansPerSecond = wheel.AngularSpeedRadiansPerSecond;
            telemetry.DriveTorqueNewtonMeters = (float)meanDriveTorqueNewtonMeters;
            if (telemetry.Grounded)
            {
                telemetry.SlipRatio = (float)result.SlipRatio;
                telemetry.LongitudinalForceNewtons = (float)result.MeanLongitudinalForceNewtons;
                telemetry.LateralForceNewtons = (float)result.MeanLateralForceNewtons;
                telemetry.SlipDemandRatio = (float)result.FinalTireState.SlipDemandRatio;
                telemetry.IsSliding = result.FinalTireState.IsSliding;
                body.AddForceAtPosition(telemetry.ContactNormalWorld * telemetry.NormalLoadNewtons
                    + prepared.WheelForward * telemetry.LongitudinalForceNewtons
                    + prepared.WheelRight * telemetry.LateralForceNewtons,
                    telemetry.ContactPointWorld, ForceMode.Force);
            }
            UpdateWheelVisual(wheel, prepared.SteeringDegrees, prepared.SuspensionLengthMeters, deltaTimeSeconds);
            return telemetry;
        }

        private bool TryGetWheelContact(
            WheelState wheel,
            Vector3 centerOrigin,
            Vector3 direction,
            float maximumDistance,
            float steeringDegrees,
            out WheelContact contact)
        {
            var sampleCount = Mathf.Clamp(definition.Tire.ContactPatchSampleCount, 1, 5);
            var steerRotation = Quaternion.AngleAxis(steeringDegrees, transform.up);
            var sampleRight = (steerRotation * transform.right).normalized;
            var halfWidth = (float)definition.Tire.SectionWidthMeters * 0.5f;
            var nearestDistance = float.PositiveInfinity;
            var nearestHit = default(RaycastHit);
            var normalSum = Vector3.zero;
            var hitCount = 0;

            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var normalizedOffset = sampleCount == 1
                    ? 0f
                    : ((sampleIndex / (float)(sampleCount - 1)) * 2f) - 1f;
                var origin = centerOrigin + (sampleRight * (normalizedOffset * halfWidth));
                if (!TryGetExternalRayContact(origin, direction, maximumDistance, out var hit))
                {
                    continue;
                }

                hitCount++;
                normalSum += hit.normal;
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                }
            }

            if (hitCount == 0)
            {
                contact = default(WheelContact);
                return false;
            }

            contact = new WheelContact
            {
                Point = nearestHit.point,
                Normal = normalSum.sqrMagnitude > 0.000001f ? normalSum.normalized : nearestHit.normal,
                Distance = nearestDistance,
                Collider = nearestHit.collider,
                SampleCount = hitCount
            };
            return true;
        }

        private bool TryGetExternalRayContact(Vector3 origin, Vector3 direction, float maximumDistance, out RaycastHit nearestHit)
        {
            // Compound body colliders may sit below the suspension mounts. Wheel rays must not support the car on itself.
            var hitCount = UnityEngine.Physics.RaycastNonAlloc(origin, direction, contactHits, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
            while (hitCount == contactHits.Length)
            {
                Array.Resize(ref contactHits, contactHits.Length * 2);
                hitCount = UnityEngine.Physics.RaycastNonAlloc(origin, direction, contactHits, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
            }

            nearestHit = default(RaycastHit);
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = contactHits[index];
                if (hit.rigidbody == body || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        private void UpdatePowertrain()
        {
            var drivenAngularSpeed = AverageDrivenWheelAngularSpeed();
            var gearRatio = Mathf.Abs((float)gearSelector.GetSelectedGearRatio(definition.Transmission));
            var coupledRpm = drivenAngularSpeed * gearRatio * (float)definition.Transmission.FinalDriveRatio
                * (float)UnitConversion.RadiansPerSecondToRevolutionsPerMinute;
            var launchRpm = (float)definition.Engine.IdleSpeedRpm + (input.Throttle * 900f);
            if (SelectedGear == 0 || coupledRpm <= definition.Engine.RedlineSpeedRpm - 100d)
                engineRevLimiterActive = false;
            else if (coupledRpm >= definition.Engine.RedlineSpeedRpm)
                engineRevLimiterActive = true;
            engineSpeedRpm = Mathf.Clamp(Mathf.Max(coupledRpm, launchRpm),
                (float)definition.Engine.IdleSpeedRpm, (float)definition.Engine.RedlineSpeedRpm);
            if (gearSelector.Mode != DriveTransmissionMode.Automatic || SelectedGear <= 0) return;
            var target = SelectedGear;
            if (engineSpeedRpm >= definition.Transmission.UpshiftSpeedRpm
                && target < definition.Transmission.ForwardGearRatios.Length) target++;
            else if (engineSpeedRpm <= definition.Transmission.DownshiftSpeedRpm && target > 1) target--;
            if (target != SelectedGear)
                ApplyGearSelection(gearSelector.SelectAutomaticGear(target, definition.Transmission.ForwardGearRatios.Length));
        }

        private float AverageDrivenWheelAngularSpeed(bool signed = false)
        {
            var drivenAngularSpeed = 0f;
            var count = 0;
            foreach (var wheel in wheels)
            {
                if (!wheel.Driven)
                {
                    continue;
                }

                drivenAngularSpeed += signed ? wheel.AngularSpeedRadiansPerSecond : Mathf.Abs(wheel.AngularSpeedRadiansPerSecond);
                count++;
            }

            return drivenAngularSpeed / Mathf.Max(count, 1);
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
            var cylinder = new GameObject("Tire", typeof(MeshFilter), typeof(MeshRenderer));
            cylinder.name = "Tire";
            cylinder.transform.SetParent(pivot, false);
            cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cylinder.GetComponent<MeshFilter>().sharedMesh = tireMesh;
            if (tireMaterial == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");
                tireMaterial = new Material(shader)
                {
                    name = "Prototype tire rubber",
                    color = new Color(0.04f, 0.045f, 0.05f)
                };
            }

            cylinder.GetComponent<MeshRenderer>().sharedMaterial = tireMaterial;

            wheel.VisualPivot = pivot;
            wheel.VisualWheel = cylinder.transform;
        }

        private void UpdateWheelVisual(WheelState wheel, float steeringDegrees, float suspensionLengthMeters, float deltaTimeSeconds)
        {
            if (wheel.VisualPivot == null)
            {
                return;
            }

            wheel.VisualPivot.localPosition = wheel.LocalMountPosition + (Vector3.down * suspensionLengthMeters);
            wheel.VisualPivot.localRotation = Quaternion.Euler(0f, steeringDegrees, 0f);
            wheel.VisualSpinDegrees = Mathf.Repeat(
                wheel.VisualSpinDegrees + (wheel.AngularSpeedRadiansPerSecond * Mathf.Rad2Deg * deltaTimeSeconds),
                360f);
            wheel.VisualWheel.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.AngleAxis(wheel.VisualSpinDegrees, Vector3.up);
        }

        private void UpdateTireMesh()
        {
            // Bake dimensions into shared mesh vertices; every controlled transform stays at unit scale.
            const int segments = 32;
            var radius = (float)definition.Tire.UnloadedRadiusMeters;
            var halfWidth = (float)definition.Tire.SectionWidthMeters * 0.5f;
            var vertices = new Vector3[(segments * 4) + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 12];
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[index * 2] = radial * radius + Vector3.down * halfWidth;
                vertices[(index * 2) + 1] = radial * radius + Vector3.up * halfWidth;
                normals[index * 2] = normals[(index * 2) + 1] = radial;
                vertices[(segments * 2) + index] = vertices[index * 2];
                vertices[(segments * 3) + index] = vertices[(index * 2) + 1];
                normals[(segments * 2) + index] = Vector3.down;
                normals[(segments * 3) + index] = Vector3.up;
                var next = (index + 1) % segments;
                var offset = index * 12;
                triangles[offset] = index * 2;
                triangles[offset + 1] = (index * 2) + 1;
                triangles[offset + 2] = next * 2;
                triangles[offset + 3] = next * 2;
                triangles[offset + 4] = (index * 2) + 1;
                triangles[offset + 5] = (next * 2) + 1;
                triangles[offset + 6] = segments * 4;
                triangles[offset + 7] = (segments * 2) + index;
                triangles[offset + 8] = (segments * 2) + next;
                triangles[offset + 9] = (segments * 4) + 1;
                triangles[offset + 10] = (segments * 3) + next;
                triangles[offset + 11] = (segments * 3) + index;
            }

            vertices[segments * 4] = Vector3.down * halfWidth;
            vertices[(segments * 4) + 1] = Vector3.up * halfWidth;
            normals[segments * 4] = Vector3.down;
            normals[(segments * 4) + 1] = Vector3.up;
            if (tireMesh == null)
            {
                tireMesh = new Mesh { name = "Prototype dimensioned tire" };
            }

            tireMesh.Clear();
            tireMesh.vertices = vertices;
            tireMesh.normals = normals;
            tireMesh.triangles = triangles;
            tireMesh.RecalculateBounds();
        }

        private float NominalRootHeightMeters()
        {
            return (float)definition.Tire.UnloadedRadiusMeters + NominalSuspensionLengthMeters();
        }

        private float NominalSuspensionLengthMeters()
        {
            var nominalCompression = (float)(definition.MassKilograms * 9.80665d / (WheelCount * definition.Suspension.SpringRateNewtonsPerMeter));
            return Mathf.Clamp((float)definition.Suspension.RestLengthMeters - nominalCompression,
                0f, (float)(definition.Suspension.RestLengthMeters + definition.Suspension.TravelMeters));
        }

        private void OnDestroy()
        {
            ReleaseOwnedObject(tireMesh);
            ReleaseOwnedObject(tireMaterial);
        }

        private static void ReleaseOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }

        private struct PreparedWheelStep
        {
            public WheelContactInput Rotation;
            public WheelTelemetry Telemetry;
            public Vector3 WheelForward;
            public Vector3 WheelRight;
            public float SteeringDegrees;
            public float SuspensionLengthMeters;
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

        private struct WheelContact
        {
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;
            public Collider Collider;
            public int SampleCount;
        }
    }
}
