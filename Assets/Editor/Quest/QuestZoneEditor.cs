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
            TrafficSystem.Spline.RoadNetworkManager manager = Object.FindAnyObjectByType<TrafficSystem.Spline.RoadNetworkManager>();
            if (manager == null || manager.roads == null || manager.roads.Count == 0)
            {
                Debug.LogWarning("[QuestZoneEditor] RoadNetworkManager not found or no roads available.");
                return;
            }

            var closestInfo = TrafficSystem.Spline.SplinePathfinder.GetClosestRoadPoint(zone.transform.position);
            if (closestInfo.road == null)
            {
                Debug.LogWarning("[QuestZoneEditor] Could not project onto road network.");
                return;
            }

            Vector3 projectedPoint = closestInfo.road.GetPoint(closestInfo.t);

            Undo.RecordObject(zone.transform, "Snap Quest Zone");
            zone.transform.position = projectedPoint;

            QuestLocation location = zone.Location ?? new QuestLocation(projectedPoint, closestInfo.road.name, 10f);
            location.Position = projectedPoint;
            if (string.IsNullOrWhiteSpace(location.LocationName))
            {
                location.LocationName = closestInfo.road.name;
            }

            Undo.RecordObject(zone, "Update Quest Zone Location");
            zone.SetLocation(location);
            EditorUtility.SetDirty(zone);
        }
    }
}
#endif
