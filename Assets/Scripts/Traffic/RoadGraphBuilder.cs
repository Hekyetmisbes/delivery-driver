using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Builds a waypoint graph from EasyRoads3D road network
    /// Supports both automatic extraction and manual waypoint assignment
    /// </summary>
    public class RoadGraphBuilder : MonoBehaviour
    {
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Type> ComponentTypeCache = new Dictionary<string, Type>();
        private static readonly string[] RoadCollectionFieldNames = { "roads", "roadObjects" };

        [Header("Road Sampling Settings")]
        [Tooltip("Distance between sampled waypoints (meters)")]
        [SerializeField] private float sampleStepMeters = 5f;

        [Tooltip("Max distance to connect road endpoints (meters)")]
        [SerializeField] private float connectionThresholdMeters = 3f;

        [Header("EasyRoads3D Detection")]
        [Tooltip("Try to auto-detect EasyRoads3D network in scene")]
        [SerializeField] private bool autoDetectRoads = true;

        [Tooltip("Manual road network root (if auto-detection fails)")]
        [SerializeField] private GameObject roadNetworkRoot;

        [Header("SimplePoly City Detection")]
        [Tooltip("Include SimplePoly City road prefabs (Road Lane, Road Corner, Road Intersection, etc.) in generated graph")]
        [SerializeField] private bool includeSimplePolyRoads = true;
        [Tooltip("Generate separate left/right lane segments for SimplePoly roads")]
        [SerializeField] private bool generateDualLaneSegmentsForSimplePoly = true;
        [Tooltip("Fallback lane center offset from road centerline (meters)")]
        [SerializeField] private float simplePolyLaneCenterOffset = 1.5f;
        [Tooltip("If graph build yields too few segments, auto-try SimplePoly dual-lane extraction as recovery")]
        [SerializeField] private bool autoRecoverSimplePolyLaneExtraction = true;

        [Header("Startup")]
        [Tooltip("Build road graph automatically on Start")]
        [SerializeField] private bool buildOnStart = true;
        [Tooltip("Delay before road graph build starts")]
        [SerializeField] private float startupBuildDelay = 0f;

        [Header("Debug Visualization")]
        [SerializeField] private bool showWaypoints = false;
        [SerializeField] private bool showConnections = true;
        [SerializeField] private bool showMidSegmentConnections = false;
        [SerializeField] private bool showConnectionsOnlyWhenSelected = true;
        [SerializeField] private bool showWaypointForward = false;
        [SerializeField] private float waypointGizmoSize = 0.5f;

        // Built road graph
        private RoadGraph roadGraph;
        public RoadGraph RoadGraph => roadGraph;
        public bool HasBuiltRoadGraph => roadGraph != null && roadGraph.roadSegments != null && roadGraph.roadSegments.Count > 0;
        public bool HasPendingBuild => deferredBuildCoroutine != null;
        private Coroutine deferredBuildCoroutine;

        private void Start()
        {
            if (buildOnStart)
            {
                BeginBuildWithDelay(startupBuildDelay);
            }
        }

        public void BeginBuildWithDelay(float delaySeconds)
        {
            if (deferredBuildCoroutine != null)
            {
                StopCoroutine(deferredBuildCoroutine);
            }

            deferredBuildCoroutine = StartCoroutine(BuildRoadGraphDeferred(Mathf.Max(0f, delaySeconds)));
        }

        private IEnumerator BuildRoadGraphDeferred(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            BuildRoadGraph();
            deferredBuildCoroutine = null;
        }

        /// <summary>
        /// Main method to build the road graph
        /// </summary>
        [ContextMenu("Rebuild Road Graph")]
        public void BuildRoadGraph()
        {
            roadGraph = new RoadGraph();

            if (autoDetectRoads)
            {
                // Try to find EasyRoads3D network automatically
                GameObject network = FindEasyRoadsNetwork();
                if (network != null)
                {
                    Debug.Log($"[RoadGraphBuilder] Found EasyRoads3D network: {network.name}");
                    ExtractRoadsFromNetwork(network);
                }
                else
                {
                    Debug.LogWarning("[RoadGraphBuilder] EasyRoads3D network not found. Assign roadNetworkRoot manually.");
                }
            }
            else if (roadNetworkRoot != null)
            {
                ExtractRoadsFromNetwork(roadNetworkRoot);
            }

            if (includeSimplePolyRoads)
            {
                ExtractSimplePolyRoadMeshes(generateDualLaneSegmentsForSimplePoly);
            }

            TryAutoRecoverSimplePolyLanes();

            // Build connections between road segments
            BuildConnections();

            // Pre-build the spatial index so the first FindPath call does not stall.
            // The pathfinder uses Mathf.Max(1f, transferMaxDistance) as cell size.
            // Pre-build with a common transfer distance to avoid lazy first-call cost.
            const float preBuildCellSize = 24f;
            roadGraph.GetOrBuildSpatialIndex(preBuildCellSize);

            Debug.Log($"[RoadGraphBuilder] Built road graph: {roadGraph.roadSegments.Count} segments, " +
                     $"{GetTotalWaypointCount()} waypoints");
        }

        /// <summary>
        /// Find EasyRoads3D network in scene
        /// </summary>
        private GameObject FindEasyRoadsNetwork()
        {
            // Try to find by typical EasyRoads3D root name
            GameObject network = GameObject.Find("Road Network");
            if (network != null) return network;

            // Try to find by component (using reflection to avoid hard dependency)
            network = FindObjectWithComponent("ERRoadNetwork");
            if (network != null) return network;

            // Fallback: find any object with "Road" in hierarchy root
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("Road") && root.transform.childCount > 0)
                {
                    Debug.Log($"[RoadGraphBuilder] Found potential road network: {root.name}");
                    return root;
                }
            }

            return null;
        }

        /// <summary>
        /// Find GameObject with specific component type name
        /// </summary>
        private GameObject FindObjectWithComponent(string componentTypeName)
        {
            Type type = ResolveComponentType(componentTypeName);
            if (type == null)
                return null;

            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Component comp = root.GetComponentInChildren(type);
                if (comp != null)
                    return comp.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Extract roads from network GameObject
        /// </summary>
        private void ExtractRoadsFromNetwork(GameObject network)
        {
            // Strategy 1: Try EasyRoads3D API via reflection
            bool extracted = TryExtractViaEasyRoadsAPI(network);

            // Strategy 2: Fallback - extract from child transforms
            if (!extracted)
            {
                Debug.Log("[RoadGraphBuilder] Using fallback: extracting from child transforms");
                ExtractFromChildTransforms(network);
            }
        }

        /// <summary>
        /// Try to use EasyRoads3D API via reflection
        /// </summary>
        private bool TryExtractViaEasyRoadsAPI(GameObject network)
        {
            try
            {
                Component roadNetwork = FindEasyRoadsNetworkComponent(network);
                if (roadNetwork == null)
                {
                    Debug.Log("[RoadGraphBuilder] EasyRoads3D network component not found. Falling back to transform extraction.");
                    return false;
                }

                // Try to access roads array/list
                System.Type networkType = roadNetwork.GetType();
                FieldInfo roadsField = null;
                foreach (string fieldName in RoadCollectionFieldNames)
                {
                    roadsField = networkType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (roadsField != null) break;
                }

                if (roadsField == null)
                {
                    foreach (string fieldName in RoadCollectionFieldNames)
                    {
                        PropertyInfo roadsProp = networkType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (roadsProp != null)
                        {
                            object roadsObj = roadsProp.GetValue(roadNetwork, null);
                            if (TrySampleRoadList(roadsObj))
                                return true;
                        }
                    }
                }
                else
                {
                    object roadsObj = roadsField.GetValue(roadNetwork);
                    if (TrySampleRoadList(roadsObj))
                        return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoadGraphBuilder] EasyRoads3D API reflection failed: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// Sample a road object using reflection to get spline points
        /// </summary>
        private void SampleRoadObject(object roadObj, int index)
        {
            try
            {
                System.Type roadType = roadObj.GetType();

                // Try to get GameObject reference
                PropertyInfo goProperty = roadType.GetProperty("gameObject");
                if (goProperty == null)
                {
                    FieldInfo goField = roadType.GetField("roadObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (goField != null)
                    {
                        GameObject roadGO = goField.GetValue(roadObj) as GameObject;
                        if (roadGO != null)
                        {
                            SampleRoadFromGameObject(roadGO, index);
                            return;
                        }
                    }
                }
                else
                {
                    GameObject roadGO = goProperty.GetValue(roadObj) as GameObject;
                    if (roadGO != null)
                    {
                        SampleRoadFromGameObject(roadGO, index);
                        return;
                    }
                }

                // If no GameObject, try sampling markers directly from road object
                if (TrySampleFromRoadObjectMarkers(roadObj, index))
                    return;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoadGraphBuilder] Failed to sample road {index}: {e.Message}");
            }
        }

        /// <summary>
        /// Sample waypoints from a road GameObject
        /// </summary>
        private void SampleRoadFromGameObject(GameObject roadGO, int segmentId)
        {
            // Try to sample from any EasyRoads component on this object
            if (TrySampleFromEasyRoadsComponent(roadGO, segmentId))
                return;

            if (TrySampleFromMarkerComponents(roadGO.transform, segmentId))
                return;

            // Avoid mesh centerline for EasyRoads roads (curved loops produce bad paths)
            if (!IsEasyRoadsObject(roadGO) && TrySampleFromMeshHierarchy(roadGO.transform, segmentId))
                return;

            // Fallback: sample from child transforms
            SampleFromTransformHierarchy(roadGO.transform, segmentId);
        }

        /// <summary>
        /// Try to sample from ERRoad component using reflection
        /// </summary>
        private bool TrySampleFromERRoadComponent(Component erRoad, int segmentId)
        {
            try
            {
                System.Type roadType = erRoad.GetType();

                // Try to find GetSplinePointAt or similar method
                MethodInfo getSplineMethod = roadType.GetMethod("GetSplinePointAt", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getRoadLengthMethod = roadType.GetMethod("GetLength", BindingFlags.Public | BindingFlags.Instance);

                if (getSplineMethod != null && getRoadLengthMethod != null)
                {
                    float roadLength = (float)getRoadLengthMethod.Invoke(erRoad, null);
                    int sampleCount = Mathf.Max(2, Mathf.CeilToInt(roadLength / sampleStepMeters));

                    RoadSegment segment = new RoadSegment(segmentId, erRoad.gameObject.name);

                    for (int i = 0; i < sampleCount; i++)
                    {
                        float t = (float)i / (sampleCount - 1);
                        float distance = t * roadLength;

                        object result = getSplineMethod.Invoke(erRoad, new object[] { distance });
                        if (result is Vector3 position)
                        {
                            Vector3 forward = Vector3.forward;
                            if (i < sampleCount - 1)
                            {
                                object nextResult = getSplineMethod.Invoke(erRoad, new object[] { distance + sampleStepMeters });
                                if (nextResult is Vector3 nextPos)
                                {
                                    forward = (nextPos - position).normalized;
                                }
                            }

                            segment.waypoints.Add(new Waypoint(position, forward, segmentId));
                        }
                    }

                    if (segment.waypoints.Count > 0)
                    {
                        RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
                        roadGraph.roadSegments.Add(segment);
                        Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' via ERRoad API: {segment.waypoints.Count} waypoints");
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoadGraphBuilder] ERRoad sampling failed: {e.Message}");
            }

            // Fallback to marker-based sampling
            if (TrySampleFromERRoadMarkers(erRoad, segmentId))
                return true;

            return false;
        }

        /// <summary>
        /// Try to sample waypoints from ERRoad marker data
        /// </summary>
        private bool TrySampleFromERRoadMarkers(Component erRoad, int segmentId)
        {
            List<Vector3> markerPositions = ExtractMarkerPositions(erRoad);
            if (markerPositions == null || markerPositions.Count < 2)
                return false;

            List<Vector3> sampled = RoadGraphMeshSampler.ResamplePolyline(markerPositions, sampleStepMeters);
            if (sampled.Count < 2)
                return false;

            RoadSegment segment = new RoadSegment(segmentId, erRoad.gameObject.name);

            for (int i = 0; i < sampled.Count; i++)
            {
                Vector3 pos = sampled[i];
                Vector3 forward = Vector3.forward;

                if (i < sampled.Count - 1)
                    forward = (sampled[i + 1] - pos).normalized;
                else if (i > 0)
                    forward = (pos - sampled[i - 1]).normalized;

                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;

                segment.waypoints.Add(new Waypoint(pos, forward, segmentId));
            }

            RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
            roadGraph.roadSegments.Add(segment);
            Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from markers: {segment.waypoints.Count} waypoints");
            return true;
        }

        /// <summary>
        /// Extract marker positions from ERRoad component using reflection
        /// </summary>
        private List<Vector3> ExtractMarkerPositions(Component erRoad)
        {
            return ExtractMarkerPositions((object)erRoad);
        }

        private List<Vector3> ExtractMarkerPositions(object roadOrMarkerContainer)
        {
            if (roadOrMarkerContainer == null) return null;

            Type roadType = roadOrMarkerContainer.GetType();
            List<Vector3> positions = new List<Vector3>();

            // Prefer common field names first
            string[] candidateNames =
            {
                "markers", "markersExt", "markerList", "markerObjects", "markerScripts",
                "roadMarkers", "markerPositions", "markersPositions"
            };

            foreach (string name in candidateNames)
            {
                FieldInfo field = roadType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && TryExtractPositionsFromValue(field.GetValue(roadOrMarkerContainer), positions))
                    return positions;

                PropertyInfo prop = roadType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && TryExtractPositionsFromValue(prop.GetValue(roadOrMarkerContainer, null), positions))
                    return positions;
            }

            // Fallback: scan any field with "marker" in name
            foreach (FieldInfo field in roadType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!field.Name.ToLower().Contains("marker")) continue;

                if (TryExtractPositionsFromValue(field.GetValue(roadOrMarkerContainer), positions))
                    return positions;
            }

            // Fallback: scan any property with "marker" in name
            foreach (PropertyInfo prop in roadType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!prop.Name.ToLower().Contains("marker")) continue;

                if (TryExtractPositionsFromValue(prop.GetValue(roadOrMarkerContainer, null), positions))
                    return positions;
            }

            return positions.Count >= 2 ? positions : null;
        }

        private bool TryExtractPositionsFromValue(object value, List<Vector3> positions)
        {
            if (value == null) return false;

            if (value is IList list)
            {
                positions.Clear();
                foreach (object item in list)
                {
                    if (TryGetPositionFromObject(item, out Vector3 pos))
                        positions.Add(pos);
                }
                return positions.Count >= 2;
            }

            if (value is IEnumerable enumerable)
            {
                positions.Clear();
                foreach (object item in enumerable)
                {
                    if (TryGetPositionFromObject(item, out Vector3 pos))
                        positions.Add(pos);
                }
                return positions.Count >= 2;
            }

            return false;
        }

        private bool TryGetPositionFromObject(object obj, out Vector3 position)
        {
            position = Vector3.zero;
            if (obj == null) return false;

            if (obj is Vector3 vec)
            {
                position = vec;
                return true;
            }

            if (obj is Transform t)
            {
                position = t.position;
                return true;
            }

            if (obj is GameObject go)
            {
                position = go.transform.position;
                return true;
            }

            if (obj is Component comp)
            {
                position = comp.transform.position;
                return true;
            }

            Type objType = obj.GetType();
            PropertyInfo posProp = objType.GetProperty("position", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (posProp != null && posProp.PropertyType == typeof(Vector3))
            {
                position = (Vector3)posProp.GetValue(obj, null);
                return true;
            }

            FieldInfo posField = objType.GetField("position", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (posField != null && posField.FieldType == typeof(Vector3))
            {
                position = (Vector3)posField.GetValue(obj);
                return true;
            }

            PropertyInfo trProp = objType.GetProperty("transform", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (trProp != null && typeof(Transform).IsAssignableFrom(trProp.PropertyType))
            {
                Transform tr = trProp.GetValue(obj, null) as Transform;
                if (tr != null)
                {
                    position = tr.position;
                    return true;
                }
            }

            return false;
        }

        private bool TrySampleFromMarkerComponents(Transform root, int segmentId)
        {
            Type markerType = ResolveComponentType("ERMarkerExt") ?? ResolveComponentType("EasyRoads3Dv3.ERMarkerExt");
            if (markerType == null) return false;

            Component[] markers = root.GetComponentsInChildren(markerType, true);
            if (markers == null || markers.Length < 2) return false;

            List<(float order, Vector3 pos)> ordered = new List<(float, Vector3)>();
            foreach (Component marker in markers)
            {
                float order = GetMarkerOrder(marker);
                ordered.Add((order, marker.transform.position));
            }

            ordered.Sort((a, b) => a.order.CompareTo(b.order));

            List<Vector3> positions = new List<Vector3>();
            foreach (var item in ordered)
                positions.Add(item.pos);

            List<Vector3> sampled = RoadGraphMeshSampler.ResamplePolyline(positions, sampleStepMeters);
            if (sampled.Count < 2) return false;

            RoadSegment segment = new RoadSegment(segmentId, root.name);
            for (int i = 0; i < sampled.Count; i++)
            {
                Vector3 pos = sampled[i];
                Vector3 forward = Vector3.forward;
                if (i < sampled.Count - 1) forward = (sampled[i + 1] - pos).normalized;
                else if (i > 0) forward = (pos - sampled[i - 1]).normalized;

                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;

                segment.waypoints.Add(new Waypoint(pos, forward, segmentId));
            }

            RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
            roadGraph.roadSegments.Add(segment);
            Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from marker components: {segment.waypoints.Count} waypoints");
            return true;
        }

        private float GetMarkerOrder(Component marker)
        {
            if (marker == null) return float.MaxValue;

            Type t = marker.GetType();
            string[] orderFields = { "startDistance", "startSplinePoint", "markerId", "markerID", "id", "index", "order", "markerIndex", "markerIndent", "distance" };

            foreach (string name in orderFields)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    if (field.FieldType == typeof(int))
                        return (int)field.GetValue(marker);
                    if (field.FieldType == typeof(float))
                        return (float)field.GetValue(marker);
                }

                PropertyInfo prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    if (prop.PropertyType == typeof(int))
                        return (int)prop.GetValue(marker, null);
                    if (prop.PropertyType == typeof(float))
                        return (float)prop.GetValue(marker, null);
                }
            }

            Transform tr = marker.transform;
            if (tr != null && tr.parent != null)
            {
                return tr.GetSiblingIndex();
            }

            return float.MaxValue;
        }

        private bool TrySampleFromEasyRoadsComponent(GameObject roadGO, int segmentId)
        {
            Component[] components = roadGO.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;
                if (!IsEasyRoadsType(comp.GetType())) continue;

                if (TrySampleFromERRoadComponent(comp, segmentId))
                    return true;

                if (TrySampleFromERRoadMarkers(comp, segmentId))
                    return true;
            }

            return false;
        }

        private bool TrySampleFromRoadObjectMarkers(object roadObj, int segmentId)
        {
            List<Vector3> markerPositions = ExtractMarkerPositions(roadObj);
            if (markerPositions == null || markerPositions.Count < 2)
                return false;

            List<Vector3> sampled = RoadGraphMeshSampler.ResamplePolyline(markerPositions, sampleStepMeters);
            if (sampled.Count < 2)
                return false;

            RoadSegment segment = new RoadSegment(segmentId, "RoadObject");
            for (int i = 0; i < sampled.Count; i++)
            {
                Vector3 pos = sampled[i];
                Vector3 forward = Vector3.forward;
                if (i < sampled.Count - 1) forward = (sampled[i + 1] - pos).normalized;
                else if (i > 0) forward = (pos - sampled[i - 1]).normalized;

                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;

                segment.waypoints.Add(new Waypoint(pos, forward, segmentId));
            }

            RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
            roadGraph.roadSegments.Add(segment);
            Debug.Log($"[RoadGraphBuilder] Sampled road from road object markers: {segment.waypoints.Count} waypoints");
            return true;
        }

        private bool TrySampleRoadList(object roadsObj)
        {
            if (roadsObj is IList roadsList)
            {
                Debug.Log($"[RoadGraphBuilder] Found {roadsList.Count} roads via EasyRoads3D API");

                for (int i = 0; i < roadsList.Count; i++)
                {
                    object roadObj = roadsList[i];
                    if (roadObj == null) continue;

                    SampleRoadObject(roadObj, i);
                }

                return roadGraph.roadSegments.Count > 0;
            }

            return false;
        }

        private Component FindEasyRoadsNetworkComponent(GameObject network)
        {
            Component[] components = network.GetComponentsInChildren<Component>(true);
            foreach (Component comp in components)
            {
                if (comp == null) continue;
                Type t = comp.GetType();
                if (!IsEasyRoadsType(t)) continue;

                foreach (string fieldName in RoadCollectionFieldNames)
                {
                    FieldInfo field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                        return comp;

                    PropertyInfo prop = t.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                        return comp;
                }
            }

            return null;
        }

        private bool IsEasyRoadsObject(GameObject go)
        {
            Component[] components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;
                if (IsEasyRoadsType(comp.GetType()))
                    return true;
            }
            return false;
        }

        private bool IsEasyRoadsType(Type type)
        {
            if (type == null) return false;
            string asm = type.Assembly.GetName().Name;
            if (!string.IsNullOrEmpty(asm) && asm.IndexOf("EasyRoads", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(type.FullName) && type.FullName.IndexOf("EasyRoads", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private bool TrySampleFromMeshHierarchy(Transform root, int segmentId)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters == null || meshFilters.Length == 0) return false;

            MeshFilter best = null;
            int bestVertexCount = 0;
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                int count = mf.sharedMesh.vertexCount;
                if (count > bestVertexCount)
                {
                    bestVertexCount = count;
                    best = mf;
                }
            }

            if (best == null) return false;

            RoadSegment segment = new RoadSegment(segmentId, root.name);
            RoadGraphMeshSampler.SampleFromMesh(best, segment, segmentId, sampleStepMeters);

            if (segment.waypoints.Count > 0)
            {
                RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
                roadGraph.roadSegments.Add(segment);
                Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from mesh hierarchy: {segment.waypoints.Count} waypoints");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extract road segments from SimplePoly City road meshes in the active scene
        /// </summary>
        private void ExtractSimplePolyRoadMeshes()
        {
            ExtractSimplePolyRoadMeshes(generateDualLaneSegmentsForSimplePoly);
        }

        private void ExtractSimplePolyRoadMeshes(bool dualLaneMode)
        {
            MeshFilter[] sceneMeshFilters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (sceneMeshFilters == null || sceneMeshFilters.Length == 0) return;

            int initialCount = roadGraph.roadSegments.Count;

            foreach (MeshFilter meshFilter in sceneMeshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                GameObject go = meshFilter.gameObject;
                if (!IsSimplePolyRoadObject(go)) continue;

                // EasyRoads roads are already sampled via their own integration path.
                if (IsEasyRoadsObject(go)) continue;

                RoadSegment centerSegment = new RoadSegment(roadGraph.roadSegments.Count, go.name);
                RoadGraphMeshSampler.SampleFromMesh(meshFilter, centerSegment, centerSegment.id, sampleStepMeters);
                if (centerSegment.waypoints.Count < 2) continue;

                RoadGraphMeshSampler.NormalizeWaypointForwards(centerSegment);

                if (dualLaneMode)
                {
                    float laneOffset = EstimateLaneCenterOffset(meshFilter);
                    AddOffsetLaneSegment(centerSegment, -laneOffset, $"{go.name}_Lane_Left");
                    AddOffsetLaneSegment(centerSegment, laneOffset, $"{go.name}_Lane_Right");
                }
                else
                {
                    roadGraph.roadSegments.Add(centerSegment);
                }
            }

            int added = roadGraph.roadSegments.Count - initialCount;
            if (added > 0)
            {
                Debug.Log($"[RoadGraphBuilder] Added {added} SimplePoly road segments");
            }
        }

        private void TryAutoRecoverSimplePolyLanes()
        {
            if (!autoRecoverSimplePolyLaneExtraction || roadGraph == null)
            {
                return;
            }

            bool likelyMisconfigured = !includeSimplePolyRoads || !generateDualLaneSegmentsForSimplePoly;
            bool graphTooSmall = roadGraph.roadSegments.Count <= 2;
            if (!likelyMisconfigured || !graphTooSmall)
            {
                return;
            }

            int before = roadGraph.roadSegments.Count;
            ExtractSimplePolyRoadMeshes(true);
            int added = roadGraph.roadSegments.Count - before;
            if (added > 0)
            {
                Debug.LogWarning($"[RoadGraphBuilder] Auto-recovery enabled: added {added} SimplePoly dual-lane segments. " +
                                 $"Consider enabling includeSimplePolyRoads + generateDualLaneSegmentsForSimplePoly on this component.");
            }
        }

        private float EstimateLaneCenterOffset(MeshFilter meshFilter)
        {
            float fallback = Mathf.Max(0.1f, simplePolyLaneCenterOffset);
            if (meshFilter == null || meshFilter.sharedMesh == null) return fallback;

            Bounds b = meshFilter.sharedMesh.bounds;
            Vector3 scale = meshFilter.transform.lossyScale;

            float sx = Mathf.Abs(b.size.x * scale.x);
            float sz = Mathf.Abs(b.size.z * scale.z);
            float roadWidth = Mathf.Min(sx, sz);

            if (roadWidth < 0.5f) return fallback;

            // 2-lane road: center-to-lane-center ~= quarter of full road width.
            float estimated = roadWidth * 0.25f;
            return Mathf.Clamp(estimated, 0.8f, 2.2f);
        }

        private void AddOffsetLaneSegment(RoadSegment source, float lateralOffset, string name)
        {
            if (source == null || source.waypoints == null || source.waypoints.Count < 2) return;

            RoadSegment laneSegment = new RoadSegment(roadGraph.roadSegments.Count, name);
            for (int i = 0; i < source.waypoints.Count; i++)
            {
                Waypoint wp = source.waypoints[i];
                Vector3 fwd = wp.forward;
                if (fwd.sqrMagnitude < 0.01f)
                {
                    if (i < source.waypoints.Count - 1)
                    {
                        fwd = (source.waypoints[i + 1].position - wp.position).normalized;
                    }
                    else if (i > 0)
                    {
                        fwd = (wp.position - source.waypoints[i - 1].position).normalized;
                    }
                }

                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;

                Vector3 lanePos = wp.position + right * lateralOffset;
                laneSegment.waypoints.Add(new Waypoint(lanePos, fwd, laneSegment.id));
            }

            RoadGraphMeshSampler.NormalizeWaypointForwards(laneSegment);
            roadGraph.roadSegments.Add(laneSegment);
        }

        private bool IsSimplePolyRoadObject(GameObject go)
        {
            if (go == null) return false;

            string lowerName = go.name.ToLowerInvariant();
            if (!lowerName.Contains("road")) return false;

            // Match typical SimplePoly road prefab names
            if (lowerName.Contains("lane") ||
                lowerName.Contains("corner") ||
                lowerName.Contains("t_intersection") ||
                lowerName.Contains("intersection") ||
                lowerName.Contains("tile") ||
                lowerName.Contains("concrete"))
            {
                return true;
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return false;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null) return false;

            string materialName = sharedMaterial.name.ToLowerInvariant();
            return materialName.Contains("road");
        }

        /// <summary>
        /// Fallback: Extract roads from child transform hierarchy
        /// </summary>
        private void ExtractFromChildTransforms(GameObject network)
        {
            int segmentId = 0;

            // EasyRoads3D typically has structure: Road Network → Road Objects → Individual Roads → Markers
            // We need to search recursively for objects with multiple children (marker points)

            List<Transform> potentialRoads = new List<Transform>();
            FindPotentialRoadTransforms(network.transform, potentialRoads);

            Debug.Log($"[RoadGraphBuilder] Found {potentialRoads.Count} potential road paths");

            foreach (Transform roadTransform in potentialRoads)
            {
                SampleRoadFromGameObject(roadTransform.gameObject, segmentId++);
            }

            // If still no roads found, try creating a simple test path
            if (roadGraph.roadSegments.Count == 0)
            {
                Debug.LogWarning("[RoadGraphBuilder] No roads found! Creating a test straight path.");
                CreateTestRoadPath();
            }
        }

        /// <summary>
        /// Recursively find transforms that have multiple children (road markers)
        /// </summary>
        private void FindPotentialRoadTransforms(Transform parent, List<Transform> results)
        {
            string parentName = parent.name.ToLower();

            // Special case 1: "Road Objects" folder - check its children directly
            if (parentName.Contains("road") && parentName.Contains("object"))
            {
                Debug.Log($"[RoadGraphBuilder] Found 'Road Objects' folder: {parent.name} with {parent.childCount} children");
                foreach (Transform child in parent)
                {
                    // Each child is a road (Default Road 001, etc.)
                    if (child.name.ToLower().Contains("road"))
                    {
                        results.Add(child);
                        Debug.Log($"[RoadGraphBuilder] Added road: {child.name}");
                    }
                }
                return; // Don't recurse further
            }

            // Special case 2: EasyRoads3D uses "markers" folder
            if (parentName.Contains("marker") && parent.childCount >= 2)
            {
                // Check if children are simple markers (no mesh renderers, just position holders)
                bool allChildrenAreMarkers = true;
                foreach (Transform child in parent)
                {
                    // EasyRoads3D markers typically have no children or are simple GameObjects
                    string childName = child.name.ToLower();
                    if (!childName.Contains("marker") && child.childCount > 1)
                    {
                        allChildrenAreMarkers = false;
                        break;
                    }
                }

                if (allChildrenAreMarkers)
                {
                    Debug.Log($"[RoadGraphBuilder] Found markers folder: {parent.name} with {parent.childCount} markers");
                    results.Add(parent);
                    return; // Don't recurse further
                }
            }

            // A road path should have multiple markers as children
            if (parent.childCount >= 2)
            {
                // Check if children are simple markers (no further children)
                bool allChildrenAreMarkers = true;
                foreach (Transform child in parent)
                {
                    if (child.childCount > 0)
                    {
                        allChildrenAreMarkers = false;
                        break;
                    }
                }

                if (allChildrenAreMarkers)
                {
                    results.Add(parent);
                    return; // Don't recurse further
                }
            }

            // Recurse into children
            foreach (Transform child in parent)
            {
                FindPotentialRoadTransforms(child, results);
            }
        }

        /// <summary>
        /// Create a test road path for debugging when no roads are found
        /// </summary>
        private void CreateTestRoadPath()
        {
            RoadSegment testSegment = new RoadSegment(0, "Test_Straight_Road");

            // Create a simple straight road with 20 waypoints
            Vector3 startPos = Vector3.zero;
            Vector3 direction = Vector3.forward;
            float roadLength = 100f;
            int waypointCount = Mathf.CeilToInt(roadLength / sampleStepMeters);

            for (int i = 0; i < waypointCount; i++)
            {
                float t = (float)i / (waypointCount - 1);
                Vector3 pos = startPos + direction * (roadLength * t);
                testSegment.waypoints.Add(new Waypoint(pos, direction, 0));
            }

            roadGraph.roadSegments.Add(testSegment);
            Debug.Log($"[RoadGraphBuilder] Created test road: {testSegment.waypoints.Count} waypoints");
        }

        /// <summary>
        /// Sample waypoints from transform hierarchy (child positions)
        /// </summary>
        private void SampleFromTransformHierarchy(Transform roadTransform, int segmentId)
        {
            RoadSegment segment = new RoadSegment(segmentId, roadTransform.name);

            // First, try to find mesh and sample from it
            MeshFilter meshFilter = roadTransform.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Debug.Log($"[RoadGraphBuilder] Sampling road '{roadTransform.name}' from mesh");
                RoadGraphMeshSampler.SampleFromMesh(meshFilter, segment, segmentId, sampleStepMeters);

                if (segment.waypoints.Count > 0)
                {
                    roadGraph.roadSegments.Add(segment);
                    Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from mesh: {segment.waypoints.Count} waypoints");
                    return;
                }
            }

            // Fallback: Get all child transforms as waypoints
            List<Transform> children = new List<Transform>();
            foreach (Transform child in roadTransform)
            {
                children.Add(child);
            }

            if (children.Count < 2)
            {
                // Skip roads without enough markers
                Debug.LogWarning($"[RoadGraphBuilder] Skipping '{roadTransform.name}' - needs at least 2 child markers/points or mesh");
                return;
            }

            // Use child positions
            for (int i = 0; i < children.Count; i++)
            {
                Vector3 pos = children[i].position;
                Vector3 forward = Vector3.forward;

                if (i < children.Count - 1)
                {
                    forward = (children[i + 1].position - pos);
                    if (forward.sqrMagnitude < 0.01f) // Too close
                    {
                        forward = Vector3.forward;
                    }
                    else
                    {
                        forward.Normalize();
                    }
                }
                else if (i > 0)
                {
                    forward = (pos - children[i - 1].position);
                    if (forward.sqrMagnitude < 0.01f)
                    {
                        forward = Vector3.forward;
                    }
                    else
                    {
                        forward.Normalize();
                    }
                }

                // Validate forward vector
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }

                segment.waypoints.Add(new Waypoint(pos, forward, segmentId));
            }

            if (segment.waypoints.Count > 0)
            {
                RoadGraphMeshSampler.NormalizeWaypointForwards(segment);
                roadGraph.roadSegments.Add(segment);
                Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from transforms: {segment.waypoints.Count} waypoints");
            }
        }

        /// <summary>
        /// Build connections between road segments at intersections
        /// </summary>
        private void BuildConnections()
        {
            RoadGraphConnectionBuilder.BuildConnections(roadGraph, connectionThresholdMeters, sampleStepMeters);

            int totalConnections = 0;
            foreach (var segment in roadGraph.roadSegments)
            {
                totalConnections += segment.connections.Count;
            }

            Debug.Log($"[RoadGraphBuilder] Built {totalConnections} connections between road segments");
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            if (TypeCache.TryGetValue(typeName, out Type cached))
                return cached;

            Type type = Type.GetType(typeName);
            if (type != null)
            {
                TypeCache[typeName] = type;
                return type;
            }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found = null;
                try
                {
                    foreach (Type t in asm.GetTypes())
                    {
                        if (t == null) continue;
                        if (t.Name == typeName || t.FullName == typeName)
                        {
                            found = t;
                            break;
                        }
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (Type t in e.Types)
                    {
                        if (t == null) continue;
                        if (t.Name == typeName || t.FullName == typeName)
                        {
                            found = t;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    TypeCache[typeName] = found;
                    return found;
                }
            }

            TypeCache[typeName] = null;
            return null;
        }

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            if (ComponentTypeCache.TryGetValue(typeName, out Type cached))
                return cached;

            Type type = ResolveType(typeName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                ComponentTypeCache[typeName] = type;
                return type;
            }

            ComponentTypeCache[typeName] = null;
            return null;
        }

        private int GetTotalWaypointCount()
        {
            int count = 0;
            foreach (var segment in roadGraph.roadSegments)
            {
                count += segment.waypoints.Count;
            }
            return count;
        }

        private void OnDrawGizmos()
        {
            DrawRoadGraphGizmos(drawSelectedOnlyConnections: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRoadGraphGizmos(drawSelectedOnlyConnections: true);
        }

        /// <summary>
        /// Gizmo visualization for debugging
        /// </summary>
        private void DrawRoadGraphGizmos(bool drawSelectedOnlyConnections)
        {
            if (roadGraph == null || roadGraph.roadSegments.Count == 0) return;

            bool shouldDrawConnections = showConnections &&
                                         (!showConnectionsOnlyWhenSelected || drawSelectedOnlyConnections);

            foreach (var segment in roadGraph.roadSegments)
            {
                if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
                {
                    continue;
                }

                if (showWaypoints)
                {
                    Gizmos.color = Color.cyan;
                    for (int i = 0; i < segment.waypoints.Count; i++)
                    {
                        Waypoint wp = segment.waypoints[i];
                        Gizmos.DrawWireSphere(wp.position, waypointGizmoSize);

                        if (showWaypointForward)
                        {
                            Gizmos.color = Color.blue;
                            Gizmos.DrawLine(wp.position, wp.position + wp.forward * 2f);
                            Gizmos.color = Color.cyan;
                        }

                        // Draw line to next waypoint
                        if (i < segment.waypoints.Count - 1)
                        {
                            Gizmos.DrawLine(wp.position, segment.waypoints[i + 1].position);
                        }
                    }
                }

                if (shouldDrawConnections)
                {
                    if (segment.connections == null)
                    {
                        continue;
                    }

                    Gizmos.color = Color.yellow;
                    foreach (var connection in segment.connections)
                    {
                        if (connection == null || connection.toSegment == null || connection.toSegment.waypoints == null)
                        {
                            continue;
                        }

                        if (connection.fromWaypointIndex < 0 ||
                            connection.fromWaypointIndex >= segment.waypoints.Count ||
                            connection.toWaypointIndex < 0 ||
                            connection.toWaypointIndex >= connection.toSegment.waypoints.Count)
                        {
                            continue;
                        }

                        if (!showMidSegmentConnections && !IsEndpointConnection(segment, connection))
                        {
                            continue;
                        }

                        Vector3 from = segment.waypoints[connection.fromWaypointIndex].position;
                        Vector3 to = connection.toSegment.waypoints[connection.toWaypointIndex].position;
                        Gizmos.DrawLine(from, to);

                        // Draw arrow head
                        Vector3 dir = (to - from).normalized;
                        Vector3 arrowPos = Vector3.Lerp(from, to, 0.7f);
                        Gizmos.DrawLine(arrowPos, arrowPos - dir * 1f + Vector3.up * 0.5f);
                        Gizmos.DrawLine(arrowPos, arrowPos - dir * 1f - Vector3.up * 0.5f);
                    }
                }
            }
        }

        private static bool IsEndpointConnection(RoadSegment fromSegment, RoadConnection connection)
        {
            if (fromSegment == null || connection?.toSegment == null)
            {
                return false;
            }

            int fromLast = fromSegment.waypoints.Count - 1;
            int toLast = connection.toSegment.waypoints.Count - 1;

            bool fromIsEndpoint = connection.fromWaypointIndex == 0 || connection.fromWaypointIndex == fromLast;
            bool toIsEndpoint = connection.toWaypointIndex == 0 || connection.toWaypointIndex == toLast;
            return fromIsEndpoint && toIsEndpoint;
        }
    }
}
