using System;
using MotorBound.Vehicle.Core;

namespace MotorBound.Vehicle.Physics
{
    public enum DriveTransmissionMode { Automatic, Manual }

    public struct TransmissionGearSelectionResult
    {
        public bool Accepted { get; internal set; }
        public bool GearChanged { get; internal set; }
        public bool ModeChanged { get; internal set; }
        public int SelectedGear { get; internal set; }
        public DriveTransmissionMode Mode { get; internal set; }
        public string Message { get; internal set; }
    }

    /// <summary>
    /// Gear-command policy only: -1 reverse, 0 neutral, 1..N forward.
    /// This does not simulate an engine clutch, synchronizer, shift duration or engine inertia.
    /// The caller must reject unsafe predicted engine RPM before submitting a manual request.
    /// </summary>
    public sealed class TransmissionGearSelector
    {
        public const double MaximumDirectionSelectionSpeedMetersPerSecond = 0.5d;
        public int SelectedGear { get; private set; } = 1;
        public DriveTransmissionMode Mode { get; private set; } = DriveTransmissionMode.Automatic;

        public void Reset()
        {
            SelectedGear = 1;
            Mode = DriveTransmissionMode.Automatic;
        }

        public TransmissionGearSelectionResult RequestGear(int requestedGear, int forwardGearCount,
            double worldSpeedMagnitudeMetersPerSecond)
        {
            if (!ValidGearCount(forwardGearCount))
                return Rejected("The transmission requires at least one forward gear.");
            if (!IsFinite(worldSpeedMagnitudeMetersPerSecond) || worldSpeedMagnitudeMetersPerSecond < 0d)
                return Rejected("Vehicle speed must be finite and nonnegative.");
            if (requestedGear < -1 || requestedGear > forwardGearCount)
                return Rejected("Requested gear is outside reverse, neutral and the configured forward gears.");

            // Magnitude is deliberately used rather than signed forward velocity: a sideways
            // slide is not stopped. Neutral cannot bypass the direction-change interlock.
            // With no signed motion observation, neutral-to-drive is conservatively interlocked.
            var directionSelection = requestedGear != 0 && requestedGear != SelectedGear
                && (requestedGear == -1 || SelectedGear <= 0);
            if (directionSelection && worldSpeedMagnitudeMetersPerSecond > MaximumDirectionSelectionSpeedMetersPerSecond)
                return Rejected("Stop to 0.5 m/s or less before selecting reverse or engaging drive from reverse/neutral.");

            return Commit(requestedGear, DriveTransmissionMode.Manual,
                requestedGear == SelectedGear ? "Manual mode; gear unchanged." : "Manual gear selected.");
        }

        public TransmissionGearSelectionResult RequestShift(int direction, int forwardGearCount,
            double worldSpeedMagnitudeMetersPerSecond)
        {
            if (direction != -1 && direction != 1)
                return Rejected("Shift direction must be -1 (down) or 1 (up).");
            if (!ValidGearCount(forwardGearCount))
                return Rejected("The transmission requires at least one forward gear.");
            // Check the endpoints before arithmetic, including int.MaxValue gear counts.
            var target = direction < 0
                ? (SelectedGear <= -1 ? -1 : SelectedGear - 1)
                : (SelectedGear >= forwardGearCount ? forwardGearCount : SelectedGear + 1);
            return RequestGear(target, forwardGearCount, worldSpeedMagnitudeMetersPerSecond);
        }

        public TransmissionGearSelectionResult RequestShiftUp(int forwardGearCount, double worldSpeedMagnitudeMetersPerSecond)
        {
            return RequestShift(1, forwardGearCount, worldSpeedMagnitudeMetersPerSecond);
        }

        public TransmissionGearSelectionResult RequestShiftDown(int forwardGearCount, double worldSpeedMagnitudeMetersPerSecond)
        {
            return RequestShift(-1, forwardGearCount, worldSpeedMagnitudeMetersPerSecond);
        }

