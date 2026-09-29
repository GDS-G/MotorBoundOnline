using System;
using System.Collections.Generic;
using MotorBound.Foundation;

namespace MotorBound.Vehicle.Core
{
    public enum BuildPlanRequirementKind
    {
        HardRequirement = 0,
        Recommendation = 1,
        RiskMitigation = 2
    }

    public enum BuildPlanIssueKind
    {
        InvalidPlan = 0,
        InvalidStep = 1,
        DuplicateStep = 2,
        MissingDependency = 3,
        DependencyCycle = 4,
        MutuallyExclusiveSteps = 5
    }

    [Serializable]
    public sealed class BuildPlanStep
    {
        public StableId StepId;
        public StableId PartDefinitionId;
        public int PartDefinitionRevision = 1;
        public string DisplayName = string.Empty;
        public BuildPlanRequirementKind RequirementKind;
        public int Quantity = 1;
        public long EstimatedUnitCostMinorUnits;
        public StableId[] RequiredStepIds = Array.Empty<StableId>();
        public StableId[] MutuallyExclusiveStepIds = Array.Empty<StableId>();
        public string[] RequiredToolsAndServices = Array.Empty<string>();
        public string[] AffectedCalibrations = Array.Empty<string>();
        public string[] ValidationTests = Array.Empty<string>();
        public string AlternativeSummary = string.Empty;
        public string EligibilityConsequences = string.Empty;
        public bool IsReversible = true;
    }

    /// <summary>
    /// A proposed dependency-complete change set. This is intentionally separate from
    /// the installed VehicleAssemblyManifest and does not grant inventory or authority.
    /// </summary>
    [Serializable]
    public sealed class PowertrainBuildPlan
    {
        public StableId PlanId;
        public int Revision = 1;
        public StableId VehicleDefinitionId;
        public StableId SourceManifestId;
        public int SourceManifestRevision = 1;
        public string Goal = string.Empty;
        public BuildPlanStep[] Steps = Array.Empty<BuildPlanStep>();
    }

    [Serializable]
    public struct BuildPlanIssue
    {
        public BuildPlanIssue(BuildPlanIssueKind kind, StableId stepId, string message)
        {
            Kind = kind;
            StepId = stepId;
            Message = message ?? string.Empty;
        }

        public BuildPlanIssueKind Kind;
        public StableId StepId;
        public string Message;
    }

    [Serializable]
    public struct BillOfMaterialsLine
    {
        public BillOfMaterialsLine(StableId stepId, StableId partDefinitionId, int partDefinitionRevision, int quantity, long estimatedUnitCostMinorUnits)
        {
            StepId = stepId;
            PartDefinitionId = partDefinitionId;
            PartDefinitionRevision = partDefinitionRevision;
            Quantity = quantity;
            EstimatedUnitCostMinorUnits = estimatedUnitCostMinorUnits;
        }

        public StableId StepId;
        public StableId PartDefinitionId;
        public int PartDefinitionRevision;
        public int Quantity;
        public long EstimatedUnitCostMinorUnits;
    }

    [Serializable]
    public sealed class BuildPlanEvaluation
    {
        public bool IsValid;
        public BuildPlanIssue[] Issues = Array.Empty<BuildPlanIssue>();
        public StableId[] DependencyOrder = Array.Empty<StableId>();
        public BillOfMaterialsLine[] BillOfMaterials = Array.Empty<BillOfMaterialsLine>();
        public long EstimatedTotalCostMinorUnits;
    }

