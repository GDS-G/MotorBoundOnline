using System;

namespace MotorBound.Foundation
{
    public static class UnitConversion
    {
        public const double MetersPerSecondToKilometersPerHour = 3.6d;
        public const double MetersPerSecondToMilesPerHour = 2.2369362920544d;
        public const double RevolutionsPerMinuteToRadiansPerSecond = Math.PI / 30d;
        public const double RadiansPerSecondToRevolutionsPerMinute = 30d / Math.PI;
        public const double StandardGravityMetersPerSecondSquared = 9.80665d;

        public static double ToKilometersPerHour(double metersPerSecond)
        {
            return metersPerSecond * MetersPerSecondToKilometersPerHour;
        }

        public static double ToMilesPerHour(double metersPerSecond)
        {
            return metersPerSecond * MetersPerSecondToMilesPerHour;
        }

        public static double RpmToRadiansPerSecond(double rpm)
        {
            return rpm * RevolutionsPerMinuteToRadiansPerSecond;
        }

        public static double RadiansPerSecondToRpm(double radiansPerSecond)
        {
            return radiansPerSecond * RadiansPerSecondToRevolutionsPerMinute;
        }
    }
}
