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
        private const float MaxConnectionTurnAngle = 92f;
        private const float MinConnectorTravelDot = 0.05f;
        private const float MinMidSegmentAlignmentDot = 0.7f;

        public static void BuildConnections(
            RoadGraph roadGraph,
            float connectionThresholdMeters,
            float sampleStepMeters,
            bool allowMidSegmentConnections = true)
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

            // --- Pass 1: directional endpoint connections ---
            foreach (RoadSegment segment in roadGraph.roadSegments)
            {
                if (segment == null || segment.waypoints.Count < 2)
                {
                    continue;
                }

                int lastIdx = segment.waypoints.Count - 1;
                TryAddDirectionalConnection(segment, segment, lastIdx, 0, threshold);

                foreach (RoadSegment otherSegment in roadGraph.roadSegments)
                {
                    if (otherSegment == null || otherSegment == segment || otherSegment.waypoints.Count < 2)
                    {
                        continue;
                    }

                    TryAddDirectionalConnection(segment, otherSegment, lastIdx, 0, threshold);
                }
            }

            // --- Pass 2: recovery connections at wider threshold ---
            if (recoveryThreshold > threshold + 0.01f)
            {
                foreach (RoadSegment segment in roadGraph.roadSegments)
                {
                    if (segment == null || segment.waypoints.Count < 2)
                    {
                        continue;
                    }

                    int lastIdx = segment.waypoints.Count - 1;

                    foreach (RoadSegment otherSegment in roadGraph.roadSegments)
                    {
                        if (otherSegment == null || otherSegment == segment || otherSegment.waypoints.Count < 2)
                        {
                            continue;
                        }

                        TryAddDirectionalConnection(segment, otherSegment, lastIdx, 0, recoveryThreshold);
                    }
                }
            }

            if (allowMidSegmentConnections)
            {
                // --- Pass 3: parallel mid-segment proximity connections ---
                // Only link strongly aligned mid-segment waypoints. This keeps the
                // fallback mesh graph flexible without allowing cross-traffic jumps.
                float midThreshold = Mathf.Max(connectionThresholdMeters, sampleStepMeters * 1.2f);
                BuildMidSegmentProximityConnections(roadGraph, midThreshold);
            }
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

                            Vector3 forwardA = GetWaypointForward(a, wpA);
                            Vector3 forwardB = GetWaypointForward(b, wpB);
                            if (Vector3.Dot(forwardA, forwardB) < MinMidSegmentAlignmentDot)
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

        private static bool TryAddDirectionalConnection(
            RoadSegment fromSegment,
            RoadSegment toSegment,
            int fromWaypointIndex,
            int toWaypointIndex,
            float threshold)
        {
            if (!ShouldCreateDirectionalConnection(fromSegment, toSegment, fromWaypointIndex, toWaypointIndex, threshold))
            {
                return false;
            }

            return AddConnectionIfMissing(fromSegment, toSegment, fromWaypointIndex, toWaypointIndex);
        }

        private static bool ShouldCreateDirectionalConnection(
            RoadSegment fromSegment,
            RoadSegment toSegment,
            int fromWaypointIndex,
            int toWaypointIndex,
            float threshold)
        {
            if (fromSegment == null || toSegment == null)
            {
                return false;
            }

            if (fromSegment.waypoints == null || toSegment.waypoints == null ||
                fromSegment.waypoints.Count < 2 || toSegment.waypoints.Count < 2)
            {
                return false;
            }

            Vector3 fromPos = fromSegment.waypoints[fromWaypointIndex].position;
            Vector3 toPos = toSegment.waypoints[toWaypointIndex].position;
            Vector3 delta = toPos - fromPos;
            if (Mathf.Abs(delta.y) > 2.5f)
            {
                return false;
            }

            delta.y = 0f;
            float thresholdSqr = threshold * threshold;
            if (delta.sqrMagnitude > thresholdSqr)
            {
                return false;
            }

            Vector3 exitForward = GetWaypointForward(fromSegment, fromWaypointIndex);
            Vector3 entryForward = GetWaypointForward(toSegment, toWaypointIndex);
            float maxTurnDot = Mathf.Cos(MaxConnectionTurnAngle * Mathf.Deg2Rad);
            if (Vector3.Dot(exitForward, entryForward) < maxTurnDot)
            {
                return false;
            }

            if (delta.sqrMagnitude > 0.0001f)
            {
                Vector3 travelDir = delta.normalized;
                if (Vector3.Dot(exitForward, travelDir) < MinConnectorTravelDot)
                {
                    return false;
                }

                if (Vector3.Dot(entryForward, travelDir) < MinConnectorTravelDot)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 GetWaypointForward(RoadSegment segment, int waypointIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                return Vector3.forward;
            }

            waypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            Vector3 forward = segment.waypoints[waypointIndex].forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                if (waypointIndex < segment.waypoints.Count - 1)
                {
                    forward = segment.waypoints[waypointIndex + 1].position - segment.waypoints[waypointIndex].position;
                }
                else if (waypointIndex > 0)
                {
                    forward = segment.waypoints[waypointIndex].position - segment.waypoints[waypointIndex - 1].position;
                }
            }

            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }
    }
}
