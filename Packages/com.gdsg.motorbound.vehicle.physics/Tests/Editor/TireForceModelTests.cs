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

        [Test]
        public void CombinedSlip_PreservesDemandDirectionInsteadOfSaturatingAxesIndependently()
        {
            var input = CreateInput(0.25d, 0.2d, 1d);
            var result = TireForceModel.Evaluate(input);
            var demandedLongitudinal = input.LongitudinalSlipStiffnessNewtonPerRatio * input.SlipRatio;
            var demandedLateral = -input.CorneringStiffnessNewtonPerRadian * input.SlipAngleRadians;

            Assert.That(result.LongitudinalForceNewtons, Is.GreaterThan(0d));
            Assert.That(result.LateralForceNewtons, Is.LessThan(0d));
            Assert.That(
                result.LongitudinalForceNewtons / result.LateralForceNewtons,
                Is.EqualTo(demandedLongitudinal / demandedLateral).Within(0.000001d));
        }

        [Test]
        public void LockedOrSpinningWheel_LosesMostLateralGrip()
        {
            var rolling = TireForceModel.Evaluate(CreateInput(0d, 0.1d, 1d));
            var locked = TireForceModel.Evaluate(CreateInput(-1d, 0.1d, 1d));
            var spinning = TireForceModel.Evaluate(CreateInput(1d, 0.1d, 1d));

            Assert.That(Math.Abs(locked.LateralForceNewtons), Is.LessThan(Math.Abs(rolling.LateralForceNewtons) * 0.2d));
            Assert.That(Math.Abs(spinning.LateralForceNewtons), Is.LessThan(Math.Abs(rolling.LateralForceNewtons) * 0.2d));
            Assert.That(locked.LongitudinalForceNewtons, Is.LessThan(0d));
            Assert.That(spinning.LongitudinalForceNewtons, Is.GreaterThan(0d));
            Assert.That(locked.LateralForceNewtons, Is.EqualTo(spinning.LateralForceNewtons).Within(0.000001d));
        }

        [TestCase(0.65d)]
        [TestCase(0.78d)]
        public void Force_AfterPeakFallsSmoothlyTowardSlidingGrip(double slidingGripRatio)
        {
            var peakForce = 0d;
            var peakSlipRatio = 0d;
            var previousForce = 0d;
            for (var sample = 0; sample <= 800; sample++)
            {
                var slipRatio = sample / 200d;
                var input = CreateInput(slipRatio, 0d, 1d);
                input.SlidingGripRatio = slidingGripRatio;
                var result = TireForceModel.Evaluate(input);
                var force = result.LongitudinalForceNewtons;

                Assert.That(force, Is.GreaterThanOrEqualTo(0d));
                Assert.That(force, Is.LessThanOrEqualTo(result.MaximumCombinedForceNewtons + 0.000001d));
                Assert.That(Math.Abs(force - previousForce), Is.LessThan(result.MaximumCombinedForceNewtons * 0.12d));
                if (force > peakForce)
                {
                    peakForce = force;
                    peakSlipRatio = slipRatio;
                }
                else if (slipRatio > peakSlipRatio && peakSlipRatio > 0d)
                {
                    Assert.That(force, Is.LessThanOrEqualTo(previousForce + 0.000001d));
                }

                previousForce = force;
            }

            var saturatedInput = CreateInput(4d, 0d, 1d);
            saturatedInput.SlidingGripRatio = slidingGripRatio;
            var saturated = TireForceModel.Evaluate(saturatedInput);
            Assert.That(peakSlipRatio, Is.GreaterThan(0.005d).And.LessThan(1d));
            Assert.That(peakForce, Is.GreaterThan(saturated.MaximumCombinedForceNewtons * 0.999d));
            Assert.That(saturated.LongitudinalForceNewtons, Is.LessThan(peakForce * (slidingGripRatio + 0.025d)));
            Assert.That(saturated.LongitudinalForceNewtons, Is.GreaterThanOrEqualTo(peakForce * slidingGripRatio));
        }

        [TestCase(0.001d)]
        [TestCase(0.55d)]
        [TestCase(0.78d)]
        [TestCase(1d)]
        public void SlidingGripRatio_ControlsHighSlipForce(double slidingGripRatio)
        {
            var input = CreateInput(1d, 0d, 1d);
            input.SlidingGripRatio = slidingGripRatio;
            input.LongitudinalSlipStiffnessNewtonPerRatio = 10000000000d;
            var result = TireForceModel.Evaluate(input);

            Assert.That(result.LongitudinalForceNewtons / result.MaximumCombinedForceNewtons,
                Is.EqualTo(slidingGripRatio).Within(0.00001d));
        }

        [Test]
        public void OmittedSlidingGripRatio_UsesPrototypeDefault()
        {
            var omitted = CreateInput(1d, 0.1d, 1d);
            var explicitDefault = omitted;
            explicitDefault.SlidingGripRatio = 0.78d;

            var omittedResult = TireForceModel.Evaluate(omitted);
            var explicitResult = TireForceModel.Evaluate(explicitDefault);

            Assert.That(omittedResult.LongitudinalForceNewtons, Is.EqualTo(explicitResult.LongitudinalForceNewtons).Within(0.000001d));
            Assert.That(omittedResult.LateralForceNewtons, Is.EqualTo(explicitResult.LateralForceNewtons).Within(0.000001d));
        }

        [Test]
        public void SmallSlip_RetainsConfiguredStiffness()
        {
            var input = CreateInput(0.00001d, 0.00001d, 1d);
            var result = TireForceModel.Evaluate(input);

            Assert.That(result.LongitudinalForceNewtons / input.SlipRatio,
                Is.EqualTo(input.LongitudinalSlipStiffnessNewtonPerRatio).Within(input.LongitudinalSlipStiffnessNewtonPerRatio * 0.000001d));
            Assert.That(-result.LateralForceNewtons / input.SlipAngleRadians,
                Is.EqualTo(input.CorneringStiffnessNewtonPerRadian).Within(input.CorneringStiffnessNewtonPerRadian * 0.000001d));
        }

        [Test]
        public void CombinedSlipSweep_IsBoundedAndSymmetricAcrossLoadsAndSurfaces()
        {
            foreach (var load in new[] { 320d, 3300d, 8000d })
            {
                foreach (var grip in new[] { 0.4d, 1d, 1.2d })
                {
                    for (var ratioIndex = -8; ratioIndex <= 8; ratioIndex++)
                    {
                        for (var angleIndex = -6; angleIndex <= 6; angleIndex++)
                        {
                            var input = CreateInput(ratioIndex * 0.5d, angleIndex * 0.25d, grip);
                            input.NormalLoadNewtons = load;
                            var result = TireForceModel.Evaluate(input);
                            var mirroredInput = input;
                            mirroredInput.SlipRatio = -input.SlipRatio;
                            mirroredInput.SlipAngleRadians = -input.SlipAngleRadians;
                            var mirroredResult = TireForceModel.Evaluate(mirroredInput);
                            var forceMagnitude = Math.Sqrt(
                                result.LongitudinalForceNewtons * result.LongitudinalForceNewtons
                                + result.LateralForceNewtons * result.LateralForceNewtons);

                            Assert.That(forceMagnitude, Is.LessThanOrEqualTo(result.MaximumCombinedForceNewtons + 0.000001d));
                            Assert.That(mirroredResult.LongitudinalForceNewtons, Is.EqualTo(-result.LongitudinalForceNewtons).Within(0.000001d));
                            Assert.That(mirroredResult.LateralForceNewtons, Is.EqualTo(-result.LateralForceNewtons).Within(0.000001d));
                            Assert.That(result.LongitudinalForceNewtons * input.SlipRatio, Is.GreaterThanOrEqualTo(0d));
                            Assert.That(result.LateralForceNewtons * input.SlipAngleRadians, Is.LessThanOrEqualTo(0d));
                        }
                    }
                }
            }
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
