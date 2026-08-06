using System;

namespace MotorBound.Vehicle.Physics
{
    public enum RoadSurfaceWaterState
    {
        Dry = 0,
        Damp = 1,
        WaterFilm = 2,
        StandingWater = 3
    }

    [Serializable]
    public struct SurfaceConditionInput
    {
        public double BaseGripMultiplier;
        public double WaterFilmDepthMillimeters;
        public double VehicleSpeedMetersPerSecond;
        public double TireWaterEvacuationFactor;
        public double RoughnessFactor;
    }

    [Serializable]
    public struct SurfaceConditionResult
    {
        public RoadSurfaceWaterState WaterState;
        public double EffectiveGripMultiplier;
        public double WaterSeverity;
    }

    /// <summary>
    /// A compact first-pass road-state layer. It deliberately exposes water depth and
    /// roughness separately so later measured tire and hydrology models can replace it.
    /// </summary>
    public static class SurfaceConditionModel
    {
        public static SurfaceConditionResult Evaluate(SurfaceConditionInput input)
        {
            var baseGrip = Math.Max(0d, input.BaseGripMultiplier);
            var waterDepth = Math.Max(0d, input.WaterFilmDepthMillimeters);
            var speed = Math.Max(0d, input.VehicleSpeedMetersPerSecond);
            var evacuation = Clamp01(input.TireWaterEvacuationFactor);
            var roughness = Clamp01(input.RoughnessFactor);

            var depthFactor = Clamp01(waterDepth / 4d);
            var speedFactor = Clamp01((speed - 3d) / 27d);
            var treadProtection = 0.35d + (0.65d * evacuation);
            var waterSeverity = depthFactor * speedFactor * (1d - (0.55d * treadProtection));
            var waterGripScale = 1d - (0.52d * waterSeverity);
            var roughnessGripScale = 1d - (0.08d * roughness);

            return new SurfaceConditionResult
            {
                WaterState = ClassifyWater(waterDepth),
                EffectiveGripMultiplier = Math.Max(0d, baseGrip * waterGripScale * roughnessGripScale),
                WaterSeverity = waterSeverity
            };
        }

        private static RoadSurfaceWaterState ClassifyWater(double depthMillimeters)
        {
            if (depthMillimeters <= 0.01d)
            {
                return RoadSurfaceWaterState.Dry;
            }

            if (depthMillimeters < 0.5d)
            {
                return RoadSurfaceWaterState.Damp;
            }

            return depthMillimeters < 3d
                ? RoadSurfaceWaterState.WaterFilm
                : RoadSurfaceWaterState.StandingWater;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0d, Math.Min(1d, value));
        }
    }
}
