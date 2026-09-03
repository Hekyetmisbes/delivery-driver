using UnityEngine;
using UnityEditor;
using DeliveryDriver.EditorTools;

namespace TrafficSystem.Spline.Editor
{
    [CustomEditor(typeof(SplineRoad))]
    public class RoadNetworkEditor : UnityEditor.Editor
    {
        private SplineRoad spline;
        private Transform handleTransform;
        private Quaternion handleRotation;

        private const float handleSize = 0.5f;
        private const float pickSize = 0.6f;
        private int selectedIndex = -1;
        private float targetSpacing = 15f;

        private void OnEnable()
        {
            spline = target as SplineRoad;
            if (spline != null)
            {
                handleTransform = spline.transform;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (spline == null) return;

            int cpCount = spline.ControlPointCount;
            int anchorCount = (cpCount - 1) / 3 + 1;
            float totalLen = spline.GetTotalLength();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Spline Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Length: {totalLen:F1} m");
            EditorGUILayout.LabelField($"Segments: {spline.SegmentCount} ({anchorCount} anchors)");
            EditorGUILayout.LabelField($"Control Points: {cpCount}");

            // Warning if road has too many dense points (like in old roads)
            if (cpCount > 24)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox($"⚠️ Bu yolda çok fazla nokta ({cpCount} nokta) var!\nEski yollar gibi aşırı yoğun noktalar editörü yavaşlatır. Yeni yollar gibi temiz ve düzenlenebilir olması için aşağıdaki butonla basitleştirin.", MessageType.Warning);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                targetSpacing = EditorGUILayout.Slider("Target Spacing (m)", targetSpacing, 6f, 30f);
                
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                if (GUILayout.Button($"Optimize & Simplify This Road ({targetSpacing:F0}m)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(spline, "Simplify Spline Road");
                    bool ok = spline.Simplify(targetSpacing);
                    if (ok)
                    {
                        SimpleRoadPlacementTool.RebuildSingleRoadVisual(spline);
                        EditorUtility.SetDirty(spline);
                        EditorUtility.SetDirty(spline.gameObject);
                        Debug.Log($"[RoadNetworkEditor] '{spline.name}' başarıyla {spline.ControlPointCount} noktaya basitleştirildi.");
                    }
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                targetSpacing = EditorGUILayout.FloatField("Target Spacing (m)", targetSpacing);
                if (GUILayout.Button("Simplify Points"))
                {
                    Undo.RecordObject(spline, "Simplify Spline Road");
                    bool ok = spline.Simplify(targetSpacing);
                    if (ok)
                    {
                        SimpleRoadPlacementTool.RebuildSingleRoadVisual(spline);
                        EditorUtility.SetDirty(spline);
                        EditorUtility.SetDirty(spline.gameObject);
                    }
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Add Segment"))
            {
                Undo.RecordObject(spline, "Add Segment");
                spline.AddSegment();
                EditorUtility.SetDirty(spline);
            }

            if (GUILayout.Button("Reset Spline"))
            {
                if (EditorUtility.DisplayDialog("Reset Spline", "Bu yolun spline noktalarını varsayılana sıfırlamak istediğinize emin misiniz?", "Evet", "İptal"))
                {
                    Undo.RecordObject(spline, "Reset Spline");
                    spline.ResetToDefault();
                    EditorUtility.SetDirty(spline);
                }
            }
            
            if (GUILayout.Button("Calculate Distances"))
            {
                spline.CalculateDistances();
                EditorUtility.SetDirty(spline);
            }

            EditorGUILayout.Space(6f);
            GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
            if (GUILayout.Button("Simplify ALL Dense Roads in Scene"))
            {
                if (EditorUtility.DisplayDialog("Tüm Yolları Basitleştir", "Sahnedeki tüm aşırı noktalı yolları yeni çizilen yollar gibi hafif ve temiz hale getirmek istiyor musunuz?", "Evet, Temizle", "İptal"))
                {
                    int fixedCount = SimpleRoadPlacementTool.SimplifyAllDenseRoadsStatic(targetSpacing);
                    Debug.Log($"[RoadNetworkEditor] {fixedCount} adet yol optimize edildi.");
                    SceneView.RepaintAll();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void OnSceneGUI()
        {
            if (spline == null) return;

            handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

            int cpCount = spline.ControlPointCount;

            // Protection against thousands of points choking the scene view (e.g. image 2)
            if (cpCount > 80)
            {
                Handles.BeginGUI();
                GUILayout.BeginArea(new Rect(15, 15, 290, 85), EditorStyles.helpBox);
                GUILayout.Label($"⚠️ Aşırı Noktalı Yol ({cpCount} nokta)", EditorStyles.boldLabel);
                GUILayout.Label("Editör performansını korumak için handlelar sınırlandı.", EditorStyles.miniLabel);
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                if (GUILayout.Button("Noktaları Basitleştir (Simplify)"))
                {
                    Undo.RecordObject(spline, "Simplify Spline Road");
                    bool ok = spline.Simplify(targetSpacing);
                    if (ok)
                    {
                        SimpleRoadPlacementTool.RebuildSingleRoadVisual(spline);
                        EditorUtility.SetDirty(spline);
                        EditorUtility.SetDirty(spline.gameObject);
                    }
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
                Handles.EndGUI();

                // Only show main anchors (i % 3 == 0) to avoid drawing thousands of handles
                for (int i = 0; i < cpCount; i += 3)
                {
                    ShowPoint(i);
                }
                return;
            }

            for (int i = 0; i < cpCount; i++)
            {
                ShowPoint(i);
            }
        }

        private void ShowPoint(int index)
        {
            Vector3 point = spline.GetControlPoint(index);
            
            float size = HandleUtility.GetHandleSize(point);
            if (index == 0)
            {
                size *= 2f;
            }

            Handles.color = (index % 3 == 0) ? Color.red : Color.yellow;
            if (Handles.Button(point, handleRotation, size * handleSize, size * pickSize, Handles.SphereHandleCap))
            {
                selectedIndex = index;
                Repaint();
            }

            if (selectedIndex == index)
            {
                EditorGUI.BeginChangeCheck();
                point = Handles.DoPositionHandle(point, handleRotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(spline, "Move Point");
                    spline.SetControlPoint(index, point);
                    EditorUtility.SetDirty(spline);
                }
            }
        }
    }
}
