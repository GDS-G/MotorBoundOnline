using System;
using MotorBound.Vehicle.Core;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class TransmissionGearSelectorTests
    {
        [Test]
        public void DefaultAndReset_SelectFirstGearAutomatic()
        {
            var selector = new TransmissionGearSelector();
            Assert.That(selector.SelectedGear, Is.EqualTo(1));
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Automatic));
            selector.RequestReverseToggle(6, 0d);
            selector.Reset();
            Assert.That(selector.SelectedGear, Is.EqualTo(1));
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Automatic));
        }

        [Test]
        public void ForwardManualShifts_WorkWhileMovingAndReportBothStateChanges()
        {
            var selector = new TransmissionGearSelector();
            var up = selector.RequestShiftUp(6, 31.2928d);
            Assert.That(up.Accepted && up.GearChanged && up.ModeChanged, Is.True);
            Assert.That(up.SelectedGear, Is.EqualTo(2));
            Assert.That(up.Mode, Is.EqualTo(DriveTransmissionMode.Manual));
            var down = selector.RequestShiftDown(6, 31.2928d);
            Assert.That(down.Accepted && down.GearChanged, Is.True);
            Assert.That(down.ModeChanged, Is.False);
            Assert.That(down.SelectedGear, Is.EqualTo(1));
        }

        [Test]
        public void ShiftEndpoints_DoNotOverflowOrInventAnotherGear()
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(int.MaxValue, int.MaxValue, 20d);
            var top = selector.RequestShiftUp(int.MaxValue, 20d);
            Assert.That(top.Accepted, Is.True);
            Assert.That(top.GearChanged, Is.False);
            Assert.That(selector.SelectedGear, Is.EqualTo(int.MaxValue));
            selector.RequestGear(-1, 6, 0d);
            var bottom = selector.RequestShiftDown(6, 0d);
            Assert.That(bottom.Accepted, Is.True);
            Assert.That(bottom.GearChanged, Is.False);
            Assert.That(selector.SelectedGear, Is.EqualTo(-1));
        }

        [TestCase(0d)]
        [TestCase(0.5d)]
        public void StoppedReverseToggle_SelectsReverseThenFirstManual(double speed)
        {
            var selector = new TransmissionGearSelector();
            Assert.That(selector.RequestReverseToggle(6, speed).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.EqualTo(-1));
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Manual));
            Assert.That(selector.RequestReverseToggle(6, speed).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.EqualTo(1));
        }

        [TestCase(0.50000001d)]
        [TestCase(31.2928d)]
        [TestCase(double.MaxValue)]
        public void MovingReverseRequest_IsAtomicAndLeavesAutomaticEnabled(double speed)
        {
            var selector = new TransmissionGearSelector();
            AssertRejectedWithoutMutation(selector, selector.RequestGear(-1, 6, speed), 1,
                DriveTransmissionMode.Automatic);
            AssertRejectedWithoutMutation(selector, selector.RequestReverseToggle(6, speed), 1,
                DriveTransmissionMode.Automatic);
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(6)]
        public void NeutralCanBeSelectedAtAnyFiniteWorldSpeed(int startingGear)
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(startingGear, 6, 0d);
            Assert.That(selector.RequestGear(0, 6, double.MaxValue).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.Zero);
        }

        [Test]
        public void NeutralDoesNotBypassReverseToForwardWhileRollingBackward()
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(-1, 6, 0d);
            // World magnitude stays positive for backwards or sideways motion.
            Assert.That(selector.RequestShiftUp(6, 12d).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.Zero);
            AssertRejectedWithoutMutation(selector, selector.RequestShiftUp(6, 12d), 0,
                DriveTransmissionMode.Manual);
            Assert.That(selector.RequestShiftUp(6, 0.5d).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.EqualTo(1));
        }

        [Test]
        public void NeutralToReverseAndDirectReverseExit_RequireStoppedMagnitude()
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(0, 6, 20d);
            Assert.That(selector.RequestShiftDown(6, 20d).Accepted, Is.False);
            selector.RequestGear(-1, 6, 0d);
            Assert.That(selector.RequestGear(1, 6, 20d).Accepted, Is.False);
            Assert.That(selector.RequestGear(1, 6, 0d).Accepted, Is.True);
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void AutomaticModeNeverPullsReverseOrNeutralIntoDrive(int gear)
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(gear, 6, 0d);
            selector.SetMode(DriveTransmissionMode.Automatic);
            for (var call = 0; call < 1000; call++)
                Assert.That(selector.SelectAutomaticGear(6, 6).Accepted, Is.False);
            Assert.That(selector.SelectedGear, Is.EqualTo(gear));
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Automatic));
        }

        [Test]
        public void AutomaticGearRequests_CannotChangeDirectionOrOverrideManual()
        {
            var selector = new TransmissionGearSelector();
            Assert.That(selector.SelectAutomaticGear(3, 6).Accepted, Is.True);
            Assert.That(selector.SelectedGear, Is.EqualTo(3));
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Automatic));
            Assert.That(selector.SelectAutomaticGear(0, 6).Accepted, Is.False);
            Assert.That(selector.SelectAutomaticGear(-1, 6).Accepted, Is.False);
            selector.ToggleMode();
            Assert.That(selector.SelectAutomaticGear(4, 6).Accepted, Is.False);
            Assert.That(selector.SelectedGear, Is.EqualTo(3));
        }

        [Test]
        public void ModeToggle_DoesNotChangeSelectedGear()
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(-1, 6, 0d);
            var automatic = selector.ToggleMode();
            Assert.That(automatic.ModeChanged, Is.True);
            Assert.That(automatic.GearChanged, Is.False);
            Assert.That(automatic.SelectedGear, Is.EqualTo(-1));
            Assert.That(selector.ToggleMode().Mode, Is.EqualTo(DriveTransmissionMode.Manual));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.001d)]
        public void InvalidWorldSpeed_IsRejectedBeforeChangingGearOrMode(double speed)
        {
            var selector = new TransmissionGearSelector();
            AssertRejectedWithoutMutation(selector, selector.RequestGear(2, 6, speed), 1,
                DriveTransmissionMode.Automatic);
            Assert.That(selector.RequestShiftUp(6, speed).Accepted, Is.False);
            Assert.That(selector.RequestReverseToggle(6, speed).Accepted, Is.False);
        }

        [TestCase(int.MinValue)]
        [TestCase(-2)]
        [TestCase(7)]
        [TestCase(int.MaxValue)]
        public void InvalidGear_IsRejectedWithMessageAndWithoutMutation(int gear)
        {
            var selector = new TransmissionGearSelector();
            AssertRejectedWithoutMutation(selector, selector.RequestGear(gear, 6, 0d), 1,
                DriveTransmissionMode.Automatic);
        }

        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        public void InvalidGearCount_IsRejected(int count)
        {
            var selector = new TransmissionGearSelector();
            Assert.That(selector.RequestGear(1, count, 0d).Accepted, Is.False);
            Assert.That(selector.RequestShiftUp(count, 0d).Accepted, Is.False);
            Assert.That(selector.SelectAutomaticGear(1, count).Accepted, Is.False);
            Assert.That(selector.Mode, Is.EqualTo(DriveTransmissionMode.Automatic));
        }

        [TestCase(int.MinValue)]
        [TestCase(-2)]
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void InvalidShiftDirection_IsRejectedWithoutArithmetic(int direction)
        {
            var selector = new TransmissionGearSelector();
            AssertRejectedWithoutMutation(selector, selector.RequestShift(direction, 6, 0d), 1,
                DriveTransmissionMode.Automatic);
        }

        [Test]
        public void InvalidMode_IsRejectedWithoutMutation()
        {
            var selector = new TransmissionGearSelector();
            AssertRejectedWithoutMutation(selector, selector.SetMode((DriveTransmissionMode)int.MaxValue), 1,
                DriveTransmissionMode.Automatic);
        }

        [Test]
        public void Ratios_PreserveReverseSignAndNeutralIsExactlyZero()
        {
            var definition = new TransmissionDefinition { ForwardGearRatios = new[] { 3.54d, 2.13d }, ReverseGearRatio = -3.33d };
            var selector = new TransmissionGearSelector();
            Assert.That(selector.GetSelectedGearRatio(definition), Is.EqualTo(3.54d));
            selector.RequestGear(2, 2, 20d);
            Assert.That(selector.GetSelectedGearRatio(definition), Is.EqualTo(2.13d));
            selector.RequestGear(0, 2, 20d);
            Assert.That(selector.GetSelectedGearRatio(definition), Is.Zero);
            selector.RequestGear(-1, 2, 0d);
            Assert.That(selector.GetSelectedGearRatio(definition), Is.EqualTo(-3.33d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(0d)]
        [TestCase(-1d)]
        public void InvalidSelectedForwardRatio_IsRejected(double ratio)
        {
            Assert.Throws<ArgumentException>(() => new TransmissionGearSelector().GetSelectedGearRatio(
                new TransmissionDefinition { ForwardGearRatios = new[] { ratio } }));
        }

        [TestCase(double.NaN)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(0d)]
        [TestCase(1d)]
        public void InvalidReverseRatio_IsRejected(double ratio)
        {
            var selector = new TransmissionGearSelector();
            selector.RequestGear(-1, 1, 0d);
            Assert.Throws<ArgumentException>(() => selector.GetSelectedGearRatio(
                new TransmissionDefinition { ForwardGearRatios = new[] { 1d }, ReverseGearRatio = ratio }));
        }

        [Test]
        public void MissingRatioDefinitionOrSelectedGear_IsRejected()
        {
            var selector = new TransmissionGearSelector();
            Assert.Throws<ArgumentNullException>(() => selector.GetSelectedGearRatio(null));
            Assert.Throws<ArgumentException>(() => selector.GetSelectedGearRatio(new TransmissionDefinition()));
            selector.RequestGear(2, 2, 0d);
            Assert.Throws<ArgumentException>(() => selector.GetSelectedGearRatio(
                new TransmissionDefinition { ForwardGearRatios = new[] { 1d } }));
        }

        private static void AssertRejectedWithoutMutation(TransmissionGearSelector selector,
            TransmissionGearSelectionResult result, int gear, DriveTransmissionMode mode)
        {
            Assert.That(result.Accepted || result.GearChanged || result.ModeChanged, Is.False);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(result.SelectedGear, Is.EqualTo(gear));
            Assert.That(result.Mode, Is.EqualTo(mode));
            Assert.That(selector.SelectedGear, Is.EqualTo(gear));
            Assert.That(selector.Mode, Is.EqualTo(mode));
        }
    }
}
