using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public static class ReferenceVehicleCatalog
    {
        public static VehicleDefinition CreateKiyoraAvenClubPrototype()
        {
            return new VehicleDefinition
            {
                DefinitionId = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-club-prototype"),
                DefinitionVersion = 1,
                Manufacturer = "Kiyora",
                Family = "Aven",
                Trim = "Club Prototype",
                DriveLayout = DriveLayout.RearWheelDrive,
                MassKilograms = 1120d,
                WheelbaseMeters = 2.35d,
                FrontTrackMeters = 1.46d,
                RearTrackMeters = 1.45d,
                CenterOfMassHeightMeters = 0.46d,
                CenterOfMassLongitudinalOffsetMeters = -0.08d,
                FrontalAreaSquareMeters = 1.82d,
                AerodynamicDragCoefficient = 0.32d,
                AerodynamicLiftCoefficient = 0.08d,
                MaximumSteeringAngleDegrees = 32d,
                Engine = new EngineDefinition
                {
                    Architecture = "K-V 2.0 L inline-four",
                    DisplacementLiters = 2.0d,
                    IdleSpeedRpm = 900d,
                    RedlineSpeedRpm = 7600d,
                    RotationalInertiaKilogramMetersSquared = 0.2d,
                    EngineBrakingTorqueNewtonMeters = 44d,
                    FullLoadTorqueCurve = new[]
                    {
                        new TorqueSample(900d, 115d),
                        new TorqueSample(2000d, 158d),
                        new TorqueSample(3500d, 190d),
                        new TorqueSample(5000d, 205d),
                        new TorqueSample(6500d, 198d),
                        new TorqueSample(7600d, 160d)
                    }
                },
                Transmission = new TransmissionDefinition
                {
                    ForwardGearRatios = new[] { 3.54d, 2.13d, 1.48d, 1.16d, 0.97d, 0.82d },
                    ReverseGearRatio = -3.33d,
                    FinalDriveRatio = 4.1d,
                    Efficiency = 0.91d,
                    UpshiftSpeedRpm = 7100d,
                    DownshiftSpeedRpm = 2800d
                },
                Tire = new TireDefinition
                {
                    UnloadedRadiusMeters = 0.305d,
                    RotationalInertiaKilogramMetersSquared = 1.15d,
                    PeakDryFrictionCoefficient = 1.08d,
                    LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
                    CorneringStiffnessNewtonPerRadian = 65000d,
                    RollingResistanceCoefficient = 0.013d,
                    ReferenceLoadNewtons = 3300d,
                    LoadSensitivityExponent = -0.08d
                },
                Suspension = new SuspensionDefinition
                {
                    RestLengthMeters = 0.34d,
                    TravelMeters = 0.18d,
                    SpringRateNewtonsPerMeter = 34000d,
                    DamperCompressionNewtonsPerMeterPerSecond = 3600d,
                    DamperReboundNewtonsPerMeterPerSecond = 4300d
                },
                Brakes = new BrakeDefinition
                {
                    MaximumServiceBrakeTorqueNewtonMeters = 2400d,
                    MaximumHandbrakeTorqueNewtonMeters = 2800d,
                    FrontBias = 0.62d
                }
            };
        }
    }
}
