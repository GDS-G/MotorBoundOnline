using MotorBound.Vehicle.Physics;
using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeInputDriver : MonoBehaviour
    {
        private RaycastVehicleController controller;
        private float smoothedSteering;
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

            if (!DrivingEnabled)
            {
                smoothedSteering = 0f;
                controller.SetInput(new VehicleInputState(0f, 0f, 0f, 0f));
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

            smoothedSteering = Mathf.MoveTowards(smoothedSteering, steeringTarget, Time.deltaTime * 3.5f);
            var throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            var brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            var handbrake = Input.GetKey(KeyCode.Space) ? 1f : 0f;
            controller.SetInput(new VehicleInputState(throttle, brake, smoothedSteering, handbrake));

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                controller.Recover();
            }
        }
    }
}
