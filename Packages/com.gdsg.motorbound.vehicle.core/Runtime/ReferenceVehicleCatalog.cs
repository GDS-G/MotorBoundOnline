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
                    EngineeringFamilyId = PowertrainId("kiyora-k-v-engineering-family"),
                    EngineeringFamilyRevision = 1,
                    AssemblySpecificationId = PowertrainId("kiyora-kv20-reference-long-block-specification"),
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
                    EngineeringFamilyId = PowertrainId("kiyora-aven-six-speed-manual-family"),
                    EngineeringFamilyRevision = 1,
                    AssemblySpecificationId = PowertrainId("kiyora-aven-six-speed-manual-reference-specification"),
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

        public static VehicleAssemblyManifest CreateKiyoraAvenPrototypeAssemblyManifest()
        {
            var chassisInstanceId = InstanceId("kiyora-aven-prototype-chassis-instance");
            var engineInstanceId = InstanceId("kiyora-aven-prototype-kv20-instance");
            var transmissionInstanceId = InstanceId("kiyora-aven-prototype-six-speed-instance");
            var runningGearInstanceId = InstanceId("kiyora-aven-prototype-running-gear-instance");

            return new VehicleAssemblyManifest
            {
                ManifestId = ManifestId("kiyora-aven-club-prototype-assembly"),
                Revision = 1,
                VehicleDefinitionId = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-club-prototype"),
                Parts = new[]
                {
                    new InstalledPartInstance
                    {
                        InstanceId = chassisInstanceId,
                        PartDefinitionId = PartId("kiyora-aven-club-body-chassis-assembly"),
                        PartDefinitionRevision = 1,
                        DisplayName = "Kiyora Aven Club body and chassis assembly",
                        InstallState = AssemblyInstallState.Released,
                        Condition01 = 1d,
                        MassKilograms = 620d,
                        OwnsMass = true,
                        MassAccountingGroupId = MassGroupId("kiyora-aven-prototype-body-chassis"),
                        CostMinorUnits = 1600000L,
                        OwnsCost = true
                    },
                    new InstalledPartInstance
                    {
                        InstanceId = engineInstanceId,
                        PartDefinitionId = PartId("kiyora-kv20-reference-dressed-engine"),
                        PartDefinitionRevision = 1,
                        DisplayName = "Kiyora K-V 2.0 reference dressed engine",
                        InstallState = AssemblyInstallState.Released,
                        Condition01 = 1d,
                        MassKilograms = 145d,
                        OwnsMass = true,
                        MassAccountingGroupId = MassGroupId("kiyora-aven-prototype-engine"),
                        CostMinorUnits = 620000L,
                        OwnsCost = true,
                        IncludedPartDefinitionIds = new[]
                        {
                            PartId("kiyora-kv20-reference-long-block"),
                            PartId("kiyora-kv20-reference-induction"),
                            PartId("kiyora-kv20-reference-accessory-drive")
                        }
                    },
                    new InstalledPartInstance
                    {
                        InstanceId = transmissionInstanceId,
                        PartDefinitionId = PartId("kiyora-aven-reference-six-speed-manual"),
                        PartDefinitionRevision = 1,
                        DisplayName = "Kiyora Aven six-speed manual assembly",
                        InstallState = AssemblyInstallState.Released,
                        Condition01 = 1d,
                        MassKilograms = 45d,
                        OwnsMass = true,
                        MassAccountingGroupId = MassGroupId("kiyora-aven-prototype-transmission"),
                        CostMinorUnits = 280000L,
                        OwnsCost = true
                    },
                    new InstalledPartInstance
                    {
                        InstanceId = runningGearInstanceId,
                        PartDefinitionId = PartId("kiyora-aven-club-running-gear-and-interior"),
                        PartDefinitionRevision = 1,
                        DisplayName = "Kiyora Aven Club running gear, interior, and operating fluids",
                        InstallState = AssemblyInstallState.Released,
                        Condition01 = 1d,
                        MassKilograms = 310d,
                        OwnsMass = true,
                        MassAccountingGroupId = MassGroupId("kiyora-aven-prototype-running-gear"),
                        CostMinorUnits = 850000L,
                        OwnsCost = true
                    }
                },
                Relationships = new[]
                {
                    Relationship("kiyora-aven-engine-to-chassis", AssemblyRelationshipKind.MechanicalAttachment, engineInstanceId, chassisInstanceId, "KIY-KV-MOUNT-V1"),
                    Relationship("kiyora-aven-transmission-to-engine", AssemblyRelationshipKind.MechanicalAttachment, transmissionInstanceId, engineInstanceId, "KIY-KV-BELLHOUSING-V1"),
                    Relationship("kiyora-aven-engine-power-to-transmission", AssemblyRelationshipKind.PowerFlow, engineInstanceId, transmissionInstanceId, "KIY-KV-CLUTCH-INPUT-V1"),
                    Relationship("kiyora-aven-running-gear-to-chassis", AssemblyRelationshipKind.MechanicalAttachment, runningGearInstanceId, chassisInstanceId, "KIY-AVEN-RUNNING-GEAR-V1")
                }
            };
        }

        public static VehicleOperatingEnvelope CreateKiyoraAvenPrototypeOperatingEnvelope()
        {
            return new VehicleOperatingEnvelope
            {
                SourceManifestId = ManifestId("kiyora-aven-club-prototype-assembly"),
                SourceManifestRevision = 1,
                DesignClass = VehicleDesignClass.LowSportsCar,
                OverallLengthMeters = 3.96d,
                BodyWidthMeters = 1.73d,
                MaximumWidthMeters = 1.91d,
                OverallHeightMeters = 1.24d,
                WheelbaseMeters = 2.35d,
                GroundClearanceMeters = 0.13d,
                ApproachAngleDegrees = 13d,
                DepartureAngleDegrees = 19d,
                BreakoverAngleDegrees = 13d,
                TurningCircleDiameterMeters = 10.2d,
                CombinationLengthMeters = 3.96d,
                GrossMassKilograms = 1450d,
                MaximumAxleLoadKilograms = 800d,
                HasTrailer = false
            };
        }

        public static DimensionalRestrictionProfile CreateStandardPassengerGarageProfile()
        {
            return new DimensionalRestrictionProfile
            {
                ProfileId = StableId.FromCatalogKey("motorbound.facility.profile", "standard-passenger-garage-prototype"),
                Revision = 1,
                DisplayName = "Standard passenger garage prototype",
                PermittedDesignClasses = new[]
                {
                    VehicleDesignClass.LowSportsCar,
                    VehicleDesignClass.CompactPassengerCar,
                    VehicleDesignClass.StandardPassengerCar,
                    VehicleDesignClass.SportUtilityVehicle,
                    VehicleDesignClass.PickupTruck
                },
                PermitsTrailers = false,
                PhysicalClearWidthMeters = 2.50d,
                PhysicalClearHeightMeters = 2.10d,
                PostedClearHeightMeters = 2.00d,
                VerticalSafetyMarginMeters = 0.05d,
                LateralSafetyMarginMeters = 0.10d,
                MaximumPostedToPhysicalVarianceMeters = 0.10d,
                MaximumVehicleLengthMeters = 5.50d,
                MaximumWheelbaseMeters = 3.30d,
                MaximumCombinationLengthMeters = 5.50d,
                MaximumGrossMassKilograms = 3500d,
                MaximumAxleLoadKilograms = 1900d,
                MinimumGroundClearanceMeters = 0.08d,
                RequiredApproachAngleDegrees = 8d,
                RequiredDepartureAngleDegrees = 8d,
                RequiredBreakoverAngleDegrees = 8d,
                MaximumTurningCircleDiameterMeters = 12.5d
            };
        }

        public static PowertrainBuildPlan CreateKiyoraAvenFactoryPowertrainBuildPlan()
        {
            var mountStep = BuildStepId("kiyora-aven-kv20-mount-kit");
            var coolingStep = BuildStepId("kiyora-aven-kv20-cooling-kit");
            var fuelStep = BuildStepId("kiyora-aven-kv20-fuel-kit");
            var controlStep = BuildStepId("kiyora-aven-kv20-control-harness");
            var exhaustStep = BuildStepId("kiyora-aven-kv20-exhaust-kit");
            var engineStep = BuildStepId("kiyora-aven-kv20-dressed-engine");

            return new PowertrainBuildPlan
            {
                PlanId = StableId.FromCatalogKey("motorbound.build-plan", "kiyora-aven-factory-powertrain-reference"),
                Revision = 1,
                VehicleDefinitionId = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-club-prototype"),
                SourceManifestId = ManifestId("kiyora-aven-club-prototype-assembly"),
                SourceManifestRevision = 1,
                Goal = "Complete and validate the factory K-V 2.0 reference powertrain",
                Steps = new[]
                {
                    BuildStep(mountStep, "Kiyora Aven K-V mount kit", "kiyora-aven-kv20-mount-kit", 84000L),
                    BuildStep(coolingStep, "Kiyora Aven K-V cooling and hose kit", "kiyora-aven-kv20-cooling-kit", 132000L),
                    BuildStep(fuelStep, "Kiyora Aven K-V fuel supply kit", "kiyora-aven-kv20-fuel-kit", 96000L),
                    BuildStep(controlStep, "Kiyora Aven K-V controller and harness", "kiyora-aven-kv20-control-harness", 175000L),
                    BuildStep(exhaustStep, "Kiyora Aven K-V exhaust and closure kit", "kiyora-aven-kv20-exhaust-kit", 148000L),
                    new BuildPlanStep
                    {
                        StepId = engineStep,
                        PartDefinitionId = PartId("kiyora-kv20-reference-dressed-engine"),
                        PartDefinitionRevision = 1,
                        DisplayName = "Kiyora K-V 2.0 reference dressed engine",
                        RequirementKind = BuildPlanRequirementKind.HardRequirement,
                        Quantity = 1,
                        EstimatedUnitCostMinorUnits = 620000L,
                        RequiredStepIds = new[] { mountStep, coolingStep, fuelStep, controlStep, exhaustStep },
                        RequiredToolsAndServices = new[] { "engine-hoist", "cooling-system-fill", "controller-commissioning" },
                        AffectedCalibrations = new[] { "factory-engine-control", "idle-and-throttle-learn" },
                        ValidationTests = new[] { "mount-residual", "service-network-completeness", "leak-test", "factory-operating-data" },
                        AlternativeSummary = "Use only a revisioned K-V assembly or a separately validated conversion package.",
                        EligibilityConsequences = "Road release requires completed services, factory calibration, and inspection.",
                        IsReversible = true
                    }
                }
            };
        }

        private static StableId Id(string key)
        {
            return StableId.FromCatalogKey("motorbound.vehicle.architecture", key);
        }

        private static StableId PowertrainId(string key)
        {
            return StableId.FromCatalogKey("motorbound.powertrain", key);
        }

        private static StableId PartId(string key)
        {
            return StableId.FromCatalogKey("motorbound.part", key);
        }

        private static StableId InstanceId(string key)
        {
            return StableId.FromCatalogKey("motorbound.part-instance.prototype", key);
        }

        private static StableId ManifestId(string key)
        {
            return StableId.FromCatalogKey("motorbound.vehicle-manifest", key);
        }

        private static StableId MassGroupId(string key)
        {
            return StableId.FromCatalogKey("motorbound.mass-accounting-group", key);
        }

        private static StableId BuildStepId(string key)
        {
            return StableId.FromCatalogKey("motorbound.build-plan-step", key);
        }

        private static AssemblyRelationship Relationship(
            string key,
            AssemblyRelationshipKind kind,
            StableId from,
            StableId to,
            string interfaceFamilyAndVersion)
        {
            return new AssemblyRelationship
            {
                RelationshipId = StableId.FromCatalogKey("motorbound.assembly-relationship.prototype", key),
                Kind = kind,
                FromPartInstanceId = from,
                ToPartInstanceId = to,
                FromPortId = "primary",
                ToPortId = "primary",
                InterfaceFamilyAndVersion = interfaceFamilyAndVersion,
                IsSatisfied = true
            };
        }

        private static BuildPlanStep BuildStep(StableId stepId, string displayName, string partKey, long estimatedCostMinorUnits)
        {
            return new BuildPlanStep
            {
                StepId = stepId,
                PartDefinitionId = PartId(partKey),
                PartDefinitionRevision = 1,
                DisplayName = displayName,
                RequirementKind = BuildPlanRequirementKind.HardRequirement,
                Quantity = 1,
                EstimatedUnitCostMinorUnits = estimatedCostMinorUnits,
                RequiredToolsAndServices = new[] { "reference-workshop" },
                ValidationTests = new[] { "identity-and-interface", "installation-completeness" },
                IsReversible = true
            };
        }
    }
}
