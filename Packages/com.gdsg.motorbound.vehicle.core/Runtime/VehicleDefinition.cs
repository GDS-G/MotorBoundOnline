using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public enum DriveLayout
    {
        FrontWheelDrive,
        RearWheelDrive,
        AllWheelDrive
    }

    [Serializable]
    public struct TorqueSample
    {
        public TorqueSample(double engineSpeedRpm, double torqueNewtonMeters)
        {
            EngineSpeedRpm = engineSpeedRpm;
            TorqueNewtonMeters = torqueNewtonMeters;
        }

        public double EngineSpeedRpm;
        public double TorqueNewtonMeters;
    }

    [Serializable]
    public sealed class EngineDefinition
    {
        public string Architecture = "Inline-four";
        public double DisplacementLiters = 2.0d;
        public double IdleSpeedRpm = 900d;
        public double RedlineSpeedRpm = 7200d;
        public double RotationalInertiaKilogramMetersSquared = 0.22d;
        public double EngineBrakingTorqueNewtonMeters = 42d;
        public TorqueSample[] FullLoadTorqueCurve = Array.Empty<TorqueSample>();

        public double EvaluateFullLoadTorqueNewtonMeters(double engineSpeedRpm)
        {
            if (FullLoadTorqueCurve == null || FullLoadTorqueCurve.Length == 0)
            {
                return 0d;
            }

            if (engineSpeedRpm <= FullLoadTorqueCurve[0].EngineSpeedRpm)
            {
                return FullLoadTorqueCurve[0].TorqueNewtonMeters;
            }

            for (var index = 1; index < FullLoadTorqueCurve.Length; index++)
            {
                var upper = FullLoadTorqueCurve[index];
                if (engineSpeedRpm > upper.EngineSpeedRpm)
                {
                    continue;
                }

                var lower = FullLoadTorqueCurve[index - 1];
                var rpmSpan = upper.EngineSpeedRpm - lower.EngineSpeedRpm;
                if (rpmSpan <= double.Epsilon)
                {
                    return upper.TorqueNewtonMeters;
                }

                var interpolation = (engineSpeedRpm - lower.EngineSpeedRpm) / rpmSpan;
                return lower.TorqueNewtonMeters + ((upper.TorqueNewtonMeters - lower.TorqueNewtonMeters) * interpolation);
            }

            return FullLoadTorqueCurve[FullLoadTorqueCurve.Length - 1].TorqueNewtonMeters;
        }
    }

    [Serializable]
    public sealed class TransmissionDefinition
    {
        public double[] ForwardGearRatios = Array.Empty<double>();
        public double ReverseGearRatio = -3.21d;
        public double FinalDriveRatio = 4.1d;
        public double Efficiency = 0.91d;
        public double UpshiftSpeedRpm = 6800d;
        public double DownshiftSpeedRpm = 2600d;
    }

    [Serializable]
    public sealed class TireDefinition
    {
        public double UnloadedRadiusMeters = 0.305d;
        public double SectionWidthMeters = 0.205d;
        public int ContactPatchSampleCount = 3;
        public double TreadWaterEvacuationFactor = 0.65d;
        public double RotationalInertiaKilogramMetersSquared = 1.15d;
        public double PeakDryFrictionCoefficient = 1.08d;
        public double LongitudinalSlipStiffnessNewtonPerRatio = 80000d;
        public double CorneringStiffnessNewtonPerRadian = 65000d;
        public double RollingResistanceCoefficient = 0.013d;
        public double ReferenceLoadNewtons = 3300d;
        public double LoadSensitivityExponent = -0.08d;
    }

    [Serializable]
    public sealed class SuspensionDefinition
    {
        public double RestLengthMeters = 0.34d;
        public double TravelMeters = 0.18d;
        public double SpringRateNewtonsPerMeter = 34000d;
        public double DamperCompressionNewtonsPerMeterPerSecond = 3600d;
        public double DamperReboundNewtonsPerMeterPerSecond = 4300d;
    }

    [Serializable]
    public sealed class BrakeDefinition
    {
        public double MaximumServiceBrakeTorqueNewtonMeters = 2400d;
        public double MaximumHandbrakeTorqueNewtonMeters = 2800d;
        public double FrontBias = 0.62d;
    }

    [Serializable]
    public sealed class VehicleDefinition
    {
        public StableId DefinitionId;
        public int DefinitionVersion = 1;
        public string Manufacturer = string.Empty;
        public string Family = string.Empty;
        public string Trim = string.Empty;
        public VehicleArchitectureIdentity Architecture = new VehicleArchitectureIdentity();
        public DriveLayout DriveLayout = DriveLayout.RearWheelDrive;
        public double MassKilograms = 1120d;
        public double WheelbaseMeters = 2.35d;
        public double FrontTrackMeters = 1.46d;
        public double RearTrackMeters = 1.45d;
        public double CenterOfMassHeightMeters = 0.46d;
        public double CenterOfMassLongitudinalOffsetMeters = -0.08d;
        public double FrontalAreaSquareMeters = 1.82d;
        public double AerodynamicDragCoefficient = 0.32d;
        public double AerodynamicLiftCoefficient = 0.08d;
        public double MaximumSteeringAngleDegrees = 32d;
        public EngineDefinition Engine = new EngineDefinition();
        public TransmissionDefinition Transmission = new TransmissionDefinition();
        public TireDefinition Tire = new TireDefinition();
        public SuspensionDefinition Suspension = new SuspensionDefinition();
        public BrakeDefinition Brakes = new BrakeDefinition();

        public string DisplayName => (Manufacturer + " " + Family + " " + Trim).Trim();

        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            Require(!DefinitionId.IsEmpty, "definitionId", "A stable definition ID is required.", issues);
            Require(!string.IsNullOrWhiteSpace(Manufacturer), "manufacturer", "Manufacturer is required.", issues);
            Require(!string.IsNullOrWhiteSpace(Family), "family", "Vehicle family is required.", issues);
            Require(Architecture != null, "architecture", "Vehicle architecture identity is required.", issues);
            Require(DefinitionVersion > 0, "definitionVersion", "Definition version must be positive.", issues);
            Require(MassKilograms > 100d, "massKilograms", "Mass must be greater than 100 kg.", issues);
            Require(WheelbaseMeters > 0.5d, "wheelbaseMeters", "Wheelbase must be greater than 0.5 m.", issues);
            Require(FrontTrackMeters > 0.5d && RearTrackMeters > 0.5d, "trackMeters", "Track widths must be greater than 0.5 m.", issues);
            Require(Engine != null, "engine", "Engine definition is required.", issues);
            Require(Transmission != null, "transmission", "Transmission definition is required.", issues);
            Require(Tire != null, "tire", "Tire definition is required.", issues);
            Require(Suspension != null, "suspension", "Suspension definition is required.", issues);
            Require(Brakes != null, "brakes", "Brake definition is required.", issues);

            if (Architecture != null)
            {
                foreach (var issue in Architecture.Validate())
                {
                    issues.Add(issue);
                }
            }

            if (Engine != null)
            {
                Require(Engine.IdleSpeedRpm > 0d, "engine.idleSpeedRpm", "Idle speed must be positive.", issues);
                Require(Engine.RedlineSpeedRpm > Engine.IdleSpeedRpm, "engine.redlineSpeedRpm", "Redline must exceed idle speed.", issues);
                Require(Engine.FullLoadTorqueCurve != null && Engine.FullLoadTorqueCurve.Length >= 2, "engine.fullLoadTorqueCurve", "At least two torque samples are required.", issues);
                if (Engine.FullLoadTorqueCurve != null)
                {
                    for (var index = 1; index < Engine.FullLoadTorqueCurve.Length; index++)
                    {
                        Require(
                            Engine.FullLoadTorqueCurve[index].EngineSpeedRpm > Engine.FullLoadTorqueCurve[index - 1].EngineSpeedRpm,
                            "engine.fullLoadTorqueCurve[" + index + "]",
                            "Torque samples must be ordered by strictly increasing RPM.",
                            issues);
                    }
                }
            }

            if (Transmission != null)
            {
                Require(Transmission.ForwardGearRatios != null && Transmission.ForwardGearRatios.Length > 0, "transmission.forwardGearRatios", "At least one forward gear is required.", issues);
                Require(Transmission.FinalDriveRatio > 0d, "transmission.finalDriveRatio", "Final drive ratio must be positive.", issues);
                Require(Transmission.Efficiency > 0d && Transmission.Efficiency <= 1d, "transmission.efficiency", "Efficiency must be in (0, 1].", issues);
            }

            if (Tire != null)
            {
                Require(Tire.UnloadedRadiusMeters > 0.1d, "tire.unloadedRadiusMeters", "Tire radius must exceed 0.1 m.", issues);
                Require(Tire.SectionWidthMeters > 0.05d, "tire.sectionWidthMeters", "Tire section width must exceed 0.05 m.", issues);
                Require(
                    Tire.ContactPatchSampleCount > 0 && Tire.ContactPatchSampleCount <= 5 && Tire.ContactPatchSampleCount % 2 == 1,
                    "tire.contactPatchSampleCount",
                    "Contact-patch sampling must use one, three, or five rays.",
                    issues);
                Require(
                    Tire.TreadWaterEvacuationFactor >= 0d && Tire.TreadWaterEvacuationFactor <= 1d,
                    "tire.treadWaterEvacuationFactor",
                    "Water evacuation factor must be in [0, 1].",
                    issues);
                Require(Tire.PeakDryFrictionCoefficient > 0d, "tire.peakDryFrictionCoefficient", "Peak friction must be positive.", issues);
            }

            return issues;
        }

        private static void Require(bool condition, string path, string message, ICollection<ValidationIssue> issues)
        {
            if (!condition)
            {
                issues.Add(new ValidationIssue(path, message));
            }
        }
    }
}
