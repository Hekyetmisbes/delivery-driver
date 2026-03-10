using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    public static class RoadGraphPathfinder
    {
        private const float LeftLanePenalty = 6f;
        private const float UnnamedLanePenalty = 1.25f;
        private const float ReverseAlignmentPenalty = 20f;
        private const float HardReverseTurnPenalty = 10f;
        private const float TransferBackwardDotThreshold = -0.15f;
        private const float TransferLeftAllowance = 0.75f;

        public static List<Vector3> FindPath(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            float transferMaxDistance = 8f)
        {
            if (graph == null || graph.roadSegments.Count == 0)
            {
                return null;
            }

            var (projectedStartSegment, projectedStartEdgeIndex, projectedStart, projectedStartTangent) = graph.ProjectPointOnRoad(startWorld);
            var (projectedEndSegment, projectedEndEdgeIndex, projectedEnd, projectedEndTangent) = graph.ProjectPointOnRoad(endWorld);

            if (projectedStartSegment == null || projectedEndSegment == null)
            {
                return null;
            }

            // Use cached spatial index instead of rebuilding every call
            float spatialCellSize = Mathf.Max(1f, transferMaxDistance);
            float heuristicCellSize = Mathf.Max(2f, spatialCellSize * 0.5f);
            float transferMaxDistanceSqr = transferMaxDistance * transferMaxDistance;

            RoadGraphSpatialIndex idx = graph.GetOrBuildSpatialIndex(spatialCellSize);

            Vector3 desiredTravelDirection = GetPlanarDirection(projectedEnd - projectedStart);
            if (desiredTravelDirection.sqrMagnitude < 0.0001f)
            {
                desiredTravelDirection = GetPlanarDirection(projectedStartTangent);
            }

            if (desiredTravelDirection.sqrMagnitude < 0.0001f)
            {
                desiredTravelDirection = GetPlanarDirection(projectedEndTangent);
            }

            int projectedStartFallbackIndex = Mathf.Clamp(
                projectedStartEdgeIndex + 1,
                0,
                projectedStartSegment.waypoints.Count - 1);
            int projectedEndFallbackIndex = Mathf.Clamp(
                projectedEndEdgeIndex,
                0,
                projectedEndSegment.waypoints.Count - 1);

            float waypointSearchRadius = Mathf.Max(6f, transferMaxDistance * 1.5f);
            long startKey = SelectPreferredWaypointKey(
                idx,
                projectedStart,
                desiredTravelDirection,
                waypointSearchRadius,
                RoadGraphSpatialIndex.PackKey(projectedStartSegment.id, projectedStartFallbackIndex));
            long endKey = SelectPreferredWaypointKey(
                idx,
                projectedEnd,
                desiredTravelDirection,
                waypointSearchRadius,
                RoadGraphSpatialIndex.PackKey(projectedEndSegment.id, projectedEndFallbackIndex));

            if (!idx.waypointByKey.TryGetValue(startKey, out RoadGraphSpatialIndex.WaypointRef startRef) ||
                !idx.waypointByKey.TryGetValue(endKey, out RoadGraphSpatialIndex.WaypointRef endRef))
            {
                return null;
            }

            // Same-segment shortcut is only safe when it continues forward on the lane.
            if (startRef.segment.id == endRef.segment.id && startRef.index <= endRef.index)
            {
                return CollectWaypointsBetween(startRef.segment, startRef.index, endRef.index, projectedStart, projectedEnd);
            }

            // A* search with Manhattan-grid heuristic.
            var gScore = new Dictionary<long, float>();
            var prev = new Dictionary<long, long>();
            var closed = new HashSet<long>();

            // Priority queue by fScore
            var pq = new SortedList<float, Queue<long>>();

            void Enqueue(long key, float cost)
            {
                if (!pq.ContainsKey(cost))
                {
                    pq[cost] = new Queue<long>();
                }

                pq[cost].Enqueue(key);
            }

            long Dequeue(out float cost)
            {
                var first = pq.Keys[0];
                cost = first;
                var queue = pq[first];
                long key = queue.Dequeue();
                if (queue.Count == 0) pq.Remove(first);
                return key;
            }

            Vector3 endPos = endRef.position;
            gScore[startKey] = 0f;
            Enqueue(startKey, GridManhattanHeuristic(startRef.position, endPos, heuristicCellSize));

            while (pq.Count > 0)
            {
                long currentKey = Dequeue(out _);

                if (currentKey == endKey)
                {
                    break;
                }

                if (closed.Contains(currentKey))
                {
                    continue;
                }

                closed.Add(currentKey);
                if (!gScore.TryGetValue(currentKey, out float currentG))
                {
                    continue;
                }

                RoadGraphSpatialIndex.UnpackKey(currentKey, out int curSegId, out int curWpIdx);

                if (!idx.segmentById.TryGetValue(curSegId, out RoadSegment curSeg))
                {
                    continue;
                }

                Vector3 curPos = curSeg.waypoints[curWpIdx].position;
                Vector3 curForward = idx.waypointByKey.TryGetValue(currentKey, out var curRef)
                    ? curRef.forward
                    : GetWaypointForward(curSeg, curWpIdx);

                void TryNeighbor(long nKey, Vector3 nPos, float extraCost)
                {
                    if (closed.Contains(nKey))
                    {
                        return;
                    }

                    float tentativeG = currentG + Vector3.Distance(curPos, nPos) + extraCost;
                    if (!gScore.TryGetValue(nKey, out float bestKnownG) || tentativeG < bestKnownG)
                    {
                        gScore[nKey] = tentativeG;
                        prev[nKey] = currentKey;
                        float f = tentativeG + GridManhattanHeuristic(nPos, endPos, heuristicCellSize);
                        Enqueue(nKey, f);
                    }
                }

                // Stay lane-faithful: keep moving forward on the current segment.
                int forwardNeighborIndex = curWpIdx + 1;
                if (forwardNeighborIndex < curSeg.waypoints.Count)
                {
                    Vector3 nextPos = curSeg.waypoints[forwardNeighborIndex].position;
                    long fwdKey = RoadGraphSpatialIndex.PackKey(curSegId, forwardNeighborIndex);
                    Vector3 nextForward = idx.waypointByKey.TryGetValue(fwdKey, out var fwdRef)
                        ? fwdRef.forward
                        : GetWaypointForward(curSeg, forwardNeighborIndex);
                    float forwardCost = GetLanePenalty(curSeg) +
                                        GetAlignmentPenalty(nextForward, desiredTravelDirection) +
                                        GetTurnPenalty(curForward, nextForward);
                    TryNeighbor(fwdKey, nextPos, forwardCost);
                }

                // Inter-segment edges (explicit road connections).
                foreach (var conn in curSeg.connections)
                {
                    if (conn.fromWaypointIndex != curWpIdx)
                    {
                        continue;
                    }

                    long nKey = RoadGraphSpatialIndex.PackKey(conn.toSegment.id, conn.toWaypointIndex);
                    if (closed.Contains(nKey))
                    {
                        continue;
                    }

                    Vector3 nPos = conn.toSegment.waypoints[conn.toWaypointIndex].position;
                    Vector3 nForward = idx.waypointByKey.TryGetValue(nKey, out var connRef)
                        ? connRef.forward
                        : GetWaypointForward(conn.toSegment, conn.toWaypointIndex);
                    float connectionCost = GetLanePenalty(conn.toSegment) +
                                           GetAlignmentPenalty(nForward, desiredTravelDirection) +
                                           GetTurnPenalty(curForward, nForward);
                    TryNeighbor(nKey, nPos, connectionCost);
                }

                // Recovery edges: short transfer hops.
                if (transferMaxDistance > 0.01f)
                {
                    Vector2Int curCell = RoadGraphSpatialIndex.ToGridCell(curPos, spatialCellSize);
                    int radius = Mathf.CeilToInt(transferMaxDistance / spatialCellSize);
                    for (int gx = curCell.x - radius; gx <= curCell.x + radius; gx++)
                    {
                        for (int gz = curCell.y - radius; gz <= curCell.y + radius; gz++)
                        {
                            var lookupCell = new Vector2Int(gx, gz);
                            if (!idx.spatialGrid.TryGetValue(lookupCell, out var bucket))
                            {
                                continue;
                            }

                            for (int i = 0; i < bucket.Count; i++)
                            {
                                long nKey = bucket[i];
                                if (nKey == currentKey || closed.Contains(nKey))
                                {
                                    continue;
                                }

                                if (!idx.waypointByKey.TryGetValue(nKey, out RoadGraphSpatialIndex.WaypointRef candidate))
                                {
                                    continue;
                                }

                                Vector3 delta = candidate.position - curPos;
                                delta.y = 0f;
                                float d2 = delta.sqrMagnitude;
                                if (d2 <= 0.0001f || d2 > transferMaxDistanceSqr)
                                {
                                    continue;
                                }

                                if (!TryGetTransferPenalty(curPos, curForward, candidate.position, out float lateralPenalty))
                                {
                                    continue;
                                }

                                float transferCost = (Mathf.Sqrt(d2) * 0.05f) +
                                                     lateralPenalty +
                                                     GetLanePenalty(candidate.segment) +
                                                     GetAlignmentPenalty(candidate.forward, desiredTravelDirection) +
                                                     GetTurnPenalty(curForward, candidate.forward);
                                TryNeighbor(nKey, candidate.position, transferCost);
                            }
                        }
                    }
                }
            }

            // Reconstruct path
            if (!prev.ContainsKey(endKey) && startKey != endKey)
            {
                return null; // No path found
            }

            var pathKeys = new List<long>();
            long cur = endKey;
            while (cur != startKey)
            {
                pathKeys.Add(cur);
                if (!prev.ContainsKey(cur))
                    return null;
                cur = prev[cur];
            }
            pathKeys.Add(startKey);
            pathKeys.Reverse();

            var path = new List<Vector3>(pathKeys.Count + 2);
            path.Add(projectedStart);
            foreach (long key in pathKeys)
            {
                RoadGraphSpatialIndex.UnpackKey(key, out int segId, out int wpIdx);
                if (idx.segmentById.TryGetValue(segId, out RoadSegment seg) &&
                    wpIdx < seg.waypoints.Count)
                {
                    path.Add(seg.waypoints[wpIdx].position);
                }
            }
            path.Add(projectedEnd);

            return path;
        }

        private static long SelectPreferredWaypointKey(
            RoadGraphSpatialIndex idx,
            Vector3 targetPosition,
            Vector3 desiredTravelDirection,
            float searchRadius,
            long fallbackKey)
        {
            // Use spatial grid for local search instead of iterating all waypoints
            long bestKey = fallbackKey;
            float bestScore = float.MaxValue;
            float searchRadiusSqr = searchRadius * searchRadius;
            int gridRadius = Mathf.CeilToInt(searchRadius / idx.cellSize) + 1;
            Vector2Int centerCell = RoadGraphSpatialIndex.ToGridCell(targetPosition, idx.cellSize);

            for (int gx = centerCell.x - gridRadius; gx <= centerCell.x + gridRadius; gx++)
            {
                for (int gz = centerCell.y - gridRadius; gz <= centerCell.y + gridRadius; gz++)
                {
                    if (!idx.spatialGrid.TryGetValue(new Vector2Int(gx, gz), out var bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        long key = bucket[i];
                        if (!idx.waypointByKey.TryGetValue(key, out RoadGraphSpatialIndex.WaypointRef candidate))
                        {
                            continue;
                        }

                        Vector3 delta = candidate.position - targetPosition;
                        delta.y = 0f;
                        float distanceSqr = delta.sqrMagnitude;
                        if (distanceSqr > searchRadiusSqr)
                        {
                            continue;
                        }

                        float distance = Mathf.Sqrt(distanceSqr);
                        float score = distance +
                                      (GetLanePenalty(candidate.segment) * 2f) +
                                      GetAlignmentPenalty(candidate.forward, desiredTravelDirection);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestKey = key;
                        }
                    }
                }
            }

            return bestKey;
        }

        private static Vector2Int ToGridCell(Vector3 position, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        private static float GridManhattanHeuristic(Vector3 from, Vector3 to, float cellSize)
        {
            Vector2Int fromCell = ToGridCell(from, cellSize);
            Vector2Int toCell = ToGridCell(to, cellSize);
            int manhattanSteps = Mathf.Abs(fromCell.x - toCell.x) + Mathf.Abs(fromCell.y - toCell.y);
            return manhattanSteps * cellSize * 0.70710678f;
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
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        private static Vector3 GetPlanarDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
        }

        private static float GetLanePenalty(RoadSegment segment)
        {
            if (segment == null || string.IsNullOrEmpty(segment.name))
            {
                return UnnamedLanePenalty;
            }

            string lowerName = segment.name.ToLowerInvariant();
            if (lowerName.Contains("lane_right") || lowerName.Contains("right_lane"))
            {
                return 0f;
            }

            if (lowerName.Contains("lane_left") || lowerName.Contains("left_lane"))
            {
                return LeftLanePenalty;
            }

            return UnnamedLanePenalty;
        }

        private static float GetAlignmentPenalty(Vector3 waypointForward, Vector3 desiredTravelDirection)
        {
            if (desiredTravelDirection.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float dot = Vector3.Dot(GetPlanarDirection(waypointForward), desiredTravelDirection);
            if (dot < 0f)
            {
                return ReverseAlignmentPenalty + (-dot * 10f);
            }

            return (1f - dot) * 2f;
        }

        private static float GetTurnPenalty(Vector3 currentForward, Vector3 nextForward)
        {
            Vector3 a = GetPlanarDirection(currentForward);
            Vector3 b = GetPlanarDirection(nextForward);
            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float dot = Vector3.Dot(a, b);
            if (dot < -0.1f)
            {
                return HardReverseTurnPenalty;
            }

            return Mathf.Max(0f, (1f - dot) * 0.75f);
        }

        private static bool TryGetTransferPenalty(
            Vector3 currentPosition,
            Vector3 currentForward,
            Vector3 candidatePosition,
            out float lateralPenalty)
        {
            lateralPenalty = 0f;

            Vector3 planarForward = GetPlanarDirection(currentForward);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Vector3 delta = candidatePosition - currentPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 moveDirection = delta.normalized;
            float forwardDot = Vector3.Dot(moveDirection, planarForward);
            if (forwardDot < TransferBackwardDotThreshold)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(Vector3.up, planarForward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            float lateralOffset = Vector3.Dot(delta, right);
            if (lateralOffset < -TransferLeftAllowance)
            {
                return false;
            }

            if (lateralOffset < 0f)
            {
                lateralPenalty = Mathf.Abs(lateralOffset) * 2.5f;
            }

            return true;
        }

        private static List<Vector3> CollectWaypointsBetween(
            RoadSegment segment, int fromIdx, int toIdx,
            Vector3 startWorld, Vector3 endWorld)
        {
            var path = new List<Vector3>();
            path.Add(startWorld);

            int step = fromIdx <= toIdx ? 1 : -1;
            for (int i = fromIdx; i != toIdx + step; i += step)
            {
                if (i >= 0 && i < segment.waypoints.Count)
                {
                    path.Add(segment.waypoints[i].position);
                }
            }

            path.Add(endWorld);
            return path;
        }
    }
}
