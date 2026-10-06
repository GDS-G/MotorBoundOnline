using System;
using MotorBound.Vehicle.Core;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class DifferentialModelTests
    {
        [TestCase(0d)]
        [TestCase(1200d)]
        [TestCase(-1200d)]
        public void OpenAxle_DoesNotCoupleDifferentWheelSpeeds(double driveTorque)
        {
            var input = CreateInput();
            input.Definition.Type = DifferentialType.Open;
            input.AxleDriveTorqueNewtonMeters = driveTorque;
            var result = DifferentialModel.Evaluate(input);
            Assert.That(result.TransferAngularImpulseNewtonMeterSeconds, Is.Zero);
            Assert.That(result.DissipatedEnergyJoules, Is.Zero);
        }

        [TestCase(50d, 20d)]
        [TestCase(20d, 50d)]
        [TestCase(-20d, -50d)]
        [TestCase(20d, -50d)]
        public void ClutchTransfer_ConservesMomentumAndOnlyDissipatesEnergy(double leftSpeed, double rightSpeed)
        {
            var input = CreateInput();
            input.LeftAngularSpeedRadiansPerSecond = leftSpeed;
            input.RightAngularSpeedRadiansPerSecond = rightSpeed;
            var result = DifferentialModel.Evaluate(input);
            var impulse = result.TransferAngularImpulseNewtonMeterSeconds;
            var afterLeft = leftSpeed - impulse / input.LeftInertiaKilogramMetersSquared;
            var afterRight = rightSpeed + impulse / input.RightInertiaKilogramMetersSquared;
            var beforeMomentum = leftSpeed * input.LeftInertiaKilogramMetersSquared + rightSpeed * input.RightInertiaKilogramMetersSquared;
            var afterMomentum = afterLeft * input.LeftInertiaKilogramMetersSquared + afterRight * input.RightInertiaKilogramMetersSquared;
            var beforeEnergy = Energy(leftSpeed, rightSpeed, input);
            var afterEnergy = Energy(afterLeft, afterRight, input);
            Assert.That(afterMomentum, Is.EqualTo(beforeMomentum).Within(1e-10d));
            Assert.That(afterEnergy, Is.LessThanOrEqualTo(beforeEnergy));
            Assert.That(beforeEnergy - afterEnergy, Is.EqualTo(result.DissipatedEnergyJoules).Within(1e-10d));
            Assert.That((afterLeft - afterRight) * (leftSpeed - rightSpeed), Is.GreaterThanOrEqualTo(-1e-10d));
        }

        [Test]
        public void ExcessiveClutchCapacity_CannotOvershootEqualizationOrManufactureEnergy()
        {
            var input = CreateInput();
            input.Definition.PreloadTorqueNewtonMeters = 1e9d;
            var result = DifferentialModel.Evaluate(input);
            var left = input.LeftAngularSpeedRadiansPerSecond - result.TransferAngularImpulseNewtonMeterSeconds / input.LeftInertiaKilogramMetersSquared;
            var right = input.RightAngularSpeedRadiansPerSecond + result.TransferAngularImpulseNewtonMeterSeconds / input.RightInertiaKilogramMetersSquared;
            Assert.That(left, Is.EqualTo(right).Within(1e-10d));
            Assert.That(result.DissipatedEnergyJoules, Is.GreaterThan(0d));
        }

        [TestCase(1200d, 144d)]
        [TestCase(-1200d, 36d)]
        public void PowerAndCoastResistance_UsesSignedMechanicalWork(double torque, double expectedTransfer)
        {
            var input = CreateInput();
            input.Definition.PreloadTorqueNewtonMeters = 0d;
            input.Definition.SlipSpeedGainNewtonMeterSecondsPerRadian = 0d;
            input.AxleDriveTorqueNewtonMeters = torque;
            Assert.That(DifferentialModel.Evaluate(input).TransferTorqueNewtonMeters, Is.EqualTo(expectedTransfer).Within(1e-10d));
            input.LeftAngularSpeedRadiansPerSecond *= -1d;
            input.RightAngularSpeedRadiansPerSecond *= -1d;
            input.AxleDriveTorqueNewtonMeters *= -1d;
            Assert.That(DifferentialModel.Evaluate(input).TransferTorqueNewtonMeters, Is.EqualTo(-expectedTransfer).Within(1e-10d));
        }

        [Test]
        public void ModestPreload_AllowsNormalInsideOutsideWheelSpeedDifference()
        {
            var input = CreateInput();
            input.AxleDriveTorqueNewtonMeters = 0d;
            input.LeftAngularSpeedRadiansPerSecond = 40d;
            input.RightAngularSpeedRadiansPerSecond = 38d;
            var impulse = DifferentialModel.Evaluate(input).TransferAngularImpulseNewtonMeterSeconds;
            var difference = 2d - impulse * (1d / input.LeftInertiaKilogramMetersSquared + 1d / input.RightInertiaKilogramMetersSquared);
            Assert.That(difference, Is.GreaterThan(1d), "This is a limited-slip clutch, not a welded axle.");
        }

        private static double Energy(double left, double right, DifferentialInput input)
        {
            return 0.5d * (input.LeftInertiaKilogramMetersSquared * left * left + input.RightInertiaKilogramMetersSquared * right * right);
        }

        private static DifferentialInput CreateInput()
        {
            return new DifferentialInput
            {
                Definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype().RearDifferential,
                LeftAngularSpeedRadiansPerSecond = 50d,
                RightAngularSpeedRadiansPerSecond = 20d,
                LeftInertiaKilogramMetersSquared = 1.15d,
                RightInertiaKilogramMetersSquared = 1.6d,
                AxleDriveTorqueNewtonMeters = 1200d,
                DeltaTimeSeconds = 1d / 360d
            };
        }
    }
}