        public TransmissionGearSelectionResult RequestReverseToggle(int forwardGearCount, double worldSpeedMagnitudeMetersPerSecond)
        {
            // R is a stopped direction selector even when neutral is selected.
            if (!IsFinite(worldSpeedMagnitudeMetersPerSecond) || worldSpeedMagnitudeMetersPerSecond < 0d)
                return Rejected("Vehicle speed must be finite and nonnegative.");
            if (worldSpeedMagnitudeMetersPerSecond > MaximumDirectionSelectionSpeedMetersPerSecond)
                return Rejected("Stop to 0.5 m/s or less before changing driving direction.");
            return RequestGear(SelectedGear == -1 ? 1 : -1, forwardGearCount, worldSpeedMagnitudeMetersPerSecond);
        }

        public TransmissionGearSelectionResult ToggleMode()
        {
            return SetMode(Mode == DriveTransmissionMode.Automatic
                ? DriveTransmissionMode.Manual : DriveTransmissionMode.Automatic);
        }

        public TransmissionGearSelectionResult SetMode(DriveTransmissionMode requestedMode)
        {
            if (requestedMode != DriveTransmissionMode.Automatic && requestedMode != DriveTransmissionMode.Manual)
                return Rejected("Unknown transmission mode.");
            // Changing mode never engages drive or selects a direction.
            return Commit(SelectedGear, requestedMode,
                requestedMode == DriveTransmissionMode.Automatic ? "Automatic mode selected." : "Manual mode selected.");
        }

        public TransmissionGearSelectionResult SelectAutomaticGear(int requestedForwardGear, int forwardGearCount)
        {
            if (!ValidGearCount(forwardGearCount) || requestedForwardGear < 1 || requestedForwardGear > forwardGearCount)
                return Rejected("Automatic gear selection requires a configured positive forward gear.");
            if (Mode != DriveTransmissionMode.Automatic || SelectedGear <= 0)
                return Rejected("Automatic shifting is inactive in manual mode, neutral or reverse.");
            // RPM thresholds and observations belong to the caller. No frame counter or
            // fabricated clutch/shift-time dynamics are hidden in this selection policy.
            return Commit(requestedForwardGear, Mode,
                requestedForwardGear == SelectedGear ? "Automatic gear unchanged." : "Automatic gear selected.");
        }

        public double GetSelectedGearRatio(TransmissionDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (SelectedGear == 0) return 0d;
            var ratio = definition.ReverseGearRatio;
            if (SelectedGear > 0)
            {
                if (definition.ForwardGearRatios == null || SelectedGear > definition.ForwardGearRatios.Length)
                    throw new ArgumentException("Selected forward gear is not configured.", nameof(definition));
                ratio = definition.ForwardGearRatios[SelectedGear - 1];
            }
            if (!IsFinite(ratio) || (SelectedGear > 0 ? ratio <= 0d : ratio >= 0d))
                throw new ArgumentException("Selected gear ratio must be finite and have the direction's sign.", nameof(definition));
            return ratio;
        }

        private TransmissionGearSelectionResult Commit(int nextGear, DriveTransmissionMode nextMode, string message)
        {
            var gearChanged = nextGear != SelectedGear;
            var modeChanged = nextMode != Mode;
            SelectedGear = nextGear;
            Mode = nextMode;
            return Result(true, gearChanged, modeChanged, message);
        }

        private TransmissionGearSelectionResult Rejected(string message)
        {
            return Result(false, false, false, message);
        }

        private TransmissionGearSelectionResult Result(bool accepted, bool gearChanged, bool modeChanged, string message)
        {
            return new TransmissionGearSelectionResult
            {
                Accepted = accepted, GearChanged = gearChanged, ModeChanged = modeChanged,
                SelectedGear = SelectedGear, Mode = Mode, Message = message
            };
        }

        private static bool ValidGearCount(int count) => count > 0;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
