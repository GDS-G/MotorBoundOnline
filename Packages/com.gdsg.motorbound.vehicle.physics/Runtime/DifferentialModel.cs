using System;
using MotorBound.Vehicle.Core;

namespace MotorBound.Vehicle.Physics
{
    public struct DifferentialInput
    {
        public DifferentialDefinition Definition;
        public double AxleDriveTorqueNewtonMeters;
        public double LeftAngularSpeedRadiansPerSecond;
        public double RightAngularSpeedRadiansPerSecond;
        public double LeftInertiaKilogramMetersSquared;
        public double RightInertiaKilogramMetersSquared;
        public double DeltaTimeSeconds;
    }

    public struct DifferentialResult
    {
        // Positive transfers momentum from left to right. Equal/opposite impulses cannot add axle momentum.
        public double TransferAngularImpulseNewtonMeterSeconds;
        public double TransferTorqueNewtonMeters;
        public double DissipatedEnergyJoules;
    }

    /// <summary>Passive friction-clutch axle, bounded by both clutch capacity and equalization impulse.</summary>
    public static class DifferentialModel
    {
        public static DifferentialResult Evaluate(DifferentialInput input)
        {
            if (input.Definition == null) throw new ArgumentNullException(nameof(input.Definition));
            RequirePositive(input.LeftInertiaKilogramMetersSquared, nameof(input.LeftInertiaKilogramMetersSquared));
            RequirePositive(input.RightInertiaKilogramMetersSquared, nameof(input.RightInertiaKilogramMetersSquared));
            RequirePositive(input.DeltaTimeSeconds, nameof(input.DeltaTimeSeconds));
            RequireFinite(input.AxleDriveTorqueNewtonMeters, nameof(input.AxleDriveTorqueNewtonMeters));
            RequireFinite(input.LeftAngularSpeedRadiansPerSecond, nameof(input.LeftAngularSpeedRadiansPerSecond));
            RequireFinite(input.RightAngularSpeedRadiansPerSecond, nameof(input.RightAngularSpeedRadiansPerSecond));
            if (input.Definition.Type == DifferentialType.Open) return default(DifferentialResult);
            if (input.Definition.Type != DifferentialType.ClutchLimitedSlip) throw new ArgumentOutOfRangeException(nameof(input.Definition.Type));
            var definition = input.Definition;
            RequireNonnegative(definition.PreloadTorqueNewtonMeters, nameof(definition.PreloadTorqueNewtonMeters));
            RequireNonnegative(definition.PowerLockFraction, nameof(definition.PowerLockFraction));
            RequireNonnegative(definition.CoastLockFraction, nameof(definition.CoastLockFraction));
            RequireNonnegative(definition.SlipSpeedGainNewtonMeterSecondsPerRadian, nameof(definition.SlipSpeedGainNewtonMeterSecondsPerRadian));
            var difference = input.LeftAngularSpeedRadiansPerSecond - input.RightAngularSpeedRadiansPerSecond;
            var carrierSpeed = (input.LeftAngularSpeedRadiansPerSecond + input.RightAngularSpeedRadiansPerSecond) * 0.5d;
            // Classify power/coast by mechanical work, including backwards rolling.
            var lockFraction = input.AxleDriveTorqueNewtonMeters * carrierSpeed >= 0d
                ? definition.PowerLockFraction : definition.CoastLockFraction;
            var capacity = definition.PreloadTorqueNewtonMeters
                           + lockFraction * Math.Abs(input.AxleDriveTorqueNewtonMeters)
                           + definition.SlipSpeedGainNewtonMeterSecondsPerRadian * Math.Abs(difference);
            var inverseInertiaSum = 1d / input.LeftInertiaKilogramMetersSquared + 1d / input.RightInertiaKilogramMetersSquared;
            var impulse = Math.Sign(difference) * Math.Min(capacity * input.DeltaTimeSeconds, Math.Abs(difference) / inverseInertiaSum);
            var dissipated = impulse * difference - 0.5d * impulse * impulse * inverseInertiaSum;
            return new DifferentialResult
            {
                TransferAngularImpulseNewtonMeterSeconds = impulse,
                TransferTorqueNewtonMeters = impulse / input.DeltaTimeSeconds,
                DissipatedEnergyJoules = Math.Max(0d, dissipated)
            };
        }

        private static void RequirePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0d) throw new ArgumentOutOfRangeException(name);
        }

        private static void RequireNonnegative(double value, string name)
        {
            RequireFinite(value, name);
            if (value < 0d) throw new ArgumentOutOfRangeException(name);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
