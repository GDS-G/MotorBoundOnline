using System.Linq;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class DimensionalCompatibilityTests
    {
        [Test]
        public void ReferenceKiyoraAven_FitsVerifiedPassengerGarage()
        {
            var result = DimensionalCompatibilityEvaluator.Evaluate(
                ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope(),
                ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile());

            Assert.That(result.Status, Is.EqualTo(DimensionalCompatibilityStatus.Compatible));
            Assert.That(result.Conflicts, Is.Empty);
            Assert.That(result.EffectiveClearHeightMeters, Is.EqualTo(2d).Within(0.000001d));
        }

        [Test]
        public void RoofAccessory_RestrictsLowGarageWithMeasuredHeightConflict()
        {
            var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope();
            vehicle.OverallHeightMeters = 2.04d;

            var result = DimensionalCompatibilityEvaluator.Evaluate(
                vehicle,
                ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile());

            Assert.That(result.Status, Is.EqualTo(DimensionalCompatibilityStatus.Restricted));
            var conflict = result.Conflicts.Single(value => value.Kind == DimensionalConflictKind.Height);
            Assert.That(conflict.RequiredValue, Is.EqualTo(2.04d));
            Assert.That(conflict.AvailableValue, Is.EqualTo(2d).Within(0.000001d));
        }

        [Test]
        public void PostedClearanceAboveSafePhysicalHeight_IsUnverified()
        {
            var profile = ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile();
            profile.PostedClearHeightMeters = 2.08d;

            var result = DimensionalCompatibilityEvaluator.Evaluate(
                ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope(),
                profile);

            Assert.That(result.Status, Is.EqualTo(DimensionalCompatibilityStatus.Unverified));
            Assert.That(result.Conflicts.Any(value => value.Message.Contains("Posted clearance exceeds")), Is.True);
        }

        [Test]
        public void TrailerCombination_IsRejectedByClassRuleAndLength()
        {
            var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope();
            vehicle.DesignClass = VehicleDesignClass.PassengerVehicleWithTrailer;
            vehicle.HasTrailer = true;
            vehicle.CombinationLengthMeters = 8.2d;

            var result = DimensionalCompatibilityEvaluator.Evaluate(
                vehicle,
                ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile());

            Assert.That(result.Status, Is.EqualTo(DimensionalCompatibilityStatus.Restricted));
            Assert.That(result.Conflicts.Any(value => value.Kind == DimensionalConflictKind.DesignClass), Is.True);
            Assert.That(result.Conflicts.Any(value => value.Kind == DimensionalConflictKind.Trailer), Is.True);
            Assert.That(result.Conflicts.Any(value => value.Kind == DimensionalConflictKind.CombinationLength), Is.True);
        }
    }
}
