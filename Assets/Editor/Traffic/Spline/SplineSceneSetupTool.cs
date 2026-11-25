using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

namespace TrafficSystem.Spline.Editor
{
    public class SplineSceneSetupTool
    {
        private const string RoutePrefix = "Route_";
        private const string GeneratedNetworkName = "RoadNetworkManager";
        private const string GeneratedRoadsName = "Generated Spline Roads";
        private const string GeneratedIntersectionsName = "Generated Road Intersections";
        private const float EndpointMergeDistance = 5f;
        private const float GeneratedLaneWidth = 0f;
        private const float GridStepTolerance = 0.35f;

        private sealed class EndpointCluster
        {
            public Vector3 Sum;
            public int Count;
            public RoadIntersection Intersection;

            public Vector3 Center => Count > 0 ? Sum / Count : Vector3.zero;

            public void Add(Vector3 position)
            {
                Sum += position;
                Count++;
            }
        }

        private sealed class RoadTileNode
        {
            public Transform Transform;
            public RoadIntersection Intersection;
            public Vector3 Position => Transform.position;
        }

        public static void SetupScene()
        {
            MigrateActiveSceneFromRouteMarkers();
        }

        public static void SetupSceneLegacyMenu()
        {
            MigrateActiveSceneFromRouteMarkers();
        }

        public static void MigrateActiveSceneFromRouteMarkers()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Traffic Migration", "Cannot migrate the road network while playing.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Traffic Migration", "No valid active scene is loaded.", "OK");
                return;
            }

            int removedMissingScripts = RemoveMissingScripts(scene);
            RoadNetworkManager manager = EnsureNetworkManager(scene);
            Transform generatedRoadsRoot = EnsureChildRoot(manager.transform, GeneratedRoadsName);
            Transform intersectionsRoot = EnsureIntersectionsRoot(manager.transform);
            ClearGeneratedChildren(generatedRoadsRoot);
            ClearGeneratedIntersections(intersectionsRoot);

