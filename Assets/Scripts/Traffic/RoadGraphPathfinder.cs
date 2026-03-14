using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    public static class RoadGraphPathfinder
    {
        private sealed class SearchBuffers
        {
            public readonly List<WaypointCandidate> candidateBuffer = new List<WaypointCandidate>(CandidateWaypointLimit + 4);
            public readonly List<long> startCandidateKeys = new List<long>(CandidateWaypointLimit);
            public readonly List<long> endCandidateKeys = new List<long>(CandidateWaypointLimit);
            public readonly Dictionary<long, float> gScore = new Dictionary<long, float>(256);
            public readonly Dictionary<long, long> prev = new Dictionary<long, long>(256);
            public readonly HashSet<long> closed = new HashSet<long>();
            public readonly SortedList<float, Queue<long>> priorityQueue = new SortedList<float, Queue<long>>();
            public readonly Stack<Queue<long>> queuePool = new Stack<Queue<long>>();
            public readonly List<long> pathKeys = new List<long>(256);

            public void PrepareCandidateBuffer(List<long> resultKeys)
            {
                candidateBuffer.Clear();
                resultKeys.Clear();
            }

            public void PreparePathSearch()
            {
                gScore.Clear();
                prev.Clear();
                closed.Clear();
                pathKeys.Clear();

                for (int i = 0; i < priorityQueue.Count; i++)
                {
                    ReturnQueue(priorityQueue.Values[i]);
                }

                priorityQueue.Clear();
            }

            public Queue<long> RentQueue()
            {
                if (queuePool.Count == 0)
                {
                    return new Queue<long>(4);
                }

                Queue<long> queue = queuePool.Pop();
                queue.Clear();
                return queue;
            }

            public void ReturnQueue(Queue<long> queue)
            {
                if (queue == null)
                {
                    return;
                }

                queue.Clear();
                queuePool.Push(queue);
            }
        }

        public readonly struct PathSearchDiagnostics
        {
            public PathSearchDiagnostics(
                RoadSegment startSegment,
                int startWaypointIndex,
                Vector3 projectedStartPoint,
                float startProjectionDistance,
                RoadSegment endSegment,
                int endWaypointIndex,
                Vector3 projectedEndPoint,
                float endProjectionDistance,
                int segmentCount,
                int connectionCount)
            {
                StartSegment = startSegment;
                StartWaypointIndex = startWaypointIndex;
                ProjectedStartPoint = projectedStartPoint;
                StartProjectionDistance = startProjectionDistance;
                EndSegment = endSegment;
                EndWaypointIndex = endWaypointIndex;
                ProjectedEndPoint = projectedEndPoint;
                EndProjectionDistance = endProjectionDistance;
                SegmentCount = segmentCount;
                ConnectionCount = connectionCount;
            }

            public RoadSegment StartSegment { get; }
            public int StartWaypointIndex { get; }
            public Vector3 ProjectedStartPoint { get; }
            public float StartProjectionDistance { get; }
            public RoadSegment EndSegment { get; }
            public int EndWaypointIndex { get; }
            public Vector3 ProjectedEndPoint { get; }
            public float EndProjectionDistance { get; }
            public int SegmentCount { get; }
            public int ConnectionCount { get; }
        }

        private readonly struct WaypointCandidate
        {
            public WaypointCandidate(long key, float score)
            {
                Key = key;
                Score = score;
            }

            public long Key { get; }
            public float Score { get; }
        }

        private const float LeftLanePenalty = 6f;
        private const float UnnamedLanePenalty = 1.25f;
        private const float ReverseAlignmentPenalty = 20f;
        private const float HardReverseTurnPenalty = 10f;
        private const float TransferBackwardDotThreshold = -0.15f;
        private const float TransferLeftAllowance = 0.75f;
        private const float TransferDistancePenaltyMultiplier = 4f;
        private const float TransferNormalizedPenalty = 40f;
        private const float TransferHeightPenalty = 10f;
        private const float MaxTransferHeightDelta = 2.5f;
        private const int TransferEndpointWindow = 2;
        private const int CandidateWaypointLimit = 4;
        private static readonly SearchBuffers SharedBuffers = new SearchBuffers();

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
            SearchBuffers buffers = SharedBuffers;
            List<long> startCandidates = buffers.startCandidateKeys;
            List<long> endCandidates = buffers.endCandidateKeys;
            CollectPreferredWaypointKeys(
                idx,
                projectedStart,
                desiredTravelDirection,
                waypointSearchRadius,
                RoadGraphSpatialIndex.PackKey(projectedStartSegment.id, projectedStartFallbackIndex),
                buffers,
                startCandidates);
            CollectPreferredWaypointKeys(
                idx,
                projectedEnd,
                desiredTravelDirection,
                waypointSearchRadius,
                RoadGraphSpatialIndex.PackKey(projectedEndSegment.id, projectedEndFallbackIndex),
                buffers,
                endCandidates);

            if (startCandidates.Count == 0 || endCandidates.Count == 0)
            {
                return null;
            }

            List<Vector3> bestPath = null;
            float bestPathCost = float.MaxValue;
            for (int startCandidateIndex = 0; startCandidateIndex < startCandidates.Count; startCandidateIndex++)
            {
                for (int endCandidateIndex = 0; endCandidateIndex < endCandidates.Count; endCandidateIndex++)
                {
                    if (!TryFindPathBetweenKeys(
                            idx,
                            startCandidates[startCandidateIndex],
                            endCandidates[endCandidateIndex],
                            projectedStart,
                            projectedEnd,
                            desiredTravelDirection,
                            transferMaxDistance,
                            transferMaxDistanceSqr,
                            spatialCellSize,
                            heuristicCellSize,
                            buffers,
                            out List<Vector3> candidatePath,
                            out float candidateCost))
                    {
                        continue;
                    }

                    if (candidateCost >= bestPathCost)
                    {
                        continue;
                    }

                    bestPath = candidatePath;
                    bestPathCost = candidateCost;
                }
            }

            return bestPath;
        }

        public static bool TryGetPathDiagnostics(
            RoadGraph graph,
            Vector3 startWorld,
            Vector3 endWorld,
            out PathSearchDiagnostics diagnostics)
        {
            diagnostics = default;
            if (graph == null || graph.roadSegments == null || graph.roadSegments.Count == 0)
            {
                return false;
            }

            var (projectedStartSegment, projectedStartEdgeIndex, projectedStart, _) = graph.ProjectPointOnRoad(startWorld);
            var (projectedEndSegment, projectedEndEdgeIndex, projectedEnd, _) = graph.ProjectPointOnRoad(endWorld);
            if (projectedStartSegment == null || projectedEndSegment == null)
            {
                return false;
            }

            int connectionCount = 0;
            for (int i = 0; i < graph.roadSegments.Count; i++)
            {
                RoadSegment segment = graph.roadSegments[i];
                if (segment != null && segment.connections != null)
                {
                    connectionCount += segment.connections.Count;
                }
            }

            diagnostics = new PathSearchDiagnostics(
                projectedStartSegment,
                projectedStartEdgeIndex,
                projectedStart,
                Vector3.Distance(startWorld, projectedStart),
                projectedEndSegment,
                projectedEndEdgeIndex,
                projectedEnd,
                Vector3.Distance(endWorld, projectedEnd),
                graph.roadSegments.Count,
                connectionCount);
            return true;
        }

        private static void CollectPreferredWaypointKeys(
            RoadGraphSpatialIndex idx,
            Vector3 targetPosition,
            Vector3 desiredTravelDirection,
            float searchRadius,
            long fallbackKey,
            SearchBuffers buffers,
            List<long> keys)
        {
            buffers.PrepareCandidateBuffer(keys);
            List<WaypointCandidate> candidates = buffers.candidateBuffer;
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
                        AddCandidate(candidates, new WaypointCandidate(key, score));
                    }
                }
            }

            if (!ContainsCandidateKey(candidates, fallbackKey))
            {
                AddCandidate(candidates, new WaypointCandidate(fallbackKey, 1000000f));
            }

            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int i = 0; i < candidates.Count && keys.Count < CandidateWaypointLimit; i++)
            {
                if (!ContainsKey(keys, candidates[i].Key))
                {
                    keys.Add(candidates[i].Key);
                }
            }

            if (keys.Count == 0)
            {
                keys.Add(fallbackKey);
            }
        }

        private static bool TryFindPathBetweenKeys(
            RoadGraphSpatialIndex idx,
            long startKey,
            long endKey,
            Vector3 projectedStart,
            Vector3 projectedEnd,
            Vector3 desiredTravelDirection,
            float transferMaxDistance,
            float transferMaxDistanceSqr,
            float spatialCellSize,
            float heuristicCellSize,
            SearchBuffers buffers,
            out List<Vector3> path,
            out float pathCost)
        {
            path = null;
            pathCost = float.MaxValue;

            if (!idx.waypointByKey.TryGetValue(startKey, out RoadGraphSpatialIndex.WaypointRef startRef) ||
                !idx.waypointByKey.TryGetValue(endKey, out RoadGraphSpatialIndex.WaypointRef endRef))
            {
                return false;
            }

            if (startRef.segment.id == endRef.segment.id && startRef.index <= endRef.index)
            {
                path = CollectWaypointsBetween(startRef.segment, startRef.index, endRef.index, projectedStart, projectedEnd);
                pathCost = Vector3.Distance(projectedStart, projectedEnd);
                return path != null && path.Count >= 2;
            }

            buffers.PreparePathSearch();
            Dictionary<long, float> gScore = buffers.gScore;
            Dictionary<long, long> prev = buffers.prev;
            HashSet<long> closed = buffers.closed;
            SortedList<float, Queue<long>> pq = buffers.priorityQueue;

            void Enqueue(long key, float cost)
            {
                if (!pq.TryGetValue(cost, out Queue<long> queue))
                {
                    queue = buffers.RentQueue();
                    pq[cost] = queue;
                }

                queue.Enqueue(key);
            }

            long Dequeue()
            {
                float first = pq.Keys[0];
                Queue<long> queue = pq[first];
                long key = queue.Dequeue();
                if (queue.Count == 0)
                {
                    pq.Remove(first);
                    buffers.ReturnQueue(queue);
                }

                return key;
            }

            Vector3 endPos = endRef.position;
            gScore[startKey] = 0f;
            Enqueue(startKey, GridManhattanHeuristic(startRef.position, endPos, heuristicCellSize));

            while (pq.Count > 0)
            {
                long currentKey = Dequeue();
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
                Vector3 curForward = idx.waypointByKey.TryGetValue(currentKey, out RoadGraphSpatialIndex.WaypointRef curRef)
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

                int forwardNeighborIndex = curWpIdx + 1;
                if (forwardNeighborIndex < curSeg.waypoints.Count)
                {
                    Vector3 nextPos = curSeg.waypoints[forwardNeighborIndex].position;
                    long fwdKey = RoadGraphSpatialIndex.PackKey(curSegId, forwardNeighborIndex);
                    Vector3 nextForward = idx.waypointByKey.TryGetValue(fwdKey, out RoadGraphSpatialIndex.WaypointRef fwdRef)
                        ? fwdRef.forward
                        : GetWaypointForward(curSeg, forwardNeighborIndex);
                    float forwardCost = GetLanePenalty(curSeg) +
                                        GetAlignmentPenalty(nextForward, desiredTravelDirection) +
                                        GetTurnPenalty(curForward, nextForward);
                    TryNeighbor(fwdKey, nextPos, forwardCost);
                }

                foreach (RoadConnection conn in curSeg.connections)
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
                    Vector3 nForward = idx.waypointByKey.TryGetValue(nKey, out RoadGraphSpatialIndex.WaypointRef connRef)
                        ? connRef.forward
                        : GetWaypointForward(conn.toSegment, conn.toWaypointIndex);
                    float connectionCost = GetLanePenalty(conn.toSegment) +
                                           GetAlignmentPenalty(nForward, desiredTravelDirection) +
                                           GetTurnPenalty(curForward, nForward);
                    TryNeighbor(nKey, nPos, connectionCost);
                }

                if (transferMaxDistance > 0.01f)
                {
                    Vector2Int curCell = RoadGraphSpatialIndex.ToGridCell(curPos, spatialCellSize);
                    int radius = Mathf.CeilToInt(transferMaxDistance / spatialCellSize);
                    for (int gx = curCell.x - radius; gx <= curCell.x + radius; gx++)
                    {
                        for (int gz = curCell.y - radius; gz <= curCell.y + radius; gz++)
                        {
                            Vector2Int lookupCell = new Vector2Int(gx, gz);
                            if (!idx.spatialGrid.TryGetValue(lookupCell, out List<long> bucket))
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

                                if (!IsTransferCandidateEligible(curSeg, curWpIdx, candidate.segment, candidate.index))
                                {
                                    continue;
                                }

                                if (!TryGetTransferPenalty(curPos, curForward, candidate.position, transferMaxDistance, out float transferPenalty))
                                {
                                    continue;
                                }

                                float transferDistance = Mathf.Sqrt(d2);
                                float normalizedTransferDistance = transferMaxDistance > 0.01f
                                    ? transferDistance / transferMaxDistance
                                    : 1f;
                                float transferCost = (transferDistance * TransferDistancePenaltyMultiplier) +
                                                     (normalizedTransferDistance * normalizedTransferDistance * TransferNormalizedPenalty) +
                                                     transferPenalty +
                                                     GetLanePenalty(candidate.segment) +
                                                     GetAlignmentPenalty(candidate.forward, desiredTravelDirection) +
                                                     GetTurnPenalty(curForward, candidate.forward);
                                TryNeighbor(nKey, candidate.position, transferCost);
                            }
                        }
                    }
                }
            }

            if (!prev.ContainsKey(endKey) && startKey != endKey)
            {
                return false;
            }

            List<long> pathKeys = buffers.pathKeys;
            long cur = endKey;
            while (cur != startKey)
            {
                pathKeys.Add(cur);
                if (!prev.ContainsKey(cur))
                {
                    return false;
                }

                cur = prev[cur];
            }

            pathKeys.Add(startKey);
            pathKeys.Reverse();

            path = new List<Vector3>(pathKeys.Count + 2);
            path.Add(projectedStart);
            for (int i = 0; i < pathKeys.Count; i++)
            {
                RoadGraphSpatialIndex.UnpackKey(pathKeys[i], out int segId, out int wpIdx);
                if (idx.segmentById.TryGetValue(segId, out RoadSegment seg) &&
                    wpIdx < seg.waypoints.Count)
                {
                    path.Add(seg.waypoints[wpIdx].position);
                }
            }

            path.Add(projectedEnd);
            pathCost = gScore.TryGetValue(endKey, out float totalCost)
                ? totalCost
                : CalculatePathLength(path);
            return path.Count >= 2;
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

        private static bool IsTransferCandidateEligible(
            RoadSegment currentSegment,
            int currentWaypointIndex,
            RoadSegment candidateSegment,
            int candidateWaypointIndex)
        {
            if (currentSegment == null || candidateSegment == null || candidateSegment == currentSegment)
            {
                return false;
            }

            return IsNearSegmentEndpoint(currentSegment, currentWaypointIndex) &&
                   IsNearSegmentEndpoint(candidateSegment, candidateWaypointIndex);
        }

        private static bool IsNearSegmentEndpoint(RoadSegment segment, int waypointIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                return false;
            }

            int lastIndex = segment.waypoints.Count - 1;
            return waypointIndex <= TransferEndpointWindow ||
                   waypointIndex >= Mathf.Max(0, lastIndex - TransferEndpointWindow);
        }

        private static bool TryGetTransferPenalty(
            Vector3 currentPosition,
            Vector3 currentForward,
            Vector3 candidatePosition,
            float transferMaxDistance,
            out float transferPenalty)
        {
            transferPenalty = 0f;

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

            float heightDelta = Mathf.Abs(candidatePosition.y - currentPosition.y);
            if (heightDelta > MaxTransferHeightDelta)
            {
                return false;
            }

            float lateralOffset = Vector3.Dot(delta, right);
            if (lateralOffset < -TransferLeftAllowance)
            {
                return false;
            }

            if (lateralOffset < 0f)
            {
                transferPenalty = Mathf.Abs(lateralOffset) * 2.5f;
            }

            transferPenalty += heightDelta * TransferHeightPenalty;

            return true;
        }

        private static List<Vector3> CollectWaypointsBetween(
            RoadSegment segment, int fromIdx, int toIdx,
            Vector3 startWorld, Vector3 endWorld)
        {
            int estimatedWaypointCount = Mathf.Abs(toIdx - fromIdx) + 3;
            var path = new List<Vector3>(estimatedWaypointCount);
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

        private static void AddCandidate(List<WaypointCandidate> candidates, WaypointCandidate candidate)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Key != candidate.Key)
                {
                    continue;
                }

                if (candidate.Score < candidates[i].Score)
                {
                    candidates[i] = candidate;
                }

                return;
            }

            candidates.Add(candidate);
        }

        private static bool ContainsCandidateKey(List<WaypointCandidate> candidates, long key)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKey(List<long> keys, long key)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static float CalculatePathLength(List<Vector3> path)
        {
            if (path == null || path.Count < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }

            return length;
        }
    }
}
