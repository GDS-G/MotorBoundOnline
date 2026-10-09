using System;

namespace MotorBound.Vehicle.Physics
{
    /// <summary>
    /// Algebraic engine-to-driven-wheel torque conversion. Gear direction controls propulsion;
    /// measured signed carrier rotation controls dissipative engine braking.
    /// This does not integrate engine inertia, an engine clutch or shift-time dynamics.
    /// </summary>
    public static class PowertrainTorqueModel
    {
        public static double CalculateAxleTorque(double driveEngineTorqueNewtonMeters,
            double engineBrakeTorqueNewtonMeters, double signedGearRatio, double finalDriveRatio,
            double efficiency, double signedCarrierAngularSpeedRadiansPerSecond)
        {
            RequireNonnegativeFinite(driveEngineTorqueNewtonMeters, nameof(driveEngineTorqueNewtonMeters));
            RequireNonnegativeFinite(engineBrakeTorqueNewtonMeters, nameof(engineBrakeTorqueNewtonMeters));
            RequireFinite(signedGearRatio, nameof(signedGearRatio));
            RequireFinite(finalDriveRatio, nameof(finalDriveRatio));
            if (finalDriveRatio <= 0d) throw new ArgumentOutOfRangeException(nameof(finalDriveRatio));
            RequireFinite(efficiency, nameof(efficiency));
            if (efficiency <= 0d || efficiency > 1d) throw new ArgumentOutOfRangeException(nameof(efficiency));
            RequireFinite(signedCarrierAngularSpeedRadiansPerSecond, nameof(signedCarrierAngularSpeedRadiansPerSecond));
            if (signedGearRatio == 0d) return 0d;

            var effectiveRatio = signedGearRatio * (finalDriveRatio * efficiency);
            RequireFinite(effectiveRatio, nameof(signedGearRatio));
            var propulsionTorque = driveEngineTorqueNewtonMeters * effectiveRatio;
            RequireFinite(propulsionTorque, nameof(driveEngineTorqueNewtonMeters));
            // At zero carrier rotation there is no signed moving-engine drag. In particular,
            // a launch must not invent a backwards engine-braking impulse from sign(gear).
            var brakeTorque = signedCarrierAngularSpeedRadiansPerSecond == 0d ? 0d
                : Math.Sign(signedCarrierAngularSpeedRadiansPerSecond)
                  * engineBrakeTorqueNewtonMeters * Math.Abs(effectiveRatio);
            RequireFinite(brakeTorque, nameof(engineBrakeTorqueNewtonMeters));
            var axleTorque = propulsionTorque - brakeTorque;
            RequireFinite(axleTorque, nameof(driveEngineTorqueNewtonMeters));
            return axleTorque;
        }

        private static void RequireNonnegativeFinite(double value, string name)
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
