using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem.Spline
{
    public class RoadNetworkManager : MonoBehaviour
    {
        public static RoadNetworkManager Instance { get; private set; }

        public List<SplineRoad> roads = new List<SplineRoad>();
        public List<RoadIntersection> intersections = new List<RoadIntersection>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                RefreshNetwork();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [ContextMenu("Refresh Network")]
        public void RefreshNetwork()
        {
            roads.Clear();
            AddUsableRoads(GetComponentsInChildren<SplineRoad>(true));
            AddUsableRoads(FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

            intersections.Clear();
            HashSet<RoadIntersection> uniqueIntersections = new HashSet<RoadIntersection>();
            RoadIntersection[] childIntersections = GetComponentsInChildren<RoadIntersection>(true);
            if (childIntersections != null)
            {
                for (int i = 0; i < childIntersections.Length; i++)
                {
                    if (childIntersections[i] != null) uniqueIntersections.Add(childIntersections[i]);
                }
            }

            RoadIntersection[] sceneIntersections = FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (sceneIntersections != null)
            {
                for (int i = 0; i < sceneIntersections.Length; i++)
                {
                    if (sceneIntersections[i] != null) uniqueIntersections.Add(sceneIntersections[i]);
                }
            }

            intersections.AddRange(uniqueIntersections);

            foreach (var intersection in intersections)
            {
                if (intersection != null)
                {
                    if (intersection.connectedRoads == null)
                    {
                        intersection.connectedRoads = new List<SplineRoad>();
                    }
                    else
                    {
                        intersection.connectedRoads.Clear();
                    }
                }
            }

            foreach (var road in roads)
            {
                if (road == null)
                {
                    continue;
                }

                road.CalculateDistances();
                if (road.startIntersection != null)
                {
                    road.startIntersection.RegisterConnectedRoad(road);
                    if (!uniqueIntersections.Contains(road.startIntersection))
                    {
                        uniqueIntersections.Add(road.startIntersection);
                        intersections.Add(road.startIntersection);
                    }
                }

                if (road.endIntersection != null)
                {
                    road.endIntersection.RegisterConnectedRoad(road);
                    if (!uniqueIntersections.Contains(road.endIntersection))
                    {
                        uniqueIntersections.Add(road.endIntersection);
                        intersections.Add(road.endIntersection);
                    }
                }
            }
        }

        public SplineRoad GetRandomRoad()
        {
            if (roads.Count == 0) return null;
            return roads[Random.Range(0, roads.Count)];
        }

        public List<SplineRoad> GetRoadsInSector(string sectorTag)
        {
            List<SplineRoad> sectorRoads = new List<SplineRoad>();
            if (string.IsNullOrEmpty(sectorTag)) return sectorRoads;

            for (int i = 0; i < roads.Count; i++)
            {
                SplineRoad r = roads[i];
                if (r != null && r.IsUsable && r.gameObject.activeInHierarchy &&
                    string.Equals(r.sectorTag, sectorTag, System.StringComparison.OrdinalIgnoreCase))
                {
                    sectorRoads.Add(r);
                }
            }
            return sectorRoads;
        }

        public SplineRoad GetRandomRoadInSector(string sectorTag)
        {
            List<SplineRoad> sectorRoads = GetRoadsInSector(sectorTag);
            if (sectorRoads.Count == 0) return GetRandomRoad();
            return sectorRoads[Random.Range(0, sectorRoads.Count)];
        }

        public (SplineRoad road, float t) GetClosestRoadPoint(Vector3 position)
        {
            SplineRoad closestRoad = null;
            float closestT = 0f;
            float minDistSq = float.MaxValue;

            for (int i = 0; i < roads.Count; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable)
                {
                    continue;
                }

                float t = road.GetClosestT(position);
                Vector3 point = road.GetPoint(t);
                float distSq = (point - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestRoad = road;
                    closestT = t;
                }
            }

            return (closestRoad, closestT);
        }

        private void AddUsableRoads(IEnumerable<SplineRoad> discoveredRoads)
        {
            if (discoveredRoads == null) return;

            foreach (SplineRoad road in discoveredRoads)
            {
                if (road == null || roads.Contains(road))
                {
                    continue;
                }

                road.CalculateDistances();
                if (road.IsUsable)
                {
                    roads.Add(road);
                }
            }
        }
    }
}
