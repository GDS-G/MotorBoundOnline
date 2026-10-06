using System;

namespace MotorBound.Vehicle.Physics
{
    public struct RoadTractionControlInput
    {
        public bool Enabled;
        public double RequestedDriveTorquePerWheelNewtonMeters;
        public double HandbrakeInput;
        public double TireRadiusMeters;
        public TireForceInput[] LoadedDrivenContacts;
        public int ContactCount;
    }

    public struct RoadTractionControlResult
    {
        public double DriveTorquePerWheelNewtonMeters;
        public double DeliveredDriveTorqueScale;
        public bool Active;
    }

    /// <summary>Prototype Road-mode engine torque actuator; it never applies steering or chassis forces.</summary>
    public static class RoadTractionControl
    {
        public const double TargetSlipRatio = 0.045d;
        private const double CorneringTargetSlipRatio = 0.02d;
        private const double MinimumLoadedContactNewtons = 20d;

        public static double EvaluateTargetSlipRatio(double slipAngleRadians)
        {
            if (double.IsNaN(slipAngleRadians)) throw new ArgumentOutOfRangeException(nameof(slipAngleRadians));
            // Reserve more lateral grip during a corner without limiting straight-line
            // acceleration. Smooth endpoints avoid an abrupt torque change at either threshold.
            var angleDegrees = Math.Abs(slipAngleRadians) * 180d / Math.PI;
            var blend = Math.Max(0d, Math.Min(1d, (angleDegrees - 0.1d) / 0.9d));
            blend = blend * blend * (3d - 2d * blend);
            return TargetSlipRatio + (CorneringTargetSlipRatio - TargetSlipRatio) * blend;
        }

        public static RoadTractionControlResult Evaluate(RoadTractionControlInput input)
        {
            var requested = input.RequestedDriveTorquePerWheelNewtonMeters;
            if (double.IsNaN(requested) || double.IsInfinity(requested))
                throw new ArgumentOutOfRangeException(nameof(input.RequestedDriveTorquePerWheelNewtonMeters));
            var result = new RoadTractionControlResult
            {
                DriveTorquePerWheelNewtonMeters = requested,
                DeliveredDriveTorqueScale = 1d
            };
            // Engine braking is untouched, and handbrake use leaves intentional slide control to the driver.
            if (!input.Enabled || requested <= 0d || input.HandbrakeInput > 0d
                || input.LoadedDrivenContacts == null || input.ContactCount <= 0) return result;
            if (double.IsNaN(input.TireRadiusMeters) || double.IsInfinity(input.TireRadiusMeters)
                || input.TireRadiusMeters <= 0d) throw new ArgumentOutOfRangeException(nameof(input.TireRadiusMeters));

            var commonTorqueLimit = requested;
            var count = Math.Min(input.ContactCount, input.LoadedDrivenContacts.Length);
            for (var index = 0; index < count; index++)
            {
                var measured = input.LoadedDrivenContacts[index];
                if (!(measured.NormalLoadNewtons > MinimumLoadedContactNewtons)) continue;
                var targetSlip = EvaluateTargetSlipRatio(measured.SlipAngleRadians);
                var targetState = measured;
                targetState.SlipRatio = targetSlip;
                // Measured lateral slip consumes the same combined grip budget as propulsion.
                var capacity = TireForceModel.Evaluate(targetState);
                var torqueLimit = Math.Max(0d, capacity.LongitudinalForceNewtons) * input.TireRadiusMeters;
                // Cut additional engine torque during positive wheelspin, allowing tire
                // reaction torque to slow the wheel physically. Braking slip does not request a cut.
                if (measured.SlipRatio > targetSlip)
                    torqueLimit *= targetSlip / measured.SlipRatio;
                commonTorqueLimit = Math.Min(commonTorqueLimit, torqueLimit);
            }

            result.DriveTorquePerWheelNewtonMeters = commonTorqueLimit;
            result.DeliveredDriveTorqueScale = Math.Max(0d, Math.Min(1d, commonTorqueLimit / requested));
            result.Active = commonTorqueLimit < requested - 0.000001d;
            return result;
        }
    }
}
