using NUnit.Framework;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class RoadSearchBudgetTests
    {
        [Test]
        public void SmallSlices_ProduceSameRouteAndCostAsUnbudgetedSearch()
        {
            var graph = new RoadGraph();
            var first = new RoadSegment(1, "First_Lane_Right");
            var second = new RoadSegment(2, "Second_Lane_Right");
            for (int i = 0; i <= 10; i++)
            {
                first.waypoints.Add(new Waypoint(new Vector3(i * 10f, 0, 0), Vector3.right, 1));
                second.waypoints.Add(new Waypoint(new Vector3(100f + i * 10f, 0, 0), Vector3.right, 2));
            }
            first.connections.Add(new RoadConnection(first, second, 10, 0));
            graph.roadSegments.Add(first);
            graph.roadSegments.Add(second);

            var baseline = RoadGraphPathfinder.StartIncrementalSearch(graph, Vector3.zero,
                new Vector3(200, 0, 0), 1f);
            var sliced = RoadGraphPathfinder.StartIncrementalSearch(graph, Vector3.zero,
                new Vector3(200, 0, 0), 1f);

            Run(baseline, 1000, 0f);
            Run(sliced, 1, 0.0001f);

            Assert.That(baseline.Status, Is.EqualTo(RoadGraphPathfinder.IncrementalSearchStatus.Succeeded));
            Assert.That(sliced.Status, Is.EqualTo(baseline.Status));
            Assert.That(sliced.Path, Is.EqualTo(baseline.Path));
            Assert.That(sliced.PathCost, Is.EqualTo(baseline.PathCost));
            Assert.That(sliced.ExpandedNodes, Is.EqualTo(baseline.ExpandedNodes));
            Assert.That(sliced.QueuePops, Is.EqualTo(baseline.QueuePops));
            Assert.That(sliced.ExpandedNodes, Is.GreaterThan(0));
        }

        private static void Run(RoadGraphPathfinder.IncrementalPathSearchSession session, int iterations, float budget)
        {
            for (int i = 0; i < 10000 && session.Status == RoadGraphPathfinder.IncrementalSearchStatus.Running; i++)
                session.Step(iterations, budget);

            Assert.That(session.Status, Is.Not.EqualTo(RoadGraphPathfinder.IncrementalSearchStatus.Running));
        }
    }
}
