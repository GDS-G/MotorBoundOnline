using MotorBound.Vehicle.Physics;
using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeInputDriver : MonoBehaviour
    {
        private RaycastVehicleController controller;
        private float smoothedSteering;
        private const float SteeringPressRate = 3.5f;
        // Keep key presses progressive, but stop holding a turn after the player releases it.
        private const float SteeringCenterRate = 12f;
        public float SteeringInput => smoothedSteering;
        public bool DrivingEnabled { get; set; } = true;

        public void Configure(RaycastVehicleController target)
        {
            controller = target;
        }

        private void Awake()
        {
            controller = controller != null ? controller : GetComponent<RaycastVehicleController>();
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            var steeringTarget = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                steeringTarget -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                steeringTarget += 1f;
            }

            var throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            var brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            var handbrake = Input.GetKey(KeyCode.Space) ? 1f : 0f;
            ApplyInput(new VehicleInputState(throttle, brake, steeringTarget, handbrake), Time.deltaTime);

            if (DrivingEnabled && Input.GetKeyDown(KeyCode.Backspace))
            {
                controller.Recover();
            }
        }

        // The live keyboard path and native driving checks share the same response.
        public void ApplyInput(VehicleInputState requestedInput, float deltaTimeSeconds)
        {
            if (controller == null) return;
            if (!DrivingEnabled)
            {
                smoothedSteering = 0f;
                controller.SetInput(default(VehicleInputState));
                return;
            }

            var rate = requestedInput.Steering == 0f ? SteeringCenterRate : SteeringPressRate;
            smoothedSteering = Mathf.MoveTowards(smoothedSteering, requestedInput.Steering,
                Mathf.Max(0f, deltaTimeSeconds) * rate);
            if (requestedInput.Steering == 0f && Mathf.Abs(smoothedSteering) < 0.0001f)
                smoothedSteering = 0f;
            controller.SetInput(new VehicleInputState(requestedInput.Throttle, requestedInput.Brake,
                smoothedSteering, requestedInput.Handbrake));
        }
    }
}
