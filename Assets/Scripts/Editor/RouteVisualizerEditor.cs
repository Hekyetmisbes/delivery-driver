using UnityEngine;
using UnityEditor;
using TrafficSystem;

namespace TrafficSystemEditor
{
    /// <summary>
    /// Editor window for visualizing and creating NPC car routes on EasyRoads3D roads
    /// </summary>
    public class RouteVisualizerEditor : EditorWindow
    {
        private RoadGraphBuilder roadGraphBuilder;
        private GameObject routeContainer;
        private bool showWaypoints = true;
        private bool showConnections = true;
        private Color routeColor = Color.green;
        private float lineWidth = 0.2f;
        private float waypointMarkerSize = 0.5f;

        [MenuItem("Tools/Traffic System/Route Visualizer")]
        public static void ShowWindow()
        {
            GetWindow<RouteVisualizerEditor>("Route Visualizer");
        }

        private void OnGUI()
        {
            GUILayout.Label("NPC Car Route Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Find RoadGraphBuilder
            EditorGUILayout.LabelField("Road Graph Builder", EditorStyles.boldLabel);
            roadGraphBuilder = (RoadGraphBuilder)EditorGUILayout.ObjectField(
                "Road Graph Builder",
                roadGraphBuilder,
                typeof(RoadGraphBuilder),
                true
            );

            if (roadGraphBuilder == null)
            {
                EditorGUILayout.HelpBox(
                    "Please assign a RoadGraphBuilder from the scene. This is usually attached to the Road Network GameObject.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Find RoadGraphBuilder in Scene"))
                {
                    roadGraphBuilder = FindObjectOfType<RoadGraphBuilder>();
                    if (roadGraphBuilder != null)
                    {
                        Debug.Log($"[RouteVisualizer] Found RoadGraphBuilder: {roadGraphBuilder.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[RouteVisualizer] No RoadGraphBuilder found in scene!");
                    }
                }

                return;
            }

            EditorGUILayout.Space(10);

            // Visualization settings
            EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
            showWaypoints = EditorGUILayout.Toggle("Show Waypoints", showWaypoints);
            showConnections = EditorGUILayout.Toggle("Show Connections", showConnections);
            routeColor = EditorGUILayout.ColorField("Route Color", routeColor);
            lineWidth = EditorGUILayout.Slider("Line Width", lineWidth, 0.1f, 1f);
            waypointMarkerSize = EditorGUILayout.Slider("Waypoint Marker Size", waypointMarkerSize, 0.1f, 2f);

            EditorGUILayout.Space(10);

            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Create Route Visualization", GUILayout.Height(40)))
            {
                CreateRouteVisualization();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear Route Visualization"))
            {
                ClearRouteVisualization();
            }

            EditorGUILayout.Space(10);

            // Info
            if (roadGraphBuilder.RoadGraph != null)
            {
                EditorGUILayout.LabelField("Road Network Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Road Segments: {roadGraphBuilder.RoadGraph.roadSegments.Count}");

                int totalWaypoints = 0;
                foreach (var segment in roadGraphBuilder.RoadGraph.roadSegments)
                {
                    totalWaypoints += segment.waypoints.Count;
                }
                EditorGUILayout.LabelField($"Total Waypoints: {totalWaypoints}");
            }
        }

        private void CreateRouteVisualization()
        {
            if (roadGraphBuilder == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "RoadGraphBuilder is null. Assign a RoadGraphBuilder from the scene.",
                    "OK"
                );
                return;
            }

            // Rebuild graph to ensure latest road data (works in edit mode)
            roadGraphBuilder.BuildRoadGraph();

            if (roadGraphBuilder.RoadGraph == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "RoadGraph is null after build. Check RoadGraphBuilder configuration.",
                    "OK"
                );
                return;
            }

            // Clear existing visualization
            ClearRouteVisualization();

            // Create container
            routeContainer = new GameObject("NPC_Route_Visualization");
            routeContainer.transform.SetParent(roadGraphBuilder.transform);

            int segmentCount = 0;
            int waypointCount = 0;

