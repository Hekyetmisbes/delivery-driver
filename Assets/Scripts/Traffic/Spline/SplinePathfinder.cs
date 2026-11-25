using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem.Spline
{
    public static class SplinePathfinder
    {
        public struct PathResult
        {
            public List<Vector3> worldPoints;
            public List<SplineRoad> roadSequence;
        }

        public static List<SplineRoad> FindRoadPath(SplineRoad startRoad, RoadIntersection exitIntersection, SplineRoad endRoad)
        {
            if (startRoad == null || endRoad == null || exitIntersection == null) return new List<SplineRoad>();
            
            PathResult result = new PathResult();
            result.worldPoints = new List<Vector3>();
            result.roadSequence = new List<SplineRoad>();

            if (startRoad == endRoad)
            {
                result.roadSequence.Add(startRoad);
                return result.roadSequence;
            }

            SearchRoadNetworkFromExit(startRoad, exitIntersection, endRoad, ref result);
            return result.roadSequence;
        }

        private static void SearchRoadNetworkFromExit(SplineRoad startRoad, RoadIntersection exitIntersection, SplineRoad endRoad, ref PathResult result)
        {
            // A* Search on Intersections graph starting from a specific exit
            Dictionary<RoadIntersection, float> gScore = new Dictionary<RoadIntersection, float>();
            Dictionary<RoadIntersection, float> fScore = new Dictionary<RoadIntersection, float>();
            Dictionary<RoadIntersection, SplineRoad> cameFromEdge = new Dictionary<RoadIntersection, SplineRoad>();
            Dictionary<RoadIntersection, RoadIntersection> cameFromNode = new Dictionary<RoadIntersection, RoadIntersection>();

            List<RoadIntersection> openSet = new List<RoadIntersection>();

            Vector3 targetPos = endRoad.transform.position;

            // Initialize only with the preferred exit intersection
            float cost = startRoad.GetTotalLength() * 0.5f;
            gScore[exitIntersection] = cost;
            fScore[exitIntersection] = cost + Vector3.Distance(exitIntersection.transform.position, targetPos);
            cameFromEdge[exitIntersection] = startRoad;
            openSet.Add(exitIntersection);

            RoadIntersection bestEndIntersection = null;

            while (openSet.Count > 0)
            {
                RoadIntersection current = openSet[0];
                float minF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (fScore.TryGetValue(openSet[i], out float f) && f < minF)
                    {
                        minF = f;
                        current = openSet[i];
                    }
                }

                if (current == endRoad.startIntersection || current == endRoad.endIntersection)
                {
                    bestEndIntersection = current;
                    break;
                }

                openSet.Remove(current);

                if (current.connectedRoads == null) continue;

                foreach (SplineRoad road in current.connectedRoads)
                {
                    if (road == null) continue;
                    RoadIntersection neighbor = (road.startIntersection == current) ? road.endIntersection : road.startIntersection;
                    if (neighbor == null) continue;

                    float tentativeG = gScore[current] + road.GetTotalLength();

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFromNode[neighbor] = current;
                        cameFromEdge[neighbor] = road;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Vector3.Distance(neighbor.transform.position, targetPos);
                        
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            if (bestEndIntersection != null)
            {
                List<RoadIntersection> nodePath = new List<RoadIntersection>();
                RoadIntersection currNode = bestEndIntersection;

                nodePath.Add(currNode);
                while (cameFromNode.ContainsKey(currNode))
                {
                    currNode = cameFromNode[currNode];
                    nodePath.Add(currNode);
                }

                nodePath.Reverse();

                result.roadSequence.Add(startRoad);

                for (int i = 1; i < nodePath.Count; i++)
                {
                    RoadIntersection fromNode = nodePath[i - 1];
                    RoadIntersection toNode = nodePath[i];
                    SplineRoad road = cameFromEdge[toNode];
                    if (road == null) continue;

                    result.roadSequence.Add(road);
                }

                if (endRoad != result.roadSequence[result.roadSequence.Count - 1])
                {
                    result.roadSequence.Add(endRoad);
                }
            }
        }

        public static List<SplineRoad> FindRoadPath(SplineRoad startRoad, SplineRoad endRoad)
        {
            if (startRoad == null || endRoad == null) return new List<SplineRoad>();
            
            PathResult result = new PathResult();
            result.worldPoints = new List<Vector3>();
            result.roadSequence = new List<SplineRoad>();

            if (startRoad == endRoad)
            {
                result.roadSequence.Add(startRoad);
                return result.roadSequence;
            }

            // Perform A* search using the roads as start/end
            SearchRoadNetwork(startRoad, endRoad, Vector3.zero, Vector3.zero, false, ref result);
            return result.roadSequence;
        }

        public static List<SplineRoad> FindRoadPath(Vector3 startPos, Vector3 endPos)
        {
            var result = FindPath(startPos, endPos);
            return result.roadSequence ?? new List<SplineRoad>();
        }

        public static PathResult FindPath(Vector3 startPos, Vector3 endPos, float sampleResolution = 5f)
        {
            PathResult result = new PathResult();
            result.worldPoints = new List<Vector3>();
            result.roadSequence = new List<SplineRoad>();

            var network = RoadNetworkManager.Instance;
            if (network == null || network.roads.Count == 0) return result;

            // 1. Snap start and end to closest roads
            var startRoadInfo = GetClosestRoadPoint(startPos);
            var endRoadInfo = GetClosestRoadPoint(endPos);

            if (startRoadInfo.road == null || endRoadInfo.road == null) return result;

            // 2. If same road, just output the points between them
            if (startRoadInfo.road == endRoadInfo.road)
            {
                result.worldPoints = SampleRoadRange(startRoadInfo.road, startRoadInfo.t, endRoadInfo.t, sampleResolution);
                result.roadSequence.Add(startRoadInfo.road);
                return result;
            }

            SearchRoadNetwork(startRoadInfo.road, endRoadInfo.road, startPos, endPos, true, ref result, startRoadInfo.t, endRoadInfo.t, sampleResolution);
            return result;
        }

        private static void SearchRoadNetwork(SplineRoad startRoad, SplineRoad endRoad, Vector3 startPos, Vector3 endPos, bool samplePoints, ref PathResult result, float startT = 0, float endT = 0, float sampleResolution = 5f)
        {
            var network = RoadNetworkManager.Instance;
            
            // A* Search on Intersections graph
            Dictionary<RoadIntersection, float> gScore = new Dictionary<RoadIntersection, float>();
            Dictionary<RoadIntersection, float> fScore = new Dictionary<RoadIntersection, float>();
            Dictionary<RoadIntersection, SplineRoad> cameFromEdge = new Dictionary<RoadIntersection, SplineRoad>();
            Dictionary<RoadIntersection, RoadIntersection> cameFromNode = new Dictionary<RoadIntersection, RoadIntersection>();

            List<RoadIntersection> openSet = new List<RoadIntersection>();

            Vector3 targetPos = endPos == Vector3.zero ? endRoad.transform.position : endPos;

            // Initialize start nodes
            if (startRoad.startIntersection != null)
            {
                float cost = samplePoints ? startRoad.TToDistance(startT) : startRoad.GetTotalLength() * 0.5f;
                gScore[startRoad.startIntersection] = cost;
                fScore[startRoad.startIntersection] = cost + Vector3.Distance(startRoad.startIntersection.transform.position, targetPos);
                cameFromEdge[startRoad.startIntersection] = startRoad;
                openSet.Add(startRoad.startIntersection);
            }
            if (startRoad.endIntersection != null)
            {
                float cost = samplePoints ? (startRoad.GetTotalLength() - startRoad.TToDistance(startT)) : startRoad.GetTotalLength() * 0.5f;
                gScore[startRoad.endIntersection] = cost;
                fScore[startRoad.endIntersection] = cost + Vector3.Distance(startRoad.endIntersection.transform.position, targetPos);
                cameFromEdge[startRoad.endIntersection] = startRoad;
                openSet.Add(startRoad.endIntersection);
            }

            RoadIntersection bestEndIntersection = null;

            while (openSet.Count > 0)
            {
                RoadIntersection current = openSet[0];
                float minF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (fScore.TryGetValue(openSet[i], out float f) && f < minF)
                    {
                        minF = f;
                        current = openSet[i];
                    }
                }

                if (current == endRoad.startIntersection || current == endRoad.endIntersection)
                {
                    bestEndIntersection = current;
                    break;
                }

                openSet.Remove(current);

                if (current.connectedRoads == null) continue;

                foreach (SplineRoad road in current.connectedRoads)
                {
                    if (road == null) continue;
                    RoadIntersection neighbor = (road.startIntersection == current) ? road.endIntersection : road.startIntersection;
                    if (neighbor == null) continue;

                    float tentativeG = gScore[current] + road.GetTotalLength();

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFromNode[neighbor] = current;
                        cameFromEdge[neighbor] = road;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Vector3.Distance(neighbor.transform.position, targetPos);
                        
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            if (bestEndIntersection != null)
            {
                List<RoadIntersection> nodePath = new List<RoadIntersection>();
                RoadIntersection currNode = bestEndIntersection;

                nodePath.Add(currNode);
                while (cameFromNode.ContainsKey(currNode))
                {
                    currNode = cameFromNode[currNode];
                    nodePath.Add(currNode);
                }

                nodePath.Reverse();

                result.roadSequence.Add(startRoad);
                if (samplePoints)
                {
                    RoadIntersection startExit = nodePath[0];
                    float startExitT = startExit == startRoad.startIntersection ? 0f : 1f;
                    AppendRoadRange(result.worldPoints, startRoad, startT, startExitT, sampleResolution);
                }

                for (int i = 1; i < nodePath.Count; i++)
                {
                    RoadIntersection fromNode = nodePath[i - 1];
                    RoadIntersection toNode = nodePath[i];
                    SplineRoad road = cameFromEdge[toNode];
                    if (road == null) continue;

                    result.roadSequence.Add(road);

                    if (samplePoints)
                    {
                        float fromT = fromNode == road.startIntersection ? 0f : 1f;
                        float toT = toNode == road.startIntersection ? 0f : 1f;
                        AppendRoadRange(result.worldPoints, road, fromT, toT, sampleResolution);
                    }
                }

                if (endRoad != result.roadSequence[result.roadSequence.Count - 1])
                {
                    result.roadSequence.Add(endRoad);
                }

                if (samplePoints)
                {
                    float endEntryT = bestEndIntersection == endRoad.startIntersection ? 0f : 1f;
                    AppendRoadRange(result.worldPoints, endRoad, endEntryT, endT, sampleResolution);
                }
            }
        }

        public static (SplineRoad road, float t) GetClosestRoadPoint(Vector3 position)
        {
            SplineRoad closestRoad = null;
            float closestT = 0f;
            float minDistSq = float.MaxValue;

            var network = RoadNetworkManager.Instance;
            if (network != null)
            {
                foreach (var road in network.roads)
                {
                    if (road == null || !road.IsUsable)
                    {
                        continue;
                    }

                    float t = road.GetClosestT(position);
                    Vector3 p = road.GetPoint(t);
                    float distSq = (p - position).sqrMagnitude;
                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        closestRoad = road;
                        closestT = t;
                    }
                }
            }

            return (closestRoad, closestT);
        }

        private static List<Vector3> SampleRoadRange(SplineRoad road, float tStart, float tEnd, float resolution)
        {
            List<Vector3> pts = new List<Vector3>();
            AppendRoadRange(pts, road, tStart, tEnd, resolution);
            return pts;
        }

        private static void AppendRoadRange(List<Vector3> points, SplineRoad road, float tStart, float tEnd, float resolution)
        {
            if (points == null || road == null)
            {
                return;
            }

            float dStart = road.TToDistance(tStart);
            float dEnd = road.TToDistance(tEnd);
            float distance = Mathf.Abs(dEnd - dStart);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1f, resolution)));

            for (int i = 0; i <= steps; i++)
            {
                float alpha = i / (float)steps;
                float d = Mathf.Lerp(dStart, dEnd, alpha);
                AppendIfFarEnough(points, road.GetPoint(road.DistanceToT(d)));
            }
        }

        private static void AppendIfFarEnough(List<Vector3> points, Vector3 point)
        {
            if (points == null)
            {
                return;
            }

            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 1f)
            {
                points.Add(point);
            }
        }
    }
}
