using System;
using NUnit.Framework;
using UnityEngine;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class SuspensionKinematicsTests
    {
        [TestCase(0f)]
        [TestCase(15f)]
        [TestCase(30f)]
        [TestCase(-30f)]
        public void FlatRoadHorizontalTranslation_WithTiltedChassisDoesNotMoveSuspension(float tiltDegrees)
        {
            var up = TiltedUp(tiltDegrees);
            var velocity = new Vector3(31.3f, 0f, 8f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(velocity, Vector3.zero, up, Vector3.up, out var rate), Is.True);
            Assert.That(rate, Is.Zero, "Horizontal road-tangent velocity is not strut extension or compression.");
        }

        [TestCase(0f, 2.5f)]
        [TestCase(30f, 2.5f)]
        [TestCase(-30f, -2.5f)]
        public void ActualHeave_HasRayLengthRateAndCorrectCompressionSign(float tiltDegrees, float verticalSpeed)
        {
            var up = TiltedUp(tiltDegrees);
            var velocity = new Vector3(20f, verticalSpeed, 5f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(velocity, Vector3.zero, up, Vector3.up, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(verticalSpeed / Math.Cos(tiltDegrees * Math.PI / 180d)).Within(0.00001d));
            Assert.That(Math.Sign(rate), Is.EqualTo(Math.Sign(verticalSpeed)));
        }

        [Test]
        public void ChassisRoll_UsesActualContactPointVelocityRatherThanCenterVelocity()
        {
            var centerOfMass = new Vector3(0f, 0.46f, 0f);
            var contactPoint = new Vector3(0.73f, 0f, 0f);
            var angularVelocity = new Vector3(0f, 0f, 1f);
            var chassisContactVelocity = Vector3.Cross(angularVelocity, contactPoint - centerOfMass);
            var up = TiltedUp(30f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(chassisContactVelocity, Vector3.zero, up, Vector3.up, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(0.73d / Math.Cos(Math.PI / 6d)).Within(0.00001d));
            Assert.That(rate, Is.GreaterThan(0f), "This contact extends as the right chassis mount rises.");
        }

        [Test]
        public void MovingGround_UsesRelativeContactPointVelocity()
        {
            var bodyVelocity = new Vector3(8f, -2f, 0f);
            var groundVelocity = new Vector3(2f, -0.5f, 0f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(bodyVelocity, groundVelocity, Vector3.up, Vector3.up, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(-1.5f));
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(bodyVelocity, bodyVelocity, TiltedUp(30f), Vector3.up, out rate), Is.True);
            Assert.That(rate, Is.Zero);
        }

        [Test]
        public void AlignedBankedRoad_DiscardsItsOwnTangentialTravel()
        {
            var normal = new Vector3(0.6f, 0.8f, 0f);
            var tangent = new Vector3(0.8f, -0.6f, 0f) * 30f;
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(tangent, Vector3.zero, normal, normal, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void NonunitAndLargeFiniteAxes_AreNormalizedBeforeCalculatingLengthRate()
        {
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(Vector3.up * 2f, Vector3.zero, Vector3.up * float.MaxValue,
                Vector3.up * 7f, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(2f));
        }

        [TestCase(0.05f)]
        [TestCase(0f)]
        [TestCase(-0.05f)]
        [TestCase(-1f)]
        public void GrazingAndOpposedContactAxes_ReturnUnsupportedZeroRate(float alignment)
        {
            var up = new Vector3((float)Math.Sqrt(Math.Max(0d, 1d - alignment * alignment)), alignment, 0f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(Vector3.up * 3f, Vector3.zero, up, Vector3.up, out var rate), Is.False);
            Assert.That(rate, Is.Zero);
        }

        [Test]
        public void ReasonablyAlignedContact_IsSupportedWithoutClampingItsPhysicalRate()
        {
            var up = new Vector3((float)Math.Sqrt(1d - 0.2d * 0.2d), 0.2f, 0f);
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(Vector3.up * 3f, Vector3.zero, up, Vector3.up, out var rate), Is.True);
            Assert.That(rate, Is.EqualTo(15f).Within(0.00001f));
        }

        [Test]
        public void MissingOrNonfiniteGeometryAndVelocity_ReturnSafeUnsupportedZeroRate()
        {
            AssertUnsupported(Vector3.up, Vector3.zero, Vector3.zero, Vector3.up);
            AssertUnsupported(Vector3.up, Vector3.zero, Vector3.up, Vector3.zero);
            AssertUnsupported(Vector3.up, Vector3.zero, Vector3.up * 0.00000001f, Vector3.up);
            AssertUnsupported(new Vector3(float.NaN, 0f, 0f), Vector3.zero, Vector3.up, Vector3.up);
            AssertUnsupported(Vector3.up, new Vector3(0f, float.PositiveInfinity, 0f), Vector3.up, Vector3.up);
            AssertUnsupported(Vector3.up, Vector3.zero, Vector3.up, new Vector3(float.NaN, 1f, 0f));
        }

        [Test]
        public void UnrepresentableLengthRate_ReturnsSafeUnsupportedZeroInsteadOfInfinity()
        {
            AssertUnsupported(Vector3.up * float.MaxValue, Vector3.down * float.MaxValue, TiltedUp(30f), Vector3.up);
        }

        [TestCase(-3f)]
        [TestCase(0f)]
        [TestCase(3f)]
        public void NormalDamperForceOpposingLengthRate_CannotAddMechanicalPower(float heaveSpeed)
        {
            var velocity = new Vector3(31.3f, heaveSpeed, 0f);
            var normal = Vector3.up;
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(velocity, Vector3.zero, TiltedUp(30f), normal, out var rate), Is.True);
            const float dampingCoefficient = 4300f;
            var damperForce = normal * (-dampingCoefficient * rate);
            Assert.That(Vector3.Dot(damperForce, velocity), Is.LessThanOrEqualTo(0f));
        }

        private static Vector3 TiltedUp(float degrees)
        {
            var radians = degrees * Math.PI / 180d;
            return new Vector3((float)-Math.Sin(radians), (float)Math.Cos(radians), 0f);
        }

        private static void AssertUnsupported(Vector3 bodyVelocity, Vector3 groundVelocity, Vector3 up, Vector3 normal)
        {
            Assert.That(SuspensionKinematics.TryCalculateLengthRate(bodyVelocity, groundVelocity, up, normal, out var rate), Is.False);
            Assert.That(rate, Is.Zero);
        }
    }
}
