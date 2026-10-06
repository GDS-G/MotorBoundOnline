using System;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class RoadTractionControlTests
    {
        [Test]
        public void SubcapacityRequest_PreservesEngineTorque()
        {
            var input = CreateInput();
            input.RequestedDriveTorquePerWheelNewtonMeters = 100d;
            var result = RoadTractionControl.Evaluate(input);
            Assert.That(result.DriveTorquePerWheelNewtonMeters, Is.EqualTo(100d));
            Assert.That(result.DeliveredDriveTorqueScale, Is.EqualTo(1d));
            Assert.That(result.Active, Is.False);
        }

        [Test]
        public void PositiveTorqueCeiling_UsesPhysicalCombinedSlipCapacity()
        {
            var input = CreateInput();
            input.LoadedDrivenContacts[0].SlipAngleRadians = 0.08d;
            var target = input.LoadedDrivenContacts[0];
            target.SlipRatio = RoadTractionControl.EvaluateTargetSlipRatio(target.SlipAngleRadians);
            var expected = TireForceModel.Evaluate(target).LongitudinalForceNewtons * input.TireRadiusMeters;
            var result = RoadTractionControl.Evaluate(input);
            Assert.That(result.DriveTorquePerWheelNewtonMeters, Is.EqualTo(expected).Within(0.00000001d));
            Assert.That(result.DeliveredDriveTorqueScale, Is.InRange(0d, 1d));
            Assert.That(result.Active, Is.True);
        }

        [Test]
        public void WetContact_ReducesAvailableEngineTorque()
        {
            var dry = RoadTractionControl.Evaluate(CreateInput());
            var wetInput = CreateInput();
            wetInput.LoadedDrivenContacts[0].SurfaceGripMultiplier = 0.45d;
            var wet = RoadTractionControl.Evaluate(wetInput);
            Assert.That(wet.DriveTorquePerWheelNewtonMeters, Is.LessThan(dry.DriveTorquePerWheelNewtonMeters));
        }

        [Test]
        public void UnloadedInsideContact_ReducesSharedTorqueBeforeWheelspin()
        {
            var input = CreateInput();
            var fullLoad = RoadTractionControl.Evaluate(input);
            input.LoadedDrivenContacts[0].NormalLoadNewtons = 900d;
            var lowerLoad = RoadTractionControl.Evaluate(input);
            Assert.That(lowerLoad.DriveTorquePerWheelNewtonMeters, Is.LessThan(fullLoad.DriveTorquePerWheelNewtonMeters));
        }

        [Test]
        public void LateralDemand_ReservesLessTorqueForPropulsion()
        {
            var straight = RoadTractionControl.Evaluate(CreateInput());
            var input = CreateInput();
            input.LoadedDrivenContacts[0].SlipAngleRadians = 0.2d;
            var cornering = RoadTractionControl.Evaluate(input);
            Assert.That(cornering.DriveTorquePerWheelNewtonMeters, Is.LessThan(straight.DriveTorquePerWheelNewtonMeters));
        }

        [Test]
        public void ExcessPositiveSlip_CutsTorqueFurtherSoReactionCanSlowWheel()
        {
            var rolling = RoadTractionControl.Evaluate(CreateInput());
            var input = CreateInput();
            input.LoadedDrivenContacts[0].SlipRatio = RoadTractionControl.TargetSlipRatio * 4d;
            var spinning = RoadTractionControl.Evaluate(input);
            Assert.That(spinning.DriveTorquePerWheelNewtonMeters,
                Is.EqualTo(rolling.DriveTorquePerWheelNewtonMeters * 0.25d).Within(0.00000001d));
        }

        [Test]
        public void CorneringReserveTarget_IsBoundedSymmetricAndSmoothAtSmallSignals()
        {
            Assert.That(RoadTractionControl.EvaluateTargetSlipRatio(0d), Is.EqualTo(0.045d));
            var previous = 0.045d;
            for (var sample = 0; sample <= 400; sample++)
            {
                var angle = sample * 0.01d * Math.PI / 180d;
                var target = RoadTractionControl.EvaluateTargetSlipRatio(angle);
                Assert.That(target, Is.InRange(0.02d, 0.045d));
                Assert.That(target, Is.LessThanOrEqualTo(previous + 0.000000001d));
                // Smoothstep's maximum derivative is 1.5; .025 range over .9 degrees.
                Assert.That(Math.Abs(target - previous), Is.LessThan(0.00042d),
                    "A small change in measured lateral slip must not jump the torque-control target.");
                Assert.That(RoadTractionControl.EvaluateTargetSlipRatio(-angle), Is.EqualTo(target));
                previous = target;
            }
            Assert.That(RoadTractionControl.EvaluateTargetSlipRatio(0.1d * Math.PI / 180d), Is.EqualTo(0.045d));
            Assert.That(RoadTractionControl.EvaluateTargetSlipRatio(0.101d * Math.PI / 180d),
                Is.EqualTo(0.045d).Within(0.0000001d));
            Assert.That(RoadTractionControl.EvaluateTargetSlipRatio(1d * Math.PI / 180d), Is.EqualTo(0.02d));
            Assert.That(previous, Is.EqualTo(0.02d));
        }

        [Test]
        public void CorneringReserve_UsesSameTargetForCapacityAndWheelspinFeedback()
        {
            var input = CreateInput();
            input.LoadedDrivenContacts[0].SlipAngleRadians = 2d * Math.PI / 180d;
            var targetState = input.LoadedDrivenContacts[0];
            var targetSlip = RoadTractionControl.EvaluateTargetSlipRatio(targetState.SlipAngleRadians);
            input.LoadedDrivenContacts[0].SlipRatio = targetSlip * 2d;
            targetState.SlipRatio = targetSlip;
            var capacity = TireForceModel.Evaluate(targetState).LongitudinalForceNewtons * input.TireRadiusMeters;
            var result = RoadTractionControl.Evaluate(input);
            Assert.That(result.DriveTorquePerWheelNewtonMeters, Is.EqualTo(capacity * 0.5d).Within(0.00000001d));
        }

        [Test]
        public void NegativeBrakingSlip_DoesNotRequestAnAdditionalEngineCut()
        {
            var rolling = RoadTractionControl.Evaluate(CreateInput());
            var input = CreateInput();
            input.LoadedDrivenContacts[0].SlipRatio = -1d;
            var braking = RoadTractionControl.Evaluate(input);
            Assert.That(braking.DriveTorquePerWheelNewtonMeters, Is.EqualTo(rolling.DriveTorquePerWheelNewtonMeters));
        }

        [Test]
        public void CommonEngineActuator_UsesLowerDrivenWheelCapacity()
        {
            var input = CreateInput();
            var weakerContact = input.LoadedDrivenContacts[0];
            weakerContact.NormalLoadNewtons = 800d;
            input.LoadedDrivenContacts = new[] { input.LoadedDrivenContacts[0], weakerContact };
            input.ContactCount = 2;
            var combined = RoadTractionControl.Evaluate(input);
            input.LoadedDrivenContacts = new[] { weakerContact };
            input.ContactCount = 1;
            var weaker = RoadTractionControl.Evaluate(input);
            Assert.That(combined.DriveTorquePerWheelNewtonMeters, Is.EqualTo(weaker.DriveTorquePerWheelNewtonMeters));
        }

        [Test]
        public void DisabledControl_PreservesPositiveTorqueDespiteWheelspin()
        {
            var input = CreateInput();
            input.Enabled = false;
            input.LoadedDrivenContacts[0].SlipRatio = 1d;
            AssertBypass(input);
        }

        [Test]
        public void HandbrakeUse_BypassesControlForIntentionalSliding()
        {
            var input = CreateInput();
            input.HandbrakeInput = 0.01d;
            AssertBypass(input);
        }

        [TestCase(0d)]
        [TestCase(-200d)]
        public void NoAccelerationAndEngineBraking_AreUntouched(double requested)
        {
            var input = CreateInput();
            input.RequestedDriveTorquePerWheelNewtonMeters = requested;
            AssertBypass(input);
        }

        [Test]
        public void NoLoadedDrivenContact_DoesNotInventAForceLimit()
        {
            var input = CreateInput();
            input.LoadedDrivenContacts[0].NormalLoadNewtons = 0d;
            AssertBypass(input);
            input.ContactCount = 0;
            AssertBypass(input);
        }

        private static void AssertBypass(RoadTractionControlInput input)
        {
            var result = RoadTractionControl.Evaluate(input);
            Assert.That(result.DriveTorquePerWheelNewtonMeters, Is.EqualTo(input.RequestedDriveTorquePerWheelNewtonMeters));
            Assert.That(result.DeliveredDriveTorqueScale, Is.EqualTo(1d));
            Assert.That(result.Active, Is.False);
        }

        private static RoadTractionControlInput CreateInput()
        {
            return new RoadTractionControlInput
            {
                Enabled = true,
                RequestedDriveTorquePerWheelNewtonMeters = 5000d,
                TireRadiusMeters = 0.305d,
                ContactCount = 1,
                LoadedDrivenContacts = new[]
                {
                    new TireForceInput
                    {
                        NormalLoadNewtons = 3000d,
                        SlipRatio = 0d,
                        SlipAngleRadians = 0d,
                        PeakDryFrictionCoefficient = 1.08d,
                        SlidingGripRatio = 0.9d,
                        SurfaceGripMultiplier = 1d,
                        LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
                        CorneringStiffnessNewtonPerRadian = 65000d,
                        ReferenceLoadNewtons = 3300d,
                        LoadSensitivityExponent = -0.08d
                    }
                }
            };
        }
    }
}
