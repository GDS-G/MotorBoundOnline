using NUnit.Framework;

namespace MotorBound.Foundation.Tests
{
    public sealed class UnitConversionTests
    {
        [TestCase(10d, 36d)]
        [TestCase(27.7777777778d, 100d)]
        public void MetersPerSecond_ConvertsToKilometersPerHour(double metersPerSecond, double expected)
        {
            Assert.That(UnitConversion.ToKilometersPerHour(metersPerSecond), Is.EqualTo(expected).Within(0.000001d));
        }

        [Test]
        public void AngularSpeed_RoundTripsRpm()
        {
            const double sourceRpm = 6500d;
            var angularSpeed = UnitConversion.RpmToRadiansPerSecond(sourceRpm);

            Assert.That(UnitConversion.RadiansPerSecondToRpm(angularSpeed), Is.EqualTo(sourceRpm).Within(0.000001d));
        }
    }
}
