using System.Linq;
using NUnit.Framework;

namespace MotorBound.Vehicle.Core.Tests
{
    public sealed class VehicleAssemblyManifestTests
    {
        [Test]
        public void ReferenceManifest_IsValidAndOwnsMassOnce()
        {
            var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();

            Assert.That(manifest.Validate(), Is.Empty);
            Assert.That(manifest.ComputeAuthoritativeMassKilograms(), Is.EqualTo(1120d).Within(0.000001d));
            Assert.That(manifest.Relationships.All(value => value.IsSatisfied), Is.True);
        }

        [Test]
        public void DuplicateMassOwner_IsRejected()
        {
            var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();
            manifest.Parts[2].MassAccountingGroupId = manifest.Parts[1].MassAccountingGroupId;

            var issues = manifest.Validate();

            Assert.That(issues.Any(value => value.Message.Contains("cannot both contribute mass")), Is.True);
        }

        [Test]
        public void RelationshipToMissingPart_IsRejected()
        {
            var manifest = ReferenceVehicleCatalog.CreateKiyoraAvenPrototypeAssemblyManifest();
            manifest.Relationships[0].ToPartInstanceId = MotorBound.Foundation.StableId.FromCatalogKey("test", "missing-part");

            var issues = manifest.Validate();

            Assert.That(issues.Any(value => value.Path.EndsWith("toPartInstanceId")), Is.True);
        }
    }
}
