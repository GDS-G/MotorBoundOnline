using System;
using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    [Serializable]
    public struct WheelTelemetry
    {
        public bool Grounded;
        public float SuspensionCompressionMeters;
        public float NormalLoadNewtons;
        public float SlipRatio;
        public float SlipAngleDegrees;
        public float LongitudinalForceNewtons;
        public float LateralForceNewtons;
        public float SurfaceGripMultiplier;
        public string SurfaceName;
    }

    [Serializable]
    public struct VehicleTelemetry
    {
        public string VehicleName;
        public float SpeedMetersPerSecond;
        public float EngineSpeedRpm;
        public int ForwardGear;
        public Vector3 LocalAccelerationMetersPerSecondSquared;
        public VehicleInputState Input;
        public WheelTelemetry[] Wheels;

        public int GroundedWheelCount
        {
            get
            {
                if (Wheels == null)
                {
                    return 0;
                }

                var count = 0;
                foreach (var wheel in Wheels)
                {
                    if (wheel.Grounded)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
