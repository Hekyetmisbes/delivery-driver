using System.Collections.Generic;
using DeliveryDriver.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class IstanbulDistrictSetupTool : EditorWindow
    {
        private const string IstanbulRootName = "Istanbul";
        private const string WorldRootName = "World";
        private const string DistrictsRootName = "Districts";
        private const string ZoneTriggerName = "ZoneTrigger";

        [SerializeField] private Transform parentOverride;
        [SerializeField] private bool fitToIstanbulTerrain = true;
        [SerializeField] private Vector3 cityOrigin = new Vector3(1500f, 0f, 0f);
        [SerializeField] private float layoutScale = 1f;
        [SerializeField] private float zoneHeight = 180f;
        [SerializeField] private float zoneOverlap = 8f;
        [SerializeField] private bool createNeighborhoodManager = true;
        [SerializeField] private bool createLandmarkPlaceholders = true;
        [SerializeField] private bool selectDistrictRootAfterCreate = true;

        private readonly string[] districtChildFolders =
        {
            "Roads",
            "Landmarks",
            "DeliveryPoints",
            "Buildings",
            "Trees",
            "Props"
        };

        public struct DistrictSpec
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

        public static readonly DistrictSpec[] Districts =
        {
            new DistrictSpec(
                "District_AtaturkHavalimani",
                "Atatürk Havalimanı",
                new Rect(0f, 0f, 0.35f, 0.35f),
                new Color(0.95f, 0.65f, 0.15f, 0.35f),
                "Landmark_Ataturk_Havalimani_Terminal"),
            new DistrictSpec(
                "District_TarihiYarimada",
                "Tarihi Yarımada & Sultanahmet",
                new Rect(0.35f, 0.05f, 0.30f, 0.32f),
                new Color(0.85f, 0.45f, 0.25f, 0.35f),
                "Landmark_Ayasofya_Sultanahmet"),
            new DistrictSpec(
                "District_GalataKarakoy",
                "Galata & Karaköy",
                new Rect(0.35f, 0.37f, 0.25f, 0.25f),
                new Color(0.15f, 0.75f, 0.85f, 0.35f),
                "Landmark_Galata_Kulesi"),
            new DistrictSpec(
                "District_TaksimBeyoglu",
                "Taksim & Beyoğlu",
                new Rect(0.35f, 0.62f, 0.25f, 0.28f),
                new Color(0.85f, 0.25f, 0.45f, 0.35f),
                "Landmark_Taksim_Meydani"),
            new DistrictSpec(
                "District_BesiktasOrtakoy",
                "Beşiktaş & Ortaköy",
                new Rect(0.60f, 0.35f, 0.20f, 0.32f),
                new Color(0.20f, 0.45f, 0.95f, 0.35f),
                "Landmark_Ortakoy_Bogazici"),
            new DistrictSpec(
                "District_LeventMaslak",
                "Levent & Maslak",
                new Rect(0.55f, 0.67f, 0.25f, 0.33f),
                new Color(0.30f, 0.85f, 0.60f, 0.35f),
                "Landmark_Maslak_Plazalar"),
            new DistrictSpec(
                "District_UskudarSalacak",
                "Üsküdar & Salacak",
                new Rect(0.75f, 0.35f, 0.25f, 0.30f),
                new Color(0.65f, 0.35f, 0.95f, 0.35f),
                "Landmark_Kiz_Kulesi"),
            new DistrictSpec(
                "District_KadikoyModa",
                "Kadıköy & Moda",
                new Rect(0.70f, 0.05f, 0.30f, 0.30f),
                new Color(0.95f, 0.40f, 0.40f, 0.35f),
                "Landmark_Kadikoy_Boga")
        };

        [MenuItem("Tools/Delivery Driver/City/Istanbul District Setup")]
        public static void Open()
        {
            IstanbulDistrictSetupTool window = GetWindow<IstanbulDistrictSetupTool>("Istanbul Districts");
            window.minSize = new Vector2(430f, 380f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Istanbul District Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "İstanbul sektöründeki semt parentlarını, NeighborhoodZone triggerlarını ve " +
                "çalışma klasörlerini (Roads, Landmarks, DeliveryPoints, Buildings, Trees, Props) oluşturur.\n" +
                "Galata Kulesi ve Atatürk Havalimanı dahil 8 ikonik bölgeyi kapsar.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            parentOverride = (Transform)EditorGUILayout.ObjectField("Parent Override", parentOverride, typeof(Transform), true);
            fitToIstanbulTerrain = EditorGUILayout.Toggle("Fit To Istanbul Terrain", fitToIstanbulTerrain);
            cityOrigin = EditorGUILayout.Vector3Field("City Origin (Default: 1500,0,0)", cityOrigin);
            layoutScale = Mathf.Max(0.1f, EditorGUILayout.FloatField("Layout Scale", layoutScale));
            zoneHeight = Mathf.Max(10f, EditorGUILayout.FloatField("Zone Height", zoneHeight));
            zoneOverlap = Mathf.Max(0f, EditorGUILayout.FloatField("Zone Overlap", zoneOverlap));
            createNeighborhoodManager = EditorGUILayout.Toggle("Create Neighborhood Manager", createNeighborhoodManager);
            createLandmarkPlaceholders = EditorGUILayout.Toggle("Create Landmark Visual Placeholders", createLandmarkPlaceholders);
            selectDistrictRootAfterCreate = EditorGUILayout.Toggle("Select District Root", selectDistrictRootAfterCreate);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Districts (8 Bölgeli Plan)", EditorStyles.boldLabel);
            for (int i = 0; i < Districts.Length; i++)
            {
                EditorGUILayout.LabelField(Districts[i].DisplayName, Districts[i].ObjectName);
            }

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Create / Update Istanbul District Skeleton", GUILayout.Height(36f)))
            {
                CreateOrUpdateSkeleton();
            }
        }

        public void CreateOrUpdateSkeleton()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform istanbulRoot = ResolveIstanbulRoot(scene);
            Transform districtsRoot = EnsureChild(istanbulRoot, DistrictsRootName);
            Bounds layoutBounds = ResolveLayoutBounds(scene, istanbulRoot);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Istanbul District Skeleton");

            if (createNeighborhoodManager)
            {
                EnsureNeighborhoodManager(istanbulRoot);
            }

            int neighborhoodLayer = LayerMask.NameToLayer("Neighborhood");
            for (int i = 0; i < Districts.Length; i++)
            {
                CreateOrUpdateDistrict(districtsRoot, Districts[i], neighborhoodLayer, layoutBounds);
            }

            EditorUtility.SetDirty(districtsRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);

            if (selectDistrictRootAfterCreate)
            {
                Selection.activeTransform = districtsRoot;
            }

            Debug.Log($"[IstanbulDistrictSetupTool] Created/updated {Districts.Length} Istanbul district skeletons around {layoutBounds.center}.");
        }

        private Transform ResolveIstanbulRoot(Scene scene)
        {
            if (parentOverride != null)
            {
                return parentOverride;
            }

            // Check if World root exists (CityHierarchyOrganizer standard)
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

            return EnsureRoot(scene, IstanbulRootName);
        }

        private void EnsureNeighborhoodManager(Transform istanbulRoot)
        {
            NeighborhoodManager existingManager = Object.FindFirstObjectByType<NeighborhoodManager>(FindObjectsInactive.Include);
            if (existingManager != null)
            {
                return;
            }

            Transform systemsRoot = EnsureChild(istanbulRoot, "Systems");
            Transform managerTransform = EnsureChild(systemsRoot, "NeighborhoodManager");
            NeighborhoodManager manager = managerTransform.GetComponent<NeighborhoodManager>();
            if (manager == null)
            {
                manager = Undo.AddComponent<NeighborhoodManager>(managerTransform.gameObject);
            }

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(managerTransform.gameObject);
        }

        private void CreateOrUpdateDistrict(Transform districtsRoot, DistrictSpec spec, int neighborhoodLayer, Bounds layoutBounds)
        {
            Transform district = EnsureChild(districtsRoot, spec.ObjectName);
            Rect area = ExpandNormalizedArea(spec.NormalizedArea, layoutBounds);
            Vector3 worldCenter = NormalizedAreaToWorldCenter(area, layoutBounds);
            Vector3 worldSize = NormalizedAreaToWorldSize(area, layoutBounds);
            district.position = worldCenter;
            district.localScale = Vector3.one;

            for (int i = 0; i < districtChildFolders.Length; i++)
            {
                EnsureChild(district, districtChildFolders[i]);
            }

            Transform zoneTrigger = EnsureChild(district, ZoneTriggerName);
            zoneTrigger.localPosition = Vector3.zero;
            zoneTrigger.localRotation = Quaternion.identity;
            zoneTrigger.localScale = Vector3.one;
            if (neighborhoodLayer >= 0)
            {
                zoneTrigger.gameObject.layer = neighborhoodLayer;
            }

            BoxCollider box = zoneTrigger.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(zoneTrigger.gameObject);
            }

            box.isTrigger = true;
            box.center = Vector3.up * (zoneHeight * 0.5f);
            box.size = new Vector3(worldSize.x, zoneHeight, worldSize.z);

            NeighborhoodZone zone = zoneTrigger.GetComponent<NeighborhoodZone>();
            if (zone == null)
            {
                zone = Undo.AddComponent<NeighborhoodZone>(zoneTrigger.gameObject);
            }

            zone.NeighborhoodName = spec.DisplayName;
            zone.DebugColor = spec.Color;
            EditorUtility.SetDirty(zone);
            EditorUtility.SetDirty(box);

            if (createLandmarkPlaceholders)
            {
                Transform landmarks = EnsureChild(district, "Landmarks");
                CreateLandmarkVisualPlaceholder(landmarks, spec);
            }
        }

        private void CreateLandmarkVisualPlaceholder(Transform landmarksRoot, DistrictSpec spec)
        {
            Transform existing = landmarksRoot.Find(spec.LandmarkName);
            if (existing != null)
            {
                return;
            }

            GameObject placeholder = new GameObject(spec.LandmarkName);
            Undo.RegisterCreatedObjectUndo(placeholder, $"Create {spec.LandmarkName}");
            placeholder.transform.SetParent(landmarksRoot, false);
            placeholder.transform.localPosition = Vector3.zero;

            // Create iconic primitive silhouettes for key landmarks
            if (spec.LandmarkName == "Landmark_Galata_Kulesi")
            {
                BuildGalataTowerSilhouette(placeholder.transform);
            }
            else if (spec.LandmarkName == "Landmark_Ataturk_Havalimani_Terminal")
            {
                BuildAirportTerminalSilhouette(placeholder.transform);
            }
            else if (spec.LandmarkName == "Landmark_Kiz_Kulesi")
            {
                BuildMaidensTowerSilhouette(placeholder.transform);
            }
        }

        private void BuildGalataTowerSilhouette(Transform root)
        {
            // Cylindrical main body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "TowerBody";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 25f, 0f);
            body.transform.localScale = new Vector3(12f, 25f, 12f);

            // Upper balcony
            GameObject balcony = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            balcony.name = "TowerBalcony";
            balcony.transform.SetParent(root, false);
            balcony.transform.localPosition = new Vector3(0f, 52f, 0f);
            balcony.transform.localScale = new Vector3(14f, 2f, 14f);

            // Conical cap / spire
            GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spire.name = "ConicalSpire";
            spire.transform.SetParent(root, false);
            spire.transform.localPosition = new Vector3(0f, 60f, 0f);
            spire.transform.localScale = new Vector3(8f, 6f, 8f);
        }

        private void BuildAirportTerminalSilhouette(Transform root)
        {
            // Main Terminal Hall
            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = "TerminalBuilding";
            terminal.transform.SetParent(root, false);
            terminal.transform.localPosition = new Vector3(0f, 8f, 0f);
            terminal.transform.localScale = new Vector3(80f, 16f, 40f);

            // Control Tower
            GameObject towerShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerShaft.name = "ControlTowerShaft";
            towerShaft.transform.SetParent(root, false);
            towerShaft.transform.localPosition = new Vector3(45f, 22f, 0f);
            towerShaft.transform.localScale = new Vector3(8f, 22f, 8f);

            GameObject towerCab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerCab.name = "ControlTowerCab";
            towerCab.transform.SetParent(root, false);
            towerCab.transform.localPosition = new Vector3(45f, 46f, 0f);
            towerCab.transform.localScale = new Vector3(14f, 4f, 14f);

            // Runway placeholder stripe
            GameObject runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
            runway.name = "Runway_35L_Placeholder";
            runway.transform.SetParent(root, false);
            runway.transform.localPosition = new Vector3(0f, 0.2f, 70f);
            runway.transform.localScale = new Vector3(180f, 0.2f, 24f);
        }

        private void BuildMaidensTowerSilhouette(Transform root)
        {
            // Rock island foundation
            GameObject basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlatform.name = "IslandBase";
            basePlatform.transform.SetParent(root, false);
            basePlatform.transform.localPosition = new Vector3(0f, 2f, 0f);
            basePlatform.transform.localScale = new Vector3(20f, 4f, 20f);

            // Tower
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.name = "Tower";
            tower.transform.SetParent(root, false);
            tower.transform.localPosition = new Vector3(0f, 12f, 0f);
            tower.transform.localScale = new Vector3(7f, 8f, 7f);
        }

        private Bounds ResolveLayoutBounds(Scene scene, Transform istanbulRoot)
        {
            if (fitToIstanbulTerrain)
            {
                if (TryGetTerrainBounds(parentOverride, out Bounds parentBounds))
                {
                    return parentBounds;
                }

                if (TryGetTerrainBounds(istanbulRoot, out Bounds istanbulBounds))
                {
                    return istanbulBounds;
                }

                if (TryGetSelectedTerrainBounds(out Bounds selectionBounds))
                {
                    return selectionBounds;
                }
            }

            Vector3 size = new Vector3(1600f * layoutScale, zoneHeight, 1600f * layoutScale);
            return new Bounds(cityOrigin, size);
        }

        private bool TryGetSelectedTerrainBounds(out Bounds bounds)
        {
            bounds = default;
            Transform[] selectedTransforms = Selection.transforms;
            bool hasBounds = false;
            for (int i = 0; i < selectedTransforms.Length; i++)
            {
                if (!TryGetTerrainBounds(selectedTransforms[i], out Bounds selectedBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = selectedBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(selectedBounds);
                }
            }

            return hasBounds;
        }

        private bool TryGetTerrainBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
            return TryGetTerrainBounds(terrains, out bounds);
        }

        private bool TryGetTerrainBounds(Terrain[] terrains, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (terrains == null)
            {
                return false;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 terrainSize = terrain.terrainData.size;
                Bounds terrainBounds = new Bounds(
                    terrain.transform.position + terrainSize * 0.5f,
                    terrainSize);

                if (!hasBounds)
                {
                    bounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(terrainBounds);
                }
            }

            return hasBounds;
        }

        private Rect ExpandNormalizedArea(Rect area, Bounds layoutBounds)
        {
            float overlapX = layoutBounds.size.x > 0.001f ? zoneOverlap / layoutBounds.size.x : 0f;
            float overlapZ = layoutBounds.size.z > 0.001f ? zoneOverlap / layoutBounds.size.z : 0f;
            float xMin = Mathf.Clamp01(area.xMin - overlapX);
            float xMax = Mathf.Clamp01(area.xMax + overlapX);
            float yMin = Mathf.Clamp01(area.yMin - overlapZ);
            float yMax = Mathf.Clamp01(area.yMax + overlapZ);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Vector3 NormalizedAreaToWorldCenter(Rect area, Bounds layoutBounds)
        {
            float x = Mathf.Lerp(layoutBounds.min.x, layoutBounds.max.x, area.center.x);
            float z = Mathf.Lerp(layoutBounds.min.z, layoutBounds.max.z, area.center.y);
            return new Vector3(x, layoutBounds.min.y, z);
        }

        private Vector3 NormalizedAreaToWorldSize(Rect area, Bounds layoutBounds)
        {
            return new Vector3(
                Mathf.Max(1f, area.width * layoutBounds.size.x),
                zoneHeight,
                Mathf.Max(1f, area.height * layoutBounds.size.z));
        }

        private Transform EnsureRoot(Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                {
                    return roots[i].transform;
                }
            }

            GameObject root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, $"Create {rootName}");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private Transform EnsureChild(Transform parent, string childName)
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
    }
}
