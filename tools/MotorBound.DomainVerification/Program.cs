using System;
using System.Collections.Generic;
using System.Linq;
using MotorBound.Foundation;
using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;

internal static class Program
{
    private static readonly List<string> Results = new List<string>();

    private static int Main()
    {
        try
        {
            VerifyStableIdentity();
            VerifyUnits();
            VerifyReferenceVehicle();
            VerifyTorqueInterpolation();
            VerifyTireEnvelope();
            VerifyWetGripReduction();
            VerifyArchitectureIdentity();
            VerifyFitmentPipeline();
            VerifyWaterFilmResponse();
            VerifyAssemblyManifest();
            VerifyDimensionalCompatibility();
            VerifyBuildPlanDependencies();
            VerifyManifestMassOwnershipGuard();
            VerifyClearanceGuardrails();
            VerifyIncompleteBuildPlanRejection();

            foreach (var result in Results)
            {
                Console.WriteLine("PASS " + result);
            }

            Console.WriteLine("Domain verification passed: " + Results.Count + " checks.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception.Message);
            return 1;
        }
    }

    private static void VerifyStableIdentity()
    {
        var first = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-club-prototype");
        var second = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-club-prototype");
        Require(first == second && !first.IsEmpty, "Stable catalog identity is not deterministic.");
        Results.Add("stable catalog identity");
    }

    private static void VerifyUnits()
    {
        Require(Math.Abs(UnitConversion.ToKilometersPerHour(10d) - 36d) < 0.000001d, "m/s to km/h conversion is wrong.");
        var sourceRpm = 6500d;
        var roundTrip = UnitConversion.RadiansPerSecondToRpm(UnitConversion.RpmToRadiansPerSecond(sourceRpm));
        Require(Math.Abs(roundTrip - sourceRpm) < 0.000001d, "RPM conversion does not round-trip.");
        Results.Add("SI boundary conversions");
    }

    private static void VerifyReferenceVehicle()
    {
        var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
        Require(!definition.Validate().Any(), "Reference Kiyora Aven definition is invalid.");
        Require(definition.DriveLayout == DriveLayout.RearWheelDrive, "Reference Kiyora Aven must be rear-wheel drive.");
        Results.Add("reference vehicle validation");
    }

    private static void VerifyTorqueInterpolation()
    {
        var engine = new EngineDefinition
        {
            FullLoadTorqueCurve = new[]
            {
                new TorqueSample(1000d, 100d),
                new TorqueSample(3000d, 200d)
            }
        };
        Require(Math.Abs(engine.EvaluateFullLoadTorqueNewtonMeters(2000d) - 150d) < 0.000001d, "Torque interpolation is incorrect.");
        Results.Add("torque-map interpolation");
    }

    private static void VerifyTireEnvelope()
    {
        var result = TireForceModel.Evaluate(CreateTireInput(1.5d, 0.5d, 1d));
        var magnitude = Math.Sqrt(
            (result.LongitudinalForceNewtons * result.LongitudinalForceNewtons)
            + (result.LateralForceNewtons * result.LateralForceNewtons));
        Require(magnitude <= result.MaximumCombinedForceNewtons + 0.000001d, "Combined tire force exceeded its grip envelope.");
        Results.Add("combined tire-force envelope");
    }

    private static void VerifyWetGripReduction()
    {
        var dry = TireForceModel.Evaluate(CreateTireInput(1d, 0d, 1d));
        var wet = TireForceModel.Evaluate(CreateTireInput(1d, 0d, 0.58d));
        Require(wet.MaximumCombinedForceNewtons < dry.MaximumCombinedForceNewtons, "Wet surface did not reduce peak tire force.");
        Results.Add("wet-surface grip reduction");
    }

    private static void VerifyArchitectureIdentity()
    {
        var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
        Require(!definition.Architecture.PlatformId.IsEmpty, "Reference vehicle has no platform identity.");
        Require(!definition.Architecture.TrimManifestId.IsEmpty, "Reference vehicle has no trim-manifest identity.");
        Results.Add("layered vehicle architecture identity");
    }

    private static void VerifyFitmentPipeline()
    {
        var result = FitmentValidator.Evaluate(
            ReferenceVehicleCatalog.CreateKiyoraAvenClubFitmentContext(),
            ReferenceVehicleCatalog.CreateKiyoraAvenReferenceFrontWheelFitment());
        Require(result.Classification == FitmentClassification.BoltIn, "Reference front wheel is not classified bolt-in.");
        Require(result.Checks.Length == 10, "Fitment pipeline did not execute all ten stages.");
        Results.Add("ten-stage physical fitment pipeline");
    }

    private static void VerifyWaterFilmResponse()
    {
        var dry = SurfaceConditionModel.Evaluate(CreateSurfaceInput(0d));
        var standingWater = SurfaceConditionModel.Evaluate(CreateSurfaceInput(4d));
        Require(standingWater.EffectiveGripMultiplier < dry.EffectiveGripMultiplier, "Standing water did not reduce effective grip.");
        Results.Add("speed-sensitive water-film response");
    }

    private static void VerifyAssemblyManifest()
    {
        var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();
        Require(!manifest.Validate().Any(), "Reference vehicle assembly manifest is invalid.");
        Require(Math.Abs(manifest.ComputeAuthoritativeMassKilograms() - 1120d) < 0.000001d, "Assembly manifest double-counts or omits reference mass.");
        Results.Add("authoritative vehicle assembly manifest");
    }

    private static void VerifyDimensionalCompatibility()
    {
        var result = DimensionalCompatibilityEvaluator.Evaluate(
            ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope(),
            ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile());
        Require(result.Status == DimensionalCompatibilityStatus.Compatible, "Reference Kiyora Aven does not fit the verified passenger garage.");
        Results.Add("vehicle-envelope facility compatibility");
    }

    private static void VerifyBuildPlanDependencies()
    {
        var result = BuildPlanEvaluator.Evaluate(ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan());
        Require(result.IsValid, "Reference powertrain build plan is incomplete.");
        Require(result.BillOfMaterials.Length == 6, "Reference powertrain bill of materials is incomplete.");
        Results.Add("dependency-complete powertrain build plan");
    }

    private static void VerifyManifestMassOwnershipGuard()
    {
        var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();
        manifest.Parts[2].MassAccountingGroupId = manifest.Parts[1].MassAccountingGroupId;
        Require(manifest.Validate().Any(issue => issue.Message.Contains("cannot both contribute mass")), "Manifest accepted duplicate mass ownership.");
        Results.Add("assembly mass-ownership guard");
    }

    private static void VerifyClearanceGuardrails()
    {
        var vehicle = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeOperatingEnvelope();
        vehicle.OverallHeightMeters = 2.04d;
        var profile = ReferenceVehicleCatalog.CreateStandardPassengerGarageProfile();
        var restricted = DimensionalCompatibilityEvaluator.Evaluate(vehicle, profile);
        Require(restricted.Status == DimensionalCompatibilityStatus.Restricted, "Low garage accepted an overheight vehicle.");

        profile.PostedClearHeightMeters = 2.08d;
        var unverified = DimensionalCompatibilityEvaluator.Evaluate(vehicle, profile);
        Require(unverified.Status == DimensionalCompatibilityStatus.Unverified, "Contradictory posted and physical clearance was not quarantined.");
        Results.Add("posted and physical clearance guardrails");
    }

    private static void VerifyIncompleteBuildPlanRejection()
    {
        var plan = ReferenceVehicleCatalog.CreateKiyoraAvenFactoryPowertrainBuildPlan();
        var removedStepId = plan.Steps[1].StepId;
        plan.Steps = plan.Steps.Where(step => step.StepId != removedStepId).ToArray();
        var result = BuildPlanEvaluator.Evaluate(plan);
        Require(!result.IsValid && result.Issues.Any(issue => issue.Kind == BuildPlanIssueKind.MissingDependency), "Incomplete powertrain build plan was accepted.");
        Results.Add("incomplete powertrain plan rejection");
    }

    private static SurfaceConditionInput CreateSurfaceInput(double waterDepthMillimeters)
    {
        return new SurfaceConditionInput
        {
            BaseGripMultiplier = 1d,
            WaterFilmDepthMillimeters = waterDepthMillimeters,
            VehicleSpeedMetersPerSecond = 30d,
            TireWaterEvacuationFactor = 0.6d,
            RoughnessFactor = 0d
        };
    }

    private static TireForceInput CreateTireInput(double slipRatio, double slipAngle, double surfaceGrip)
    {
        return new TireForceInput
        {
            NormalLoadNewtons = 3200d,
            SlipRatio = slipRatio,
            SlipAngleRadians = slipAngle,
            PeakDryFrictionCoefficient = 1.08d,
            SurfaceGripMultiplier = surfaceGrip,
            LongitudinalSlipStiffnessNewtonPerRatio = 80000d,
            CorneringStiffnessNewtonPerRadian = 65000d,
            ReferenceLoadNewtons = 3300d,
            LoadSensitivityExponent = -0.08d
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
