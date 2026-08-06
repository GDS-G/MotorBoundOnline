using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public enum FitmentClassification
    {
        BoltIn = 0,
        CompatibleWithAlternateFactoryParts = 1,
        AdapterInstallation = 2,
        MinorFabrication = 3,
        MajorFabrication = 4,
        Incompatible = 5
    }

    public enum FitmentStage
    {
        Identity = 1,
        Mount = 2,
        StaticGeometry = 3,
        DynamicSweep = 4,
        Alignment = 5,
        Capacity = 6,
        Serviceability = 7,
        Control = 8,
        Regulatory = 9,
        VisualCompleteness = 10
    }

    [Serializable]
    public struct SpatialEnvelope
    {
        public SpatialEnvelope(double widthMeters, double heightMeters, double lengthMeters)
        {
            WidthMeters = widthMeters;
            HeightMeters = heightMeters;
            LengthMeters = lengthMeters;
        }

        public double WidthMeters;
        public double HeightMeters;
        public double LengthMeters;

        public bool IsPositive => WidthMeters > 0d && HeightMeters > 0d && LengthMeters > 0d;

        public bool FitsInside(SpatialEnvelope available, double allowanceMeters)
        {
            return WidthMeters <= available.WidthMeters + allowanceMeters
                && HeightMeters <= available.HeightMeters + allowanceMeters
                && LengthMeters <= available.LengthMeters + allowanceMeters;
        }

        public double MaximumExcessMeters(SpatialEnvelope available)
        {
            return Math.Max(
                Math.Max(0d, WidthMeters - available.WidthMeters),
                Math.Max(
                    Math.Max(0d, HeightMeters - available.HeightMeters),
                    Math.Max(0d, LengthMeters - available.LengthMeters)));
        }
    }

    [Serializable]
    public sealed class HardpointDefinition
    {
        public StableId HardpointId;
        public int Version = 1;
        public string Name = string.Empty;
        public string MountPatternCode = string.Empty;
        public double PositionXMeters;
        public double PositionYMeters;
        public double PositionZMeters;
        public double RotationXRad;
        public double RotationYRad;
        public double RotationZRad;
        public double RatedLoadNewtons;
        public double PositionalToleranceMeters = 0.001d;
        public SpatialEnvelope AvailableEnvelope;
        public double ServiceClearanceMeters;
        public double NominalVoltage;
    }

    [Serializable]
    public sealed class ComponentFitmentDefinition
    {
        public StableId ComponentDefinitionId;
        public int Revision = 1;
        public string DisplayName = string.Empty;
        public StableId RequiredPlatformId;
        public StableId RequiredHardpointId;
        public string RequiredMountPatternCode = string.Empty;
        public string[] AdapterCompatibleHostPatternCodes = Array.Empty<string>();
        public SpatialEnvelope OccupiedEnvelope;
        public double DynamicSweepAllowanceMeters;
        public double AlignmentToleranceMeters = 0.002d;
        public double AppliedLoadNewtons;
        public double RequiredServiceClearanceMeters;
        public double NominalVoltage;
        public bool UsesAlternateFactoryParts;
        public bool HasRequiredControlProtocol = true;
        public bool IsRegulatoryCompliant = true;
        public bool HasRequiredVisualClosureParts = true;
        public double MinorFabricationLimitMeters = 0.006d;
        public double MajorFabricationLimitMeters = 0.05d;
    }

    [Serializable]
    public sealed class VehicleFitmentContext
    {
        public StableId PlatformId;
        public HardpointDefinition[] Hardpoints = Array.Empty<HardpointDefinition>();

        public HardpointDefinition FindHardpoint(StableId hardpointId)
        {
            if (Hardpoints == null)
            {
                return null;
            }

            foreach (var hardpoint in Hardpoints)
            {
                if (hardpoint != null && hardpoint.HardpointId == hardpointId)
                {
                    return hardpoint;
                }
            }

            return null;
        }
    }

    [Serializable]
    public struct FitmentCheck
    {
        public FitmentCheck(FitmentStage stage, bool passed, string conflict, string remedy)
        {
            Stage = stage;
            Passed = passed;
            Conflict = conflict ?? string.Empty;
            Remedy = remedy ?? string.Empty;
        }

        public FitmentStage Stage;
        public bool Passed;
        public string Conflict;
        public string Remedy;
    }

    [Serializable]
    public sealed class FitmentResult
    {
        public FitmentClassification Classification;
        public FitmentCheck[] Checks = Array.Empty<FitmentCheck>();

        public bool IsInstallable => Classification != FitmentClassification.Incompatible;

        public FitmentCheck Find(FitmentStage stage)
        {
            foreach (var check in Checks)
            {
                if (check.Stage == stage)
                {
                    return check;
                }
            }

            return default(FitmentCheck);
        }
    }

    public static class FitmentValidator
    {
        public static FitmentResult Evaluate(VehicleFitmentContext vehicle, ComponentFitmentDefinition component)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var checks = new List<FitmentCheck>(10);
            var classification = component.UsesAlternateFactoryParts
                ? FitmentClassification.CompatibleWithAlternateFactoryParts
                : FitmentClassification.BoltIn;

            var identityPasses = !vehicle.PlatformId.IsEmpty
                && !component.ComponentDefinitionId.IsEmpty
                && component.Revision > 0
                && component.RequiredPlatformId == vehicle.PlatformId;
            checks.Add(identityPasses
                ? Pass(FitmentStage.Identity)
                : Fail(FitmentStage.Identity, "Component or platform identity is missing or the platform IDs do not match.", "Select a revision authored for this platform."));

            var hardpoint = vehicle.FindHardpoint(component.RequiredHardpointId);
            var mountPasses = hardpoint != null && StringComparer.Ordinal.Equals(hardpoint.MountPatternCode, component.RequiredMountPatternCode);
            if (!mountPasses && hardpoint != null && Contains(component.AdapterCompatibleHostPatternCodes, hardpoint.MountPatternCode))
            {
                mountPasses = true;
                classification = Max(classification, FitmentClassification.AdapterInstallation);
                checks.Add(new FitmentCheck(
                    FitmentStage.Mount,
                    true,
                    "Mount pattern " + hardpoint.MountPatternCode + " differs from required pattern " + component.RequiredMountPatternCode + ".",
                    "Install the validated adapter for the two named patterns."));
            }
            else
            {
                checks.Add(mountPasses
                    ? Pass(FitmentStage.Mount)
                    : Fail(FitmentStage.Mount, "The required hardpoint or mounting pattern is unavailable.", "Use a compatible hardpoint, alternate factory carrier, or validated adapter."));
            }

            var geometryPasses = hardpoint != null && component.OccupiedEnvelope.IsPositive && hardpoint.AvailableEnvelope.IsPositive;
            var excessMeters = geometryPasses ? component.OccupiedEnvelope.MaximumExcessMeters(hardpoint.AvailableEnvelope) : double.MaxValue;
            if (geometryPasses && excessMeters > 0d)
            {
                if (excessMeters <= component.MinorFabricationLimitMeters)
                {
                    classification = Max(classification, FitmentClassification.MinorFabrication);
                }
                else if (excessMeters <= component.MajorFabricationLimitMeters)
                {
                    classification = Max(classification, FitmentClassification.MajorFabrication);
                }
                else
                {
                    geometryPasses = false;
                }
            }

            checks.Add(geometryPasses
                ? new FitmentCheck(
                    FitmentStage.StaticGeometry,
                    true,
                    excessMeters > 0d ? "Envelope exceeds bolt-in space by " + ToMillimeters(excessMeters) + " mm." : string.Empty,
                    excessMeters > 0d ? "Create and inspect a documented clearance modification." : string.Empty)
                : Fail(FitmentStage.StaticGeometry, "The component has invalid dimensions or exceeds the available envelope.", "Choose a smaller component or a validated major structural package."));

            var sweepPasses = hardpoint != null
                && component.OccupiedEnvelope.FitsInside(hardpoint.AvailableEnvelope, Math.Max(0d, component.DynamicSweepAllowanceMeters));
            checks.Add(sweepPasses
                ? Pass(FitmentStage.DynamicSweep)
                : Fail(FitmentStage.DynamicSweep, "The dynamic motion envelope collides with surrounding geometry.", "Change the component, travel limit, hardpoint, or surrounding package."));

            var alignmentPasses = hardpoint != null
                && component.AlignmentToleranceMeters >= hardpoint.PositionalToleranceMeters;
            checks.Add(alignmentPasses
                ? Pass(FitmentStage.Alignment)
                : Fail(
                    FitmentStage.Alignment,
                    "Required alignment tolerance is " + ToMillimeters(component.AlignmentToleranceMeters) + " mm but the hardpoint tolerance is " + ToMillimeters(hardpoint == null ? 0d : hardpoint.PositionalToleranceMeters) + " mm.",
                    "Measure and correct the mounting datum or select a more tolerant component."));

            var capacityPasses = hardpoint != null && component.AppliedLoadNewtons <= hardpoint.RatedLoadNewtons;
            checks.Add(capacityPasses
                ? Pass(FitmentStage.Capacity)
                : Fail(
                    FitmentStage.Capacity,
                    "Applied load " + component.AppliedLoadNewtons + " N exceeds the hardpoint rating " + (hardpoint == null ? 0d : hardpoint.RatedLoadNewtons) + " N.",
                    "Use a lower-load component or a validated structural reinforcement."));

            var servicePasses = hardpoint != null && component.RequiredServiceClearanceMeters <= hardpoint.ServiceClearanceMeters;
            checks.Add(servicePasses
                ? Pass(FitmentStage.Serviceability)
                : Fail(
                    FitmentStage.Serviceability,
                    "Service clearance is short by " + ToMillimeters(component.RequiredServiceClearanceMeters - (hardpoint == null ? 0d : hardpoint.ServiceClearanceMeters)) + " mm.",
                    "Provide a documented service-access procedure or revise the package."));

            var voltageMatches = hardpoint != null
                && (component.NominalVoltage <= 0d || hardpoint.NominalVoltage <= 0d || Math.Abs(component.NominalVoltage - hardpoint.NominalVoltage) < 0.01d);
            var controlPasses = component.HasRequiredControlProtocol && voltageMatches;
            checks.Add(controlPasses
                ? Pass(FitmentStage.Control)
                : Fail(FitmentStage.Control, "Electrical voltage or control protocol is incompatible.", "Add a validated controller, harness, relay, or power conversion package."));

            checks.Add(component.IsRegulatoryCompliant
                ? Pass(FitmentStage.Regulatory)
                : Fail(FitmentStage.Regulatory, "The configured installation is not approved for this operating context.", "Restrict use to an allowed context or select a compliant configuration."));
            checks.Add(component.HasRequiredVisualClosureParts
                ? Pass(FitmentStage.VisualCompleteness)
                : Fail(FitmentStage.VisualCompleteness, "Required closure, trim, or adjacent visual parts are missing.", "Install the required companion parts before final inspection."));

            foreach (var check in checks)
            {
                if (!check.Passed)
                {
                    classification = FitmentClassification.Incompatible;
                    break;
                }
            }

            return new FitmentResult
            {
                Classification = classification,
                Checks = checks.ToArray()
            };
        }

        private static FitmentCheck Pass(FitmentStage stage)
        {
            return new FitmentCheck(stage, true, string.Empty, string.Empty);
        }

        private static FitmentCheck Fail(FitmentStage stage, string conflict, string remedy)
        {
            return new FitmentCheck(stage, false, conflict, remedy);
        }

        private static bool Contains(string[] values, string candidate)
        {
            if (values == null)
            {
                return false;
            }

            foreach (var value in values)
            {
                if (StringComparer.Ordinal.Equals(value, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static FitmentClassification Max(FitmentClassification left, FitmentClassification right)
        {
            return (FitmentClassification)Math.Max((int)left, (int)right);
        }

        private static string ToMillimeters(double meters)
        {
            return Math.Max(0d, meters * 1000d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
