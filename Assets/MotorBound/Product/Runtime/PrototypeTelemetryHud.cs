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
            GUILayout.Label("MOTORBOUND // VEHICLE DYNAMICS PROTOTYPE", headingStyle);
            GUILayout.Label(telemetry.VehicleName, bodyStyle);
            GUILayout.Space(6f);
            GUILayout.Label(
                string.Format(
                    "{0,6:0.0} km/h  |  {1,5:0} rpm  |  Gear {2}  |  Grounded {3}/4",
                    UnitConversion.ToKilometersPerHour(telemetry.SpeedMetersPerSecond),
                    telemetry.EngineSpeedRpm,
                    telemetry.ForwardGear,
                    telemetry.GroundedWheelCount),
                bodyStyle);
            GUILayout.Label(
                string.Format(
                    "Critical-vehicle step {0} Hz  |  three-ray contact patch target",
                    telemetry.SimulationFrequencyHertz),
                bodyStyle);
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
            GUILayout.Space(8f);

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
            GUILayout.Label("Backspace recover  |  F1 hide/show telemetry", bodyStyle);
            GUILayout.EndArea();
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
