using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public class RoadsideBuildingPlacementTool : EditorWindow
    {
        private const string BuildingsFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Buildings";
        private const string NaturesFolder = "Assets/ThirdParty/LowPolyCity/Prefab/Natures";
        private const string GeneratedRootName = "Generated Roadside Buildings";

        private enum RoadSideMode
        {
            Both,
            Left,
            Right
        }

        [Header("General Settings")]
        [SerializeField] private Transform outputParent;
        [SerializeField] private RoadSideMode sideMode = RoadSideMode.Both;
        [SerializeField] private int seed = 3481;
        [SerializeField] private bool useSelectedRoadsOnly = true;
        [SerializeField] private bool clearBeforePopulate = true;

        [Header("Lot & Road Layout")]
        [SerializeField] private float roadHalfWidth = 4.25f;
        [SerializeField] private float sidewalkWidth = 2.2f;
        [SerializeField] private float buildingSetback = 1.5f;
        [SerializeField] private float lotGap = 0.05f;
        [SerializeField] private float startMargin = 8f;
        [SerializeField] private float endMargin = 8f;
        [SerializeField] private float intersectionBuildingClearance = 25f;
        [SerializeField] private float footprintPadding = 1.0f;
        [SerializeField] private float minFrontage = 2.5f;
        [SerializeField] private float maxFrontage = 24f;
        [SerializeField] private float maxGroundSlopeDelta = 2.8f;
        [SerializeField] private float groundOffset = 0.02f;
        [SerializeField] private float yawJitter = 1.5f;
        [SerializeField] private float buildingYawOffset;

        [Header("Building Colliders")]
        [SerializeField] private bool autoAddBoxColliders = true;
        [SerializeField] private Vector3 colliderPadding = Vector3.zero;
        [SerializeField] private Vector3 minimumColliderSize = new Vector3(1.0f, 1.5f, 1.0f);

        [Header("Trees & Greenery")]
        [SerializeField] private bool placeTrees = true;
        [SerializeField] private float treeChance = 0.60f;
        [SerializeField] private float treeOffsetFromRoad = 6.6f;
        [SerializeField] private float treeScaleMin = 0.85f;
        [SerializeField] private float treeScaleMax = 1.25f;

        [SerializeField] private bool placeGreeneryBetweenBuildings = true;
        [SerializeField] private float betweenBuildingsGreeneryChance = 0.80f;
        [SerializeField] private float greeneryScaleMin = 0.75f;
        [SerializeField] private float greeneryScaleMax = 1.25f;
        [SerializeField] private float intersectionGreeneryChance = 0.85f;

        private readonly List<GameObject> buildingPrefabs = new List<GameObject>();
        private readonly List<GameObject> treePrefabs = new List<GameObject>();
        private readonly List<GameObject> greeneryPrefabs = new List<GameObject>();

        private readonly List<RoadIntersection> cachedIntersections = new List<RoadIntersection>();
        private readonly List<SplineRoad> cachedAllRoads = new List<SplineRoad>();

        private Vector2 scrollPosition;

        [MenuItem("Tools/Delivery Driver/City/Roadside Building Placement")]
        public static void Open()
        {
            RoadsideBuildingPlacementTool window = GetWindow<RoadsideBuildingPlacementTool>("Roadside Buildings");
            window.minSize = new Vector2(430f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDefaultPrefabs();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Roadside Building Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Secili SplineRoad yollarinin iki yanina binalari yan yana (sik aralikli) dizer, kavsaklara tasmasini engeller, aralara agac ve yesillik yerlestirir ve binalara BoxCollider ekler.", MessageType.Info);

            outputParent = (Transform)EditorGUILayout.ObjectField("Output Parent", outputParent, typeof(Transform), true);
            sideMode = (RoadSideMode)EditorGUILayout.EnumPopup("Road Side", sideMode);
            useSelectedRoadsOnly = EditorGUILayout.Toggle("Use Selected Roads Only", useSelectedRoadsOnly);
            clearBeforePopulate = EditorGUILayout.Toggle("Clear Before Populate", clearBeforePopulate);
            seed = EditorGUILayout.IntField("Random Seed", seed);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Lot & Road Layout", EditorStyles.boldLabel);
            roadHalfWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Road Half Width", roadHalfWidth));
            sidewalkWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Sidewalk Width", sidewalkWidth));
            buildingSetback = Mathf.Max(0f, EditorGUILayout.FloatField("Building Setback", buildingSetback));
            lotGap = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Lot Gap (Bina Araligi)", "Binalar arasindaki bosluk. Kucuk degerler binalari yan yana bitisik yapar."), lotGap));
            startMargin = Mathf.Max(0f, EditorGUILayout.FloatField("Start Margin", startMargin));
            endMargin = Mathf.Max(0f, EditorGUILayout.FloatField("End Margin", endMargin));
            intersectionBuildingClearance = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Intersection Clearance", "Kavsak merkezinden itibaren bina olusturulmayacak guvenli yaricap."), intersectionBuildingClearance));
            footprintPadding = Mathf.Max(0.5f, EditorGUILayout.FloatField("Footprint Padding", footprintPadding));
            minFrontage = Mathf.Max(1f, EditorGUILayout.FloatField("Min Frontage (m)", minFrontage));
            maxFrontage = Mathf.Max(minFrontage, EditorGUILayout.FloatField("Max Frontage (m)", maxFrontage));
            maxGroundSlopeDelta = Mathf.Max(0.1f, EditorGUILayout.FloatField("Max Ground Slope Delta", maxGroundSlopeDelta));
            groundOffset = Mathf.Max(0f, EditorGUILayout.FloatField("Ground Offset", groundOffset));
            yawJitter = Mathf.Max(0f, EditorGUILayout.FloatField("Yaw Jitter (deg)", yawJitter));
            buildingYawOffset = EditorGUILayout.FloatField("Building Yaw Offset", buildingYawOffset);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Building Colliders", EditorStyles.boldLabel);
            autoAddBoxColliders = EditorGUILayout.Toggle(new GUIContent("Auto Add BoxColliders", "Yerlestirilen binalara otomatik BoxCollider ekler."), autoAddBoxColliders);
            using (new EditorGUI.DisabledScope(!autoAddBoxColliders))
            {
                colliderPadding = EditorGUILayout.Vector3Field("Collider Padding", colliderPadding);
                minimumColliderSize = EditorGUILayout.Vector3Field("Minimum Collider Size", minimumColliderSize);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Trees & Greenery (Agac ve Yesillikler)", EditorStyles.boldLabel);
            placeTrees = EditorGUILayout.Toggle("Place Roadside Trees", placeTrees);
            using (new EditorGUI.DisabledScope(!placeTrees))
            {
                treeChance = EditorGUILayout.Slider("Tree Chance", treeChance, 0f, 1f);
                treeOffsetFromRoad = Mathf.Max(0f, EditorGUILayout.FloatField("Tree Offset From Road", treeOffsetFromRoad));
                treeScaleMin = Mathf.Max(0.1f, EditorGUILayout.FloatField("Tree Scale Min", treeScaleMin));
                treeScaleMax = Mathf.Max(treeScaleMin, EditorGUILayout.FloatField("Tree Scale Max", treeScaleMax));
            }

            EditorGUILayout.Space(3f);
            placeGreeneryBetweenBuildings = EditorGUILayout.Toggle(new GUIContent("Greenery Between Buildings", "Bina aralarina cali, saksi bitkisi ve yesillikler yerlestirir."), placeGreeneryBetweenBuildings);
            using (new EditorGUI.DisabledScope(!placeGreeneryBetweenBuildings))
            {
                betweenBuildingsGreeneryChance = EditorGUILayout.Slider("Between Buildings Chance", betweenBuildingsGreeneryChance, 0f, 1f);
                intersectionGreeneryChance = EditorGUILayout.Slider("Intersection Greenery Chance", intersectionGreeneryChance, 0f, 1f);
                greeneryScaleMin = Mathf.Max(0.1f, EditorGUILayout.FloatField("Greenery Scale Min", greeneryScaleMin));
                greeneryScaleMax = Mathf.Max(greeneryScaleMin, EditorGUILayout.FloatField("Greenery Scale Max", greeneryScaleMax));
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload Prefabs"))
                {
                    LoadDefaultPrefabs();
                }

                if (GUILayout.Button("Use Selection As Parent"))
                {
                    outputParent = Selection.activeTransform;
                }
            }

            EditorGUILayout.LabelField($"Building Prefabs: {buildingPrefabs.Count} | Trees: {treePrefabs.Count} | Greenery/Bushes: {greeneryPrefabs.Count}");

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Populate Roads", GUILayout.Height(36f)))
            {
                PopulateRoads();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Colliders To Existing Buildings", GUILayout.Height(26f)))
                {
                    AddCollidersToExistingBuildings();
                }

                if (GUILayout.Button("Clear Generated Buildings", GUILayout.Height(26f)))
                {
                    ClearGeneratedRoot();
                }
            }

            EditorGUILayout.Space(6f);
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("Add Colliders To All Buildings In Scene (Tum Binalara Collider Ekle)", GUILayout.Height(32f)))
            {
                AddCollidersToExistingBuildings();
            }

            EditorGUILayout.Space(4f);
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Remove All Natures_Grass Fence (Sahnedeki Tum Citleri Sil)", GUILayout.Height(34f)))
            {
                RemoveAllGrassFencesFromScene();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.65f, 0.9f, 1f);
                if (GUILayout.Button("Align Grass Fences Parallel (Citleri Yola Paralel Yap)", GUILayout.Height(28f)))
                {
                    AlignGrassFencesParallelToRoad();
                }

                GUI.backgroundColor = new Color(1f, 0.65f, 0.65f);
                if (GUILayout.Button("Clean Colliders From Roads", GUILayout.Height(28f)))
                {
                    RemoveCollidersFromRoads();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        private void PopulateRoads()
        {
            if (buildingPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Roadside Buildings", "Building prefab bulunamadi. Reload Prefabs butonunu deneyin.", "OK");
                return;
            }

            List<SplineRoad> roads = CollectRoads();
            if (roads.Count == 0)
            {
                EditorUtility.DisplayDialog("Roadside Buildings", "Yerlesim yapilacak SplineRoad bulunamadi. Lutfen sahnede SplineRoad secin veya Use Selected Roads Only secenegini kapatin.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform root = ResolveOutputRoot(scene);
            if (clearBeforePopulate)
            {
                ClearGeneratedChildren(root);
                if (outputParent != null && outputParent != root)
                {
                    ClearGeneratedChildren(outputParent);
                }
            }

            // Cache all scene intersections and roads once for fast avoidance tests
            CacheSceneIntersectionsAndRoads();

            Random.State previousState = Random.state;
            Random.InitState(seed);

            int placedBuildings = 0;
            int placedTrees = 0;
            int placedGreenery = 0;

            for (int i = 0; i < roads.Count; i++)
            {
                SplineRoad road = roads[i];
                if (road == null || !road.IsUsable)
                {
                    continue;
                }

                GameObject roadRoot = new GameObject($"Buildings_{road.name}");
                Undo.RegisterCreatedObjectUndo(roadRoot, "Create Roadside Building Group");
                roadRoot.transform.SetParent(root, false);

                if (sideMode == RoadSideMode.Both || sideMode == RoadSideMode.Right)
                {
                    PlaceAlongRoad(road, roadRoot.transform, 1f, ref placedBuildings, ref placedTrees, ref placedGreenery);
                }

                if (sideMode == RoadSideMode.Both || sideMode == RoadSideMode.Left)
                {
                    PlaceAlongRoad(road, roadRoot.transform, -1f, ref placedBuildings, ref placedTrees, ref placedGreenery);
                }
            }

            Random.state = previousState;
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[RoadsideBuildingPlacementTool] Placed {placedBuildings} buildings, {placedTrees} trees, and {placedGreenery} greenery objects along {roads.Count} road(s).");
        }

        private void CacheSceneIntersectionsAndRoads()
        {
            cachedIntersections.Clear();
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (intersections != null)
            {
                cachedIntersections.AddRange(intersections);
            }

            cachedAllRoads.Clear();
            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allRoads != null)
            {
                for (int i = 0; i < allRoads.Length; i++)
                {
                    if (allRoads[i] != null && allRoads[i].IsUsable)
                    {
                        cachedAllRoads.Add(allRoads[i]);
                    }
                }
            }
        }

        private void PlaceAlongRoad(SplineRoad road, Transform parent, float side, ref int placedBuildings, ref int placedTrees, ref int placedGreenery)
        {
            float length = road.GetTotalLength();
            float distance = Mathf.Min(startMargin, length);
            float maxDistance = Mathf.Max(0f, length - endMargin);

            while (distance < maxDistance)
            {
                GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Count)];
                Vector2 footprint = GetPrefabFootprint(prefab, out float frontOffset);
                float frontage = Mathf.Clamp(footprint.x * footprintPadding, minFrontage, maxFrontage);
                float depth = Mathf.Max(footprint.y * footprintPadding, minFrontage);
                float centerDistance = distance + frontage * 0.5f;

                if (centerDistance >= maxDistance)
                {
                    break;
                }

                // 1. Yol mesafesi seviyesinde kavşak kontrolü
                if (IsInsideIntersectionClearance(road, centerDistance, frontage * 0.5f))
                {
                    // Kavşak boşluk alanına yeşillik / ağaç serpiştirme
                    if (placeGreeneryBetweenBuildings && (greeneryPrefabs.Count > 0 || treePrefabs.Count > 0) && Random.value <= intersectionGreeneryChance)
                    {
                        TryPlaceRoadsidePlant(road, parent, centerDistance, side, ref placedGreenery, ref placedTrees);
                    }

                    // Bir sonraki noktayı aramak için küçük adımla ilerle
                    distance += Mathf.Max(2.5f, frontage * 0.5f);
                    continue;
                }

                // 2. Pozisyon oluşturma (frontOffset ile ön cephe yoldan tam güvenli mesafede tutulur)
                if (TryBuildPose(road, centerDistance, side, depth, frontOffset, out Vector3 position, out Quaternion rotation))
                {
                    // 3. Dünya koordinatı seviyesinde kavşak ve diğer yollarla çakışma testi
                    if (IsPositionBlockedByIntersectionsOrRoads(road, position, frontage, depth))
                    {
                        // Bu pozisyon bir kavşağın içine veya kesişen başka bir yolun asfaltına taşıyor!
                        distance += Mathf.Max(2.5f, frontage * 0.5f);
                        continue;
                    }

                    // Bina yerleştir
                    GameObject instance = InstantiatePrefab(prefab, parent);
                    if (instance != null)
                    {
                        Undo.RegisterCreatedObjectUndo(instance, "Place Roadside Building");
                        instance.transform.SetPositionAndRotation(position, rotation);
                        instance.isStatic = true;
                        placedBuildings++;

                        if (autoAddBoxColliders)
                        {
                            EnsureBuildingCollider(instance);
                        }
                    }

                    float lotEndDistance = distance + frontage;

                    // 4. Binaların arasına sık çalı/yeşillik koyma
                    if (placeGreeneryBetweenBuildings && greeneryPrefabs.Count > 0 && Random.value <= betweenBuildingsGreeneryChance)
                    {
                        float greeneryDistance = Mathf.Clamp(lotEndDistance + (lotGap * 0.5f), startMargin, maxDistance);
                        TryPlaceGreeneryBetweenBuildings(road, parent, greeneryDistance, side, depth, ref placedGreenery);
                    }

                    // 5. Yol kenarı kaldırım ağacı koyma
                    if (placeTrees && treePrefabs.Count > 0 && Random.value <= treeChance)
                    {
                        float treeDistance = Mathf.Clamp(lotEndDistance + (lotGap * 0.5f), startMargin, maxDistance);
                        TryPlaceTree(road, parent, treeDistance, side, ref placedTrees);
                    }

                    // Binalar birbirine lotGap kadar bitişik ardışık dizilir
                    distance += frontage + lotGap;
                }
                else
                {
                    // Zemin eğimi veya snap nedeniyle pose bulunamadıysa küçük adımla devam et
                    distance += Mathf.Max(2.0f, frontage * 0.4f);
                }
            }
        }

        private bool TryPlaceTree(SplineRoad road, Transform parent, float distance, float side, ref int placedTrees)
        {
            if (treePrefabs.Count == 0)
            {
                return false;
            }

            if (!TryBuildTreePose(road, distance, side, out Vector3 treePosition, out Quaternion treeRotation))
            {
                return false;
            }

            GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
            GameObject tree = InstantiatePrefab(treePrefab, parent);
            if (tree == null)
            {
                return false;
            }

            Undo.RegisterCreatedObjectUndo(tree, "Place Roadside Tree");
            tree.transform.SetPositionAndRotation(treePosition, treeRotation);
            float scale = Random.Range(treeScaleMin, treeScaleMax);
            tree.transform.localScale *= scale;
            tree.isStatic = true;
            placedTrees++;
            return true;
        }

        private bool TryPlaceGreeneryBetweenBuildings(SplineRoad road, Transform parent, float distance, float side, float buildingDepth, ref int placedGreenery)
        {
            if (greeneryPrefabs.Count == 0)
            {
                return false;
            }

            float length = road.GetTotalLength();
            if (distance <= startMargin || distance >= length - endMargin)
            {
                return false;
            }

            float t = road.DistanceToT(distance);
            Vector3 center = road.GetPoint(t);
            Vector3 tangent = road.GetTangent(t);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;

            GameObject prefab = greeneryPrefabs[Random.Range(0, greeneryPrefabs.Count)];
            string pName = prefab.name.ToLowerInvariant();
            bool isFenceOrBar = pName.Contains("fence") || pName.Contains("bar");

            // Çit veya bar ise kaldırım kenarına paralel, doğal çalı ise bina arasına yerleştir
            float lateralOffset = isFenceOrBar
                ? roadHalfWidth + sidewalkWidth + 0.35f
                : roadHalfWidth + sidewalkWidth + Random.Range(0.2f, buildingSetback + 0.5f);
            Vector3 position = center + right * side * lateralOffset;

            if (!TrySnapToGround(position, out position))
            {
                return false;
            }

            GameObject instance = InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Between-Building Greenery");

            Quaternion rotation;
            if (isFenceOrBar)
            {
                // Yola tam paralel: modelin uzun kenarını yol teğetine (tangent) paralel yapıyoruz
                Quaternion roadFacing = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                rotation = roadFacing * Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            float scale = isFenceOrBar ? 1.0f : Random.Range(greeneryScaleMin, greeneryScaleMax);
            instance.transform.localScale *= scale;
            instance.isStatic = true;
            placedGreenery++;
            return true;
        }

        private bool TryPlaceRoadsidePlant(SplineRoad road, Transform parent, float distance, float side, ref int placedGreenery, ref int placedTrees)
        {
            if (greeneryPrefabs.Count > 0 && (treePrefabs.Count == 0 || Random.value <= 0.6f))
            {
                return TryPlaceGreeneryBetweenBuildings(road, parent, distance, side, 4f, ref placedGreenery);
            }
            else if (treePrefabs.Count > 0)
            {
                return TryPlaceTree(road, parent, distance, side, ref placedTrees);
            }

            return false;
        }

        private bool IsInsideIntersectionClearance(SplineRoad road, float distance, float extraClearance)
        {
            if (road == null || intersectionBuildingClearance <= 0f)
            {
                return false;
            }

            float clearance = intersectionBuildingClearance + Mathf.Max(0f, extraClearance);
            float length = road.GetTotalLength();

            // Yolun uç noktaları (Start / End)
            if (distance <= clearance)
            {
                if (road.startIntersection != null || IsNearAnyIntersection(road.GetPoint(0f), clearance))
                {
                    return true;
                }
            }

            if (length - distance <= clearance)
            {
                if (road.endIntersection != null || IsNearAnyIntersection(road.GetPoint(1f), clearance))
                {
                    return true;
                }
            }

            float t = road.DistanceToT(distance);
            Vector3 point = road.GetPoint(t);
            return IsNearAnyIntersection(point, clearance);
        }

        private bool IsNearAnyIntersection(Vector3 point, float clearance)
        {
            float clearanceSqr = clearance * clearance;
            for (int i = 0; i < cachedIntersections.Count; i++)
            {
                RoadIntersection intersection = cachedIntersections[i];
                if (intersection == null)
                {
                    continue;
                }

                Vector3 delta = point - intersection.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= clearanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPositionBlockedByIntersectionsOrRoads(SplineRoad currentRoad, Vector3 position, float frontage, float depth)
        {
            float buildingRadius = Mathf.Max(frontage, depth) * 0.5f;

            // 1. Herhangi bir kavşak merkezine mesafe testi (Bina pozisyonu üzerinden)
            float clearance = intersectionBuildingClearance + buildingRadius;
            float clearanceSqr = clearance * clearance;

            for (int i = 0; i < cachedIntersections.Count; i++)
            {
                RoadIntersection intersection = cachedIntersections[i];
                if (intersection == null)
                {
                    continue;
                }

                Vector3 delta = position - intersection.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= clearanceSqr)
                {
                    return true; // Kavşağın içine veya çok yakınına giriyor
                }
            }

            // 2. Sahnedeki DİĞER SplineRoad hatlarına yakınlık testi (Kavşaktan çıkan kollar veya yan yollar)
            float minDistanceToRoad = roadHalfWidth + sidewalkWidth + 1.2f;
            float minDistanceSqr = minDistanceToRoad * minDistanceToRoad;

            for (int i = 0; i < cachedAllRoads.Count; i++)
            {
                SplineRoad otherRoad = cachedAllRoads[i];
                if (otherRoad == null || !otherRoad.IsUsable || otherRoad == currentRoad)
                {
                    continue;
                }

                float closestT = otherRoad.GetClosestT(position, 20);
                Vector3 roadPoint = otherRoad.GetPoint(closestT);
                Vector3 diff = position - roadPoint;
                diff.y = 0f;

                if (diff.sqrMagnitude < minDistanceSqr)
                {
                    return true; // Başka bir yolun asfaltına veya kaldırımına taşıyor
                }
            }

            return false;
        }

        private bool TryBuildPose(SplineRoad road, float distance, float side, float buildingDepth, float frontOffset, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            float t = road.DistanceToT(distance);
            Vector3 center = road.GetPoint(t);
            Vector3 tangent = road.GetTangent(t);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            Vector3 faceDirection = -right * side;

            // Ön cephenin yol kenarindan (asfalttan) uzakligi: sidewalkWidth + buildingSetback
            // Model pivotunun yol merkezine mesafesi: roadHalfWidth + sidewalkWidth + buildingSetback + frontOffset
            float forwardExtent = frontOffset > 0.01f ? frontOffset : (buildingDepth * 0.5f);
            float lateralOffset = roadHalfWidth + sidewalkWidth + buildingSetback + forwardExtent;
            position = center + right * side * lateralOffset;

            if (!TrySnapToGround(position, out position))
            {
                return false;
            }

            if (IsGroundTooUneven(position, right, side, buildingDepth))
            {
                return false;
            }

            float yaw = buildingYawOffset + Random.Range(-yawJitter, yawJitter);
            rotation = Quaternion.LookRotation(faceDirection, Vector3.up) * Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        private bool TryBuildTreePose(SplineRoad road, float distance, float side, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            float t = road.DistanceToT(distance);
            Vector3 center = road.GetPoint(t);
            Vector3 right = road.GetRight(t);
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            position = center + right.normalized * side * treeOffsetFromRoad;
            if (!TrySnapToGround(position, out position))
            {
                return false;
            }

            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            return true;
        }

        private bool IsGroundTooUneven(Vector3 center, Vector3 right, float side, float depth)
        {
            Vector3 inner = center - right.normalized * side * depth * 0.4f;
            Vector3 outer = center + right.normalized * side * depth * 0.4f;
            if (!TrySnapToGround(inner, out Vector3 innerGround) || !TrySnapToGround(outer, out Vector3 outerGround))
            {
                return false;
            }

            return Mathf.Abs(innerGround.y - outerGround.y) > maxGroundSlopeDelta;
        }

        private bool TrySnapToGround(Vector3 position, out Vector3 snapped)
        {
            if (TrySampleTerrainHeight(position, out float height))
            {
                snapped = new Vector3(position.x, height + groundOffset, position.z);
                return true;
            }

            Vector3 origin = position + Vector3.up * 120f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 260f))
            {
                snapped = hit.point + Vector3.up * groundOffset;
                return true;
            }

            snapped = position;
            return false;
        }

        private bool TrySampleTerrainHeight(Vector3 position, out float height)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                bool insideX = position.x >= terrainPosition.x && position.x <= terrainPosition.x + terrainSize.x;
                bool insideZ = position.z >= terrainPosition.z && position.z <= terrainPosition.z + terrainSize.z;
                if (!insideX || !insideZ)
                {
                    continue;
                }

                height = terrainPosition.y + terrain.SampleHeight(position);
                return true;
            }

            height = position.y;
            return false;
        }

        private List<SplineRoad> CollectRoads()
        {
            List<SplineRoad> roads = new List<SplineRoad>();
            if (useSelectedRoadsOnly)
            {
                for (int i = 0; i < Selection.transforms.Length; i++)
                {
                    AddRoadsFromSelection(Selection.transforms[i], roads);
                }

                return roads;
            }

            roads.AddRange(Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            return roads;
        }

        private void AddRoadsFromSelection(Transform selected, List<SplineRoad> roads)
        {
            if (selected == null)
            {
                return;
            }

            AddRoad(selected.GetComponent<SplineRoad>(), roads);
            AddRoadsFromIntersections(selected, roads);

            SplineRoad[] parentRoads = selected.GetComponentsInParent<SplineRoad>(true);
            for (int i = 0; i < parentRoads.Length; i++)
            {
                AddRoad(parentRoads[i], roads);
            }

            SplineRoad[] childRoads = selected.GetComponentsInChildren<SplineRoad>(true);
            for (int i = 0; i < childRoads.Length; i++)
            {
                AddRoad(childRoads[i], roads);
            }

            Transform cursor = selected.parent;
            while (cursor != null)
            {
                SplineRoad[] siblingScopeRoads = cursor.GetComponentsInChildren<SplineRoad>(true);
                for (int i = 0; i < siblingScopeRoads.Length; i++)
                {
                    AddRoad(siblingScopeRoads[i], roads);
                }

                if (roads.Count > 0)
                {
                    return;
                }

                cursor = cursor.parent;
            }
        }

        private void AddRoadsFromIntersections(Transform selected, List<SplineRoad> roads)
        {
            RoadIntersection[] intersections = selected.GetComponentsInChildren<RoadIntersection>(true);
            for (int i = 0; i < intersections.Length; i++)
            {
                RoadIntersection intersection = intersections[i];
                if (intersection == null || intersection.connectedRoads == null)
                {
                    continue;
                }

                for (int roadIndex = 0; roadIndex < intersection.connectedRoads.Count; roadIndex++)
                {
                    AddRoad(intersection.connectedRoads[roadIndex], roads);
                }
            }
        }

        private void AddRoad(SplineRoad road, List<SplineRoad> roads)
        {
            if (road != null && road.IsUsable && !roads.Contains(road))
            {
                roads.Add(road);
            }
        }

        private GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            Object instance = PrefabUtility.InstantiatePrefab(prefab, parent);
            return instance as GameObject;
        }

        private Vector2 GetPrefabFootprint(GameObject prefab, out float frontOffset)
        {
            frontOffset = 0f;
            if (prefab == null)
            {
                return new Vector2(minFrontage, minFrontage);
            }

            if (TryCalculatePreciseLocalBounds(prefab.transform, out Bounds localBounds))
            {
                // Unity LookRotation faceDirection: Yerel X cephe genisligi (frontage), yerel Z derinlik (depth)
                float width = Mathf.Abs(localBounds.size.x);
                float length = Mathf.Abs(localBounds.size.z);
                float frontage = Mathf.Max(minFrontage, width);
                float depth = Mathf.Max(minFrontage, length);
                // frontOffset: objenin yerel +Z ekseninde pivot'tan öne (yola) doğru uzandığı maksimum mesafe
                frontOffset = Mathf.Max(0f, localBounds.max.z);
                return new Vector2(frontage, depth);
            }

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return new Vector2(minFrontage, minFrontage);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float bWidth = Mathf.Max(minFrontage, bounds.size.x);
            float bDepth = Mathf.Max(minFrontage, bounds.size.z);
            frontOffset = bDepth * 0.5f;
            return new Vector2(bWidth, bDepth);
        }

        #region Building Collider Support
        private void EnsureBuildingCollider(GameObject instance, bool forceUpdate = false)
        {
            if (instance == null)
            {
                return;
            }

            BoxCollider box = instance.GetComponent<BoxCollider>();
            if (!forceUpdate && box != null)
            {
                return;
            }

            if (TryCalculatePreciseLocalBounds(instance.transform, out Bounds localBounds))
            {
                if (box == null)
                {
                    box = Undo.AddComponent<BoxCollider>(instance);
                }

                Undo.RecordObject(box, "Configure Building BoxCollider");
                box.isTrigger = false;
                box.center = localBounds.center;
                // Şişirme yapmadan modelin gerçek sınırlarına tam oturur
                box.size = Vector3.Max(localBounds.size + colliderPadding, minimumColliderSize);
            }
        }

        public void AddCollidersToExistingBuildings()
        {
            Scene scene = SceneManager.GetActiveScene();
            int count = 0;
            List<GameObject> candidates = new List<GameObject>();

            // 1. Kullanıcı sahnede bir parent veya obje seçtiyse önce seçili nesneleri tara
            if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
            {
                for (int i = 0; i < Selection.gameObjects.Length; i++)
                {
                    FindBuildingsRecursive(Selection.gameObjects[i].transform, candidates);
                }
            }

            // 2. Sahnedeki tüm kök objeleri tara
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int r = 0; r < rootObjects.Length; r++)
            {
                GameObject rootGo = rootObjects[r];
                if (rootGo == null) continue;

                // Yol hiyerarşilerini, arabaları veya kameraları atla
                if (rootGo.name == "Generated Spline Roads" || rootGo.name == "Generated Road Intersections") continue;
                if (rootGo.GetComponent<SplineRoad>() != null || rootGo.GetComponent<RoadIntersection>() != null) continue;

                string rLower = rootGo.name.ToLowerInvariant();
                if (rLower.Contains("car") || rLower.Contains("vehicle") || rLower.Contains("player") || rLower.Contains("terrain"))
                {
                    continue;
                }

                FindBuildingsRecursive(rootGo.transform, candidates);
            }

            // 3. Bulunan tüm bina objelerine hassas BoxCollider uygula
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject bld = candidates[i];
                if (bld == null) continue;

                EnsureBuildingCollider(bld, true);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Building Colliders", $"Sahnedeki {count} adet binaya model sinirlarina tam oturan BoxCollider basariyla eklendi/guncellendi.", "OK");
            Debug.Log($"[RoadsideBuildingPlacementTool] Added/updated BoxColliders for {count} buildings.");
        }

        private void FindBuildingsRecursive(Transform current, List<GameObject> results)
        {
            if (current == null) return;
            GameObject go = current.gameObject;

            // Kesinlikle çit, lamba, ağaç, yol, araba, kamera olmamalı
            string lower = go.name.ToLowerInvariant();
            if (lower.Contains("tree") || lower.Contains("bush") || lower.Contains("plant") || lower.Contains("greenery") ||
                lower.Contains("fence") || lower.Contains("bar") || lower.Contains("light") || lower.Contains("lamp") ||
                lower.Contains("prop") || lower.Contains("camera") || lower.Contains("waypoint") || lower.Contains("direction") ||
                lower.StartsWith("splineroad") || lower.StartsWith("roadintersection"))
            {
                return;
            }

            // Eğer bu objenin adı doğrudan bir bina ismiyse ve mesh içeriyorsa listeye ekle
            if (IsBuildingName(lower) && go.GetComponentInChildren<MeshFilter>(true) != null)
            {
                if (!results.Contains(go))
                {
                    results.Add(go);
                }
                return; // Alt objelerine ayrı ayrı collider ekleme, ana binaya ekle
            }

            // Aksi halde child'ları gez
            for (int i = 0; i < current.childCount; i++)
            {
                FindBuildingsRecursive(current.GetChild(i), results);
            }
        }

        private static bool IsBuildingName(string lower)
        {
            return lower.StartsWith("building") ||
                   lower.StartsWith("house") ||
                   lower.StartsWith("shop") ||
                   lower.Contains("residential") ||
                   lower.Contains("bakery") ||
                   lower.Contains("restaurant") ||
                   lower.Contains("fruits") ||
                   lower.Contains("fast food") ||
                   lower.Contains("auto service") ||
                   lower.Contains("pizza") ||
                   lower.Contains("market") ||
                   lower.Contains("store") ||
                   lower.Contains("factory") ||
                   lower.Contains("office") ||
                   lower.Contains("hotel") ||
                   lower.Contains("hospital") ||
                   lower.Contains("bank") ||
                   lower.Contains("pharmacy") ||
                   lower.Contains("gas station") ||
                   lower.Contains("stadium") ||
                   lower.Contains("clothing");
        }

        public void AlignGrassFencesParallelToRoad()
        {
            Scene scene = SceneManager.GetActiveScene();
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (roads.Length == 0)
            {
                EditorUtility.DisplayDialog("Align Fences", "Sahnede SplineRoad bulunamadi.", "OK");
                return;
            }

            int alignedCount = 0;
            GameObject[] rootObjects = scene.GetRootGameObjects();

            for (int r = 0; r < rootObjects.Length; r++)
            {
                GameObject rootGo = rootObjects[r];
                if (rootGo == null) continue;

                Transform[] allChildren = rootGo.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    Transform t = allChildren[i];
                    if (t == null) continue;

                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("grass fence") || lower.Contains("grass bar") || lower.Contains("grass_fence") || lower.Contains("grass_bar"))
                    {
                        Vector3 pos = t.position;
                        SplineRoad bestRoad = null;
                        float bestDistance = float.MaxValue;
                        float bestT = 0f;

                        for (int j = 0; j < roads.Length; j++)
                        {
                            SplineRoad road = roads[j];
                            if (road == null || !road.IsUsable) continue;

                            int samples = Mathf.Max(12, road.SegmentCount * 6);
                            for (int s = 0; s <= samples; s++)
                            {
                                float tVal = s / (float)samples;
                                Vector3 roadPt = road.GetPoint(tVal);
                                float d = Vector3.Distance(pos, roadPt);
                                if (d < bestDistance)
                                {
                                    bestDistance = d;
                                    bestRoad = road;
                                    bestT = tVal;
                                }
                            }
                        }

                        if (bestRoad != null)
                        {
                            Undo.RecordObject(t, "Align Grass Fence Parallel");
                            Vector3 tangent = bestRoad.GetTangent(bestT);
                            tangent.y = 0f;
                            if (tangent.sqrMagnitude > 0.0001f)
                            {
                                // Yola tam paralel hizalama
                                Quaternion roadFacing = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                                t.rotation = roadFacing * Quaternion.Euler(0f, 90f, 0f);
                                alignedCount++;
                            }
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Align Grass Fences", $"Sahnedeki {alignedCount} adet Grass Fence / Bar basariyla yola paralel hizalandi!", "OK");
            Debug.Log($"[RoadsideBuildingPlacementTool] Aligned {alignedCount} grass fence/bar objects parallel to road.");
        }

        private static bool IsLikelyBuilding(GameObject candidate)
        {
            if (candidate == null) return false;
            if (IsRoadOrIntersectionObject(candidate)) return false;

            string lower = candidate.name.ToLowerInvariant();
            if (lower.Contains("tree") || lower.Contains("bush") || lower.Contains("plant") || lower.Contains("greenery") ||
                lower.Contains("fence") || lower.Contains("bar") || lower.Contains("light") || lower.Contains("lamp") ||
                lower.Contains("prop") || lower.Contains("camera") || lower.Contains("spawner") || lower.Contains("manager"))
            {
                return false;
            }

            bool hasBuildingName = lower.StartsWith("building") ||
                                   lower.StartsWith("house") ||
                                   lower.StartsWith("shop") ||
                                   lower.Contains("residential") ||
                                   lower.Contains("bakery") ||
                                   lower.Contains("restaurant") ||
                                   lower.Contains("fruits") ||
                                   lower.Contains("fast food") ||
                                   lower.Contains("auto service") ||
                                   lower.Contains("pizza") ||
                                   lower.Contains("market") ||
                                   lower.Contains("office");

            if (!hasBuildingName) return false;

            return candidate.GetComponentInChildren<MeshFilter>(true) != null;
        }

        public void RemoveCollidersFromRoads()
        {
            Scene scene = SceneManager.GetActiveScene();
            int removedCount = 0;

            // 1. Sahnedeki tüm SplineRoad nesneleri ve altındaki BoxCollider'ları temizle
            SplineRoad[] roads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roads.Length; i++)
            {
                if (roads[i] == null) continue;
                BoxCollider[] boxes = roads[i].GetComponentsInChildren<BoxCollider>(true);
                for (int j = 0; j < boxes.Length; j++)
                {
                    if (boxes[j] != null)
                    {
                        Undo.DestroyObjectImmediate(boxes[j]);
                        removedCount++;
                    }
                }
            }

            // 2. Sahnedeki tüm RoadIntersection nesneleri ve altındaki BoxCollider'ları temizle
            RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < intersections.Length; i++)
            {
                if (intersections[i] == null) continue;
                BoxCollider[] boxes = intersections[i].GetComponentsInChildren<BoxCollider>(true);
                for (int j = 0; j < boxes.Length; j++)
                {
                    if (boxes[j] != null)
                    {
                        Undo.DestroyObjectImmediate(boxes[j]);
                        removedCount++;
                    }
                }
            }

            // 3. Adı "Generated Spline Roads", "Road", "Intersection", "Spline" olan tüm root nesneleri ve altını tara
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject rootGo = rootObjects[i];
                if (rootGo == null) continue;
                string rName = rootGo.name.ToLowerInvariant();
                if (rName.Contains("spline") || rName.Contains("road") || rName.Contains("intersection"))
                {
                    BoxCollider[] boxes = rootGo.GetComponentsInChildren<BoxCollider>(true);
                    for (int j = 0; j < boxes.Length; j++)
                    {
                        if (boxes[j] != null)
                        {
                            Undo.DestroyObjectImmediate(boxes[j]);
                            removedCount++;
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Clean Road Colliders", $"Yollardan ve kavsaklardan {removedCount} adet hatali BoxCollider basariyla temizlendi! Artik araba havada gitmeyecek, asfalta tam oturacak.", "OK");
            Debug.Log($"[RoadsideBuildingPlacementTool] Cleaned {removedCount} BoxColliders from road and intersection objects.");
        }

        public void RemoveAllGrassFencesFromScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int removedCount = 0;

            List<GameObject> toDestroy = new List<GameObject>();
            GameObject[] rootObjects = scene.GetRootGameObjects();

            for (int r = 0; r < rootObjects.Length; r++)
            {
                GameObject rootGo = rootObjects[r];
                if (rootGo == null) continue;

                Transform[] allChildren = rootGo.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    Transform t = allChildren[i];
                    if (t == null) continue;

                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("grass fence") || 
                        lower.Contains("grass_fence") || 
                        lower.Contains("grassfence") || 
                        lower.Contains("natures_grass fence") ||
                        lower.Contains("grass bar") || 
                        lower.Contains("grass_bar") ||
                        lower.Contains("grassbar") ||
                        (lower.Contains("grass") && lower.Contains("fence")))
                    {
                        if (!toDestroy.Contains(t.gameObject))
                        {
                            toDestroy.Add(t.gameObject);
                        }
                    }
                }
            }

            for (int i = toDestroy.Count - 1; i >= 0; i--)
            {
                if (toDestroy[i] != null)
                {
                    Undo.DestroyObjectImmediate(toDestroy[i]);
                    removedCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Grass Fence Temizliği", $"Sahneden toplam {removedCount} adet 'Natures_Grass Fence' ve 'Grass Bar' basariyla silindi!", "OK");
            Debug.Log($"[RoadsideBuildingPlacementTool] Removed {removedCount} grass fence/bar objects across the whole scene.");
        }

        private static bool IsRoadOrIntersectionObject(GameObject candidate)
        {
            if (candidate == null) return false;

            // Eğer isimde building, house, shop, roadside geçiyorsa kesinlikle yol objesi değildir!
            string name = candidate.name.ToLowerInvariant();
            if (name.Contains("building") || name.Contains("house") || name.Contains("shop") || name.Contains("roadside"))
            {
                return false;
            }

            if (candidate.GetComponent<SplineRoad>() != null || candidate.GetComponent<RoadIntersection>() != null) return true;
            if (candidate.GetComponentInParent<SplineRoad>() != null || candidate.GetComponentInParent<RoadIntersection>() != null) return true;

            return name.StartsWith("splineroad") || 
                   name.StartsWith("roadintersection") || 
                   name == "generated spline roads" || 
                   name == "generated road intersections";
        }

        private static bool HasSolidColliderInHierarchy(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCalculatePreciseLocalBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
            {
                return false;
            }

            bool hasBounds = false;
            Matrix4x4 worldToLocal = root.worldToLocalMatrix;

            // 1. MeshFilter'ların sharedMesh orijinal yerel bounds'larını kullan (Dünya rotasyonundan asla etkilenmez)
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters != null && meshFilters.Length > 0)
            {
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    MeshFilter mf = meshFilters[i];
                    if (mf == null || mf.sharedMesh == null)
                    {
                        continue;
                    }

                    Bounds mb = mf.sharedMesh.bounds;
                    if (mb.size.sqrMagnitude < 0.001f)
                    {
                        continue;
                    }

                    // child transform'unu root'un yerel uzayına dönüştüren matris
                    Matrix4x4 localMatrix = worldToLocal * mf.transform.localToWorldMatrix;
                    Vector3 min = mb.min;
                    Vector3 max = mb.max;
                    Vector3[] corners =
                    {
                        new Vector3(min.x, min.y, min.z),
                        new Vector3(min.x, min.y, max.z),
                        new Vector3(min.x, max.y, min.z),
                        new Vector3(min.x, max.y, max.z),
                        new Vector3(max.x, min.y, min.z),
                        new Vector3(max.x, min.y, max.z),
                        new Vector3(max.x, max.y, min.z),
                        new Vector3(max.x, max.y, max.z)
                    };

                    for (int c = 0; c < corners.Length; c++)
                    {
                        Vector3 p = localMatrix.MultiplyPoint3x4(corners[c]);
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(p, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(p);
                        }
                    }
                }
            }

            if (hasBounds)
            {
                return true;
            }

            // 2. MeshFilter yoksa Renderer.localBounds kullan
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                Bounds lb = r.localBounds;
                if (lb.size.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                Matrix4x4 localMatrix = worldToLocal * r.transform.localToWorldMatrix;
                Vector3 min = lb.min;
                Vector3 max = lb.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z)
                };

                for (int c = 0; c < corners.Length; c++)
                {
                    Vector3 p = localMatrix.MultiplyPoint3x4(corners[c]);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(p, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(p);
                    }
                }
            }

            return hasBounds;
        }
        #endregion

        private Transform ResolveOutputRoot(Scene scene)
        {
            if (outputParent != null && !IsRoadNetworkOutputParent(outputParent))
            {
                return outputParent;
            }

            return EnsureGeneratedRoot(scene);
        }

        private bool IsRoadNetworkOutputParent(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.GetComponent<RoadIntersection>() != null || candidate.GetComponent<SplineRoad>() != null)
            {
                return true;
            }

            if (candidate.GetComponentInParent<RoadIntersection>() != null || candidate.GetComponentInParent<SplineRoad>() != null)
            {
                return true;
            }

            string name = candidate.name.ToLowerInvariant();
            if (name.Contains("road") || name.Contains("intersection") || name.Contains("spline"))
            {
                return true;
            }

            bool containsIntersections = candidate.GetComponentsInChildren<RoadIntersection>(true).Length > 0;
            bool containsRoads = candidate.GetComponentsInChildren<SplineRoad>(true).Length > 0;
            return containsIntersections || containsRoads;
        }

        private Transform EnsureGeneratedRoot(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == GeneratedRootName)
                {
                    return rootObject.transform;
                }
            }

            GameObject root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Generated Roadside Buildings Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private void ClearGeneratedRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform root = outputParent != null ? outputParent : null;
            if (root == null)
            {
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    if (rootObject.name == GeneratedRootName)
                    {
                        root = rootObject.transform;
                        break;
                    }
                }
            }

            if (root != null)
            {
                ClearGeneratedChildren(root);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void ClearGeneratedChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name.StartsWith("Buildings_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private void LoadDefaultPrefabs()
        {
            buildingPrefabs.Clear();
            treePrefabs.Clear();
            greeneryPrefabs.Clear();

            string[] buildingGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BuildingsFolder });
            for (int i = 0; i < buildingGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(buildingGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && IsCityBuildingPrefab(prefab.name))
                {
                    buildingPrefabs.Add(prefab);
                }
            }

            string[] natureGuids = AssetDatabase.FindAssets("t:Prefab", new[] { NaturesFolder });
            for (int i = 0; i < natureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(natureGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                if (IsTreePrefab(prefab.name))
                {
                    treePrefabs.Add(prefab);
                }
                else if (IsGreeneryPrefab(prefab.name))
                {
                    greeneryPrefabs.Add(prefab);
                }
            }
        }

        private bool IsCityBuildingPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            string lowerName = prefabName.ToLowerInvariant();
            return lowerName.Contains("house") ||
                   lowerName.Contains("residential") ||
                   lowerName.Contains("shop") ||
                   lowerName.Contains("bakery") ||
                   lowerName.Contains("bar") ||
                   lowerName.Contains("restaurant") ||
                   lowerName.Contains("market") ||
                   lowerName.Contains("pizza") ||
                   lowerName.Contains("coffee") ||
                   lowerName.Contains("clothing") ||
                   lowerName.Contains("drug") ||
                   lowerName.Contains("fast food") ||
                   lowerName.Contains("auto service") ||
                   lowerName.Contains("fruits") ||
                   lowerName.Contains("books") ||
                   lowerName.Contains("chicken") ||
                   lowerName.Contains("gift") ||
                   lowerName.Contains("sky");
        }

        private bool IsTreePrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            string lowerName = prefabName.ToLowerInvariant();
            return lowerName.Contains("tree") || lowerName.Contains("fir");
        }

        private bool IsGreeneryPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            string lowerName = prefabName.ToLowerInvariant();
            if (lowerName.Contains("fence") || lowerName.Contains("bar") || lowerName.Contains("box") || lowerName.Contains("cube"))
            {
                return false;
            }

            return lowerName.Contains("bush") ||
                   lowerName.Contains("pot") ||
                   lowerName.Contains("flower");
        }
    }
}
