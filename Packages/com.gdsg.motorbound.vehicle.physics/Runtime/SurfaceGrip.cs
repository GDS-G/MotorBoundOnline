using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    [DisallowMultipleComponent]
    public sealed class SurfaceGrip : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float gripMultiplier = 1f;
        [SerializeField] private string surfaceName = "Dry asphalt";

        public float GripMultiplier => gripMultiplier;
        public string SurfaceName => string.IsNullOrWhiteSpace(surfaceName) ? gameObject.name : surfaceName;

        public void Configure(float multiplier, string displayName)
        {
            gripMultiplier = Mathf.Max(0f, multiplier);
            surfaceName = displayName ?? string.Empty;
        }
    }
}
