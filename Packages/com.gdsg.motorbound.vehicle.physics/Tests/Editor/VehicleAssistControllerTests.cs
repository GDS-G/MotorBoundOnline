using System;
using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class VehicleAssistControllerTests
    {
        private const double Step = 1d / 360d;

        [TestCase(DriverAssistMode.Road, true, DriverAssistPhase.RoadGrip)]
        [TestCase(DriverAssistMode.Sport, true, DriverAssistPhase.RoadGrip)]
        [TestCase(DriverAssistMode.Off, false, DriverAssistPhase.Unassisted)]
        public void Modes_SeparateTorquePolicyFromPhysicalObservations(DriverAssistMode mode, bool limit,
            DriverAssistPhase phase)
        {
            var decision = new VehicleAssistController().Evaluate(mode, Observation(), Step);
            Assert.That(decision.UseRoadTractionControl, Is.EqualTo(limit));
            Assert.That(decision.Phase, Is.EqualTo(phase));
        }

        [TestCase(-1d)]
        [TestCase(1d)]
        public void Sport_OrdinaryHardSteeringAndWheelspinDoNotSilentlyDisableAssistance(double direction)
        {
            var observation = Observation();
            observation.Steering = direction;
            observation.BodySideslipDegrees = direction * 12d;
            observation.HasSlidingDrivenContact = true;
            var decision = new VehicleAssistController().Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(decision.UseRoadTractionControl, Is.True);
            Assert.That(decision.Phase, Is.EqualTo(DriverAssistPhase.RoadGrip));
        }

        [TestCase(-1d)]
        [TestCase(1d)]
        public void Sport_HandbrakeInitiationRequiresPhysicalSlideThenAllowsItsContinuation(double direction)
        {
            var policy = new VehicleAssistController();
            var observation = Observation();
            observation.Steering = direction * 0.12d;
            observation.Handbrake = 1d;
            var initiating = policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(initiating.Phase, Is.EqualTo(DriverAssistPhase.SlideInitiation));
            Assert.That(initiating.UseRoadTractionControl, Is.False);
            observation.Handbrake = 0d;
            observation.BodySideslipDegrees = direction * 10d;
            observation.HasSlidingDrivenContact = true;
            for (var index = 0; index < 360 * 6; index++)
            {
                var sliding = policy.Evaluate(DriverAssistMode.Sport, observation, Step);
                Assert.That(sliding.UseRoadTractionControl, Is.False,
                    "Releasing the handbrake or passing an arbitrary timer must not cancel a confirmed slide.");
                Assert.That(sliding.Phase, Is.EqualTo(DriverAssistPhase.Sliding));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Sport_NoConfirmedSlideExpiresWithoutPermanentAssistanceBypass(bool hasSlidingContact)
        {
            var policy = new VehicleAssistController();
            var observation = Observation();
            observation.Handbrake = 1d;
            observation.Steering = 0.12d;
            policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            observation.Handbrake = 0d;
            // A locked/slipping tire alone does not mean the body entered a slide.
            observation.HasSlidingDrivenContact = hasSlidingContact;
            for (var frame = 0; frame < 360; frame++)
                policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            var decision = policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(decision.UseRoadTractionControl, Is.True);
            Assert.That(decision.Phase, Is.EqualTo(DriverAssistPhase.RoadGrip));
        }

        [Test]
        public void Sport_SideslipAloneDoesNotConfirmDrivenContactSlide()
        {
            var policy = new VehicleAssistController();
            var observation = Observation();
            observation.Handbrake = 1d;
            observation.Steering = 0.12d;
            policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            observation.Handbrake = 0d;
            observation.BodySideslipDegrees = 10d;
            for (var frame = 0; frame < 360; frame++)
                policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
        }

        [Test]
        public void Sport_ThrottleLiftAndCountersteerDoNotTriggerImmediateRoadTorqueCut()
        {
            var policy = CreateSlidingPolicy(out var observation);
            observation.Throttle = 0d;
            observation.Steering = -0.4d;
            var recovery = policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(recovery.Phase, Is.EqualTo(DriverAssistPhase.Recovery));
            Assert.That(recovery.UseRoadTractionControl, Is.False);
            observation.Throttle = 1d;
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).Phase,
                Is.EqualTo(DriverAssistPhase.Sliding));
        }

        [Test]
        public void Sport_RoadControlReturnsOnlyAfterContinuousSettledGroundedRecovery()
        {
            var policy = CreateSlidingPolicy(out var observation);
            observation.BodySideslipDegrees = 1d;
            observation.YawRateDegreesPerSecond = 2d;
            observation.HasSlidingDrivenContact = false;
            for (var frame = 0; frame < 100; frame++)
                Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.False);
            // A renewed lateral/yaw disturbance restarts the hysteresis timer.
            observation.YawRateDegreesPerSecond = 8d;
            policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            observation.YawRateDegreesPerSecond = 2d;
            for (var frame = 0; frame < 100; frame++)
                Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.False);
            for (var frame = 0; frame < 30; frame++)
                policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
        }

        [TestCase(0d, 2)]
        [TestCase(20d, 0)]
        public void Sport_StoppedOrAirborneObservationClearsSlideIntent(double speed, int loadedContacts)
        {
            var policy = CreateSlidingPolicy(out var observation);
            observation.SpeedMetersPerSecond = speed;
            observation.LoadedDrivenContactCount = loadedContacts;
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).Phase,
                Is.EqualTo(DriverAssistPhase.RoadGrip));
            observation = Observation();
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
        }

        [TestCase(DriverAssistMode.Road)]
        [TestCase(DriverAssistMode.Off)]
        public void ModeSwitchAndReset_ClearPendingSlide(DriverAssistMode otherMode)
        {
            var policy = CreateSlidingPolicy(out var observation);
            policy.Evaluate(otherMode, observation, Step);
            observation.Handbrake = 0d;
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
            policy = CreateSlidingPolicy(out observation);
            policy.Reset();
            Assert.That(policy.Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
        }

        [TestCase(4d, 0.12d, 2)]
        [TestCase(20d, 0d, 2)]
        [TestCase(20d, 0.12d, 0)]
        public void Sport_HandbrakeRequiresMovingTurnWithDrivenRoadContact(double speed, double steering, int contacts)
        {
            var observation = Observation();
            observation.SpeedMetersPerSecond = speed;
            observation.Steering = steering;
            observation.LoadedDrivenContactCount = contacts;
            observation.Handbrake = 1d;
            Assert.That(new VehicleAssistController().Evaluate(DriverAssistMode.Sport, observation, Step).UseRoadTractionControl, Is.True);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidTimeStep_IsRejected(double step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VehicleAssistController().Evaluate(DriverAssistMode.Sport, Observation(), step));
        }

        private static VehicleAssistController CreateSlidingPolicy(out VehicleAssistObservation observation)
        {
            var policy = new VehicleAssistController();
            observation = Observation();
            observation.Steering = 0.12d;
            observation.Handbrake = 1d;
            policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            observation.Handbrake = 0d;
            observation.BodySideslipDegrees = 10d;
            observation.YawRateDegreesPerSecond = 20d;
            observation.HasSlidingDrivenContact = true;
            policy.Evaluate(DriverAssistMode.Sport, observation, Step);
            return policy;
        }

        private static VehicleAssistObservation Observation()
        {
            return new VehicleAssistObservation { SpeedMetersPerSecond = 20d, Throttle = 1d, LoadedDrivenContactCount = 2 };
        }
    }
}
