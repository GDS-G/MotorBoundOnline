using System;
using MotorBound.Vehicle.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotorBound.Product
{
    /// <summary>Bounded tire marks driven by actual sliding contacts, independently of steering input.</summary>
    // Native EditMode validation creates these visual resources outside Play mode; receive lifecycle cleanup there too.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PrototypeSkidTrails : MonoBehaviour
    {
        private const int MaximumSegments = 2048;
        private const int WheelCount = 4;
        private const float MinimumSpeedMetersPerSecond = 2f;
        private const float MinimumSegmentLengthMeters = 0.08f;
        private const float MaximumSegmentLengthMeters = 2.5f;
        private const float SurfaceOffsetMeters = 0.012f;

        private readonly ContactAnchor[] anchors = new ContactAnchor[WheelCount];
        private RaycastVehicleController controller;
        private GameObject trailRoot;
        private Mesh trailMesh;
        private Material trailMaterial;
        private Vector3[] vertices;
        private Vector3[] normals;
        private int nextSegment;
        private bool meshDirty;
        private bool hasVehiclePosition;
        private Vector3 previousVehiclePosition;

        public int SegmentCount { get; private set; }
        public Mesh TrailMesh => trailMesh;
        public const string RubberShaderResourcePath = "PrototypeSkidRubber";

        public void Configure(RaycastVehicleController target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // A Resources shader is an explicit player-build dependency. Editor-only
            // Shader.Find("Standard") succeeded in tests but was stripped from 0.2.2.
            var rubberShader = Resources.Load<Shader>(RubberShaderResourcePath)
                ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Unlit/Color");
            if (rubberShader == null)
            {
                Debug.LogError("Prototype skid marks disabled: no retained rubber shader is available.", this);
                return;
            }
            controller = target;
            BreakContacts();
            hasVehiclePosition = false;
            if (trailRoot != null) return;
            nextSegment = 0;
            SegmentCount = 0;
            meshDirty = false;

            // Vertex positions are in world metres; the mark root never follows or scales with the car.
            trailRoot = new GameObject("Prototype sliding tire marks", typeof(MeshFilter), typeof(MeshRenderer));
            trailRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            trailRoot.transform.localScale = Vector3.one;
            trailMesh = new Mesh { name = "Bounded prototype tire marks" };
            trailMesh.MarkDynamic();
            vertices = new Vector3[MaximumSegments * 4];
            normals = new Vector3[vertices.Length];
            var triangles = new int[MaximumSegments * 6];
            for (var segment = 0; segment < MaximumSegments; segment++)
            {
                var vertex = segment * 4;
                var triangle = segment * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            trailMesh.vertices = vertices;
            trailMesh.normals = normals;
            trailMesh.triangles = triangles;
            trailRoot.GetComponent<MeshFilter>().sharedMesh = trailMesh;
            trailMaterial = new Material(rubberShader)
            {
                name = "Prototype skid rubber",
                color = new Color(0.022f, 0.024f, 0.025f, 1f)
            };
            if (trailMaterial.HasProperty("_Glossiness")) trailMaterial.SetFloat("_Glossiness", 0.02f);
            var renderer = trailRoot.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = trailMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void LateUpdate()
        {
            // Editor validation samples explicitly. Editing a scene or prefab must not draw gameplay marks.
            if (Application.IsPlaying(gameObject)) SampleContacts();
        }

        // Shared with manual native driving validation, which does not dispatch rendered-frame callbacks.
        public void SampleContacts()
        {
            if (!isActiveAndEnabled || controller == null || controller.Definition == null
                || controller.SimulationPaused || trailMesh == null)
            {
                BreakContacts();
                hasVehiclePosition = false;
                return;
            }

            var vehiclePosition = controller.transform.position;
            if (hasVehiclePosition
                && (vehiclePosition - previousVehiclePosition).sqrMagnitude > MaximumSegmentLengthMeters * MaximumSegmentLengthMeters)
            {
                BreakContacts();
            }
            previousVehiclePosition = vehiclePosition;
            hasVehiclePosition = true;

            var telemetry = controller.Telemetry;
            if (telemetry.SpeedMetersPerSecond < MinimumSpeedMetersPerSecond || telemetry.Wheels == null)
            {
                BreakContacts();
                return;
            }

            var width = (float)controller.Definition.Tire.SectionWidthMeters;
            for (var index = 0; index < anchors.Length; index++)
            {
                if (index >= telemetry.Wheels.Length)
                {
                    anchors[index] = default(ContactAnchor);
                    continue;
                }

                var wheel = telemetry.Wheels[index];
                if (!wheel.Grounded || !wheel.IsSliding || wheel.NormalLoadNewtons <= 20f
                    || !IsFinite(wheel.ContactPointWorld) || !IsFinite(wheel.ContactNormalWorld)
                    || wheel.ContactNormalWorld.sqrMagnitude < 0.25f)
                {
                    anchors[index] = default(ContactAnchor);
                    continue;
                }

                var normal = wheel.ContactNormalWorld.normalized;
                var point = wheel.ContactPointWorld + normal * SurfaceOffsetMeters;
                var anchor = anchors[index];
                var delta = point - anchor.Point;
                if (!anchor.Active || Mathf.Abs(anchor.Width - width) > 0.001f
                    || delta.sqrMagnitude > MaximumSegmentLengthMeters * MaximumSegmentLengthMeters
                    || Vector3.Dot(anchor.Normal, normal) < 0.85f)
                {
                    anchors[index] = new ContactAnchor { Active = true, Point = point, Normal = normal, Width = width };
                    continue;
                }

                if (delta.sqrMagnitude < MinimumSegmentLengthMeters * MinimumSegmentLengthMeters) continue;
                var averagedNormal = (normal + anchor.Normal).normalized;
                var direction = Vector3.ProjectOnPlane(delta, averagedNormal);
                if (direction.sqrMagnitude < MinimumSegmentLengthMeters * MinimumSegmentLengthMeters) continue;
                var across = Vector3.Cross(averagedNormal, direction.normalized).normalized * (width * 0.5f);
                var left = point - across;
                var right = point + across;
                AppendSegment(anchor.HasEdges ? anchor.Left : anchor.Point - across,
                    anchor.HasEdges ? anchor.Right : anchor.Point + across,
                    left, right, anchor.Normal, normal);
                anchors[index] = new ContactAnchor
                {
                    Active = true, HasEdges = true, Point = point, Normal = normal,
                    Width = width, Left = left, Right = right
                };
            }

            if (!meshDirty) return;
            trailMesh.vertices = vertices;
            trailMesh.normals = normals;
            trailMesh.RecalculateBounds();
            meshDirty = false;
        }

        private void AppendSegment(Vector3 startLeft, Vector3 startRight, Vector3 endLeft, Vector3 endRight,
            Vector3 startNormal, Vector3 endNormal)
        {
            // Reuse the oldest quad once full; retained geometry and memory never grow with distance travelled.
            var offset = nextSegment * 4;
            vertices[offset] = startLeft;
            vertices[offset + 1] = startRight;
            vertices[offset + 2] = endLeft;
            vertices[offset + 3] = endRight;
            normals[offset] = normals[offset + 1] = startNormal;
            normals[offset + 2] = normals[offset + 3] = endNormal;
            nextSegment = (nextSegment + 1) % MaximumSegments;
            SegmentCount = Mathf.Min(SegmentCount + 1, MaximumSegments);
            meshDirty = true;
        }

        private void BreakContacts()
        {
            Array.Clear(anchors, 0, anchors.Length);
        }

        private void OnDisable()
        {
            BreakContacts();
            hasVehiclePosition = false;
        }

        private void OnDestroy()
        {
            ReleaseOwnedObject(trailRoot);
            ReleaseOwnedObject(trailMesh);
            ReleaseOwnedObject(trailMaterial);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void ReleaseOwnedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private struct ContactAnchor
        {
            public bool Active;
            public bool HasEdges;
            public Vector3 Point;
            public Vector3 Normal;
            public float Width;
            public Vector3 Left;
            public Vector3 Right;
        }
    }
}
