using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.EditorTools
{
    public partial class SimpleRoadPlacementTool
    {
        // Only set on the temporary batch instance. Manual SceneView behavior is unchanged.
        private Dictionary<Vector3, int> batchEndpointDegrees;
        private Rect? batchSeaArea;

        internal static void BakePlannedRoads(
            Dictionary<SplineRoad, List<Vector3>> paths,
            Dictionary<RoadIntersection, Transform> junctionParents,
            Rect seaArea,
            float width,
            float pavement,
            float seaHeight)
        {
            var tool = CreateInstance<SimpleRoadPlacementTool>();
            try
            {
                tool.toolEnabled = false;
                tool.snapToGround = false; // The planner already solved the final elevation.
                tool.roadWidth = width;
                tool.sidewalkWidth = pavement;
                tool.intersectionRoadTrimDistance = Mathf.Max(8.5f, width * 0.5f + pavement + 0.5f);
                tool.splineLaneWidth = Mathf.Min(width * 0.25f, 2.1f);
                tool.bridgeSupportBaseY = seaHeight;
                tool.batchEndpointDegrees = new Dictionary<Vector3, int>();
                foreach (var pair in paths)
                {
                    SplineRoad road = pair.Key;
                    List<Vector3> points = pair.Value;
                    tool.batchEndpointDegrees[points[0]] = road.startIntersection.connectedRoads.Count;
                    tool.batchEndpointDegrees[points[points.Count - 1]] = road.endIntersection.connectedRoads.Count;
                }

                int layer = LayerMask.NameToLayer("Road");
                foreach (var pair in paths)
                {
                    SplineRoad road = pair.Key;
                    bool bridge = road.sectorTag == "Highways";
                    tool.targetCategory = bridge ? RoadTargetCategory.HighwaysAndBridges : RoadTargetCategory.Ankara;
                    tool.autoCreateBridges = bridge;
                    tool.batchSeaArea = bridge ? seaArea : (Rect?)null;
                    tool.RebuildRoadVisuals(road.gameObject, pair.Value);
                    SetAsphaltLayer(road.transform, layer);
                }

                tool.autoCreateBridges = false;
                tool.batchSeaArea = null;
                foreach (var pair in junctionParents)
                {
                    RoadIntersection junction = pair.Key;
                    if (junction.connectedRoads.Count < 3) continue;
                    var branches = new List<RoadBranchInfo>();
                    foreach (SplineRoad road in junction.connectedRoads)
                    {
                        bool start = road.startIntersection == junction;
                        Vector3 tangent = start ? road.GetTangent(0f) : -road.GetTangent(1f);
                        tangent.y = 0f;
                        branches.Add(new RoadBranchInfo
                        {
                            Road = road,
                            IsStart = start,
                            Endpoint = road.GetPoint(start ? 0f : 1f),
                            OutgoingDirection = tangent.normalized
                        });
                    }
                    GameObject visual = new GameObject("Intersection_" + junction.name);
                    Undo.RegisterCreatedObjectUndo(visual, "Bake planned intersection");
                    visual.transform.SetParent(pair.Value, true); // Mesh vertices are world-space.
                    tool.BuildProceduralIntersection(visual, junction.transform.position, branches);
                    SetAsphaltLayer(visual.transform, layer);
                }
            }
            finally
            {
                DestroyImmediate(tool);
            }
        }

        private static void SetAsphaltLayer(Transform root, int layer)
        {
            Transform asphalt = root.Find("Asphalt");
            if (asphalt != null && layer >= 0) asphalt.gameObject.layer = layer;
        }
    }
}
