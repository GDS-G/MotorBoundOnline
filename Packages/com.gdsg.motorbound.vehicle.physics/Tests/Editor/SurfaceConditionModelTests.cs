using NUnit.Framework;

namespace MotorBound.Vehicle.Physics.Tests
{
    public sealed class SurfaceConditionModelTests
    {
        [Test]
        public void StandingWaterAtSpeed_ReducesEffectiveGrip()
        {
            var dry = SurfaceConditionModel.Evaluate(CreateInput(0d, 30d, 0.5d));
            var standingWater = SurfaceConditionModel.Evaluate(CreateInput(4d, 30d, 0.5d));

            Assert.That(dry.WaterState, Is.EqualTo(RoadSurfaceWaterState.Dry));
            Assert.That(standingWater.WaterState, Is.EqualTo(RoadSurfaceWaterState.StandingWater));
            Assert.That(standingWater.EffectiveGripMultiplier, Is.LessThan(dry.EffectiveGripMultiplier));
        }

        [Test]
        public void BetterWaterEvacuation_PreservesMoreGrip()
        {
            var wornTread = SurfaceConditionModel.Evaluate(CreateInput(3d, 28d, 0.1d));
            var effectiveTread = SurfaceConditionModel.Evaluate(CreateInput(3d, 28d, 0.9d));

            Assert.That(effectiveTread.EffectiveGripMultiplier, Is.GreaterThan(wornTread.EffectiveGripMultiplier));
        }

        private static SurfaceConditionInput CreateInput(double depthMillimeters, double speedMetersPerSecond, double evacuation)
        {
            return new SurfaceConditionInput
            {
                BaseGripMultiplier = 1d,
                WaterFilmDepthMillimeters = depthMillimeters,
                VehicleSpeedMetersPerSecond = speedMetersPerSecond,
                TireWaterEvacuationFactor = evacuation,
                RoughnessFactor = 0d
            };
        }
    }
}
