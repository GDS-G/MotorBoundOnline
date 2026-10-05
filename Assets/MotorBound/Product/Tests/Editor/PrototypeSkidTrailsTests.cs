using System.Linq;
using MotorBound.Vehicle.Core;
using MotorBound.Vehicle.Physics;
using NUnit.Framework;
using UnityEngine;

namespace MotorBound.Product.Tests
{
    public sealed class PrototypeSkidTrailsTests
    {
        private GameObject car;
        private Rigidbody body;
        private RaycastVehicleController controller;
        private PrototypeSkidTrails trails;

        [SetUp]
        public void SetUp()
        {
            car = new GameObject("Skid trail test vehicle", typeof(Rigidbody));
            // Keep these controlled visual-input tests clear of any pre-existing scene contact surfaces.
            car.transform.position = new Vector3(1000f, 50f, 1000f);
            body = car.GetComponent<Rigidbody>();
            controller = car.AddComponent<RaycastVehicleController>();
            controller.Configure(ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype());
            SetSpeed(8f);
            trails = car.AddComponent<PrototypeSkidTrails>();
            trails.Configure(controller);
        }

        [TearDown]
        public void TearDown()
        {
            if (car != null) Object.DestroyImmediate(car);
        }

        [Test]
        public void GrippingContacts_DoNotMarkEvenWithFullSteering()
        {
            controller.SetInput(new VehicleInputState(0f, 0f, 1f, 0f));
            SetSpeed(8f);
            Assert.That(controller.Telemetry.Input.Steering, Is.EqualTo(1f));
            SampleContact(new Vector3(12f, 0f, -15f), false);
            SampleContact(new Vector3(12f, 0f, -14f), false);
            Assert.That(trails.SegmentCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SlidingContacts_MarkReportedWorldPoseAtConfiguredTireWidth(bool touring)
        {
            if (touring)
            {
                var state = PrototypeGarageCatalog.CreateNewState();
                Assert.That(PrototypeGarageCatalog.TryInstallCompanionKit(state, out _), Is.True);
                Assert.That(PrototypeGarageCatalog.TryInstallWheelPackage(state, PrototypeGarageCatalog.TouringKey, out _), Is.True);
                controller.Configure(PrototypeGarageCatalog.Compile(state).Vehicle);
                SetSpeed(8f);
            }

            Assert.That(controller.Telemetry.Input.Steering, Is.Zero,
                "Sliding contact, rather than the steering command, must trigger marks.");
            var start = new Vector3(12f, 0f, -15f);
            var end = start + Vector3.forward;
            SampleContact(start);
            SampleContact(end);

            Assert.That(trails.SegmentCount, Is.EqualTo(1));
            var vertices = trails.TrailMesh.vertices;
            var width = (float)controller.Definition.Tire.SectionWidthMeters;
            Assert.That(Vector3.Distance(vertices[0], vertices[1]), Is.EqualTo(width).Within(0.0001f));
            Assert.That(Vector3.Distance(vertices[2], vertices[3]), Is.EqualTo(width).Within(0.0001f));
            Assert.That(Vector3.Distance((vertices[0] + vertices[1]) * 0.5f, start + Vector3.up * 0.012f), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance((vertices[2] + vertices[3]) * 0.5f, end + Vector3.up * 0.012f), Is.LessThan(0.0001f));

            var filter = FindTrailFilter();
            Assert.That(filter.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(filter.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(filter.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(filter.transform.parent, Is.Null);
            Assert.That(filter.GetComponents<Collider>(), Is.Empty);
        }

        [Test]
        public void SlidingAtRest_DoesNotCreateNumericalSkidMarks()
        {
            SetSpeed(1f);
            SampleContact(Vector3.zero);
            SampleContact(Vector3.forward);
            Assert.That(trails.SegmentCount, Is.Zero);
        }

        [Test]
        public void LostContactPauseAndDisable_BreakTrailsBeforeResuming()
        {
            SampleContact(Vector3.zero);
            SampleContact(Vector3.forward);
            Assert.That(trails.SegmentCount, Is.EqualTo(1));

            controller.Telemetry.Wheels[0] = default(WheelTelemetry);
            trails.SampleContacts();
            SampleContact(Vector3.forward * 1.3f);
            Assert.That(trails.SegmentCount, Is.EqualTo(1), "Contact loss must prevent bridging the airborne interval.");
            SampleContact(Vector3.forward * 2.3f);
            Assert.That(trails.SegmentCount, Is.EqualTo(2));

            controller.SimulationPaused = true;
            trails.SampleContacts();
            controller.SimulationPaused = false;
            SampleContact(Vector3.forward * 2.6f);
            Assert.That(trails.SegmentCount, Is.EqualTo(2), "Pause must end the previous segment even over a short gap.");
            SampleContact(Vector3.forward * 3.6f);
            Assert.That(trails.SegmentCount, Is.EqualTo(3));

            trails.enabled = false;
            SampleContact(Vector3.forward * 3.9f);
            Assert.That(trails.SegmentCount, Is.EqualTo(3), "Explicit sampling must respect a disabled component.");
            trails.enabled = true;
            SampleContact(Vector3.forward * 4.2f);
            Assert.That(trails.SegmentCount, Is.EqualTo(3));
            SampleContact(Vector3.forward * 5.2f);
            Assert.That(trails.SegmentCount, Is.EqualTo(4));
            AssertSegmentsBounded();
        }

        [Test]
        public void RecoveryAndVehicleTeleport_BreakTrails()
        {
            SampleContact(Vector3.zero);
            SampleContact(Vector3.forward);
            controller.Recover();
            trails.SampleContacts();
            SetSpeed(8f);
            SampleContact(Vector3.forward * 1.3f);
            Assert.That(trails.SegmentCount, Is.EqualTo(1));
            SampleContact(Vector3.forward * 2.3f);
            Assert.That(trails.SegmentCount, Is.EqualTo(2));

            car.transform.position += Vector3.right * 100f;
            SampleContact(Vector3.forward * 2.6f);
            Assert.That(trails.SegmentCount, Is.EqualTo(2), "A vehicle teleport must break marks even before fresh contact data arrives.");
            SampleContact(Vector3.forward * 3.6f);
            Assert.That(trails.SegmentCount, Is.EqualTo(3));
            SampleContact(Vector3.forward * 100f);
            Assert.That(trails.SegmentCount, Is.EqualTo(3), "A discontinuity in contact poses must not draw a long connecting quad.");
            SampleContact(Vector3.forward * 101f);
            Assert.That(trails.SegmentCount, Is.EqualTo(4));
            AssertSegmentsBounded();
        }

        [Test]
        public void ContinuousSliding_ReusesBoundedMeshAndSegmentPool()
        {
            var mesh = trails.TrailMesh;
            var capacity = mesh.vertexCount / 4;
            Assert.That(capacity, Is.LessThanOrEqualTo(2048));
            Assert.That(capacity, Is.GreaterThan(0));
            SampleContact(Vector3.zero);
            for (var index = 1; index <= capacity + 5; index++)
                SampleContact(Vector3.forward * (index * 0.2f));

            Assert.That(trails.SegmentCount, Is.EqualTo(capacity));
            Assert.That(trails.TrailMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertexCount, Is.EqualTo(capacity * 4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(capacity * 6));
            AssertSegmentsBounded();
        }

        [Test]
        public void ConfigureAgain_ReusesResourcesAndDestroyReleasesThem()
        {
            SampleContact(Vector3.zero);
            SampleContact(Vector3.forward);
            var mesh = trails.TrailMesh;
            var root = FindTrailFilter().gameObject;
            var material = root.GetComponent<MeshRenderer>().sharedMaterial;
            trails.Configure(controller);
            Assert.That(trails.TrailMesh, Is.SameAs(mesh));
            Assert.That(FindTrailFilter().gameObject, Is.SameAs(root));
            Assert.That(trails.SegmentCount, Is.EqualTo(1));

            Object.DestroyImmediate(trails);
            Assert.That(root == null, Is.True);
            Assert.That(mesh == null, Is.True);
            Assert.That(material == null, Is.True);
        }

        private void SetSpeed(float metersPerSecond)
        {
            body.velocity = Vector3.forward * metersPerSecond;
            controller.SimulateStep(1f / RaycastVehicleController.CriticalVehicleSimulationFrequencyHertz);
        }

        private void SampleContact(Vector3 point, bool sliding = true)
        {
            // Feed controlled wheel telemetry to the same sampling method used in rendered gameplay.
            // Actual physics classification is covered separately by tire-model and driving scenarios.
            controller.Telemetry.Wheels[0] = new WheelTelemetry
            {
                Grounded = true, NormalLoadNewtons = 1200f, IsSliding = sliding,
                SlipDemandRatio = sliding ? 4f : 1f,
                ContactPointWorld = point, ContactNormalWorld = Vector3.up
            };
            trails.SampleContacts();
        }

        private MeshFilter FindTrailFilter()
        {
            return Object.FindObjectsOfType<MeshFilter>().Single(filter => filter.sharedMesh == trails.TrailMesh);
        }

        private void AssertSegmentsBounded()
        {
            var vertices = trails.TrailMesh.vertices;
            for (var index = 0; index < trails.SegmentCount; index++)
            {
                var offset = index * 4;
                var start = (vertices[offset] + vertices[offset + 1]) * 0.5f;
                var end = (vertices[offset + 2] + vertices[offset + 3]) * 0.5f;
                Assert.That(Vector3.Distance(start, end), Is.LessThanOrEqualTo(2.5001f));
            }
        }
    }
}
