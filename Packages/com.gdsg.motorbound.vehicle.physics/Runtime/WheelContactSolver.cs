using System;

namespace MotorBound.Vehicle.Physics
{
    public struct WheelContactInput
    {
        public TireForceInput Tire;
        public double AngularSpeedRadiansPerSecond;
        public double LongitudinalSpeedMetersPerSecond;
        public double RadiusMeters;
        public double RotationalInertiaKilogramMetersSquared;
        public double DriveTorqueNewtonMeters;
        public double BrakeTorqueNewtonMeters;
        public double RollingResistanceCoefficient;
        public double DeltaTimeSeconds;
    }

    public struct WheelContactResult
    {
        public double AngularSpeedRadiansPerSecond;
        public double MeanLongitudinalForceNewtons;
        public double MeanLateralForceNewtons;
        public double SlipRatio;
        public TireForceResult FinalTireState;
        public int SubstepCount;
        public double BrakeAngularImpulseNewtonMeterSeconds;
        public double AngularLimitImpulseNewtonMeterSeconds;
    }

    /// <summary>Wheel rotational/contact integration with the chassis contact held fixed for one physics step.</summary>
    public static class WheelContactSolver
    {
        public const double MinimumSlipReferenceSpeedMetersPerSecond = 1.5d;
        private const double MaximumLinearFeedbackGain = 0.5d;
        private const double MaximumAngularSpeedRadiansPerSecond = 1000d;
        private const int MaximumSubsteps = 64;

        public static WheelContactResult Solve(WheelContactInput input)
        {
            ValidateInput(input);
            var substeps = CalculateSubstepCount(input);
            var stepSeconds = input.DeltaTimeSeconds / substeps;
            var angularSpeed = input.AngularSpeedRadiansPerSecond;
            var longitudinalSum = 0d;
            var lateralSum = 0d;
            var brakeImpulse = 0d;
            var limitImpulse = 0d;

            for (var step = 0; step < substeps; step++)
            {
                var force = EvaluateDeliveredForce(input, angularSpeed);
                var withoutBrake = angularSpeed
                    + (input.DriveTorqueNewtonMeters - force.LongitudinalForceNewtons * input.RadiusMeters)
                    * stepSeconds / input.RotationalInertiaKilogramMetersSquared;
                angularSpeed = ApplyBrakeAndLimit(input, withoutBrake, stepSeconds, ref brakeImpulse, ref limitImpulse);
                longitudinalSum += force.LongitudinalForceNewtons;
                lateralSum += force.LateralForceNewtons;
            }

            return BuildResult(input, angularSpeed, longitudinalSum, lateralSum, substeps, brakeImpulse, limitImpulse);
        }

        internal static void ValidateInput(WheelContactInput input)
        {
            RequirePositiveFinite(input.DeltaTimeSeconds, nameof(input.DeltaTimeSeconds));
            RequirePositiveFinite(input.RadiusMeters, nameof(input.RadiusMeters));
            RequirePositiveFinite(input.RotationalInertiaKilogramMetersSquared, nameof(input.RotationalInertiaKilogramMetersSquared));
            RequireFinite(input.AngularSpeedRadiansPerSecond, nameof(input.AngularSpeedRadiansPerSecond));
            RequireFinite(input.LongitudinalSpeedMetersPerSecond, nameof(input.LongitudinalSpeedMetersPerSecond));
            RequireFinite(input.DriveTorqueNewtonMeters, nameof(input.DriveTorqueNewtonMeters));
            RequireFinite(input.BrakeTorqueNewtonMeters, nameof(input.BrakeTorqueNewtonMeters));
            RequireFinite(input.RollingResistanceCoefficient, nameof(input.RollingResistanceCoefficient));
            RequireFinite(input.Tire.LongitudinalSlipStiffnessNewtonPerRatio, nameof(input.Tire.LongitudinalSlipStiffnessNewtonPerRatio));
        }

