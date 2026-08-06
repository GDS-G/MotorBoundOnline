using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class FitmentValidatorTests
    {
        [Test]
        public void ReferenceWheel_IsBoltInAcrossAllTenStages()
        {
            var result = FitmentValidator.Evaluate(
                ReferenceVehicleCatalog.CreateKiyoraAvenClubFitmentContext(),
                ReferenceVehicleCatalog.CreateKiyoraAvenReferenceFrontWheelFitment());

            Assert.That(result.Classification, Is.EqualTo(FitmentClassification.BoltIn));
            Assert.That(result.IsInstallable, Is.True);
            Assert.That(result.Checks, Has.Length.EqualTo(10));
            Assert.That(result.Find(FitmentStage.DynamicSweep).Passed, Is.True);
        }

        [Test]
        public void KnownPatternAdapter_IsReportedAsAdapterInstallation()
        {
            var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenClubFitmentContext();
            var component = ReferenceVehicleCatalog.CreateKiyoraAvenReferenceFrontWheelFitment();
            component.RequiredMountPatternCode = "AFTERMARKET-CENTERLOCK-V1";
            component.AdapterCompatibleHostPatternCodes = new[] { "KIY-AVEN-HUB-4X100-V1" };

            var result = FitmentValidator.Evaluate(vehicle, component);

            Assert.That(result.Classification, Is.EqualTo(FitmentClassification.AdapterInstallation));
            Assert.That(result.Find(FitmentStage.Mount).Passed, Is.True);
            Assert.That(result.Find(FitmentStage.Mount).Remedy, Does.Contain("adapter"));
        }

        [Test]
        public void ExcessHardpointLoad_IsIncompatibleWithExactCapacityConflict()
        {
            var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenClubFitmentContext();
            var component = ReferenceVehicleCatalog.CreateKiyoraAvenReferenceFrontWheelFitment();
            component.AppliedLoadNewtons = 22000d;

            var result = FitmentValidator.Evaluate(vehicle, component);

            Assert.That(result.Classification, Is.EqualTo(FitmentClassification.Incompatible));
            Assert.That(result.Find(FitmentStage.Capacity).Passed, Is.False);
            Assert.That(result.Find(FitmentStage.Capacity).Conflict, Does.Contain("22000"));
            Assert.That(result.Find(FitmentStage.Capacity).Conflict, Does.Contain("18000"));
        }
    }
}
