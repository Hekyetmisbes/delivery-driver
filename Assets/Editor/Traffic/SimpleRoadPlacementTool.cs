using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class SimpleRoadPlacementTool : EditorWindow
    {
        private const string DefaultRoadPrefabPath = "Assets/ThirdParty/LowPolyCity/Prefab/Roads/Road Lane_01.prefab";
        private const string FallbackRoadPrefabPath = "Assets/ThirdParty/LowPolyCity/Prefab/Roads/Road Tile.prefab";
        private const string DefaultRoadMaterialPath = "Assets/ThirdParty/LowPolyCity/Materials/Road.mat";
        private const string DefaultSidewalkMaterialPath = "Assets/ThirdParty/LowPolyCity/Materials/Road Concrete Tile.mat";
        private const string GeneratedMaterialFolderPath = "Assets/Art/Materials/RoadTools";
        private const string GeneratedAsphaltMaterialPath = GeneratedMaterialFolderPath + "/Generated_Asphalt.mat";
        private const string GeneratedLaneMaterialPath = GeneratedMaterialFolderPath + "/Generated_LaneMarking.mat";
        private const string DefaultRootName = "Placed Roads";
        private const string GeneratedNetworkName = "RoadNetworkManager";
        private const string GeneratedIntersectionsName = "Generated Road Intersections";
        private const string GeneratedIntersectionVisualsName = "Generated Intersection Visuals";
        private const float MinDashDistance = 0.2f;

        private enum PlacementMode
        {
            SmoothMesh,
            PrefabTiles
        }

        private enum PrefabForwardAxis
        {
            ZForward,
            XForward
        }

        private enum IntermediateContinuationMode
        {
            BranchNewRoad,
            TrimAndContinue
        }

        [SerializeField] private PlacementMode placementMode = PlacementMode.SmoothMesh;
        [SerializeField] private IntermediateContinuationMode intermediateContinuationMode = IntermediateContinuationMode.BranchNewRoad;
        [SerializeField] private GameObject roadPrefab;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material sidewalkMaterial;
        [SerializeField] private Material laneMarkingMaterial;
        [SerializeField] private Transform roadParent;
        [SerializeField] private float tileLength = 10f;
        [SerializeField] private float roadWidth = 8.5f;
        [SerializeField] private float sidewalkWidth = 2.2f;
        [SerializeField] private float sidewalkHeightOffset = 0.15f;
        [SerializeField] private float sidewalkBevelWidth = 0.25f;
        [SerializeField] private bool createSidewalkCollider;
        [SerializeField] private bool createLaneMarkings = true;
        [SerializeField] private bool createSideEdgeLines;
        [SerializeField] private float laneMarkingWidth = 0.22f;
        [SerializeField] private float laneDashLength = 3.2f;
        [SerializeField] private float laneDashGap = 4.4f;
        [SerializeField] private float laneMarkingYOffset = 0.025f;
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private float groundOffset = 0.12f;
        [SerializeField] private float groundProbeHeight = 80f;
        [SerializeField] private float groundProbeDepth = 180f;
        [SerializeField] private bool autoCreateBridges = true;
        [SerializeField] private float bridgeMinGroundClearance = 2f;
        [SerializeField] private float bridgeMinLength = 8f;
        [SerializeField] private float bridgeArchHeight = 3.5f;
        [SerializeField] private float bridgeDeckThickness = 0.45f;
        [SerializeField] private float bridgeSideBeamHeight = 0.9f;
        [SerializeField] private float bridgeSideBeamWidth = 0.45f;
        [SerializeField] private float bridgeRailHeight = 1.1f;
        [SerializeField] private float bridgeRailWidth = 0.28f;
        [SerializeField] private float bridgePillarSpacing = 28f;
        [SerializeField] private float bridgePillarWidth = 1.2f;
        [SerializeField] private float bridgeSupportBaseY = 0f;
        [SerializeField] private bool createBridgeStructureCollider = true;
        [SerializeField] private bool createBridgeTowers = true;
        [SerializeField] private float bridgeTowerHeight = 22f;
        [SerializeField] private float bridgeTowerWidth = 1.8f;
        [SerializeField] private float bridgeTowerSpacing = 90f;
        [SerializeField] private float bridgeTowerSideOffset = 5.8f;
        [SerializeField] private float bridgeTopBeamHeight = 1.1f;
        [SerializeField] private int samplesPerSegment = 10;
        [SerializeField] private int roadCrossSegments = 6;
        [SerializeField] private int sidewalkCrossSegments = 2;
        [SerializeField] private float roadYOffset = 0.03f;
        [SerializeField] private PrefabForwardAxis forwardAxis = PrefabForwardAxis.ZForward;
        [SerializeField] private bool createSplineRoad = true;
        [SerializeField] private bool createRoadNetworkLinks = true;
        [SerializeField] private float endpointMergeDistance = 5f;
        [SerializeField] private float splineLaneWidth = 1.4f;
        [SerializeField] private bool connectToExistingRoads = true;
        [SerializeField] private float connectionSnapDistance = 8.5f;
        [SerializeField] private float connectionSampleStep = 4f;
        [SerializeField] private float branchPreviewLength = 22f;
        [SerializeField] private float branchSideAngle = 55f;
        [SerializeField] private float branchHandleSize = 0.55f;
        [SerializeField] private bool enableFreeAngleBranch = true;
        [SerializeField] private float branchToggleDistance = 1.2f;
        [SerializeField] private float intersectionSize = 18f;
        [SerializeField] private float intersectionRoadTrimDistance = 8.5f;
        [SerializeField] private float intersectionSidewalkWidth = 3f;
        [SerializeField] private float crosswalkStripeLength = 4.2f;
        [SerializeField] private float crosswalkStripeWidth = 0.55f;
        [SerializeField] private float crosswalkStripeGap = 0.55f;
        [SerializeField] private int crosswalkStripeCount = 7;
        [SerializeField] private bool createMeshCollider;
        [SerializeField] private bool toolEnabled = true;

        [SerializeField] private List<Vector3> smoothPath = new List<Vector3>();

        private bool hasFirstPoint;
        private Vector3 firstPoint;
        private Vector3 hoverPoint;
        private bool hasHoverPoint;
        private bool isHoverSnappedToRoad;
        private GameObject activeSmoothRoad;
        private bool handledSceneClick;
        private int selectedJunctionPointIndex = -1;
        private readonly List<BranchRecord> branchRecords = new List<BranchRecord>();

        private sealed class BranchRecord
        {
            public Vector3 Junction;
            public int Slot;
            public GameObject BranchRoad;
            public GameObject FirstSplitRoad;
            public GameObject SecondSplitRoad;
            public List<Vector3> OriginalPath;
            public string IntersectionVisualName;
        }

        [MenuItem("Tools/Delivery Driver/City/Simple Road Placement")]
        public static void Open()
        {
            SimpleRoadPlacementTool window = GetWindow<SimpleRoadPlacementTool>("Road Placement");
            window.minSize = new Vector2(380f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
            Selection.selectionChanged -= OnSelectionChange;
            Selection.selectionChanged += OnSelectionChange;
            LoadDefaultAssets();
            CleanOrphanRoadIntersections();
            CleanOrphanIntersectionVisuals();
            SimplifyAllDenseRoads();
            RebuildAllIntersections();
            TryLoadSelectedRoad();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Selection.selectionChanged -= OnSelectionChange;
        }

        private void OnSelectionChange()
        {
            TryLoadSelectedRoad();
        }

        private void TryLoadSelectedRoad()
        {
            if (Selection.activeGameObject == null) return;
            SplineRoad road = Selection.activeGameObject.GetComponent<SplineRoad>();
            if (road == null) road = Selection.activeGameObject.GetComponentInParent<SplineRoad>();
            if (road != null && road.IsUsable && (activeSmoothRoad == null || road.gameObject != activeSmoothRoad))
            {
                LoadRoadIntoTool(road, selectPointIndex: -1);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Simple Road Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Smooth Mesh modu: Shift + Left Click ile noktalar ekle, yol tek parca mesh olarak yumusak baglanir. Prefab Tiles modu eski duz prefab doseme davranisidir.", MessageType.Info);

            toolEnabled = EditorGUILayout.Toggle("Tool Enabled", toolEnabled);
            placementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", placementMode);
            roadParent = (Transform)EditorGUILayout.ObjectField("Road Parent", roadParent, typeof(Transform), true);
            roadYOffset = EditorGUILayout.FloatField("Road Y Offset", roadYOffset);

            EditorGUILayout.Space(6f);
            if (placementMode == PlacementMode.SmoothMesh)
            {
                DrawSmoothMeshSettings();
            }
            else
            {
                DrawPrefabTileSettings();
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection As Parent"))
                {
                    roadParent = Selection.activeTransform;
                }

                if (GUILayout.Button("Load Defaults"))
                {
                    LoadDefaultAssets();
                    createSideEdgeLines = false;
                }
            }
        }

        private void DrawSmoothMeshSettings()
        {
            roadMaterial = (Material)EditorGUILayout.ObjectField("Road Material", roadMaterial, typeof(Material), false);
            sidewalkMaterial = (Material)EditorGUILayout.ObjectField("Sidewalk Material", sidewalkMaterial, typeof(Material), false);
            laneMarkingMaterial = (Material)EditorGUILayout.ObjectField("Lane Marking Material", laneMarkingMaterial, typeof(Material), false);
            roadWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Road Width", roadWidth));
            sidewalkWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Sidewalk Width", sidewalkWidth));
            sidewalkHeightOffset = Mathf.Max(0f, EditorGUILayout.FloatField("Sidewalk Height Offset", sidewalkHeightOffset));
            sidewalkBevelWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Sidewalk Bevel Width", sidewalkBevelWidth));
            samplesPerSegment = Mathf.Clamp(EditorGUILayout.IntField("Curve Smoothness", samplesPerSegment), 2, 32);
            roadCrossSegments = Mathf.Clamp(EditorGUILayout.IntField("Road Cross Segments", roadCrossSegments), 1, 16);
            sidewalkCrossSegments = Mathf.Clamp(EditorGUILayout.IntField("Sidewalk Cross Segments", sidewalkCrossSegments), 1, 8);
            createMeshCollider = EditorGUILayout.Toggle("Create Mesh Collider", createMeshCollider);
            createSidewalkCollider = EditorGUILayout.Toggle("Create Sidewalk Collider", createSidewalkCollider);

            EditorGUILayout.Space(6f);
            snapToGround = EditorGUILayout.Toggle("Snap To Ground", snapToGround);
            using (new EditorGUI.DisabledScope(!snapToGround))
            {
                groundOffset = Mathf.Max(0f, EditorGUILayout.FloatField("Ground Offset", groundOffset));
                groundProbeHeight = Mathf.Max(1f, EditorGUILayout.FloatField("Probe Height", groundProbeHeight));
                groundProbeDepth = Mathf.Max(1f, EditorGUILayout.FloatField("Probe Depth", groundProbeDepth));
            }

            EditorGUILayout.Space(6f);
            autoCreateBridges = EditorGUILayout.Toggle("Auto Create Bridges", autoCreateBridges);
            using (new EditorGUI.DisabledScope(!autoCreateBridges))
            {
                bridgeMinGroundClearance = Mathf.Max(0f, EditorGUILayout.FloatField("Bridge Min Ground Clearance", bridgeMinGroundClearance));
                bridgeMinLength = Mathf.Max(1f, EditorGUILayout.FloatField("Bridge Min Length", bridgeMinLength));
                bridgeArchHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Bridge Arch Height (Over Water)", bridgeArchHeight));
                bridgeDeckThickness = Mathf.Max(0.05f, EditorGUILayout.FloatField("Bridge Deck Thickness", bridgeDeckThickness));
                bridgeSideBeamHeight = Mathf.Max(0.05f, EditorGUILayout.FloatField("Bridge Side Beam Height", bridgeSideBeamHeight));
                bridgeSideBeamWidth = Mathf.Max(0.05f, EditorGUILayout.FloatField("Bridge Side Beam Width", bridgeSideBeamWidth));
                bridgeRailHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Bridge Rail Height", bridgeRailHeight));
                bridgeRailWidth = Mathf.Max(0.05f, EditorGUILayout.FloatField("Bridge Rail Width", bridgeRailWidth));
                bridgePillarSpacing = Mathf.Max(4f, EditorGUILayout.FloatField("Bridge Pillar Spacing", bridgePillarSpacing));
                bridgePillarWidth = Mathf.Max(0.2f, EditorGUILayout.FloatField("Bridge Pillar Width", bridgePillarWidth));
                bridgeSupportBaseY = EditorGUILayout.FloatField("Bridge Support Base Y", bridgeSupportBaseY);
                createBridgeStructureCollider = EditorGUILayout.Toggle("Create Bridge Structure Collider", createBridgeStructureCollider);
                createBridgeTowers = EditorGUILayout.Toggle("Create Bridge Towers", createBridgeTowers);
                using (new EditorGUI.DisabledScope(!createBridgeTowers))
                {
                    bridgeTowerHeight = Mathf.Max(2f, EditorGUILayout.FloatField("Bridge Tower Height", bridgeTowerHeight));
                    bridgeTowerWidth = Mathf.Max(0.2f, EditorGUILayout.FloatField("Bridge Tower Width", bridgeTowerWidth));
                    bridgeTowerSpacing = Mathf.Max(12f, EditorGUILayout.FloatField("Bridge Tower Spacing", bridgeTowerSpacing));
                    bridgeTowerSideOffset = Mathf.Max(0.5f, EditorGUILayout.FloatField("Bridge Tower Side Offset", bridgeTowerSideOffset));
                    bridgeTopBeamHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("Bridge Top Beam Height", bridgeTopBeamHeight));
                }
            }

            EditorGUILayout.Space(6f);
            createLaneMarkings = EditorGUILayout.Toggle("Create Lane Markings", createLaneMarkings);
            using (new EditorGUI.DisabledScope(!createLaneMarkings))
            {
                createSideEdgeLines = EditorGUILayout.Toggle("Create Side Edge Lines", createSideEdgeLines);
                laneMarkingWidth = Mathf.Max(0.03f, EditorGUILayout.FloatField("Line Width", laneMarkingWidth));
                laneDashLength = Mathf.Max(MinDashDistance, EditorGUILayout.FloatField("Dash Length", laneDashLength));
                laneDashGap = Mathf.Max(MinDashDistance, EditorGUILayout.FloatField("Dash Gap", laneDashGap));
                laneMarkingYOffset = Mathf.Max(0.001f, EditorGUILayout.FloatField("Line Y Offset", laneMarkingYOffset));
            }

            EditorGUILayout.Space(6f);
            createSplineRoad = EditorGUILayout.Toggle("Create SplineRoad", createSplineRoad);
            using (new EditorGUI.DisabledScope(!createSplineRoad))
            {
                createRoadNetworkLinks = EditorGUILayout.Toggle("Create NPC Network Links", createRoadNetworkLinks);
                using (new EditorGUI.DisabledScope(!createRoadNetworkLinks))
                {
                    endpointMergeDistance = Mathf.Max(0.2f, EditorGUILayout.FloatField("Endpoint Merge Distance", endpointMergeDistance));
                }

                splineLaneWidth = EditorGUILayout.FloatField("Spline Lane Width", splineLaneWidth);
            }

            EditorGUILayout.Space(6f);
            connectToExistingRoads = EditorGUILayout.Toggle("Connect To Existing Roads", connectToExistingRoads);
            using (new EditorGUI.DisabledScope(!connectToExistingRoads))
            {
                connectionSnapDistance = Mathf.Max(0.2f, EditorGUILayout.FloatField("Connection Snap Distance", connectionSnapDistance));
                connectionSampleStep = Mathf.Max(0.5f, EditorGUILayout.FloatField("Connection Sample Step", connectionSampleStep));
            }

            EditorGUILayout.Space(6f);
            branchPreviewLength = Mathf.Max(2f, EditorGUILayout.FloatField("Branch Preview Length", branchPreviewLength));
            branchSideAngle = Mathf.Clamp(EditorGUILayout.FloatField("Branch Side Angle", branchSideAngle), 15f, 120f);
            branchHandleSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Branch Handle Size", branchHandleSize));
            enableFreeAngleBranch = EditorGUILayout.Toggle("Free Angle Branch", enableFreeAngleBranch);
            branchToggleDistance = Mathf.Max(0.2f, EditorGUILayout.FloatField("Branch Toggle Distance", branchToggleDistance));
            intersectionSize = Mathf.Max(roadWidth + 2f, EditorGUILayout.FloatField("Intersection Size", intersectionSize));
            intersectionRoadTrimDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Intersection Road Trim", intersectionRoadTrimDistance));
            intersectionSidewalkWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Intersection Sidewalk", intersectionSidewalkWidth));
            crosswalkStripeLength = Mathf.Max(0.2f, EditorGUILayout.FloatField("Crosswalk Stripe Length", crosswalkStripeLength));
            crosswalkStripeWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("Crosswalk Stripe Width", crosswalkStripeWidth));
            crosswalkStripeGap = Mathf.Max(0.05f, EditorGUILayout.FloatField("Crosswalk Stripe Gap", crosswalkStripeGap));
            crosswalkStripeCount = Mathf.Clamp(EditorGUILayout.IntField("Crosswalk Stripe Count", crosswalkStripeCount), 2, 16);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Smooth Road"))
                {
                    StartNewSmoothRoad();
                }

                if (GUILayout.Button("Undo Last Point"))
                {
                    UndoLastSmoothPoint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(smoothPath.Count < 2 || activeSmoothRoad == null))
                {
                    if (GUILayout.Button("Rebuild Current Road"))
                    {
                        RebuildSmoothRoad();
                    }
                }

                if (GUILayout.Button("Rebuild Intersections"))
                {
                    RebuildAllIntersections();
                }

                if (GUILayout.Button("Connect Touching Roads"))
                {
                    ConnectTouchingRoadsInScene();
                }

                if (GUILayout.Button("Simplify Road Points"))
                {
                    SimplifyAllDenseRoads();
                }

                if (GUILayout.Button("Rebuild All Roads (Snap to Ground)"))
                {
                    RebuildAllRoadsInScene();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Continuation Settings", EditorStyles.boldLabel);
            intermediateContinuationMode = (IntermediateContinuationMode)EditorGUILayout.EnumPopup("Intermediate Mode", intermediateContinuationMode);

            int continuationIndex = GetContinuationPointIndex();
            string pointStatus;
            if (continuationIndex >= 0 && continuationIndex < smoothPath.Count)
            {
                pointStatus = selectedJunctionPointIndex >= 0
                    ? $"Point #{continuationIndex} (Selected with Ctrl)"
                    : $"Point #{continuationIndex} (End of Road)";
            }
            else
            {
                pointStatus = "None";
            }
            EditorGUILayout.LabelField("Continuation Point", pointStatus);
            EditorGUILayout.LabelField("Current Points", smoothPath.Count.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedJunctionPointIndex < 0 || selectedJunctionPointIndex == smoothPath.Count - 1))
                {
                    if (GUILayout.Button("Reset To End"))
                    {
                        selectedJunctionPointIndex = -1;
                        SceneView.RepaintAll();
                    }
                }

                using (new EditorGUI.DisabledScope(selectedJunctionPointIndex <= 0 || selectedJunctionPointIndex >= smoothPath.Count - 1))
                {
                    if (GUILayout.Button("Trim To Selected"))
                    {
                        Undo.IncrementCurrentGroup();
                        Undo.SetCurrentGroupName("Trim Road To Selected Point");
                        smoothPath.RemoveRange(selectedJunctionPointIndex + 1, smoothPath.Count - (selectedJunctionPointIndex + 1));
                        selectedJunctionPointIndex = smoothPath.Count - 1;
                        RebuildSmoothRoad();
                        SceneView.RepaintAll();
                    }
                }
            }

            if (Selection.activeGameObject != null)
            {
                SplineRoad selectedRoad = Selection.activeGameObject.GetComponent<SplineRoad>();
                if (selectedRoad != null && (activeSmoothRoad == null || activeSmoothRoad != selectedRoad.gameObject))
                {
                    EditorGUILayout.Space(4f);
                    if (GUILayout.Button($"Load '{selectedRoad.name}' Into Tool", GUILayout.Height(24f)))
                    {
                        LoadRoadIntoTool(selectedRoad, selectStart: false);
                    }
                }
            }
        }

        private void DrawPrefabTileSettings()
        {
            roadPrefab = (GameObject)EditorGUILayout.ObjectField("Road Prefab", roadPrefab, typeof(GameObject), false);
            tileLength = EditorGUILayout.FloatField("Tile Length", tileLength);
            forwardAxis = (PrefabForwardAxis)EditorGUILayout.EnumPopup("Prefab Forward Axis", forwardAxis);

            EditorGUILayout.Space(6f);
            createSplineRoad = EditorGUILayout.Toggle("Create SplineRoad", createSplineRoad);
            using (new EditorGUI.DisabledScope(!createSplineRoad))
            {
                splineLaneWidth = EditorGUILayout.FloatField("Spline Lane Width", splineLaneWidth);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Clear First Point"))
            {
                hasFirstPoint = false;
                SceneView.RepaintAll();
            }

            if (hasFirstPoint)
            {
                EditorGUILayout.LabelField("First Point", firstPoint.ToString("F2"));
            }
        }

        private bool TrySelectPointNearMouse(Vector2 mousePosition)
        {
            float bestScreenDistance = 45f;
            int bestPointIndex = -1;
            SplineRoad bestRoad = null;

            if (smoothPath != null && smoothPath.Count > 0)
            {
                for (int i = 0; i < smoothPath.Count; i++)
                {
                    Vector2 guiPoint = HandleUtility.WorldToGUIPoint(smoothPath[i]);
                    float dist = Vector2.Distance(mousePosition, guiPoint);
                    if (dist < bestScreenDistance)
                    {
                        bestScreenDistance = dist;
                        bestPointIndex = i;
                        bestRoad = activeSmoothRoad != null ? activeSmoothRoad.GetComponent<SplineRoad>() : null;
                    }
                }
            }

            SplineRoad[] sceneRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int r = 0; r < sceneRoads.Length; r++)
            {
                SplineRoad road = sceneRoads[r];
                if (road == null || !road.IsUsable) continue;

                int cpCount = road.ControlPointCount;
                if (cpCount >= 4)
                {
                    int anchorCount = (cpCount - 1) / 3 + 1;
                    for (int a = 0; a < anchorCount; a++)
                    {
                        Vector3 pt = road.GetControlPoint(a * 3);
                        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(pt);
                        float dist = Vector2.Distance(mousePosition, guiPoint);
                        if (dist < bestScreenDistance)
                        {
                            bestScreenDistance = dist;
                            bestPointIndex = a;
                            bestRoad = road;
                        }
                    }
                }
                else
                {
                    Vector3 p0 = road.GetPoint(0f);
                    Vector2 g0 = HandleUtility.WorldToGUIPoint(p0);
                    float d0 = Vector2.Distance(mousePosition, g0);
                    if (d0 < bestScreenDistance)
                    {
                        bestScreenDistance = d0;
                        bestPointIndex = 0;
                        bestRoad = road;
                    }

                    Vector3 p1 = road.GetPoint(1f);
                    Vector2 g1 = HandleUtility.WorldToGUIPoint(p1);
                    float d1 = Vector2.Distance(mousePosition, g1);
                    if (d1 < bestScreenDistance)
                    {
                        bestScreenDistance = d1;
                        bestPointIndex = -1;
                        bestRoad = road;
                    }
                }
            }

            if (bestRoad != null)
            {
                LoadRoadIntoTool(bestRoad, bestPointIndex);
                return true;
            }
            else if (bestPointIndex >= 0 && smoothPath != null && bestPointIndex < smoothPath.Count)
            {
                selectedJunctionPointIndex = bestPointIndex;
                return true;
            }

            return false;
        }

        private void CleanOrphanIntersectionVisuals()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform roadsRoot = roadParent != null ? roadParent : EnsureRoadRoot(scene);
            if (roadsRoot == null) return;
            Transform intersectionsRoot = roadsRoot.Find(GeneratedIntersectionVisualsName);
            if (intersectionsRoot == null) return;

            float checkRadius = roadWidth * 0.85f;
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < intersectionsRoot.childCount; i++)
            {
                Transform child = intersectionsRoot.GetChild(i);
                if (child == null) continue;

                Vector3 visualCenter = child.position;
                MeshFilter mf = child.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    visualCenter = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                }

                List<RoadBranchInfo> rawBranches = GetIntersectionBranches(visualCenter, checkRadius);
                List<RoadBranchInfo> branches = FilterUniqueBranches(rawBranches);

                if (branches.Count < 3)
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            for (int d = 0; d < toDestroy.Count; d++)
            {
                Undo.DestroyObjectImmediate(toDestroy[d]);
            }

            if (toDestroy.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void CleanOrphanRoadIntersections()
        {
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (intersections == null || intersections.Length == 0) return;

            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float checkRadiusSqr = (roadWidth * 0.9f) * (roadWidth * 0.9f);
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection inter = intersections[i];
                if (inter == null) continue;

                if (inter.connectedRoads != null)
                {
                    inter.connectedRoads.RemoveAll(r => r == null || !r.IsUsable);
                }

                int touchingCount = 0;
                Vector3 pos = inter.transform.position;
                for (int r = 0; r < allRoads.Length; r++)
                {
                    SplineRoad rd = allRoads[r];
                    if (rd == null || !rd.IsUsable) continue;

                    if (FlatDistanceSqr(rd.GetPoint(0f), pos) <= checkRadiusSqr ||
                        FlatDistanceSqr(rd.GetPoint(1f), pos) <= checkRadiusSqr)
                    {
                        touchingCount++;
                    }
                }

                if (touchingCount < 2)
                {
                    toDestroy.Add(inter.gameObject);
                }
                else
                {
                    SceneVisibilityManager.instance.DisablePicking(inter.gameObject, true);
                }
            }

            for (int d = 0; d < toDestroy.Count; d++)
            {
                Undo.DestroyObjectImmediate(toDestroy[d]);
            }
        }

        private bool TryFindNearbyRoadEndpoint(Vector3 point, out Vector3 closestEndpoint)
        {
            closestEndpoint = Vector3.zero;
            float dynamicSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 3f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;

            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad))
                {
                    continue;
                }

                Vector3 start = road.GetPoint(0f);
                Vector3 end = road.GetPoint(1f);

                float d0 = FlatDistanceSqr(point, start);
                if (d0 < bestDistanceSqr)
                {
                    bestDistanceSqr = d0;
                    closestEndpoint = start;
                }

                float d1 = FlatDistanceSqr(point, end);
                if (d1 < bestDistanceSqr)
                {
                    bestDistanceSqr = d1;
                    closestEndpoint = end;
                }
            }

            return bestDistanceSqr < (dynamicSnapDist * dynamicSnapDist);
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
            isHoverSnappedToRoad = false;

            CleanOrphanIntersectionVisuals();
            CleanOrphanRoadIntersections();

            if (hasHoverPoint && current.shift && !current.alt)
            {
                Vector3 snapTarget = Vector3.zero;
                bool hasSnap = false;

                if (TryFindNearbyIntersection(hoverPoint, out RoadIntersection nearbyInter))
                {
                    snapTarget = nearbyInter.transform.position;
                    hasSnap = true;
                }
                else if (TryFindNearbyRoadEndpoint(hoverPoint, out Vector3 endpointSnap))
                {
                    snapTarget = endpointSnap;
                    hasSnap = true;
                }
                else if (TryFindNearbyRoadPoint(hoverPoint, out SplineRoad snapRoad, out float snapT, out Vector3 snapPoint))
                {
                    snapTarget = snapPoint;
                    hasSnap = true;
                }

                if (hasSnap)
                {
                    hoverPoint = snapTarget;
                    isHoverSnappedToRoad = true;
                }
            }

            int passiveControlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.shift && !current.control && !current.alt)
            {
                HandleUtility.AddDefaultControl(passiveControlId);
            }

            if (current.type == EventType.MouseDown && current.button == 0 && current.control && !current.alt)
            {
                if (TrySelectPointNearMouse(current.mousePosition))
                {
                    handledSceneClick = true;
                    current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }
            }

            if (current.type == EventType.MouseDown && current.button == 0 && current.shift && !current.control && !current.alt)
            {
                if (hasHoverPoint)
                {
                    current.Use();
                    GUIUtility.hotControl = passiveControlId;
                    HandlePlacementClick(hoverPoint);
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }
            }

            DrawScenePreview();
            if (handledSceneClick || current.type == EventType.Used)
            {
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape)
                {
                    if (selectedJunctionPointIndex != -1)
                    {
                        selectedJunctionPointIndex = -1;
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    else if (smoothPath.Count > 0)
                    {
                        StartNewSmoothRoad();
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                }
                else if (current.keyCode == KeyCode.N)
                {
                    StartNewSmoothRoad();
                    current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }
            }

            if (current.type == EventType.MouseDown && current.button == 1 && current.shift && !current.alt)
            {
                current.Use();
                UndoLastSmoothPoint();
                return;
            }
        }

        private int GetContinuationPointIndex()
        {
            if (selectedJunctionPointIndex >= 0 && selectedJunctionPointIndex < smoothPath.Count)
            {
                return selectedJunctionPointIndex;
            }

            return smoothPath.Count > 0 ? smoothPath.Count - 1 : -1;
        }

        private void HandlePlacementClick(Vector3 point)
        {
            point.y += roadYOffset;

            if (placementMode == PlacementMode.SmoothMesh)
            {
                if (connectToExistingRoads)
                {
                    point = ResolveConnectionPoint(point);
                }

                ContinueSmoothRoad(point);
                return;
            }

            if (!hasFirstPoint)
            {
                firstPoint = point;
                hasFirstPoint = true;
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            CreatePrefabRoadSegment(firstPoint, point);
            firstPoint = point;
            hasFirstPoint = true;
            Repaint();
            SceneView.RepaintAll();
        }

        private void ContinueSmoothRoad(Vector3 point)
        {
            int continuationIndex = GetContinuationPointIndex();
            if (continuationIndex < 0 || smoothPath.Count == 0)
            {
                AddSmoothPoint(point);
                selectedJunctionPointIndex = smoothPath.Count - 1;
                return;
            }

            Vector3 continuationPoint = smoothPath[continuationIndex];
            if (Vector3.Distance(continuationPoint, point) < 0.2f)
            {
                return;
            }

            // Case A: Continuation point is at the end of the road (standard forward extension)
            if (continuationIndex == smoothPath.Count - 1)
            {
                smoothPath.Add(point);
                selectedJunctionPointIndex = smoothPath.Count - 1;
                if (smoothPath.Count >= 2)
                {
                    RebuildSmoothRoad();
                }

                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // Case B: Continuation point is at the start (index 0) - Extend backwards
            if (continuationIndex == 0)
            {
                smoothPath.Insert(0, point);
                selectedJunctionPointIndex = 0;
                if (smoothPath.Count >= 2)
                {
                    RebuildSmoothRoad();
                }

                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // Case C: Intermediate point continuation (0 < continuationIndex < smoothPath.Count - 1)
            if (intermediateContinuationMode == IntermediateContinuationMode.TrimAndContinue)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Trim Road & Continue");
                smoothPath.RemoveRange(continuationIndex + 1, smoothPath.Count - (continuationIndex + 1));
                smoothPath.Add(point);
                selectedJunctionPointIndex = smoothPath.Count - 1;
                if (smoothPath.Count >= 2)
                {
                    RebuildSmoothRoad();
                }

                Repaint();
                SceneView.RepaintAll();
            }
            else // BranchNewRoad
            {
                Vector3 forward = GetJunctionForward(continuationIndex);
                Vector3 dir = point - continuationPoint;
                dir.y = 0f;
                int slot = GetFreeAngleSlot(forward, dir.sqrMagnitude > 0.001f ? dir.normalized : forward);
                StartBranchRoad(continuationIndex, continuationPoint, point, "Continue Branch Road", slot);
            }
        }

        private void AddSmoothPoint(Vector3 point)
        {
            if (smoothPath.Count > 0 && Vector3.Distance(smoothPath[smoothPath.Count - 1], point) < 0.2f)
            {
                return;
            }

            smoothPath.Add(point);
            selectedJunctionPointIndex = smoothPath.Count - 1;
            if (smoothPath.Count >= 2)
            {
                RebuildSmoothRoad();
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private Vector3 ResolveConnectionPoint(Vector3 point)
        {
            if (TryFindNearbyIntersection(point, out RoadIntersection intersection))
            {
                Vector3 snapped = intersection.transform.position;
                CreateOrUpdateIntersectionVisual(snapped, GetCurrentRoadForward());
                return snapped;
            }

            if (TryFindNearbyRoadEndpoint(point, out Vector3 endpointSnap))
            {
                CreateOrUpdateIntersectionVisual(endpointSnap, GetCurrentRoadForward());
                return endpointSnap;
            }

            if (TryFindNearbyRoadPoint(point, out SplineRoad road, out float t, out Vector3 roadPoint))
            {
                if (t > 0.03f && t < 0.97f)
                {
                    SplitExistingRoadAtConnection(road, t, roadPoint);
                }
                else
                {
                    roadPoint = t <= 0.03f ? road.GetPoint(0f) : road.GetPoint(1f);
                }

                CreateOrUpdateIntersectionVisual(roadPoint, road.GetTangent(t));
                return roadPoint;
            }

            return point;
        }

        private bool TryFindNearbyIntersection(Vector3 point, out RoadIntersection closest)
        {
            closest = null;
            float dynamicSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 2f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null)
                {
                    continue;
                }

                float distanceSqr = FlatDistanceSqr(point, intersection.transform.position);
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                closest = intersection;
            }

            return closest != null;
        }

        private bool TryFindNearbyRoadPoint(Vector3 point, out SplineRoad closestRoad, out float closestT, out Vector3 closestPoint)
        {
            closestRoad = null;
            closestT = 0f;
            closestPoint = Vector3.zero;

            float dynamicSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 2.5f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad))
                {
                    continue;
                }

                float length = road.GetTotalLength();
                int sampleCount = Mathf.Max(12, Mathf.CeilToInt(length / 1.0f));
                float bestRoadT = -1f;
                float bestRoadDistSqr = bestDistanceSqr;

                for (int sample = 0; sample <= sampleCount; sample++)
                {
                    float t = sample / (float)sampleCount;
                    Vector3 candidate = road.GetPoint(t);
                    float distanceSqr = FlatDistanceSqr(point, candidate);
                    if (distanceSqr < bestRoadDistSqr)
                    {
                        bestRoadDistSqr = distanceSqr;
                        bestRoadT = t;
                    }
                }

                if (bestRoadT >= 0f)
                {
                    float step = 1f / sampleCount;
                    float low = Mathf.Clamp01(bestRoadT - step);
                    float high = Mathf.Clamp01(bestRoadT + step);
                    for (int iter = 0; iter < 6; iter++)
                    {
                        float mid1 = low + (high - low) / 3f;
                        float mid2 = high - (high - low) / 3f;
                        float d1 = FlatDistanceSqr(point, road.GetPoint(mid1));
                        float d2 = FlatDistanceSqr(point, road.GetPoint(mid2));
                        if (d1 < d2)
                        {
                            high = mid2;
                        }
                        else
                        {
                            low = mid1;
                        }
                    }

                    float refinedT = (low + high) * 0.5f;
                    Vector3 refinedPoint = road.GetPoint(refinedT);
                    float refinedDistSqr = FlatDistanceSqr(point, refinedPoint);
                    if (refinedDistSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = refinedDistSqr;
                        closestRoad = road;
                        closestT = refinedT;
                        closestPoint = refinedPoint;
                    }
                }
            }

            return closestRoad != null;
        }

        private float FlatDistanceSqr(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        private Vector3 GetCurrentRoadForward()
        {
            if (smoothPath.Count >= 2)
            {
                Vector3 forward = smoothPath[smoothPath.Count - 1] - smoothPath[smoothPath.Count - 2];
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    return forward.normalized;
                }
            }

            return Vector3.forward;
        }

        private void RebuildSmoothRoad()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform root = roadParent != null ? roadParent : EnsureRoadRoot(scene);
            activeSmoothRoad = EnsureActiveSmoothRoad(scene, root);
            EnsurePlainAsphaltMaterial();

            Undo.RegisterFullObjectHierarchyUndo(activeSmoothRoad, "Rebuild Smooth Road");

            List<Vector3> samples = BuildSmoothSamples(smoothPath);
            if (snapToGround)
            {
                SnapSamplesToGround(samples);
            }

            List<Vector3> visualSamples = BuildTrimmedVisualSamples(samples);

            GameObject asphalt = EnsureChild(activeSmoothRoad.transform, "Asphalt");
            GameObject sidewalks = EnsureChild(activeSmoothRoad.transform, "Sidewalks");
            GameObject laneMarkings = EnsureChild(activeSmoothRoad.transform, "Lane Markings");
            GameObject bridgeStructures = EnsureChild(activeSmoothRoad.transform, "Bridge Structures");

            if (visualSamples.Count >= 2)
            {
                ApplyStripMesh(asphalt, visualSamples, -roadWidth * 0.5f, roadWidth * 0.5f, roadMaterial, "Smooth Road Asphalt");
                asphalt.SetActive(true);
            }
            else
            {
                asphalt.SetActive(false);
            }

            if (sidewalkWidth > 0.01f && visualSamples.Count >= 2)
            {
                ApplySidewalkMesh(sidewalks, visualSamples);
                sidewalks.SetActive(true);
            }
            else
            {
                sidewalks.SetActive(false);
            }

            if (createLaneMarkings && visualSamples.Count >= 2)
            {
                ApplyLaneMarkingsMesh(laneMarkings, visualSamples);
                laneMarkings.SetActive(true);
            }
            else
            {
                laneMarkings.SetActive(false);
            }

            if (autoCreateBridges && visualSamples.Count >= 2)
            {
                ApplyBridgeStructures(bridgeStructures, visualSamples);
            }
            else
            {
                bridgeStructures.SetActive(false);
            }

            if (createSplineRoad)
            {
                SplineRoad splineRoad = activeSmoothRoad.GetComponent<SplineRoad>();
                if (splineRoad == null)
                {
                    splineRoad = Undo.AddComponent<SplineRoad>(activeSmoothRoad);
                }

                splineRoad.laneWidth = splineLaneWidth;
                splineRoad.SetLinearPath(smoothPath.Count >= 2 ? smoothPath : samples);
                if (createRoadNetworkLinks)
                {
                    AssignRoadNetworkEndpoints(splineRoad, samples, scene);
                }

                EditorUtility.SetDirty(splineRoad);
            }

            if (samples.Count >= 2)
            {
                UpdateIntersectionsAtEndpoints(samples[0], samples[samples.Count - 1]);
            }

            EditorUtility.SetDirty(activeSmoothRoad);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private void UpdateIntersectionsAtEndpoints(Vector3 p0, Vector3 p1)
        {
            CheckAndUpdateIntersectionAtPoint(p0);
            CheckAndUpdateIntersectionAtPoint(p1);
        }

        private void CheckAndUpdateIntersectionAtPoint(Vector3 point)
        {
            List<RoadBranchInfo> rawBranches = GetIntersectionBranches(point);
            List<RoadBranchInfo> branches = FilterUniqueBranches(rawBranches);
            if (branches.Count >= 3)
            {
                Vector3 forward = branches[0].OutgoingDirection;
                CreateOrUpdateIntersectionVisual(point, forward);
            }
            else
            {
                CleanOrphanIntersectionVisuals();
            }
        }

        private void RebuildRoadVisuals(GameObject roadObj, List<Vector3> samples)
        {
            if (roadObj == null || samples == null || samples.Count < 2) return;
            if (snapToGround)
            {
                SnapSamplesToGround(samples);
            }

            List<Vector3> visualSamples = BuildTrimmedVisualSamples(samples);

            GameObject asphalt = EnsureChild(roadObj.transform, "Asphalt");
            GameObject sidewalks = EnsureChild(roadObj.transform, "Sidewalks");
            GameObject laneMarkings = EnsureChild(roadObj.transform, "Lane Markings");
            GameObject bridgeStructures = EnsureChild(roadObj.transform, "Bridge Structures");

            if (visualSamples.Count >= 2)
            {
                ApplyStripMesh(asphalt, visualSamples, -roadWidth * 0.5f, roadWidth * 0.5f, roadMaterial, "Smooth Road Asphalt");
                asphalt.SetActive(true);
            }
            else
            {
                asphalt.SetActive(false);
            }

            if (sidewalkWidth > 0.01f && visualSamples.Count >= 2)
            {
                ApplySidewalkMesh(sidewalks, visualSamples);
                sidewalks.SetActive(true);
            }
            else
            {
                sidewalks.SetActive(false);
            }

            if (createLaneMarkings && visualSamples.Count >= 2)
            {
                ApplyLaneMarkingsMesh(laneMarkings, visualSamples);
                laneMarkings.SetActive(true);
            }
            else
            {
                laneMarkings.SetActive(false);
            }

            if (autoCreateBridges && visualSamples.Count >= 2)
            {
                ApplyBridgeStructures(bridgeStructures, visualSamples);
            }
            else
            {
                bridgeStructures.SetActive(false);
            }
        }

        private GameObject EnsureActiveSmoothRoad(Scene scene, Transform root)
        {
            if (activeSmoothRoad != null)
            {
                return activeSmoothRoad;
            }

            GameObject roadObject = new GameObject($"SmoothRoad_{System.DateTime.Now:HHmmssfff}");
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Smooth Road");
            SceneManager.MoveGameObjectToScene(roadObject, scene);
            roadObject.transform.SetParent(root, true);
            return roadObject;
        }

        private void ApplySidewalkMesh(GameObject sidewalks, List<Vector3> samples)
        {
            Mesh mesh = new Mesh { name = "Smooth Road Sidewalks Mesh" };
            int columnsPerSide = Mathf.Max(2, sidewalkCrossSegments + 2);
            List<Vector3> vertices = new List<Vector3>(samples.Count * columnsPerSide * 2);
            List<Vector2> uvs = new List<Vector2>(samples.Count * columnsPerSide * 2);
            List<int> triangles = new List<int>((samples.Count - 1) * (columnsPerSide - 1) * 12);
            float halfRoad = roadWidth * 0.5f;
            float bevelWidth = Mathf.Min(sidewalkBevelWidth, sidewalkWidth);

            AppendSidewalkSide(vertices, uvs, triangles, samples, -1f, halfRoad, bevelWidth, columnsPerSide);
            AppendSidewalkSide(vertices, uvs, triangles, samples, 1f, halfRoad, bevelWidth, columnsPerSide);

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = EnsureComponent<MeshFilter>(sidewalks);
            MeshRenderer renderer = EnsureComponent<MeshRenderer>(sidewalks);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = sidewalkMaterial != null ? sidewalkMaterial : roadMaterial;
            sidewalks.isStatic = true;

            MeshCollider meshCollider = sidewalks.GetComponent<MeshCollider>();
            if (createSidewalkCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = Undo.AddComponent<MeshCollider>(sidewalks);
                }

                meshCollider.sharedMesh = mesh;
            }
            else if (meshCollider != null)
            {
                Undo.DestroyObjectImmediate(meshCollider);
            }
        }

        private void ApplyLaneMarkingsMesh(GameObject target, List<Vector3> samples)
        {
            Mesh mesh = new Mesh { name = "Smooth Road Lane Markings Mesh" };
            List<Vector3> vertices = new List<Vector3>(samples.Count * 8);
            List<Vector2> uvs = new List<Vector2>(samples.Count * 8);
            List<int> triangles = new List<int>(samples.Count * 12);
            List<float> cumulativeDistances = BuildCumulativeDistances(samples);

            AppendDashedLine(vertices, uvs, triangles, samples, cumulativeDistances, 0f);

            if (createSideEdgeLines)
            {
                float sideOffset = Mathf.Max(0f, roadWidth * 0.5f - laneMarkingWidth * 1.5f);
                AppendContinuousLine(vertices, uvs, triangles, samples, -sideOffset);
                AppendContinuousLine(vertices, uvs, triangles, samples, sideOffset);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = EnsureComponent<MeshFilter>(target);
            MeshRenderer renderer = EnsureComponent<MeshRenderer>(target);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = laneMarkingMaterial != null ? laneMarkingMaterial : GetOrCreateLaneMarkingMaterial();
            target.isStatic = true;
        }

        private void AppendDashedLine(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<float> cumulativeDistances,
            float lateralOffset)
        {
            if (samples.Count < 2 || cumulativeDistances.Count != samples.Count)
            {
                return;
            }

            float totalDistance = cumulativeDistances[cumulativeDistances.Count - 1];
            float repeatDistance = laneDashLength + laneDashGap;

            for (float dashStart = 0f; dashStart < totalDistance; dashStart += repeatDistance)
            {
                float dashEnd = Mathf.Min(dashStart + laneDashLength, totalDistance);
                if (dashEnd - dashStart < MinDashDistance)
                {
                    continue;
                }

                Vector3 start = SampleByDistance(samples, cumulativeDistances, dashStart);
                Vector3 end = SampleByDistance(samples, cumulativeDistances, dashEnd);
                AppendLineQuad(vertices, uvs, triangles, start, end, lateralOffset);
            }
        }

        private void AppendContinuousLine(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            float lateralOffset)
        {
            for (int i = 0; i < samples.Count - 1; i++)
            {
                AppendLineQuad(vertices, uvs, triangles, samples[i], samples[i + 1], lateralOffset);
            }
        }

        private void AppendLineQuad(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 start,
            Vector3 end,
            float lateralOffset)
        {
            Vector3 tangent = end - start;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            Vector3 offset = right * lateralOffset;
            Vector3 halfWidth = right * (laneMarkingWidth * 0.5f);
            bool skipGroundSnap = autoCreateBridges && (IsBridgeSample(start) || IsBridgeSample(end));
            int index = vertices.Count;

            vertices.Add(AdjustRoadVertexToGround(start + offset - halfWidth, laneMarkingYOffset, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(start + offset + halfWidth, laneMarkingYOffset, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(end + offset - halfWidth, laneMarkingYOffset, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(end + offset + halfWidth, laneMarkingYOffset, skipGroundSnap));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 1);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private List<float> BuildCumulativeDistances(List<Vector3> samples)
        {
            List<float> distances = new List<float>(samples.Count);
            float total = 0f;
            distances.Add(total);

            for (int i = 1; i < samples.Count; i++)
            {
                total += Vector3.Distance(samples[i - 1], samples[i]);
                distances.Add(total);
            }

            return distances;
        }

        private Vector3 SampleByDistance(List<Vector3> samples, List<float> cumulativeDistances, float targetDistance)
        {
            if (samples.Count == 0)
            {
                return Vector3.zero;
            }

            if (targetDistance <= 0f)
            {
                return samples[0];
            }

            int lastIndex = samples.Count - 1;
            if (targetDistance >= cumulativeDistances[lastIndex])
            {
                return samples[lastIndex];
            }

            for (int i = 1; i < cumulativeDistances.Count; i++)
            {
                if (targetDistance > cumulativeDistances[i])
                {
                    continue;
                }

                float previousDistance = cumulativeDistances[i - 1];
                float segmentDistance = Mathf.Max(0.001f, cumulativeDistances[i] - previousDistance);
                float t = (targetDistance - previousDistance) / segmentDistance;
                return Vector3.Lerp(samples[i - 1], samples[i], t);
            }

            return samples[lastIndex];
        }

        private void ApplyStripMesh(GameObject target, List<Vector3> samples, float leftOffset, float rightOffset, Material material, string meshName)
        {
            Mesh mesh = new Mesh { name = meshName };
            List<Vector3> vertices = new List<Vector3>(samples.Count * 2);
            List<Vector2> uvs = new List<Vector2>(samples.Count * 2);
            List<int> triangles = new List<int>((samples.Count - 1) * 6);

            AppendStrip(vertices, uvs, triangles, samples, leftOffset, rightOffset, roadCrossSegments);

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = EnsureComponent<MeshFilter>(target);
            MeshRenderer renderer = EnsureComponent<MeshRenderer>(target);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            target.isStatic = true;

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            bool shouldCreateCollider = createMeshCollider || (autoCreateBridges && BuildBridgeSpans(samples).Count > 0);
            if (shouldCreateCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = Undo.AddComponent<MeshCollider>(target);
                }

                meshCollider.sharedMesh = mesh;
            }
            else if (meshCollider != null)
            {
                Undo.DestroyObjectImmediate(meshCollider);
            }
        }

        private void AppendStrip(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            float leftOffset,
            float rightOffset,
            int crossSegments)
        {
            AppendStrip(vertices, uvs, triangles, samples, leftOffset, rightOffset, crossSegments, roadYOffset, false);
        }

        private void AppendStrip(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            float leftOffset,
            float rightOffset,
            int crossSegments,
            float verticalOffset,
            bool keepRelativeToPath)
        {
            int startVertex = vertices.Count;
            float accumulatedDistance = 0f;
            int columns = Mathf.Max(1, crossSegments) + 1;
            List<bool> isBridgeList = new List<bool>(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    accumulatedDistance += Vector3.Distance(samples[i - 1], samples[i]);
                }

                Vector3 right = GetPathRight(samples, i);
                bool skipGroundSnap = keepRelativeToPath || (autoCreateBridges && IsBridgeSample(samples[i]));
                isBridgeList.Add(skipGroundSnap);
                float v = accumulatedDistance / Mathf.Max(1f, tileLength);
                for (int column = 0; column < columns; column++)
                {
                    float u = column / (float)(columns - 1);
                    float offset = Mathf.Lerp(leftOffset, rightOffset, u);
                    vertices.Add(AdjustRoadVertexToGround(samples[i] + right * offset, verticalOffset, skipGroundSnap));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int row = startVertex + i * columns;
                int nextRow = row + columns;
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = row + column;
                    int b = row + column + 1;
                    int c = nextRow + column;
                    int d = nextRow + column + 1;

                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            // If no sidewalk exists, add outer edge skirts to the asphalt so the road does not float
            if (sidewalkWidth <= 0.001f)
            {
                AppendAsphaltEdgeSkirt(vertices, uvs, triangles, samples, isBridgeList, startVertex, columns, -1f, leftOffset, verticalOffset);
                AppendAsphaltEdgeSkirt(vertices, uvs, triangles, samples, isBridgeList, startVertex, columns, 1f, rightOffset, verticalOffset);
            }
        }

        private void AppendAsphaltEdgeSkirt(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<bool> isBridgeList,
            int startVertex,
            int columns,
            float side,
            float offset,
            float verticalOffset)
        {
            int skirtStartVertex = vertices.Count;
            float accumulatedDistance = 0f;
            int columnIndex = side < 0f ? 0 : (columns - 1);

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    accumulatedDistance += Vector3.Distance(samples[i - 1], samples[i]);
                }

                float v = accumulatedDistance / Mathf.Max(1f, tileLength);
                int topIndex = startVertex + i * columns + columnIndex;
                Vector3 outerTop = vertices[topIndex];
                Vector3 right = GetPathRight(samples, i);
                bool isBridge = isBridgeList[i];

                Vector3 outerBottom;
                if (isBridge)
                {
                    outerBottom = outerTop - Vector3.up * Mathf.Max(bridgeDeckThickness, 0.45f);
                }
                else
                {
                    outerBottom = AdjustRoadVertexToGround(samples[i] + right * offset, verticalOffset - 0.35f, false);
                }

                vertices.Add(outerBottom);
                uvs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int topA = startVertex + i * columns + columnIndex;
                int topB = startVertex + (i + 1) * columns + columnIndex;
                int botA = skirtStartVertex + i;
                int botB = skirtStartVertex + i + 1;

                if (side > 0f)
                {
                    triangles.Add(topA);
                    triangles.Add(topB);
                    triangles.Add(botA);

                    triangles.Add(topB);
                    triangles.Add(botB);
                    triangles.Add(botA);
                }
                else
                {
                    triangles.Add(topA);
                    triangles.Add(botA);
                    triangles.Add(topB);

                    triangles.Add(topB);
                    triangles.Add(botA);
                    triangles.Add(botB);
                }
            }
        }

        private void AppendSidewalkSide(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            float side,
            float halfRoad,
            float bevelWidth,
            int columns)
        {
            if (samples == null || samples.Count < 2 || columns < 2 || sidewalkWidth <= 0.001f)
            {
                return;
            }

            int startVertex = vertices.Count;
            float accumulatedDistance = 0f;
            float usableBevelWidth = Mathf.Clamp(bevelWidth, 0f, sidewalkWidth);
            List<bool> isBridgeList = new List<bool>(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    accumulatedDistance += Vector3.Distance(samples[i - 1], samples[i]);
                }

                Vector3 right = GetPathRight(samples, i);
                bool isBridge = autoCreateBridges && IsBridgeSample(samples[i]);
                isBridgeList.Add(isBridge);
                float v = accumulatedDistance / Mathf.Max(1f, tileLength);
                for (int column = 0; column < columns; column++)
                {
                    float u = column / (float)(columns - 1);
                    float distanceFromRoad = sidewalkWidth * u;
                    float offset = side * (halfRoad + distanceFromRoad);
                    float rampT = usableBevelWidth > 0.001f ? Mathf.Clamp01(distanceFromRoad / usableBevelWidth) : 1f;
                    float heightOffset = Mathf.SmoothStep(0f, sidewalkHeightOffset, rampT);
                    vertices.Add(AdjustRoadVertexToGround(samples[i] + right * offset, roadYOffset + heightOffset, isBridge));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int row = startVertex + i * columns;
                int nextRow = row + columns;
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = row + column;
                    int b = row + column + 1;
                    int c = nextRow + column;
                    int d = nextRow + column + 1;

                    if (side < 0f)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(d);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(d);
                    }
                }
            }

            // Outer curb skirt (drop wall) to prevent floating edges when terrain slopes away
            int skirtStartVertex = vertices.Count;
            accumulatedDistance = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    accumulatedDistance += Vector3.Distance(samples[i - 1], samples[i]);
                }

                float v = accumulatedDistance / Mathf.Max(1f, tileLength);
                int topIndex = startVertex + i * columns + (columns - 1);
                Vector3 outerTop = vertices[topIndex];
                Vector3 right = GetPathRight(samples, i);
                float outerOffset = side * (halfRoad + sidewalkWidth);
                bool isBridge = isBridgeList[i];

                Vector3 outerBottom;
                if (isBridge)
                {
                    outerBottom = outerTop - Vector3.up * Mathf.Max(bridgeDeckThickness, 0.45f);
                }
                else
                {
                    // Drop down 35 cm into the ground from ground level
                    outerBottom = AdjustRoadVertexToGround(samples[i] + right * outerOffset, roadYOffset - 0.35f, false);
                }

                vertices.Add(outerBottom);
                uvs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int topA = startVertex + i * columns + (columns - 1);
                int topB = startVertex + (i + 1) * columns + (columns - 1);
                int botA = skirtStartVertex + i;
                int botB = skirtStartVertex + i + 1;

                if (side > 0f)
                {
                    // Right side: outward normal points right
                    triangles.Add(topA);
                    triangles.Add(topB);
                    triangles.Add(botA);

                    triangles.Add(topB);
                    triangles.Add(botB);
                    triangles.Add(botA);
                }
                else
                {
                    // Left side: outward normal points left
                    triangles.Add(topA);
                    triangles.Add(botA);
                    triangles.Add(topB);

                    triangles.Add(topB);
                    triangles.Add(botA);
                    triangles.Add(botB);
                }
            }
        }

        private void ApplyBridgeStructures(GameObject target, List<Vector3> samples)
        {
            List<Vector2Int> spans = BuildBridgeSpans(samples);
            if (spans.Count == 0)
            {
                target.SetActive(false);
                return;
            }

            Mesh mesh = new Mesh { name = "Smooth Road Bridge Structures Mesh" };
            List<Vector3> vertices = new List<Vector3>(spans.Count * 96);
            List<int> triangles = new List<int>(spans.Count * 144);
            List<Vector2> uvs = new List<Vector2>(spans.Count * 96);
            List<float> distances = BuildCumulativeDistances(samples);
            float halfRoad = roadWidth * 0.5f;
            float sideOffset = halfRoad + sidewalkWidth + bridgeSideBeamWidth * 0.5f;
            float railOffset = Mathf.Max(halfRoad, halfRoad + sidewalkWidth - bridgeRailWidth * 0.5f);

            for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                Vector2Int span = spans[spanIndex];
                for (int i = span.x; i < span.y; i++)
                {
                    AppendBridgeSegment(vertices, uvs, triangles, samples, i, -sideOffset, bridgeSideBeamWidth, bridgeSideBeamHeight, -bridgeDeckThickness - bridgeSideBeamHeight * 0.5f);
                    AppendBridgeSegment(vertices, uvs, triangles, samples, i, sideOffset, bridgeSideBeamWidth, bridgeSideBeamHeight, -bridgeDeckThickness - bridgeSideBeamHeight * 0.5f);

                    if (bridgeRailHeight > 0.01f)
                    {
                        AppendBridgeSegment(vertices, uvs, triangles, samples, i, -railOffset, bridgeRailWidth, bridgeRailHeight, bridgeRailHeight * 0.5f);
                        AppendBridgeSegment(vertices, uvs, triangles, samples, i, railOffset, bridgeRailWidth, bridgeRailHeight, bridgeRailHeight * 0.5f);
                    }
                }

                AppendBridgePillars(vertices, uvs, triangles, samples, distances, span);
                if (createBridgeTowers)
                {
                    AppendBridgeTowers(vertices, uvs, triangles, samples, distances, span);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = EnsureComponent<MeshFilter>(target);
            MeshRenderer renderer = EnsureComponent<MeshRenderer>(target);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = sidewalkMaterial != null ? sidewalkMaterial : roadMaterial;
            target.isStatic = true;

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            if (createBridgeStructureCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = Undo.AddComponent<MeshCollider>(target);
                }

                meshCollider.sharedMesh = mesh;
            }
            else if (meshCollider != null)
            {
                Undo.DestroyObjectImmediate(meshCollider);
            }

            target.SetActive(true);
        }

        private List<Vector2Int> BuildBridgeSpans(List<Vector3> samples)
        {
            List<Vector2Int> spans = new List<Vector2Int>();
            if (samples == null || samples.Count < 2)
            {
                return spans;
            }

            List<float> distances = BuildCumulativeDistances(samples);
            int start = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                if (IsBridgeSample(samples[i]))
                {
                    if (start < 0)
                    {
                        start = Mathf.Max(0, i - 1);
                    }
                }
                else if (start >= 0)
                {
                    AddBridgeSpanIfLongEnough(spans, distances, start, Mathf.Min(samples.Count - 1, i));
                    start = -1;
                }
            }

            if (start >= 0)
            {
                AddBridgeSpanIfLongEnough(spans, distances, start, samples.Count - 1);
            }

            return spans;
        }

        private void AddBridgeSpanIfLongEnough(List<Vector2Int> spans, List<float> distances, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            float length = distances[end] - distances[start];
            if (length >= bridgeMinLength)
            {
                spans.Add(new Vector2Int(start, end));
            }
        }

        private bool IsBridgeSample(Vector3 sample)
        {
            if (!autoCreateBridges)
            {
                return false;
            }

            float groundY = bridgeSupportBaseY;
            if (TryGetGroundHeight(sample, out float hitY))
            {
                groundY = hitY;
            }

            return sample.y - groundY >= bridgeMinGroundClearance;
        }

        private void AppendBridgeSegment(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            int sampleIndex,
            float lateralOffset,
            float width,
            float height,
            float verticalOffset)
        {
            Vector3 a = samples[sampleIndex];
            Vector3 b = samples[sampleIndex + 1];
            Vector3 tangent = b - a;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 forward = tangent.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 start = a + right * lateralOffset + Vector3.up * verticalOffset;
            Vector3 end = b + right * lateralOffset + Vector3.up * verticalOffset;
            float length = Vector3.Distance(start, end);
            Vector3 center = (start + end) * 0.5f;
            AppendOrientedBox(vertices, uvs, triangles, center, forward, right, new Vector3(width, height, length));
        }

        private void AppendBridgePillars(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<float> distances,
            Vector2Int span)
        {
            float spanStartDistance = distances[span.x];
            float spanEndDistance = distances[span.y];
            float firstPillarDistance = spanStartDistance + bridgePillarSpacing * 0.5f;
            float pillarSideOffset = Mathf.Max(bridgePillarWidth, roadWidth * 0.32f);

            for (float distance = firstPillarDistance; distance < spanEndDistance; distance += bridgePillarSpacing)
            {
                Vector3 center = SampleByDistance(samples, distances, distance);
                Vector3 forward = GetPathForwardAtDistance(samples, distances, distance);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                AppendBridgePillar(vertices, uvs, triangles, center + right * -pillarSideOffset, forward, right);
                AppendBridgePillar(vertices, uvs, triangles, center + right * pillarSideOffset, forward, right);
            }
        }

        private void AppendBridgeTowers(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<float> distances,
            Vector2Int span)
        {
            float spanStartDistance = distances[span.x];
            float spanEndDistance = distances[span.y];
            float spanLength = spanEndDistance - spanStartDistance;
            if (spanLength < bridgeMinLength)
            {
                return;
            }

            AppendBridgeTowerAtDistance(vertices, uvs, triangles, samples, distances, spanStartDistance);
            if (spanLength > bridgeTowerSpacing * 0.75f)
            {
                for (float distance = spanStartDistance + bridgeTowerSpacing; distance < spanEndDistance - bridgeTowerSpacing * 0.45f; distance += bridgeTowerSpacing)
                {
                    AppendBridgeTowerAtDistance(vertices, uvs, triangles, samples, distances, distance);
                }
            }

            AppendBridgeTowerAtDistance(vertices, uvs, triangles, samples, distances, spanEndDistance);
        }

        private void AppendBridgeTowerAtDistance(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<float> distances,
            float distance)
        {
            Vector3 center = SampleByDistance(samples, distances, distance);
            Vector3 forward = GetPathForwardAtDistance(samples, distances, distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float sideOffset = Mathf.Max(bridgeTowerSideOffset, roadWidth * 0.5f + bridgeTowerWidth);
            float towerCenterY = center.y + bridgeTowerHeight * 0.5f;
            Vector3 leftTowerCenter = new Vector3(center.x, towerCenterY, center.z) - right * sideOffset;
            Vector3 rightTowerCenter = new Vector3(center.x, towerCenterY, center.z) + right * sideOffset;
            Vector3 towerSize = new Vector3(bridgeTowerWidth, bridgeTowerHeight, bridgeTowerWidth);

            AppendOrientedBox(vertices, uvs, triangles, leftTowerCenter, forward, right, towerSize);
            AppendOrientedBox(vertices, uvs, triangles, rightTowerCenter, forward, right, towerSize);

            Vector3 beamCenter = new Vector3(center.x, center.y + bridgeTowerHeight - bridgeTopBeamHeight * 0.5f, center.z);
            Vector3 beamSize = new Vector3(sideOffset * 2f + bridgeTowerWidth, bridgeTopBeamHeight, bridgeTowerWidth);
            AppendOrientedBox(vertices, uvs, triangles, beamCenter, forward, right, beamSize);
        }

        private Vector3 GetPathForwardAtDistance(List<Vector3> samples, List<float> distances, float distance)
        {
            if (samples.Count < 2)
            {
                return Vector3.forward;
            }

            for (int i = 1; i < distances.Count; i++)
            {
                if (distance > distances[i])
                {
                    continue;
                }

                Vector3 forward = samples[i] - samples[i - 1];
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }

            Vector3 lastForward = samples[samples.Count - 1] - samples[samples.Count - 2];
            lastForward.y = 0f;
            return lastForward.sqrMagnitude > 0.0001f ? lastForward.normalized : Vector3.forward;
        }

        private void AppendBridgePillar(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 topCenter,
            Vector3 forward,
            Vector3 right)
        {
            float bottomY = bridgeSupportBaseY;
            if (TrySampleTerrainHeight(topCenter, out float terrainHeight))
            {
                bottomY = terrainHeight;
            }

            float topY = topCenter.y - bridgeDeckThickness - bridgeSideBeamHeight;
            if (topY - bottomY < bridgePillarWidth)
            {
                bottomY = topY - bridgePillarWidth;
            }

            Vector3 center = new Vector3(topCenter.x, (topY + bottomY) * 0.5f, topCenter.z);
            float height = Mathf.Max(bridgePillarWidth, topY - bottomY);
            AppendOrientedBox(vertices, uvs, triangles, center, forward, right, new Vector3(bridgePillarWidth, height, bridgePillarWidth));
        }

        private void AppendOrientedBox(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            Vector3 size)
        {
            Vector3 halfRight = right.normalized * (size.x * 0.5f);
            Vector3 halfUp = Vector3.up * (size.y * 0.5f);
            Vector3 halfForward = forward.normalized * (size.z * 0.5f);
            int index = vertices.Count;

            vertices.Add(center - halfRight - halfUp - halfForward);
            vertices.Add(center + halfRight - halfUp - halfForward);
            vertices.Add(center + halfRight - halfUp + halfForward);
            vertices.Add(center - halfRight - halfUp + halfForward);
            vertices.Add(center - halfRight + halfUp - halfForward);
            vertices.Add(center + halfRight + halfUp - halfForward);
            vertices.Add(center + halfRight + halfUp + halfForward);
            vertices.Add(center - halfRight + halfUp + halfForward);

            for (int i = 0; i < 8; i++)
            {
                uvs.Add(Vector2.zero);
            }

            AppendBoxFace(triangles, index, 0, 1, 2, 3);
            AppendBoxFace(triangles, index, 4, 7, 6, 5);
            AppendBoxFace(triangles, index, 0, 4, 5, 1);
            AppendBoxFace(triangles, index, 1, 5, 6, 2);
            AppendBoxFace(triangles, index, 2, 6, 7, 3);
            AppendBoxFace(triangles, index, 3, 7, 4, 0);
        }

        private void AppendBoxFace(List<int> triangles, int vertexOffset, int a, int b, int c, int d)
        {
            triangles.Add(vertexOffset + a);
            triangles.Add(vertexOffset + b);
            triangles.Add(vertexOffset + c);
            triangles.Add(vertexOffset + a);
            triangles.Add(vertexOffset + c);
            triangles.Add(vertexOffset + d);
        }

        private List<Vector3> BuildSmoothSamples(List<Vector3> points)
        {
            List<Vector3> samples = new List<Vector3>(points.Count * samplesPerSegment);
            if (points.Count < 2)
            {
                return samples;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = i > 0 ? points[i - 1] : points[i];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = i + 2 < points.Count ? points[i + 2] : p2;

                float segmentDist = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(samplesPerSegment, Mathf.CeilToInt(segmentDist / 2.0f));

                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    samples.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            samples.Add(points[points.Count - 1]);
            return samples;
        }

        private void SnapSamplesToGround(List<Vector3> samples)
        {
            if (samples == null || samples.Count == 0) return;

            if (autoCreateBridges && samples.Count >= 2)
            {
                int startBridge = -1;
                int endBridge = -1;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector3 sample = samples[i];
                    float groundY = bridgeSupportBaseY;
                    bool hasGround = TryGetGroundHeight(sample, out groundY);

                    bool isOverGap = (!hasGround) || (sample.y - groundY >= bridgeMinGroundClearance);

                    if (isOverGap)
                    {
                        if (startBridge < 0) startBridge = Mathf.Max(0, i - 1);
                        endBridge = Mathf.Min(samples.Count - 1, i + 1);
                    }
                }

                if (startBridge >= 0 && endBridge > startBridge)
                {
                    Vector3 startPos = samples[startBridge];
                    Vector3 endPos = samples[endBridge];

                    for (int i = startBridge; i <= endBridge; i++)
                    {
                        float t = (float)(i - startBridge) / (endBridge - startBridge);
                        float deckY = Mathf.Lerp(startPos.y, endPos.y, t);

                        if (bridgeArchHeight > 0.01f)
                        {
                            deckY += Mathf.Sin(t * Mathf.PI) * bridgeArchHeight;
                        }

                        samples[i] = new Vector3(samples[i].x, deckY, samples[i].z);
                    }

                    for (int i = 0; i < startBridge; i++)
                    {
                        if (TryGetGroundHeight(samples[i], out float groundHeight))
                        {
                            samples[i] = new Vector3(samples[i].x, groundHeight + groundOffset, samples[i].z);
                        }
                    }

                    for (int i = endBridge + 1; i < samples.Count; i++)
                    {
                        if (TryGetGroundHeight(samples[i], out float groundHeight))
                        {
                            samples[i] = new Vector3(samples[i].x, groundHeight + groundOffset, samples[i].z);
                        }
                    }

                    return;
                }
            }

            for (int i = 0; i < samples.Count; i++)
            {
                if (TryGetGroundHeight(samples[i], out float groundHeight))
                {
                    samples[i] = new Vector3(samples[i].x, groundHeight + groundOffset, samples[i].z);
                }
            }
        }

        private List<Vector3> BuildTrimmedVisualSamples(List<Vector3> samples)
        {
            if (samples == null || samples.Count < 2)
            {
                return samples != null ? new List<Vector3>(samples) : new List<Vector3>();
            }

            float startTrim = GetEndpointTrimDistance(samples[0]);
            float endTrim = GetEndpointTrimDistance(samples[samples.Count - 1]);
            if (startTrim <= 0f && endTrim <= 0f)
            {
                return new List<Vector3>(samples);
            }

            List<float> distances = BuildCumulativeDistances(samples);
            float totalDistance = distances[distances.Count - 1];
            if (totalDistance <= startTrim + endTrim + 0.5f)
            {
                return new List<Vector3>(samples);
            }

            float fromDistance = Mathf.Clamp(startTrim, 0f, totalDistance);
            float toDistance = Mathf.Clamp(totalDistance - endTrim, fromDistance + 0.5f, totalDistance);
            List<Vector3> trimmed = new List<Vector3>(samples.Count);
            trimmed.Add(SampleByDistance(samples, distances, fromDistance));

            for (int i = 1; i < samples.Count - 1; i++)
            {
                float distance = distances[i];
                if (distance > fromDistance && distance < toDistance)
                {
                    trimmed.Add(samples[i]);
                }
            }

            trimmed.Add(SampleByDistance(samples, distances, toDistance));
            return trimmed;
        }

        private sealed class RoadBranchInfo
        {
            public SplineRoad Road;
            public bool IsStart;
            public Vector3 Endpoint;
            public Vector3 OutgoingDirection;
        }

        private sealed class AngularPerimeterPoint
        {
            public Vector3 Position;
            public float Angle;
            public int BranchIndex;
            public bool IsRightEdge;
        }

        private float GetEndpointTrimDistance(Vector3 endpoint)
        {
            Vector3 intersectionPos;
            if (!TryFindNearbyIntersectionOrVisual(endpoint, out intersectionPos, out int branchCount))
            {
                return 0f;
            }

            // Only 4 or more connections (4-way crossroad) get trimmed for the square intersection tile
            if (branchCount >= 4)
            {
                return intersectionRoadTrimDistance > 0f ? intersectionRoadTrimDistance : (intersectionSize * 0.5f);
            }

            // 3 or fewer connections -> 0 trim so roads connect directly to each other!
            return 0f;
        }

        private bool TryFindNearbyIntersectionOrVisual(Vector3 endpoint, out Vector3 center, out int branchCount)
        {
            center = endpoint;
            branchCount = 0;

            float checkDist = Mathf.Max(endpointMergeDistance, 6.5f);
            float checkDistSqr = checkDist * checkDist;

            // 1. Check existing RoadIntersection components
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null) continue;

                if (FlatDistanceSqr(endpoint, intersection.transform.position) <= checkDistSqr)
                {
                    center = intersection.transform.position;
                    List<RoadBranchInfo> branches = GetIntersectionBranches(center);
                    branchCount = Mathf.Max(branches.Count, intersection.connectedRoads != null ? intersection.connectedRoads.Count : 0);
                    return true;
                }
            }

            // 2. Check existing Intersection Visuals by name/position
            Transform roadsRoot = roadParent != null ? roadParent : EnsureRoadRoot(SceneManager.GetActiveScene());
            Transform visualsRoot = roadsRoot != null ? roadsRoot.Find(GeneratedIntersectionVisualsName) : null;
            if (visualsRoot == null)
            {
                GameObject rootObj = GameObject.Find(GeneratedIntersectionVisualsName);
                if (rootObj != null) visualsRoot = rootObj.transform;
            }

            if (visualsRoot != null)
            {
                for (int i = 0; i < visualsRoot.childCount; i++)
                {
                    Transform child = visualsRoot.GetChild(i);
                    if (child == null) continue;

                    MeshFilter mf = child.GetComponentInChildren<MeshFilter>();
                    Vector3 visualCenter = (mf != null && mf.sharedMesh != null) ? mf.transform.TransformPoint(mf.sharedMesh.bounds.center) : child.position;
                    if (FlatDistanceSqr(endpoint, visualCenter) <= checkDistSqr)
                    {
                        center = visualCenter;
                        List<RoadBranchInfo> branches = GetIntersectionBranches(center);
                        branchCount = branches.Count;
                        return true;
                    }
                }
            }

            // 3. Check directly via GetIntersectionBranches
            List<RoadBranchInfo> directBranches = GetIntersectionBranches(endpoint);
            if (directBranches.Count >= 2)
            {
                center = endpoint;
                branchCount = directBranches.Count;
                return true;
            }

            return false;
        }

        private List<RoadBranchInfo> GetIntersectionBranches(Vector3 center, float searchRadius = 6.5f)
        {
            List<RoadBranchInfo> branches = new List<RoadBranchInfo>();
            float radiusSqr = searchRadius * searchRadius;

            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allRoads.Length; i++)
            {
                SplineRoad r = allRoads[i];
                if (r == null || !r.IsUsable) continue;

                Vector3 p0 = r.GetPoint(0f);
                if (FlatDistanceSqr(p0, center) <= radiusSqr)
                {
                    Vector3 tangent = r.GetTangent(0.01f);
                    tangent.y = 0f;
                    if (tangent.sqrMagnitude < 0.001f && r.ControlPointCount >= 4)
                    {
                        tangent = r.GetControlPoint(3) - p0;
                        tangent.y = 0f;
                    }
                    Vector3 dir = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
                    branches.Add(new RoadBranchInfo { Road = r, IsStart = true, Endpoint = p0, OutgoingDirection = dir });
                }

                Vector3 p1 = r.GetPoint(1f);
                if (FlatDistanceSqr(p1, center) <= radiusSqr)
                {
                    Vector3 tangent = -r.GetTangent(0.99f);
                    tangent.y = 0f;
                    if (tangent.sqrMagnitude < 0.001f && r.ControlPointCount >= 4)
                    {
                        tangent = p1 - r.GetControlPoint(r.ControlPointCount - 4);
                        tangent.y = 0f;
                    }
                    Vector3 dir = tangent.sqrMagnitude > 0.001f ? tangent.normalized : -Vector3.forward;
                    branches.Add(new RoadBranchInfo { Road = r, IsStart = false, Endpoint = p1, OutgoingDirection = dir });
                }
            }

            // Also check current smoothPath if not yet in SplineRoad or actively being placed
            if (activeSmoothRoad != null && smoothPath.Count >= 2)
            {
                SplineRoad activeSpline = activeSmoothRoad.GetComponent<SplineRoad>();
                bool trackedStart = activeSpline != null && branches.Exists(b => b.Road == activeSpline && b.IsStart);
                bool trackedEnd = activeSpline != null && branches.Exists(b => b.Road == activeSpline && !b.IsStart);

                if (!trackedStart && FlatDistanceSqr(smoothPath[0], center) <= radiusSqr)
                {
                    Vector3 dir = smoothPath[1] - smoothPath[0];
                    dir.y = 0f;
                    branches.Add(new RoadBranchInfo { Road = activeSpline, IsStart = true, Endpoint = smoothPath[0], OutgoingDirection = dir.normalized });
                }

                if (!trackedEnd && FlatDistanceSqr(smoothPath[smoothPath.Count - 1], center) <= radiusSqr)
                {
                    Vector3 dir = smoothPath[smoothPath.Count - 2] - smoothPath[smoothPath.Count - 1];
                    dir.y = 0f;
                    branches.Add(new RoadBranchInfo { Road = activeSpline, IsStart = false, Endpoint = smoothPath[smoothPath.Count - 1], OutgoingDirection = dir.normalized });
                }
            }

            return branches;
        }

        private bool TryGetGroundHeight(Vector3 position, out float height)
        {
            if (TrySampleTerrainHeight(position, out height))
            {
                return true;
            }

            Vector3 rayOrigin = position + Vector3.up * groundProbeHeight;
            float rayDistance = groundProbeHeight + groundProbeDepth;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance);
            if (hits == null || hits.Length == 0)
            {
                height = position.y;
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger || IsPartOfAnyRoad(hit.collider.transform))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                height = hit.point.y;
                found = true;
            }

            if (found)
            {
                return true;
            }

            height = position.y;
            return false;
        }

        private bool TrySampleTerrainHeight(Vector3 position, out float height)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            float margin = 1.0f;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                bool insideX = position.x >= terrainPosition.x - margin && position.x <= terrainPosition.x + terrainSize.x + margin;
                bool insideZ = position.z >= terrainPosition.z - margin && position.z <= terrainPosition.z + terrainSize.z + margin;
                if (!insideX || !insideZ)
                {
                    continue;
                }

                Vector3 clampedPos = new Vector3(
                    Mathf.Clamp(position.x, terrainPosition.x, terrainPosition.x + terrainSize.x),
                    position.y,
                    Mathf.Clamp(position.z, terrainPosition.z, terrainPosition.z + terrainSize.z)
                );

                height = terrainPosition.y + terrain.SampleHeight(clampedPos);
                return true;
            }

            height = position.y;
            return false;
        }

        private bool IsPartOfActiveRoad(Transform candidate)
        {
            return activeSmoothRoad != null && candidate != null && candidate.IsChildOf(activeSmoothRoad.transform);
        }

        private bool IsPartOfAnyRoad(Transform candidate)
        {
            if (candidate == null) return false;
            if (activeSmoothRoad != null && candidate.IsChildOf(activeSmoothRoad.transform)) return true;
            if (candidate.GetComponentInParent<SplineRoad>() != null) return true;
            if (candidate.GetComponentInParent<RoadIntersection>() != null) return true;

            Transform roadsRoot = roadParent != null ? roadParent : null;
            if (roadsRoot != null && candidate.IsChildOf(roadsRoot)) return true;

            string cName = candidate.name;
            if (cName.Contains("SmoothRoad") || cName.Contains("Asphalt") || cName.Contains("Bridge") || cName.Contains("Intersection") || cName.Contains("Sidewalk") || cName.Contains("Road"))
            {
                return true;
            }

            return false;
        }

        private Vector3 AdjustRoadVertexToGround(Vector3 position, float extraOffset)
        {
            return AdjustRoadVertexToGround(position, extraOffset, false);
        }

        private Vector3 AdjustRoadVertexToGround(Vector3 position, float extraOffset, bool skipGroundSnap)
        {
            if (skipGroundSnap || !snapToGround || !TryGetGroundHeight(position, out float height))
            {
                return position + Vector3.up * extraOffset;
            }

            return new Vector3(position.x, height + groundOffset + extraOffset, position.z);
        }

        private void AssignRoadNetworkEndpoints(SplineRoad road, List<Vector3> samples, Scene scene)
        {
            if (road == null || samples == null || samples.Count < 2)
            {
                return;
            }

            RoadNetworkManager manager = EnsureRoadNetworkManager(scene);
            Transform intersectionsRoot = EnsureChildRoot(manager.transform, GeneratedIntersectionsName);
            RoadIntersection start = FindOrCreateEndpointIntersection(intersectionsRoot, samples[0], "Start");
            RoadIntersection end = FindOrCreateEndpointIntersection(intersectionsRoot, samples[samples.Count - 1], "End");

            road.startIntersection = start;
            road.endIntersection = end;
            start.RegisterConnectedRoad(road);
            end.RegisterConnectedRoad(road);

            manager.RefreshNetwork();
            EditorUtility.SetDirty(start);
            EditorUtility.SetDirty(end);
            EditorUtility.SetDirty(manager);
        }

        private RoadNetworkManager EnsureRoadNetworkManager(Scene scene)
        {
            RoadNetworkManager manager = Object.FindFirstObjectByType<RoadNetworkManager>();
            if (manager != null)
            {
                return manager;
            }

            GameObject managerObject = new GameObject(GeneratedNetworkName);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create RoadNetworkManager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            return Undo.AddComponent<RoadNetworkManager>(managerObject);
        }

        private Transform EnsureChildRoot(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private RoadIntersection FindOrCreateEndpointIntersection(Transform intersectionsRoot, Vector3 position, string endpointName)
        {
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float mergeDistanceSqr = endpointMergeDistance * endpointMergeDistance;
            RoadIntersection closest = null;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null)
                {
                    continue;
                }

                Vector3 delta = intersection.transform.position - position;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > mergeDistanceSqr || distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                closest = intersection;
                closestDistanceSqr = distanceSqr;
            }

            if (closest != null)
            {
                Undo.RecordObject(closest.transform, "Move Road Intersection");
                closest.transform.position = Vector3.Lerp(closest.transform.position, position, 0.5f);
                return closest;
            }

            GameObject intersectionObject = new GameObject($"RoadIntersection_{endpointName}_{System.DateTime.Now:HHmmssfff}");
            Undo.RegisterCreatedObjectUndo(intersectionObject, "Create Road Intersection");
            intersectionObject.transform.SetParent(intersectionsRoot, false);
            intersectionObject.transform.position = position;

            RoadIntersection created = Undo.AddComponent<RoadIntersection>(intersectionObject);
            created.isStopSignControlled = true;
            return created;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 GetPathRight(List<Vector3> samples, int index)
        {
            int previousIndex = Mathf.Max(0, index - 1);
            int nextIndex = Mathf.Min(samples.Count - 1, index + 1);
            Vector3 tangent = samples[nextIndex] - samples[previousIndex];
            tangent.y = 0f;

            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.forward;
            }

            return Vector3.Cross(Vector3.up, tangent.normalized).normalized;
        }

        private T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return Undo.AddComponent<T>(target);
        }

        private GameObject EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent, false);
            return child;
        }

        private void StartNewSmoothRoad()
        {
            smoothPath.Clear();
            activeSmoothRoad = null;
            selectedJunctionPointIndex = -1;
            SceneView.RepaintAll();
            Repaint();
        }

        private void UndoLastSmoothPoint()
        {
            if (smoothPath.Count == 0)
            {
                return;
            }

            smoothPath.RemoveAt(smoothPath.Count - 1);
            if (selectedJunctionPointIndex >= smoothPath.Count)
            {
                selectedJunctionPointIndex = -1;
            }

            if (smoothPath.Count >= 2 && activeSmoothRoad != null)
            {
                RebuildSmoothRoad();
            }
            else if (smoothPath.Count < 2 && activeSmoothRoad != null)
            {
                Undo.DestroyObjectImmediate(activeSmoothRoad);
                activeSmoothRoad = null;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void CreatePrefabRoadSegment(Vector3 start, Vector3 end)
        {
            if (roadPrefab == null)
            {
                EditorUtility.DisplayDialog("Road Placement", "Road Prefab secilmeden yol olusturulamiyor.", "OK");
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
            Transform root = roadParent != null ? roadParent : EnsureRoadRoot(scene);
            GameObject segmentObject = new GameObject($"RoadSegment_{System.DateTime.Now:HHmmssfff}");
            Undo.RegisterCreatedObjectUndo(segmentObject, "Create Road Segment");
            SceneManager.MoveGameObjectToScene(segmentObject, scene);
            segmentObject.transform.SetParent(root, true);

            int tileCount = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.1f, tileLength)));
            Vector3 direction = flatDelta.normalized;
            Quaternion rotation = BuildRoadRotation(direction);

            for (int i = 0; i < tileCount; i++)
            {
                float t = (i + 0.5f) / tileCount;
                Vector3 position = Vector3.Lerp(start, end, t);
                Object instance = PrefabUtility.InstantiatePrefab(roadPrefab, scene);
                GameObject roadTile = instance as GameObject;
                if (roadTile == null)
                {
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(roadTile, "Place Road Tile");
                roadTile.transform.SetParent(segmentObject.transform, true);
                roadTile.transform.SetPositionAndRotation(position, rotation);
                roadTile.isStatic = true;
            }

            if (createSplineRoad)
            {
                SplineRoad splineRoad = Undo.AddComponent<SplineRoad>(segmentObject);
                splineRoad.laneWidth = splineLaneWidth;
                splineRoad.SetLinearPath(new[] { start, end });
                EditorUtility.SetDirty(splineRoad);
            }

            EditorUtility.SetDirty(segmentObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private Transform EnsureRoadRoot(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == DefaultRootName)
                {
                    return rootObject.transform;
                }
            }

            GameObject newRoot = new GameObject(DefaultRootName);
            Undo.RegisterCreatedObjectUndo(newRoot, "Create Placed Roads Root");
            SceneManager.MoveGameObjectToScene(newRoot, scene);
            return newRoot.transform;
        }

        private Quaternion BuildRoadRotation(Vector3 direction)
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
            GUILayout.BeginArea(new Rect(12f, 12f, 440f, 96f), EditorStyles.helpBox);
            GUILayout.Label("Simple Road Placement", EditorStyles.boldLabel);

            if (placementMode == PlacementMode.SmoothMesh)
            {
                string statusText = smoothPath.Count == 0
                    ? "Yeni Yol Modu: İlk noktayı koymak için Shift+Sol Tık yapın."
                    : $"Yol çiziliyor ({smoothPath.Count} nokta). Bitirip yeni yola başlamak için N'ye basın.";
                GUILayout.Label(statusText);

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Yeni Yol (N)", GUILayout.Height(22)))
                    {
                        StartNewSmoothRoad();
                    }

                    if (GUILayout.Button("Yolları Bağla / Kavşak", GUILayout.Height(22)))
                    {
                        ConnectTouchingRoadsInScene();
                    }

                    if (smoothPath.Count > 0 && GUILayout.Button("Sil (Sağ Tık)", GUILayout.Height(22)))
                    {
                        UndoLastSmoothPoint();
                    }
                }
            }
            else
            {
                GUILayout.Label(hasFirstPoint ? "Shift + Sol Tık: Yolu bitir ve devam et" : "Shift + Sol Tık: İlk noktayı seç");
            }

            GUILayout.EndArea();
            Handles.EndGUI();

            if (placementMode == PlacementMode.SmoothMesh)
            {
                DrawSmoothPreview();
                return;
            }

            if (hasFirstPoint)
            {
                Handles.color = Color.green;
                Handles.SphereHandleCap(0, firstPoint, Quaternion.identity, HandleUtility.GetHandleSize(firstPoint) * 0.18f, EventType.Repaint);
            }

            if (hasFirstPoint && hasHoverPoint)
            {
                Vector3 previewPoint = hoverPoint + Vector3.up * roadYOffset;
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(5f, firstPoint, previewPoint);
                Handles.SphereHandleCap(0, previewPoint, Quaternion.identity, HandleUtility.GetHandleSize(previewPoint) * 0.14f, EventType.Repaint);
            }
        }

        private void DrawSmoothPreview()
        {
            int continuationIndex = GetContinuationPointIndex();

            for (int i = 0; i < smoothPath.Count; i++)
            {
                float handleSize = HandleUtility.GetHandleSize(smoothPath[i]) * 0.16f;
                bool isSelected = (i == continuationIndex && selectedJunctionPointIndex >= 0);

                if (isSelected)
                {
                    Handles.color = Color.magenta;
                }
                else if (i == smoothPath.Count - 1)
                {
                    Handles.color = Color.yellow;
                }
                else if (i == 0)
                {
                    Handles.color = new Color(0.25f, 0.85f, 1f);
                }
                else
                {
                    Handles.color = Color.green;
                }

                if (Event.current.control && !Event.current.alt)
                {
                    if (Handles.Button(smoothPath[i], Quaternion.identity, handleSize * 1.15f, handleSize * 1.55f, Handles.SphereHandleCap))
                    {
                        selectedJunctionPointIndex = i;
                        handledSceneClick = true;
                        Event.current.Use();
                        Repaint();
                    }
                }
                else
                {
                    Handles.SphereHandleCap(0, smoothPath[i], Quaternion.identity, handleSize, EventType.Repaint);
                }

                if (isSelected)
                {
                    Handles.color = Color.magenta;
                    Handles.DrawWireDisc(smoothPath[i], Vector3.up, handleSize * 1.85f);
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
                    labelStyle.normal.textColor = Color.magenta;
                    Handles.Label(smoothPath[i] + Vector3.up * (handleSize * 1.75f), $"[Devam Noktasi #{i}]", labelStyle);
                }
            }

            if (Event.current.control && !Event.current.alt)
            {
                DrawSceneRoadHandles(showIntermediatePoints: true);
            }
            else if (smoothPath.Count == 0)
            {
                DrawSceneRoadHandles(showIntermediatePoints: false);
            }

            if (smoothPath.Count >= 2)
            {
                List<Vector3> samples = BuildSmoothSamples(smoothPath);
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(4f, samples.ToArray());
            }

            if (continuationIndex >= 0 && continuationIndex < smoothPath.Count && hasHoverPoint)
            {
                Vector3 origin = smoothPath[continuationIndex];
                Vector3 previewPoint = isHoverSnappedToRoad ? hoverPoint : (hoverPoint + Vector3.up * roadYOffset);
                Handles.color = isHoverSnappedToRoad ? new Color(0f, 1f, 0.9f, 0.95f) : Color.yellow;
                Handles.DrawAAPolyLine(isHoverSnappedToRoad ? 5f : 3f, origin, previewPoint);

                if (isHoverSnappedToRoad)
                {
                    Handles.color = new Color(0f, 1f, 0.9f, 0.95f);
                    Handles.DrawWireDisc(previewPoint, Vector3.up, 2.2f);
                    Handles.DrawWireDisc(previewPoint, Vector3.up, 0.9f);
                    Handles.SphereHandleCap(0, previewPoint, Quaternion.identity, 0.45f, EventType.Repaint);
                    GUIStyle lockStyle = new GUIStyle(EditorStyles.boldLabel);
                    lockStyle.normal.textColor = new Color(0f, 1f, 0.9f);
                    Handles.Label(previewPoint + Vector3.up * 1.5f, "[KAVŞAK KİLİTLENDİ (Shift+Tık)]", lockStyle);
                }
                else
                {
                    Handles.SphereHandleCap(0, previewPoint, Quaternion.identity, HandleUtility.GetHandleSize(previewPoint) * 0.13f, EventType.Repaint);
                }
            }
            else if (smoothPath.Count == 0 && hasHoverPoint && isHoverSnappedToRoad)
            {
                Handles.color = new Color(0f, 1f, 0.9f, 0.95f);
                Handles.DrawWireDisc(hoverPoint, Vector3.up, 2.2f);
                Handles.DrawWireDisc(hoverPoint, Vector3.up, 0.9f);
                Handles.SphereHandleCap(0, hoverPoint, Quaternion.identity, 0.45f, EventType.Repaint);
                GUIStyle lockStyle = new GUIStyle(EditorStyles.boldLabel);
                lockStyle.normal.textColor = new Color(0f, 1f, 0.9f);
                Handles.Label(hoverPoint + Vector3.up * 1.5f, "[KAVŞAK KİLİTLENDİ (Shift+Tık)]", lockStyle);
            }

            if (Event.current.shift && Event.current.control && smoothPath.Count >= 2)
            {
                DrawBranchHandles();
            }
        }

        private void DrawSceneRoadHandles(bool showIntermediatePoints)
        {
            SplineRoad[] sceneRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int r = 0; r < sceneRoads.Length; r++)
            {
                SplineRoad road = sceneRoads[r];
                if (road == null || !road.IsUsable || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad))
                {
                    continue;
                }

                Vector3 startPoint = road.GetPoint(0f);
                Vector3 endPoint = road.GetPoint(1f);

                // Start endpoint (Cyan)
                float startSize = HandleUtility.GetHandleSize(startPoint) * 0.18f;
                Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                if (Handles.Button(startPoint, Quaternion.identity, startSize, startSize * 1.5f, Handles.SphereHandleCap))
                {
                    if (Event.current.shift || smoothPath.Count == 0)
                    {
                        StartNewSmoothRoad();
                        smoothPath.Add(startPoint);
                        selectedJunctionPointIndex = 0;
                    }
                    else
                    {
                        LoadRoadIntoTool(road, selectPointIndex: 0);
                    }
                    handledSceneClick = true;
                    Event.current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }

                // End endpoint (Yellow)
                float endSize = HandleUtility.GetHandleSize(endPoint) * 0.18f;
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                if (Handles.Button(endPoint, Quaternion.identity, endSize, endSize * 1.5f, Handles.SphereHandleCap))
                {
                    if (Event.current.shift || smoothPath.Count == 0)
                    {
                        StartNewSmoothRoad();
                        smoothPath.Add(endPoint);
                        selectedJunctionPointIndex = 0;
                    }
                    else
                    {
                        LoadRoadIntoTool(road, selectPointIndex: -1);
                    }
                    handledSceneClick = true;
                    Event.current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }

                // Only draw intermediate points if holding Ctrl
                if (showIntermediatePoints)
                {
                    int cpCount = road.ControlPointCount;
                    if (cpCount >= 4)
                    {
                        int anchorCount = (cpCount - 1) / 3 + 1;
                        for (int a = 1; a < anchorCount - 1; a++)
                        {
                            int cp = a * 3;
                            Vector3 pt = road.GetControlPoint(cp);
                            float size = HandleUtility.GetHandleSize(pt) * 0.16f;
                            Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);

                            if (Handles.Button(pt, Quaternion.identity, size, size * 1.5f, Handles.SphereHandleCap))
                            {
                                LoadRoadIntoTool(road, selectPointIndex: a);
                                handledSceneClick = true;
                                Event.current.Use();
                                Repaint();
                                SceneView.RepaintAll();
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void LoadRoadIntoTool(SplineRoad road, int selectPointIndex = -1)
        {
            if (road == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Load Road Into Placement Tool");

            activeSmoothRoad = road.gameObject;
            smoothPath.Clear();

            for (int i = 0; i < road.ControlPointCount; i += 3)
            {
                smoothPath.Add(road.GetControlPoint(i));
            }

            if (smoothPath.Count < 2)
            {
                smoothPath.Clear();
                smoothPath.Add(road.GetPoint(0f));
                smoothPath.Add(road.GetPoint(1f));
            }

            if (selectPointIndex >= 0 && selectPointIndex < smoothPath.Count)
            {
                selectedJunctionPointIndex = selectPointIndex;
            }
            else
            {
                selectedJunctionPointIndex = smoothPath.Count - 1;
            }

            Selection.activeGameObject = road.gameObject;
            SceneView.RepaintAll();
            Repaint();
        }

        private void LoadRoadIntoTool(SplineRoad road, bool selectStart)
        {
            LoadRoadIntoTool(road, selectStart ? 0 : -1);
        }

        private void DrawBranchHandles()
        {
            int index = selectedJunctionPointIndex >= 0 && selectedJunctionPointIndex < smoothPath.Count
                ? selectedJunctionPointIndex
                : smoothPath.Count - 1;

            Vector3 origin = smoothPath[index];
            Vector3 forward = GetJunctionForward(index);
            DrawBranchHandle(index, origin, Quaternion.AngleAxis(-branchSideAngle, Vector3.up) * forward, new Color(0.2f, 0.85f, 1f), "Left Branch", -1);
            DrawBranchHandle(index, origin, forward, Color.green, "Straight Branch", 0);
            DrawBranchHandle(index, origin, Quaternion.AngleAxis(branchSideAngle, Vector3.up) * forward, new Color(1f, 0.75f, 0.15f), "Right Branch", 1);

            if (enableFreeAngleBranch && hasHoverPoint && !handledSceneClick)
            {
                DrawFreeAngleBranch(index, origin, forward);
            }
        }

        private Vector3 GetJunctionForward(int index)
        {
            Vector3 forward = Vector3.forward;
            if (smoothPath.Count < 2)
            {
                return forward;
            }

            if (index <= 0)
            {
                forward = smoothPath[1] - smoothPath[0];
            }
            else if (index >= smoothPath.Count - 1)
            {
                forward = smoothPath[smoothPath.Count - 1] - smoothPath[smoothPath.Count - 2];
            }
            else
            {
                forward = smoothPath[index + 1] - smoothPath[index - 1];
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        private void DrawBranchHandle(int junctionIndex, Vector3 origin, Vector3 direction, Color color, string undoName, int slot)
        {
            Vector3 candidate = origin + direction.normalized * branchPreviewLength;
            candidate = SnapPreviewPointToGround(candidate);

            Handles.color = color;
            Handles.DrawAAPolyLine(4f, origin, candidate);

            float size = HandleUtility.GetHandleSize(candidate) * branchHandleSize;
            if (Handles.Button(candidate, Quaternion.identity, size, size * 1.35f, Handles.SphereHandleCap))
            {
                StartBranchRoad(junctionIndex, origin, candidate, undoName, slot);
                handledSceneClick = true;
                Event.current.Use();
            }
        }

        private void DrawFreeAngleBranch(int junctionIndex, Vector3 origin, Vector3 referenceForward)
        {
            Vector3 candidate = SnapPreviewPointToGround(hoverPoint);
            Vector3 direction = candidate - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 4f)
            {
                return;
            }

            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(3f, origin, candidate);
            Handles.SphereHandleCap(0, candidate, Quaternion.identity, HandleUtility.GetHandleSize(candidate) * branchHandleSize * 0.7f, EventType.Repaint);

            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && current.shift && current.control && !current.alt)
            {
                int slot = GetFreeAngleSlot(referenceForward, direction.normalized);
                StartBranchRoad(junctionIndex, origin, candidate, "Free Angle Branch", slot);
                handledSceneClick = true;
                current.Use();
            }
        }

        private int GetFreeAngleSlot(Vector3 referenceForward, Vector3 direction)
        {
            float angle = Vector3.SignedAngle(referenceForward, direction, Vector3.up);
            return 1000 + Mathf.RoundToInt(angle / 5f);
        }

        private Vector3 SnapPreviewPointToGround(Vector3 point)
        {
            point.y += roadYOffset;
            if (snapToGround && TryGetGroundHeight(point, out float height))
            {
                point.y = height + groundOffset;
            }

            return point;
        }

        private void StartBranchRoad(int branchIndex, Vector3 origin, Vector3 candidate, string undoName, int slot)
        {
            Vector3 junctionForward = GetJunctionForward(branchIndex);
            BranchRecord existing = FindBranchRecord(origin, slot);
            if (existing != null)
            {
                RemoveBranchRecord(existing);
                handledSceneClick = true;
                SceneView.RepaintAll();
                Repaint();
                return;
            }

            BranchRecord record = new BranchRecord
            {
                Junction = origin,
                Slot = slot,
                OriginalPath = new List<Vector3>(smoothPath),
                IntersectionVisualName = BuildIntersectionVisualName(origin)
            };

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            if (smoothPath.Count >= 2 && activeSmoothRoad != null)
            {
                if (branchIndex > 0 && branchIndex < smoothPath.Count - 1)
                {
                    SplitActiveRoadAtJunction(branchIndex, record);
                }
                else
                {
                    RebuildSmoothRoad();
                }
            }

            smoothPath.Clear();
            activeSmoothRoad = null;
            smoothPath.Add(origin);
            smoothPath.Add(candidate);
            selectedJunctionPointIndex = smoothPath.Count - 1;
            RebuildSmoothRoad();
            record.BranchRoad = activeSmoothRoad;
            branchRecords.Add(record);

            CreateOrUpdateIntersectionVisual(origin, junctionForward);
            if (record.FirstSplitRoad != null)
            {
                SplineRoad r1 = record.FirstSplitRoad.GetComponent<SplineRoad>();
                if (r1 != null && r1.IsUsable)
                {
                    RebuildRoadVisuals(record.FirstSplitRoad, SampleRoadPath(r1, 0f, 1f));
                }
            }
            if (record.SecondSplitRoad != null)
            {
                SplineRoad r2 = record.SecondSplitRoad.GetComponent<SplineRoad>();
                if (r2 != null && r2.IsUsable)
                {
                    RebuildRoadVisuals(record.SecondSplitRoad, SampleRoadPath(r2, 0f, 1f));
                }
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void SplitActiveRoadAtJunction(int junctionIndex, BranchRecord record)
        {
            if (activeSmoothRoad == null || junctionIndex <= 0 || junctionIndex >= smoothPath.Count - 1)
            {
                return;
            }

            List<Vector3> originalPath = new List<Vector3>(smoothPath);
            GameObject oldRoad = activeSmoothRoad;

            Undo.DestroyObjectImmediate(oldRoad);

            List<Vector3> firstPath = originalPath.GetRange(0, junctionIndex + 1);
            List<Vector3> secondPath = originalPath.GetRange(junctionIndex, originalPath.Count - junctionIndex);
            record.FirstSplitRoad = CreateSmoothRoadFromPath(firstPath);
            record.SecondSplitRoad = CreateSmoothRoadFromPath(secondPath);
        }

        private void SplitExistingRoadAtConnection(SplineRoad road, float t, Vector3 connectionPoint)
        {
            if (road == null || road.gameObject == null || t <= 0.02f || t >= 0.98f)
            {
                return;
            }

            float dynamicSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 2.5f);
            Vector3 existingPoint = road.GetPoint(t);
            if (FlatDistanceSqr(existingPoint, connectionPoint) > dynamicSnapDist * dynamicSnapDist)
            {
                return;
            }

            List<Vector3> firstPath = SampleRoadPath(road, 0f, t);
            List<Vector3> secondPath = SampleRoadPath(road, t, 1f);
            if (firstPath.Count < 2 || secondPath.Count < 2)
            {
                return;
            }

            firstPath[firstPath.Count - 1] = connectionPoint;
            secondPath[0] = connectionPoint;

            List<Vector3> savedSmoothPath = new List<Vector3>(smoothPath);
            GameObject savedActiveRoad = activeSmoothRoad;
            int savedSelectedIndex = selectedJunctionPointIndex;
            Transform savedRoadParent = roadParent;
            Transform targetParent = road.transform.parent != null ? road.transform.parent : savedRoadParent;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Split Road For Connection");
            Vector3 roadTangent = road.GetTangent(t);
            Undo.DestroyObjectImmediate(road.gameObject);

            roadParent = targetParent;
            CreateSmoothRoadFromPath(firstPath);
            CreateSmoothRoadFromPath(secondPath);

            roadParent = savedRoadParent;
            smoothPath = savedSmoothPath;
            activeSmoothRoad = savedActiveRoad;
            selectedJunctionPointIndex = savedSelectedIndex;

            CreateOrUpdateIntersectionVisual(connectionPoint, roadTangent);
        }

        private List<Vector3> SampleRoadPath(SplineRoad road, float startT, float endT)
        {
            List<Vector3> path = new List<Vector3>();
            float totalLength = road.GetTotalLength();
            float startDistance = road.TToDistance(startT);
            float endDistance = road.TToDistance(endT);
            float range = Mathf.Abs(endDistance - startDistance);
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(range / 2.0f));

            for (int i = 0; i <= sampleCount; i++)
            {
                float lerp = i / (float)sampleCount;
                float distance = Mathf.Lerp(startDistance, endDistance, lerp);
                float t = totalLength > 0f ? road.DistanceToT(distance) : Mathf.Lerp(startT, endT, lerp);
                path.Add(road.GetPoint(t));
            }

            return path;
        }

        private GameObject CreateSmoothRoadFromPath(List<Vector3> path)
        {
            smoothPath = new List<Vector3>(path);
            activeSmoothRoad = null;
            selectedJunctionPointIndex = -1;
            RebuildSmoothRoad();
            return activeSmoothRoad;
        }

        private BranchRecord FindBranchRecord(Vector3 junction, int slot)
        {
            for (int i = branchRecords.Count - 1; i >= 0; i--)
            {
                BranchRecord record = branchRecords[i];
                if (record == null || record.BranchRoad == null)
                {
                    branchRecords.RemoveAt(i);
                    continue;
                }

                if (record.Slot == slot && Vector3.Distance(record.Junction, junction) <= branchToggleDistance)
                {
                    return record;
                }
            }

            return null;
        }

        private void RemoveBranchRecord(BranchRecord record)
        {
            if (record == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Remove Road Branch");

            if (record.BranchRoad != null)
            {
                Undo.DestroyObjectImmediate(record.BranchRoad);
            }

            if (activeSmoothRoad == record.BranchRoad)
            {
                activeSmoothRoad = null;
            }

            bool shouldRestoreSplitRoad = record.OriginalPath != null &&
                                          record.OriginalPath.Count >= 2 &&
                                          (record.FirstSplitRoad != null || record.SecondSplitRoad != null);

            if (record.FirstSplitRoad != null)
            {
                Undo.DestroyObjectImmediate(record.FirstSplitRoad);
            }

            if (record.SecondSplitRoad != null)
            {
                Undo.DestroyObjectImmediate(record.SecondSplitRoad);
            }

            branchRecords.Remove(record);
            bool shouldRemoveIntersectionVisual = !HasBranchAtJunction(record.Junction);

            if (shouldRemoveIntersectionVisual)
            {
                RemoveIntersectionVisual(record.IntersectionVisualName);
            }

            if (shouldRestoreSplitRoad)
            {
                smoothPath = new List<Vector3>(record.OriginalPath);
                activeSmoothRoad = null;
                selectedJunctionPointIndex = -1;
                RebuildSmoothRoad();
            }
            else
            {
                selectedJunctionPointIndex = -1;
            }
        }

        private bool HasBranchAtJunction(Vector3 junction)
        {
            for (int i = 0; i < branchRecords.Count; i++)
            {
                BranchRecord record = branchRecords[i];
                if (record != null && Vector3.Distance(record.Junction, junction) <= branchToggleDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveIntersectionVisual(string visualName)
        {
            if (string.IsNullOrEmpty(visualName))
            {
                return;
            }

            GameObject visual = GameObject.Find(visualName);
            if (visual != null)
            {
                Undo.DestroyObjectImmediate(visual);
            }
        }

        private string BuildIntersectionVisualName(Vector3 origin)
        {
            int x = Mathf.RoundToInt(origin.x * 10f);
            int z = Mathf.RoundToInt(origin.z * 10f);
            return $"Intersection_{x}_{z}";
        }

        private void ApplyIntersectionSquare(
            GameObject target,
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float size,
            Material material,
            string meshName,
            float yOffset)
        {
            float half = size * 0.5f;
            Mesh mesh = new Mesh { name = meshName };
            List<Vector3> vertices = new List<Vector3>(4)
            {
                AdjustRoadVertexToGround(center - right * half - forward * half, yOffset),
                AdjustRoadVertexToGround(center + right * half - forward * half, yOffset),
                AdjustRoadVertexToGround(center - right * half + forward * half, yOffset),
                AdjustRoadVertexToGround(center + right * half + forward * half, yOffset)
            };

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = EnsureComponent<MeshFilter>(target);
            MeshRenderer renderer = EnsureComponent<MeshRenderer>(target);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            target.isStatic = true;
        }

        private List<RoadBranchInfo> FilterUniqueBranches(List<RoadBranchInfo> branches)
        {
            if (branches == null || branches.Count == 0) return new List<RoadBranchInfo>();
            List<RoadBranchInfo> unique = new List<RoadBranchInfo>();
            for (int i = 0; i < branches.Count; i++)
            {
                Vector3 dir = branches[i].OutgoingDirection;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) continue;
                dir.Normalize();

                bool duplicate = false;
                for (int u = 0; u < unique.Count; u++)
                {
                    Vector3 uDir = unique[u].OutgoingDirection;
                    if (Vector3.Angle(dir, uDir) < 45f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    unique.Add(branches[i]);
                }
            }
            return unique;
        }

        private void CreateOrUpdateIntersectionVisual(Vector3 origin, Vector3 forward)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform roadsRoot = roadParent != null ? roadParent : EnsureRoadRoot(scene);
            Transform intersectionsRoot = EnsureChildRoot(roadsRoot, GeneratedIntersectionVisualsName);

            // Find ANY existing visual within roadWidth distance in intersectionsRoot
            GameObject visual = null;
            float findRadiusSqr = (roadWidth * 0.85f) * (roadWidth * 0.85f);
            List<GameObject> duplicateVisuals = new List<GameObject>();

            for (int i = 0; i < intersectionsRoot.childCount; i++)
            {
                Transform child = intersectionsRoot.GetChild(i);
                if (child == null) continue;

                Vector3 childPos = child.position;
                MeshFilter mf = child.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    childPos = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                }

                if (FlatDistanceSqr(childPos, origin) <= findRadiusSqr)
                {
                    if (visual == null)
                    {
                        visual = child.gameObject;
                    }
                    else
                    {
                        duplicateVisuals.Add(child.gameObject);
                    }
                }
            }

            for (int d = 0; d < duplicateVisuals.Count; d++)
            {
                Undo.DestroyObjectImmediate(duplicateVisuals[d]);
            }

            List<RoadBranchInfo> rawBranches = GetIntersectionBranches(origin);
            List<RoadBranchInfo> branches = FilterUniqueBranches(rawBranches);

            // If 2 or fewer connections, this is not a junction, delete visual completely
            if (branches.Count <= 2)
            {
                if (visual != null)
                {
                    Undo.DestroyObjectImmediate(visual);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            if (visual == null)
            {
                visual = new GameObject(BuildIntersectionVisualName(origin));
                Undo.RegisterCreatedObjectUndo(visual, "Create Intersection Visual");
                visual.transform.SetParent(intersectionsRoot, false);
            }
            else
            {
                visual.name = BuildIntersectionVisualName(origin);
            }

            visual.transform.position = Vector3.zero;

            if (branches.Count >= 4)
            {
                // Truly 4 or more distinct directions -> 4-Way Crossroad Square Intersection
                Build4WayCrossIntersection(visual, origin, branches, forward);
            }
            else // branches.Count == 3
            {
                // Exactly 3 connections -> 3-Way T-Junction: Direct connection, NO SQUARE, clean crosswalks at connections
                Build3WayTIntersection(visual, origin, branches);
            }

            EditorUtility.SetDirty(visual);
            EditorSceneManager.MarkSceneDirty(scene);
            RefreshConnectedRoadsAtIntersection(origin);
        }

        private void RefreshConnectedRoadsAtIntersection(Vector3 center)
        {
            float checkDistSqr = (connectionSnapDistance * 1.5f) * (connectionSnapDistance * 1.5f);
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad r = roads[i];
                if (r == null || !r.IsUsable) continue;

                if (FlatDistanceSqr(r.GetPoint(0f), center) <= checkDistSqr || FlatDistanceSqr(r.GetPoint(1f), center) <= checkDistSqr)
                {
                    RebuildRoadVisuals(r.gameObject, SampleRoadPath(r, 0f, 1f));
                }
            }
        }

        private void Build3WayTIntersection(GameObject visual, Vector3 origin, List<RoadBranchInfo> branches)
        {
            GameObject sidewalkBase = EnsureChild(visual.transform, "SidewalkBase");
            GameObject asphalt = EnsureChild(visual.transform, "Asphalt");
            GameObject crosswalks = EnsureChild(visual.transform, "Crosswalks");

            // Absolutely NO square asphalt and NO square sidewalk for 3-way junctions!
            // Roads meet directly at center with 0 trim.
            if (sidewalkBase != null)
            {
                MeshFilter sf = sidewalkBase.GetComponent<MeshFilter>();
                if (sf != null) sf.sharedMesh = null;
                sidewalkBase.SetActive(false);
            }
            if (asphalt != null)
            {
                MeshFilter af = asphalt.GetComponent<MeshFilter>();
                if (af != null) af.sharedMesh = null;
                asphalt.SetActive(false);
            }

            if (branches == null || branches.Count < 3)
            {
                crosswalks.SetActive(false);
                return;
            }

            float halfRoad = roadWidth * 0.5f;
            float crosswalkDist = halfRoad + 0.8f;

            // Crosswalks: place zebra stripes cleanly at the 3 road connection mouths
            Mesh cwMesh = new Mesh { name = "3Way T-Junction Crosswalks" };
            List<Vector3> cwVerts = new List<Vector3>();
            List<Vector2> cwUvs = new List<Vector2>();
            List<int> cwTris = new List<int>();

            for (int i = 0; i < branches.Count && i < 3; i++)
            {
                Vector3 dir = branches[i].OutgoingDirection;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) continue;
                dir.Normalize();

                Vector3 mouthCenter = origin + dir * crosswalkDist;
                Vector3 across = Vector3.Cross(Vector3.up, dir).normalized;
                AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, mouthCenter, dir, across);
            }

            cwMesh.SetVertices(cwVerts);
            cwMesh.SetUVs(0, cwUvs);
            cwMesh.SetTriangles(cwTris, 0);
            cwMesh.RecalculateNormals();
            cwMesh.RecalculateBounds();

            MeshFilter cwf = EnsureComponent<MeshFilter>(crosswalks);
            MeshRenderer cwr = EnsureComponent<MeshRenderer>(crosswalks);
            cwf.sharedMesh = cwMesh;
            cwr.sharedMaterial = laneMarkingMaterial != null ? laneMarkingMaterial : GetOrCreateLaneMarkingMaterial();
            crosswalks.SetActive(true);
        }

        private void Build4WayCrossIntersection(GameObject visual, Vector3 origin, List<RoadBranchInfo> branches, Vector3 fallbackForward)
        {
            Vector3 flatForward = fallbackForward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;

            GameObject sidewalkBase = EnsureChild(visual.transform, "SidewalkBase");
            GameObject asphalt = EnsureChild(visual.transform, "Asphalt");
            GameObject crosswalks = EnsureChild(visual.transform, "Crosswalks");

            ApplyIntersectionSquare(
                sidewalkBase,
                origin,
                flatForward,
                right,
                intersectionSize + intersectionSidewalkWidth * 2f,
                sidewalkMaterial != null ? sidewalkMaterial : roadMaterial,
                "Intersection Sidewalk Mesh",
                -0.01f);
            sidewalkBase.SetActive(true);

            ApplyIntersectionSquare(
                asphalt,
                origin,
                flatForward,
                right,
                intersectionSize,
                roadMaterial,
                "Intersection Asphalt Mesh",
                0.015f);
            asphalt.SetActive(true);

            Mesh cwMesh = new Mesh { name = "4Way Crossroad Crosswalks" };
            List<Vector3> cwVerts = new List<Vector3>();
            List<Vector2> cwUvs = new List<Vector2>();
            List<int> cwTris = new List<int>();

            float halfIntersection = intersectionSize * 0.5f;

            if (branches != null && branches.Count >= 4)
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    Vector3 dir = branches[i].OutgoingDirection;
                    Vector3 boundaryCenter = origin + dir * (halfIntersection - crosswalkStripeLength * 0.65f);
                    Vector3 across = Vector3.Cross(Vector3.up, dir).normalized;
                    AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, boundaryCenter, dir, across);
                }
            }
            else
            {
                AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, origin + flatForward * (halfIntersection - crosswalkStripeLength * 0.65f), flatForward, right);
                AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, origin - flatForward * (halfIntersection - crosswalkStripeLength * 0.65f), -flatForward, -right);
                AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, origin + right * (halfIntersection - crosswalkStripeLength * 0.65f), right, -flatForward);
                AppendCrosswalkAtBoundary(cwVerts, cwUvs, cwTris, origin - right * (halfIntersection - crosswalkStripeLength * 0.65f), -right, flatForward);
            }

            cwMesh.SetVertices(cwVerts);
            cwMesh.SetUVs(0, cwUvs);
            cwMesh.SetTriangles(cwTris, 0);
            cwMesh.RecalculateNormals();
            cwMesh.RecalculateBounds();

            MeshFilter cwf = EnsureComponent<MeshFilter>(crosswalks);
            MeshRenderer cwr = EnsureComponent<MeshRenderer>(crosswalks);
            cwf.sharedMesh = cwMesh;
            cwr.sharedMaterial = laneMarkingMaterial != null ? laneMarkingMaterial : GetOrCreateLaneMarkingMaterial();
            crosswalks.SetActive(true);
        }

        private void AppendCrosswalkAtBoundary(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 boundaryCenter,
            Vector3 alongRoadDir,
            Vector3 acrossRoadDir)
        {
            float totalStripeWidth = crosswalkStripeCount * crosswalkStripeWidth + (crosswalkStripeCount - 1) * crosswalkStripeGap;
            float startOffset = -totalStripeWidth * 0.5f + crosswalkStripeWidth * 0.5f;

            for (int i = 0; i < crosswalkStripeCount; i++)
            {
                float offset = startOffset + i * (crosswalkStripeWidth + crosswalkStripeGap);
                Vector3 stripeCenter = boundaryCenter + acrossRoadDir.normalized * offset;
                Vector3 halfAlong = alongRoadDir.normalized * (crosswalkStripeLength * 0.5f);
                Vector3 halfAcross = acrossRoadDir.normalized * (crosswalkStripeWidth * 0.5f);
                AppendIntersectionQuad(
                    vertices,
                    uvs,
                    triangles,
                    stripeCenter - halfAlong - halfAcross,
                    stripeCenter - halfAlong + halfAcross,
                    stripeCenter + halfAlong - halfAcross,
                    stripeCenter + halfAlong + halfAcross,
                    laneMarkingYOffset + 0.02f);
            }
        }

        private void ConnectTouchingRoadsInScene()
        {
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (roads == null || roads.Length < 2)
            {
                RebuildAllIntersections();
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Connect Touching Roads In Scene");

            float snapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 2.5f);
            float snapDistSqr = snapDist * snapDist;
            bool connectedAny = false;

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad r1 = roads[i];
                if (r1 == null || !r1.IsUsable) continue;

                Vector3[] endpoints = new Vector3[] { r1.GetPoint(0f), r1.GetPoint(1f) };
                for (int ep = 0; ep < 2; ep++)
                {
                    Vector3 endpoint = endpoints[ep];

                    // Skip if already at an intersection
                    if (TryFindNearbyIntersection(endpoint, out RoadIntersection _))
                    {
                        continue;
                    }

                    // Look for another road that this endpoint touches along its body
                    for (int j = 0; j < roads.Length; j++)
                    {
                        if (i == j) continue;
                        SplineRoad r2 = roads[j];
                        if (r2 == null || !r2.IsUsable) continue;

                        float len = r2.GetTotalLength();
                        int sampleCount = Mathf.Max(8, Mathf.CeilToInt(len / 1.5f));
                        float bestDistSqr = snapDistSqr;
                        float bestT = -1f;
                        Vector3 bestPoint = Vector3.zero;

                        for (int s = 0; s <= sampleCount; s++)
                        {
                            float t = s / (float)sampleCount;
                            Vector3 p = r2.GetPoint(t);
                            float dSqr = FlatDistanceSqr(endpoint, p);
                            if (dSqr < bestDistSqr)
                            {
                                bestDistSqr = dSqr;
                                bestT = t;
                                bestPoint = p;
                            }
                        }

                        if (bestT > 0.02f && bestT < 0.98f)
                        {
                            if (ep == 0)
                            {
                                r1.SetControlPoint(0, bestPoint);
                            }
                            else
                            {
                                r1.SetControlPoint(r1.ControlPointCount - 1, bestPoint);
                            }
                            EditorUtility.SetDirty(r1);

                            SplitExistingRoadAtConnection(r2, bestT, bestPoint);
                            connectedAny = true;
                            break;
                        }
                    }
                }
            }

            RebuildAllIntersections();
            if (connectedAny)
            {
                Debug.Log("[SimpleRoadPlacementTool] Temas eden yollar başarıyla kavşak ile bağlandı.");
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private void SimplifyAllDenseRoads()
        {
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int fixedCount = 0;
            for (int r = 0; r < roads.Length; r++)
            {
                SplineRoad road = roads[r];
                if (road == null || !road.IsUsable) continue;

                int cpCount = road.ControlPointCount;
                int anchorCount = (cpCount - 1) / 3 + 1;
                float length = road.GetTotalLength();

                // If road has more than 6 anchors and average distance between anchors is under 8 meters:
                if (anchorCount > 6 && (length / anchorCount) < 8f)
                {
                    int targetAnchors = Mathf.Max(3, Mathf.CeilToInt(length / 14f));
                    List<Vector3> simplifiedAnchors = new List<Vector3>(targetAnchors + 1);
                    for (int i = 0; i <= targetAnchors; i++)
                    {
                        float t = i / (float)targetAnchors;
                        simplifiedAnchors.Add(road.GetPoint(t));
                    }

                    Undo.RecordObject(road, "Simplify Dense Spline Road");
                    road.SetLinearPath(simplifiedAnchors);
                    EditorUtility.SetDirty(road);
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"[SimpleRoadPlacementTool] {fixedCount} adet aşırı noktalı yol optimize edildi, gereksiz noktalar temizlendi.");
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private void RebuildAllIntersections()
        {
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null) continue;
                Vector3 center = intersection.transform.position;
                Vector3 forward = Vector3.forward;
                if (intersection.connectedRoads != null && intersection.connectedRoads.Count > 0 && intersection.connectedRoads[0] != null)
                {
                    forward = intersection.connectedRoads[0].GetTangent(0f);
                }
                CreateOrUpdateIntersectionVisual(center, forward);
            }

            Transform roadsRoot = roadParent != null ? roadParent : EnsureRoadRoot(SceneManager.GetActiveScene());
            Transform visualsRoot = roadsRoot != null ? roadsRoot.Find(GeneratedIntersectionVisualsName) : null;
            if (visualsRoot != null)
            {
                for (int i = 0; i < visualsRoot.childCount; i++)
                {
                    Transform visual = visualsRoot.GetChild(i);
                    if (visual == null) continue;
                    MeshFilter mf = visual.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Vector3 center = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                        CreateOrUpdateIntersectionVisual(center, Vector3.forward);
                    }
                }
            }

            if (activeSmoothRoad != null && smoothPath.Count >= 2)
            {
                RebuildSmoothRoad();
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void RebuildAllRoadsInScene()
        {
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (roads == null || roads.Length == 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild All Roads In Scene");

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable) continue;
                RebuildRoadVisuals(road.gameObject, SampleRoadPath(road, 0f, 1f));
                EditorUtility.SetDirty(road.gameObject);
            }

            if (activeSmoothRoad != null && smoothPath.Count >= 2)
            {
                RebuildSmoothRoad();
            }

            RebuildAllIntersections();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
            Repaint();
        }

        private void AppendIntersectionQuad(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            float yOffset)
        {
            int index = vertices.Count;
            vertices.Add(AdjustRoadVertexToGround(a, yOffset));
            vertices.Add(AdjustRoadVertexToGround(b, yOffset));
            vertices.Add(AdjustRoadVertexToGround(c, yOffset));
            vertices.Add(AdjustRoadVertexToGround(d, yOffset));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 1);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private void LoadDefaultAssets()
        {
            createSideEdgeLines = false;
            snapToGround = true;
            groundOffset = Mathf.Max(groundOffset, 0.12f);
            if (sidewalkHeightOffset <= 0.035f)
            {
                sidewalkHeightOffset = 0.15f;
            }

            if (sidewalkBevelWidth > 0.5f)
            {
                sidewalkBevelWidth = 0.25f;
            }
            createSidewalkCollider = false;
            samplesPerSegment = Mathf.Max(samplesPerSegment, 12);
            roadCrossSegments = Mathf.Max(roadCrossSegments, 6);
            sidewalkCrossSegments = Mathf.Max(sidewalkCrossSegments, 2);
            createRoadNetworkLinks = true;
            connectToExistingRoads = true;
            autoCreateBridges = true;
            bridgeMinGroundClearance = Mathf.Max(bridgeMinGroundClearance, 2f);
            bridgeMinLength = Mathf.Max(bridgeMinLength, 8f);
            bridgeArchHeight = Mathf.Max(bridgeArchHeight, 3.5f);
            bridgePillarSpacing = Mathf.Max(bridgePillarSpacing, 28f);
            createBridgeTowers = true;
            createBridgeStructureCollider = true;
            bridgeTowerHeight = Mathf.Max(bridgeTowerHeight, 22f);
            bridgeTowerSpacing = Mathf.Max(bridgeTowerSpacing, 90f);

            if (roadPrefab == null)
            {
                roadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRoadPrefabPath);
                if (roadPrefab == null)
                {
                    roadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackRoadPrefabPath);
                }
            }

            if (roadMaterial == null || AssetDatabase.GetAssetPath(roadMaterial) == DefaultRoadMaterialPath)
            {
                roadMaterial = GetOrCreatePlainAsphaltMaterial();
            }

            if (sidewalkMaterial == null)
            {
                sidewalkMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultSidewalkMaterialPath);
            }

            if (laneMarkingMaterial == null)
            {
                laneMarkingMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedLaneMaterialPath);
            }
        }

        private Material GetOrCreateLaneMarkingMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedLaneMaterialPath);
            if (material != null)
            {
                laneMarkingMaterial = material;
                return material;
            }

            EnsureGeneratedMaterialFolder();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = "Generated_LaneMarking",
                color = Color.white
            };

            AssetDatabase.CreateAsset(material, GeneratedLaneMaterialPath);
            AssetDatabase.SaveAssets();
            laneMarkingMaterial = material;
            return material;
        }

        private void EnsurePlainAsphaltMaterial()
        {
            if (roadMaterial == null || AssetDatabase.GetAssetPath(roadMaterial) == DefaultRoadMaterialPath)
            {
                roadMaterial = GetOrCreatePlainAsphaltMaterial();
            }
        }

        private Material GetOrCreatePlainAsphaltMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAsphaltMaterialPath);
            if (material != null)
            {
                return material;
            }

            EnsureGeneratedMaterialFolder();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = "Generated_Asphalt",
                color = new Color(0.13f, 0.14f, 0.14f, 1f)
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", material.color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            AssetDatabase.CreateAsset(material, GeneratedAsphaltMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private void EnsureGeneratedMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
            {
                AssetDatabase.CreateFolder("Assets", "Art");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            }

            if (!AssetDatabase.IsValidFolder(GeneratedMaterialFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "RoadTools");
            }
        }
    }
}