        internal static int CalculateSubstepCount(WheelContactInput input)
        {
            var referenceSpeed = Math.Max(Math.Abs(input.LongitudinalSpeedMetersPerSecond), MinimumSlipReferenceSpeedMetersPerSecond);
            // Linearization of r*Fx(omega) gives Ck*r^2/(I*Vref). A single explicit
            // 360 Hz step has gain about 12 at low speed for the reference wheels.
            // Keep gain <= 0.5 rather than manufacturing chassis damping or tire grip.
            var feedbackGain = input.Tire.NormalLoadNewtons > 0d
                ? input.DeltaTimeSeconds * Math.Max(0d, input.Tire.LongitudinalSlipStiffnessNewtonPerRatio)
                  * input.RadiusMeters * input.RadiusMeters
                  / (input.RotationalInertiaKilogramMetersSquared * referenceSpeed)
                : 0d;
            // The authored stock/touring wheels need at most 24 substeps at 360 Hz.
            // Bound work for unsupported extreme inputs; this is not an arbitrary-dt solver.
            return (int)Math.Max(1d, Math.Min(MaximumSubsteps, Math.Ceiling(feedbackGain / MaximumLinearFeedbackGain)));
        }

        internal static TireForceResult EvaluateDeliveredForce(WheelContactInput input, double angularSpeed)
        {
            var referenceSpeed = Math.Max(Math.Abs(input.LongitudinalSpeedMetersPerSecond), MinimumSlipReferenceSpeedMetersPerSecond);
            var tireInput = input.Tire;
            tireInput.SlipRatio = (angularSpeed * input.RadiusMeters - input.LongitudinalSpeedMetersPerSecond) / referenceSpeed;
            var force = TireForceModel.Evaluate(tireInput);
            var longitudinal = force.LongitudinalForceNewtons;
            if (Math.Abs(input.LongitudinalSpeedMetersPerSecond) > 0.25d)
                longitudinal -= Math.Sign(input.LongitudinalSpeedMetersPerSecond)
                                * Math.Max(0d, input.RollingResistanceCoefficient) * Math.Max(0d, tireInput.NormalLoadNewtons);
            var lateral = force.LateralForceNewtons;
            var magnitude = Math.Sqrt(longitudinal * longitudinal + lateral * lateral);
            if (magnitude > force.MaximumCombinedForceNewtons && magnitude > 0d)
            {
                var scale = force.MaximumCombinedForceNewtons / magnitude;
                longitudinal *= scale;
                lateral *= scale;
            }

            return new TireForceResult(longitudinal, lateral, force.MaximumCombinedForceNewtons,
                force.EffectiveFrictionCoefficient, force.SlipDemandRatio, force.IsSliding);
        }

        internal static double ApplyBrakeAndLimit(WheelContactInput input, double withoutBrake, double stepSeconds,
            ref double brakeImpulse, ref double limitImpulse)
        {
            var brakeSpeedChange = Math.Max(0d, input.BrakeTorqueNewtonMeters)
                * stepSeconds / input.RotationalInertiaKilogramMetersSquared;
            var afterBrake = Math.Sign(withoutBrake) * Math.Max(0d, Math.Abs(withoutBrake) - brakeSpeedChange);
            // A static brake may hold omega at zero, but it cannot rotate the
            // wheel backwards. This also avoids sign chatter during handbrake lock.
            brakeImpulse += (withoutBrake - afterBrake) * input.RotationalInertiaKilogramMetersSquared;
            var limitedSpeed = Math.Max(-MaximumAngularSpeedRadiansPerSecond,
                Math.Min(MaximumAngularSpeedRadiansPerSecond, afterBrake));
            limitImpulse += (limitedSpeed - afterBrake) * input.RotationalInertiaKilogramMetersSquared;
            return limitedSpeed;
        }

        internal static WheelContactResult BuildResult(WheelContactInput input, double angularSpeed,
            double longitudinalSum, double lateralSum, int substeps, double brakeImpulse, double limitImpulse)
        {
            var referenceSpeed = Math.Max(Math.Abs(input.LongitudinalSpeedMetersPerSecond), MinimumSlipReferenceSpeedMetersPerSecond);
            var tireInput = input.Tire;
            tireInput.SlipRatio = (angularSpeed * input.RadiusMeters - input.LongitudinalSpeedMetersPerSecond) / referenceSpeed;
            return new WheelContactResult
            {
                AngularSpeedRadiansPerSecond = angularSpeed,
                MeanLongitudinalForceNewtons = longitudinalSum / substeps,
                MeanLateralForceNewtons = lateralSum / substeps,
                SlipRatio = tireInput.SlipRatio,
                FinalTireState = TireForceModel.Evaluate(tireInput),
                SubstepCount = substeps,
                BrakeAngularImpulseNewtonMeterSeconds = brakeImpulse,
                AngularLimitImpulseNewtonMeterSeconds = limitImpulse
            };
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }

        private static void RequirePositiveFinite(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0d) throw new ArgumentOutOfRangeException(name);
        }
    }
}
