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
    /// Master Editor Tool to construct Istanbul city end-to-end using pre-existing scene districts.
    /// Places slope-adapted roads, randomized side streets, roadside buildings, trees,
    /// and connects Atatürk Havalimanı, Galata Kulesi, and other iconic districts on the Terrain surface.
    /// </summary>
    public class IstanbulCityMasterBuilderTool : EditorWindow
    {
        private const string IstanbulRootName = "Istanbul";
        private const string WorldRootName = "World";
        private const string DistrictsRootName = "Districts";
        private const string RoadsRootName = "Roads";
        private const string BuildingsRootName = "Buildings";
        private const string TreesRootName = "Trees";

        private const string BuildingsFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Buildings";
        private const string NaturesFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Natures";
        private const string DefaultRoadMaterialPath = "Assets/Art/Materials/RoadTools/Generated_Asphalt.mat";

        [Header("General Settings")]
        [SerializeField] private Transform parentOverride;
        [SerializeField] private bool fitToTerrain = true;
        [SerializeField] private Vector3 cityOrigin = new Vector3(1500f, 0f, 0f);
        [SerializeField] private float layoutScale = 1.0f;
        [SerializeField] private int randomSeed = 5342;

        [Header("Road & Slope Configuration")]
        [SerializeField] private float mainRoadWidth = 8.5f;
        [SerializeField] private float sideStreetWidth = 6.0f;
        [SerializeField] private float sidewalkWidth = 2.2f;
        [SerializeField] private float sideStreetDensity = 0.45f;
        [SerializeField] private float maxSlopeAngle = 25f;

        [Header("Building & Prop Placement")]
        [SerializeField] private bool placeRoadsideBuildings = true;
        [SerializeField] private float buildingSetback = 1.2f;
        [SerializeField] private float lotGap = 0.2f;
        [SerializeField] private bool placeTrees = true;
        [SerializeField] private float treeChance = 0.5f;

        private readonly List<GameObject> buildingPrefabs = new List<GameObject>();
        private readonly List<GameObject> treePrefabs = new List<GameObject>();

        private class ExistingDistrictData
        {
            public IstanbulDistrictSetupTool.DistrictSpec Spec;
            public Transform DistrictRoot;
            public Transform RoadsFolder;
            public Transform BuildingsFolder;
            public Transform TreesFolder;
            public Vector3 Position;
        }

        [MenuItem("Tools/Delivery Driver/City/Istanbul Master City Builder")]
        public static void Open()
        {
            IstanbulCityMasterBuilderTool window = GetWindow<IstanbulCityMasterBuilderTool>("Istanbul Master Builder");
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
            EditorGUILayout.LabelField("Istanbul Master City Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sahnede bulunan İstanbul mahallelerinin içine, Terrain yüzeyi üzerine " +
                "yol ağlarını, sokak aralarını, binaları ve Atatürk Havalimanı ile Galata Kulesi bağlantılarını inşa eder.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            parentOverride = (Transform)EditorGUILayout.ObjectField("Parent Override", parentOverride, typeof(Transform), true);
            fitToTerrain = EditorGUILayout.Toggle("Fit To Terrain Bounds", fitToTerrain);
            cityOrigin = EditorGUILayout.Vector3Field("City Origin (Default: 1500,0,0)", cityOrigin);
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
            EditorGUILayout.LabelField("Buildings & Trees", EditorStyles.boldLabel);
            placeRoadsideBuildings = EditorGUILayout.Toggle("Place Roadside Buildings", placeRoadsideBuildings);
            buildingSetback = Mathf.Max(0.1f, EditorGUILayout.FloatField("Building Setback", buildingSetback));
            placeTrees = EditorGUILayout.Toggle("Place Trees", placeTrees);
            treeChance = Mathf.Clamp01(EditorGUILayout.FloatField("Tree Chance", treeChance));

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("BUILD ROADS & BUILDINGS IN ISTANBUL DISTRICTS", GUILayout.Height(42f)))
            {
                BuildFullIstanbulCity();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Clear Generated Istanbul Roads & Buildings", GUILayout.Height(24f)))
            {
                ClearGeneratedCity();
            }
        }

        private void BuildFullIstanbulCity()
        {
            Random.InitState(randomSeed);
            Scene scene = SceneManager.GetActiveScene();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Istanbul Roads and Buildings");

            Transform istanbulRoot = ResolveIstanbulRoot(scene);
            Transform districtsRoot = EnsureChild(istanbulRoot, DistrictsRootName);
            Bounds layoutBounds = ResolveLayoutBounds(scene, istanbulRoot);

            // Step 1: Discover / Resolve Existing Districts in Scene
            EnsureNeighborhoodManager(istanbulRoot);
            int neighborhoodLayer = LayerMask.NameToLayer("Neighborhood");
            List<ExistingDistrictData> districtDataList = ResolveExistingDistricts(districtsRoot, neighborhoodLayer, layoutBounds);

            // Step 2: Build Main Road Network & Randomized Side Streets Inside District Folders
            List<List<Vector3>> roadPaths = BuildIstanbulRoadPaths(districtDataList, layoutBounds);
            List<(SplineRoad spline, ExistingDistrictData parentDistrict)> generatedSplineRoads = CreateRoadsFromPaths(roadPaths, districtDataList, istanbulRoot);

            // Step 3: Populate Roadside Buildings inside District "Buildings" folders
            if (placeRoadsideBuildings && buildingPrefabs.Count > 0)
            {
                PopulateRoadsideBuildings(generatedSplineRoads, istanbulRoot);
            }

            // Step 4: Populate Nature / Trees inside District "Trees" folders
            if (placeTrees && treePrefabs.Count > 0)
            {
                PopulateTrees(generatedSplineRoads, istanbulRoot);
            }

            // Step 5: Sync Road Network Manager
            SyncRoadNetworkManager(istanbulRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[IstanbulCityMasterBuilderTool] Successfully built Istanbul roads & buildings across {districtDataList.Count} districts!");
        }

        private Transform ResolveIstanbulRoot(Scene scene)
        {
            if (parentOverride != null) return parentOverride;

            GameObject[] roots = scene.GetRootGameObjects();
            Transform worldRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == WorldRootName)
                {
                    worldRoot = roots[i].transform;
                    break;
                }
            }

            if (worldRoot != null)
            {
                Transform existingUnderWorld = worldRoot.Find(IstanbulRootName);
                if (existingUnderWorld != null) return existingUnderWorld;
                return EnsureChild(worldRoot, IstanbulRootName);
            }

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == IstanbulRootName) return roots[i].transform;
            }

            GameObject newRoot = new GameObject(IstanbulRootName);
            Undo.RegisterCreatedObjectUndo(newRoot, "Ensure Istanbul Root");
            SceneManager.MoveGameObjectToScene(newRoot, scene);
            return newRoot.transform;
        }

        private List<ExistingDistrictData> ResolveExistingDistricts(Transform districtsRoot, int neighborhoodLayer, Bounds layoutBounds)
        {
            List<ExistingDistrictData> list = new List<ExistingDistrictData>();
            var specs = IstanbulDistrictSetupTool.Districts;

            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                Transform dRoot = districtsRoot.Find(spec.ObjectName);

                if (dRoot == null)
                {
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
                box.size = new Vector3(320f, 180f, 320f);

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

        private List<List<Vector3>> BuildIstanbulRoadPaths(List<ExistingDistrictData> districts, Bounds layoutBounds)
        {
            List<List<Vector3>> paths = new List<List<Vector3>>();
            Dictionary<string, Vector3> districtPos = new Dictionary<string, Vector3>();
            foreach (var d in districts)
            {
                districtPos[d.Spec.ObjectName] = d.Position;
            }

            // 1. Sahil Yolu: Atatürk Havalimanı -> Tarihi Yarımada (Kennedy Caddesi)
            if (districtPos.ContainsKey("District_AtaturkHavalimani") && districtPos.ContainsKey("District_TarihiYarimada"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_AtaturkHavalimani"], districtPos["District_TarihiYarimada"]));
            }

            // 2. Galata Köprüsü: Tarihi Yarımada -> Galata & Karaköy
            if (districtPos.ContainsKey("District_TarihiYarimada") && districtPos.ContainsKey("District_GalataKarakoy"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_TarihiYarimada"], districtPos["District_GalataKarakoy"]));
            }

            // 3. İstiklal / Yokuş Aksı: Galata & Karaköy -> Taksim & Beyoğlu
            if (districtPos.ContainsKey("District_GalataKarakoy") && districtPos.ContainsKey("District_TaksimBeyoglu"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_GalataKarakoy"], districtPos["District_TaksimBeyoglu"]));
            }

            // 4. Dolmabahçe Sahil Yolu: Taksim & Beyoğlu -> Beşiktaş & Ortaköy
            if (districtPos.ContainsKey("District_TaksimBeyoglu") && districtPos.ContainsKey("District_BesiktasOrtakoy"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_TaksimBeyoglu"], districtPos["District_BesiktasOrtakoy"]));
            }

            // 5. Büyükdere Caddesi: Beşiktaş & Ortaköy -> Levent & Maslak
            if (districtPos.ContainsKey("District_BesiktasOrtakoy") && districtPos.ContainsKey("District_LeventMaslak"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_BesiktasOrtakoy"], districtPos["District_LeventMaslak"]));
            }

            // 6. E-5 / Basın Ekspres Transit Yolu: Atatürk Havalimanı -> Taksim & Beyoğlu
            if (districtPos.ContainsKey("District_AtaturkHavalimani") && districtPos.ContainsKey("District_TaksimBeyoglu"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_AtaturkHavalimani"], districtPos["District_TaksimBeyoglu"]));
            }

            // 7. Boğaz Geçiş Aksı: Beşiktaş & Ortaköy -> Üsküdar & Salacak (15 Temmuz Köprüsü bağlantısı)
            if (districtPos.ContainsKey("District_BesiktasOrtakoy") && districtPos.ContainsKey("District_UskudarSalacak"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_BesiktasOrtakoy"], districtPos["District_UskudarSalacak"]));
            }

            // 8. FSM Kuzey Geçiş Aksı: Levent & Maslak -> Üsküdar / Anadolu
            if (districtPos.ContainsKey("District_LeventMaslak") && districtPos.ContainsKey("District_UskudarSalacak"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_LeventMaslak"], districtPos["District_UskudarSalacak"]));
            }

            // 9. Anadolu Sahil Yolu: Üsküdar & Salacak -> Kadıköy & Moda
            if (districtPos.ContainsKey("District_UskudarSalacak") && districtPos.ContainsKey("District_KadikoyModa"))
            {
                paths.Add(SampleSlopeAdaptedPath(districtPos["District_UskudarSalacak"], districtPos["District_KadikoyModa"]));
            }

            // 10. Randomized Side Streets (Sokak Araları) per district
            int sideStreetCount = Mathf.RoundToInt(districts.Count * 3 * sideStreetDensity);
            for (int i = 0; i < sideStreetCount; i++)
            {
                int cIdx = Random.Range(0, districts.Count);
                Vector3 origin = districts[cIdx].Position;
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

                ExistingDistrictData closestDistrict = GetClosestDistrict(path[0], districts);
                Transform parent = closestDistrict != null ? closestDistrict.RoadsFolder : fallbackRoot;

                GameObject roadGO = new GameObject($"Istanbul_Road_{i + 1}");
                roadGO.transform.SetParent(parent, false);
                roadGO.transform.position = path[0];

                SplineRoad spline = roadGO.AddComponent<SplineRoad>();
                spline.sectorTag = "İstanbul";
                spline.SetLinearPath(path);

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

        private Mesh GenerateRibbonRoadMesh(List<Vector3> points, float width)
        {
            Mesh mesh = new Mesh();
            mesh.name = "GeneratedIstanbulRoadMesh";

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

        private void SyncRoadNetworkManager(Transform istanbulRoot)
        {
            RoadNetworkManager network = Object.FindFirstObjectByType<RoadNetworkManager>(FindObjectsInactive.Include);
            if (network == null)
            {
                Transform sysRoot = EnsureChild(istanbulRoot, "Systems");
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
            Transform istanbulRoot = ResolveIstanbulRoot(scene);

            if (istanbulRoot != null)
            {
                // Clear roads, buildings, trees inside districts while preserving district skeletons
                Transform districtsRoot = istanbulRoot.Find(DistrictsRootName);
                if (districtsRoot != null)
                {
                    for (int i = 0; i < districtsRoot.childCount; i++)
                    {
                        Transform d = districtsRoot.GetChild(i);
                        ClearFolder(d.Find(RoadsRootName));
                        ClearFolder(d.Find(BuildingsRootName));
                        ClearFolder(d.Find(TreesRootName));
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[IstanbulCityMasterBuilderTool] Cleared generated Istanbul roads and buildings.");
            }
        }

        private void ClearFolder(Transform folder)
        {
            if (folder == null) return;
            for (int i = folder.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(folder.GetChild(i).gameObject);
            }
        }

        private float GetGroundHeight(Vector3 pos)
        {
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

        private Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;

            GameObject newChild = new GameObject(name);
            newChild.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(newChild, "Ensure Child");
            return newChild.transform;
        }

        private void EnsureNeighborhoodManager(Transform istanbulRoot)
        {
            NeighborhoodManager existingManager = Object.FindFirstObjectByType<NeighborhoodManager>(FindObjectsInactive.Include);
            if (existingManager != null) return;

            Transform systemsRoot = EnsureChild(istanbulRoot, "Systems");
            Transform managerTransform = EnsureChild(systemsRoot, "NeighborhoodManager");
            NeighborhoodManager manager = managerTransform.GetComponent<NeighborhoodManager>();
            if (manager == null) Undo.AddComponent<NeighborhoodManager>(managerTransform.gameObject);
        }

        private Bounds ResolveLayoutBounds(Scene scene, Transform istanbulRoot)
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
