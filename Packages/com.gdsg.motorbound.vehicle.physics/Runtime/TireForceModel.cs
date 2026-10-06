using System;

namespace MotorBound.Vehicle.Physics
{
    public struct TireForceInput
    {
        public double NormalLoadNewtons;
        public double SlipRatio;
        public double SlipAngleRadians;
        public double PeakDryFrictionCoefficient;
        public double SlidingGripRatio;
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
            double effectiveFrictionCoefficient,
            double slipDemandRatio = 0d,
            bool isSliding = false)
        {
            LongitudinalForceNewtons = longitudinalForceNewtons;
            LateralForceNewtons = lateralForceNewtons;
            MaximumCombinedForceNewtons = maximumCombinedForceNewtons;
            EffectiveFrictionCoefficient = effectiveFrictionCoefficient;
            SlipDemandRatio = slipDemandRatio;
            IsSliding = isSliding;
        }

        public double LongitudinalForceNewtons { get; }
        public double LateralForceNewtons { get; }
        public double MaximumCombinedForceNewtons { get; }
        public double EffectiveFrictionCoefficient { get; }
        public double SlipDemandRatio { get; }
        public bool IsSliding { get; }
    }

    /// <summary>
    /// Compact combined-slip prototype. It is deliberately replaceable by a measured-data model.
    /// </summary>
    public static class TireForceModel
    {
        // Keep the established peak location independent of authored sliding grip.
        // An omitted input retains the original curve; vehicle definitions choose their own tail.
        private const double ReferenceSlidingGripRatio = 0.78d;
        private static readonly double ShapeFactor = 2d - 2d * Math.Asin(ReferenceSlidingGripRatio) / Math.PI;
        private static readonly double PeakSlipDemandRatio = ShapeFactor * Math.Tan(Math.PI / (2d * ShapeFactor));

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

            // Saturate the complete slip demand once. Saturating the axes independently
            // let a locked/spinning wheel retain almost full cornering grip.
            var demandMagnitude = Math.Sqrt(requestedLongitudinal * requestedLongitudinal
                                            + requestedLateral * requestedLateral);
            if (demandMagnitude <= double.Epsilon)
            {
                return new TireForceResult(0d, 0d, maximumForce, effectiveMu);
            }

            var slidingRatio = IsFinite(input.SlidingGripRatio) && input.SlidingGripRatio > 0d
                ? Math.Min(input.SlidingGripRatio, 1d) : ReferenceSlidingGripRatio;
            // The reference curve retains unit slope at zero demand and the same finite
            // peak for every tire. Change only the post-peak decline so a more forgiving
            // sliding tail cannot also move the breakaway point or alter ordinary grip.
            var normalizedDemand = demandMagnitude / maximumForce;
            var normalizedForce = Math.Sin(ShapeFactor * Math.Atan(normalizedDemand / ShapeFactor));
            var isSliding = normalizedDemand > PeakSlipDemandRatio;
            if (isSliding && slidingRatio != ReferenceSlidingGripRatio)
            {
                var remainingGrip = Clamp((normalizedForce - ReferenceSlidingGripRatio)
                                          / (1d - ReferenceSlidingGripRatio), 0d, 1d);
                normalizedForce = slidingRatio + (1d - slidingRatio) * remainingGrip;
            }
            var forceMagnitude = maximumForce * normalizedForce;
            var scale = forceMagnitude / demandMagnitude;
            return new TireForceResult(requestedLongitudinal * scale, requestedLateral * scale,
                maximumForce, effectiveMu, normalizedDemand, isSliding);
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
