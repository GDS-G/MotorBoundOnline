using System.Linq;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class VehicleDefinitionTests
    {
        [Test]
        public void ReferenceKiyoraAven_IsValid()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();

            Assert.That(definition.Validate(), Is.Empty);
            Assert.That(definition.DriveLayout, Is.EqualTo(DriveLayout.RearWheelDrive));
        }

        [Test]
        public void TorqueCurve_InterpolatesBetweenSamples()
        {
            var engine = new EngineDefinition
            {
                FullLoadTorqueCurve = new[]
                {
                    new TorqueSample(1000d, 100d),
                    new TorqueSample(3000d, 200d)
                }
            };

            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(2000d), Is.EqualTo(150d).Within(0.000001d));
            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(500d), Is.EqualTo(100d));
            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(4000d), Is.EqualTo(200d));
        }

        [TestCase(0d)]
        [TestCase(1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Validation_RejectsInvalidReverseRatio(double ratio)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Transmission.ReverseGearRatio = ratio;
            Assert.That(definition.Validate().Any(issue => issue.Path == "transmission.reverseGearRatio"), Is.True);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Validation_RejectsInvalidForwardRatio(double ratio)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Transmission.ForwardGearRatios[1] = ratio;
            Assert.That(definition.Validate().Any(issue => issue.Path == "transmission.forwardGearRatios[1]"), Is.True);
        }

        [Test]
        public void Validation_ReportsNonIncreasingTorqueSamples()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Engine.FullLoadTorqueCurve[2] = new TorqueSample(1500d, 190d);

            var issues = definition.Validate();

            Assert.That(issues.Any(issue => issue.Path == "engine.fullLoadTorqueCurve[2]"), Is.True);
        }

        [TestCase(0d)]
        [TestCase(-0.1d)]
        [TestCase(1.0001d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Validation_RejectsInvalidSlidingGripRatio(double slidingGripRatio)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Tire.SlidingGripRatio = slidingGripRatio;

            Assert.That(definition.Validate().Any(issue => issue.Path == "tire.slidingGripRatio"), Is.True);
        }

        [TestCase(0.01d)]
        [TestCase(0.78d)]
        [TestCase(1d)]
        public void Validation_AcceptsSlidingGripRatioWithinRange(double slidingGripRatio)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Tire.SlidingGripRatio = slidingGripRatio;

            Assert.That(definition.Validate(), Is.Empty);
        }

        [Test]
        public void ReferenceClubAxle_HasExplicitPassiveLimitedSlipHardware()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            Assert.That(definition.FrontDifferential.Type, Is.EqualTo(DifferentialType.Open));
            Assert.That(definition.RearDifferential.Type, Is.EqualTo(DifferentialType.ClutchLimitedSlip));
            Assert.That(definition.RearDifferential.PreloadTorqueNewtonMeters, Is.GreaterThan(0d));
            Assert.That(definition.RearDifferential.CoastLockFraction, Is.LessThan(definition.RearDifferential.PowerLockFraction));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Validation_RejectsInvalidDifferentialResistance(double value)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.RearDifferential.PreloadTorqueNewtonMeters = value;
            definition.FrontDifferential.SlipSpeedGainNewtonMeterSecondsPerRadian = value;
            var issues = definition.Validate();
            Assert.That(issues.Any(issue => issue.Path == "rearDifferential.preloadTorqueNewtonMeters"), Is.True);
            Assert.That(issues.Any(issue => issue.Path == "frontDifferential.slipSpeedGainNewtonMeterSecondsPerRadian"), Is.True);
        }

        [TestCase(-0.1d)]
        [TestCase(1.01d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Validation_RejectsInvalidDifferentialLockFraction(double value)
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.RearDifferential.PowerLockFraction = value;
            definition.RearDifferential.CoastLockFraction = value;
            Assert.That(definition.Validate().Count(issue => issue.Path.Contains("LockFraction")), Is.EqualTo(2));
        }

        [Test]
        public void Validation_RejectsMissingOrUnknownAxleDifferential()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.FrontDifferential = null;
            definition.RearDifferential.Type = (DifferentialType)99;
            var issues = definition.Validate();
            Assert.That(issues.Any(issue => issue.Path == "frontDifferential"), Is.True);
            Assert.That(issues.Any(issue => issue.Path == "rearDifferential.type"), Is.True);
        }
    }
}
