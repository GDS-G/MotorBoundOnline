using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    [DisallowMultipleComponent]
    public sealed class SurfaceGrip : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float gripMultiplier = 1f;
        [SerializeField] private string surfaceName = "Dry asphalt";
        [SerializeField, Min(0f)] private float waterFilmDepthMillimeters;
        [SerializeField, Range(0f, 1f)] private float roughnessFactor;

        public float GripMultiplier => gripMultiplier;
        public string SurfaceName => string.IsNullOrWhiteSpace(surfaceName) ? gameObject.name : surfaceName;
        public float WaterFilmDepthMillimeters => waterFilmDepthMillimeters;
        public float RoughnessFactor => roughnessFactor;

        public void Configure(float multiplier, string displayName)
        {
            Configure(multiplier, displayName, 0f, 0f);
        }

        public void Configure(float multiplier, string displayName, float waterDepthMillimeters, float roughness)
        {
            gripMultiplier = Mathf.Max(0f, multiplier);
            surfaceName = displayName ?? string.Empty;
            waterFilmDepthMillimeters = Mathf.Max(0f, waterDepthMillimeters);
            roughnessFactor = Mathf.Clamp01(roughness);
        }

        public SurfaceConditionResult Evaluate(float speedMetersPerSecond, float tireWaterEvacuationFactor)
        {
            return SurfaceConditionModel.Evaluate(new SurfaceConditionInput
            {
                BaseGripMultiplier = gripMultiplier,
                WaterFilmDepthMillimeters = waterFilmDepthMillimeters,
                VehicleSpeedMetersPerSecond = speedMetersPerSecond,
                TireWaterEvacuationFactor = tireWaterEvacuationFactor,
                RoughnessFactor = roughnessFactor
            });
        }
    }
}
