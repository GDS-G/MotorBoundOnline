using System;
using MotorBound.Vehicle.Core;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class AxleContactSolverTests
    {
        [Test]
        public void OpenAxle_MatchesIndependentWheelSolverAtSharedTimestep()
        {
            var input = CreateInput(3d);
            input.Differential.Type = DifferentialType.Open;
            input.AxleDriveTorqueNewtonMeters = 240d;
            input.Left.DriveTorqueNewtonMeters = 120d;
            input.Right.DriveTorqueNewtonMeters = 120d;
            input.Left.BrakeTorqueNewtonMeters = 70d;
            input.Right.BrakeTorqueNewtonMeters = 140d;
            var result = AxleContactSolver.Solve(input);
            AssertEquivalent(result.Left, WheelContactSolver.Solve(input.Left));
            AssertEquivalent(result.Right, WheelContactSolver.Solve(input.Right));
            Assert.That(result.MeanDifferentialTransferTorqueNewtonMeters, Is.Zero);
            Assert.That(result.LeftMeanDriveTorqueNewtonMeters, Is.EqualTo(120d));
            Assert.That(result.RightMeanDriveTorqueNewtonMeters, Is.EqualTo(120d));
        }

        [Test]
        public void ZeroCapacityClutch_IsExactlyEquivalentToOpenAxle()
        {
            var input = CreateInput(8d);
            input.AxleDriveTorqueNewtonMeters = 1000d;
            input.Left.AngularSpeedRadiansPerSecond *= 2d;
            input.Differential = new DifferentialDefinition { Type = DifferentialType.ClutchLimitedSlip };
            var clutch = AxleContactSolver.Solve(input);
            input.Differential.Type = DifferentialType.Open;
            var open = AxleContactSolver.Solve(input);
            AssertEquivalent(clutch.Left, open.Left);
            AssertEquivalent(clutch.Right, open.Right);
            Assert.That(clutch.DifferentialDissipatedEnergyJoules, Is.Zero);
        }

        [Test]
        public void BothAirborne_CouplesWheelRotationWithoutAnyTireForce()
        {
            var input = CreateInput(0d);
            input.Left.Tire.NormalLoadNewtons = input.Right.Tire.NormalLoadNewtons = 0d;
            input.Left.AngularSpeedRadiansPerSecond = 100d;
            input.Right.AngularSpeedRadiansPerSecond = 20d;
            input.Right.RotationalInertiaKilogramMetersSquared = 1.6d;
            input.AxleDriveTorqueNewtonMeters = 1000d;
            var result = AxleContactSolver.Solve(input);
            AssertMomentum(input.Left, result.Left, result.LeftMeanDriveTorqueNewtonMeters);
            AssertMomentum(input.Right, result.Right, result.RightMeanDriveTorqueNewtonMeters);
            Assert.That(result.Left.MeanLongitudinalForceNewtons, Is.Zero);
            Assert.That(result.Right.MeanLongitudinalForceNewtons, Is.Zero);
            Assert.That(result.Left.MeanLateralForceNewtons, Is.Zero);
            Assert.That(result.Right.MeanLateralForceNewtons, Is.Zero);
            Assert.That(result.DifferentialDissipatedEnergyJoules, Is.GreaterThan(0d));
        }

        [Test]
        public void LoadedZeroGripContact_WithRollingResistanceHasNoForceButAllowsPassiveClutchTransfer()
        {
            var input = CreateInput(8d);
            input.Left.Tire.SurfaceGripMultiplier = 0d;
            input.Left.RollingResistanceCoefficient = 0.013d;
            input.Left.AngularSpeedRadiansPerSecond = 100d;
            input.Left.Tire.SlipAngleRadians = 0.2d;
            input.AxleDriveTorqueNewtonMeters = 1000d;
            var result = AxleContactSolver.Solve(input);

            Assert.That(input.Left.Tire.NormalLoadNewtons, Is.GreaterThan(0d));
            Assert.That(result.Left.MeanLongitudinalForceNewtons, Is.Zero,
                "Rolling drag must not create contact force when the complete friction envelope is zero.");
            Assert.That(result.Left.MeanLateralForceNewtons, Is.Zero);
            Assert.That(result.Left.FinalTireState.MaximumCombinedForceNewtons, Is.Zero);
            Assert.That(result.MeanDifferentialTransferTorqueNewtonMeters, Is.GreaterThan(0d));
            Assert.That(result.RightMeanDriveTorqueNewtonMeters, Is.GreaterThan(input.AxleDriveTorqueNewtonMeters * 0.5d));
            Assert.That(result.DifferentialDissipatedEnergyJoules, Is.GreaterThan(0d));
            AssertMomentum(input.Left, result.Left, result.LeftMeanDriveTorqueNewtonMeters);
            AssertMomentum(input.Right, result.Right, result.RightMeanDriveTorqueNewtonMeters);
            AssertFiniteAndEnvelope(result.Left);
        }

        [TestCase(0d)]
        [TestCase(180d)]
        [TestCase(1400d)]
        public void UnequalInertiaContactAndBrakes_AccountForEachWheelAndTotalMomentum(double brake)
        {
            var input = CreateInput(8d);
            input.Right.RotationalInertiaKilogramMetersSquared = 1.6d;
            input.Left.AngularSpeedRadiansPerSecond *= 1.03d;
            input.Right.AngularSpeedRadiansPerSecond *= 0.95d;
            input.Left.Tire.SlipAngleRadians = 0.06d;
            input.Right.Tire.SlipAngleRadians = 0.10d;
            input.Right.Tire.NormalLoadNewtons = 1600d;
            input.Left.RollingResistanceCoefficient = input.Right.RollingResistanceCoefficient = 0.013d;
            input.Left.BrakeTorqueNewtonMeters = input.Right.BrakeTorqueNewtonMeters = brake;
            input.AxleDriveTorqueNewtonMeters = 1000d;
            var result = AxleContactSolver.Solve(input);
            AssertMomentum(input.Left, result.Left, result.LeftMeanDriveTorqueNewtonMeters);
            AssertMomentum(input.Right, result.Right, result.RightMeanDriveTorqueNewtonMeters);
            Assert.That(result.LeftMeanDriveTorqueNewtonMeters + result.RightMeanDriveTorqueNewtonMeters,
                Is.EqualTo(input.AxleDriveTorqueNewtonMeters).Within(1e-10d));
            Assert.That(result.DifferentialDissipatedEnergyJoules, Is.GreaterThanOrEqualTo(0d));
            AssertFiniteAndEnvelope(result.Left);
            AssertFiniteAndEnvelope(result.Right);
        }

        [TestCase(0.5d)]
        [TestCase(1.5d)]
        [TestCase(3d)]
        [TestCase(8.33d)]
        public void CoupledLowSpeedRolling_RemainsStableWithoutAlternatingWheelspinAndBraking(double speed)
        {
            var input = CreateInput(speed);
            input.Left.AngularSpeedRadiansPerSecond *= 1.02d;
            input.Right.AngularSpeedRadiansPerSecond *= 0.98d;
            AxleContactResult result = default(AxleContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = AxleContactSolver.Solve(input);
                Assert.That(result.Left.SlipRatio, Is.GreaterThanOrEqualTo(-1e-10d));
                Assert.That(result.Right.SlipRatio, Is.LessThanOrEqualTo(1e-10d));
                AssertFiniteAndEnvelope(result.Left);
                AssertFiniteAndEnvelope(result.Right);
                input.Left.AngularSpeedRadiansPerSecond = result.Left.AngularSpeedRadiansPerSecond;
                input.Right.AngularSpeedRadiansPerSecond = result.Right.AngularSpeedRadiansPerSecond;
            }
            Assert.That(Math.Abs(result.Left.SlipRatio) + Math.Abs(result.Right.SlipRatio), Is.LessThan(1e-8d));
        }

        [TestCase(0d)]
        [TestCase(0.1d)]
        public void UnloadedOrLowGripInsideWheel_PassivelyTransfersDriveToLoadedWheel(double insideGrip)
        {
            var limited = CreateInput(8d);
            limited.AxleDriveTorqueNewtonMeters = 1200d;
            limited.Left.Tire.SurfaceGripMultiplier = insideGrip;
            if (insideGrip == 0d) limited.Left.Tire.NormalLoadNewtons = 0d;
            var open = limited;
            open.Differential = new DifferentialDefinition();
            AxleContactResult limitedResult = default(AxleContactResult);
            AxleContactResult openResult = default(AxleContactResult);
            for (var step = 0; step < 360; step++)
            {
                limitedResult = AxleContactSolver.Solve(limited);
                openResult = AxleContactSolver.Solve(open);
                AssertMomentum(limited.Left, limitedResult.Left, limitedResult.LeftMeanDriveTorqueNewtonMeters);
                AssertMomentum(limited.Right, limitedResult.Right, limitedResult.RightMeanDriveTorqueNewtonMeters);
                limited.Left.AngularSpeedRadiansPerSecond = limitedResult.Left.AngularSpeedRadiansPerSecond;
                limited.Right.AngularSpeedRadiansPerSecond = limitedResult.Right.AngularSpeedRadiansPerSecond;
                open.Left.AngularSpeedRadiansPerSecond = openResult.Left.AngularSpeedRadiansPerSecond;
                open.Right.AngularSpeedRadiansPerSecond = openResult.Right.AngularSpeedRadiansPerSecond;
            }
            Assert.That(limitedResult.MeanDifferentialTransferTorqueNewtonMeters, Is.GreaterThan(0d));
            Assert.That(limitedResult.Right.MeanLongitudinalForceNewtons, Is.GreaterThan(openResult.Right.MeanLongitudinalForceNewtons));
            Assert.That(limitedResult.Left.AngularSpeedRadiansPerSecond, Is.LessThan(openResult.Left.AngularSpeedRadiansPerSecond));
            Assert.That(limitedResult.Left.MeanLateralForceNewtons, Is.Zero);
            Assert.That(limitedResult.Right.MeanLateralForceNewtons, Is.Zero, "The differential cannot manufacture lateral tire force or yaw.");
        }

        [Test]
        public void HandbrakeLock_HoldsBothWheelsWithClutchCouplingAndForceEnvelopeIntact()
        {
            var input = CreateInput(14d);
            input.Left.Tire.SlipAngleRadians = input.Right.Tire.SlipAngleRadians = 0.1d;
            input.Left.BrakeTorqueNewtonMeters = input.Right.BrakeTorqueNewtonMeters = 1400d;
            AxleContactResult result = default(AxleContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = AxleContactSolver.Solve(input);
                Assert.That(result.Left.AngularSpeedRadiansPerSecond, Is.GreaterThanOrEqualTo(0d));
                Assert.That(result.Right.AngularSpeedRadiansPerSecond, Is.GreaterThanOrEqualTo(0d));
                AssertFiniteAndEnvelope(result.Left);
                AssertFiniteAndEnvelope(result.Right);
                input.Left.AngularSpeedRadiansPerSecond = result.Left.AngularSpeedRadiansPerSecond;
                input.Right.AngularSpeedRadiansPerSecond = result.Right.AngularSpeedRadiansPerSecond;
            }
            Assert.That(result.Left.AngularSpeedRadiansPerSecond, Is.Zero);
            Assert.That(result.Right.AngularSpeedRadiansPerSecond, Is.Zero);
            Assert.That(result.Left.FinalTireState.IsSliding, Is.True);
            Assert.That(result.Right.FinalTireState.IsSliding, Is.True);
        }

        [Test]
        public void OverGripAxleDrive_CanSpinBothWheelsDespiteNumericallyStableLimitedSlip()
        {
            var input = CreateInput(8d);
            input.AxleDriveTorqueNewtonMeters = 3000d;
            AxleContactResult result = default(AxleContactResult);
            for (var step = 0; step < 180; step++)
            {
                result = AxleContactSolver.Solve(input);
                input.Left.AngularSpeedRadiansPerSecond = result.Left.AngularSpeedRadiansPerSecond;
                input.Right.AngularSpeedRadiansPerSecond = result.Right.AngularSpeedRadiansPerSecond;
            }
            Assert.That(result.Left.SlipRatio, Is.GreaterThan(0.2d));
            Assert.That(result.Right.SlipRatio, Is.GreaterThan(0.2d));
            Assert.That(result.Left.FinalTireState.IsSliding && result.Right.FinalTireState.IsSliding, Is.True);
            AssertFiniteAndEnvelope(result.Left);
            AssertFiniteAndEnvelope(result.Right);
        }

        private static void AssertEquivalent(WheelContactResult actual, WheelContactResult expected)
        {
            Assert.That(actual.AngularSpeedRadiansPerSecond, Is.EqualTo(expected.AngularSpeedRadiansPerSecond).Within(1e-10d));
            Assert.That(actual.MeanLongitudinalForceNewtons, Is.EqualTo(expected.MeanLongitudinalForceNewtons).Within(1e-10d));
            Assert.That(actual.MeanLateralForceNewtons, Is.EqualTo(expected.MeanLateralForceNewtons).Within(1e-10d));
            Assert.That(actual.BrakeAngularImpulseNewtonMeterSeconds, Is.EqualTo(expected.BrakeAngularImpulseNewtonMeterSeconds).Within(1e-10d));
        }

        private static void AssertMomentum(WheelContactInput input, WheelContactResult result, double deliveredDriveTorque)
        {
            var momentumChange = (result.AngularSpeedRadiansPerSecond - input.AngularSpeedRadiansPerSecond) * input.RotationalInertiaKilogramMetersSquared;
            var accounted = (deliveredDriveTorque - result.MeanLongitudinalForceNewtons * input.RadiusMeters) * input.DeltaTimeSeconds
                            - result.BrakeAngularImpulseNewtonMeterSeconds + result.AngularLimitImpulseNewtonMeterSeconds;
            Assert.That(momentumChange, Is.EqualTo(accounted).Within(1e-8d));
        }

        private static void AssertFiniteAndEnvelope(WheelContactResult result)
        {
            Assert.That(double.IsNaN(result.AngularSpeedRadiansPerSecond) || double.IsInfinity(result.AngularSpeedRadiansPerSecond), Is.False);
            var magnitude = Math.Sqrt(result.MeanLongitudinalForceNewtons * result.MeanLongitudinalForceNewtons + result.MeanLateralForceNewtons * result.MeanLateralForceNewtons);
            Assert.That(magnitude, Is.LessThanOrEqualTo(result.FinalTireState.MaximumCombinedForceNewtons + 1e-6d));
        }

        private static AxleContactInput CreateInput(double speed)
        {
            var wheel = new WheelContactInput
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
                    SlidingGripRatio = 0.90d,
                    SurfaceGripMultiplier = 1d,
                    LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
                    CorneringStiffnessNewtonPerRadian = 65000d,
                    ReferenceLoadNewtons = 3300d,
                    LoadSensitivityExponent = -0.08d
                }
            };
            return new AxleContactInput { Left = wheel, Right = wheel, Differential = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype().RearDifferential };
        }
    }
}
