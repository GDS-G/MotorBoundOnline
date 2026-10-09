using MotorBound.Foundation;
using MotorBound.Vehicle.Physics;
using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeTelemetryHud : MonoBehaviour
    {
        private static readonly string[] WheelLabels = { "FL", "FR", "RL", "RR" };
        private RaycastVehicleController controller;
        private PrototypeGarageSession garage;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private bool visible = true;

        public void Configure(RaycastVehicleController target)
        {
            controller = target;
            garage = target.GetComponent<PrototypeGarageSession>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible || controller == null || (garage != null && garage.IsInGarage))
            {
                return;
            }

            EnsureStyles();
            var telemetry = controller.Telemetry;
            var width = Mathf.Min(520f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, Screen.height - 32f), GUI.skin.box);
            GUILayout.Label("MOTORBOUND // VEHICLE DYNAMICS " + Application.version, headingStyle);
            GUILayout.Label(telemetry.VehicleName, bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label(
                string.Format(
                    "{0:0.0} mph / {1:0.0} km/h  |  {2,5:0} rpm  |  {3}",
                    UnitConversion.ToMilesPerHour(telemetry.SpeedMetersPerSecond),
                    UnitConversion.ToKilometersPerHour(telemetry.SpeedMetersPerSecond),
                    telemetry.EngineSpeedRpm,
                    GearLabel(telemetry.ForwardGear)),
                bodyStyle);
            GUILayout.Label(
                string.Format(
                    "{0} transmission  |  Grounded {1}/4  |  Physics {2} Hz",
                    controller.TransmissionMode, telemetry.GroundedWheelCount, telemetry.SimulationFrequencyHertz),
                bodyStyle);
            GUILayout.Label(controller.GearSelectionMessage, bodyStyle);
            if (telemetry.EngineRevLimiterActive) GUILayout.Label("REV LIMITER / propulsion cut — shift up", bodyStyle);
            GUILayout.Label(
                string.Format(
                    "Throttle {0:0.00}  Brake {1:0.00}  Steer {2:+0.00;-0.00;0.00}  Handbrake {3:0.00}",
                    telemetry.Input.Throttle,
                    telemetry.Input.Brake,
                    telemetry.Input.Steering,
                    telemetry.Input.Handbrake),
                bodyStyle);
            GUILayout.Label(
                string.Format(
                    "Acceleration  lateral {0:+0.00;-0.00;0.00} m/s^2  longitudinal {1:+0.00;-0.00;0.00} m/s^2",
                    telemetry.LocalAccelerationMetersPerSecondSquared.x,
                    telemetry.LocalAccelerationMetersPerSecondSquared.z),
                bodyStyle);
            GUILayout.Label("Assist: " + controller.AssistMode.ToString().ToUpperInvariant()
                + " / " + AssistPhaseLabel(telemetry.AssistPhase)
                + (telemetry.TractionControlActive ? " / torque limiting" : "") + "  |  F2 cycle", bodyStyle);
            GUILayout.Label(string.Format("Body slide {0:+0.0;-0.0;0.0} deg  |  Delivered drive {1:0}%",
                telemetry.BodySideslipDegrees, telemetry.DeliveredDriveTorqueScale * 100f), bodyStyle);
            GUILayout.Space(8f);
            var frontSliding = false;
            var rearSliding = false;
            if (telemetry.Wheels != null)
                for (var index = 0; index < telemetry.Wheels.Length; index++)
                    if (telemetry.SpeedMetersPerSecond >= 2f && telemetry.Wheels[index].Grounded
                        && telemetry.Wheels[index].NormalLoadNewtons > 20f && telemetry.Wheels[index].IsSliding)
                    {
                        if (index < 2) frontSliding = true;
                        else rearSliding = true;
                    }
            GUILayout.Label("Tires: " + (frontSliding && rearSliding ? "BOTH AXLES SLIDING"
                : frontSliding ? "FRONT TIRES SLIDING / PUSHING WIDE"
                : rearSliding ? "REAR TIRES SLIDING" : "GRIPPING"), bodyStyle);

            if (telemetry.Wheels != null)
            {
                for (var index = 0; index < telemetry.Wheels.Length; index++)
                {
                    var wheel = telemetry.Wheels[index];
                    GUILayout.Label(
                        string.Format(
                            "{0} {1,-16} grip {2:0.00}  load {3,5:0} N  slip {4,6:+0.00;-0.00;0.00}  angle {5,6:+0.0;-0.0;0.0} deg  rays {6}  water {7:0.0} mm",
                            WheelLabels[index],
                            wheel.SurfaceName ?? "Unknown",
                            wheel.SurfaceGripMultiplier,
                            wheel.NormalLoadNewtons,
                            wheel.SlipRatio,
                            wheel.SlipAngleDegrees,
                            wheel.ContactSampleCount,
                            wheel.WaterFilmDepthMillimeters),
                        bodyStyle);
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("W throttle  |  S brake  |  A/D steer  |  Space handbrake", bodyStyle);
            GUILayout.Label("Q/E shift down/up (manual)  |  M auto/manual  |  R reverse/first when stopped", bodyStyle);
            GUILayout.Label("Backspace recover  |  F1 telemetry  |  F2 Road / Sport / Off", bodyStyle);
            GUILayout.Label("Sport: Space in a turn initiates; release, feather W, countersteer.", bodyStyle);
            GUILayout.EndArea();
        }

        private static string AssistPhaseLabel(DriverAssistPhase phase)
        {
            switch (phase)
            {
                case DriverAssistPhase.SlideInitiation: return "slide entry";
                case DriverAssistPhase.Sliding: return "physical slide / limiter bypass";
                case DriverAssistPhase.Recovery: return "catching slide / limiter bypass";
                case DriverAssistPhase.Unassisted: return "unassisted";
                default: return "road grip";
            }
        }

        public static string GearLabel(int selectedGear)
        {
            return selectedGear < 0 ? "Gear R" : selectedGear == 0 ? "Gear N" : "Gear " + selectedGear;
        }

        private void EnsureStyles()
        {
            if (headingStyle != null)
            {
                return;
            }

            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.8f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }
    }
}
