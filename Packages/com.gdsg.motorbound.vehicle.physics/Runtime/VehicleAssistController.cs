using System;

namespace MotorBound.Vehicle.Physics
{
    public enum DriverAssistMode { Road, Sport, Off }
    public enum DriverAssistPhase { RoadGrip, SlideInitiation, Sliding, Recovery, Unassisted }

    public struct VehicleAssistObservation
    {
        public double SpeedMetersPerSecond;
        public double BodySideslipDegrees;
        public double YawRateDegreesPerSecond;
        public double Throttle;
        public double Brake;
        public double Steering;
        public double Handbrake;
        public int LoadedDrivenContactCount;
        public bool HasSlidingDrivenContact;
    }

    public struct VehicleAssistDecision
    {
        public bool UseRoadTractionControl;
        public DriverAssistPhase Phase;
    }

    /// <summary>
    /// Optional driver-intent policy, separate from the physical tire/drivetrain solvers.
    /// It only selects whether the engine torque limiter may intervene. It cannot add
    /// grip, yaw torque, countersteering, speed, or wheel impulses.
    /// </summary>
    public sealed class VehicleAssistController
    {
        public const double UnconfirmedInitiationSeconds = 0.85d;
        public const double SettledRecoverySeconds = 0.35d;
        private DriverAssistMode previousMode;
        private bool observedMode;
        private DriverAssistPhase phase = DriverAssistPhase.RoadGrip;
        private double unconfirmedSeconds;
        private double settledSeconds;

        public void Reset()
        {
            phase = DriverAssistPhase.RoadGrip;
            unconfirmedSeconds = 0d;
            settledSeconds = 0d;
            observedMode = false;
        }

        public VehicleAssistDecision Evaluate(DriverAssistMode mode, VehicleAssistObservation observation,
            double deltaTimeSeconds)
        {
            if (mode < DriverAssistMode.Road || mode > DriverAssistMode.Off)
                throw new ArgumentOutOfRangeException(nameof(mode));
            RequireFinite(deltaTimeSeconds, nameof(deltaTimeSeconds));
            if (deltaTimeSeconds <= 0d) throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            RequireFinite(observation.SpeedMetersPerSecond, nameof(observation.SpeedMetersPerSecond));
            RequireFinite(observation.BodySideslipDegrees, nameof(observation.BodySideslipDegrees));
            RequireFinite(observation.YawRateDegreesPerSecond, nameof(observation.YawRateDegreesPerSecond));
            RequireFinite(observation.Throttle, nameof(observation.Throttle));
            RequireFinite(observation.Brake, nameof(observation.Brake));
            RequireFinite(observation.Steering, nameof(observation.Steering));
            RequireFinite(observation.Handbrake, nameof(observation.Handbrake));
            if (!observedMode || previousMode != mode)
            {
                Reset();
                previousMode = mode;
                observedMode = true;
            }

            if (mode == DriverAssistMode.Off)
                return Decision(false, DriverAssistPhase.Unassisted);
            if (mode == DriverAssistMode.Road)
                return Decision(true, DriverAssistPhase.RoadGrip);

            // A reset/stop/loss of all driven contacts cannot retain a hidden slide latch.
            if (observation.SpeedMetersPerSecond < 2d || observation.LoadedDrivenContactCount <= 0)
            {
                phase = DriverAssistPhase.RoadGrip;
                unconfirmedSeconds = settledSeconds = 0d;
                return Decision(true, phase);
            }

            var handbrake = observation.Handbrake > 0.1d;
            var intentionalInitiation = handbrake && observation.SpeedMetersPerSecond >= 6d
                && Math.Abs(observation.Steering) >= 0.04d;
            var measuredSlide = Math.Abs(observation.BodySideslipDegrees) >= 3d
                && observation.HasSlidingDrivenContact;
            var settled = !handbrake && Math.Abs(observation.BodySideslipDegrees) < 2d
                && Math.Abs(observation.YawRateDegreesPerSecond) < 5d;

            if (phase == DriverAssistPhase.RoadGrip && intentionalInitiation)
            {
                phase = DriverAssistPhase.SlideInitiation;
                unconfirmedSeconds = settledSeconds = 0d;
            }

            if (phase == DriverAssistPhase.SlideInitiation)
            {
                if (measuredSlide)
                    phase = DriverAssistPhase.Sliding;
                else
                {
                    unconfirmedSeconds = handbrake ? 0d : unconfirmedSeconds + deltaTimeSeconds;
                    if (unconfirmedSeconds >= UnconfirmedInitiationSeconds)
                        phase = DriverAssistPhase.RoadGrip;
                }
            }

            if (phase == DriverAssistPhase.Sliding || phase == DriverAssistPhase.Recovery)
            {
                // Lifting/braking requests recovery; reapplying power to an already
                // initiated physical slide may sustain it. No force is commanded here.
                if (!handbrake && (observation.Throttle <= 0.1d || observation.Brake > 0.05d || settled))
                    phase = DriverAssistPhase.Recovery;
                else if (measuredSlide && observation.Throttle > 0.1d && observation.Brake <= 0.05d)
                    phase = DriverAssistPhase.Sliding;

                settledSeconds = settled ? settledSeconds + deltaTimeSeconds : 0d;
                if (settledSeconds >= SettledRecoverySeconds)
                {
                    phase = DriverAssistPhase.RoadGrip;
                    unconfirmedSeconds = settledSeconds = 0d;
                }
            }

            return Decision(phase == DriverAssistPhase.RoadGrip, phase);
        }

        private static VehicleAssistDecision Decision(bool useRoadControl, DriverAssistPhase nextPhase)
        {
            return new VehicleAssistDecision { UseRoadTractionControl = useRoadControl, Phase = nextPhase };
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
