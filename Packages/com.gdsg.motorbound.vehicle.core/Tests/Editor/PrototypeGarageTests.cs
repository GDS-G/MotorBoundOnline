using System;
using System.Linq;
using MotorBound.Foundation;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class PrototypeGarageTests
    {
        [Test]
        public void NewGarage_SplitsFourWheelInstancesWithoutDoubleCounting()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var configuration = PrototypeGarageCatalog.Compile(state);

            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
            Assert.That(state.Manifest.Parts.Length, Is.EqualTo(8));
            Assert.That(state.Manifest.Relationships.Length, Is.EqualTo(8));
            Assert.That(configuration.Vehicle.MassKilograms, Is.EqualTo(1120d));
            Assert.That(state.Manifest.ComputeAuthoritativeCostMinorUnits(),
                Is.EqualTo(ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest().ComputeAuthoritativeCostMinorUnits()));
            Assert.That(configuration.Envelope.GrossMassKilograms, Is.EqualTo(1120d));
            Assert.That(configuration.Envelope.GroundClearanceMeters, Is.EqualTo(0.13d).Within(1e-10d));
            Assert.That(configuration.Vehicle.Validate(), Is.Empty);
            Assert.That(configuration.Envelope.Validate(), Is.Empty);
            Assert.That(state.Manifest.Parts.All(part => part.InstanceId != part.PartDefinitionId), Is.True);
        }

        [Test]
        public void MissingHardwarePreviewAndRejectedInstall_NeverMutateInstalledAssembly()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var parts = state.Manifest.Parts;
            var relations = state.Manifest.Relationships;
            var ids = parts.Select(part => part.PartDefinitionId).ToArray();
            var proposal = PrototypeGarageCatalog.PreviewWheelPackage(state, PrototypeGarageCatalog.TouringKey);

            Assert.That(proposal.CanInstall, Is.False);
            Assert.That(proposal.Summary, Does.Contain("companion"));
            Assert.That(proposal.Plan.Issues.Any(issue => issue.Kind == BuildPlanIssueKind.MissingDependency), Is.True);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _), Is.False);
            Assert.That(state.Manifest.Revision, Is.EqualTo(1));
            Assert.That(state.Manifest.Parts, Is.SameAs(parts));
            Assert.That(state.Manifest.Relationships, Is.SameAs(relations));
            Assert.That(state.Manifest.Parts.Select(part => part.PartDefinitionId), Is.EqualTo(ids));
            Assert.That(state.Manifest.ComputeAuthoritativeMassKilograms(), Is.EqualTo(1120d));
            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
        }

        [Test]
        public void HardwareThenTouring_UpdatesApprovedMassTiresAndEnvelopeWhileRetainingInstances()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var stock = PrototypeGarageCatalog.Compile(state);
            var wheelIds = state.Manifest.Parts.Skip(4).Select(part => part.InstanceId).ToArray();
            var wheels = state.Manifest.Parts.Skip(4).ToArray();

            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out _), Is.True);
            Assert.That(state.Manifest.ComputeAuthoritativeMassKilograms(), Is.EqualTo(1122d));
            var preview = PrototypeGarageCatalog.PreviewWheelPackage(state, PrototypeGarageCatalog.TouringKey);
            Assert.That(preview.CanInstall, Is.True);
            Assert.That(preview.Plan.IsValid, Is.True);
            Assert.That(preview.Plan.BillOfMaterials.Single(line => line.Quantity == 4).PartDefinitionRevision, Is.EqualTo(1));
            Assert.That(state.Manifest.Revision, Is.EqualTo(2));
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _), Is.True);
            var touring = PrototypeGarageCatalog.Compile(state);

            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
            Assert.That(state.Manifest.Revision, Is.EqualTo(3));
            Assert.That(touring.Vehicle.MassKilograms, Is.EqualTo(1130d));
            Assert.That(touring.Vehicle.Tire.UnloadedRadiusMeters, Is.EqualTo(0.315d));
            Assert.That(touring.Vehicle.Tire.SectionWidthMeters, Is.EqualTo(0.225d));
            Assert.That(touring.Vehicle.Tire.TreadWaterEvacuationFactor, Is.EqualTo(0.78d));
            Assert.That(touring.Vehicle.Tire.PeakDryFrictionCoefficient, Is.EqualTo(stock.Vehicle.Tire.PeakDryFrictionCoefficient));
            Assert.That(touring.Vehicle.Tire.RotationalInertiaKilogramMetersSquared,
                Is.EqualTo(1.15d * (20d / 18d) * Math.Pow(0.315d / 0.305d, 2d)).Within(1e-10d));
            var delta = 0.01d - 10d * 9.80665d / (4d * 34000d);
            Assert.That(touring.Envelope.GroundClearanceMeters, Is.EqualTo(0.13d + delta).Within(1e-10d));
            Assert.That(touring.Envelope.OverallHeightMeters, Is.EqualTo(1.24d + delta).Within(1e-10d));
            Assert.That(touring.Envelope.GrossMassKilograms, Is.EqualTo(touring.Vehicle.MassKilograms));
            Assert.That(touring.Envelope.SourceManifestRevision, Is.EqualTo(3));
            Assert.That(touring.HasCompanionKit, Is.True);
            Assert.That(touring.WheelPackageKey, Is.EqualTo(PrototypeGarageCatalog.TouringKey));
            for (var index = 0; index < wheelIds.Length; index++)
            {
                Assert.That(state.Manifest.FindPart(wheelIds[index]), Is.SameAs(wheels[index]));
            }
        }

        [Test]
        public void ReinstallAndReturnToStock_AvoidDuplicateHardwareAndKeepExistingKitMass()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            PrototypeGarageCatalog.TryInstallCompanionKit(state, out _);
            PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _);
            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out _), Is.True);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _), Is.True);
            Assert.That(state.Manifest.Revision, Is.EqualTo(3));
            Assert.That(state.Manifest.Parts.Length, Is.EqualTo(9));
            PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.StockRoadKey, out _);
            var configuration = PrototypeGarageCatalog.Compile(state);
            Assert.That(configuration.Vehicle.MassKilograms, Is.EqualTo(1122d));
            Assert.That(configuration.HasCompanionKit, Is.True);
            Assert.That(configuration.Vehicle.Tire.UnloadedRadiusMeters, Is.EqualTo(0.305d));
            Assert.That(state.Manifest.Revision, Is.EqualTo(4));
            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
        }

        [Test]
        public void IncompatibleAndUnknownPackages_ExplainRejectionWithoutMutating()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var rejected = PrototypeGarageCatalog.PreviewWheelPackage(state, PrototypeGarageCatalog.IncompatibleKey);
            Assert.That(rejected.CanInstall, Is.False);
            Assert.That(rejected.Fitment.Find(FitmentStage.Mount).Passed, Is.False);
            Assert.That(rejected.Fitment.Find(FitmentStage.StaticGeometry).Passed, Is.False);
            Assert.That(rejected.Summary, Does.Contain("4x100"));
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.IncompatibleKey, out _), Is.False);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, "unknown", out _), Is.False);
            Assert.That(state.Manifest.Revision, Is.EqualTo(1));
            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
        }

        [Test]
        public void CompilationAndOptionDescriptors_DoNotExposeMutableCatalogAuthority()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var compiled = PrototypeGarageCatalog.Compile(state);
            compiled.Vehicle.MassKilograms = 10d;
            compiled.Vehicle.Tire.UnloadedRadiusMeters = 10d;
            compiled.Envelope.GroundClearanceMeters = 10d;
            PrototypeGarageCatalog.Options[0].Key = "altered";
            var next = PrototypeGarageCatalog.Compile(state);
            Assert.That(next.Vehicle.MassKilograms, Is.EqualTo(1120d));
            Assert.That(next.Vehicle.Tire.UnloadedRadiusMeters, Is.EqualTo(0.305d));
            Assert.That(next.Envelope.GroundClearanceMeters, Is.EqualTo(0.13d).Within(1e-10d));
            Assert.That(PrototypeGarageCatalog.Options[0].Key, Is.EqualTo(PrototypeGarageCatalog.StockRoadKey));
            Assert.That(state.Manifest.Revision, Is.EqualTo(1));
        }

        [TestCase("schema")]
        [TestCase("manifest-id")]
        [TestCase("vehicle-id")]
        [TestCase("invalid-revision")]
        [TestCase("unknown-part")]
        [TestCase("definition-revision")]
        [TestCase("duplicate-wheel")]
        [TestCase("missing-wheel")]
        [TestCase("mass")]
        [TestCase("mass-owner")]
        [TestCase("mass-group")]
        [TestCase("pose")]
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("condition")]
        [TestCase("included-parts")]
        [TestCase("unreleased")]
        [TestCase("missing-relationships")]
        [TestCase("unsatisfied-relationship")]
        [TestCase("wrong-relationship-endpoint")]
        [TestCase("duplicate-relationship")]
        public void InvalidSavedAssembly_IsRejectedBeforeCompilationOrMutation(string alteration)
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            var unknown = StableId.FromCatalogKey("tests", "unknown");
            switch (alteration)
            {
                case "schema": state.SchemaVersion = 2; break;
                case "manifest-id": state.Manifest.ManifestId = unknown; break;
                case "vehicle-id": state.Manifest.VehicleDefinitionId = unknown; break;
                case "invalid-revision": state.Manifest.Revision = int.MinValue; break;
                case "unknown-part": state.Manifest.Parts[4].PartDefinitionId = unknown; break;
                case "definition-revision": state.Manifest.Parts[4].PartDefinitionRevision = 2; break;
                case "duplicate-wheel": state.Manifest.Parts[5] = state.Manifest.Parts[4]; break;
                case "missing-wheel": state.Manifest.Parts = state.Manifest.Parts.Take(7).ToArray(); break;
                case "mass": state.Manifest.Parts[0].MassKilograms = 10d; break;
                case "mass-owner": state.Manifest.Parts[0].OwnsMass = false; break;
                case "mass-group": state.Manifest.Parts[0].MassAccountingGroupId = unknown; break;
                case "pose": state.Manifest.Parts[4].LocalPositionXMeters += 0.1d; break;
                case "nan": state.Manifest.Parts[0].LocalPositionYMeters = double.NaN; break;
                case "infinity": state.Manifest.Parts[0].MassKilograms = double.PositiveInfinity; break;
                case "condition": state.Manifest.Parts[0].Condition01 = 0.5d; break;
                case "included-parts": state.Manifest.Parts[0].IncludedPartDefinitionIds = new[] { unknown }; break;
                case "unreleased": state.Manifest.Parts[4].InstallState = AssemblyInstallState.Fitted; break;
                case "missing-relationships": state.Manifest.Relationships = null; break;
                case "unsatisfied-relationship": state.Manifest.Relationships[0].IsSatisfied = false; break;
                case "wrong-relationship-endpoint": state.Manifest.Relationships[0].FromPartInstanceId = state.Manifest.Parts[3].InstanceId; break;
                case "duplicate-relationship": state.Manifest.Relationships[1] = state.Manifest.Relationships[0]; break;
            }

            var revision = state.Manifest.Revision;
            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Not.Empty);
            Assert.Throws<ArgumentException>(() => PrototypeGarageCatalog.Compile(state));
            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out _), Is.False);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.StockRoadKey, out _), Is.False);
            Assert.That(state.Manifest.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void MixedWheelPackagesOrMissingRequiredKitOrDuplicateKit_AreRejected()
        {
            var touring = PrototypeGarageCatalog.CreateNewState();
            PrototypeGarageCatalog.TryInstallCompanionKit(touring, out _);
            PrototypeGarageCatalog.TryInstallWheelPackage(touring, PrototypeGarageCatalog.TouringKey, out _);
            var stock = PrototypeGarageCatalog.CreateNewState();
            stock.Manifest.Parts[4] = touring.Manifest.Parts[4];
            Assert.That(PrototypeGarageCatalog.Validate(stock), Is.Not.Empty);

            var kit = touring.Manifest.Parts.Last();
            touring.Manifest.Parts = touring.Manifest.Parts.Concat(new[] { kit }).ToArray();
            Assert.That(PrototypeGarageCatalog.Validate(touring), Is.Not.Empty);
            touring.Manifest.Parts = touring.Manifest.Parts.Take(8).ToArray();
            Assert.That(PrototypeGarageCatalog.Validate(touring).Any(issue => issue.Path.Contains("companionKit")), Is.True);
        }

        [Test]
        public void NullStateOrManifest_FailsWithActionableValidation()
        {
            Assert.That(PrototypeGarageCatalog.Validate(null), Is.Not.Empty);
            Assert.That(PrototypeGarageCatalog.Validate(new PrototypeGarageState()), Is.Not.Empty);
            Assert.That(PrototypeGarageCatalog.PreviewWheelPackage(null, PrototypeGarageCatalog.StockRoadKey).CanInstall, Is.False);
            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(null, out _), Is.False);
        }

        [Test]
        public void OmittedSchemaDefault_IsInvalidEvenWhenManifestIsComplete()
        {
            var state = new PrototypeGarageState { Manifest = PrototypeGarageCatalog.CreateNewState().Manifest };
            Assert.That(state.SchemaVersion, Is.EqualTo(0));
            Assert.That(PrototypeGarageCatalog.Validate(state).Any(issue => issue.Path == "garage.schemaVersion"), Is.True);
            Assert.Throws<ArgumentException>(() => PrototypeGarageCatalog.Compile(state));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LastAvailableRevision_RemainsValidAndPreventsFurtherMutation(bool installWheels)
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            if (installWheels) PrototypeGarageCatalog.TryInstallCompanionKit(state, out _);
            state.Manifest.Revision = int.MaxValue - 1;

            var installed = installWheels
                ? PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _)
                : PrototypeGarageCatalog.TryInstallCompanionKit(state, out _);
            Assert.That(installed, Is.True);
            Assert.That(state.Manifest.Revision, Is.EqualTo(int.MaxValue));
            Assert.That(PrototypeGarageCatalog.Validate(state), Is.Empty);
            var configuration = PrototypeGarageCatalog.Compile(state);
            Assert.That(configuration.Envelope.SourceManifestRevision, Is.EqualTo(int.MaxValue));
            var parts = state.Manifest.Parts;
            var definitions = parts.Select(part => part.PartDefinitionId).ToArray();
            var differentPackage = installWheels ? PrototypeGarageCatalog.StockRoadKey : PrototypeGarageCatalog.TouringKey;

            Assert.That(PrototypeGarageCatalog.PreviewWheelPackage(state, differentPackage).CanInstall, Is.False);
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, differentPackage, out var rejection), Is.False);
            Assert.That(rejection, Does.Contain("revision limit"));
            Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, configuration.WheelPackageKey, out _), Is.True);
            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out _), Is.True);
            Assert.That(state.Manifest.Parts, Is.SameAs(parts));
            Assert.That(parts.Select(part => part.PartDefinitionId), Is.EqualTo(definitions));
            Assert.That(state.Manifest.Revision, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ExhaustedRevision_RejectsNewHardwareWithoutMutation()
        {
            var state = PrototypeGarageCatalog.CreateNewState();
            state.Manifest.Revision = int.MaxValue;
            var parts = state.Manifest.Parts;
            var relationships = state.Manifest.Relationships;

            Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out var message), Is.False);
            Assert.That(message, Does.Contain("revision limit"));
            Assert.That(state.Manifest.Parts, Is.SameAs(parts));
            Assert.That(state.Manifest.Relationships, Is.SameAs(relationships));
            Assert.That(PrototypeGarageCatalog.Compile(state).Vehicle.MassKilograms, Is.EqualTo(1120d));
            Assert.That(state.Manifest.Revision, Is.EqualTo(int.MaxValue));
        }
    }
}
