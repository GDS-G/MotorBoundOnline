using System;
using MotorBound.Vehicle.Core;

namespace MotorBound.Vehicle.Physics
{
    public struct AxleContactInput
    {
        public WheelContactInput Left;
        public WheelContactInput Right;
        public DifferentialDefinition Differential;
        public double AxleDriveTorqueNewtonMeters;
    }

    public struct AxleContactResult
    {
        public WheelContactResult Left;
        public WheelContactResult Right;
        public double LeftMeanDriveTorqueNewtonMeters;
        public double RightMeanDriveTorqueNewtonMeters;
        public double MeanDifferentialTransferTorqueNewtonMeters;
        public double DifferentialDissipatedEnergyJoules;
    }

    /// <summary>Shared tire/wheel microsteps with a passive mechanical differential, including unloaded wheels.</summary>
    public static class AxleContactSolver
    {
        public static AxleContactResult Solve(AxleContactInput input)
        {
            WheelContactSolver.ValidateInput(input.Left);
            WheelContactSolver.ValidateInput(input.Right);
            if (input.Left.DeltaTimeSeconds != input.Right.DeltaTimeSeconds)
                throw new ArgumentException("Axle wheels must use the same timestep.");
            if (double.IsNaN(input.AxleDriveTorqueNewtonMeters) || double.IsInfinity(input.AxleDriveTorqueNewtonMeters))
                throw new ArgumentOutOfRangeException(nameof(input.AxleDriveTorqueNewtonMeters));
            var substeps = Math.Max(WheelContactSolver.CalculateSubstepCount(input.Left), WheelContactSolver.CalculateSubstepCount(input.Right));
            var stepSeconds = input.Left.DeltaTimeSeconds / substeps;
            var baseTorque = input.AxleDriveTorqueNewtonMeters * 0.5d;
            var leftSpeed = input.Left.AngularSpeedRadiansPerSecond;
            var rightSpeed = input.Right.AngularSpeedRadiansPerSecond;
            var leftLongitudinalSum = 0d;
            var leftLateralSum = 0d;
            var rightLongitudinalSum = 0d;
            var rightLateralSum = 0d;
            var leftBrakeImpulse = 0d;
            var rightBrakeImpulse = 0d;
            var leftLimitImpulse = 0d;
            var rightLimitImpulse = 0d;
            var transferImpulse = 0d;
            var dissipatedEnergy = 0d;
            for (var step = 0; step < substeps; step++)
            {
                var leftForce = WheelContactSolver.EvaluateDeliveredForce(input.Left, leftSpeed);
                var rightForce = WheelContactSolver.EvaluateDeliveredForce(input.Right, rightSpeed);
                // Advance external drive and contact reaction together. The differential acts
                // on these provisional rotations, so even a coarse capacity cannot add energy.
                var leftProvisional = leftSpeed + (baseTorque - leftForce.LongitudinalForceNewtons * input.Left.RadiusMeters)
                    * stepSeconds / input.Left.RotationalInertiaKilogramMetersSquared;
                var rightProvisional = rightSpeed + (baseTorque - rightForce.LongitudinalForceNewtons * input.Right.RadiusMeters)
                    * stepSeconds / input.Right.RotationalInertiaKilogramMetersSquared;
                var differential = DifferentialModel.Evaluate(new DifferentialInput
                {
                    Definition = input.Differential,
                    AxleDriveTorqueNewtonMeters = input.AxleDriveTorqueNewtonMeters,
                    LeftAngularSpeedRadiansPerSecond = leftProvisional,
                    RightAngularSpeedRadiansPerSecond = rightProvisional,
                    LeftInertiaKilogramMetersSquared = input.Left.RotationalInertiaKilogramMetersSquared,
                    RightInertiaKilogramMetersSquared = input.Right.RotationalInertiaKilogramMetersSquared,
                    DeltaTimeSeconds = stepSeconds
                });
                var impulse = differential.TransferAngularImpulseNewtonMeterSeconds;
                leftProvisional -= impulse / input.Left.RotationalInertiaKilogramMetersSquared;
                rightProvisional += impulse / input.Right.RotationalInertiaKilogramMetersSquared;
                // Static brakes act after clutch transfer; sufficient handbrake torque still
                // holds both wheels at zero rather than having a post-step lock unlock them.
                leftSpeed = WheelContactSolver.ApplyBrakeAndLimit(input.Left, leftProvisional, stepSeconds, ref leftBrakeImpulse, ref leftLimitImpulse);
                rightSpeed = WheelContactSolver.ApplyBrakeAndLimit(input.Right, rightProvisional, stepSeconds, ref rightBrakeImpulse, ref rightLimitImpulse);
                leftLongitudinalSum += leftForce.LongitudinalForceNewtons;
                leftLateralSum += leftForce.LateralForceNewtons;
                rightLongitudinalSum += rightForce.LongitudinalForceNewtons;
                rightLateralSum += rightForce.LateralForceNewtons;
                transferImpulse += impulse;
                dissipatedEnergy += differential.DissipatedEnergyJoules;
            }
            var meanTransfer = transferImpulse / input.Left.DeltaTimeSeconds;
            return new AxleContactResult
            {
                Left = WheelContactSolver.BuildResult(input.Left, leftSpeed, leftLongitudinalSum, leftLateralSum, substeps, leftBrakeImpulse, leftLimitImpulse),
                Right = WheelContactSolver.BuildResult(input.Right, rightSpeed, rightLongitudinalSum, rightLateralSum, substeps, rightBrakeImpulse, rightLimitImpulse),
                LeftMeanDriveTorqueNewtonMeters = baseTorque - meanTransfer,
                RightMeanDriveTorqueNewtonMeters = baseTorque + meanTransfer,
                MeanDifferentialTransferTorqueNewtonMeters = meanTransfer,
                DifferentialDissipatedEnergyJoules = dissipatedEnergy
            };
        }
    }
}
