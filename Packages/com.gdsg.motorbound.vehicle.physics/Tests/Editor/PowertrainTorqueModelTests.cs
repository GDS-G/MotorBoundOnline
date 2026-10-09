using System;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class PowertrainTorqueModelTests
    {
        private const double FinalDrive = 4.1d;
        private const double Efficiency = 0.91d;

        [TestCase(3.54d)]
        [TestCase(-3.33d)]
        public void Propulsion_FollowsSelectedGearAndUsesFinalDriveEfficiency(double gear)
        {
            var expected = 190d * gear * FinalDrive * Efficiency;
            Assert.That(Calculate(190d, 0d, gear, 100d), Is.EqualTo(expected).Within(1e-10d));
            Assert.That(Calculate(190d, 0d, gear, -100d), Is.EqualTo(expected).Within(1e-10d));
        }

        [TestCase(3.54d, 100d)]
        [TestCase(3.54d, -100d)]
        [TestCase(-3.33d, 100d)]
        [TestCase(-3.33d, -100d)]
        [TestCase(3.54d, double.Epsilon)]
        [TestCase(-3.33d, -double.Epsilon)]
        public void EngineBraking_AlwaysOpposesActualCarrierRotationIncludingOpposedGear(double gear, double carrier)
        {
            var torque = Calculate(0d, 44d, gear, carrier);
            Assert.That(torque * carrier, Is.LessThanOrEqualTo(0d), "Engine braking cannot add wheel rotational energy.");
            Assert.That(Math.Sign(torque), Is.EqualTo(-Math.Sign(carrier)));
            Assert.That(Math.Abs(torque), Is.EqualTo(44d * Math.Abs(gear) * FinalDrive * Efficiency).Within(1e-10d));
        }

        [TestCase(3.54d, 100d)]
        [TestCase(-3.33d, -100d)]
        public void OrdinaryForwardAndReverseCoast_SubtractDragFromPropulsionMagnitude(double gear, double carrier)
        {
            Assert.That(Calculate(190d, 44d, gear, carrier),
                Is.EqualTo((190d - 44d) * gear * FinalDrive * Efficiency).Within(1e-10d));
        }

        [TestCase(3.54d, -100d)]
        [TestCase(-3.33d, 100d)]
        public void OpposedMomentum_PropulsionAndEngineBrakingBothResistCarrierRotation(double gear, double carrier)
        {
            var torque = Calculate(190d, 44d, gear, carrier);
            Assert.That(torque * carrier, Is.LessThan(0d));
            Assert.That(torque, Is.EqualTo((190d + 44d) * gear * FinalDrive * Efficiency).Within(1e-10d));
        }

        [TestCase(-100d)]
        [TestCase(0d)]
        [TestCase(100d)]
        public void Neutral_TransmitsNeitherPropulsionNorEngineBraking(double carrier)
        {
            Assert.That(Calculate(190d, 44d, 0d, carrier), Is.Zero);
        }

        [TestCase(3.54d)]
        [TestCase(-3.33d)]
        public void StaticCarrier_HasNoInventedBrakeDirectionOrBackwardsLaunchTorque(double gear)
        {
            Assert.That(Calculate(0d, 44d, gear, 0d), Is.Zero);
            Assert.That(Calculate(190d, 44d, gear, 0d),
                Is.EqualTo(Calculate(190d, 0d, gear, 0d)));
        }

        [TestCase(3.54d, -100d)]
        [TestCase(3.54d, 100d)]
        [TestCase(-3.33d, -100d)]
        [TestCase(-3.33d, 100d)]
        public void PropulsionAndBrakeComponents_CanBeSeparatedForPropulsionOnlyTractionControl(double gear, double carrier)
        {
            var combined = Calculate(190d, 44d, gear, carrier);
            var propulsion = Calculate(190d, 0d, gear, carrier);
            var drag = Calculate(0d, 44d, gear, carrier);
            Assert.That(combined, Is.EqualTo(propulsion + drag).Within(1e-10d));
            var torqueAfterReducingPropulsion = Calculate(95d, 0d, gear, carrier) + drag;
            Assert.That(torqueAfterReducingPropulsion - Calculate(95d, 0d, gear, carrier), Is.EqualTo(drag).Within(1e-10d));
            Assert.That(drag * carrier, Is.LessThan(0d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void EveryNonfiniteInput_IsRejectedBeforeTorqueEvaluation(double invalid)
        {
            for (var inputIndex = 0; inputIndex < 6; inputIndex++)
            {
                var values = new[] { 190d, 44d, 3.54d, FinalDrive, Efficiency, 100d };
                values[inputIndex] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() => PowertrainTorqueModel.CalculateAxleTorque(
                    values[0], values[1], values[2], values[3], values[4], values[5]));
            }
        }

        [Test]
        public void NegativeTorqueMagnitudesAndInvalidFinalDriveOrEfficiency_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(-1d, 44d, 3.54d, 100d));
            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(190d, -1d, 3.54d, 100d));
            foreach (var finalDrive in new[] { -1d, 0d })
                Assert.Throws<ArgumentOutOfRangeException>(() => PowertrainTorqueModel.CalculateAxleTorque(
                    190d, 44d, 3.54d, finalDrive, Efficiency, 100d));
            foreach (var efficiency in new[] { -1d, 0d, 1.00001d })
                Assert.Throws<ArgumentOutOfRangeException>(() => PowertrainTorqueModel.CalculateAxleTorque(
                    190d, 44d, 3.54d, FinalDrive, efficiency, 100d));
        }

        [Test]
        public void FiniteInputsWhoseTorqueOverflows_AreRejectedRatherThanReturningInfinity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(double.MaxValue, 0d, 3.54d, 100d));
            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0d, double.MaxValue, 3.54d, 100d));
            Assert.Throws<ArgumentOutOfRangeException>(() => PowertrainTorqueModel.CalculateAxleTorque(
                1d, 0d, double.MaxValue, 2d, 1d, 100d));
            Assert.Throws<ArgumentOutOfRangeException>(() => PowertrainTorqueModel.CalculateAxleTorque(
                double.MaxValue, double.MaxValue, 1d, 1d, 1d, -1d));
        }

        [Test]
        public void NeutralStillValidatesInputsButAvoidsUnnecessaryTorqueOverflow()
        {
            Assert.That(PowertrainTorqueModel.CalculateAxleTorque(double.MaxValue, double.MaxValue,
                0d, double.MaxValue, 1d, double.MaxValue), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(double.NaN, 44d, 0d, 0d));
        }

        private static double Calculate(double drive, double brake, double gear, double carrier)
        {
            return PowertrainTorqueModel.CalculateAxleTorque(drive, brake, gear, FinalDrive, Efficiency, carrier);
        }
    }
}
