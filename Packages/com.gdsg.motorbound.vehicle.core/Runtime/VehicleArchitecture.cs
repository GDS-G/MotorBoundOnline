using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    /// <summary>
    /// Stable identities for the authored layers that lead to one buildable vehicle.
    /// Display names may change without changing any of these identities.
    /// </summary>
    [Serializable]
    public sealed class VehicleArchitectureIdentity
    {
        public StableId FamilyId;
        public StableId GenerationId;
        public StableId PlatformId;
        public StableId BodyShellId;
        public StableId ChassisConfigurationId;
        public StableId PowertrainConfigurationId;
        public StableId InteriorConfigurationId;
        public StableId TrimManifestId;

        public IReadOnlyList<ValidationIssue> Validate(string pathPrefix = "architecture")
        {
            var issues = new List<ValidationIssue>();
            Require(FamilyId, pathPrefix + ".familyId", "A vehicle-family ID is required.", issues);
            Require(GenerationId, pathPrefix + ".generationId", "A vehicle-generation ID is required.", issues);
            Require(PlatformId, pathPrefix + ".platformId", "A platform ID is required.", issues);
            Require(BodyShellId, pathPrefix + ".bodyShellId", "A body-shell ID is required.", issues);
            Require(ChassisConfigurationId, pathPrefix + ".chassisConfigurationId", "A chassis-configuration ID is required.", issues);
            Require(PowertrainConfigurationId, pathPrefix + ".powertrainConfigurationId", "A powertrain-configuration ID is required.", issues);
            Require(InteriorConfigurationId, pathPrefix + ".interiorConfigurationId", "An interior-configuration ID is required.", issues);
            Require(TrimManifestId, pathPrefix + ".trimManifestId", "A trim-manifest ID is required.", issues);
            return issues;
        }

        private static void Require(StableId id, string path, string message, ICollection<ValidationIssue> issues)
        {
            if (id.IsEmpty)
            {
                issues.Add(new ValidationIssue(path, message));
            }
        }
    }

    /// <summary>
    /// The shared authoring convention for every platform and component package.
    /// </summary>
    public static class VehicleCoordinateSystem
    {
        public const string LinearUnit = "meter";
        public const string MassUnit = "kilogram";
        public const string AngularUnit = "radian";
        public const string ForwardAxis = "+Z";
        public const string UpAxis = "+Y";
        public const string RightAxis = "+X";
        public const double AuthoringScale = 1d;
    }
}
