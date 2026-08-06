using System.Linq;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class VehicleDefinitionTests
    {
        [Test]
        public void ReferenceKiyoraAven_IsValid()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();

            Assert.That(definition.Validate(), Is.Empty);
            Assert.That(definition.DriveLayout, Is.EqualTo(DriveLayout.RearWheelDrive));
        }

        [Test]
        public void TorqueCurve_InterpolatesBetweenSamples()
        {
            var engine = new EngineDefinition
            {
                FullLoadTorqueCurve = new[]
                {
                    new TorqueSample(1000d, 100d),
                    new TorqueSample(3000d, 200d)
                }
            };

            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(2000d), Is.EqualTo(150d).Within(0.000001d));
            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(500d), Is.EqualTo(100d));
            Assert.That(engine.EvaluateFullLoadTorqueNewtonMeters(4000d), Is.EqualTo(200d));
        }

        [Test]
        public void Validation_ReportsNonIncreasingTorqueSamples()
        {
            var definition = ReferenceVehicleCatalog.CreateKiyoraAvenClubPrototype();
            definition.Engine.FullLoadTorqueCurve[2] = new TorqueSample(1500d, 190d);

            var issues = definition.Validate();

            Assert.That(issues.Any(issue => issue.Path == "engine.fullLoadTorqueCurve[2]"), Is.True);
        }
    }
}
