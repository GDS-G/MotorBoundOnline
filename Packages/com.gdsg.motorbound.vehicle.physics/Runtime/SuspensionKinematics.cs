using System;
using UnityEngine;

namespace MotorBound.Vehicle.Physics
{
    /// <summary>Geometric extension rate for a chassis-mounted suspension ray at its ground contact.</summary>
    public static class SuspensionKinematics
    {
        public const float MinimumRayContactAlignment = 0.1f;
        private const double MinimumDirectionLength = 0.000001d;

        /// <summary>
        /// Positive means extension/rebound; negative means compression. Tangential
        /// travel cannot extend a ray on a plane, even when the chassis is tilted.
        /// Returns false and zero for invalid, opposing, or near-grazing contact axes.
        /// </summary>
        public static bool TryCalculateLengthRate(Vector3 chassisPointVelocity, Vector3 groundPointVelocity,
            Vector3 chassisUp, Vector3 contactNormal, out float lengthRateMetersPerSecond)
        {
            lengthRateMetersPerSecond = 0f;
            if (!IsFinite(chassisPointVelocity) || !IsFinite(groundPointVelocity)
                || !TryNormalize(chassisUp, out var up) || !TryNormalize(contactNormal, out var normal)) return false;
            var alignment = (double)up.x * normal.x + (double)up.y * normal.y + (double)up.z * normal.z;
            if (alignment <= MinimumRayContactAlignment) return false;

            // n dot (v_chassis_at_contact - v_ground_at_contact - up * lengthRate) = 0.
            // Compute in double so finite large inputs cannot overflow intermediate
            // velocity subtraction or axis normalization before the guarded float output.
            var normalVelocity = ((double)chassisPointVelocity.x - groundPointVelocity.x) * normal.x
                                 + ((double)chassisPointVelocity.y - groundPointVelocity.y) * normal.y
                                 + ((double)chassisPointVelocity.z - groundPointVelocity.z) * normal.z;
            var rate = normalVelocity / alignment;
            if (double.IsNaN(rate) || double.IsInfinity(rate) || Math.Abs(rate) > float.MaxValue) return false;
            lengthRateMetersPerSecond = (float)rate;
            return true;
        }

        private static bool TryNormalize(Vector3 direction, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(direction)) return false;
            var magnitude = Math.Sqrt((double)direction.x * direction.x
                                      + (double)direction.y * direction.y
                                      + (double)direction.z * direction.z);
            if (magnitude <= MinimumDirectionLength) return false;
            normalized = new Vector3((float)(direction.x / magnitude), (float)(direction.y / magnitude),
                (float)(direction.z / magnitude));
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
