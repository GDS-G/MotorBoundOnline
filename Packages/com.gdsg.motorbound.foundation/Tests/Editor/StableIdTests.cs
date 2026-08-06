using NUnit.Framework;

namespace MotorBound.Foundation.Tests
{
    public sealed class StableIdTests
    {
        [Test]
        public void CatalogIdentity_IsDeterministicAndKeySensitive()
        {
            var first = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-prototype");
            var second = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-aven-prototype");
            var other = StableId.FromCatalogKey("motorbound.vehicle", "kiyora-ren-prototype");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(other));
            Assert.That(first.IsEmpty, Is.False);
        }

        [Test]
        public void TryParse_NormalizesGuidText()
        {
            var parsed = StableId.TryParse("A0CE658793CB462582AE7EE6FEA8A7CC", out var id);

            Assert.That(parsed, Is.True);
            Assert.That(id.Value, Is.EqualTo("a0ce6587-93cb-4625-82ae-7ee6fea8a7cc"));
        }
    }
}
