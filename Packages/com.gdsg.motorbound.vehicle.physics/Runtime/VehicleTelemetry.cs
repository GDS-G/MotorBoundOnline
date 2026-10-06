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
        public float AngularSpeedRadiansPerSecond;
        public float DriveTorqueNewtonMeters;
        public float SurfaceGripMultiplier;
        public int ContactSampleCount;
        public float WaterFilmDepthMillimeters;
        public Vector3 ContactPointWorld;
        public Vector3 ContactNormalWorld;
        public float SlipDemandRatio;
        public bool IsSliding;
        public string SurfaceName;
    }

    [Serializable]
    public struct VehicleTelemetry
    {
        public string VehicleName;
        public float SpeedMetersPerSecond;
        public float EngineSpeedRpm;
        public int ForwardGear;
        public int SimulationFrequencyHertz;
        public Vector3 LocalAccelerationMetersPerSecondSquared;
        public VehicleInputState Input;
        public bool RoadTractionControlEnabled;
        public bool TractionControlActive;
        public float DeliveredDriveTorqueScale;
        public DriverAssistMode AssistMode;
        public DriverAssistPhase AssistPhase;
        public float BodySideslipDegrees;
        public float FrontDifferentialTransferTorqueNewtonMeters;
        public float RearDifferentialTransferTorqueNewtonMeters;
        // Largest absolute axle transfer, useful as a layout-independent HUD summary.
        public float DifferentialTransferTorqueNewtonMeters;
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
