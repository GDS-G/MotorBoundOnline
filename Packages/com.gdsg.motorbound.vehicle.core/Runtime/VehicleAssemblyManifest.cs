using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public enum AssemblyInstallState
    {
        Planned = 0,
        PartsAcquired = 1,
        Staged = 2,
        Fitted = 3,
        Fastened = 4,
        ServicesConnected = 5,
        FluidsAndAdjustmentsCompleted = 6,
        Tested = 7,
        Released = 8
    }

    public enum AssemblyRelationshipKind
    {
        MechanicalAttachment = 0,
        RotationOrArticulation = 1,
        PowerFlow = 2,
        FluidFlow = 3,
        ElectricalCircuit = 4,
        ControlSignal = 5,
        BodySeam = 6,
        AssemblyDependency = 7
    }

    /// <summary>
    /// One physical part instance. Catalog data describes the nominal product; this
    /// record owns the serialized instance, installed pose, condition, and service state.
    /// </summary>
    [Serializable]
    public sealed class InstalledPartInstance
    {
        public StableId InstanceId;
        public StableId PartDefinitionId;
        public int PartDefinitionRevision = 1;
        public string DisplayName = string.Empty;
        public AssemblyInstallState InstallState;
        public double LocalPositionXMeters;
        public double LocalPositionYMeters;
        public double LocalPositionZMeters;
        public double LocalRotationXRadians;
        public double LocalRotationYRadians;
        public double LocalRotationZRadians;
        public double Condition01 = 1d;
        public double MassKilograms;
        public bool OwnsMass = true;
        public StableId MassAccountingGroupId;
        public long CostMinorUnits;
        public bool OwnsCost = true;
        public StableId[] IncludedPartDefinitionIds = Array.Empty<StableId>();
    }

    [Serializable]
    public sealed class AssemblyRelationship
    {
        public StableId RelationshipId;
        public AssemblyRelationshipKind Kind;
        public StableId FromPartInstanceId;
        public StableId ToPartInstanceId;
        public string FromPortId = string.Empty;
        public string ToPortId = string.Empty;
        public string InterfaceFamilyAndVersion = string.Empty;
        public bool IsSatisfied;
    }

    /// <summary>
    /// Server-safe source of truth for what is installed. Rendering, physics, audio,
    /// diagnostics, inventory, damage, legality, and service records derive from it.
    /// </summary>
    [Serializable]
    public sealed class VehicleAssemblyManifest
    {
        public StableId ManifestId;
        public int Revision = 1;
        public StableId VehicleDefinitionId;
        public InstalledPartInstance[] Parts = Array.Empty<InstalledPartInstance>();
        public AssemblyRelationship[] Relationships = Array.Empty<AssemblyRelationship>();

        public InstalledPartInstance FindPart(StableId instanceId)
        {
            if (Parts == null)
            {
                return null;
            }

            foreach (var part in Parts)
            {
                if (part != null && part.InstanceId == instanceId)
                {
                    return part;
                }
            }

            return null;
        }

        public double ComputeAuthoritativeMassKilograms()
        {
            var result = 0d;
            if (Parts == null)
            {
                return result;
            }

            foreach (var part in Parts)
            {
                if (part != null && part.OwnsMass)
                {
                    result += Math.Max(0d, part.MassKilograms);
                }
            }

            return result;
        }

        public long ComputeAuthoritativeCostMinorUnits()
        {
            long result = 0;
            if (Parts == null)
            {
                return result;
            }

            foreach (var part in Parts)
            {
                if (part != null && part.OwnsCost)
                {
                    result += Math.Max(0L, part.CostMinorUnits);
                }
            }

            return result;
        }

        public IReadOnlyList<ValidationIssue> Validate(string pathPrefix = "assemblyManifest")
        {
            var issues = new List<ValidationIssue>();
            Require(!ManifestId.IsEmpty, pathPrefix + ".manifestId", "A stable manifest ID is required.", issues);
            Require(Revision > 0, pathPrefix + ".revision", "Manifest revision must be positive.", issues);
            Require(!VehicleDefinitionId.IsEmpty, pathPrefix + ".vehicleDefinitionId", "The vehicle definition ID is required.", issues);
            Require(Parts != null && Parts.Length > 0, pathPrefix + ".parts", "The manifest must contain at least one physical part instance.", issues);

            var partIds = new HashSet<StableId>();
            var massOwners = new Dictionary<StableId, StableId>();
            if (Parts != null)
            {
                for (var index = 0; index < Parts.Length; index++)
                {
                    var part = Parts[index];
                    var path = pathPrefix + ".parts[" + index + "]";
                    if (part == null)
                    {
                        issues.Add(new ValidationIssue(path, "Part instance cannot be null."));
                        continue;
                    }

                    Require(!part.InstanceId.IsEmpty, path + ".instanceId", "A stable physical part-instance ID is required.", issues);
                    Require(!part.PartDefinitionId.IsEmpty, path + ".partDefinitionId", "A stable catalog part-definition ID is required.", issues);
                    Require(part.PartDefinitionRevision > 0, path + ".partDefinitionRevision", "Part-definition revision must be positive.", issues);
                    Require(!string.IsNullOrWhiteSpace(part.DisplayName), path + ".displayName", "Part display name is required.", issues);
                    Require(part.Condition01 >= 0d && part.Condition01 <= 1d, path + ".condition01", "Part condition must be in [0, 1].", issues);
                    Require(part.MassKilograms >= 0d, path + ".massKilograms", "Part mass cannot be negative.", issues);
                    Require(part.CostMinorUnits >= 0L, path + ".costMinorUnits", "Part cost cannot be negative.", issues);

                    if (!part.InstanceId.IsEmpty && !partIds.Add(part.InstanceId))
                    {
                        issues.Add(new ValidationIssue(path + ".instanceId", "Duplicate physical part-instance ID."));
                    }

                    if (part.OwnsMass && part.MassKilograms > 0d)
                    {
                        Require(!part.MassAccountingGroupId.IsEmpty, path + ".massAccountingGroupId", "A mass-owning part must declare its accounting group.", issues);
                        if (!part.MassAccountingGroupId.IsEmpty)
                        {
                            if (massOwners.TryGetValue(part.MassAccountingGroupId, out var existingOwner))
                            {
                                issues.Add(new ValidationIssue(
                                    path + ".massAccountingGroupId",
                                    "Mass accounting group is already owned by part instance " + existingOwner + "; complete assemblies and their constituents cannot both contribute mass."));
                            }
                            else
                            {
                                massOwners.Add(part.MassAccountingGroupId, part.InstanceId);
                            }
                        }
                    }
                }
            }

            var relationshipIds = new HashSet<StableId>();
            if (Relationships != null)
            {
                for (var index = 0; index < Relationships.Length; index++)
                {
                    var relationship = Relationships[index];
                    var path = pathPrefix + ".relationships[" + index + "]";
                    if (relationship == null)
                    {
                        issues.Add(new ValidationIssue(path, "Assembly relationship cannot be null."));
                        continue;
                    }

                    Require(!relationship.RelationshipId.IsEmpty, path + ".relationshipId", "A stable relationship ID is required.", issues);
                    if (!relationship.RelationshipId.IsEmpty && !relationshipIds.Add(relationship.RelationshipId))
                    {
                        issues.Add(new ValidationIssue(path + ".relationshipId", "Duplicate assembly relationship ID."));
                    }

                    Require(partIds.Contains(relationship.FromPartInstanceId), path + ".fromPartInstanceId", "Relationship source part is not present in the manifest.", issues);
                    Require(partIds.Contains(relationship.ToPartInstanceId), path + ".toPartInstanceId", "Relationship destination part is not present in the manifest.", issues);
                    Require(relationship.FromPartInstanceId != relationship.ToPartInstanceId, path, "A relationship cannot connect a part instance to itself.", issues);
                    Require(!string.IsNullOrWhiteSpace(relationship.InterfaceFamilyAndVersion), path + ".interfaceFamilyAndVersion", "Relationship interface family and version are required.", issues);
                }
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