            foreach (var segment in roadGraphBuilder.RoadGraph.roadSegments)
            {
                if (segment.waypoints.Count < 2) continue;

                // Create segment container
                GameObject segmentObj = new GameObject($"Route_{segment.name}");
                segmentObj.transform.SetParent(routeContainer.transform);

                // Create LineRenderer for the route path
                GameObject lineObj = new GameObject("RouteLine");
                lineObj.transform.SetParent(segmentObj.transform);
                LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

                // Configure LineRenderer
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = routeColor;
                lineRenderer.endColor = routeColor;
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
                lineRenderer.positionCount = segment.waypoints.Count;
                lineRenderer.useWorldSpace = true;

                // Set waypoint positions
                for (int i = 0; i < segment.waypoints.Count; i++)
                {
                    Waypoint wp = segment.waypoints[i];
                    Vector3 pos = wp.position + Vector3.up * 0.3f; // Slightly above road
                    lineRenderer.SetPosition(i, pos);

                    // Create waypoint markers if enabled
                    if (showWaypoints)
                    {
                        GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        markerObj.name = $"Waypoint_{i}";
                        markerObj.transform.SetParent(segmentObj.transform);
                        markerObj.transform.position = pos;
                        markerObj.transform.localScale = Vector3.one * waypointMarkerSize;

                        // Set color
                        Renderer renderer = markerObj.GetComponent<Renderer>();
                        Material mat = new Material(Shader.Find("Standard"));
                        mat.color = routeColor;
                        renderer.material = mat;

                        // Remove collider
                        DestroyImmediate(markerObj.GetComponent<Collider>());

                        // Add forward direction indicator
                        GameObject arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        arrowObj.name = "Direction";
                        arrowObj.transform.SetParent(markerObj.transform);
                        arrowObj.transform.localPosition = Vector3.forward * 0.5f;
                        arrowObj.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
                        arrowObj.transform.localRotation = Quaternion.LookRotation(wp.forward);

                        Renderer arrowRenderer = arrowObj.GetComponent<Renderer>();
                        Material arrowMat = new Material(Shader.Find("Standard"));
                        arrowMat.color = Color.blue;
                        arrowRenderer.material = arrowMat;

                        DestroyImmediate(arrowObj.GetComponent<Collider>());
                    }

                    waypointCount++;
                }

                // Create connection visualizations if enabled
                if (showConnections && segment.connections != null && segment.connections.Count > 0)
                {
                    foreach (var connection in segment.connections)
                    {
                        GameObject connectionObj = new GameObject("Connection");
                        connectionObj.transform.SetParent(segmentObj.transform);
                        LineRenderer connLineRenderer = connectionObj.AddComponent<LineRenderer>();

                        connLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                        connLineRenderer.startColor = Color.yellow;
                        connLineRenderer.endColor = Color.yellow;
                        connLineRenderer.startWidth = lineWidth * 0.5f;
                        connLineRenderer.endWidth = lineWidth * 0.5f;
                        connLineRenderer.positionCount = 2;
                        connLineRenderer.useWorldSpace = true;

                        Vector3 fromPos = segment.waypoints[connection.fromWaypointIndex].position + Vector3.up * 0.5f;
                        Vector3 toPos = connection.toSegment.waypoints[connection.toWaypointIndex].position + Vector3.up * 0.5f;

                        connLineRenderer.SetPosition(0, fromPos);
                        connLineRenderer.SetPosition(1, toPos);
                    }
                }

                segmentCount++;
            }

            Debug.Log($"[RouteVisualizer] Created route visualization: {segmentCount} segments, {waypointCount} waypoints");

            EditorUtility.DisplayDialog(
                "Success",
                $"Route visualization created!\n\nSegments: {segmentCount}\nWaypoints: {waypointCount}",
                "OK"
            );

            // Select the container in hierarchy
            Selection.activeGameObject = routeContainer;
        }

        private void ClearRouteVisualization()
        {
            // Find existing visualization
            GameObject existingViz = GameObject.Find("NPC_Route_Visualization");
            if (existingViz != null)
            {
                DestroyImmediate(existingViz);
                Debug.Log("[RouteVisualizer] Cleared existing route visualization");
            }

            routeContainer = null;
        }

        private void OnDestroy()
        {
            // Optional: Clear on window close
            // ClearRouteVisualization();
        }
    }
}
