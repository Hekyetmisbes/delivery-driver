#if UNITY_EDITOR
using DeliveryDriver.Quest;
using TrafficSystem;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.Quest.Editor
{
    [CustomEditor(typeof(QuestZone))]
    public class QuestZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            QuestZone zone = (QuestZone)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quest Zone Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Snap To Nearest Road Waypoint"))
            {
                SnapToNearestWaypoint(zone);
            }
        }

        private static void SnapToNearestWaypoint(QuestZone zone)
        {
            RoadGraphBuilder builder = Object.FindAnyObjectByType<RoadGraphBuilder>();
            if (builder == null || builder.RoadGraph == null)
            {
                Debug.LogWarning("[QuestZoneEditor] RoadGraphBuilder not found or RoadGraph not built.");
                return;
            }

            var (segment, waypointIndex, projectedPoint, _) = builder.RoadGraph.ProjectPointOnRoad(zone.transform.position);
            if (segment == null)
            {
                Debug.LogWarning("[QuestZoneEditor] Could not project onto road network.");
                return;
            }

            Undo.RecordObject(zone.transform, "Snap Quest Zone");
            zone.transform.position = projectedPoint;

            QuestLocation location = zone.Location ?? new QuestLocation(projectedPoint, segment.name, 10f);
            location.Position = projectedPoint;
            location.RoadSegmentIndex = segment.id;
            location.WaypointIndex = waypointIndex;
            if (string.IsNullOrWhiteSpace(location.LocationName))
            {
                location.LocationName = segment.name;
            }

            Undo.RecordObject(zone, "Update Quest Zone Location");
            zone.SetLocation(location);
            EditorUtility.SetDirty(zone);
        }
    }
}
#endif
