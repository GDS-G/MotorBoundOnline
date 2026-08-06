using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    public struct VehicleInputState
    {
        public VehicleInputState(float throttle, float brake, float steering, float handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            Steering = Mathf.Clamp(steering, -1f, 1f);
            Handbrake = Mathf.Clamp01(handbrake);
        }

        public float Throttle { get; }
        public float Brake { get; }
        public float Steering { get; }
        public float Handbrake { get; }
    }
}
