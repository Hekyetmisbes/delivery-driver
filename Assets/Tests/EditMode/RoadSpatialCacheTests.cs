using NUnit.Framework;
using TrafficSystem;

namespace DeliveryDriver.Tests
{
    public class RoadSpatialCacheTests
    {
        [Test]
        public void TwoTransferTiers_AreReused_AndThirdEvictsLeastRecent()
        {
            var graph = new RoadGraph();
            var first = graph.GetOrBuildSpatialIndex(6);
            var second = graph.GetOrBuildSpatialIndex(10);
            Assert.That(graph.GetOrBuildSpatialIndex(6), Is.SameAs(first));
            graph.GetOrBuildSpatialIndex(16);
            Assert.That(graph.GetOrBuildSpatialIndex(6), Is.SameAs(first));
            Assert.That(graph.GetOrBuildSpatialIndex(10), Is.Not.SameAs(second));
        }

        [Test]
        public void Invalidation_ClearsBothIndices()
        {
            var graph = new RoadGraph();
            var first = graph.GetOrBuildSpatialIndex(6);
            var second = graph.GetOrBuildSpatialIndex(10);
            graph.InvalidateSpatialIndex();
            Assert.That(graph.GetOrBuildSpatialIndex(6), Is.Not.SameAs(first));
            Assert.That(graph.GetOrBuildSpatialIndex(10), Is.Not.SameAs(second));
        }
    }
}
