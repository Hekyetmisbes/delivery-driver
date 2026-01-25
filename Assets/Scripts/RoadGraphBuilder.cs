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

        [Header("Debug Visualization")]
        [SerializeField] private bool showWaypoints = true;
        [SerializeField] private bool showConnections = true;
        [SerializeField] private bool showWaypointForward = false;
        [SerializeField] private float waypointGizmoSize = 0.5f;

        // Built road graph
        private RoadGraph roadGraph;
        public RoadGraph RoadGraph => roadGraph;

        private void Start()
        {
            BuildRoadGraph();
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

            // Build connections between road segments
            BuildConnections();

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
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Component comp = root.GetComponentInChildren(System.Type.GetType(componentTypeName));
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
                // Try to get ERRoadNetwork component
                Component roadNetwork = network.GetComponent("ERRoadNetwork");
                if (roadNetwork == null)
                {
                    roadNetwork = network.GetComponentInChildren(System.Type.GetType("ERRoadNetwork"));
                }

                if (roadNetwork == null)
                {
                    Debug.LogWarning("[RoadGraphBuilder] ERRoadNetwork component not found");
                    return false;
                }

                // Try to access roads array/list
                System.Type networkType = roadNetwork.GetType();
                FieldInfo roadsField = networkType.GetField("roads", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (roadsField == null)
                {
                    roadsField = networkType.GetField("roadObjects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (roadsField != null)
                {
                    object roadsObj = roadsField.GetValue(roadNetwork);
                    if (roadsObj is System.Collections.IList roadsList)
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
                        }
                    }
                }
                else
                {
                    GameObject roadGO = goProperty.GetValue(roadObj) as GameObject;
                    if (roadGO != null)
                    {
                        SampleRoadFromGameObject(roadGO, index);
                    }
                }
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
            // Try to get ERRoad component and sample it
            Component erRoad = roadGO.GetComponent("ERRoad");
            if (erRoad != null)
            {
                // Try to get marker positions or spline data
                if (TrySampleFromERRoadComponent(erRoad, segmentId))
                {
                    return;
                }
            }

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

            return false;
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
                SampleFromTransformHierarchy(roadTransform, segmentId++);
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
            // Special case: EasyRoads3D uses "markers" folder
            if (parent.name.ToLower().Contains("marker") && parent.childCount >= 2)
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

            // Get all child transforms as waypoints
            List<Transform> children = new List<Transform>();
            foreach (Transform child in roadTransform)
            {
                children.Add(child);
            }

            if (children.Count < 2)
            {
                // Skip roads without enough markers
                Debug.LogWarning($"[RoadGraphBuilder] Skipping '{roadTransform.name}' - needs at least 2 child markers/points");
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
                roadGraph.roadSegments.Add(segment);
                Debug.Log($"[RoadGraphBuilder] Sampled road '{segment.name}' from transforms: {segment.waypoints.Count} waypoints");
            }
        }

        /// <summary>
        /// Build connections between road segments at intersections
        /// </summary>
        private void BuildConnections()
        {
            float threshold = connectionThresholdMeters;

            foreach (var segment in roadGraph.roadSegments)
            {
                if (segment.waypoints.Count == 0) continue;

                // Check start and end points for connections
                Vector3 startPos = segment.waypoints[0].position;
                Vector3 endPos = segment.waypoints[segment.waypoints.Count - 1].position;

                foreach (var otherSegment in roadGraph.roadSegments)
                {
                    if (otherSegment == segment || otherSegment.waypoints.Count == 0) continue;

                    Vector3 otherStart = otherSegment.waypoints[0].position;
                    Vector3 otherEnd = otherSegment.waypoints[otherSegment.waypoints.Count - 1].position;

                    // End of current -> Start of other
                    if (Vector3.Distance(endPos, otherStart) < threshold)
                    {
                        segment.connections.Add(new RoadConnection(
                            segment, otherSegment,
                            segment.waypoints.Count - 1, 0
                        ));
                    }

                    // End of current -> End of other (reverse direction)
                    if (Vector3.Distance(endPos, otherEnd) < threshold)
                    {
                        segment.connections.Add(new RoadConnection(
                            segment, otherSegment,
                            segment.waypoints.Count - 1, otherSegment.waypoints.Count - 1
                        ));
                    }
                }
            }

            int totalConnections = 0;
            foreach (var segment in roadGraph.roadSegments)
            {
                totalConnections += segment.connections.Count;
            }

            Debug.Log($"[RoadGraphBuilder] Built {totalConnections} connections between road segments");
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

        /// <summary>
        /// Gizmo visualization for debugging
        /// </summary>
        private void OnDrawGizmos()
        {
            if (roadGraph == null || roadGraph.roadSegments.Count == 0) return;

            foreach (var segment in roadGraph.roadSegments)
            {
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

                if (showConnections)
                {
                    Gizmos.color = Color.yellow;
                    foreach (var connection in segment.connections)
                    {
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
    }
}
