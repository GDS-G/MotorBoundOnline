using System;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class WheelContactSolverTests
    {
        [TestCase(0.5d)]
        [TestCase(1.5d)]
        [TestCase(3d)]
        [TestCase(8.33d)]
        [TestCase(-3d)]
        public void LowSpeedRollingPerturbation_SettlesWithoutNumericalChatter(double speed)
        {
            var input = CreateInput(speed);
            input.AngularSpeedRadiansPerSecond = speed / input.RadiusMeters * 1.02d;
            var previousSlip = (input.AngularSpeedRadiansPerSecond * input.RadiusMeters - speed)
                / Math.Max(Math.Abs(speed), WheelContactSolver.MinimumSlipReferenceSpeedMetersPerSecond);
            for (var step = 0; step < 180; step++)
            {
                var result = WheelContactSolver.Solve(input);
                Assert.That(result.SlipRatio * previousSlip, Is.GreaterThanOrEqualTo(-0.0000000001d),
                    "A rolling wheel must not alternate between braking and wheelspin from the integration step.");
                Assert.That(Math.Abs(result.SlipRatio), Is.LessThanOrEqualTo(Math.Abs(previousSlip) + 0.0000000001d));
                Assert.That(result.SubstepCount, Is.InRange(1, 64));
                input.AngularSpeedRadiansPerSecond = result.AngularSpeedRadiansPerSecond;
                previousSlip = result.SlipRatio;
            }
            Assert.That(Math.Abs(previousSlip), Is.LessThan(0.00000001d));
        }

        [TestCase(1.5d)]
        [TestCase(3d)]
        [TestCase(8.33d)]
        public void SubpeakDriveTorque_SettlesToItsTransmittedForce(double speed)
        {
            var input = CreateInput(speed);
            input.DriveTorqueNewtonMeters = 120d;
            input.RollingResistanceCoefficient = 0.013d;
            WheelContactResult result = default(WheelContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = WheelContactSolver.Solve(input);
                Assert.That(result.FinalTireState.IsSliding, Is.False);
                input.AngularSpeedRadiansPerSecond = result.AngularSpeedRadiansPerSecond;
            }
            Assert.That(result.MeanLongitudinalForceNewtons * input.RadiusMeters,
                Is.EqualTo(input.DriveTorqueNewtonMeters).Within(0.0000001d));
            Assert.That(result.SlipRatio, Is.InRange(0d, 0.02d));
        }

        [TestCase(0d)]
        [TestCase(180d)]
        [TestCase(1400d)]
        public void MeanDeliveredForceAndBrakeImpulse_AccountForAngularMomentum(double brakeTorque)
        {
            var input = CreateInput(3d);
            input.DriveTorqueNewtonMeters = 120d;
            input.BrakeTorqueNewtonMeters = brakeTorque;
            input.RollingResistanceCoefficient = 0.013d;
            input.Tire.SlipAngleRadians = 0.08d;
            var result = WheelContactSolver.Solve(input);
            var angularMomentumChange = (result.AngularSpeedRadiansPerSecond - input.AngularSpeedRadiansPerSecond)
                * input.RotationalInertiaKilogramMetersSquared;
            var appliedImpulse = (input.DriveTorqueNewtonMeters
                                  - result.MeanLongitudinalForceNewtons * input.RadiusMeters) * input.DeltaTimeSeconds
                                 - result.BrakeAngularImpulseNewtonMeterSeconds
                                 + result.AngularLimitImpulseNewtonMeterSeconds;
            Assert.That(angularMomentumChange, Is.EqualTo(appliedImpulse).Within(0.000000001d));
            Assert.That(Math.Abs(result.BrakeAngularImpulseNewtonMeterSeconds),
                Is.LessThanOrEqualTo(brakeTorque * input.DeltaTimeSeconds + 0.000000001d));
            AssertEnvelope(result);
        }

        [TestCase(13.89d)]
        [TestCase(-13.89d)]
        public void HandbrakeLock_HoldsZeroRotationWithoutReversalOrLostSlidingGrip(double speed)
        {
            var input = CreateInput(speed);
            input.BrakeTorqueNewtonMeters = 1400d;
            input.Tire.SlipAngleRadians = 0.1d;
            WheelContactResult result = default(WheelContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = WheelContactSolver.Solve(input);
                Assert.That(result.AngularSpeedRadiansPerSecond * speed, Is.GreaterThanOrEqualTo(0d),
                    "Brake torque must never drive a forward wheel backwards or vice versa.");
                input.AngularSpeedRadiansPerSecond = result.AngularSpeedRadiansPerSecond;
            }
            Assert.That(result.AngularSpeedRadiansPerSecond, Is.Zero);
            Assert.That(result.SlipRatio, Is.EqualTo(-Math.Sign(speed)).Within(0.000001d));
            Assert.That(result.FinalTireState.IsSliding, Is.True);
            Assert.That(result.MeanLongitudinalForceNewtons * speed, Is.LessThan(0d));
            Assert.That(Math.Abs(result.MeanLateralForceNewtons),
                Is.LessThan(result.FinalTireState.MaximumCombinedForceNewtons * 0.2d));
            AssertEnvelope(result);
        }

        [Test]
        public void OverGripDriveTorque_StillProducesWheelspin()
        {
            var input = CreateInput(8d);
            input.DriveTorqueNewtonMeters = 1500d;
            WheelContactResult result = default(WheelContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = WheelContactSolver.Solve(input);
                AssertEnvelope(result);
                input.AngularSpeedRadiansPerSecond = result.AngularSpeedRadiansPerSecond;
            }
            Assert.That(result.SlipRatio, Is.GreaterThan(0.2d));
            Assert.That(result.FinalTireState.IsSliding, Is.True,
                "Numerical stability must not force a driven tire back into grip.");
        }

        private static void AssertEnvelope(WheelContactResult result)
        {
            var magnitude = Math.Sqrt(result.MeanLongitudinalForceNewtons * result.MeanLongitudinalForceNewtons
                                      + result.MeanLateralForceNewtons * result.MeanLateralForceNewtons);
            Assert.That(magnitude, Is.LessThanOrEqualTo(result.FinalTireState.MaximumCombinedForceNewtons + 0.000001d));
        }

        private static WheelContactInput CreateInput(double speed)
        {
            return new WheelContactInput
            {
                AngularSpeedRadiansPerSecond = speed / 0.305d,
                LongitudinalSpeedMetersPerSecond = speed,
                RadiusMeters = 0.305d,
                RotationalInertiaKilogramMetersSquared = 1.15d,
                DeltaTimeSeconds = 1d / 360d,
                Tire = new TireForceInput
                {
                    NormalLoadNewtons = 2800d,
                    PeakDryFrictionCoefficient = 1.08d,
                    SlidingGripRatio = 0.78d,
                    SurfaceGripMultiplier = 1d,
                    LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
                    CorneringStiffnessNewtonPerRadian = 65000d,
                    ReferenceLoadNewtons = 3300d,
                    LoadSensitivityExponent = -0.08d
                }
            };
        }
    }
}
