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

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.TransformPoint(localOffset);
            var positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);
            var lookTarget = target.position + (target.forward * 3.2f) + (Vector3.up * 0.4f);
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

            transform.position = target.TransformPoint(localOffset);
            transform.rotation = Quaternion.LookRotation((target.position + (Vector3.up * 0.4f)) - transform.position, Vector3.up);
        }
    }
}
