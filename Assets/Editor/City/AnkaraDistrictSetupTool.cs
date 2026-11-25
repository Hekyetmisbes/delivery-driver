using System.Collections.Generic;
using DeliveryDriver.City;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class AnkaraDistrictSetupTool : EditorWindow
    {
        private const string AnkaraRootName = "Ankara";
        private const string CityRootName = "City";
        private const string DistrictsRootName = "Districts";
        private const string ZoneTriggerName = "ZoneTrigger";

        [SerializeField] private Transform parentOverride;
        [SerializeField] private bool fitToAnkaraCityTerrain = true;
        [SerializeField] private Vector3 cityOrigin = Vector3.zero;
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

        private static readonly DistrictSpec[] Districts =
        {
            new DistrictSpec(
                "District_OstimLiman",
                "Ostim Liman",
                new Rect(0f, 0f, 0.33f, 0.22f),
                new Color(0.95f, 0.55f, 0.18f, 0.35f),
                "Landmark_Port_Cranes"),
            new DistrictSpec(
                "District_KizilayKordon",
                "Kizilay Kordon",
                new Rect(0.33f, 0f, 0.67f, 0.22f),
                new Color(0.15f, 0.75f, 1f, 0.35f),
                "Landmark_Coastal_Promenade"),
            new DistrictSpec(
                "District_SihhiyeTransfer",
                "Sihhiye Aktarma",
                new Rect(0f, 0.22f, 1f, 0.16f),
                new Color(0.35f, 0.8f, 0.55f, 0.35f),
                "Landmark_Central_Junction"),
            new DistrictSpec(
                "District_UlusKale",
                "Ulus Kale Kapilari",
                new Rect(0f, 0.38f, 0.5f, 0.2f),
                new Color(0.8f, 0.55f, 0.3f, 0.35f),
                "Landmark_Castle_Gates"),
            new DistrictSpec(
                "District_HamamonuKaleici",
                "Hamamonu Kaleici",
                new Rect(0.5f, 0.38f, 0.5f, 0.2f),
                new Color(0.7f, 0.45f, 0.25f, 0.35f),
                "Landmark_Old_Town"),
            new DistrictSpec(
                "District_AnittepePlato",
                "Anittepe Plato",
                new Rect(0f, 0.58f, 1f, 0.18f),
                new Color(0.95f, 0.9f, 0.55f, 0.35f),
                "Landmark_Monumental_Avenue"),
            new DistrictSpec(
                "District_CankayaYamaci",
                "Cankaya Yamaci",
                new Rect(0f, 0.76f, 1f, 0.14f),
                new Color(0.45f, 0.75f, 0.35f, 0.35f),
                "Landmark_Embassy_Ridge"),
            new DistrictSpec(
                "District_AtakuleZirve",
                "Atakule Zirve",
                new Rect(0f, 0.9f, 1f, 0.1f),
                new Color(0.6f, 0.45f, 1f, 0.35f),
                "Landmark_Atakule_Peak")
        };

        [MenuItem("Tools/Delivery Driver/City/Ankara District Setup")]
        public static void Open()
        {
            AnkaraDistrictSetupTool window = GetWindow<AnkaraDistrictSetupTool>("Ankara Districts");
            window.minSize = new Vector2(430f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ankara District Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Epik Alternatif Ankara planindaki mahalle parentlarini, NeighborhoodZone triggerlarini ve calisma klasorlerini olusturur. Tekrar calistirinca mevcut objeleri gunceller.", MessageType.Info);

            parentOverride = (Transform)EditorGUILayout.ObjectField("Parent Override", parentOverride, typeof(Transform), true);
            fitToAnkaraCityTerrain = EditorGUILayout.Toggle("Fit To Ankara/City Terrain", fitToAnkaraCityTerrain);
            cityOrigin = EditorGUILayout.Vector3Field("City Origin", cityOrigin);
            layoutScale = Mathf.Max(0.1f, EditorGUILayout.FloatField("Layout Scale", layoutScale));
            zoneHeight = Mathf.Max(10f, EditorGUILayout.FloatField("Zone Height", zoneHeight));
            zoneOverlap = Mathf.Max(0f, EditorGUILayout.FloatField("Zone Overlap", zoneOverlap));
            createNeighborhoodManager = EditorGUILayout.Toggle("Create Neighborhood Manager", createNeighborhoodManager);
            createLandmarkPlaceholders = EditorGUILayout.Toggle("Create Landmark Placeholders", createLandmarkPlaceholders);
            selectDistrictRootAfterCreate = EditorGUILayout.Toggle("Select District Root", selectDistrictRootAfterCreate);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Districts", EditorStyles.boldLabel);
            for (int i = 0; i < Districts.Length; i++)
            {
                EditorGUILayout.LabelField(Districts[i].DisplayName, Districts[i].ObjectName);
            }

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Create / Update Ankara District Skeleton", GUILayout.Height(34f)))
            {
                CreateOrUpdateSkeleton();
            }
        }

        private void CreateOrUpdateSkeleton()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform ankaraRoot = parentOverride != null ? parentOverride : EnsureRoot(scene, AnkaraRootName);
            Transform districtsRoot = EnsureChild(ankaraRoot, DistrictsRootName);
            Bounds layoutBounds = ResolveLayoutBounds(scene, ankaraRoot);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Ankara District Skeleton");

            if (createNeighborhoodManager)
            {
                EnsureNeighborhoodManager(ankaraRoot);
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

            Debug.Log($"[AnkaraDistrictSetupTool] Created/updated {Districts.Length} Ankara district skeletons.");
        }

        private void EnsureNeighborhoodManager(Transform ankaraRoot)
        {
            NeighborhoodManager existingManager = Object.FindFirstObjectByType<NeighborhoodManager>(FindObjectsInactive.Include);
            if (existingManager != null)
            {
                return;
            }

            Transform systemsRoot = EnsureChild(ankaraRoot, "Systems");
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
                Transform placeholder = EnsureChild(landmarks, spec.LandmarkName);
                placeholder.localPosition = Vector3.zero;
            }
        }

        private Bounds ResolveLayoutBounds(Scene scene, Transform ankaraRoot)
        {
            if (fitToAnkaraCityTerrain)
            {
                if (TryGetTerrainBounds(parentOverride, out Bounds parentBounds))
                {
                    return parentBounds;
                }

                Transform cityRoot = ankaraRoot != null ? ankaraRoot.Find(CityRootName) : null;
                if (TryGetTerrainBounds(cityRoot, out Bounds cityBounds))
                {
                    return cityBounds;
                }

                if (TryGetTerrainBounds(ankaraRoot, out Bounds ankaraBounds))
                {
                    return ankaraBounds;
                }

                if (TryGetSelectedTerrainBounds(out Bounds selectionBounds))
                {
                    return selectionBounds;
                }

                Terrain[] activeTerrains = Terrain.activeTerrains;
                if (TryGetTerrainBounds(activeTerrains, out Bounds activeBounds))
                {
                    return activeBounds;
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
