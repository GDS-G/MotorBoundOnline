using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public enum VehicleDesignClass
    {
        LowSportsCar = 0,
        CompactPassengerCar = 1,
        StandardPassengerCar = 2,
        SportUtilityVehicle = 3,
        PickupTruck = 4,
        HighRoofVan = 5,
        LightCommercialVehicle = 6,
        MediumTruck = 7,
        HeavyTruck = 8,
        Bus = 9,
        EmergencyVehicle = 10,
        Transporter = 11,
        ArticulatedCombination = 12,
        PassengerVehicleWithTrailer = 13,
        OversizePermitEnvelope = 14
    }

    public enum DimensionalCompatibilityStatus
    {
        Compatible = 0,
        Restricted = 1,
        Unverified = 2
    }

    public enum DimensionalConflictKind
    {
        Profile = 0,
        DesignClass = 1,
        Trailer = 2,
        Width = 3,
        Height = 4,
        Length = 5,
        Wheelbase = 6,
        CombinationLength = 7,
        GrossMass = 8,
        AxleLoad = 9,
        GroundClearance = 10,
        ApproachAngle = 11,
        DepartureAngle = 12,
        BreakoverAngle = 13,
        TurningCircle = 14
    }

    /// <summary>
    /// Live dimensions derived from the authoritative installed assembly. Accessories,
    /// cargo, suspension state, and a trailer may all change this record.
    /// </summary>
    [Serializable]
    public sealed class VehicleOperatingEnvelope
    {
        public StableId SourceManifestId;
        public int SourceManifestRevision = 1;
        public VehicleDesignClass DesignClass = VehicleDesignClass.StandardPassengerCar;
        public double OverallLengthMeters;
        public double BodyWidthMeters;
        public double MaximumWidthMeters;
        public double OverallHeightMeters;
        public double WheelbaseMeters;
        public double GroundClearanceMeters;
        public double ApproachAngleDegrees;
        public double DepartureAngleDegrees;
        public double BreakoverAngleDegrees;
        public double TurningCircleDiameterMeters;
        public double CombinationLengthMeters;
        public double GrossMassKilograms;
        public double MaximumAxleLoadKilograms;
        public bool HasTrailer;

        public IReadOnlyList<ValidationIssue> Validate(string pathPrefix = "operatingEnvelope")
        {
            var issues = new List<ValidationIssue>();
            Require(!SourceManifestId.IsEmpty, pathPrefix + ".sourceManifestId", "An authoritative assembly manifest ID is required.", issues);
            Require(SourceManifestRevision > 0, pathPrefix + ".sourceManifestRevision", "The source manifest revision must be positive.", issues);
            Require(OverallLengthMeters > 0d, pathPrefix + ".overallLengthMeters", "Overall length must be positive.", issues);
            Require(BodyWidthMeters > 0d, pathPrefix + ".bodyWidthMeters", "Body width must be positive.", issues);
            Require(MaximumWidthMeters >= BodyWidthMeters, pathPrefix + ".maximumWidthMeters", "Maximum width must include and not be narrower than the body.", issues);
            Require(OverallHeightMeters > 0d, pathPrefix + ".overallHeightMeters", "Overall height must be positive.", issues);
            Require(WheelbaseMeters > 0d && WheelbaseMeters < OverallLengthMeters, pathPrefix + ".wheelbaseMeters", "Wheelbase must be positive and shorter than overall length.", issues);
            Require(GroundClearanceMeters > 0d, pathPrefix + ".groundClearanceMeters", "Ground clearance must be positive.", issues);
            Require(ApproachAngleDegrees > 0d && ApproachAngleDegrees <= 90d, pathPrefix + ".approachAngleDegrees", "Approach angle must be in (0, 90].", issues);
            Require(DepartureAngleDegrees > 0d && DepartureAngleDegrees <= 90d, pathPrefix + ".departureAngleDegrees", "Departure angle must be in (0, 90].", issues);
            Require(BreakoverAngleDegrees > 0d && BreakoverAngleDegrees <= 180d, pathPrefix + ".breakoverAngleDegrees", "Breakover angle must be in (0, 180].", issues);
            Require(TurningCircleDiameterMeters > 0d, pathPrefix + ".turningCircleDiameterMeters", "Turning circle must be positive.", issues);
            Require(CombinationLengthMeters >= OverallLengthMeters, pathPrefix + ".combinationLengthMeters", "Combination length must include the vehicle's overall length.", issues);
            Require(GrossMassKilograms > 0d, pathPrefix + ".grossMassKilograms", "Gross mass must be positive.", issues);
            Require(MaximumAxleLoadKilograms > 0d && MaximumAxleLoadKilograms <= GrossMassKilograms, pathPrefix + ".maximumAxleLoadKilograms", "Maximum axle load must be positive and cannot exceed gross mass.", issues);
            Require(HasTrailer || Math.Abs(CombinationLengthMeters - OverallLengthMeters) <= 0.001d, pathPrefix + ".combinationLengthMeters", "A vehicle without a trailer must not report a longer combination envelope.", issues);
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

    /// <summary>
    /// Machine-readable route or facility restrictions. Zero-valued optional limits mean
    /// that the profile does not constrain that dimension, never that the dimension is unknown.
    /// Verified physical width and height are always required.
    /// </summary>
    [Serializable]
    public sealed class DimensionalRestrictionProfile
    {
        public StableId ProfileId;
        public int Revision = 1;
        public string DisplayName = string.Empty;
        public VehicleDesignClass[] PermittedDesignClasses = Array.Empty<VehicleDesignClass>();
        public bool PermitsTrailers;
        public double PhysicalClearWidthMeters;
        public double PhysicalClearHeightMeters;
        public double PostedClearHeightMeters;
        public double VerticalSafetyMarginMeters = 0.05d;
        public double LateralSafetyMarginMeters = 0.05d;
        public double MaximumPostedToPhysicalVarianceMeters = 0.15d;
        public double MaximumVehicleLengthMeters;
        public double MaximumWheelbaseMeters;
        public double MaximumCombinationLengthMeters;
        public double MaximumGrossMassKilograms;
        public double MaximumAxleLoadKilograms;
        public double MinimumGroundClearanceMeters;
        public double RequiredApproachAngleDegrees;
        public double RequiredDepartureAngleDegrees;
        public double RequiredBreakoverAngleDegrees;
        public double MaximumTurningCircleDiameterMeters;

        public IReadOnlyList<ValidationIssue> Validate(string pathPrefix = "restrictionProfile")
        {
            var issues = new List<ValidationIssue>();
            Require(!ProfileId.IsEmpty, pathPrefix + ".profileId", "A stable restriction-profile ID is required.", issues);
            Require(Revision > 0, pathPrefix + ".revision", "Restriction-profile revision must be positive.", issues);
            Require(!string.IsNullOrWhiteSpace(DisplayName), pathPrefix + ".displayName", "Restriction-profile display name is required.", issues);
            Require(PermittedDesignClasses != null && PermittedDesignClasses.Length > 0, pathPrefix + ".permittedDesignClasses", "At least one design vehicle class is required.", issues);
            Require(PhysicalClearWidthMeters > 0d, pathPrefix + ".physicalClearWidthMeters", "Verified physical clear width is required.", issues);
            Require(PhysicalClearHeightMeters > 0d, pathPrefix + ".physicalClearHeightMeters", "Verified physical clear height is required.", issues);
            Require(VerticalSafetyMarginMeters >= 0d && VerticalSafetyMarginMeters < PhysicalClearHeightMeters, pathPrefix + ".verticalSafetyMarginMeters", "Vertical safety margin must be nonnegative and below physical clearance.", issues);
            Require(LateralSafetyMarginMeters >= 0d && (2d * LateralSafetyMarginMeters) < PhysicalClearWidthMeters, pathPrefix + ".lateralSafetyMarginMeters", "Lateral safety margins must leave positive usable width.", issues);
            Require(MaximumPostedToPhysicalVarianceMeters >= 0d, pathPrefix + ".maximumPostedToPhysicalVarianceMeters", "Posted-to-physical variance cannot be negative.", issues);

            if (PostedClearHeightMeters > 0d)
            {
                var safePhysicalHeight = PhysicalClearHeightMeters - VerticalSafetyMarginMeters;
                Require(PostedClearHeightMeters <= safePhysicalHeight + 0.001d, pathPrefix + ".postedClearHeightMeters", "Posted clearance exceeds the verified physical clearance after safety margin.", issues);
                Require(safePhysicalHeight - PostedClearHeightMeters <= MaximumPostedToPhysicalVarianceMeters + 0.001d, pathPrefix + ".postedClearHeightMeters", "Posted clearance does not agree with the verified physical clearance within the permitted variance.", issues);
            }

            RequireNonnegative(MaximumVehicleLengthMeters, pathPrefix + ".maximumVehicleLengthMeters", issues);
            RequireNonnegative(MaximumWheelbaseMeters, pathPrefix + ".maximumWheelbaseMeters", issues);
            RequireNonnegative(MaximumCombinationLengthMeters, pathPrefix + ".maximumCombinationLengthMeters", issues);
            RequireNonnegative(MaximumGrossMassKilograms, pathPrefix + ".maximumGrossMassKilograms", issues);
            RequireNonnegative(MaximumAxleLoadKilograms, pathPrefix + ".maximumAxleLoadKilograms", issues);
            RequireNonnegative(MinimumGroundClearanceMeters, pathPrefix + ".minimumGroundClearanceMeters", issues);
            RequireNonnegative(RequiredApproachAngleDegrees, pathPrefix + ".requiredApproachAngleDegrees", issues);
            RequireNonnegative(RequiredDepartureAngleDegrees, pathPrefix + ".requiredDepartureAngleDegrees", issues);
            RequireNonnegative(RequiredBreakoverAngleDegrees, pathPrefix + ".requiredBreakoverAngleDegrees", issues);
            RequireNonnegative(MaximumTurningCircleDiameterMeters, pathPrefix + ".maximumTurningCircleDiameterMeters", issues);
            return issues;
        }

        public double EffectiveClearHeightMeters
        {
            get
            {
                var physical = Math.Max(0d, PhysicalClearHeightMeters - VerticalSafetyMarginMeters);
                return PostedClearHeightMeters > 0d ? Math.Min(physical, PostedClearHeightMeters) : physical;
            }
        }

        public double EffectiveClearWidthMeters => Math.Max(0d, PhysicalClearWidthMeters - (2d * LateralSafetyMarginMeters));

        private static void RequireNonnegative(double value, string path, ICollection<ValidationIssue> issues)
        {
            Require(value >= 0d, path, "Optional dimensional limits cannot be negative.", issues);
        }

        private static void Require(bool condition, string path, string message, ICollection<ValidationIssue> issues)
        {
            if (!condition)
            {
                issues.Add(new ValidationIssue(path, message));
            }
        }
    }

    [Serializable]
    public struct DimensionalConflict
    {
        public DimensionalConflict(DimensionalConflictKind kind, double requiredValue, double availableValue, string message)
        {
            Kind = kind;
            RequiredValue = requiredValue;
            AvailableValue = availableValue;
            Message = message ?? string.Empty;
        }

        public DimensionalConflictKind Kind;
        public double RequiredValue;
        public double AvailableValue;
        public string Message;
    }

    [Serializable]
    public sealed class DimensionalCompatibilityResult
    {
        public DimensionalCompatibilityStatus Status;
        public DimensionalConflict[] Conflicts = Array.Empty<DimensionalConflict>();
        public double EffectiveClearWidthMeters;
        public double EffectiveClearHeightMeters;
        public bool IsCompatible => Status == DimensionalCompatibilityStatus.Compatible;
    }

    public static class DimensionalCompatibilityEvaluator
    {
        public static DimensionalCompatibilityResult Evaluate(VehicleOperatingEnvelope vehicle, DimensionalRestrictionProfile profile)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var conflicts = new List<DimensionalConflict>();
            var vehicleIssues = vehicle.Validate();
            var profileIssues = profile.Validate();
            if (vehicleIssues.Count > 0 || profileIssues.Count > 0)
            {
                foreach (var issue in vehicleIssues)
                {
                    conflicts.Add(new DimensionalConflict(DimensionalConflictKind.Profile, 0d, 0d, issue.ToString()));
                }

                foreach (var issue in profileIssues)
                {
                    conflicts.Add(new DimensionalConflict(DimensionalConflictKind.Profile, 0d, 0d, issue.ToString()));
                }

                return Create(DimensionalCompatibilityStatus.Unverified, profile, conflicts);
            }

            if (!Contains(profile.PermittedDesignClasses, vehicle.DesignClass))
            {
                conflicts.Add(new DimensionalConflict(DimensionalConflictKind.DesignClass, (double)vehicle.DesignClass, 0d, "Vehicle design class is not permitted by this route or facility."));
            }

            if (vehicle.HasTrailer && !profile.PermitsTrailers)
            {
                conflicts.Add(new DimensionalConflict(DimensionalConflictKind.Trailer, 1d, 0d, "Trailers are not permitted by this route or facility."));
            }

            AddMaximumConflict(conflicts, DimensionalConflictKind.Width, vehicle.MaximumWidthMeters, profile.EffectiveClearWidthMeters, "Vehicle maximum width exceeds usable clear width.");
            AddMaximumConflict(conflicts, DimensionalConflictKind.Height, vehicle.OverallHeightMeters, profile.EffectiveClearHeightMeters, "Vehicle height exceeds usable clear height.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.Length, vehicle.OverallLengthMeters, profile.MaximumVehicleLengthMeters, "Vehicle length exceeds the supported maximum.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.Wheelbase, vehicle.WheelbaseMeters, profile.MaximumWheelbaseMeters, "Vehicle wheelbase exceeds the supported maximum.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.CombinationLength, vehicle.CombinationLengthMeters, profile.MaximumCombinationLengthMeters, "Vehicle combination length exceeds the supported maximum.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.GrossMass, vehicle.GrossMassKilograms, profile.MaximumGrossMassKilograms, "Vehicle gross mass exceeds the supported maximum.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.AxleLoad, vehicle.MaximumAxleLoadKilograms, profile.MaximumAxleLoadKilograms, "Vehicle axle load exceeds the supported maximum.");
            AddOptionalMinimumConflict(conflicts, DimensionalConflictKind.GroundClearance, vehicle.GroundClearanceMeters, profile.MinimumGroundClearanceMeters, "Vehicle ground clearance is below the required minimum.");
            AddOptionalMinimumConflict(conflicts, DimensionalConflictKind.ApproachAngle, vehicle.ApproachAngleDegrees, profile.RequiredApproachAngleDegrees, "Vehicle approach angle is below the facility requirement.");
            AddOptionalMinimumConflict(conflicts, DimensionalConflictKind.DepartureAngle, vehicle.DepartureAngleDegrees, profile.RequiredDepartureAngleDegrees, "Vehicle departure angle is below the facility requirement.");
            AddOptionalMinimumConflict(conflicts, DimensionalConflictKind.BreakoverAngle, vehicle.BreakoverAngleDegrees, profile.RequiredBreakoverAngleDegrees, "Vehicle breakover angle is below the facility requirement.");
            AddOptionalMaximumConflict(conflicts, DimensionalConflictKind.TurningCircle, vehicle.TurningCircleDiameterMeters, profile.MaximumTurningCircleDiameterMeters, "Vehicle turning circle exceeds the maneuver envelope.");

            return Create(
                conflicts.Count == 0 ? DimensionalCompatibilityStatus.Compatible : DimensionalCompatibilityStatus.Restricted,
                profile,
                conflicts);
        }

        private static DimensionalCompatibilityResult Create(
            DimensionalCompatibilityStatus status,
            DimensionalRestrictionProfile profile,
            List<DimensionalConflict> conflicts)
        {
            return new DimensionalCompatibilityResult
            {
                Status = status,
                Conflicts = conflicts.ToArray(),
                EffectiveClearWidthMeters = profile.EffectiveClearWidthMeters,
                EffectiveClearHeightMeters = profile.EffectiveClearHeightMeters
            };
        }

        private static void AddMaximumConflict(
            ICollection<DimensionalConflict> conflicts,
            DimensionalConflictKind kind,
            double required,
            double available,
            string message)
        {
            if (required > available + 0.000001d)
            {
                conflicts.Add(new DimensionalConflict(kind, required, available, message));
            }
        }

        private static void AddOptionalMaximumConflict(
            ICollection<DimensionalConflict> conflicts,
            DimensionalConflictKind kind,
            double required,
            double optionalMaximum,
            string message)
        {
            if (optionalMaximum > 0d)
            {
                AddMaximumConflict(conflicts, kind, required, optionalMaximum, message);
            }
        }

        private static void AddOptionalMinimumConflict(
            ICollection<DimensionalConflict> conflicts,
            DimensionalConflictKind kind,
            double available,
            double optionalMinimum,
            string message)
        {
            if (optionalMinimum > 0d && available + 0.000001d < optionalMinimum)
            {
                conflicts.Add(new DimensionalConflict(kind, optionalMinimum, available, message));
            }
        }

        private static bool Contains(VehicleDesignClass[] values, VehicleDesignClass candidate)
        {
            foreach (var value in values)
            {
                if (value == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
