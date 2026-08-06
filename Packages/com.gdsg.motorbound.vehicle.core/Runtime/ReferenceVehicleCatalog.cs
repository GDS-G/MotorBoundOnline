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
                Architecture = new VehicleArchitectureIdentity
                {
                    FamilyId = Id("kiyora-aven-family"),
                    GenerationId = Id("kiyora-aven-generation-1"),
                    PlatformId = Id("kiyora-aven-roadster-platform-1"),
                    BodyShellId = Id("kiyora-aven-roadster-shell-1"),
                    ChassisConfigurationId = Id("kiyora-aven-club-chassis-1"),
                    PowertrainConfigurationId = Id("kiyora-aven-kv20-rwd-6mt"),
                    InteriorConfigurationId = Id("kiyora-aven-club-interior-1"),
                    TrimManifestId = Id("kiyora-aven-club-trim-1")
                },
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
                    SectionWidthMeters = 0.205d,
                    ContactPatchSampleCount = 3,
                    TreadWaterEvacuationFactor = 0.68d,
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

        public static VehicleFitmentContext CreateKiyoraAvenClubFitmentContext()
        {
            return new VehicleFitmentContext
            {
                PlatformId = Id("kiyora-aven-roadster-platform-1"),
                Hardpoints = new[]
                {
                    new HardpointDefinition
                    {
                        HardpointId = Id("kiyora-aven-front-left-hub-hardpoint"),
                        Version = 1,
                        Name = "Front left hub",
                        MountPatternCode = "KIY-AVEN-HUB-4X100-V1",
                        PositionXMeters = -0.73d,
                        PositionYMeters = -0.34d,
                        PositionZMeters = 1.175d,
                        RatedLoadNewtons = 18000d,
                        PositionalToleranceMeters = 0.0015d,
                        AvailableEnvelope = new SpatialEnvelope(0.235d, 0.66d, 0.66d),
                        ServiceClearanceMeters = 0.12d,
                        NominalVoltage = 12d
                    }
                }
            };
        }

        public static ComponentFitmentDefinition CreateKiyoraAvenReferenceFrontWheelFitment()
        {
            return new ComponentFitmentDefinition
            {
                ComponentDefinitionId = StableId.FromCatalogKey("motorbound.part", "kiyora-aven-club-front-wheel-16x7"),
                Revision = 1,
                DisplayName = "Kiyora Aven Club 16 x 7 front wheel and tire",
                RequiredPlatformId = Id("kiyora-aven-roadster-platform-1"),
                RequiredHardpointId = Id("kiyora-aven-front-left-hub-hardpoint"),
                RequiredMountPatternCode = "KIY-AVEN-HUB-4X100-V1",
                OccupiedEnvelope = new SpatialEnvelope(0.205d, 0.61d, 0.61d),
                DynamicSweepAllowanceMeters = 0.03d,
                AlignmentToleranceMeters = 0.002d,
                AppliedLoadNewtons = 6200d,
                RequiredServiceClearanceMeters = 0.08d,
                NominalVoltage = 0d,
                HasRequiredControlProtocol = true,
                IsRegulatoryCompliant = true,
                HasRequiredVisualClosureParts = true
            };
        }

        private static StableId Id(string key)
        {
            return StableId.FromCatalogKey("motorbound.vehicle.architecture", key);
        }
    }
}
