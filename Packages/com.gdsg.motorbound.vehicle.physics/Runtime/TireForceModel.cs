using System;

namespace MotorBound.Vehicle.Physics
{
    public struct TireForceInput
    {
        public double NormalLoadNewtons;
        public double SlipRatio;
        public double SlipAngleRadians;
        public double PeakDryFrictionCoefficient;
        public double SurfaceGripMultiplier;
        public double LongitudinalSlipStiffnessNewtonPerRatio;
        public double CorneringStiffnessNewtonPerRadian;
        public double ReferenceLoadNewtons;
        public double LoadSensitivityExponent;
    }

    public struct TireForceResult
    {
        public TireForceResult(
            double longitudinalForceNewtons,
            double lateralForceNewtons,
            double maximumCombinedForceNewtons,
            double effectiveFrictionCoefficient)
        {
            LongitudinalForceNewtons = longitudinalForceNewtons;
            LateralForceNewtons = lateralForceNewtons;
            MaximumCombinedForceNewtons = maximumCombinedForceNewtons;
            EffectiveFrictionCoefficient = effectiveFrictionCoefficient;
        }

        public double LongitudinalForceNewtons { get; }
        public double LateralForceNewtons { get; }
        public double MaximumCombinedForceNewtons { get; }
        public double EffectiveFrictionCoefficient { get; }
    }

    /// <summary>
    /// Compact combined-slip prototype. It is deliberately replaceable by a measured-data model.
    /// </summary>
    public static class TireForceModel
    {
        public static TireForceResult Evaluate(TireForceInput input)
        {
            if (!IsFinite(input.NormalLoadNewtons) || input.NormalLoadNewtons <= 0d)
            {
                return new TireForceResult(0d, 0d, 0d, 0d);
            }

            var referenceLoad = Math.Max(input.ReferenceLoadNewtons, 1d);
            var loadRatio = Math.Max(input.NormalLoadNewtons / referenceLoad, 0.05d);
            var effectiveMu = Math.Max(0d, input.PeakDryFrictionCoefficient)
                              * Math.Max(0d, input.SurfaceGripMultiplier)
                              * Math.Pow(loadRatio, input.LoadSensitivityExponent);
            var maximumForce = effectiveMu * input.NormalLoadNewtons;
            if (maximumForce <= double.Epsilon)
            {
                return new TireForceResult(0d, 0d, 0d, effectiveMu);
            }

            var slipRatio = Clamp(input.SlipRatio, -4d, 4d);
            var slipAngle = Clamp(input.SlipAngleRadians, -Math.PI * 0.49d, Math.PI * 0.49d);
            var requestedLongitudinal = Math.Max(0d, input.LongitudinalSlipStiffnessNewtonPerRatio) * slipRatio;
            var requestedLateral = -Math.Max(0d, input.CorneringStiffnessNewtonPerRadian) * slipAngle;

            // Smoothly approach peak force in either direction before enforcing the combined envelope.
            var longitudinal = maximumForce * Math.Tanh(requestedLongitudinal / maximumForce);
            var lateral = maximumForce * Math.Tanh(requestedLateral / maximumForce);
            var normalizedMagnitudeSquared = ((longitudinal * longitudinal) + (lateral * lateral)) / (maximumForce * maximumForce);
            if (normalizedMagnitudeSquared > 1d)
            {
                var scale = 1d / Math.Sqrt(normalizedMagnitudeSquared);
                longitudinal *= scale;
                lateral *= scale;
            }

            return new TireForceResult(longitudinal, lateral, maximumForce, effectiveMu);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
