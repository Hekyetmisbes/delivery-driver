using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class CastleBuilderTool : EditorWindow
    {
        private const string DefaultRootName = "Placed Castle Walls";

        public enum PrefabForwardAxis
        {
            ZForward,
            XForward
        }

        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject towerPrefab;
        [SerializeField] private Transform castleParent;
        [SerializeField] private float tileLength = 5f;
        [SerializeField] private PrefabForwardAxis forwardAxis = PrefabForwardAxis.ZForward;
        [SerializeField] private float yOffset = 0f;
        [SerializeField] private bool toolEnabled = true;

        private bool hasFirstPoint;
        private Vector3 firstPoint;
        private Vector3 hoverPoint;
        private bool hasHoverPoint;
        private bool handledSceneClick;

        [MenuItem("Tools/Delivery Driver/City/Castle Builder")]
        public static void Open()
        {
            CastleBuilderTool window = GetWindow<CastleBuilderTool>("Castle Builder");
            window.minSize = new Vector2(300f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Castle Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Shift + Left Click: Kale duvarı yerleştir.\nİlk tıklama başlangıç noktasıdır, sonrakiler araya duvar örer.", MessageType.Info);

            toolEnabled = EditorGUILayout.Toggle("Tool Enabled", toolEnabled);
            wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab", wallPrefab, typeof(GameObject), false);
            towerPrefab = (GameObject)EditorGUILayout.ObjectField("Tower Prefab (Optional)", towerPrefab, typeof(GameObject), false);
            castleParent = (Transform)EditorGUILayout.ObjectField("Castle Parent", castleParent, typeof(Transform), true);
            tileLength = Mathf.Max(0.1f, EditorGUILayout.FloatField("Wall Tile Length", tileLength));
            forwardAxis = (PrefabForwardAxis)EditorGUILayout.EnumPopup("Prefab Forward Axis", forwardAxis);
            yOffset = EditorGUILayout.FloatField("Y Offset", yOffset);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection As Parent"))
                {
                    castleParent = Selection.activeTransform;
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Clear First Point (Break Chain)"))
            {
                hasFirstPoint = false;
                SceneView.RepaintAll();
            }

            if (hasFirstPoint)
            {
                EditorGUILayout.LabelField("First Point", firstPoint.ToString("F2"));
            }
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (!toolEnabled)
            {
                return;
            }

            Event current = Event.current;
            hasHoverPoint = TryGetScenePoint(current.mousePosition, out hoverPoint);
            handledSceneClick = false;

            if ((current.shift || current.control) && !current.alt)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            DrawScenePreview();
            
            if (handledSceneClick || current.type == EventType.Used)
            {
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0 || !current.shift || current.alt)
            {
                return;
            }

            if (!hasHoverPoint)
            {
                return;
            }

            current.Use();
            HandlePlacementClick(hoverPoint);
        }

        private void HandlePlacementClick(Vector3 point)
        {
            point.y += yOffset;

            if (!hasFirstPoint)
            {
                firstPoint = point;
                hasFirstPoint = true;

                // Place initial tower if assigned
                if (towerPrefab != null)
                {
                    PlaceTower(firstPoint);
                }

                Repaint();
                SceneView.RepaintAll();
                return;
            }

            CreatePrefabWallSegment(firstPoint, point);
            
            // Place tower at the end of the segment
            if (towerPrefab != null)
            {
                PlaceTower(point);
            }

            firstPoint = point;
            hasFirstPoint = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private void PlaceTower(Vector3 position)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform root = castleParent != null ? castleParent : EnsureRoot(scene);

            Object instance = PrefabUtility.InstantiatePrefab(towerPrefab, scene);
            GameObject towerInstance = instance as GameObject;
            if (towerInstance != null)
            {
                Undo.RegisterCreatedObjectUndo(towerInstance, "Place Castle Tower");
                towerInstance.transform.SetParent(root, true);
                towerInstance.transform.position = position;
                towerInstance.isStatic = true;
                EditorUtility.SetDirty(towerInstance);
            }
        }

        private void CreatePrefabWallSegment(Vector3 start, Vector3 end)
        {
            if (wallPrefab == null)
            {
                EditorUtility.DisplayDialog("Castle Builder", "Wall Prefab seçilmeden duvar oluşturulamıyor.", "OK");
                return;
            }

            Vector3 flatDelta = end - start;
            flatDelta.y = 0f;
            float distance = flatDelta.magnitude;
            if (distance < 0.1f)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform root = castleParent != null ? castleParent : EnsureRoot(scene);
            GameObject segmentObject = new GameObject($"WallSegment_{System.DateTime.Now:HHmmssfff}");
            Undo.RegisterCreatedObjectUndo(segmentObject, "Create Wall Segment");
            SceneManager.MoveGameObjectToScene(segmentObject, scene);
            segmentObject.transform.SetParent(root, true);

            int tileCount = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.1f, tileLength)));
            Vector3 direction = flatDelta.normalized;
            Quaternion rotation = BuildRotation(direction);

            for (int i = 0; i < tileCount; i++)
            {
                float t = (i + 0.5f) / tileCount;
                Vector3 position = Vector3.Lerp(start, end, t);
                Object instance = PrefabUtility.InstantiatePrefab(wallPrefab, scene);
                GameObject wallTile = instance as GameObject;
                if (wallTile == null)
                {
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(wallTile, "Place Wall Tile");
                wallTile.transform.SetParent(segmentObject.transform, true);
                wallTile.transform.SetPositionAndRotation(position, rotation);
                wallTile.isStatic = true;
            }

            EditorUtility.SetDirty(segmentObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private Transform EnsureRoot(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == DefaultRootName)
                {
                    return rootObject.transform;
                }
            }

            GameObject newRoot = new GameObject(DefaultRootName);
            Undo.RegisterCreatedObjectUndo(newRoot, "Create Castle Root");
            SceneManager.MoveGameObjectToScene(newRoot, scene);
            return newRoot.transform;
        }

        private Quaternion BuildRotation(Vector3 direction)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (forwardAxis == PrefabForwardAxis.XForward)
            {
                rotation *= Quaternion.Euler(0f, -90f, 0f);
            }

            return rotation;
        }

        private bool TryGetScenePoint(Vector2 mousePosition, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                point = hit.point;
                return true;
            }

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void DrawScenePreview()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 360f, 64f), EditorStyles.helpBox);
            GUILayout.Label("Castle Builder", EditorStyles.boldLabel);
            GUILayout.Label(hasFirstPoint ? "Shift + Left Click: duvarı bitir ve yeni noktadan devam et." : "Shift + Left Click: ilk kuleyi (başlangıç) seç.");
            GUILayout.EndArea();
            Handles.EndGUI();

            if (hasFirstPoint)
            {
                Handles.color = Color.green;
                Handles.SphereHandleCap(0, firstPoint, Quaternion.identity, HandleUtility.GetHandleSize(firstPoint) * 0.18f, EventType.Repaint);
            }

            if (hasFirstPoint && hasHoverPoint)
            {
                Vector3 previewPoint = hoverPoint + Vector3.up * yOffset;
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(5f, firstPoint, previewPoint);
                Handles.SphereHandleCap(0, previewPoint, Quaternion.identity, HandleUtility.GetHandleSize(previewPoint) * 0.14f, EventType.Repaint);
            }
        }
    }
}