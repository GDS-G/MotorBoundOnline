using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    [Serializable]
    public sealed class PrototypeGarageState
    {
        // Zero is deliberately invalid so an omitted serialized version cannot
        // inherit the supported schema from a constructor/field initializer.
        public int SchemaVersion;
        public VehicleAssemblyManifest Manifest;
    }

    [Serializable]
    public sealed class PrototypeWheelPackage
    {
        public string Key;
        public string DisplayName;
        public string Description;
    }

    public sealed class PrototypeGarageConfiguration
    {
        public VehicleDefinition Vehicle;
        public VehicleOperatingEnvelope Envelope;
        public string WheelPackageKey;
        public string WheelPackageName;
        public bool HasCompanionKit;
    }

    public sealed class PrototypeGarageProposal
    {
        public bool CanInstall;
        public string Summary;
        public BuildPlanEvaluation Plan;
        public FitmentResult Fitment;
    }

    /// <summary>
    /// Closed, revisioned reference catalog for the one-car garage milestone. Saved
    /// manifests select authored parts; they cannot supply arbitrary physics values.
    /// This is not a general assembly solver or a calibrated aftermarket catalog.
    /// </summary>
    public static class PrototypeGarageCatalog
    {
        public const string StockRoadKey = "stock-road";
        public const string TouringKey = "touring-225";
        public const string IncompatibleKey = "oversize-5lug";

        private static readonly string[] WheelLocations = { "front-left", "front-right", "rear-left", "rear-right" };
        private static readonly StableId KitInstanceId = InstanceId("touring-companion-kit");
        private static readonly StableId KitDefinitionId = PartId("touring-companion-kit");
        private static readonly StableId KitStepId = StepId("touring-companion-kit");

        public static PrototypeWheelPackage[] Options => new[]
        {
            new PrototypeWheelPackage
            {
                Key = StockRoadKey,
                DisplayName = "Stock road — 205 mm",
                Description = "Four stock wheels/tires, 305 mm radius. Reference vehicle setup."
            },
            new PrototypeWheelPackage
            {
                Key = TouringKey,
                DisplayName = "Touring — 225 mm",
                Description = "Four 315 mm-radius wheels/tires with 225 mm sections. Requires touring companion hardware. Illustrative, uncalibrated catalog values."
            },
            new PrototypeWheelPackage
            {
                Key = IncompatibleKey,
                DisplayName = "Oversize five-lug — incompatible",
                Description = "Wrong hub pattern and excessive envelope; no approved conversion in this prototype."
            }
        };

        public static PrototypeGarageState CreateNewState()
        {
            return new PrototypeGarageState { SchemaVersion = 1, Manifest = CreateManifest(StockRoadKey, false) };
        }

        public static PrototypeGarageConfiguration Compile(PrototypeGarageState state)
        {
            var issues = Validate(state);
            if (issues.Count != 0)
            {
                throw new ArgumentException("Garage state is invalid: " + issues[0], nameof(state));
            }

            var key = InstalledPackageKey(state.Manifest);
            var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            var stockSettledHeight = SettledRootHeight(vehicle);
            vehicle.MassKilograms = state.Manifest.ComputeAuthoritativeMassKilograms();
            if (key == TouringKey)
            {
                vehicle.Tire.UnloadedRadiusMeters = 0.315d;
                vehicle.Tire.SectionWidthMeters = 0.225d;
                vehicle.Tire.TreadWaterEvacuationFactor = 0.78d;
                // Keep dry compound/stiffness unchanged. This inertia uses the same
                // reference wheel mass-distribution factor with the authored mass/radius.
                vehicle.Tire.RotationalInertiaKilogramMetersSquared *= (20d / 18d) * Math.Pow(0.315d / 0.305d, 2d);
            }

            var heightDelta = SettledRootHeight(vehicle) - stockSettledHeight;
            vehicle.CenterOfMassHeightMeters += heightDelta;
            var envelope = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope();
            envelope.SourceManifestId = state.Manifest.ManifestId;
            envelope.SourceManifestRevision = state.Manifest.Revision;
            envelope.GrossMassKilograms = vehicle.MassKilograms;
            envelope.MaximumAxleLoadKilograms = vehicle.MassKilograms * 0.55d;
            envelope.MaximumWidthMeters = Math.Max(1.91d, Math.Max(vehicle.FrontTrackMeters, vehicle.RearTrackMeters) + vehicle.Tire.SectionWidthMeters);
            envelope.OverallHeightMeters += heightDelta;
            envelope.GroundClearanceMeters += heightDelta;
            // Preserve authored reference overhang angles and adjust their clearance
            // triangle only. This level-ground estimate does not certify swept fitment.
            var clearanceRatio = envelope.GroundClearanceMeters / 0.13d;
            envelope.ApproachAngleDegrees = AdjustAngle(envelope.ApproachAngleDegrees, clearanceRatio);
            envelope.DepartureAngleDegrees = AdjustAngle(envelope.DepartureAngleDegrees, clearanceRatio);
            envelope.BreakoverAngleDegrees = 2d * AdjustAngle(envelope.BreakoverAngleDegrees / 2d, clearanceRatio);
            return new PrototypeGarageConfiguration
            {
                Vehicle = vehicle,
                Envelope = envelope,
                WheelPackageKey = key,
                WheelPackageName = PackageName(key),
                HasCompanionKit = state.Manifest.FindPart(KitInstanceId) != null
            };
        }

        public static IReadOnlyList<ValidationIssue> Validate(PrototypeGarageState state)
        {
            var issues = new List<ValidationIssue>();
            if (state == null)
            {
                issues.Add(new ValidationIssue("garage", "A saved garage state is required."));
                return issues;
            }

            Require(state.SchemaVersion == 1, "garage.schemaVersion", "Unsupported garage schema; expected version 1.", issues);
            if (state.Manifest == null)
            {
                issues.Add(new ValidationIssue("garage.manifest", "Installed assembly manifest is required."));
                return issues;
            }

            var manifest = state.Manifest;
            foreach (var issue in manifest.Validate())
            {
                issues.Add(issue);
            }

            Require(manifest.Revision > 0, "assemblyManifest.revision", "Revision must be positive.", issues);
            var key = InstalledPackageKey(manifest);
            Require(key != null, "assemblyManifest.wheels", "A complete approved wheel package is required.", issues);
            var hasKit = manifest.FindPart(KitInstanceId) != null;
            Require(key != TouringKey || hasKit, "assemblyManifest.companionKit", "Touring wheels require the installed touring companion hardware kit.", issues);
            var expected = CreateManifest(key ?? StockRoadKey, hasKit);
            Require(manifest.ManifestId == expected.ManifestId, "assemblyManifest.manifestId", "Unknown reference garage manifest ID.", issues);
            Require(manifest.VehicleDefinitionId == expected.VehicleDefinitionId, "assemblyManifest.vehicleDefinitionId", "Only the revisioned Kiyora Aven reference build is supported.", issues);
            Require(manifest.Parts != null && manifest.Parts.Length == expected.Parts.Length, "assemblyManifest.parts", "Unexpected, duplicate, or missing installed parts.", issues);

            if (manifest.Parts != null)
            {
                foreach (var actual in manifest.Parts)
                {
                    if (actual == null) continue;
                    var approved = expected.FindPart(actual.InstanceId);
                    if (approved == null)
                    {
                        issues.Add(new ValidationIssue("assemblyManifest.parts", "Unknown part-instance ID: " + actual.InstanceId));
                        continue;
                    }

                    ValidatePart(actual, approved, issues);
                }

                foreach (var approved in expected.Parts)
                {
                    Require(manifest.FindPart(approved.InstanceId) != null, "assemblyManifest.parts", "Missing installed part: " + approved.DisplayName, issues);
                }
            }

            Require(manifest.Relationships != null && manifest.Relationships.Length == expected.Relationships.Length,
                "assemblyManifest.relationships", "Complete approved assembly relationships are required.", issues);
            if (manifest.Relationships != null)
            {
                foreach (var approved in expected.Relationships)
                {
                    AssemblyRelationship actual = null;
                    foreach (var candidate in manifest.Relationships)
                    {
                        if (candidate != null && candidate.RelationshipId == approved.RelationshipId) actual = candidate;
                    }

                    var path = "assemblyManifest.relationships." + approved.RelationshipId;
                    Require(actual != null, path, "Missing approved relationship.", issues);
                    if (actual == null) continue;
                    Require(actual.Kind == approved.Kind && actual.FromPartInstanceId == approved.FromPartInstanceId
                        && actual.ToPartInstanceId == approved.ToPartInstanceId && actual.FromPortId == approved.FromPortId
                        && actual.ToPortId == approved.ToPortId && actual.InterfaceFamilyAndVersion == approved.InterfaceFamilyAndVersion
                        && actual.IsSatisfied, path, "Relationship does not match the released reference assembly.", issues);
                }
            }

            return issues;
        }

        public static PrototypeGarageProposal PreviewWheelPackage(PrototypeGarageState state, string packageKey)
        {
            var issues = Validate(state);
            if (issues.Count > 0) return Rejected("Invalid saved configuration: " + issues[0]);
            if (packageKey != StockRoadKey && packageKey != TouringKey && packageKey != IncompatibleKey)
                return Rejected("Unknown wheel package. Select one of the authored prototype packages.");
            if (state.Manifest.Revision == int.MaxValue && InstalledPackageKey(state.Manifest) != packageKey)
                return Rejected("Assembly revision limit reached. The saved configuration can still be loaded and driven, but cannot accept further changes.");

            var kitInstalled = state.Manifest.FindPart(KitInstanceId) != null;
            var fitment = ReferenceVehicleCatalog.CreateKiyoraAvenReferenceFrontWheelFitment();
            fitment.ComponentDefinitionId = WheelDefinitionId(packageKey);
            fitment.DisplayName = PackageName(packageKey);
            fitment.OccupiedEnvelope = packageKey == TouringKey
                ? new SpatialEnvelope(0.225d, 0.63d, 0.63d)
                : packageKey == IncompatibleKey ? new SpatialEnvelope(0.32d, 0.8d, 0.8d) : new SpatialEnvelope(0.205d, 0.61d, 0.61d);
            if (packageKey == IncompatibleKey) fitment.RequiredMountPatternCode = "UNSUPPORTED-5X114.3";
            var result = FitmentValidator.Evaluate(ReferenceVehicleCatalog.CreateKiyoraAvenClubFitmentContext(), fitment);
            var steps = new List<BuildPlanStep>();
            if (packageKey == TouringKey && kitInstalled)
            {
                steps.Add(new BuildPlanStep
                {
                    StepId = KitStepId, PartDefinitionId = KitDefinitionId,
                    DisplayName = "Reuse installed touring companion hardware", Quantity = 1,
                    EstimatedUnitCostMinorUnits = 0L
                });
            }

            steps.Add(new BuildPlanStep
            {
                StepId = StepId(packageKey), PartDefinitionId = WheelDefinitionId(packageKey),
                DisplayName = PackageName(packageKey), Quantity = 4,
                EstimatedUnitCostMinorUnits = packageKey == TouringKey ? 45000L : 30000L,
                RequiredStepIds = packageKey == TouringKey ? new[] { KitStepId } : Array.Empty<StableId>(),
                RequiredToolsAndServices = new[] { "prototype-workshop", "wheel-fastener-check" },
                ValidationTests = new[] { "approved-four-wheel-set", "released-manifest", "clearance-profile" }
            });
            var plan = BuildPlanEvaluator.Evaluate(new PowertrainBuildPlan
            {
                PlanId = StableId.FromCatalogKey("motorbound.build-plan", "garage-" + packageKey),
                VehicleDefinitionId = state.Manifest.VehicleDefinitionId,
                SourceManifestId = state.Manifest.ManifestId, SourceManifestRevision = state.Manifest.Revision,
                Goal = "Install the authored four-wheel package", Steps = steps.ToArray()
            });
            var canInstall = result.Classification == FitmentClassification.BoltIn && plan.IsValid;
            return new PrototypeGarageProposal
            {
                CanInstall = canInstall, Plan = plan, Fitment = result,
                Summary = packageKey == IncompatibleKey
                    ? "Cannot install: five-lug mount does not match the Aven 4x100 hubs and the wheel envelope exceeds the approved space."
                    : packageKey == TouringKey && !kitInstalled
                        ? "Missing companion part: install the touring hardware kit before fitting all four touring wheels."
                        : InstalledPackageKey(state.Manifest) == packageKey
                            ? "This matched four-wheel package is already installed."
                            : "Ready to install all four " + PackageName(packageKey) + " wheels. Catalog values are illustrative, not calibrated performance claims."
            };
        }

        public static bool TryInstallWheelPackage(PrototypeGarageState state, string packageKey, out string message)
        {
            var proposal = PreviewWheelPackage(state, packageKey);
            message = proposal.Summary;
            if (!proposal.CanInstall) return false;
            if (InstalledPackageKey(state.Manifest) == packageKey) return true;
            foreach (var location in WheelLocations)
            {
                var wheel = state.Manifest.FindPart(InstanceId("wheel-" + location));
                SetWheelDefinition(wheel, packageKey);
            }

            state.Manifest.Revision++;
            message = "Installed four " + PackageName(packageKey) + " wheels. Drive to compare the authored configuration.";
            return true;
        }

        public static bool TryInstallCompanionKit(PrototypeGarageState state, out string message)
        {
            var issues = Validate(state);
            if (issues.Count > 0)
            {
                message = "Invalid saved configuration: " + issues[0];
                return false;
            }

            if (state.Manifest.FindPart(KitInstanceId) != null)
            {
                message = "The touring companion hardware kit is already installed.";
                return true;
            }

            if (state.Manifest.Revision == int.MaxValue)
            {
                message = "Assembly revision limit reached. The saved configuration can still be loaded and driven, but cannot accept further changes.";
                return false;
            }

            var reference = CreateManifest(InstalledPackageKey(state.Manifest), true);
            var parts = new List<InstalledPartInstance>(state.Manifest.Parts) { reference.FindPart(KitInstanceId) };
            var relationships = new List<AssemblyRelationship>(state.Manifest.Relationships)
            {
                reference.Relationships[reference.Relationships.Length - 1]
            };
            state.Manifest.Parts = parts.ToArray();
            state.Manifest.Relationships = relationships.ToArray();
            state.Manifest.Revision++;
            message = "Touring companion hardware installed (+2 kg). The matched touring wheel package is now available.";
            return true;
        }

        private static VehicleAssemblyManifest CreateManifest(string packageKey, bool hasKit)
        {
            var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();
            manifest.ManifestId = StableId.FromCatalogKey("motorbound.vehicle-manifest", "kiyora-aven-playable-garage-v1");
            var runningGear = manifest.Parts[3];
            runningGear.PartDefinitionId = PartId("running-gear-interior-fluids-excluding-wheels");
            runningGear.DisplayName = "Aven running gear, interior and fluids (excludes four wheels/tires)";
            runningGear.MassKilograms = 238d;
            runningGear.CostMinorUnits = 730000L;
            var parts = new List<InstalledPartInstance>(manifest.Parts);
            var relationships = new List<AssemblyRelationship>(manifest.Relationships);
            for (var i = 0; i < WheelLocations.Length; i++)
            {
                var location = WheelLocations[i];
                var part = new InstalledPartInstance
                {
                    InstanceId = InstanceId("wheel-" + location), InstallState = AssemblyInstallState.Released,
                    MassAccountingGroupId = StableId.FromCatalogKey("motorbound.mass-accounting-group", "garage-wheel-" + location),
                    LocalPositionXMeters = (i % 2 == 0 ? -1d : 1d) * (i < 2 ? 0.73d : 0.725d),
                    LocalPositionYMeters = -0.34d,
                    LocalPositionZMeters = i < 2 ? 1.175d : -1.175d
                };
                SetWheelDefinition(part, packageKey);
                parts.Add(part);
                relationships.Add(Relationship("wheel-" + location, part.InstanceId, runningGear.InstanceId, "KIY-AVEN-HUB-4X100-V1", location));
            }

            if (hasKit)
            {
                parts.Add(new InstalledPartInstance
                {
                    InstanceId = KitInstanceId, PartDefinitionId = KitDefinitionId,
                    DisplayName = "Aven touring companion hardware kit", InstallState = AssemblyInstallState.Released,
                    MassKilograms = 2d, CostMinorUnits = 12000L,
                    MassAccountingGroupId = StableId.FromCatalogKey("motorbound.mass-accounting-group", "garage-touring-kit")
                });
                relationships.Add(Relationship("touring-companion-kit", KitInstanceId, runningGear.InstanceId, "KIY-AVEN-TOURING-HARDWARE-V1", "touring-hardware"));
            }

            manifest.Parts = parts.ToArray();
            manifest.Relationships = relationships.ToArray();
            return manifest;
        }

        private static void SetWheelDefinition(InstalledPartInstance wheel, string packageKey)
        {
            wheel.PartDefinitionId = WheelDefinitionId(packageKey);
            wheel.PartDefinitionRevision = 1;
            wheel.DisplayName = PackageName(packageKey) + " wheel/tire";
            wheel.MassKilograms = packageKey == TouringKey ? 20d : 18d;
            wheel.CostMinorUnits = packageKey == TouringKey ? 45000L : 30000L;
        }

        private static void ValidatePart(InstalledPartInstance actual, InstalledPartInstance expected, ICollection<ValidationIssue> issues)
        {
            var path = "assemblyManifest.parts." + expected.InstanceId;
            Require(actual.PartDefinitionId == expected.PartDefinitionId && actual.PartDefinitionRevision == expected.PartDefinitionRevision,
                path + ".definition", "Part identity/revision is not approved for this slot and matched wheel set.", issues);
            Require(actual.DisplayName == expected.DisplayName && actual.InstallState == expected.InstallState,
                path + ".installState", "Only the named released catalog part is supported.", issues);
            Require(Equal(actual.MassKilograms, expected.MassKilograms) && actual.OwnsMass == expected.OwnsMass
                && actual.MassAccountingGroupId == expected.MassAccountingGroupId && actual.CostMinorUnits == expected.CostMinorUnits
                && actual.OwnsCost == expected.OwnsCost, path + ".accounting", "Saved mass/cost ownership must match the catalog; edited physics authority is not accepted.", issues);
            Require(Equal(actual.LocalPositionXMeters, expected.LocalPositionXMeters) && Equal(actual.LocalPositionYMeters, expected.LocalPositionYMeters)
                && Equal(actual.LocalPositionZMeters, expected.LocalPositionZMeters) && Equal(actual.LocalRotationXRadians, expected.LocalRotationXRadians)
                && Equal(actual.LocalRotationYRadians, expected.LocalRotationYRadians) && Equal(actual.LocalRotationZRadians, expected.LocalRotationZRadians),
                path + ".pose", "Installed pose must be finite and match the approved reference hardpoint.", issues);
            Require(Equal(actual.Condition01, expected.Condition01), path + ".condition", "This milestone supports only the undamaged reference condition.", issues);
            Require(EqualIds(actual.IncludedPartDefinitionIds, expected.IncludedPartDefinitionIds), path + ".includedParts", "Included-components manifest does not match the approved assembly.", issues);
        }

        private static string InstalledPackageKey(VehicleAssemblyManifest manifest)
        {
            var wheel = manifest.FindPart(InstanceId("wheel-front-left"));
            if (wheel == null) return null;
            if (wheel.PartDefinitionId == WheelDefinitionId(StockRoadKey)) return StockRoadKey;
            if (wheel.PartDefinitionId == WheelDefinitionId(TouringKey)) return TouringKey;
            return null;
        }

        private static PrototypeGarageProposal Rejected(string message)
        {
            return new PrototypeGarageProposal { CanInstall = false, Summary = message, Plan = new BuildPlanEvaluation() };
        }

        private static AssemblyRelationship Relationship(string key, StableId from, StableId to, string family, string port)
        {
            return new AssemblyRelationship
            {
                RelationshipId = StableId.FromCatalogKey("motorbound.assembly-relationship.prototype", "garage-" + key),
                Kind = AssemblyRelationshipKind.MechanicalAttachment, FromPartInstanceId = from, ToPartInstanceId = to,
                FromPortId = "mount", ToPortId = port, InterfaceFamilyAndVersion = family, IsSatisfied = true
            };
        }

        private static double SettledRootHeight(VehicleDefinition vehicle)
        {
            return vehicle.Tire.UnloadedRadiusMeters + vehicle.Suspension.RestLengthMeters
                - vehicle.MassKilograms * 9.80665d / (4d * vehicle.Suspension.SpringRateNewtonsPerMeter);
        }

        private static double AdjustAngle(double degrees, double clearanceRatio)
        {
            return Math.Atan(Math.Tan(degrees * Math.PI / 180d) * clearanceRatio) * 180d / Math.PI;
        }

        private static bool Equal(double actual, double expected)
        {
            return !double.IsNaN(actual) && !double.IsInfinity(actual) && Math.Abs(actual - expected) <= 0.00000001d;
        }

        private static bool EqualIds(StableId[] actual, StableId[] expected)
        {
            if (actual == null || actual.Length != expected.Length) return false;
            for (var index = 0; index < actual.Length; index++) if (actual[index] != expected[index]) return false;
            return true;
        }

        private static string PackageName(string key) => key == TouringKey ? "Touring 225" : key == IncompatibleKey ? "Oversize five-lug" : "Stock road 205";
        private static StableId WheelDefinitionId(string key) => PartId("wheel-tire-" + key);
        private static StableId PartId(string key) => StableId.FromCatalogKey("motorbound.part", "kiyora-aven-garage-" + key);
        private static StableId InstanceId(string key) => StableId.FromCatalogKey("motorbound.part-instance.prototype", "kiyora-aven-garage-" + key);
        private static StableId StepId(string key) => StableId.FromCatalogKey("motorbound.build-plan-step", "kiyora-aven-garage-" + key);

        private static void Require(bool condition, string path, string message, ICollection<ValidationIssue> issues)
        {
            if (!condition) issues.Add(new ValidationIssue(path, message));
        }
    }
}
