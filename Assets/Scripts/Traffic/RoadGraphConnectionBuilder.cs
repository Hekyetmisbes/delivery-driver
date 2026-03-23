using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    internal static class RoadGraphConnectionBuilder
    {
        /// <summary>
        /// Maximum number of mid-segment proximity connections to create per
        /// segment pair.  Keeps the graph manageable while still linking nearby
        /// parallel road pieces.
        /// </summary>
        private const int MaxMidSegmentConnectionsPerPair = 4;

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

            // --- Pass 1: endpoint-to-endpoint connections (original logic) ---
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

            // --- Pass 2: recovery connections at wider threshold ---
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

            // --- Pass 3: mid-segment proximity connections ---
            // SimplePoly dual-lane road layouts produce many short segments whose
            // mid-waypoints are spatially close but have no endpoint overlap.
            // Link nearby mid-segment waypoints so the A* search can move between
            // adjacent road pieces without relying solely on spatial transfers.
            float midThreshold = Mathf.Max(connectionThresholdMeters, sampleStepMeters * 1.2f);
            BuildMidSegmentProximityConnections(roadGraph, midThreshold);
        }

        /// <summary>
        /// For each segment pair, find up to <see cref="MaxMidSegmentConnectionsPerPair"/>
        /// waypoint pairs that are within <paramref name="threshold"/> of each other and
        /// create explicit connections between them.  This dramatically improves
        /// pathfinding success on tiled road layouts.
        /// </summary>
        private static void BuildMidSegmentProximityConnections(RoadGraph roadGraph, float threshold)
        {
            if (roadGraph == null || roadGraph.roadSegments == null)
            {
                return;
            }

            float thresholdSqr = threshold * threshold;
            List<RoadSegment> segments = roadGraph.roadSegments;

            for (int segA = 0; segA < segments.Count; segA++)
            {
                RoadSegment a = segments[segA];
                if (a == null || a.waypoints == null || a.waypoints.Count == 0)
                {
                    continue;
                }

                for (int segB = segA + 1; segB < segments.Count; segB++)
                {
                    RoadSegment b = segments[segB];
                    if (b == null || b.waypoints == null || b.waypoints.Count == 0)
                    {
                        continue;
                    }

                    int connectionsCreated = 0;

                    for (int wpA = 0; wpA < a.waypoints.Count && connectionsCreated < MaxMidSegmentConnectionsPerPair; wpA++)
                    {
                        Vector3 posA = a.waypoints[wpA].position;

                        for (int wpB = 0; wpB < b.waypoints.Count && connectionsCreated < MaxMidSegmentConnectionsPerPair; wpB++)
                        {
                            Vector3 delta = b.waypoints[wpB].position - posA;
                            if (Mathf.Abs(delta.y) > 2.5f)
                            {
                                continue;
                            }

                            delta.y = 0f;
                            if (delta.sqrMagnitude > thresholdSqr)
                            {
                                continue;
                            }

                            bool addedAny = false;
                            if (AddConnectionIfMissing(a, b, wpA, wpB))
                            {
                                addedAny = true;
                            }

                            if (AddConnectionIfMissing(b, a, wpB, wpA))
                            {
                                addedAny = true;
                            }

                            if (addedAny)
                            {
                                connectionsCreated++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a new connection was actually added (not a duplicate).
        /// </summary>
        private static bool AddConnectionIfMissing(RoadSegment fromSegment, RoadSegment toSegment, int fromWaypointIndex, int toWaypointIndex)
        {
            if (fromSegment == null || toSegment == null)
            {
                return false;
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
                    return false;
                }
            }

            fromSegment.connections.Add(new RoadConnection(fromSegment, toSegment, fromWaypointIndex, toWaypointIndex));
            return true;
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
