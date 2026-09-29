using System.Linq;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class BuildPlanEvaluatorTests
    {
        [Test]
        public void ReferenceFactoryPlan_IsDependencyCompleteWithBillOfMaterials()
        {
            var plan = ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan();

            var result = BuildPlanEvaluator.Evaluate(plan);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.BillOfMaterials, Has.Length.EqualTo(6));
            Assert.That(result.DependencyOrder.Last(), Is.EqualTo(plan.Steps.Last().StepId));
            Assert.That(result.EstimatedTotalCostMinorUnits, Is.EqualTo(1255000L));
        }

        [Test]
        public void MissingRequiredCoolingStep_IsReported()
        {
            var plan = ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan();
            var coolingStepId = plan.Steps[1].StepId;
            plan.Steps = plan.Steps.Where(value => value.StepId != coolingStepId).ToArray();

            var result = BuildPlanEvaluator.Evaluate(plan);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(value => value.Kind == BuildPlanIssueKind.MissingDependency), Is.True);
        }

        [Test]
        public void CircularDependency_IsRejected()
        {
            var plan = ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan();
            plan.Steps[0].RequiredStepIds = new[] { plan.Steps[plan.Steps.Length - 1].StepId };

            var result = BuildPlanEvaluator.Evaluate(plan);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(value => value.Kind == BuildPlanIssueKind.DependencyCycle), Is.True);
        }

        [Test]
        public void MutuallyExclusiveSelections_AreRejected()
        {
            var plan = ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan();
            plan.Steps[0].MutuallyExclusiveStepIds = new[] { plan.Steps[1].StepId };

            var result = BuildPlanEvaluator.Evaluate(plan);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(value => value.Kind == BuildPlanIssueKind.MutuallyExclusiveSteps), Is.True);
        }
    }
}
