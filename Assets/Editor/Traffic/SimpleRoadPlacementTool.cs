using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public partial class SimpleRoadPlacementTool : EditorWindow
    {
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

        public enum RoadTargetCategory
        {
            AutoDetect,
            Ankara,
            Istanbul,
            HighwaysAndBridges,
            CustomParent
        }

        [SerializeField] private PlacementMode placementMode = PlacementMode.SmoothMesh;
        [SerializeField] private RoadTargetCategory targetCategory = RoadTargetCategory.AutoDetect;
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
        [SerializeField] private float laneMarkingYOffset = 0.015f;
        [SerializeField] private float crosswalkYOffset = 0.026f;
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
        [SerializeField] private bool createMeshCollider = true;
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

        private SplineRoad[] cachedSceneRoads;
        private RoadIntersection[] cachedSceneIntersections;
        private readonly List<GameObject> cachedOrphanRoads = new List<GameObject>();
        private double lastSceneCacheTime;

        private void EnsureSceneCache()
        {
            double now = EditorApplication.timeSinceStartup;
            if (cachedSceneRoads == null || cachedSceneIntersections == null || now - lastSceneCacheTime > 1.0)
            {
                cachedSceneRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                cachedSceneIntersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                cachedOrphanRoads.Clear();

                List<Transform> placedRoots = FindAllPlacedRoadsRoots();
                for (int pr = 0; pr < placedRoots.Count; pr++)
                {
                    Transform pRoot = placedRoots[pr];
                    if (pRoot == null) continue;
                    for (int i = 0; i < pRoot.childCount; i++)
                    {
                        Transform child = pRoot.GetChild(i);
                        if (child == null) continue;
                        if (child.name == GeneratedIntersectionsName || child.name == GeneratedIntersectionVisualsName) continue;
                        if (child.GetComponent<SplineRoad>() == null)
                        {
                            cachedOrphanRoads.Add(child.gameObject);
                        }
                    }
                }

                MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (allRenderers != null)
                {
                    for (int mr = 0; mr < allRenderers.Length; mr++)
                    {
                        MeshRenderer renderer = allRenderers[mr];
                        if (renderer == null) continue;
                        string rName = renderer.name;
                        if (rName.Contains("Asphalt") || rName.Contains("Sidewalk") || rName.StartsWith("SmoothRoad_") || rName.StartsWith("RoadSegment_"))
                        {
                            GameObject rootRoad = GetRoadRootObject(renderer.gameObject);
                            if (rootRoad != null && rootRoad.GetComponent<SplineRoad>() == null && rootRoad.GetComponent<RoadIntersection>() == null)
                            {
                                if (!cachedOrphanRoads.Contains(rootRoad))
                                {
                                    cachedOrphanRoads.Add(rootRoad);
                                }
                            }
                        }
                    }
                }

                lastSceneCacheTime = now;
            }
        }

        private void InvalidateSceneCache()
        {
            cachedSceneRoads = null;
            cachedSceneIntersections = null;
            cachedOrphanRoads.Clear();
        }

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

        [MenuItem("Tools/Delivery Driver/City/Rebuild All Intersections")]
        public static void RebuildAllIntersectionsMenu()
        {
            SimpleRoadPlacementTool window = GetWindow<SimpleRoadPlacementTool>("Road Placement");
            window.RebuildAllIntersections();
            Debug.Log("[SimpleRoadPlacementTool] All intersection visuals rebuilt successfully.");
        }

        [MenuItem("Tools/Delivery Driver/City/Fix Crosswalks & Rebuild Intersections", false, 25)]
        [MenuItem("Tools/Delivery Driver/Traffic/Fix Crosswalks & Rebuild Intersections", false, 25)]
        public static void FixCrosswalksAndRebuildIntersectionsMenu()
        {
            FixAllCrosswalksInSceneStatic();
        }

        [MenuItem("Tools/Delivery Driver/City/Simplify All Dense Roads in Scene")]
        public static void SimplifyAllDenseRoadsMenu()
        {
            int count = SimplifyAllDenseRoadsStatic(14f);
            Debug.Log($"[SimpleRoadPlacementTool] {count} adet aşırı noktalı yol optimize edildi, yeni yollar gibi hafifletildi.");
        }

        [MenuItem("Tools/Delivery Driver/City/Merge All 2-Way Road Joints & Fix Gaps", false, 24)]
        [MenuItem("Tools/Delivery Driver/Traffic/Merge All 2-Way Road Joints & Fix Gaps", false, 24)]
        public static void MergeAllTwoWayRoadsMenu()
        {
            int count = RoadMergerUtility.MergeAllTwoWayRoadsInScene();
            Debug.Log($"[SimpleRoadPlacementTool] {count} adet 2 yönlü yol bağlantısı başarıyla birleştirildi!");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
            Selection.selectionChanged -= OnSelectionChange;
            Selection.selectionChanged += OnSelectionChange;
            LoadDefaultAssets();
            InvalidateSceneCache();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Selection.selectionChanged -= OnSelectionChange;
            InvalidateSceneCache();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnHierarchyChange()
        {
            if (activeSmoothRoad == null && smoothPath.Count >= 2)
            {
                smoothPath.Clear();
                selectedJunctionPointIndex = -1;
                CleanOrphanIntersectionVisuals();
                SceneView.RepaintAll();
                Repaint();
            }
            InvalidateSceneCache();
        }

        private Vector2 scrollPos;

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.LabelField("Simple Road Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Smooth Mesh modu: Shift + Left Click ile noktalar ekle, yol tek parca mesh olarak yumusak baglanir. Prefab Tiles modu eski duz prefab doseme davranisidir.", MessageType.Info);

            toolEnabled = EditorGUILayout.Toggle("Tool Enabled", toolEnabled);
            placementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", placementMode);
            targetCategory = (RoadTargetCategory)EditorGUILayout.EnumPopup("Target Sector / City", targetCategory);
            if (targetCategory == RoadTargetCategory.CustomParent)
            {
                roadParent = (Transform)EditorGUILayout.ObjectField("Road Parent", roadParent, typeof(Transform), true);
            }
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
                if (GUILayout.Button("🏛️ Organize Hierarchy (World)"))
                {
                    CityHierarchyOrganizer.OrganizeHierarchyInScene();
                }

                if (GUILayout.Button("Load Defaults"))
                {
                    LoadDefaultAssets();
                    createSideEdgeLines = false;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🧹 Clean & Rebuild All Intersections"))
                {
                    CleanOrphanRoadIntersections();
                    CleanOrphanIntersectionVisuals();
                    RebuildAllIntersections();
                    InvalidateSceneCache();
                }

                GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
                if (GUILayout.Button("🧹 Bozuk & Yetim Yolları Temizle"))
                {
                    int cleaned = CleanOrphanRoadsAndMeshes();
                    CleanOrphanRoadIntersections();
                    CleanOrphanIntersectionVisuals();
                    InvalidateSceneCache();
                    SceneView.RepaintAll();
                    EditorUtility.DisplayDialog("Yol Temizliği", $"{cleaned} adet bozuk veya yetim yol objesi temizlendi.", "Tamam");
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
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
                laneMarkingYOffset = Mathf.Max(0.001f, EditorGUILayout.FloatField("Line Y Offset (Above Road)", laneMarkingYOffset));
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
            crosswalkYOffset = Mathf.Max(0.005f, EditorGUILayout.FloatField("Crosswalk Y Offset", crosswalkYOffset));

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

                if (GUILayout.Button("Fix Crosswalks"))
                {
                    FixAllCrosswalksInScene();
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
                SplineRoad selectedRoad = Selection.activeGameObject.GetComponentInParent<SplineRoad>();
                GameObject rootRoad = selectedRoad != null ? selectedRoad.gameObject : GetRoadRootObject(Selection.activeGameObject);
                if (rootRoad != null && IsPartOfAnyRoad(rootRoad.transform))
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Seçili Yol Objesi: {rootRoad.name}", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (selectedRoad != null && (activeSmoothRoad == null || activeSmoothRoad != rootRoad))
                    {
                        if (GUILayout.Button("Tool'a Yükle", GUILayout.Height(24)))
                        {
                            LoadRoadIntoTool(selectedRoad, selectStart: false);
                        }
                    }
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("Bu Yolu Sahneden Sil (Del)", GUILayout.Height(24)))
                    {
                        Undo.IncrementCurrentGroup();
                        Undo.SetCurrentGroupName("Delete Selected Road");
                        Undo.DestroyObjectImmediate(rootRoad);
                        if (activeSmoothRoad == rootRoad)
                        {
                            activeSmoothRoad = null;
                            smoothPath.Clear();
                        }
                        CleanOrphanIntersectionVisuals();
                        InvalidateSceneCache();
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
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
                if (road == null) continue;

                if (!road.IsUsable)
                {
                    Vector3 pt = road.transform.position;
                    if (road.ControlPointCount > 0) pt = road.GetControlPoint(0);
                    Vector2 guiPoint = HandleUtility.WorldToGUIPoint(pt);
                    float dist = Vector2.Distance(mousePosition, guiPoint);
                    if (dist < bestScreenDistance)
                    {
                        bestScreenDistance = dist;
                        bestPointIndex = -1;
                        bestRoad = road;
                    }
                    continue;
                }

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

                    // Also check samples along curve so clicking anywhere on the road selects it
                    float roadLen = road.GetTotalLength();
                    int sampleSteps = Mathf.Clamp(Mathf.RoundToInt(roadLen / 8f), 3, 25);
                    for (int s = 1; s < sampleSteps; s++)
                    {
                        float t = s / (float)sampleSteps;
                        Vector3 pt = road.GetPoint(t);
                        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(pt);
                        float dist = Vector2.Distance(mousePosition, guiPoint);
                        if (dist < bestScreenDistance)
                        {
                            bestScreenDistance = dist;
                            bestPointIndex = -1;
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
                else
                {
                    BuildProceduralIntersection(child.gameObject, visualCenter, branches);
                    EditorUtility.SetDirty(child.gameObject);
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

        private bool IsValidSnapCandidate(Vector3 candidate, Vector3 hoverOrClickPos)
        {
            if (smoothPath == null || smoothPath.Count == 0)
            {
                return true;
            }

            int cIndex = GetContinuationPointIndex();
            Vector3 origin = (cIndex >= 0 && cIndex < smoothPath.Count) ? smoothPath[cIndex] : smoothPath[smoothPath.Count - 1];

            // 1. Never snap back to the origin point we are currently extending from (within 2.5 meters)
            if (FlatDistanceSqr(candidate, origin) < 2.5f * 2.5f)
            {
                return false;
            }

            // 2. Vector representing where the user is trying to draw towards
            Vector3 userDelta = hoverOrClickPos - origin;
            userDelta.y = 0f;
            if (userDelta.sqrMagnitude < 0.01f)
            {
                return false;
            }
            Vector3 userDir = userDelta.normalized;

            Vector3 toCandidate = candidate - origin;
            toCandidate.y = 0f;

            // Candidate must not be behind the user's cursor direction
            if (Vector3.Dot(toCandidate.normalized, userDir) < 0.15f)
            {
                return false;
            }

            // 3. If road already has a heading (Count >= 2), candidate must not be behind current road direction
            if (smoothPath.Count >= 2)
            {
                int prevIndex = cIndex > 0 ? cIndex - 1 : 1;
                Vector3 roadHeading = smoothPath[cIndex] - smoothPath[prevIndex];
                roadHeading.y = 0f;
                if (roadHeading.sqrMagnitude > 0.01f)
                {
                    if (Vector3.Dot(toCandidate.normalized, roadHeading.normalized) < -0.1f)
                    {
                        return false;
                    }
                }
            }

            // 4. If road has only 1 point, do not snap to an intersection/endpoint within 10 meters (prevents snapping back to starting junction)
            if (smoothPath.Count == 1 && FlatDistanceSqr(candidate, origin) < 10f * 10f)
            {
                return false;
            }

            return true;
        }

        private bool TryFindNearbyRoadEndpoint(Vector3 point, out Vector3 closestEndpoint)
        {
            return TryFindNearbyRoadEndpoint(point, out closestEndpoint, out _, out _);
        }

        private bool TryFindNearbyRoadEndpoint(Vector3 point, out Vector3 closestEndpoint, out SplineRoad closestRoad, out bool isStart)
        {
            closestEndpoint = Vector3.zero;
            closestRoad = null;
            isStart = false;
            EnsureSceneCache();
            float dynamicSnapDist = Mathf.Clamp(connectionSnapDistance, 2.5f, 4.5f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;

            SplineRoad[] roads = cachedSceneRoads;
            if (roads == null) return false;

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad))
                {
                    continue;
                }

                Vector3 start = road.GetPoint(0f);
                Vector3 end = road.GetPoint(1f);

                if (IsValidSnapCandidate(start, point))
                {
                    float d0 = FlatDistanceSqr(point, start);
                    if (d0 < bestDistanceSqr)
                    {
                        bestDistanceSqr = d0;
                        closestEndpoint = start;
                        closestRoad = road;
                        isStart = true;
                    }
                }

                if (IsValidSnapCandidate(end, point))
                {
                    float d1 = FlatDistanceSqr(point, end);
                    if (d1 < bestDistanceSqr)
                    {
                        bestDistanceSqr = d1;
                        closestEndpoint = end;
                        closestRoad = road;
                        isStart = false;
                    }
                }
            }

            return closestRoad != null;
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

            if (hasHoverPoint && current.shift && !current.alt && connectToExistingRoads)
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

                GameObject picked = HandleUtility.PickGameObject(current.mousePosition, false);
                if (picked != null)
                {
                    SplineRoad pickedRoad = picked.GetComponentInParent<SplineRoad>();
                    if (pickedRoad != null)
                    {
                        LoadRoadIntoTool(pickedRoad);
                        handledSceneClick = true;
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    else if (IsPartOfAnyRoad(picked.transform))
                    {
                        GameObject rootRoad = GetRoadRootObject(picked);
                        if (rootRoad != null)
                        {
                            Selection.activeGameObject = rootRoad;
                            handledSceneClick = true;
                            current.Use();
                            Repaint();
                            SceneView.RepaintAll();
                            return;
                        }
                    }
                }
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.shift && !current.control && !current.alt && smoothPath.Count == 0)
            {
                GameObject picked = HandleUtility.PickGameObject(current.mousePosition, false);
                if (picked != null)
                {
                    SplineRoad pickedRoad = picked.GetComponentInParent<SplineRoad>();
                    if (pickedRoad != null)
                    {
                        LoadRoadIntoTool(pickedRoad);
                        handledSceneClick = true;
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    else if (IsPartOfAnyRoad(picked.transform))
                    {
                        GameObject rootRoad = GetRoadRootObject(picked);
                        if (rootRoad != null)
                        {
                            Selection.activeGameObject = rootRoad;
                            handledSceneClick = true;
                            current.Use();
                            Repaint();
                            SceneView.RepaintAll();
                            return;
                        }
                    }
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
                if (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl)
                {
                    SceneView.RepaintAll();
                }
                else if (current.keyCode == KeyCode.Escape)
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
                else if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
                {
                    if (selectedJunctionPointIndex >= 0 && selectedJunctionPointIndex < smoothPath.Count)
                    {
                        DeleteSelectedPoint();
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    else if (smoothPath.Count > 0 || activeSmoothRoad != null)
                    {
                        DeleteCurrentActiveRoad();
                        current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    else if (Selection.activeGameObject != null && IsPartOfAnyRoad(Selection.activeGameObject.transform))
                    {
                        GameObject rootRoad = GetRoadRootObject(Selection.activeGameObject);
                        if (rootRoad != null)
                        {
                            Undo.IncrementCurrentGroup();
                            Undo.SetCurrentGroupName("Delete Selected Road");
                            Undo.DestroyObjectImmediate(rootRoad);
                            CleanOrphanIntersectionVisuals();
                            InvalidateSceneCache();
                            current.Use();
                            Repaint();
                            SceneView.RepaintAll();
                            return;
                        }
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

            if (current.type == EventType.KeyUp)
            {
                if (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl)
                {
                    SceneView.RepaintAll();
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
            if (!isHoverSnappedToRoad)
            {
                point.y += roadYOffset;
            }

            if (placementMode == PlacementMode.SmoothMesh)
            {
                bool wasConnectingEndpoint = isHoverSnappedToRoad && smoothPath.Count >= 1;
                if (connectToExistingRoads)
                {
                    point = ResolveConnectionPoint(point);
                }

                ContinueSmoothRoad(point);

                if (wasConnectingEndpoint)
                {
                    RebuildAllIntersections();
                    StartNewSmoothRoad();
                    Debug.Log("[SimpleRoadPlacementTool] Yol başarıyla mevcut yola bağlandı ve tamamlandı.");
                }
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
                // Prevent doubling back / acute U-turn loop
                if (smoothPath.Count >= 2)
                {
                    Vector3 prevDir = smoothPath[smoothPath.Count - 1] - smoothPath[smoothPath.Count - 2];
                    prevDir.y = 0f;
                    Vector3 newDir = point - smoothPath[smoothPath.Count - 1];
                    newDir.y = 0f;
                    if (prevDir.sqrMagnitude > 0.01f && newDir.sqrMagnitude > 0.01f)
                    {
                        float dot = Vector3.Dot(prevDir.normalized, newDir.normalized);
                        if (dot < -0.65f && !Event.current.alt)
                        {
                            Debug.LogWarning("[SimpleRoadPlacementTool] Yol geriye doğru U dönüşü yapamaz (geriye katlanma engellendi). Geriye doğru çizmek istiyorsanız Alt tuşunu basılı tutun.");
                            return;
                        }
                    }
                }

                smoothPath.Add(point);
                selectedJunctionPointIndex = smoothPath.Count - 1;
                InvalidateSceneCache();
                if (smoothPath.Count >= 2)
                {
                    RebuildSmoothRoad();
                }

                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // Case B: Continuation point is at the start (index 0) - Reverse path so continuation point becomes the tail
            if (continuationIndex == 0)
            {
                smoothPath.Reverse();
                continuationIndex = smoothPath.Count - 1;

                if (smoothPath.Count >= 2)
                {
                    Vector3 prevDir = smoothPath[smoothPath.Count - 1] - smoothPath[smoothPath.Count - 2];
                    prevDir.y = 0f;
                    Vector3 newDir = point - smoothPath[smoothPath.Count - 1];
                    newDir.y = 0f;
                    if (prevDir.sqrMagnitude > 0.01f && newDir.sqrMagnitude > 0.01f)
                    {
                        float dot = Vector3.Dot(prevDir.normalized, newDir.normalized);
                        if (dot < -0.65f && !Event.current.alt)
                        {
                            Debug.LogWarning("[SimpleRoadPlacementTool] Yol geriye doğru U dönüşü yapamaz (geriye katlanma engellendi). Geriye doğru çizmek istiyorsanız Alt tuşunu basılı tutun.");
                            return;
                        }
                    }
                }

                smoothPath.Add(point);
                selectedJunctionPointIndex = smoothPath.Count - 1;
                InvalidateSceneCache();
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
                Vector3 tangent = road.GetTangent(t);
                if (t > 0.03f && t < 0.97f)
                {
                    SplitExistingRoadAtConnection(road, t, roadPoint);
                }
                else
                {
                    roadPoint = t <= 0.03f ? road.GetPoint(0f) : road.GetPoint(1f);
                    CreateOrUpdateIntersectionVisual(roadPoint, tangent);
                }

                return roadPoint;
            }

            return point;
        }

        private bool TryFindNearbyIntersection(Vector3 point, out RoadIntersection closest)
        {
            closest = null;
            EnsureSceneCache();
            float dynamicSnapDist = Mathf.Clamp(connectionSnapDistance, 2.5f, 4.5f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;
            RoadIntersection[] intersections = cachedSceneIntersections;
            if (intersections == null) return false;

            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null)
                {
                    continue;
                }

                Vector3 pos = intersection.transform.position;
                if (!IsValidSnapCandidate(pos, point))
                {
                    continue;
                }

                float distanceSqr = FlatDistanceSqr(point, pos);
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

            EnsureSceneCache();
            float dynamicSnapDist = Mathf.Clamp(connectionSnapDistance * 0.65f, 1.8f, 3.2f);
            float bestDistanceSqr = dynamicSnapDist * dynamicSnapDist;
            SplineRoad[] roads = cachedSceneRoads;
            if (roads == null) return false;

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad))
                {
                    continue;
                }

                // Broadphase bounding box check
                Vector3 p0 = road.GetPoint(0f);
                Vector3 p1 = road.GetPoint(1f);
                float minX = Mathf.Min(p0.x, p1.x) - dynamicSnapDist - 5f;
                float maxX = Mathf.Max(p0.x, p1.x) + dynamicSnapDist + 5f;
                float minZ = Mathf.Min(p0.z, p1.z) - dynamicSnapDist - 5f;
                float maxZ = Mathf.Max(p0.z, p1.z) + dynamicSnapDist + 5f;
                if (point.x < minX || point.x > maxX || point.z < minZ || point.z > maxZ)
                {
                    Vector3 mid = road.GetPoint(0.5f);
                    minX = Mathf.Min(minX, mid.x - dynamicSnapDist - 5f);
                    maxX = Mathf.Max(maxX, mid.x + dynamicSnapDist + 5f);
                    minZ = Mathf.Min(minZ, mid.z - dynamicSnapDist - 5f);
                    maxZ = Mathf.Max(maxZ, mid.z + dynamicSnapDist + 5f);
                    if (point.x < minX || point.x > maxX || point.z < minZ || point.z > maxZ)
                    {
                        continue;
                    }
                }

                float length = road.GetTotalLength();
                int sampleCount = Mathf.Max(10, Mathf.CeilToInt(length / 2.0f));
                float bestRoadT = -1f;
                float bestRoadDistSqr = bestDistanceSqr;

                for (int sample = 0; sample <= sampleCount; sample++)
                {
                    float t = sample / (float)sampleCount;
                    Vector3 candidate = road.GetPoint(t);
                    if (!IsValidSnapCandidate(candidate, point)) continue;

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
                    for (int iter = 0; iter < 5; iter++)
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
                    if (IsValidSnapCandidate(refinedPoint, point))
                    {
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

            bool capStart = GetEndpointTrimDistance(samples[0]) <= 0.01f;
            bool capEnd = GetEndpointTrimDistance(samples[samples.Count - 1]) <= 0.01f;

            if (visualSamples.Count >= 2)
            {
                ApplyStripMesh(asphalt, visualSamples, -roadWidth * 0.5f, roadWidth * 0.5f, roadMaterial, "Smooth Road Asphalt", capStart, capEnd);
                asphalt.SetActive(true);
            }
            else
            {
                asphalt.SetActive(false);
            }

            if (sidewalkWidth > 0.01f && visualSamples.Count >= 2)
            {
                ApplySidewalkMesh(sidewalks, visualSamples, capStart, capEnd);
                sidewalks.SetActive(true);
            }
            else
            {
                sidewalks.SetActive(false);
            }

            if (createLaneMarkings && visualSamples.Count >= 2)
            {
                ApplyLaneMarkingsMesh(laneMarkings, visualSamples, !capStart, !capEnd);
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
                splineRoad.sectorTag = ResolveCurrentTargetSectorName();
                splineRoad.SetSmoothPath(smoothPath.Count >= 2 ? smoothPath : samples);
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
            SplitRoadsPassingThroughPoint(point);
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

            bool capStart = GetEndpointTrimDistance(samples[0]) <= 0.01f;
            bool capEnd = GetEndpointTrimDistance(samples[samples.Count - 1]) <= 0.01f;

            if (visualSamples.Count >= 2)
            {
                Material mat = roadMaterial != null && !AssetDatabase.GetAssetPath(roadMaterial).Contains("LowPolyCity")
                    ? roadMaterial
                    : GetOrCreatePlainAsphaltMaterial();
                ApplyStripMesh(asphalt, visualSamples, -roadWidth * 0.5f, roadWidth * 0.5f, mat, "Smooth Road Asphalt", capStart, capEnd);
                asphalt.SetActive(true);
            }
            else
            {
                asphalt.SetActive(false);
            }

            if (sidewalkWidth > 0.01f && visualSamples.Count >= 2)
            {
                ApplySidewalkMesh(sidewalks, visualSamples, capStart, capEnd);
                sidewalks.SetActive(true);
            }
            else
            {
                sidewalks.SetActive(false);
            }

            if (createLaneMarkings && visualSamples.Count >= 2)
            {
                ApplyLaneMarkingsMesh(laneMarkings, visualSamples, !capStart, !capEnd);
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

        private void ApplySidewalkMesh(GameObject sidewalks, List<Vector3> samples, bool capStart = false, bool capEnd = false)
        {
            Mesh mesh = new Mesh { name = "Smooth Road Sidewalks Mesh" };
            int columnsPerSide = Mathf.Max(2, sidewalkCrossSegments + 2);
            List<Vector3> vertices = new List<Vector3>(samples.Count * columnsPerSide * 2 + 32);
            List<Vector2> uvs = new List<Vector2>(samples.Count * columnsPerSide * 2 + 32);
            List<int> triangles = new List<int>((samples.Count - 1) * (columnsPerSide - 1) * 12 + 48);
            float halfRoad = roadWidth * 0.5f;
            float bevelWidth = Mathf.Min(sidewalkBevelWidth, sidewalkWidth);

            AppendSidewalkSide(vertices, uvs, triangles, samples, -1f, halfRoad, bevelWidth, columnsPerSide, capStart, capEnd);
            AppendSidewalkSide(vertices, uvs, triangles, samples, 1f, halfRoad, bevelWidth, columnsPerSide, capStart, capEnd);

            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

        private void ApplyLaneMarkingsMesh(
            GameObject target,
            List<Vector3> samples,
            bool isStartAtIntersection = false,
            bool isEndAtIntersection = false)
        {
            Mesh mesh = new Mesh { name = "Smooth Road Lane Markings Mesh" };
            List<Vector3> vertices = new List<Vector3>(samples.Count * 8);
            List<Vector2> uvs = new List<Vector2>(samples.Count * 8);
            List<int> triangles = new List<int>(samples.Count * 12);
            List<float> cumulativeDistances = BuildCumulativeDistances(samples);

            if (!isStartAtIntersection && samples.Count > 0)
            {
                isStartAtIntersection = GetEndpointTrimDistance(samples[0]) > 0.01f;
            }
            if (!isEndAtIntersection && samples.Count > 0)
            {
                isEndAtIntersection = GetEndpointTrimDistance(samples[samples.Count - 1]) > 0.01f;
            }

            float crosswalkClearance = (crosswalkStripeLength * 0.5f) + 1.2f;
            float startMargin = isStartAtIntersection ? crosswalkClearance : 0f;
            float endMargin = isEndAtIntersection ? crosswalkClearance : 0f;

            AppendDashedLine(vertices, uvs, triangles, samples, cumulativeDistances, 0f, startMargin, endMargin);

            if (createSideEdgeLines)
            {
                float sideOffset = Mathf.Max(0f, roadWidth * 0.5f - laneMarkingWidth * 1.5f);
                AppendContinuousLine(vertices, uvs, triangles, samples, cumulativeDistances, -sideOffset, startMargin, endMargin);
                AppendContinuousLine(vertices, uvs, triangles, samples, cumulativeDistances, sideOffset, startMargin, endMargin);
            }

            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
            float lateralOffset,
            float startMargin = 0f,
            float endMargin = 0f)
        {
            if (samples.Count < 2 || cumulativeDistances.Count != samples.Count)
            {
                return;
            }

            float totalDistance = cumulativeDistances[cumulativeDistances.Count - 1];
            float repeatDistance = laneDashLength + laneDashGap;

            float effectiveStart = Mathf.Clamp(startMargin, 0f, totalDistance);
            float effectiveEnd = Mathf.Clamp(totalDistance - endMargin, effectiveStart, totalDistance);

            if (effectiveEnd - effectiveStart < laneDashLength)
            {
                return;
            }

            for (float dashStart = effectiveStart; dashStart < effectiveEnd; dashStart += repeatDistance)
            {
                float dashEnd = Mathf.Min(dashStart + laneDashLength, effectiveEnd);
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
            List<float> cumulativeDistances,
            float lateralOffset,
            float startMargin = 0f,
            float endMargin = 0f)
        {
            if (samples.Count < 2 || cumulativeDistances.Count != samples.Count)
            {
                return;
            }

            float totalDistance = cumulativeDistances[cumulativeDistances.Count - 1];
            float effectiveStart = Mathf.Clamp(startMargin, 0f, totalDistance);
            float effectiveEnd = Mathf.Clamp(totalDistance - endMargin, effectiveStart, totalDistance);

            if (effectiveEnd - effectiveStart < 0.5f)
            {
                return;
            }

            float step = 1.0f;
            Vector3 prev = SampleByDistance(samples, cumulativeDistances, effectiveStart);
            for (float d = effectiveStart + step; d < effectiveEnd; d += step)
            {
                Vector3 curr = SampleByDistance(samples, cumulativeDistances, d);
                AppendLineQuad(vertices, uvs, triangles, prev, curr, lateralOffset);
                prev = curr;
            }
            Vector3 finalPt = SampleByDistance(samples, cumulativeDistances, effectiveEnd);
            AppendLineQuad(vertices, uvs, triangles, prev, finalPt, lateralOffset);
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

            float lineY = roadYOffset + Mathf.Max(0.002f, laneMarkingYOffset);
            vertices.Add(AdjustRoadVertexToGround(start + offset - halfWidth, lineY, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(start + offset + halfWidth, lineY, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(end + offset - halfWidth, lineY, skipGroundSnap));
            vertices.Add(AdjustRoadVertexToGround(end + offset + halfWidth, lineY, skipGroundSnap));

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

        private void ApplyStripMesh(GameObject target, List<Vector3> samples, float leftOffset, float rightOffset, Material material, string meshName, bool capStart = false, bool capEnd = false)
        {
            Mesh mesh = new Mesh { name = meshName };
            List<Vector3> vertices = new List<Vector3>(samples.Count * 2 + 32);
            List<Vector2> uvs = new List<Vector2>(samples.Count * 2 + 32);
            List<int> triangles = new List<int>((samples.Count - 1) * 6 + 48);

            AppendStrip(vertices, uvs, triangles, samples, leftOffset, rightOffset, roadCrossSegments, roadYOffset, false, capStart, capEnd);

            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
            AppendStrip(vertices, uvs, triangles, samples, leftOffset, rightOffset, crossSegments, roadYOffset, false, false, false);
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
            bool keepRelativeToPath,
            bool capStart = false,
            bool capEnd = false)
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

            // Watertight End-Caps for Asphalt / Bridge Deck
            if (capStart && samples.Count >= 2)
            {
                AppendStripEndCap(vertices, uvs, triangles, samples, isBridgeList, startVertex, columns, 0, -1f, leftOffset, rightOffset, verticalOffset);
            }
            if (capEnd && samples.Count >= 2)
            {
                AppendStripEndCap(vertices, uvs, triangles, samples, isBridgeList, startVertex, columns, samples.Count - 1, 1f, leftOffset, rightOffset, verticalOffset);
            }
        }

        private void AppendStripEndCap(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<bool> isBridgeList,
            int startVertex,
            int columns,
            int sampleIndex,
            float directionSign,
            float leftOffset,
            float rightOffset,
            float verticalOffset)
        {
            Vector3 tangent = sampleIndex == 0 ? (samples[1] - samples[0]) : (samples[samples.Count - 1] - samples[samples.Count - 2]);
            tangent.y = 0f;
            Vector3 targetNormal = (directionSign * (tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward)).normalized;

            Vector3 right = GetPathRight(samples, sampleIndex);
            bool isBridge = isBridgeList[sampleIndex];
            int capStartVertex = vertices.Count;

            for (int col = 0; col < columns; col++)
            {
                int topIdx = startVertex + sampleIndex * columns + col;
                Vector3 topV = vertices[topIdx];
                float u = col / (float)(columns - 1);
                float offset = Mathf.Lerp(leftOffset, rightOffset, u);

                Vector3 botV = isBridge
                    ? topV - Vector3.up * Mathf.Max(bridgeDeckThickness, 0.45f)
                    : AdjustRoadVertexToGround(samples[sampleIndex] + right * offset, verticalOffset - 0.35f, false);

                vertices.Add(topV);
                vertices.Add(botV);
                uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 0f));
            }

            for (int col = 0; col < columns - 1; col++)
            {
                int topA = capStartVertex + col * 2;
                int botA = topA + 1;
                int topB = capStartVertex + (col + 1) * 2;
                int botB = topB + 1;

                Vector3 vTopA = vertices[topA];
                Vector3 vTopB = vertices[topB];
                Vector3 vBotA = vertices[botA];

                Vector3 faceNormal = Vector3.Cross(vTopB - vTopA, vBotA - vTopA);
                if (Vector3.Dot(faceNormal, targetNormal) > 0f)
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
            int columns,
            bool capStart = false,
            bool capEnd = false)
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

            // Watertight End-Caps for Sidewalks
            if (capStart && samples.Count >= 2)
            {
                AppendSidewalkEndCap(vertices, uvs, triangles, samples, isBridgeList, startVertex, skirtStartVertex, columns, 0, -1f, side, halfRoad);
            }
            if (capEnd && samples.Count >= 2)
            {
                AppendSidewalkEndCap(vertices, uvs, triangles, samples, isBridgeList, startVertex, skirtStartVertex, columns, samples.Count - 1, 1f, side, halfRoad);
            }
        }

        private void AppendSidewalkEndCap(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            List<Vector3> samples,
            List<bool> isBridgeList,
            int startVertex,
            int skirtStartVertex,
            int columns,
            int sampleIndex,
            float directionSign,
            float side,
            float halfRoad)
        {
            Vector3 tangent = sampleIndex == 0 ? (samples[1] - samples[0]) : (samples[samples.Count - 1] - samples[samples.Count - 2]);
            tangent.y = 0f;
            Vector3 targetNormal = (directionSign * (tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward)).normalized;

            Vector3 right = GetPathRight(samples, sampleIndex);
            bool isBridge = isBridgeList[sampleIndex];
            int capStartVertex = vertices.Count;

            // Inner bottom vertex (at road / deck level)
            int innerTopIdx = startVertex + sampleIndex * columns + 0;
            Vector3 innerTop = vertices[innerTopIdx];
            Vector3 innerBot = isBridge
                ? innerTop - Vector3.up * Mathf.Max(bridgeDeckThickness, 0.45f)
                : AdjustRoadVertexToGround(samples[sampleIndex] + right * (side * halfRoad), roadYOffset - 0.35f, false);

            // Outer bottom vertex (from skirt)
            int outerBotIdx = skirtStartVertex + sampleIndex;
            Vector3 outerBot = vertices[outerBotIdx];

            for (int col = 0; col < columns; col++)
            {
                int topIdx = startVertex + sampleIndex * columns + col;
                Vector3 topV = vertices[topIdx];
                float u = col / (float)(columns - 1);
                Vector3 botV = Vector3.Lerp(innerBot, outerBot, u);

                vertices.Add(topV);
                vertices.Add(botV);
                uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 0f));
            }

            for (int col = 0; col < columns - 1; col++)
            {
                int topA = capStartVertex + col * 2;
                int botA = topA + 1;
                int topB = capStartVertex + (col + 1) * 2;
                int botB = topB + 1;

                Vector3 vTopA = vertices[topA];
                Vector3 vTopB = vertices[topB];
                Vector3 vBotA = vertices[botA];

                Vector3 faceNormal = Vector3.Cross(vTopB - vTopA, vBotA - vTopA);
                if (Vector3.Dot(faceNormal, targetNormal) > 0f)
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

            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

        private bool CanCreateBridges()
        {
            if (!autoCreateBridges)
            {
                return false;
            }

            // City streets in Istanbul and Ankara must follow ground/terrain; no bridges inside cities
            if (targetCategory == RoadTargetCategory.Istanbul || targetCategory == RoadTargetCategory.Ankara)
            {
                return false;
            }

            return true;
        }

        private bool IsBridgeSample(Vector3 sample)
        {
            if (batchSeaArea.HasValue && batchSeaArea.Value.Contains(new Vector2(sample.x, sample.z))) return true;
            if (!CanCreateBridges())
            {
                return false;
            }

            float groundY = bridgeSupportBaseY;
            if (TryGetGroundHeight(sample, out float hitY))
            {
                groundY = hitY;
            }

            return sample.y - groundY >= Mathf.Max(bridgeMinGroundClearance, 2.5f);
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
            float spanLength = spanEndDistance - spanStartDistance;
            if (spanLength < 2f) return;

            float pillarSideOffset = Mathf.Max(bridgePillarWidth, roadWidth * 0.32f);

            // Abutment pillars at bridge start and end
            float abutmentOffset = Mathf.Min(1.2f, spanLength * 0.15f);
            float startPillarDist = spanStartDistance + abutmentOffset;
            float endPillarDist = spanEndDistance - abutmentOffset;

            // Start abutment pillar pair
            Vector3 startCenter = SampleByDistance(samples, distances, startPillarDist);
            Vector3 startForward = GetPathForwardAtDistance(samples, distances, startPillarDist);
            Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
            AppendBridgePillar(vertices, uvs, triangles, startCenter + startRight * -pillarSideOffset, startForward, startRight);
            AppendBridgePillar(vertices, uvs, triangles, startCenter + startRight * pillarSideOffset, startForward, startRight);

            // Intermediate pillar pairs evenly spaced
            float remainingDist = endPillarDist - startPillarDist;
            if (remainingDist > bridgePillarSpacing * 1.1f)
            {
                int intermediateCount = Mathf.FloorToInt(remainingDist / bridgePillarSpacing);
                float step = remainingDist / (intermediateCount + 1);
                for (int i = 1; i <= intermediateCount; i++)
                {
                    float distance = startPillarDist + i * step;
                    Vector3 center = SampleByDistance(samples, distances, distance);
                    Vector3 forward = GetPathForwardAtDistance(samples, distances, distance);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                    AppendBridgePillar(vertices, uvs, triangles, center + right * -pillarSideOffset, forward, right);
                    AppendBridgePillar(vertices, uvs, triangles, center + right * pillarSideOffset, forward, right);
                }
            }

            // End abutment pillar pair
            if (endPillarDist - startPillarDist > 2f)
            {
                Vector3 endCenter = SampleByDistance(samples, distances, endPillarDist);
                Vector3 endForward = GetPathForwardAtDistance(samples, distances, endPillarDist);
                Vector3 endRight = Vector3.Cross(Vector3.up, endForward).normalized;
                AppendBridgePillar(vertices, uvs, triangles, endCenter + endRight * -pillarSideOffset, endForward, endRight);
                AppendBridgePillar(vertices, uvs, triangles, endCenter + endRight * pillarSideOffset, endForward, endRight);
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
            if (TryGetGroundHeight(topCenter, out float groundHeight))
            {
                bottomY = groundHeight;
            }
            else if (TrySampleTerrainHeight(topCenter, out float terrainHeight))
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
                Vector3 p0 = i > 0 ? points[i - 1] : (points[0] * 2f - points[1]);
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = i + 2 < points.Count ? points[i + 2] : (points[points.Count - 1] * 2f - points[points.Count - 2]);

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

            if (CanCreateBridges() && samples.Count >= 2)
            {
                int startBridge = -1;
                int endBridge = -1;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector3 sample = samples[i];
                    float groundY = bridgeSupportBaseY;
                    bool hasGround = TryGetGroundHeight(sample, out groundY);

                    bool isOverGap = (!hasGround) || (sample.y - groundY >= Mathf.Max(bridgeMinGroundClearance, 2.5f));

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

            List<float> distances = BuildCumulativeDistances(samples);
            float totalDistance = distances[distances.Count - 1];
            float startTrim = GetEndpointTrimDistance(samples[0], totalDistance);
            float endTrim = GetEndpointTrimDistance(samples[samples.Count - 1], totalDistance);
            if (startTrim <= 0f && endTrim <= 0f)
            {
                return new List<Vector3>(samples);
            }

            if (totalDistance <= startTrim + endTrim + 1f)
            {
                float available = Mathf.Max(0.5f, totalDistance - 1f);
                float scale = available / Mathf.Max(0.01f, startTrim + endTrim);
                startTrim *= scale;
                endTrim *= scale;
            }

            float fromDistance = Mathf.Clamp(startTrim, 0f, totalDistance);
            float toDistance = Mathf.Clamp(totalDistance - endTrim, fromDistance + 0.2f, totalDistance);
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

        private float GetEndpointTrimDistance(Vector3 endpoint, float totalRoadDistance = 0f)
        {
            if (batchEndpointDegrees != null)
            {
                if (!batchEndpointDegrees.TryGetValue(endpoint, out int degree) || degree < 3) return 0f;
                float trim = Mathf.Max(intersectionRoadTrimDistance, roadWidth * 0.5f + sidewalkWidth + 0.5f);
                if (totalRoadDistance > 0.5f) trim = Mathf.Min(trim, totalRoadDistance * 0.45f);
                return Mathf.Max(0f, trim - 0.15f);
            }
            Vector3 intersectionPos;
            if (!TryFindNearbyIntersectionOrVisual(endpoint, out intersectionPos, out int branchCount))
            {
                return 0f;
            }

            if (branchCount >= 3)
            {
                float minTrim = roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 0f) + 0.5f;
                float trimDist = intersectionRoadTrimDistance > 0f ? Mathf.Max(minTrim, intersectionRoadTrimDistance) : minTrim;
                if (totalRoadDistance > 0.5f)
                {
                    trimDist = Mathf.Min(trimDist, totalRoadDistance * 0.45f);
                }
                // Allow a small 0.15m overlap so road penetrates slightly under intersection for 100% watertight seal
                return Mathf.Max(0f, trimDist - 0.15f);
            }

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
                bool hasP0 = FlatDistanceSqr(p0, center) <= radiusSqr;
                if (hasP0)
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
                bool hasP1 = FlatDistanceSqr(p1, center) <= radiusSqr;
                if (hasP1)
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

                // If neither p0 nor p1 is at center, check if this road passes through center in its interior
                if (!hasP0 && !hasP1)
                {
                    float totalLen = r.GetTotalLength();
                    int sampleSteps = Mathf.Max(8, Mathf.CeilToInt(totalLen / 1.5f));
                    float bestDistSqr = radiusSqr;
                    float bestT = -1f;

                    for (int s = 1; s < sampleSteps; s++)
                    {
                        float t = s / (float)sampleSteps;
                        Vector3 pt = r.GetPoint(t);
                        float dSqr = FlatDistanceSqr(pt, center);
                        if (dSqr < bestDistSqr)
                        {
                            bestDistSqr = dSqr;
                            bestT = t;
                        }
                    }

                    if (bestT > 0.04f && bestT < 0.96f)
                    {
                        Vector3 tangent = r.GetTangent(bestT);
                        tangent.y = 0f;
                        if (tangent.sqrMagnitude > 0.001f)
                        {
                            Vector3 dir = tangent.normalized;
                            Vector3 throughPt = r.GetPoint(bestT);
                            // Backward branch (towards p0)
                            branches.Add(new RoadBranchInfo { Road = r, IsStart = false, Endpoint = throughPt, OutgoingDirection = -dir });
                            // Forward branch (towards p1)
                            branches.Add(new RoadBranchInfo { Road = r, IsStart = true, Endpoint = throughPt, OutgoingDirection = dir });
                        }
                    }
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

            Transform systemsRoot = null;
            GameObject systemsGo = GameObject.Find("Systems");
            if (systemsGo != null)
            {
                systemsRoot = systemsGo.transform;
            }

            GameObject managerObject = new GameObject(GeneratedNetworkName);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create RoadNetworkManager");
            if (systemsRoot != null)
            {
                managerObject.transform.SetParent(systemsRoot, false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(managerObject, scene);
            }
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
                if (closest.connectedRoads == null)
                {
                    closest.connectedRoads = new List<SplineRoad>();
                }
                return closest;
            }

            GameObject intersectionObject = new GameObject($"RoadIntersection_{endpointName}_{System.DateTime.Now:HHmmssfff}");
            Undo.RegisterCreatedObjectUndo(intersectionObject, "Create Road Intersection");
            intersectionObject.transform.SetParent(intersectionsRoot, false);
            intersectionObject.transform.position = position;

            RoadIntersection created = Undo.AddComponent<RoadIntersection>(intersectionObject);
            created.isStopSignControlled = true;
            created.sectorTag = ResolveCurrentTargetSectorName();
            if (created.connectedRoads == null)
            {
                created.connectedRoads = new List<SplineRoad>();
            }
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
            CleanOrphanIntersectionVisuals();
            InvalidateSceneCache();
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

            InvalidateSceneCache();
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

        private void DeleteCurrentActiveRoad()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Delete Active Road");
            if (activeSmoothRoad != null)
            {
                Undo.DestroyObjectImmediate(activeSmoothRoad);
                activeSmoothRoad = null;
            }
            smoothPath.Clear();
            selectedJunctionPointIndex = -1;
            CleanOrphanIntersectionVisuals();
            InvalidateSceneCache();
            SceneView.RepaintAll();
            Repaint();
        }

        private void DeleteSelectedPoint()
        {
            if (selectedJunctionPointIndex < 0 || selectedJunctionPointIndex >= smoothPath.Count)
            {
                DeleteCurrentActiveRoad();
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Delete Selected Road Point");
            smoothPath.RemoveAt(selectedJunctionPointIndex);
            selectedJunctionPointIndex = -1;

            InvalidateSceneCache();
            if (smoothPath.Count >= 2 && activeSmoothRoad != null)
            {
                RebuildSmoothRoad();
            }
            else if (smoothPath.Count < 2 && activeSmoothRoad != null)
            {
                Undo.DestroyObjectImmediate(activeSmoothRoad);
                activeSmoothRoad = null;
            }
            else if (smoothPath.Count == 0)
            {
                StartNewSmoothRoad();
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
                splineRoad.sectorTag = ResolveCurrentTargetSectorName();
                splineRoad.SetLinearPath(new[] { start, end });
                EditorUtility.SetDirty(splineRoad);
            }

            EditorUtility.SetDirty(segmentObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private string ResolveCurrentTargetSectorName()
        {
            switch (targetCategory)
            {
                case RoadTargetCategory.Istanbul:
                    return "İstanbul";
                case RoadTargetCategory.HighwaysAndBridges:
                    return "Highways";
                case RoadTargetCategory.CustomParent:
                    if (roadParent != null)
                    {
                        return CityHierarchyOrganizer.ResolveSectorFromTransform(roadParent);
                    }
                    return "Ankara";
                case RoadTargetCategory.AutoDetect:
                default:
                    if (Selection.activeTransform != null)
                    {
                        return CityHierarchyOrganizer.ResolveSectorFromTransform(Selection.activeTransform);
                    }
                    return "Ankara";
            }
        }

        private Transform ResolveTargetCityFolder(Scene scene)
        {
            if (targetCategory == RoadTargetCategory.CustomParent && roadParent != null)
            {
                return roadParent;
            }

            string targetCityName = "Ankara";
            if (targetCategory == RoadTargetCategory.Istanbul) targetCityName = "İstanbul";
            else if (targetCategory == RoadTargetCategory.HighwaysAndBridges) targetCityName = "Highways & Bridges";
            else if (targetCategory == RoadTargetCategory.AutoDetect)
            {
                if (Selection.activeTransform != null)
                {
                    string selSector = CityHierarchyOrganizer.ResolveSectorFromTransform(Selection.activeTransform);
                    if (selSector == "İstanbul") targetCityName = "İstanbul";
                    else if (selSector == "Highways") targetCityName = "Highways & Bridges";
                    else targetCityName = "Ankara";
                }
                else
                {
                    targetCityName = "Ankara";
                }
            }

            GameObject world = GameObject.Find("World");
            Transform cityTr = null;
            if (world != null)
            {
                cityTr = world.transform.Find(targetCityName);
                if (cityTr == null && targetCityName == "İstanbul") cityTr = world.transform.Find("Istanbul");
            }
            if (cityTr == null)
            {
                cityTr = GameObject.Find(targetCityName)?.transform;
                if (cityTr == null && targetCityName == "İstanbul") cityTr = GameObject.Find("Istanbul")?.transform;
            }

            if (cityTr != null)
            {
                Transform placedRoads = cityTr.Find("Placed Roads");
                if (placedRoads == null)
                {
                    GameObject pr = new GameObject("Placed Roads");
                    Undo.RegisterCreatedObjectUndo(pr, "Create Placed Roads folder");
                    pr.transform.SetParent(cityTr, false);
                    placedRoads = pr.transform;
                }
                return placedRoads;
            }

            return null;
        }

        private Transform EnsureRoadRoot(Scene scene)
        {
            Transform resolved = ResolveTargetCityFolder(scene);
            if (resolved != null)
            {
                return resolved;
            }

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
                // If we are placing a highway/bridge over water or deep abyss (>= 3m drop)
                if (CanCreateBridges() && smoothPath != null && smoothPath.Count > 0)
                {
                    int cIndex = GetContinuationPointIndex();
                    float bridgeDeckY = (cIndex >= 0 && cIndex < smoothPath.Count) ? smoothPath[cIndex].y : smoothPath[smoothPath.Count - 1].y;

                    int waterLayer = LayerMask.NameToLayer("Water");
                    bool isWater = hit.collider.gameObject.name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || (waterLayer != -1 && hit.collider.gameObject.layer == waterLayer);

                    if (isWater || (bridgeDeckY - hit.point.y >= 3.0f))
                    {
                        Plane deckPlane = new Plane(Vector3.up, new Vector3(0f, bridgeDeckY, 0f));
                        if (deckPlane.Raycast(ray, out float deckDist))
                        {
                            point = ray.GetPoint(deckDist);
                            return true;
                        }
                    }
                }

                point = hit.point;
                return true;
            }

            // Fallback for void / horizon when drawing a bridge: maintain bridge deck height
            if (CanCreateBridges() && smoothPath != null && smoothPath.Count > 0)
            {
                int cIndex = GetContinuationPointIndex();
                float bridgeDeckY = (cIndex >= 0 && cIndex < smoothPath.Count) ? smoothPath[cIndex].y : smoothPath[smoothPath.Count - 1].y;
                Plane deckPlane = new Plane(Vector3.up, new Vector3(0f, bridgeDeckY, 0f));
                if (deckPlane.Raycast(ray, out float deckDist))
                {
                    point = ray.GetPoint(deckDist);
                    return true;
                }
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
            GUILayout.BeginArea(new Rect(12f, 12f, 530f, 96f), EditorStyles.helpBox);
            GUILayout.Label("Simple Road Placement", EditorStyles.boldLabel);

            if (placementMode == PlacementMode.SmoothMesh)
            {
                string statusText;
                if (activeSmoothRoad != null)
                {
                    statusText = $"Seçili Yol: '{activeSmoothRoad.name}' ({smoothPath.Count} nokta). Silmek için: [Yolu Sil (Del)] veya Del tuşu.";
                }
                else if (smoothPath.Count > 0)
                {
                    statusText = $"Yol çiziliyor ({smoothPath.Count} nokta). Nokta sil: Sağ Tık. Yolu sil: Del.";
                }
                else
                {
                    statusText = "Yeni Yol: Shift+Sol Tık. Yol Seç / Sil: Yola Ctrl+Sol Tık yapın ve 'Del' tuşuna basın.";
                }
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

                    if (smoothPath.Count > 0 && GUILayout.Button("Nokta Geri Al (Sağ Tık)", GUILayout.Height(22)))
                    {
                        UndoLastSmoothPoint();
                    }

                    if ((smoothPath.Count > 0 || activeSmoothRoad != null) && GUILayout.Button("Yolu Sil (Del)", GUILayout.Height(22)))
                    {
                        DeleteCurrentActiveRoad();
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

                bool canSelectPoint = !Event.current.shift && !Event.current.alt;
                if (canSelectPoint)
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
                    Handles.Label(smoothPath[i] + Vector3.up * (handleSize * 1.75f), $"[Devam Noktasi #{i}] (Sil: Del)", labelStyle);
                }
            }

            if (Event.current.control && !Event.current.alt)
            {
                DrawSceneRoadHandles(showIntermediatePoints: true);
            }
            else if (smoothPath.Count == 0 || !Event.current.shift)
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
                    Handles.Label(previewPoint + Vector3.up * 1.5f, "[YOLA KİLİTLENDİ (Shift+Tık ile Bağla)]", lockStyle);
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
                Handles.Label(hoverPoint + Vector3.up * 1.5f, "[YOLA KİLİTLENDİ (Shift+Tık ile Başla)]", lockStyle);
            }

            if (Event.current.shift && Event.current.control && smoothPath.Count >= 2)
            {
                DrawBranchHandles();
            }
        }

        private void DrawSceneRoadHandles(bool showIntermediatePoints)
        {
            EnsureSceneCache();
            SplineRoad[] sceneRoads = cachedSceneRoads;
            if (sceneRoads != null)
            {
                for (int r = 0; r < sceneRoads.Length; r++)
                {
                    SplineRoad road = sceneRoads[r];
                    if (road == null || (activeSmoothRoad != null && road.gameObject == activeSmoothRoad && smoothPath.Count > 0))
                    {
                        continue;
                    }

                    // If road is unusable or broken, draw an orange warning handle so it's visible and deletable!
                    if (!road.IsUsable)
                    {
                        Vector3 brokenPos = road.transform.position;
                        if (road.ControlPointCount > 0)
                        {
                            brokenPos = road.GetControlPoint(0);
                        }
                        float brokenSize = HandleUtility.GetHandleSize(brokenPos) * 0.22f;
                        Handles.color = new Color(1f, 0.3f, 0.1f, 0.95f);
                        if (Handles.Button(brokenPos, Quaternion.identity, brokenSize, brokenSize * 1.6f, Handles.SphereHandleCap))
                        {
                            Selection.activeGameObject = road.gameObject;
                            LoadRoadIntoTool(road);
                            handledSceneClick = true;
                            Event.current.Use();
                            Repaint();
                            SceneView.RepaintAll();
                            return;
                        }
                        GUIStyle brokenStyle = new GUIStyle(EditorStyles.boldLabel);
                        brokenStyle.normal.textColor = new Color(1f, 0.4f, 0.1f);
                        Handles.Label(brokenPos + Vector3.up * 1.2f, $"⚠️ [Bozuk Yol: {road.name} (Tıkla ve Sil)]", brokenStyle);
                        continue;
                    }

                    Vector3 startPoint = road.GetPoint(0f);
                    Vector3 endPoint = road.GetPoint(1f);

                    // Start endpoint (Cyan)
                    float startSize = HandleUtility.GetHandleSize(startPoint) * 0.18f;
                    Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                    if (Handles.Button(startPoint, Quaternion.identity, startSize, startSize * 1.5f, Handles.SphereHandleCap))
                    {
                        if (Event.current.control)
                        {
                            LoadRoadIntoTool(road, selectPointIndex: 0);
                        }
                        else
                        {
                            StartNewSmoothRoad();
                            AddSmoothPoint(startPoint);
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
                        if (Event.current.control)
                        {
                            LoadRoadIntoTool(road, selectPointIndex: -1);
                        }
                        else
                        {
                            StartNewSmoothRoad();
                            AddSmoothPoint(endPoint);
                        }
                        handledSceneClick = true;
                        Event.current.Use();
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }

                    // Points along road when holding Ctrl (showIntermediatePoints)
                    if (showIntermediatePoints)
                    {
                        int cpCount = road.ControlPointCount;
                        if (cpCount >= 4)
                        {
                            int anchorCount = (cpCount - 1) / 3 + 1;
                            if (anchorCount > 2)
                            {
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
                            else
                            {
                                // Straight 2-anchor road: sample points along the road so handles always appear when Ctrl is held!
                                float len = road.GetTotalLength();
                                int sampleSteps = Mathf.Clamp(Mathf.RoundToInt(len / 12f), 1, 6);
                                for (int s = 1; s <= sampleSteps; s++)
                                {
                                    float t = s / (float)(sampleSteps + 1);
                                    Vector3 pt = road.GetPoint(t);
                                    float size = HandleUtility.GetHandleSize(pt) * 0.16f;
                                    Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);

                                    if (Handles.Button(pt, Quaternion.identity, size, size * 1.5f, Handles.SphereHandleCap))
                                    {
                                        LoadRoadIntoTool(road, selectPointIndex: -1);
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
            }

            // Draw handles for all orphan road objects discovered in cache
            if (cachedOrphanRoads != null)
            {
                for (int o = 0; o < cachedOrphanRoads.Count; o++)
                {
                    GameObject orphan = cachedOrphanRoads[o];
                    if (orphan != null)
                    {
                        DrawOrphanRoadHandle(orphan);
                        if (handledSceneClick) return;
                    }
                }
            }
        }

        private void DrawOrphanRoadHandle(GameObject target)
        {
            if (target == null) return;
            Vector3 orphanPos = target.transform.position;
            MeshFilter mf = target.GetComponentInChildren<MeshFilter>();

            List<Vector3> samplePoints = new List<Vector3>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                samplePoints.Add(mf.transform.TransformPoint(new Vector3(b.min.x, b.center.y, b.min.z)));
                samplePoints.Add(mf.transform.TransformPoint(b.center));
                samplePoints.Add(mf.transform.TransformPoint(new Vector3(b.max.x, b.center.y, b.max.z)));
                orphanPos = mf.transform.TransformPoint(b.center);
            }
            else
            {
                samplePoints.Add(orphanPos);
            }

            for (int p = 0; p < samplePoints.Count; p++)
            {
                Vector3 pt = samplePoints[p];
                float ptSize = HandleUtility.GetHandleSize(pt) * 0.22f;
                Handles.color = new Color(1f, 0.45f, 0.05f, 0.95f);
                if (Handles.Button(pt, Quaternion.identity, ptSize, ptSize * 1.6f, Handles.SphereHandleCap))
                {
                    Selection.activeGameObject = target;
                    handledSceneClick = true;
                    Event.current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    return;
                }
            }

            GUIStyle orphanStyle = new GUIStyle(EditorStyles.boldLabel);
            orphanStyle.normal.textColor = new Color(1f, 0.55f, 0.1f);
            Handles.Label(orphanPos + Vector3.up * 1.5f, $"⚠️ [Yetim Yol: {target.name} (Tıkla ve Sil)]", orphanStyle);
        }

        private static List<Transform> FindAllPlacedRoadsRoots()
        {
            List<Transform> list = new List<Transform>();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return list;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                FindPlacedRoadsRecursive(roots[i].transform, list);
            }
            return list;
        }

        private static void FindPlacedRoadsRecursive(Transform tr, List<Transform> list)
        {
            if (tr == null) return;
            if (tr.name == DefaultRootName)
            {
                list.Add(tr);
                return;
            }
            for (int i = 0; i < tr.childCount; i++)
            {
                FindPlacedRoadsRecursive(tr.GetChild(i), list);
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

            if (road.ControlPointCount >= 4)
            {
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
            }

            if (selectPointIndex >= 0 && selectPointIndex < smoothPath.Count)
            {
                selectedJunctionPointIndex = selectPointIndex;
            }
            else
            {
                selectedJunctionPointIndex = smoothPath.Count > 0 ? smoothPath.Count - 1 : -1;
            }

            Selection.activeGameObject = road.gameObject;
            SceneView.RepaintAll();
            Repaint();
        }

        private void LoadRoadIntoTool(SplineRoad road, bool selectStart)
        {
            LoadRoadIntoTool(road, selectStart ? 0 : -1);
        }

        private GameObject GetRoadRootObject(GameObject obj)
        {
            if (obj == null) return null;
            SplineRoad spline = obj.GetComponentInParent<SplineRoad>();
            if (spline != null) return spline.gameObject;
            RoadIntersection intersection = obj.GetComponentInParent<RoadIntersection>();
            if (intersection != null) return intersection.gameObject;

            Transform current = obj.transform;
            while (current.parent != null && 
                   current.parent.name != DefaultRootName && 
                   current.parent.name != GeneratedIntersectionVisualsName &&
                   !current.parent.name.StartsWith("SmoothRoad_") &&
                   !current.parent.name.StartsWith("RoadSegment_"))
            {
                if (current.name.StartsWith("SmoothRoad_") || current.name.StartsWith("RoadSegment_"))
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return current.gameObject;
        }

        public static int CleanOrphanRoadsAndMeshes()
        {
            int cleanedCount = 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clean Orphan Road Objects");

            // 1. SplineRoads that are broken or have < 4 points or total length < 0.5f
            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRoads.Length; i++)
            {
                SplineRoad road = allRoads[i];
                if (road == null) continue;
                if (!road.IsUsable || road.ControlPointCount < 4 || road.GetTotalLength() < 0.5f)
                {
                    Undo.DestroyObjectImmediate(road.gameObject);
                    cleanedCount++;
                }
            }

            // 2. Orphan meshes under all Placed Roads folders without SplineRoad
            List<Transform> placedRoots = FindAllPlacedRoadsRoots();
            for (int pr = 0; pr < placedRoots.Count; pr++)
            {
                Transform pRoot = placedRoots[pr];
                if (pRoot == null) continue;
                for (int i = pRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = pRoot.GetChild(i);
                    if (child == null) continue;
                    if (child.name == GeneratedIntersectionsName || child.name == GeneratedIntersectionVisualsName) continue;
                    if (child.GetComponent<SplineRoad>() == null)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        cleanedCount++;
                    }
                }
            }

            // 3. Any GameObject anywhere in scene named "SmoothRoad_*" or "RoadSegment_*" without SplineRoad
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int i = 0; i < rootObjects.Length; i++)
                {
                    cleanedCount += CleanOrphanRoadObjectsRecursive(rootObjects[i].transform);
                }
            }

            return cleanedCount;
        }

        private static int CleanOrphanRoadObjectsRecursive(Transform parent)
        {
            if (parent == null) return 0;
            int count = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                count += CleanOrphanRoadObjectsRecursive(child);

                if (child.name.StartsWith("SmoothRoad_") || child.name.StartsWith("RoadSegment_"))
                {
                    if (child.GetComponent<SplineRoad>() == null)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        count++;
                    }
                }
            }
            return count;
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
            GameObject roadObject = road.gameObject;
            Vector3 roadTangent = road.GetTangent(t);
            Transform targetParent = roadObject.transform.parent != null ? roadObject.transform.parent : savedRoadParent;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Split Road For Connection");
            Undo.DestroyObjectImmediate(roadObject);

            roadParent = targetParent;
            CreateSmoothRoadFromPath(firstPath);
            CreateSmoothRoadFromPath(secondPath);

            roadParent = savedRoadParent;
            if (savedActiveRoad == roadObject)
            {
                smoothPath.Clear();
                activeSmoothRoad = null;
                selectedJunctionPointIndex = -1;
            }
            else
            {
                smoothPath = savedSmoothPath;
                activeSmoothRoad = savedActiveRoad;
                selectedJunctionPointIndex = savedSelectedIndex;
            }

            CreateOrUpdateIntersectionVisual(connectionPoint, roadTangent);
        }

        private List<Vector3> SampleRoadPath(SplineRoad road, float startT, float endT)
        {
            List<Vector3> path = new List<Vector3>();
            float totalLength = road.GetTotalLength();
            float startDistance = road.TToDistance(startT);
            float endDistance = road.TToDistance(endT);
            float range = Mathf.Abs(endDistance - startDistance);
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(range / 14.0f));

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

            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
                    if (Vector3.Angle(dir, uDir) < 18f)
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

        private bool SplitRoadsPassingThroughPoint(Vector3 point)
        {
            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allRoads == null || allRoads.Length == 0) return false;

            float splitSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 1.5f);
            float splitSnapDistSqr = splitSnapDist * splitSnapDist;
            bool splitAny = false;

            for (int r = 0; r < allRoads.Length; r++)
            {
                SplineRoad road = allRoads[r];
                if (road == null || !road.IsUsable || road.gameObject == null) continue;

                Vector3 p0 = road.GetPoint(0f);
                Vector3 p1 = road.GetPoint(1f);

                if (FlatDistanceSqr(p0, point) <= splitSnapDistSqr || FlatDistanceSqr(p1, point) <= splitSnapDistSqr)
                {
                    continue;
                }

                float length = road.GetTotalLength();
                int sampleCount = Mathf.Max(16, Mathf.CeilToInt(length / 0.5f));
                float bestT = -1f;
                float bestDistSqr = splitSnapDistSqr;

                for (int sample = 0; sample <= sampleCount; sample++)
                {
                    float t = sample / (float)sampleCount;
                    Vector3 candidate = road.GetPoint(t);
                    float dSqr = FlatDistanceSqr(point, candidate);
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        bestT = t;
                    }
                }

                if (bestT < 0f && road.ControlPointCount >= 7)
                {
                    for (int cp = 3; cp < road.ControlPointCount - 3; cp += 3)
                    {
                        Vector3 anchor = road.GetControlPoint(cp);
                        if (FlatDistanceSqr(anchor, point) <= splitSnapDistSqr)
                        {
                            bestT = cp / (float)(road.ControlPointCount - 1);
                            break;
                        }
                    }
                }

                if (bestT > 0.03f && bestT < 0.97f)
                {
                    SplitExistingRoadAtConnection(road, bestT, point);
                    splitAny = true;
                    break;
                }
            }

            return splitAny;
        }

        private void CreateOrUpdateIntersectionVisual(Vector3 origin, Vector3 forward)
        {
            SplitRoadsPassingThroughPoint(origin);

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

            // Always anchor origin to the exact mathematical center of the connected road endpoints
            Vector3 trueCenter = Vector3.zero;
            for (int b = 0; b < branches.Count; b++)
            {
                trueCenter += branches[b].Endpoint;
            }
            trueCenter /= branches.Count;
            origin = trueCenter;

            // Snap nearby RoadIntersection to the true center so runtime logic and Gizmos stay perfectly aligned
            RoadIntersection[] nearbyIntersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int ni = 0; ni < nearbyIntersections.Length; ni++)
            {
                if (nearbyIntersections[ni] != null && FlatDistanceSqr(nearbyIntersections[ni].transform.position, origin) <= findRadiusSqr)
                {
                    Undo.RecordObject(nearbyIntersections[ni].transform, "Snap Intersection Center");
                    nearbyIntersections[ni].transform.position = origin;
                }
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

            BuildProceduralIntersection(visual, origin, branches);

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

        private void BuildProceduralIntersection(GameObject visual, Vector3 origin, List<RoadBranchInfo> branches)
        {
            GameObject sidewalkBase = EnsureChild(visual.transform, "SidewalkBase");
            GameObject asphalt = EnsureChild(visual.transform, "Asphalt");
            GameObject crosswalks = EnsureChild(visual.transform, "Crosswalks");

            if (branches == null || branches.Count < 3)
            {
                if (sidewalkBase != null) sidewalkBase.SetActive(false);
                if (asphalt != null) asphalt.SetActive(false);
                if (crosswalks != null) crosswalks.SetActive(false);
                return;
            }

            // 1. Sort branches by angle around Y axis (counter-clockwise)
            List<RoadBranchInfo> sortedBranches = new List<RoadBranchInfo>(branches);
            sortedBranches.Sort((a, b) =>
            {
                Vector3 da = a.OutgoingDirection;
                da.y = 0f;
                Vector3 db = b.OutgoingDirection;
                db.y = 0f;
                float angleA = Mathf.Atan2(da.z, da.x);
                float angleB = Mathf.Atan2(db.z, db.x);
                return angleA.CompareTo(angleB);
            });

            int N = sortedBranches.Count;
            float halfRoad = roadWidth * 0.5f;
            float sw = sidewalkWidth > 0.01f ? sidewalkWidth : 0f;
            float defaultTrim = halfRoad + sw + 0.5f;
            float trimDist = intersectionRoadTrimDistance > 0f ? Mathf.Max(defaultTrim, intersectionRoadTrimDistance) : defaultTrim;

            Vector3[] dirs = new Vector3[N];
            Vector3[] rights = new Vector3[N];
            Vector3[] mouthCenters = new Vector3[N];
            Vector3[] mouthL = new Vector3[N];
            Vector3[] mouthR = new Vector3[N];
            Vector3[] swL = new Vector3[N];
            Vector3[] swR = new Vector3[N];

            for (int i = 0; i < N; i++)
            {
                Vector3 dir = sortedBranches[i].OutgoingDirection;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                dirs[i] = dir.normalized;
                rights[i] = Vector3.Cross(Vector3.up, dirs[i]).normalized;
                mouthCenters[i] = origin + dirs[i] * trimDist;

                SplineRoad branchRoad = sortedBranches[i].Road;
                if (branchRoad != null && branchRoad.IsUsable)
                {
                    bool isStart = sortedBranches[i].IsStart;
                    float totalLen = branchRoad.GetTotalLength();
                    float trim = Mathf.Min(trimDist, totalLen * 0.45f);
                    float targetDist = isStart ? trim : Mathf.Max(0.01f, totalLen - trim);
                    float t = branchRoad.DistanceToT(targetDist);
                    Vector3 roadPt = branchRoad.GetPoint(t);
                    Vector3 roadTan = branchRoad.GetTangent(t);
                    roadTan.y = 0f;
                    Vector3 roadOutDir = isStart ? roadTan : -roadTan;
                    if (roadOutDir.sqrMagnitude > 0.001f)
                    {
                        Vector3 normOutDir = roadOutDir.normalized;
                        Vector3 radial = roadPt - origin;
                        radial.y = 0f;
                        if (radial.sqrMagnitude > 0.01f)
                        {
                            radial.Normalize();
                            if (Vector3.Dot(normOutDir, radial) < 0.25f)
                            {
                                normOutDir = Vector3.Lerp(radial, normOutDir, 0.65f).normalized;
                            }
                        }

                        dirs[i] = normOutDir;
                        rights[i] = Vector3.Cross(Vector3.up, dirs[i]).normalized;
                        mouthCenters[i] = roadPt;
                    }
                }

                mouthL[i] = mouthCenters[i] - rights[i] * halfRoad;
                mouthR[i] = mouthCenters[i] + rights[i] * halfRoad;
                swL[i] = mouthCenters[i] - rights[i] * (halfRoad + sw);
                swR[i] = mouthCenters[i] + rights[i] * (halfRoad + sw);
            }

            // 2. Build Asphalt Perimeter Points & Corner Sidewalks
            List<Vector3> asphaltPerimeter = new List<Vector3>();
            List<(List<Vector3> inner, List<Vector3> outer)> cornerSidewalks = new List<(List<Vector3> inner, List<Vector3> outer)>();

            // For N == 3 (T-junction), find the single most opposite pair of branches that form the uninterrupted through-side
            int throughBranchIndex = -1;
            if (N == 3)
            {
                float minDot = 0f;
                for (int b = 0; b < N; b++)
                {
                    int nextB = (b + 1) % N;
                    float d = Vector3.Dot(dirs[b], dirs[nextB]);
                    if (d < minDot)
                    {
                        minDot = d;
                        if (d < -0.80f)
                        {
                            throughBranchIndex = b;
                        }
                    }
                }
            }

            for (int i = 0; i < N; i++)
            {
                int next = (i + 1) % N;
                // Add mouth points (from mouthR to mouthL in CCW order around origin)
                asphaltPerimeter.Add(mouthR[i]);
                asphaltPerimeter.Add(mouthL[i]);

                // Corner from branch i CCW edge (mouthL[i]) to branch next CW edge (mouthR[next])
                Vector3 startAsphalt = mouthL[i];
                Vector3 endAsphalt = mouthR[next];
                Vector3 startSW = swL[i];
                Vector3 endSW = swR[next];

                Vector3 dirI = dirs[i];
                Vector3 dirNext = dirs[next];

                List<Vector3> cornerInner = new List<Vector3>();
                List<Vector3> cornerOuter = new List<Vector3>();

                // Only the uninterrupted back side of a true T-junction (N == 3) should have a straight through-sidewalk
                bool isThroughSide = (i == throughBranchIndex);
                if (isThroughSide)
                {
                    int throughSteps = 3;
                    for (int s = 0; s <= throughSteps; s++)
                    {
                        float u = s / (float)throughSteps;
                        Vector3 innerPt = Vector3.Lerp(startAsphalt, endAsphalt, u);
                        Vector3 outerPt = Vector3.Lerp(startSW, endSW, u);
                        cornerInner.Add(innerPt);
                        cornerOuter.Add(outerPt);

                        if (s > 0 && s < throughSteps)
                        {
                            asphaltPerimeter.Add(innerPt);
                        }
                    }
                }
                else
                {
                    // Smooth, continuous circular/Hermite fillet corner with normal-offset sidewalk
                    Vector3 A = startAsphalt;
                    Vector3 B = endAsphalt;
                    Vector3 dA = dirI;
                    Vector3 dB = dirNext;
                    float D = Vector3.Distance(A, B);

                    if (D < 0.05f)
                    {
                        cornerInner.Add(A);
                        cornerOuter.Add(startSW);
                    }
                    else
                    {
                        float denom = dA.x * dB.z - dA.z * dB.x;
                        Vector3 Delta = A - B;
                        bool hasCleanApex = false;
                        float t = 0f, s = 0f;

                        if (Mathf.Abs(denom) > 0.05f)
                        {
                            t = (Delta.x * dB.z - Delta.z * dB.x) / denom;
                            s = (Delta.x * dA.z - Delta.z * dA.x) / denom;

                            if (t > 0.1f && s > 0.1f && t < D * 1.5f && s < D * 1.5f)
                            {
                                Vector3 testApex = A - dA * t;
                                float distApex = Vector3.Distance(testApex, origin);
                                float minAllowed = Mathf.Min(halfRoad * 0.6f, trimDist * 0.35f);
                                if (distApex >= minAllowed * 0.5f)
                                {
                                    hasCleanApex = true;
                                }
                            }
                        }

                        float kA, kB;
                        if (hasCleanApex)
                        {
                            float maxT = Mathf.Min(t, D * 0.85f);
                            float maxS = Mathf.Min(s, D * 0.85f);
                            kA = maxT * 0.552f;
                            kB = maxS * 0.552f;
                        }
                        else
                        {
                            float safeK = Mathf.Min(D * 0.35f, halfRoad * 0.85f);
                            kA = safeK;
                            kB = safeK;
                        }

                        Vector3 C0 = A;
                        Vector3 C1 = A - dA * kA;
                        Vector3 C2 = B - dB * kB;
                        Vector3 C3 = B;

                        int steps = 8;
                        for (int sIdx = 0; sIdx <= steps; sIdx++)
                        {
                            float u = sIdx / (float)steps;
                            float om = 1f - u;
                            float om2 = om * om;
                            float om3 = om2 * om;
                            float u2 = u * u;
                            float u3 = u2 * u;

                            Vector3 innerPt = om3 * C0 + 3f * om2 * u * C1 + 3f * om * u2 * C2 + u3 * C3;

                            Vector3 tangent = 3f * om2 * (C1 - C0) + 6f * om * u * (C2 - C1) + 3f * u2 * (C3 - C2);
                            tangent.y = 0f;
                            if (tangent.sqrMagnitude < 0.0001f)
                            {
                                tangent = B - A;
                                tangent.y = 0f;
                            }
                            tangent.Normalize();

                            Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;
                            Vector3 outerPt = innerPt + normal * sw;

                            // Ensure exact alignment at boundary ends with adjacent road sidewalks
                            if (sIdx == 0)
                            {
                                innerPt = startAsphalt;
                                outerPt = startSW;
                            }
                            else if (sIdx == steps)
                            {
                                innerPt = endAsphalt;
                                outerPt = endSW;
                            }

                            cornerInner.Add(innerPt);
                            cornerOuter.Add(outerPt);

                            if (sIdx > 0 && sIdx < steps)
                            {
                                asphaltPerimeter.Add(innerPt);
                            }
                        }
                    }
                }

                if (sw > 0.01f)
                {
                    cornerSidewalks.Add((cornerInner, cornerOuter));
                }
            }

            // 2. Resolve Materials from Procedural System
            Material aspMat = roadMaterial != null && !AssetDatabase.GetAssetPath(roadMaterial).Contains("LowPolyCity")
                ? roadMaterial
                : null;

            if (aspMat == null && branches != null)
            {
                for (int b = 0; b < branches.Count; b++)
                {
                    if (branches[b]?.Road != null)
                    {
                        Transform roadAsphalt = branches[b].Road.transform.Find("Asphalt");
                        if (roadAsphalt != null)
                        {
                            MeshRenderer mr = roadAsphalt.GetComponent<MeshRenderer>();
                            if (mr != null && mr.sharedMaterial != null)
                            {
                                aspMat = mr.sharedMaterial;
                                break;
                            }
                        }
                    }
                }
            }

            if (aspMat == null)
            {
                aspMat = GetOrCreatePlainAsphaltMaterial();
            }

            Material swMat = sidewalkMaterial != null
                ? sidewalkMaterial
                : null;

            if (swMat == null && branches != null)
            {
                for (int b = 0; b < branches.Count; b++)
                {
                    if (branches[b]?.Road != null)
                    {
                        Transform roadSw = branches[b].Road.transform.Find("Sidewalks");
                        if (roadSw != null)
                        {
                            MeshRenderer mr = roadSw.GetComponent<MeshRenderer>();
                            if (mr != null && mr.sharedMaterial != null)
                            {
                                swMat = mr.sharedMaterial;
                                break;
                            }
                        }
                    }
                }
            }

            if (swMat == null)
            {
                swMat = AssetDatabase.LoadAssetAtPath<Material>(DefaultSidewalkMaterialPath);
            }
            if (swMat == null) swMat = aspMat;

            // 3. Build Asphalt Mesh
            Mesh asphaltMesh = new Mesh { name = "Intersection Asphalt Mesh" };
            List<Vector3> aspVerts = new List<Vector3>(asphaltPerimeter.Count + 1);
            List<Vector2> aspUvs = new List<Vector2>(asphaltPerimeter.Count + 1);
            List<int> aspTris = new List<int>(asphaltPerimeter.Count * 3);

            float uvTile = Mathf.Max(1f, tileLength);
            Vector3 centerPos = AdjustRoadVertexToGround(origin, roadYOffset, false);
            aspVerts.Add(centerPos);
            aspUvs.Add(new Vector2(centerPos.x / uvTile, centerPos.z / uvTile));

            for (int p = 0; p < asphaltPerimeter.Count; p++)
            {
                Vector3 pos = AdjustRoadVertexToGround(asphaltPerimeter[p], roadYOffset, false);
                aspVerts.Add(pos);
                aspUvs.Add(new Vector2(pos.x / uvTile, pos.z / uvTile));
            }

            int perimeterCount = asphaltPerimeter.Count;
            for (int p = 0; p < perimeterCount; p++)
            {
                int curr = 1 + p;
                int nextP = 1 + ((p + 1) % perimeterCount);

                // Top face (normal pointing UP towards camera)
                aspTris.Add(0);
                aspTris.Add(nextP);
                aspTris.Add(curr);
            }

            asphaltMesh.SetVertices(aspVerts);
            asphaltMesh.SetUVs(0, aspUvs);
            asphaltMesh.SetTriangles(aspTris, 0);
            asphaltMesh.RecalculateNormals();
            asphaltMesh.RecalculateTangents();
            asphaltMesh.RecalculateBounds();

            MeshFilter af = EnsureComponent<MeshFilter>(asphalt);
            MeshRenderer ar = EnsureComponent<MeshRenderer>(asphalt);
            af.sharedMesh = asphaltMesh;
            ar.sharedMaterial = aspMat;
            asphalt.SetActive(true);

            MeshCollider ac = asphalt.GetComponent<MeshCollider>();
            if (createMeshCollider)
            {
                if (ac == null) ac = Undo.AddComponent<MeshCollider>(asphalt);
                ac.sharedMesh = asphaltMesh;
            }
            else if (ac != null)
            {
                Undo.DestroyObjectImmediate(ac);
            }

            // 4. Build Sidewalks Mesh
            if (sw > 0.01f && cornerSidewalks.Count > 0)
            {
                Mesh swMesh = new Mesh { name = "Intersection Sidewalks Mesh" };
                List<Vector3> swVerts = new List<Vector3>();
                List<Vector2> swUvs = new List<Vector2>();
                List<int> swTris = new List<int>();

                float swTopHeight = roadYOffset + sidewalkHeightOffset;

                foreach (var corner in cornerSidewalks)
                {
                    List<Vector3> inner = corner.inner;
                    List<Vector3> outer = corner.outer;
                    int count = inner.Count;

                    int startIndex = swVerts.Count;

                    // Top surface vertices
                    for (int s = 0; s < count; s++)
                    {
                        Vector3 inPos = AdjustRoadVertexToGround(inner[s], swTopHeight, false);
                        Vector3 outPos = AdjustRoadVertexToGround(outer[s], swTopHeight, false);

                        swVerts.Add(inPos);
                        swVerts.Add(outPos);

                        float v = s / (float)(count - 1);
                        swUvs.Add(new Vector2(0f, v));
                        swUvs.Add(new Vector2(1f, v));
                    }

                    // Top surface quads (normal UP)
                    for (int s = 0; s < count - 1; s++)
                    {
                        int row = startIndex + s * 2;
                        int nextRow = row + 2;

                        int inA = row;
                        int outA = row + 1;
                        int inB = nextRow;
                        int outB = nextRow + 1;

                        swTris.Add(inA);
                        swTris.Add(inB);
                        swTris.Add(outA);

                        swTris.Add(outA);
                        swTris.Add(inB);
                        swTris.Add(outB);
                    }

                    // Outer curb drop skirt
                    int skirtStart = swVerts.Count;
                    for (int s = 0; s < count; s++)
                    {
                        Vector3 topOuter = swVerts[startIndex + s * 2 + 1];
                        Vector3 bottomOuter = AdjustRoadVertexToGround(outer[s], roadYOffset - 0.35f, false);

                        swVerts.Add(topOuter);
                        swVerts.Add(bottomOuter);
                        swUvs.Add(new Vector2(0f, s / (float)(count - 1)));
                        swUvs.Add(new Vector2(1f, s / (float)(count - 1)));
                    }

                    for (int s = 0; s < count - 1; s++)
                    {
                        int row = skirtStart + s * 2;
                        int nextRow = row + 2;

                        // Outer face
                        swTris.Add(row);
                        swTris.Add(nextRow);
                        swTris.Add(row + 1);

                        swTris.Add(nextRow);
                        swTris.Add(nextRow + 1);
                        swTris.Add(row + 1);
                    }

                    // Inner curb step
                    int innerCurbStart = swVerts.Count;
                    for (int s = 0; s < count; s++)
                    {
                        Vector3 topInner = swVerts[startIndex + s * 2];
                        Vector3 bottomInner = AdjustRoadVertexToGround(inner[s], roadYOffset, false);

                        swVerts.Add(topInner);
                        swVerts.Add(bottomInner);
                        swUvs.Add(new Vector2(0f, s / (float)(count - 1)));
                        swUvs.Add(new Vector2(1f, s / (float)(count - 1)));
                    }

                    for (int s = 0; s < count - 1; s++)
                    {
                        int row = innerCurbStart + s * 2;
                        int nextRow = row + 2;

                        swTris.Add(row);
                        swTris.Add(row + 1);
                        swTris.Add(nextRow);

                        swTris.Add(nextRow);
                        swTris.Add(row + 1);
                        swTris.Add(nextRow + 1);
                    }
                }

                swMesh.SetVertices(swVerts);
                swMesh.SetUVs(0, swUvs);
                swMesh.SetTriangles(swTris, 0);
                swMesh.RecalculateNormals();
                swMesh.RecalculateTangents();
                swMesh.RecalculateBounds();

                MeshFilter sf = EnsureComponent<MeshFilter>(sidewalkBase);
                MeshRenderer sr = EnsureComponent<MeshRenderer>(sidewalkBase);
                sf.sharedMesh = swMesh;
                sr.sharedMaterial = swMat;
                sidewalkBase.SetActive(true);

                MeshCollider sc = sidewalkBase.GetComponent<MeshCollider>();
                if (createSidewalkCollider)
                {
                    if (sc == null) sc = Undo.AddComponent<MeshCollider>(sidewalkBase);
                    sc.sharedMesh = swMesh;
                }
                else if (sc != null)
                {
                    Undo.DestroyObjectImmediate(sc);
                }
            }
            else if (sidewalkBase != null)
            {
                sidewalkBase.SetActive(false);
            }

            // 5. Build Crosswalks
            Mesh cwMesh = new Mesh { name = "Intersection Crosswalks Mesh" };
            List<Vector3> cwVerts = new List<Vector3>();
            List<Vector2> cwUvs = new List<Vector2>();
            List<int> cwTris = new List<int>();

            for (int i = 0; i < N; i++)
            {
                Vector3 mouthCenter = mouthCenters[i];
                Vector3 dir = dirs[i];
                Vector3 across = rights[i];

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

        private void AppendCrosswalkAtBoundary(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 boundaryCenter,
            Vector3 alongRoadDir,
            Vector3 acrossRoadDir)
        {
            float usableWidth = Mathf.Max(2f, roadWidth - 1.0f);
            float width = Mathf.Max(0.15f, crosswalkStripeWidth);
            float gap = Mathf.Max(0.1f, crosswalkStripeGap);
            float pitch = width + gap;

            int count = Mathf.Max(3, Mathf.RoundToInt((usableWidth - width) / pitch) + 1);
            float actualPitch = count > 1 ? (usableWidth - width) / (count - 1) : 0f;
            float startOffset = -usableWidth * 0.5f + width * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float offset = startOffset + i * actualPitch;
                Vector3 stripeCenter = boundaryCenter + acrossRoadDir.normalized * offset;
                Vector3 halfAlong = alongRoadDir.normalized * (crosswalkStripeLength * 0.5f);
                Vector3 halfAcross = acrossRoadDir.normalized * (width * 0.5f);
                AppendIntersectionQuad(
                    vertices,
                    uvs,
                    triangles,
                    stripeCenter - halfAlong - halfAcross,
                    stripeCenter - halfAlong + halfAcross,
                    stripeCenter + halfAlong - halfAcross,
                    stripeCenter + halfAlong + halfAcross,
                    roadYOffset + Mathf.Max(crosswalkYOffset, laneMarkingYOffset + 0.012f));
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

        public static void RebuildSingleRoadVisual(SplineRoad road)
        {
            if (road == null || !road.IsUsable) return;

            SimpleRoadPlacementTool tool = ScriptableObject.CreateInstance<SimpleRoadPlacementTool>();
            try
            {
                tool.EnsurePlainAsphaltMaterial();
                tool.RebuildRoadVisuals(road.gameObject, tool.SampleRoadPath(road, 0f, 1f));
                EditorUtility.SetDirty(road.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }
        }

        public static int SimplifyAllDenseRoadsStatic(float targetSpacing = 14f)
        {
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (roads == null || roads.Length == 0) return 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Simplify All Dense Roads");

            int fixedCount = 0;
            SimpleRoadPlacementTool tool = ScriptableObject.CreateInstance<SimpleRoadPlacementTool>();

            try
            {
                tool.EnsurePlainAsphaltMaterial();

                for (int r = 0; r < roads.Length; r++)
                {
                    SplineRoad road = roads[r];
                    if (road == null || !road.IsUsable) continue;

                    int cpCount = road.ControlPointCount;
                    int anchorCount = (cpCount - 1) / 3 + 1;
                    float length = road.GetTotalLength();

                    bool isDense = (anchorCount > 5 && (length / anchorCount) < 9f) || cpCount > 24;
                    if (isDense)
                    {
                        Undo.RecordObject(road, "Simplify Spline Road");
                        bool simplified = road.Simplify(targetSpacing);
                        if (simplified)
                        {
                            EditorUtility.SetDirty(road);
                            fixedCount++;

                            if (road.transform.Find("Asphalt") != null || road.GetComponent<MeshFilter>() != null)
                            {
                                tool.RebuildRoadVisuals(road.gameObject, tool.SampleRoadPath(road, 0f, 1f));
                                EditorUtility.SetDirty(road.gameObject);
                            }
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                SceneView.RepaintAll();
            }

            return fixedCount;
        }

        private void SimplifyAllDenseRoads()
        {
            int fixedCount = SimplifyAllDenseRoadsStatic(14f);
            if (fixedCount > 0)
            {
                Debug.Log($"[SimpleRoadPlacementTool] {fixedCount} adet aşırı noktalı yol optimize edildi, gereksiz noktalar temizlendi.");
            }
            else
            {
                Debug.Log("[SimpleRoadPlacementTool] Sahnedeki tüm yollar zaten optimize durumda.");
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private void SplitRoadsPassingThroughIntersections()
        {
            List<Vector3> intersectionPositions = new List<Vector3>();

            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (intersections != null)
            {
                for (int i = 0; i < intersections.Length; i++)
                {
                    if (intersections[i] != null)
                    {
                        intersectionPositions.Add(intersections[i].transform.position);
                    }
                }
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform roadsRoot = roadParent != null ? roadParent : EnsureRoadRoot(scene);
            if (roadsRoot != null)
            {
                Transform visRoot = roadsRoot.Find(GeneratedIntersectionVisualsName);
                if (visRoot != null)
                {
                    for (int v = 0; v < visRoot.childCount; v++)
                    {
                        Transform child = visRoot.GetChild(v);
                        if (child == null) continue;
                        Vector3 cPos = child.position;
                        MeshFilter mf = child.GetComponentInChildren<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            cPos = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                        }
                        bool duplicate = false;
                        for (int p = 0; p < intersectionPositions.Count; p++)
                        {
                            if (FlatDistanceSqr(intersectionPositions[p], cPos) < 4f)
                            {
                                duplicate = true;
                                break;
                            }
                        }
                        if (!duplicate)
                        {
                            intersectionPositions.Add(cPos);
                        }
                    }
                }
            }

            if (intersectionPositions.Count == 0) return;

            float splitSnapDist = Mathf.Max(connectionSnapDistance, roadWidth * 0.5f + (sidewalkWidth > 0.01f ? sidewalkWidth : 2.2f) + 1.5f);
            float splitSnapDistSqr = splitSnapDist * splitSnapDist;

            bool splitAny = true;
            int maxIterations = 20;
            int iteration = 0;

            while (splitAny && iteration < maxIterations)
            {
                splitAny = false;
                iteration++;

                SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (allRoads == null || allRoads.Length == 0) break;

                for (int i = 0; i < intersectionPositions.Count; i++)
                {
                    Vector3 intPos = intersectionPositions[i];

                    for (int r = 0; r < allRoads.Length; r++)
                    {
                        SplineRoad road = allRoads[r];
                        if (road == null || !road.IsUsable || road.gameObject == null) continue;

                        Vector3 p0 = road.GetPoint(0f);
                        Vector3 p1 = road.GetPoint(1f);

                        // If road already starts or ends at this intersection, it is already a proper branch
                        if (FlatDistanceSqr(p0, intPos) <= splitSnapDistSqr || FlatDistanceSqr(p1, intPos) <= splitSnapDistSqr)
                        {
                            continue;
                        }

                        // Check if road passes near this intersection in its interior
                        float length = road.GetTotalLength();
                        int sampleCount = Mathf.Max(16, Mathf.CeilToInt(length / 0.5f));
                        float bestT = -1f;
                        float bestDistSqr = splitSnapDistSqr;

                        for (int sample = 0; sample <= sampleCount; sample++)
                        {
                            float t = sample / (float)sampleCount;
                            Vector3 candidate = road.GetPoint(t);
                            float dSqr = FlatDistanceSqr(intPos, candidate);
                            if (dSqr < bestDistSqr)
                            {
                                bestDistSqr = dSqr;
                                bestT = t;
                            }
                        }

                        // Also check internal anchor control points
                        if (bestT < 0f && road.ControlPointCount >= 7)
                        {
                            for (int cp = 3; cp < road.ControlPointCount - 3; cp += 3)
                            {
                                Vector3 anchor = road.GetControlPoint(cp);
                                if (FlatDistanceSqr(anchor, intPos) <= splitSnapDistSqr)
                                {
                                    bestT = cp / (float)(road.ControlPointCount - 1);
                                    break;
                                }
                            }
                        }

                        if (bestT > 0.03f && bestT < 0.97f)
                        {
                            SplitExistingRoadAtConnection(road, bestT, intPos);
                            splitAny = true;
                            break;
                        }
                    }

                    if (splitAny) break;
                }
            }
        }

        private void RebuildAllIntersections()
        {
            SplitRoadsPassingThroughIntersections();
            CleanOrphanRoadIntersections();
            CleanOrphanIntersectionVisuals();

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

            // Also check all SplineRoad endpoints to catch any junctions without a RoadIntersection component
            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allRoads != null && allRoads.Length > 0)
            {
                float clusterDistSqr = (roadWidth * 0.85f) * (roadWidth * 0.85f);
                List<Vector3> checkedPoints = new List<Vector3>();

                for (int r = 0; r < allRoads.Length; r++)
                {
                    SplineRoad road = allRoads[r];
                    if (road == null || !road.IsUsable || road.ControlPointCount < 2) continue;

                    Vector3 p0 = road.GetControlPoint(0);
                    Vector3 p1 = road.GetControlPoint(road.ControlPointCount - 1);

                    TryCheckJunctionCluster(p0, checkedPoints, clusterDistSqr);
                    TryCheckJunctionCluster(p1, checkedPoints, clusterDistSqr);
                }
            }

            if (activeSmoothRoad != null && smoothPath.Count >= 2)
            {
                RebuildSmoothRoad();
            }

            SceneView.RepaintAll();
            Repaint();
        }

        public static void FixAllCrosswalksInSceneStatic()
        {
            SimpleRoadPlacementTool tool = null;
            bool createdTemporary = false;
            if (HasOpenInstances<SimpleRoadPlacementTool>())
            {
                tool = GetWindow<SimpleRoadPlacementTool>("Road Placement", false);
            }
            if (tool == null)
            {
                tool = ScriptableObject.CreateInstance<SimpleRoadPlacementTool>();
                createdTemporary = true;
            }

            try
            {
                tool.LoadDefaultAssets();
                tool.FixAllCrosswalksInScene();
            }
            finally
            {
                if (createdTemporary && tool != null)
                {
                    DestroyImmediate(tool);
                }
            }
        }

        public void FixAllCrosswalksInScene()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Fix All Crosswalks In Scene");

            // 1. Ensure lane marking material exists and has Late Opaque queue (2100)
            Material mat = GetOrCreateLaneMarkingMaterial();
            if (mat != null)
            {
                mat.renderQueue = 2100;
                if (mat.HasProperty("_QueueOffset"))
                {
                    mat.SetFloat("_QueueOffset", 100f);
                }
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                laneMarkingMaterial = mat;
            }

            // 2. Ensure Y-offsets are adequate for grazing car camera angles
            crosswalkYOffset = Mathf.Max(0.026f, crosswalkYOffset);
            laneMarkingYOffset = Mathf.Max(0.015f, laneMarkingYOffset);

            // 3. Rebuild all roads and intersection geometry (updating lane markings and corner curbs)
            RebuildAllRoadsInScene();

            // 4. Double check all Crosswalks objects in scene to ensure visibility and material
            int fixedCount = 0;
            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                MeshRenderer mr = allRenderers[i];
                if (mr == null || mr.gameObject == null) continue;
                if (!mr.gameObject.scene.IsValid()) continue;

                string goName = mr.gameObject.name.ToLowerInvariant();
                bool isCrosswalkObj = goName.Contains("crosswalk");

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                bool isCrosswalkMesh = mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLowerInvariant().Contains("crosswalk");

                if (isCrosswalkObj || isCrosswalkMesh)
                {
                    Undo.RecordObject(mr.gameObject, "Fix Crosswalk Object");
                    if (!mr.gameObject.activeSelf)
                    {
                        mr.gameObject.SetActive(true);
                    }
                    if (!mr.enabled)
                    {
                        mr.enabled = true;
                    }

                    if (mat != null && (mr.sharedMaterial == null || mr.sharedMaterial.renderQueue < 2050))
                    {
                        mr.sharedMaterial = mat;
                    }

                    fixedCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
            Repaint();

            Debug.Log($"[SimpleRoadPlacementTool] Yaya geçitleri (Crosswalks) başarıyla güncellendi! {fixedCount} adet yaya geçidi objesi kontrol edildi, yükseklik 2.6cm'e çıkarıldı ve RenderQueue 2100 (Late Opaque) uygulandı.");
        }

        private void TryCheckJunctionCluster(Vector3 pt, List<Vector3> checkedPoints, float clusterDistSqr)
        {
            for (int i = 0; i < checkedPoints.Count; i++)
            {
                if (FlatDistanceSqr(pt, checkedPoints[i]) <= clusterDistSqr) return;
            }
            checkedPoints.Add(pt);

            List<RoadBranchInfo> rawBranches = GetIntersectionBranches(pt);
            List<RoadBranchInfo> branches = FilterUniqueBranches(rawBranches);
            if (branches.Count >= 3)
            {
                CreateOrUpdateIntersectionVisual(pt, Vector3.forward);
            }
        }

        private void RebuildAllRoadsInScene()
        {
            EnsurePlainAsphaltMaterial();
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (roads == null || roads.Length == 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild All Roads In Scene");

            for (int i = 0; i < roads.Length; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable) continue;

                if (road.ControlPointCount >= 4)
                {
                    List<Vector3> anchors = new List<Vector3>((road.ControlPointCount - 1) / 3 + 1);
                    for (int c = 0; c < road.ControlPointCount; c += 3)
                    {
                        anchors.Add(road.GetControlPoint(c));
                    }
                    road.SetSmoothPath(anchors);
                }

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

            if(initializingIsolatedBatch)return;
            roadMaterial = GetOrCreatePlainAsphaltMaterial();

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
                if (material.renderQueue < 2050)
                {
                    material.renderQueue = 2100;
                    if (material.HasProperty("_QueueOffset"))
                    {
                        material.SetFloat("_QueueOffset", 100f);
                    }
                    EditorUtility.SetDirty(material);
                }
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
                color = Color.white,
                renderQueue = 2100
            };
            if (material.HasProperty("_QueueOffset"))
            {
                material.SetFloat("_QueueOffset", 100f);
            }

            AssetDatabase.CreateAsset(material, GeneratedLaneMaterialPath);
            AssetDatabase.SaveAssets();
            laneMarkingMaterial = material;
            return material;
        }

        private void EnsurePlainAsphaltMaterial()
        {
            if (roadMaterial == null || AssetDatabase.GetAssetPath(roadMaterial).Contains("LowPolyCity"))
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