            string source = "Route markers";
            int intersectionsCreated;
            List<SplineRoad> roads = BuildSplineRoadsFromRouteMarkers(scene);
            if (roads.Count == 0)
            {
                roads = BuildSplineRoadsFromRoadTiles(scene, generatedRoadsRoot, intersectionsRoot, out int tileCount, out intersectionsCreated);
                source = $"SimplePoly road tiles ({tileCount} tiles)";
                if (roads.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Traffic Migration",
                        "No Route_ lane markers or SimplePoly road tiles were found in the active scene.",
                        "OK");
                    return;
                }
            }
            else
            {
                intersectionsCreated = BuildIntersections(roads, intersectionsRoot);
            }

            manager.RefreshNetwork();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Traffic Migration",
                $"Spline road network migrated from {source}.\n\nRoads: {roads.Count}\nIntersections: {intersectionsCreated}\nMissing scripts removed: {removedMissingScripts}",
                "OK");
        }

        private static RoadNetworkManager EnsureNetworkManager(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RoadNetworkManager existing = root.GetComponentInChildren<RoadNetworkManager>(true);
                if (existing != null)
                {
                    return existing;
                }
            }

            GameObject managerObject = new GameObject(GeneratedNetworkName);
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create RoadNetworkManager");
            return Undo.AddComponent<RoadNetworkManager>(managerObject);
        }

        private static Transform EnsureIntersectionsRoot(Transform managerTransform)
        {
            return EnsureChildRoot(managerTransform, GeneratedIntersectionsName);
        }

        private static Transform EnsureChildRoot(Transform parent, string childName)
        {
            Transform root = parent.Find(childName);
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(rootObject, $"Create {childName}");
            rootObject.transform.SetParent(parent, false);
            return rootObject.transform;
        }

        private static void ClearGeneratedIntersections(Transform root)
        {
            ClearGeneratedChildren(root);
        }

        private static void ClearGeneratedChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            }
        }

        private static List<SplineRoad> BuildSplineRoadsFromRouteMarkers(Scene scene)
        {
            List<SplineRoad> roads = new List<SplineRoad>(512);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform route = transforms[i];
                    if (route == null || !route.gameObject.activeInHierarchy || !route.name.StartsWith(RoutePrefix))
                    {
                        continue;
                    }

                    if (!TryCollectRouteAnchors(route, out List<Vector3> anchors))
                    {
                        continue;
                    }

                    SplineRoad road = route.GetComponent<SplineRoad>();
                    if (road == null)
                    {
                        road = Undo.AddComponent<SplineRoad>(route.gameObject);
                    }

                    Undo.RecordObject(road, "Configure SplineRoad From Route Markers");
                    road.laneWidth = GeneratedLaneWidth;
                    road.startIntersection = null;
                    road.endIntersection = null;
                    road.SetLinearPath(anchors);
                    EditorUtility.SetDirty(road);
                    roads.Add(road);
                }
            }

            return roads;
        }

        private static List<SplineRoad> BuildSplineRoadsFromRoadTiles(
            Scene scene,
            Transform roadsRoot,
            Transform intersectionsRoot,
            out int tileCount,
            out int intersectionsCreated)
        {
            List<RoadTileNode> nodes = CollectRoadTileNodes(scene);
            tileCount = nodes.Count;
            intersectionsCreated = 0;

            List<SplineRoad> roads = new List<SplineRoad>(nodes.Count * 2);
            if (nodes.Count < 2 || !TryResolveGridStep(nodes, out float gridStep))
            {
                return roads;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                RoadTileNode node = nodes[i];
                GameObject intersectionObject = new GameObject($"RoadTileIntersection_{i:0000}_{node.Transform.name}");
                Undo.RegisterCreatedObjectUndo(intersectionObject, "Create Road Tile Intersection");
                intersectionObject.transform.SetParent(intersectionsRoot, false);
                intersectionObject.transform.position = node.Position;

                RoadIntersection intersection = Undo.AddComponent<RoadIntersection>(intersectionObject);
                intersection.isStopSignControlled = IsStopControlledTile(node.Transform.name);
                node.Intersection = intersection;
                intersectionsCreated++;
            }

            float maxNeighborDistance = gridStep * (1f + GridStepTolerance);
            float maxNeighborDistanceSqr = maxNeighborDistance * maxNeighborDistance;
            int roadIndex = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Vector3 delta = nodes[j].Position - nodes[i].Position;
                    delta.y = 0f;
                    float distanceSqr = delta.sqrMagnitude;
                    if (distanceSqr < 0.01f || distanceSqr > maxNeighborDistanceSqr)
                    {
                        continue;
                    }

                    if (!IsMostlyCardinal(delta))
                    {
                        continue;
                    }

                    SplineRoad road = CreateGeneratedRoad(roadsRoot, roadIndex++, nodes[i], nodes[j]);
                    roads.Add(road);
                }
            }

            return roads;
        }

        private static List<RoadTileNode> CollectRoadTileNodes(Scene scene)
        {
            List<RoadTileNode> nodes = new List<RoadTileNode>(512);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy || !IsRoadTileName(candidate.name))
                    {
                        continue;
                    }

                    nodes.Add(new RoadTileNode { Transform = candidate });
                }
            }

            return nodes;
        }

        private static bool IsRoadTileName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            if (!objectName.StartsWith("Road ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("lane") ||
                   lowerName.Contains("intersection") ||
                   lowerName.Contains("corner");
        }

        private static bool IsStopControlledTile(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("intersection") || lowerName.Contains("corner");
        }

        private static bool TryResolveGridStep(List<RoadTileNode> nodes, out float gridStep)
        {
            gridStep = 0f;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Vector3 delta = nodes[j].Position - nodes[i].Position;
                    delta.y = 0f;

                    if (!IsMostlyCardinal(delta))
                    {
                        continue;
                    }

                    float distanceSqr = delta.sqrMagnitude;
                    if (distanceSqr > 4f && distanceSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = distanceSqr;
                    }
                }
            }

            if (bestDistanceSqr == float.MaxValue)
            {
                return false;
            }

            gridStep = Mathf.Sqrt(bestDistanceSqr);
            return gridStep > 0.1f;
        }

        private static bool IsMostlyCardinal(Vector3 delta)
        {
            float absX = Mathf.Abs(delta.x);
            float absZ = Mathf.Abs(delta.z);
            float larger = Mathf.Max(absX, absZ);
            float smaller = Mathf.Min(absX, absZ);
            return larger > 0.1f && smaller <= larger * 0.2f;
        }

        private static SplineRoad CreateGeneratedRoad(Transform roadsRoot, int roadIndex, RoadTileNode start, RoadTileNode end)
        {
            GameObject roadObject = new GameObject($"SplineRoad_{roadIndex:00000}_{start.Transform.name}_To_{end.Transform.name}");
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Generated SplineRoad");
            roadObject.transform.SetParent(roadsRoot, false);

            SplineRoad road = Undo.AddComponent<SplineRoad>(roadObject);
            road.laneWidth = 1.4f;
            road.startIntersection = start.Intersection;
            road.endIntersection = end.Intersection;
            road.SetLinearPath(new[] { start.Position, end.Position });

            start.Intersection.RegisterConnectedRoad(road);
            end.Intersection.RegisterConnectedRoad(road);
            EditorUtility.SetDirty(road);

            return road;
        }

        private static bool TryCollectRouteAnchors(Transform route, out List<Vector3> anchors)
        {
            anchors = new List<Vector3>(route.childCount);
            for (int i = 0; i < route.childCount; i++)
            {
                Transform child = route.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    anchors.Add(child.position);
                }
            }

            return anchors.Count >= 2;
        }

        private static int BuildIntersections(List<SplineRoad> roads, Transform intersectionsRoot)
        {
            List<EndpointCluster> clusters = new List<EndpointCluster>();
            float mergeDistanceSqr = EndpointMergeDistance * EndpointMergeDistance;

            for (int i = 0; i < roads.Count; i++)
            {
                RegisterEndpoint(clusters, roads[i].GetPoint(0f), mergeDistanceSqr);
                RegisterEndpoint(clusters, roads[i].GetPoint(1f), mergeDistanceSqr);
            }

            int created = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                EndpointCluster cluster = clusters[i];
                GameObject intersectionObject = new GameObject($"RoadIntersection_{i:0000}");
                Undo.RegisterCreatedObjectUndo(intersectionObject, "Create Road Intersection");
                intersectionObject.transform.SetParent(intersectionsRoot, false);
                intersectionObject.transform.position = cluster.Center;

                RoadIntersection intersection = Undo.AddComponent<RoadIntersection>(intersectionObject);
                intersection.isStopSignControlled = false;
                cluster.Intersection = intersection;
                created++;
            }

            for (int i = 0; i < roads.Count; i++)
            {
                SplineRoad road = roads[i];
                Undo.RecordObject(road, "Assign Road Intersections");
                road.startIntersection = FindCluster(clusters, road.GetPoint(0f), mergeDistanceSqr)?.Intersection;
                road.endIntersection = FindCluster(clusters, road.GetPoint(1f), mergeDistanceSqr)?.Intersection;
                road.startIntersection?.RegisterConnectedRoad(road);
                road.endIntersection?.RegisterConnectedRoad(road);
                EditorUtility.SetDirty(road);
            }

            return created;
        }

        private static void RegisterEndpoint(List<EndpointCluster> clusters, Vector3 position, float mergeDistanceSqr)
        {
            EndpointCluster cluster = FindCluster(clusters, position, mergeDistanceSqr);
            if (cluster == null)
            {
                cluster = new EndpointCluster();
                clusters.Add(cluster);
            }

            cluster.Add(position);
        }

        private static EndpointCluster FindCluster(List<EndpointCluster> clusters, Vector3 position, float mergeDistanceSqr)
        {
            for (int i = 0; i < clusters.Count; i++)
            {
                Vector3 delta = clusters[i].Center - position;
                if (delta.sqrMagnitude <= mergeDistanceSqr)
                {
                    return clusters[i];
                }
            }

            return null;
        }

        private static int RemoveMissingScripts(Scene scene)
        {
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    GameObject obj = transforms[i].gameObject;
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj);
                    if (count <= 0)
                    {
                        continue;
                    }

                    Undo.RegisterCompleteObjectUndo(obj, "Remove Missing Script Components");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                    removed += count;
                    EditorUtility.SetDirty(obj);
                }
            }

            return removed;
        }
    }
}
