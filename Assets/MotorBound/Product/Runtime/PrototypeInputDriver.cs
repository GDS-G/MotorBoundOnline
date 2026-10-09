using MotorBound.Vehicle.Physics;
using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeInputDriver : MonoBehaviour
    {
        private RaycastVehicleController controller;
        private float smoothedSteering;
        // A short digital key tap should be a correction, not near-instant full lock.
        // Held input still reaches the complete authored range in half a second at every speed.
        private const float SteeringPressRate = 2f;
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
            if (DrivingEnabled && Input.GetKeyDown(KeyCode.F2)) CycleDriverAssistMode();
            if (Input.GetKeyDown(KeyCode.E)) ShiftUp();
            if (Input.GetKeyDown(KeyCode.Q)) ShiftDown();
            if (Input.GetKeyDown(KeyCode.R)) ToggleReverse();
            if (Input.GetKeyDown(KeyCode.M)) ToggleTransmissionMode();
            ApplyInput(new VehicleInputState(throttle, brake, steeringTarget, handbrake), Time.deltaTime);

            if (DrivingEnabled && Input.GetKeyDown(KeyCode.Backspace))
            {
                controller.Recover();
            }
        }

        public void ToggleRoadTractionControl()
        {
            if (controller != null && DrivingEnabled)
                controller.RoadTractionControlEnabled = !controller.RoadTractionControlEnabled;
        }

        public void CycleDriverAssistMode()
        {
            if (controller == null || !DrivingEnabled) return;
            controller.AssistMode = controller.AssistMode == DriverAssistMode.Road ? DriverAssistMode.Sport
                : controller.AssistMode == DriverAssistMode.Sport ? DriverAssistMode.Off : DriverAssistMode.Road;
        }

        public bool ShiftUp() => controller != null && DrivingEnabled && controller.TryShiftGear(1);
        public bool ShiftDown() => controller != null && DrivingEnabled && controller.TryShiftGear(-1);
        public bool ToggleReverse() => controller != null && DrivingEnabled && controller.TryToggleReverse();
        public bool ToggleTransmissionMode() => controller != null && DrivingEnabled && controller.ToggleTransmissionMode();

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

            var remainingSeconds = Mathf.Max(0f, deltaTimeSeconds);
            if (smoothedSteering * requestedInput.Steering < 0f)
            {
                // Countersteering must unwind the old turn as promptly as key release.
                var unwindSeconds = Mathf.Min(remainingSeconds, Mathf.Abs(smoothedSteering) / SteeringCenterRate);
                smoothedSteering = Mathf.MoveTowards(smoothedSteering, 0f, unwindSeconds * SteeringCenterRate);
                remainingSeconds -= unwindSeconds;
            }
            var rate = requestedInput.Steering == 0f ? SteeringCenterRate : SteeringPressRate;
            smoothedSteering = Mathf.MoveTowards(smoothedSteering, requestedInput.Steering,
                remainingSeconds * rate);
            if (requestedInput.Steering == 0f && Mathf.Abs(smoothedSteering) < 0.0001f)
                smoothedSteering = 0f;
            controller.SetInput(new VehicleInputState(requestedInput.Throttle, requestedInput.Brake,
                smoothedSteering, requestedInput.Handbrake));
        }
    }
}
