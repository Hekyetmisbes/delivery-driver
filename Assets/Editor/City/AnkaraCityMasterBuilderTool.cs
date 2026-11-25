using System.Collections.Generic;
using DeliveryDriver.City;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    /// <summary>
    /// Master Editor Tool to construct Ankara city end-to-end using pre-existing scene districts.
    /// Places slope-aware roads, randomized side streets, roadside buildings, trees,
    /// and 2 major Istanbul suspension/highway bridge connections directly on top of the Terrain
    /// and inside existing District folders (Roads, Buildings, Trees).
    /// </summary>
    public class AnkaraCityMasterBuilderTool : EditorWindow
    {
        private const string AnkaraRootName = "Ankara";
        private const string CityRootName = "City";
        private const string DistrictsRootName = "Districts";
        private const string RoadsRootName = "Roads";
        private const string BridgesRootName = "Istanbul_Bridges";
        private const string BuildingsRootName = "Buildings";
        private const string TreesRootName = "Trees";

        private const string BuildingsFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Buildings";
        private const string NaturesFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Natures";
        private const string DefaultRoadMaterialPath = "Assets/Art/Materials/RoadTools/Generated_Asphalt.mat";

        [Header("General Settings")]
        [SerializeField] private Transform parentOverride;
        [SerializeField] private bool fitToTerrain = true;
        [SerializeField] private Vector3 cityOrigin = Vector3.zero;
        [SerializeField] private float layoutScale = 1.0f;
        [SerializeField] private int randomSeed = 4281;

        [Header("Road & Slope Configuration")]
        [SerializeField] private float mainRoadWidth = 8.5f;
        [SerializeField] private float sideStreetWidth = 6.0f;
        [SerializeField] private float sidewalkWidth = 2.2f;
        [SerializeField] private float sideStreetDensity = 0.45f;
        [SerializeField] private float maxSlopeAngle = 25f;

        [Header("Istanbul Bridges")]
        [SerializeField] private bool createIstanbulBridges = true;
        [SerializeField] private float bridgeSpanLength = 220f;
        [SerializeField] private float bridgeHeightAboveSea = 18f;
        [SerializeField] private float bridgeTowerHeight = 32f;
        [SerializeField] private float bridgeTowerWidth = 2.4f;

        [Header("Building & Prop Placement")]
        [SerializeField] private bool placeRoadsideBuildings = true;
        [SerializeField] private float buildingSetback = 1.2f;
        [SerializeField] private float lotGap = 0.2f;
        [SerializeField] private bool placeTrees = true;
        [SerializeField] private float treeChance = 0.5f;

        private readonly List<GameObject> buildingPrefabs = new List<GameObject>();
        private readonly List<GameObject> treePrefabs = new List<GameObject>();

        private struct DistrictSpec
        {
            public readonly string ObjectName;
            public readonly string DisplayName;
            public readonly Rect NormalizedArea;
            public readonly Color Color;
            public readonly string LandmarkName;

            public DistrictSpec(string objectName, string displayName, Rect normalizedArea, Color color, string landmarkName)
            {
                ObjectName = objectName;
                DisplayName = displayName;
                NormalizedArea = normalizedArea;
                Color = color;
                LandmarkName = landmarkName;
            }
        }

        private class ExistingDistrictData
        {
            public DistrictSpec Spec;
            public Transform DistrictRoot;
            public Transform RoadsFolder;
            public Transform BuildingsFolder;
            public Transform TreesFolder;
            public Vector3 Position;
        }

        private static readonly DistrictSpec[] Districts =
        {
            new DistrictSpec("District_OstimLiman", "Ostim Liman", new Rect(0f, 0f, 0.33f, 0.22f), new Color(0.95f, 0.55f, 0.18f, 0.35f), "Landmark_Port_Cranes"),
            new DistrictSpec("District_KizilayKordon", "Kizilay Kordon", new Rect(0.33f, 0f, 0.67f, 0.22f), new Color(0.15f, 0.75f, 1f, 0.35f), "Landmark_Coastal_Promenade"),
            new DistrictSpec("District_SihhiyeTransfer", "Sihhiye Aktarma", new Rect(0f, 0.22f, 1f, 0.16f), new Color(0.35f, 0.8f, 0.55f, 0.35f), "Landmark_Central_Junction"),
            new DistrictSpec("District_UlusKale", "Ulus Kale Kapilari", new Rect(0f, 0.38f, 0.5f, 0.2f), new Color(0.8f, 0.55f, 0.3f, 0.35f), "Landmark_Castle_Gates"),
            new DistrictSpec("District_HamamonuKaleici", "Hamamonu Kaleici", new Rect(0.5f, 0.38f, 0.5f, 0.2f), new Color(0.7f, 0.45f, 0.25f, 0.35f), "Landmark_Old_Town"),
            new DistrictSpec("District_AnittepePlato", "Anittepe Plato", new Rect(0f, 0.58f, 1f, 0.18f), new Color(0.95f, 0.9f, 0.55f, 0.35f), "Landmark_Monumental_Avenue"),
            new DistrictSpec("District_CankayaYamaci", "Cankaya Yamaci", new Rect(0f, 0.76f, 1f, 0.14f), new Color(0.45f, 0.75f, 0.35f, 0.35f), "Landmark_Embassy_Ridge"),
            new DistrictSpec("District_AtakuleZirve", "Atakule Zirve", new Rect(0f, 0.9f, 1f, 0.1f), new Color(0.6f, 0.45f, 1f, 0.35f), "Landmark_Atakule_Peak")
        };

        [MenuItem("Tools/Delivery Driver/City/Ankara Master City Builder")]
        public static void Open()
        {
            AnkaraCityMasterBuilderTool window = GetWindow<AnkaraCityMasterBuilderTool>("Ankara Master Builder");
            window.minSize = new Vector2(460f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPrefabs();
        }

        private void LoadPrefabs()
        {
            buildingPrefabs.Clear();
            treePrefabs.Clear();

            string[] buildingGuids = AssetDatabase.FindAssets("t:GameObject", new[] { BuildingsFolder });
            foreach (string guid in buildingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) buildingPrefabs.Add(go);
            }

            string[] natureGuids = AssetDatabase.FindAssets("t:GameObject", new[] { NaturesFolder });
            foreach (string guid in natureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.name.ToLower().Contains("tree")) treePrefabs.Add(go);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ankara Master City Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sahnede hazır olan Ankara mahallelerinin içine, Terrain yüzeyi üzerine " +
                "yol ağlarını, sokak aralarını, binaları ve İstanbul'a uzanan 2 mega köprü bağlantısını inşa eder.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            parentOverride = (Transform)EditorGUILayout.ObjectField("Parent Override", parentOverride, typeof(Transform), true);
            fitToTerrain = EditorGUILayout.Toggle("Fit To Terrain Bounds", fitToTerrain);
            cityOrigin = EditorGUILayout.Vector3Field("City Origin", cityOrigin);
            layoutScale = Mathf.Max(0.1f, EditorGUILayout.FloatField("Layout Scale", layoutScale));
            randomSeed = EditorGUILayout.IntField("Random Seed", randomSeed);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Road & Slope Configuration", EditorStyles.boldLabel);
            mainRoadWidth = Mathf.Max(3f, EditorGUILayout.FloatField("Main Road Width", mainRoadWidth));
            sideStreetWidth = Mathf.Max(2f, EditorGUILayout.FloatField("Side Street Width", sideStreetWidth));
            sidewalkWidth = Mathf.Max(0.5f, EditorGUILayout.FloatField("Sidewalk Width", sidewalkWidth));
            sideStreetDensity = Mathf.Clamp01(EditorGUILayout.FloatField("Side Street Density", sideStreetDensity));
            maxSlopeAngle = Mathf.Max(5f, EditorGUILayout.FloatField("Max Road Slope Angle", maxSlopeAngle));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Istanbul Bridges", EditorStyles.boldLabel);
            createIstanbulBridges = EditorGUILayout.Toggle("Create 2 Istanbul Bridges", createIstanbulBridges);
            if (createIstanbulBridges)
            {
                bridgeSpanLength = Mathf.Max(50f, EditorGUILayout.FloatField("Bridge Span Length", bridgeSpanLength));
                bridgeHeightAboveSea = EditorGUILayout.FloatField("Bridge Height Above Sea", bridgeHeightAboveSea);
                bridgeTowerHeight = Mathf.Max(10f, EditorGUILayout.FloatField("Bridge Tower Height", bridgeTowerHeight));
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Buildings & Trees", EditorStyles.boldLabel);
            placeRoadsideBuildings = EditorGUILayout.Toggle("Place Roadside Buildings", placeRoadsideBuildings);
            buildingSetback = Mathf.Max(0.1f, EditorGUILayout.FloatField("Building Setback", buildingSetback));
            placeTrees = EditorGUILayout.Toggle("Place Trees", placeTrees);
            treeChance = Mathf.Clamp01(EditorGUILayout.FloatField("Tree Chance", treeChance));

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("BUILD ROADS & BUILDINGS IN EXISTING DISTRICTS", GUILayout.Height(42f)))
            {
                BuildFullAnkaraCity();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Clear Generated Roads & Buildings", GUILayout.Height(24f)))
            {
                ClearGeneratedCity();
            }
        }

        private void BuildFullAnkaraCity()
        {
            Random.InitState(randomSeed);
            Scene scene = SceneManager.GetActiveScene();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Ankara Roads and Buildings");

            Transform ankaraRoot = parentOverride != null ? parentOverride : EnsureRoot(scene, AnkaraRootName);
            Transform districtsRoot = EnsureChild(ankaraRoot, DistrictsRootName);
            Transform bridgesRoot = EnsureChild(ankaraRoot, BridgesRootName);

            Bounds layoutBounds = ResolveLayoutBounds(scene, ankaraRoot);

            // Step 1: Discover / Resolve Existing Districts in Scene
            EnsureNeighborhoodManager(ankaraRoot);
            int neighborhoodLayer = LayerMask.NameToLayer("Neighborhood");
            List<ExistingDistrictData> districtDataList = ResolveExistingDistricts(districtsRoot, neighborhoodLayer, layoutBounds);

            List<Vector3> districtCenters = new List<Vector3>();
            foreach (var d in districtDataList)
            {
                districtCenters.Add(d.Position);
            }

            // Step 2: Build Main Road Network & Randomized Side Streets Inside District Folders
            List<List<Vector3>> roadPaths = BuildRoadNetworkPaths(districtCenters, layoutBounds);
            List<(SplineRoad spline, ExistingDistrictData parentDistrict)> generatedSplineRoads = CreateRoadsFromPaths(roadPaths, districtDataList, ankaraRoot);

            // Step 3: Build 2 Istanbul Suspension Bridges
            List<SplineRoad> allRoads = new List<SplineRoad>();
            foreach (var item in generatedSplineRoads) allRoads.Add(item.spline);

            if (createIstanbulBridges)
            {
                List<SplineRoad> bridgeSplines = CreateIstanbulBridges(bridgesRoot, layoutBounds);
                allRoads.AddRange(bridgeSplines);
            }

            // Step 4: Populate Roadside Buildings inside District "Buildings" folders on Terrain Surface
            if (placeRoadsideBuildings && buildingPrefabs.Count > 0)
            {
                PopulateRoadsideBuildings(generatedSplineRoads, ankaraRoot);
            }

            // Step 5: Populate Nature / Trees inside District "Trees" folders on Terrain Surface
            if (placeTrees && treePrefabs.Count > 0)
            {
                PopulateTrees(generatedSplineRoads, ankaraRoot);
            }

            // Step 6: Sync Road Network Manager
            SyncRoadNetworkManager(ankaraRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[AnkaraCityMasterBuilderTool] Successfully built Ankara roads & buildings across {districtDataList.Count} existing districts!");
        }

        private List<ExistingDistrictData> ResolveExistingDistricts(Transform districtsRoot, int neighborhoodLayer, Bounds layoutBounds)
        {
            List<ExistingDistrictData> list = new List<ExistingDistrictData>();

            for (int i = 0; i < Districts.Length; i++)
            {
                DistrictSpec spec = Districts[i];
                Transform dRoot = districtsRoot.Find(spec.ObjectName);

                if (dRoot == null)
                {
                    // Search anywhere under districtsRoot or scene by display name
                    NeighborhoodZone zoneInScene = Object.FindFirstObjectByType<NeighborhoodZone>(FindObjectsInactive.Include);
                    if (zoneInScene != null && zoneInScene.NeighborhoodName == spec.DisplayName)
                    {
                        dRoot = zoneInScene.transform.parent;
                    }
                }

                if (dRoot == null)
                {
                    dRoot = EnsureChild(districtsRoot, spec.ObjectName);
                    Rect area = ExpandNormalizedArea(spec.NormalizedArea, layoutBounds);
                    Vector3 worldCenter = NormalizedAreaToWorldCenter(area, layoutBounds);
                    worldCenter.y = GetGroundHeight(worldCenter);
                    dRoot.position = worldCenter;
                }

                Vector3 dPos = dRoot.position;
                dPos.y = GetGroundHeight(dPos);
                dRoot.position = dPos;

                ExistingDistrictData data = new ExistingDistrictData
                {
                    Spec = spec,
                    DistrictRoot = dRoot,
                    RoadsFolder = EnsureChild(dRoot, RoadsRootName),
                    BuildingsFolder = EnsureChild(dRoot, BuildingsRootName),
                    TreesFolder = EnsureChild(dRoot, TreesRootName),
                    Position = dPos
                };

                // Ensure Zone Trigger exists
                Transform zoneTrigger = EnsureChild(dRoot, "ZoneTrigger");
                zoneTrigger.localPosition = Vector3.zero;
                if (neighborhoodLayer >= 0) zoneTrigger.gameObject.layer = neighborhoodLayer;

                BoxCollider box = zoneTrigger.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(zoneTrigger.gameObject);
                box.isTrigger = true;
                box.center = Vector3.up * 90f;
                box.size = new Vector3(300f, 180f, 300f);

                NeighborhoodZone zone = zoneTrigger.GetComponent<NeighborhoodZone>();
                if (zone == null) zone = Undo.AddComponent<NeighborhoodZone>(zoneTrigger.gameObject);
                zone.NeighborhoodName = spec.DisplayName;
                zone.DebugColor = spec.Color;

                EditorUtility.SetDirty(zone);
                EditorUtility.SetDirty(box);

                list.Add(data);
            }

            return list;
        }

        private List<List<Vector3>> BuildRoadNetworkPaths(List<Vector3> centers, Bounds layoutBounds)
        {
            List<List<Vector3>> paths = new List<List<Vector3>>();

            // Main Avenues connecting consecutive district centers in a ring + cross avenues
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 start = centers[i];
                Vector3 end = centers[(i + 1) % centers.Count];

                List<Vector3> path = SampleSlopeAdaptedPath(start, end);
                if (path.Count >= 2) paths.Add(path);
            }

            // Cross Avenues connecting central hubs
            if (centers.Count >= 8)
            {
                paths.Add(SampleSlopeAdaptedPath(centers[2], centers[1])); // Sihhiye -> Kizilay
                paths.Add(SampleSlopeAdaptedPath(centers[2], centers[3])); // Sihhiye -> Ulus
                paths.Add(SampleSlopeAdaptedPath(centers[5], centers[6])); // Anittepe -> Cankaya
                paths.Add(SampleSlopeAdaptedPath(centers[6], centers[7])); // Cankaya -> Atakule (Up the slope!)
            }

            // Randomized Side Streets (Sokak Araları)
            int sideStreetCount = Mathf.RoundToInt(centers.Count * 3 * sideStreetDensity);
            for (int i = 0; i < sideStreetCount; i++)
            {
                int cIdx = Random.Range(0, centers.Count);
                Vector3 origin = centers[cIdx];
                Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(40f, 150f);
                Vector3 sideTarget = origin + new Vector3(randomOffset.x, 0f, randomOffset.y);
                sideTarget.y = GetGroundHeight(sideTarget);

                List<Vector3> sidePath = SampleSlopeAdaptedPath(origin, sideTarget);
                if (sidePath.Count >= 2) paths.Add(sidePath);
            }

            return paths;
        }

        private List<Vector3> SampleSlopeAdaptedPath(Vector3 start, Vector3 end)
        {
            List<Vector3> points = new List<Vector3>();
            float distance = Vector3.Distance(start, end);
            int steps = Mathf.Max(3, Mathf.CeilToInt(distance / 25f));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 pt = Vector3.Lerp(start, end, t);
                pt.y = GetGroundHeight(pt) + 0.05f;

                // Smooth out extreme slope steps
                if (points.Count > 0)
                {
                    Vector3 prev = points[points.Count - 1];
                    float deltaY = Mathf.Abs(pt.y - prev.y);
                    float horizontalDist = Vector3.Distance(new Vector3(pt.x, 0, pt.z), new Vector3(prev.x, 0, prev.z));
                    if (horizontalDist > 0.1f && (deltaY / horizontalDist) > Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad))
                    {
                        pt.y = Mathf.Lerp(prev.y, pt.y, 0.5f);
                    }
                }
                points.Add(pt);
            }
            return points;
        }

        private List<(SplineRoad spline, ExistingDistrictData parentDistrict)> CreateRoadsFromPaths(
            List<List<Vector3>> paths, List<ExistingDistrictData> districts, Transform fallbackRoot)
        {
            var result = new List<(SplineRoad spline, ExistingDistrictData parentDistrict)>();
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>(DefaultRoadMaterialPath);

            for (int i = 0; i < paths.Count; i++)
            {
                List<Vector3> path = paths[i];
                if (path.Count < 2) continue;

                // Find closest district
                ExistingDistrictData closestDistrict = GetClosestDistrict(path[0], districts);
                Transform parent = closestDistrict != null ? closestDistrict.RoadsFolder : fallbackRoot;

                GameObject roadGO = new GameObject($"Ankara_Road_{i + 1}");
                roadGO.transform.SetParent(parent, false);
                roadGO.transform.position = path[0];

                SplineRoad spline = roadGO.AddComponent<SplineRoad>();
                spline.SetLinearPath(path);

                // Create Road Mesh directly on Terrain
                MeshFilter mf = roadGO.AddComponent<MeshFilter>();
                MeshRenderer mr = roadGO.AddComponent<MeshRenderer>();
                if (roadMat != null) mr.sharedMaterial = roadMat;

                Mesh roadMesh = GenerateRibbonRoadMesh(path, mainRoadWidth);
                mf.sharedMesh = roadMesh;

                MeshCollider mc = roadGO.AddComponent<MeshCollider>();
                mc.sharedMesh = roadMesh;

                result.Add((spline, closestDistrict));
            }
            return result;
        }

        private ExistingDistrictData GetClosestDistrict(Vector3 point, List<ExistingDistrictData> districts)
        {
            ExistingDistrictData closest = null;
            float minDistSq = float.MaxValue;
            foreach (var d in districts)
            {
                float distSq = (d.Position - point).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closest = d;
                }
            }
            return closest;
        }

        private List<SplineRoad> CreateIstanbulBridges(Transform parent, Bounds layoutBounds)
        {
            List<SplineRoad> bridgeSplines = new List<SplineRoad>();

            // Istanbul Bridge #1: 15 July Martyrs / Bosphorus Style
            Vector3 bridge1Start = layoutBounds.center + new Vector3(-layoutBounds.extents.x * 0.7f, 0, layoutBounds.extents.z * 0.8f);
            Vector3 bridge1End = bridge1Start + new Vector3(-bridgeSpanLength, 0, 40f);
            bridge1Start.y = GetGroundHeight(bridge1Start) + 2f;
            bridge1End.y = bridge1Start.y + 5f;

            SplineRoad b1Spline = BuildSingleBridge(parent, "Bridge_15_July_Martyrs", bridge1Start, bridge1End, 38f, bridgeTowerHeight);
            if (b1Spline != null) bridgeSplines.Add(b1Spline);

            // Istanbul Bridge #2: Fatih Sultan Mehmet / Kuzey Marmara Style
            Vector3 bridge2Start = layoutBounds.center + new Vector3(-layoutBounds.extents.x * 0.7f, 0, layoutBounds.extents.z * 0.4f);
            Vector3 bridge2End = bridge2Start + new Vector3(-bridgeSpanLength, 0, -30f);
            bridge2Start.y = GetGroundHeight(bridge2Start) + 2f;
            bridge2End.y = bridge2Start.y + 4f;

            SplineRoad b2Spline = BuildSingleBridge(parent, "Bridge_Fatih_Sultan_Mehmet", bridge2Start, bridge2End, 42f, bridgeTowerHeight * 1.15f);
            if (b2Spline != null) bridgeSplines.Add(b2Spline);

            return bridgeSplines;
        }

        private SplineRoad BuildSingleBridge(Transform parent, string bridgeName, Vector3 start, Vector3 end, float width, float towerHeight)
        {
            GameObject bridgeGO = new GameObject(bridgeName);
            bridgeGO.transform.SetParent(parent, false);

            List<Vector3> bridgePoints = new List<Vector3>
            {
                start,
                Vector3.Lerp(start, end, 0.33f) + Vector3.up * 3f,
                Vector3.Lerp(start, end, 0.66f) + Vector3.up * 3f,
                end
            };

            SplineRoad spline = bridgeGO.AddComponent<SplineRoad>();
            spline.SetLinearPath(bridgePoints);

            // Deck Mesh
            MeshFilter mf = bridgeGO.AddComponent<MeshFilter>();
            MeshRenderer mr = bridgeGO.AddComponent<MeshRenderer>();
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>(DefaultRoadMaterialPath);
            if (roadMat != null) mr.sharedMaterial = roadMat;

            Mesh deckMesh = GenerateRibbonRoadMesh(bridgePoints, width);
            mf.sharedMesh = deckMesh;
            MeshCollider mc = bridgeGO.AddComponent<MeshCollider>();
            mc.sharedMesh = deckMesh;

            // Bridge Suspension Towers
            for (int i = 1; i <= 2; i++)
            {
                float t = i * 0.33f;
                Vector3 towerPos = Vector3.Lerp(start, end, t);
                Vector3 forward = (end - start).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

                CreateTowerColumn(bridgeGO.transform, $"Tower_{i}_Left", towerPos + side * (width * 0.55f), towerHeight);
                CreateTowerColumn(bridgeGO.transform, $"Tower_{i}_Right", towerPos - side * (width * 0.55f), towerHeight);
            }

            return spline;
        }

        private void CreateTowerColumn(Transform parent, string name, Vector3 pos, float height)
        {
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = name;
            tower.transform.SetParent(parent, false);
            tower.transform.position = pos + Vector3.up * (height * 0.5f);
            tower.transform.localScale = new Vector3(bridgeTowerWidth, height, bridgeTowerWidth);
        }

        private Mesh GenerateRibbonRoadMesh(List<Vector3> points, float width)
        {
            Mesh mesh = new Mesh();
            mesh.name = "GeneratedRoadMesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float halfW = width * 0.5f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 current = points[i];
                Vector3 forward = (i < points.Count - 1) ? (points[i + 1] - current).normalized : (current - points[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

                verts.Add(current - side * halfW);
                verts.Add(current + side * halfW);

                float v = (float)i / (points.Count - 1);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));

                if (i < points.Count - 1)
                {
                    int baseIdx = i * 2;
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + 2);
                    tris.Add(baseIdx + 1);

                    tris.Add(baseIdx + 1);
                    tris.Add(baseIdx + 2);
                    tris.Add(baseIdx + 3);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void PopulateRoadsideBuildings(List<(SplineRoad spline, ExistingDistrictData parentDistrict)> roads, Transform fallbackRoot)
        {
            foreach (var item in roads)
            {
                SplineRoad spline = item.spline;
                Transform parent = item.parentDistrict != null ? item.parentDistrict.BuildingsFolder : fallbackRoot;

                if (spline == null || spline.ControlPointCount < 4) continue;

                float length = spline.GetTotalLength();
                int buildingSteps = Mathf.FloorToInt(length / (14f + lotGap));

                for (int i = 1; i < buildingSteps - 1; i++)
                {
                    float t = (float)i / buildingSteps;
                    Vector3 pos = spline.GetPoint(t);
                    Vector3 tangent = spline.GetTangent(t);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

                    if (Random.value > 0.25f) PlaceBuildingAt(parent, pos + side * (mainRoadWidth * 0.5f + buildingSetback), Quaternion.LookRotation(side, Vector3.up));
                    if (Random.value > 0.25f) PlaceBuildingAt(parent, pos - side * (mainRoadWidth * 0.5f + buildingSetback), Quaternion.LookRotation(-side, Vector3.up));
                }
            }
        }

        private void PlaceBuildingAt(Transform parent, Vector3 pos, Quaternion rot)
        {
            if (buildingPrefabs.Count == 0) return;
            GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Count)];

            pos.y = GetGroundHeight(pos);
            GameObject bGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            bGO.transform.position = pos;
            bGO.transform.rotation = rot;
            Undo.RegisterCreatedObjectUndo(bGO, "Place Building");
        }

        private void PopulateTrees(List<(SplineRoad spline, ExistingDistrictData parentDistrict)> roads, Transform fallbackRoot)
        {
            foreach (var item in roads)
            {
                SplineRoad spline = item.spline;
                Transform parent = item.parentDistrict != null ? item.parentDistrict.TreesFolder : fallbackRoot;

                if (spline == null) continue;

                float length = spline.GetTotalLength();
                int steps = Mathf.FloorToInt(length / 10f);

                for (int i = 1; i < steps; i++)
                {
                    if (Random.value > treeChance) continue;
                    float t = (float)i / steps;
                    Vector3 pos = spline.GetPoint(t);
                    Vector3 tangent = spline.GetTangent(t);
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

                    float sideOffset = (Random.value > 0.5f ? 1f : -1f) * (mainRoadWidth * 0.5f + buildingSetback + 4f);
                    Vector3 treePos = pos + side * sideOffset;
                    treePos.y = GetGroundHeight(treePos);

                    GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
                    GameObject treeGO = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, parent);
                    treeGO.transform.position = treePos;
                    treeGO.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    float scale = Random.Range(0.85f, 1.3f);
                    treeGO.transform.localScale = Vector3.one * scale;

                    Undo.RegisterCreatedObjectUndo(treeGO, "Place Tree");
                }
            }
        }

        private void SyncRoadNetworkManager(Transform ankaraRoot)
        {
            RoadNetworkManager network = Object.FindFirstObjectByType<RoadNetworkManager>(FindObjectsInactive.Include);
            if (network == null)
            {
                Transform sysRoot = EnsureChild(ankaraRoot, "Systems");
                GameObject netGO = EnsureChild(sysRoot, "RoadNetworkManager").gameObject;
                network = netGO.GetComponent<RoadNetworkManager>();
                if (network == null) network = Undo.AddComponent<RoadNetworkManager>(netGO);
            }

            network.RefreshNetwork();
            EditorUtility.SetDirty(network);
        }

        private void ClearGeneratedCity()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform ankaraRoot = parentOverride != null ? parentOverride : scene.GetRootGameObjects().Length > 0 ? GameObject.Find(AnkaraRootName)?.transform : null;

            if (ankaraRoot != null)
            {
                Undo.DestroyObjectImmediate(ankaraRoot.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[AnkaraCityMasterBuilderTool] Cleared generated Ankara Master City.");
            }
        }

        private float GetGroundHeight(Vector3 pos)
        {
            // Sample terrain height directly if terrain present, otherwise raycast downwards
            Terrain activeTerrain = Terrain.activeTerrains.Length > 0 ? Terrain.activeTerrains[0] : null;
            if (activeTerrain != null)
            {
                return activeTerrain.SampleHeight(pos) + activeTerrain.transform.position.y;
            }

            if (Physics.Raycast(pos + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f))
            {
                return hit.point.y;
            }
            return pos.y;
        }

        private Transform EnsureRoot(Scene scene, string name)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i].name == name) return rootObjects[i].transform;
            }
            GameObject newRoot = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(newRoot, "Ensure Root");
            return newRoot.transform;
        }

        private Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;

            GameObject newChild = new GameObject(name);
            newChild.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(newChild, "Ensure Child");
            return newChild.transform;
        }

        private void EnsureNeighborhoodManager(Transform ankaraRoot)
        {
            NeighborhoodManager existingManager = Object.FindFirstObjectByType<NeighborhoodManager>(FindObjectsInactive.Include);
            if (existingManager != null) return;

            Transform systemsRoot = EnsureChild(ankaraRoot, "Systems");
            Transform managerTransform = EnsureChild(systemsRoot, "NeighborhoodManager");
            NeighborhoodManager manager = managerTransform.GetComponent<NeighborhoodManager>();
            if (manager == null) Undo.AddComponent<NeighborhoodManager>(managerTransform.gameObject);
        }

        private Bounds ResolveLayoutBounds(Scene scene, Transform ankaraRoot)
        {
            if (fitToTerrain)
            {
                Terrain[] terrains = Terrain.activeTerrains;
                if (terrains != null && terrains.Length > 0)
                {
                    Bounds b = new Bounds(terrains[0].transform.position + terrains[0].terrainData.size * 0.5f, terrains[0].terrainData.size);
                    for (int i = 1; i < terrains.Length; i++)
                    {
                        b.Encapsulate(new Bounds(terrains[i].transform.position + terrains[i].terrainData.size * 0.5f, terrains[i].terrainData.size));
                    }
                    return b;
                }
            }
            return new Bounds(cityOrigin, new Vector3(1600f * layoutScale, 200f, 1600f * layoutScale));
        }

        private Rect ExpandNormalizedArea(Rect normalizedArea, Bounds bounds)
        {
            Vector3 size = bounds.size;
            return new Rect(
                bounds.min.x + normalizedArea.x * size.x,
                bounds.min.z + normalizedArea.y * size.z,
                normalizedArea.width * size.x,
                normalizedArea.height * size.z);
        }

        private Vector3 NormalizedAreaToWorldCenter(Rect area, Bounds bounds)
        {
            return new Vector3(area.x + area.width * 0.5f, bounds.center.y, area.y + area.height * 0.5f);
        }
    }
}
