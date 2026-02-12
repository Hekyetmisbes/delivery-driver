using UnityEngine;
using UnityEditor;
using TrafficSystem;

namespace TrafficSystemEditor
{
    /// <summary>
    /// Draws route gizmos in Scene view for better visualization
    /// </summary>
    [InitializeOnLoad]
    public static class RouteGizmoDrawer
    {
        private static bool enableGizmos = true;
        private static Color routeColor = new Color(0f, 1f, 0f, 0.8f);
        private static Color waypointColor = new Color(0f, 0.8f, 1f, 0.9f);
        private static Color connectionColor = new Color(1f, 1f, 0f, 0.6f);

        static RouteGizmoDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!enableGizmos) return;

            // Find RoadGraphBuilder in scene
            RoadGraphBuilder[] builders = Object.FindObjectsByType<RoadGraphBuilder>(FindObjectsSortMode.None);
            if (builders.Length == 0) return;

            foreach (var builder in builders)
            {
                if (builder.RoadGraph == null) continue;

                DrawRoadGraph(builder.RoadGraph);
            }
        }

        private static void DrawRoadGraph(RoadGraph graph)
        {
            if (graph.roadSegments == null) return;

            foreach (var segment in graph.roadSegments)
            {
                if (segment.waypoints.Count < 2) continue;

                // Draw route line
                Handles.color = routeColor;
                for (int i = 0; i < segment.waypoints.Count - 1; i++)
                {
                    Vector3 from = segment.waypoints[i].position + Vector3.up * 0.3f;
                    Vector3 to = segment.waypoints[i + 1].position + Vector3.up * 0.3f;
                    Handles.DrawLine(from, to, 3f);
                }

                // Draw waypoints
                Handles.color = waypointColor;
                for (int i = 0; i < segment.waypoints.Count; i++)
                {
                    Waypoint wp = segment.waypoints[i];
                    Vector3 pos = wp.position + Vector3.up * 0.3f;

                    // Draw sphere
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.5f, EventType.Repaint);

                    // Draw forward direction
                    Handles.color = Color.blue;
                    Handles.DrawLine(pos, pos + wp.forward * 2f, 2f);
                    Handles.color = waypointColor;

                    // Draw label every 5th waypoint
                    if (i % 5 == 0)
                    {
                        Handles.Label(pos + Vector3.up * 1f, $"{segment.name}\nWP {i}");
                    }
                }

                // Draw connections
                if (segment.connections != null)
                {
                    Handles.color = connectionColor;
                    foreach (var connection in segment.connections)
                    {
                        Vector3 from = segment.waypoints[connection.fromWaypointIndex].position + Vector3.up * 0.5f;
                        Vector3 to = connection.toSegment.waypoints[connection.toWaypointIndex].position + Vector3.up * 0.5f;

                        Handles.DrawDottedLine(from, to, 4f);

                        // Draw arrow head
                        Vector3 direction = (to - from).normalized;
                        Vector3 arrowPos = Vector3.Lerp(from, to, 0.7f);
                        DrawArrow(arrowPos, direction, 1f);
                    }
                }
            }
        }

        private static void DrawArrow(Vector3 position, Vector3 direction, float size)
        {
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 left = -right;

            Handles.DrawLine(position, position + left * size - direction * size, 2f);
            Handles.DrawLine(position, position + right * size - direction * size, 2f);
        }
    }

    /// <summary>
    /// Adds menu item to toggle route gizmos
    /// </summary>
    public static class RouteGizmoMenu
    {
        private const string MenuPath = "Tools/Traffic System/Toggle Route Gizmos";
        private static bool gizmosEnabled = true;

        [MenuItem(MenuPath)]
        private static void ToggleGizmos()
        {
            gizmosEnabled = !gizmosEnabled;
            SceneView.RepaintAll();
            Debug.Log($"[RouteGizmo] Gizmos {(gizmosEnabled ? "enabled" : "disabled")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleGizmosValidate()
        {
            Menu.SetChecked(MenuPath, gizmosEnabled);
            return true;
        }
    }
}
