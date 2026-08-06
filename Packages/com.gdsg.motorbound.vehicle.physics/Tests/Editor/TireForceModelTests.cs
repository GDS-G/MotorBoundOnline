using System;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class TireForceModelTests
    {
        [Test]
        public void CombinedForce_NeverExceedsFrictionEnvelope()
        {
            var result = TireForceModel.Evaluate(CreateInput(1.5d, 0.5d, 1d));
            var magnitude = Math.Sqrt(
                (result.LongitudinalForceNewtons * result.LongitudinalForceNewtons)
                + (result.LateralForceNewtons * result.LateralForceNewtons));

            Assert.That(magnitude, Is.LessThanOrEqualTo(result.MaximumCombinedForceNewtons + 0.000001d));
        }

        [Test]
        public void WetSurface_ReducesPeakForce()
        {
            var dry = TireForceModel.Evaluate(CreateInput(1d, 0d, 1d));
            var wet = TireForceModel.Evaluate(CreateInput(1d, 0d, 0.58d));

            Assert.That(wet.MaximumCombinedForceNewtons, Is.LessThan(dry.MaximumCombinedForceNewtons));
            Assert.That(wet.LongitudinalForceNewtons, Is.LessThan(dry.LongitudinalForceNewtons));
        }

        [Test]
        public void ZeroNormalLoad_ProducesNoForce()
        {
            var input = CreateInput(1d, 0.2d, 1d);
            input.NormalLoadNewtons = 0d;

            var result = TireForceModel.Evaluate(input);

            Assert.That(result.LongitudinalForceNewtons, Is.Zero);
            Assert.That(result.LateralForceNewtons, Is.Zero);
        }

        [Test]
        public void LateralForce_OpposesSlipAngle()
        {
            var positiveSlip = TireForceModel.Evaluate(CreateInput(0d, 0.1d, 1d));
            var negativeSlip = TireForceModel.Evaluate(CreateInput(0d, -0.1d, 1d));

            Assert.That(positiveSlip.LateralForceNewtons, Is.LessThan(0d));
            Assert.That(negativeSlip.LateralForceNewtons, Is.GreaterThan(0d));
        }

        private static TireForceInput CreateInput(double slipRatio, double slipAngleRadians, double surfaceGrip)
        {
            return new TireForceInput
            {
                NormalLoadNewtons = 3200d,
                SlipRatio = slipRatio,
                SlipAngleRadians = slipAngleRadians,
                PeakDryFrictionCoefficient = 1.08d,
                SurfaceGripMultiplier = surfaceGrip,
                LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
                CorneringStiffnessNewtonPerRadian = 65000d,
                ReferenceLoadNewtons = 3300d,
                LoadSensitivityExponent = -0.08d
            };
        }
    }
}
