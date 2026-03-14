using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    internal static class RoadGraphConnectionBuilder
    {
        public static void BuildConnections(RoadGraph roadGraph, float connectionThresholdMeters, float sampleStepMeters)
        {
            if (roadGraph == null || roadGraph.roadSegments == null)
            {
                return;
            }

            float threshold = connectionThresholdMeters;
            float recoveryThreshold = Mathf.Max(connectionThresholdMeters, sampleStepMeters);

            foreach (RoadSegment segment in roadGraph.roadSegments)
            {
                if (segment != null)
                {
                    segment.connections = new List<RoadConnection>();
                }
            }

            foreach (RoadSegment segment in roadGraph.roadSegments)
            {
                if (segment == null || segment.waypoints.Count == 0)
                {
                    continue;
                }

                Vector3 startPos = segment.waypoints[0].position;
                Vector3 endPos = segment.waypoints[segment.waypoints.Count - 1].position;
                int lastIdx = segment.waypoints.Count - 1;

                if (Vector3.Distance(endPos, startPos) < threshold)
                {
                    AddConnectionIfMissing(segment, segment, lastIdx, 0);
                }

                foreach (RoadSegment otherSegment in roadGraph.roadSegments)
                {
                    if (otherSegment == null || otherSegment == segment || otherSegment.waypoints.Count == 0)
                    {
                        continue;
                    }

                    Vector3 otherStart = otherSegment.waypoints[0].position;
                    Vector3 otherEnd = otherSegment.waypoints[otherSegment.waypoints.Count - 1].position;
                    int otherLastIdx = otherSegment.waypoints.Count - 1;

                    if (Vector3.Distance(endPos, otherStart) < threshold)
                    {
                        AddConnectionIfMissing(segment, otherSegment, lastIdx, 0);
                    }

                    if (Vector3.Distance(endPos, otherEnd) < threshold)
                    {
                        AddConnectionIfMissing(segment, otherSegment, lastIdx, otherLastIdx);
                    }

                    if (Vector3.Distance(startPos, otherStart) < threshold)
                    {
                        AddConnectionIfMissing(segment, otherSegment, 0, 0);
                    }

                    if (Vector3.Distance(startPos, otherEnd) < threshold)
                    {
                        AddConnectionIfMissing(segment, otherSegment, 0, otherLastIdx);
                    }
                }
            }

            if (recoveryThreshold > threshold + 0.01f)
            {
                foreach (RoadSegment segment in roadGraph.roadSegments)
                {
                    if (segment == null || segment.waypoints.Count == 0)
                    {
                        continue;
                    }

                    Vector3 startPos = segment.waypoints[0].position;
                    Vector3 endPos = segment.waypoints[segment.waypoints.Count - 1].position;
                    int lastIdx = segment.waypoints.Count - 1;

                    foreach (RoadSegment otherSegment in roadGraph.roadSegments)
                    {
                        if (otherSegment == null || otherSegment == segment || otherSegment.waypoints.Count == 0)
                        {
                            continue;
                        }

                        Vector3 otherStart = otherSegment.waypoints[0].position;
                        Vector3 otherEnd = otherSegment.waypoints[otherSegment.waypoints.Count - 1].position;
                        int otherLastIdx = otherSegment.waypoints.Count - 1;

                        if (ShouldCreateRecoveryConnection(endPos, otherStart, recoveryThreshold))
                        {
                            AddConnectionIfMissing(segment, otherSegment, lastIdx, 0);
                        }

                        if (ShouldCreateRecoveryConnection(endPos, otherEnd, recoveryThreshold))
                        {
                            AddConnectionIfMissing(segment, otherSegment, lastIdx, otherLastIdx);
                        }

                        if (ShouldCreateRecoveryConnection(startPos, otherStart, recoveryThreshold))
                        {
                            AddConnectionIfMissing(segment, otherSegment, 0, 0);
                        }

                        if (ShouldCreateRecoveryConnection(startPos, otherEnd, recoveryThreshold))
                        {
                            AddConnectionIfMissing(segment, otherSegment, 0, otherLastIdx);
                        }
                    }
                }
            }
        }

        private static void AddConnectionIfMissing(RoadSegment fromSegment, RoadSegment toSegment, int fromWaypointIndex, int toWaypointIndex)
        {
            if (fromSegment == null || toSegment == null)
            {
                return;
            }

            if (fromSegment.connections == null)
            {
                fromSegment.connections = new List<RoadConnection>();
            }

            for (int i = 0; i < fromSegment.connections.Count; i++)
            {
                RoadConnection existing = fromSegment.connections[i];
                if (existing == null)
                {
                    continue;
                }

                if (existing.toSegment == toSegment &&
                    existing.fromWaypointIndex == fromWaypointIndex &&
                    existing.toWaypointIndex == toWaypointIndex)
                {
                    return;
                }
            }

            fromSegment.connections.Add(new RoadConnection(fromSegment, toSegment, fromWaypointIndex, toWaypointIndex));
        }

        private static bool ShouldCreateRecoveryConnection(Vector3 from, Vector3 to, float threshold)
        {
            Vector3 delta = to - from;
            if (Mathf.Abs(delta.y) > 2.5f)
            {
                return false;
            }

            delta.y = 0f;
            return delta.sqrMagnitude <= threshold * threshold;
        }
    }
}