    public static class BuildPlanEvaluator
    {
        public static BuildPlanEvaluation Evaluate(PowertrainBuildPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var issues = new List<BuildPlanIssue>();
            var stepsById = new Dictionary<StableId, BuildPlanStep>();
            var inputOrder = new List<StableId>();

            ValidatePlanHeader(plan, issues);
            if (plan.Steps == null || plan.Steps.Length == 0)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "A build plan must contain at least one selected step."));
            }
            else
            {
                for (var index = 0; index < plan.Steps.Length; index++)
                {
                    var step = plan.Steps[index];
                    if (step == null)
                    {
                        issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, StableId.Empty, "Build plan step " + index + " is null."));
                        continue;
                    }

                    ValidateStep(step, issues);
                    if (!step.StepId.IsEmpty)
                    {
                        if (stepsById.ContainsKey(step.StepId))
                        {
                            issues.Add(new BuildPlanIssue(BuildPlanIssueKind.DuplicateStep, step.StepId, "Build plan contains the same step ID more than once."));
                        }
                        else
                        {
                            stepsById.Add(step.StepId, step);
                            inputOrder.Add(step.StepId);
                        }
                    }
                }
            }

            ValidateDependenciesAndExclusions(stepsById, inputOrder, issues);
            var dependencyOrder = TopologicalOrder(stepsById, inputOrder, issues);
            var billOfMaterials = CreateBillOfMaterials(stepsById, inputOrder, out var estimatedTotalCostMinorUnits);

            return new BuildPlanEvaluation
            {
                IsValid = issues.Count == 0,
                Issues = issues.ToArray(),
                DependencyOrder = dependencyOrder.ToArray(),
                BillOfMaterials = billOfMaterials.ToArray(),
                EstimatedTotalCostMinorUnits = estimatedTotalCostMinorUnits
            };
        }

        private static void ValidatePlanHeader(PowertrainBuildPlan plan, ICollection<BuildPlanIssue> issues)
        {
            if (plan.PlanId.IsEmpty)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "A stable build-plan ID is required."));
            }

            if (plan.Revision <= 0)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "Build-plan revision must be positive."));
            }

            if (plan.VehicleDefinitionId.IsEmpty)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "The target vehicle definition is required."));
            }

            if (plan.SourceManifestId.IsEmpty || plan.SourceManifestRevision <= 0)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "A versioned source assembly manifest is required."));
            }

            if (string.IsNullOrWhiteSpace(plan.Goal))
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidPlan, StableId.Empty, "A build goal is required."));
            }
        }

        private static void ValidateStep(BuildPlanStep step, ICollection<BuildPlanIssue> issues)
        {
            if (step.StepId.IsEmpty)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, StableId.Empty, "Every build-plan step requires a stable ID."));
            }

            if (step.PartDefinitionId.IsEmpty || step.PartDefinitionRevision <= 0)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, step.StepId, "Every build-plan step requires a versioned part definition."));
            }

            if (string.IsNullOrWhiteSpace(step.DisplayName))
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, step.StepId, "Every build-plan step requires a display name."));
            }

            if (step.Quantity <= 0)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, step.StepId, "Bill-of-materials quantity must be positive."));
            }

            if (step.EstimatedUnitCostMinorUnits < 0L)
            {
                issues.Add(new BuildPlanIssue(BuildPlanIssueKind.InvalidStep, step.StepId, "Estimated unit cost cannot be negative."));
            }
        }

        private static void ValidateDependenciesAndExclusions(
            IDictionary<StableId, BuildPlanStep> stepsById,
            IEnumerable<StableId> inputOrder,
            ICollection<BuildPlanIssue> issues)
        {
            var reportedExclusions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stepId in inputOrder)
            {
                var step = stepsById[stepId];
                if (step.RequiredStepIds != null)
                {
                    foreach (var requiredStepId in step.RequiredStepIds)
                    {
                        if (!stepsById.ContainsKey(requiredStepId))
                        {
                            issues.Add(new BuildPlanIssue(
                                BuildPlanIssueKind.MissingDependency,
                                stepId,
                                "Missing required build step " + requiredStepId + "."));
                        }
                    }
                }

                if (step.MutuallyExclusiveStepIds == null)
                {
                    continue;
                }

                foreach (var excludedStepId in step.MutuallyExclusiveStepIds)
                {
                    if (!stepsById.ContainsKey(excludedStepId))
                    {
                        continue;
                    }

                    var first = stepId.CompareTo(excludedStepId) <= 0 ? stepId : excludedStepId;
                    var second = stepId.CompareTo(excludedStepId) <= 0 ? excludedStepId : stepId;
                    var pairKey = first.Value + ":" + second.Value;
                    if (reportedExclusions.Add(pairKey))
                    {
                        issues.Add(new BuildPlanIssue(
                            BuildPlanIssueKind.MutuallyExclusiveSteps,
                            stepId,
                            "Selected build steps " + first + " and " + second + " are mutually exclusive."));
                    }
                }
            }
        }

        private static List<StableId> TopologicalOrder(
            IDictionary<StableId, BuildPlanStep> stepsById,
            IEnumerable<StableId> inputOrder,
            ICollection<BuildPlanIssue> issues)
        {
            var result = new List<StableId>();
            var state = new Dictionary<StableId, int>();
            var reportedCycles = new HashSet<StableId>();
            foreach (var stepId in inputOrder)
            {
                Visit(stepId, stepsById, state, result, issues, reportedCycles);
            }

            return result;
        }

        private static void Visit(
            StableId stepId,
            IDictionary<StableId, BuildPlanStep> stepsById,
            IDictionary<StableId, int> state,
            ICollection<StableId> result,
            ICollection<BuildPlanIssue> issues,
            ISet<StableId> reportedCycles)
        {
            if (state.TryGetValue(stepId, out var currentState))
            {
                if (currentState == 1 && reportedCycles.Add(stepId))
                {
                    issues.Add(new BuildPlanIssue(BuildPlanIssueKind.DependencyCycle, stepId, "Build-plan dependency cycle detected at step " + stepId + "."));
                }

                return;
            }

            state[stepId] = 1;
            var step = stepsById[stepId];
            if (step.RequiredStepIds != null)
            {
                foreach (var requiredStepId in step.RequiredStepIds)
                {
                    if (stepsById.ContainsKey(requiredStepId))
                    {
                        Visit(requiredStepId, stepsById, state, result, issues, reportedCycles);
                    }
                }
            }

            state[stepId] = 2;
            if (!result.Contains(stepId))
            {
                result.Add(stepId);
            }
        }

        private static List<BillOfMaterialsLine> CreateBillOfMaterials(
            IDictionary<StableId, BuildPlanStep> stepsById,
            IEnumerable<StableId> inputOrder,
            out long estimatedTotalCostMinorUnits)
        {
            var result = new List<BillOfMaterialsLine>();
            estimatedTotalCostMinorUnits = 0L;
            foreach (var stepId in inputOrder)
            {
                var step = stepsById[stepId];
                result.Add(new BillOfMaterialsLine(
                    step.StepId,
                    step.PartDefinitionId,
                    step.PartDefinitionRevision,
                    step.Quantity,
                    step.EstimatedUnitCostMinorUnits));
                estimatedTotalCostMinorUnits += step.EstimatedUnitCostMinorUnits * step.Quantity;
            }

            return result;
        }
    }
}
