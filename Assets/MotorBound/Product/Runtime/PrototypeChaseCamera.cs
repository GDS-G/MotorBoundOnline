using UnityEngine;

namespace MotorBound.Product
{
    [DisallowMultipleComponent]
    public sealed class PrototypeChaseCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.7f, -6.8f);
        [SerializeField] private float positionSharpness = 8f;
        [SerializeField] private float rotationSharpness = 10f;
        private Transform target;
        private PrototypeGarageSession garage;
        private Rigidbody targetBody;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            garage = target.GetComponent<PrototypeGarageSession>();
            targetBody = target.GetComponent<Rigidbody>();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            GetView(out var desiredPosition, out var lookTarget);
            var positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);
            var desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            var rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            GetView(out var position, out var lookTarget);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
        }

        private void GetView(out Vector3 position, out Vector3 lookTarget)
        {
            var parked = garage != null && garage.IsInGarage;
            position = parked
                ? target.position + new Vector3(2.6f, 2.5f, 4f)
                : target.TransformPoint(localOffset);
            lookTarget = target.position + Vector3.up * 0.4f + (parked ? Vector3.zero : target.forward * 3.2f);

            // Keep the chase camera on this side of workshop walls and track barriers.
            var origin = target.position + Vector3.up * 0.55f;
            var offset = position - origin;
            var distance = offset.magnitude;
            foreach (var hit in UnityEngine.Physics.RaycastAll(origin, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == targetBody || hit.collider.transform.IsChildOf(target)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0.3f, hit.distance - 0.25f));
            }
            position = origin + offset.normalized * distance;
        }
    }
}
