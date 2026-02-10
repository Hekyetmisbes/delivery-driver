using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using DeliveryDriver.City;

public class SimplePolyCityRoadGridTool : EditorWindow
{
    private const int North = 1;
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;

    [Header("Scene References")]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private Transform roadParent;
    [SerializeField] private Transform buildingParent;
    [SerializeField] private Transform neighborhoodParent;

    [Header("Grid")]
    [SerializeField] private int gridWidth = 40;
    [SerializeField] private int gridHeight = 40;
    [SerializeField] private float cellSize = 8f;
    [SerializeField] private bool autoCellSizeFromStraightPrefab = true;
    [SerializeField] private float cellSizePadding;
    [SerializeField] private float terrainInset = 4f;
    [SerializeField] private float heightOffset = 0.03f;
    [SerializeField] private bool autoFitGridToTerrain = true;

    [Header("Road Pattern")]
    [SerializeField] private int avenueSpacingX = 5;
    [SerializeField] private int avenueSpacingZ = 5;
    [SerializeField, Range(0f, 0.45f)] private float skipChance = 0.07f;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool randomizeSeedEachBuild;
    [SerializeField] private bool cleanupGaps = true;
    [SerializeField, Range(1, 5)] private int cleanupIterations = 2;

    [Header("SimplePoly Prefabs")]
    [SerializeField] private GameObject straightPrefab;
    [SerializeField] private GameObject cornerPrefab;
    [SerializeField] private GameObject tIntersectionPrefab;
    [SerializeField] private GameObject crossIntersectionPrefab;
    [SerializeField] private GameObject deadEndPrefab;

    [Header("Rotation Offsets (Y)")]
    [SerializeField] private float straightRotationOffset;
    [SerializeField] private float cornerRotationOffset;
    [SerializeField] private float tIntersectionRotationOffset;
    [SerializeField] private float crossRotationOffset;
    [SerializeField] private float deadEndRotationOffset;

    [Header("Buildings")]
    [SerializeField] private bool generateBuildings = true;
    [SerializeField] private bool clearBuildingsBeforeGenerate = true;
    [SerializeField, Range(0f, 1f)] private float buildingSpawnChance = 0.9f;
    [SerializeField] private float buildingSetbackFromRoad = 1.1f;
    [SerializeField] private float buildingTowardRoadOffset = 4f;
    [SerializeField] private float buildingHeightOffset;
    [SerializeField] private bool randomizeBuildingYaw = false;
    [SerializeField] private float buildingRandomYawRange = 6f;
    [SerializeField] private bool randomizeBuildingScale = true;
    [SerializeField] private Vector2 buildingScaleRange = new Vector2(0.95f, 1.08f);
    [SerializeField] private bool autoScaleBuildingsToFitLot = true;
    [SerializeField] private float buildingMinSpacing = 0.4f;
    [SerializeField] private float buildingFootprintPadding = 0.15f;
    [SerializeField] private List<GameObject> buildingPrefabs = new List<GameObject>();

    [Header("Neighborhoods")]
    [SerializeField] private bool generateNeighborhoods = true;
    [SerializeField] private bool clearNeighborhoodsBeforeGenerate = true;
    [SerializeField] private int neighborhoodSize = 3;
    [SerializeField] private float neighborhoodZoneHeight = 10f;
    [SerializeField] private List<string> neighborhoodNames = new List<string>();

    private Vector2 scrollPosition;

    [MenuItem("Tools/SimplePoly/City Road Grid Tool")]
    public static void ShowWindow()
    {
        GetWindow<SimplePolyCityRoadGridTool>("City Road Grid Tool");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("SimplePoly City Grid Road Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawSceneSection();
        EditorGUILayout.Space();
        DrawGridSection();
        EditorGUILayout.Space();
        DrawPatternSection();
        EditorGUILayout.Space();
        DrawPrefabSection();
        EditorGUILayout.Space();
        DrawRotationSection();
        EditorGUILayout.Space();
        DrawBuildingSection();
        EditorGUILayout.Space();
        DrawNeighborhoodSection();
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Load SimplePoly Prefabs", GUILayout.Height(28)))
            {
                AutoLoadPrefabs();
            }

            if (GUILayout.Button("Clear Roads", GUILayout.Height(28)))
            {
                ClearRoads();
            }

            if (GUILayout.Button("Clear Buildings", GUILayout.Height(28)))
            {
                ClearBuildings();
            }

            if (GUILayout.Button("Clear Neighborhoods", GUILayout.Height(28)))
            {
                ClearNeighborhoods();
            }
        }

        if (GUILayout.Button("Generate City (Roads + Buildings + Neighborhoods)", GUILayout.Height(42)))
        {
            GenerateRoadGrid();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneSection()
    {
        EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
        roadParent = (Transform)EditorGUILayout.ObjectField("Road Parent", roadParent, typeof(Transform), true);
        buildingParent = (Transform)EditorGUILayout.ObjectField("Building Parent", buildingParent, typeof(Transform), true);
        neighborhoodParent = (Transform)EditorGUILayout.ObjectField("Neighborhood Parent", neighborhoodParent, typeof(Transform), true);
    }

    private void DrawGridSection()
    {
        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        gridWidth = Mathf.Max(2, EditorGUILayout.IntField("Grid Width", gridWidth));
        gridHeight = Mathf.Max(2, EditorGUILayout.IntField("Grid Height", gridHeight));
        cellSize = Mathf.Max(1f, EditorGUILayout.FloatField("Cell Size", cellSize));
        autoCellSizeFromStraightPrefab = EditorGUILayout.Toggle("Auto Cell Size From Straight", autoCellSizeFromStraightPrefab);
        cellSizePadding = EditorGUILayout.FloatField("Cell Size Padding", cellSizePadding);
        terrainInset = Mathf.Max(0f, EditorGUILayout.FloatField("Terrain Inset", terrainInset));
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);
        autoFitGridToTerrain = EditorGUILayout.Toggle("Auto Fit To Terrain", autoFitGridToTerrain);
    }

    private void DrawPatternSection()
    {
        EditorGUILayout.LabelField("Road Pattern", EditorStyles.boldLabel);
        avenueSpacingX = Mathf.Max(1, EditorGUILayout.IntField("Avenue Spacing X", avenueSpacingX));
        avenueSpacingZ = Mathf.Max(1, EditorGUILayout.IntField("Avenue Spacing Z", avenueSpacingZ));
        skipChance = EditorGUILayout.Slider("Random Skip Chance", skipChance, 0f, 0.45f);
        cleanupGaps = EditorGUILayout.Toggle("Cleanup Gaps", cleanupGaps);
        cleanupIterations = EditorGUILayout.IntSlider("Cleanup Iterations", cleanupIterations, 1, 5);
        randomizeSeedEachBuild = EditorGUILayout.Toggle("Randomize Seed Each Build", randomizeSeedEachBuild);
        randomSeed = EditorGUILayout.IntField("Seed", randomSeed);
    }

    private void DrawPrefabSection()
    {
        EditorGUILayout.LabelField("SimplePoly Prefabs", EditorStyles.boldLabel);
        straightPrefab = (GameObject)EditorGUILayout.ObjectField("Straight", straightPrefab, typeof(GameObject), false);
        cornerPrefab = (GameObject)EditorGUILayout.ObjectField("Corner", cornerPrefab, typeof(GameObject), false);
        tIntersectionPrefab = (GameObject)EditorGUILayout.ObjectField("T Intersection", tIntersectionPrefab, typeof(GameObject), false);
        crossIntersectionPrefab = (GameObject)EditorGUILayout.ObjectField("Cross Intersection", crossIntersectionPrefab, typeof(GameObject), false);
        deadEndPrefab = (GameObject)EditorGUILayout.ObjectField("Dead End (Optional)", deadEndPrefab, typeof(GameObject), false);
    }

    private void DrawRotationSection()
    {
        EditorGUILayout.LabelField("Rotation Offsets (Y)", EditorStyles.boldLabel);
        straightRotationOffset = EditorGUILayout.FloatField("Straight Offset", straightRotationOffset);
        cornerRotationOffset = EditorGUILayout.FloatField("Corner Offset", cornerRotationOffset);
        tIntersectionRotationOffset = EditorGUILayout.FloatField("T Offset", tIntersectionRotationOffset);
        crossRotationOffset = EditorGUILayout.FloatField("Cross Offset", crossRotationOffset);
        deadEndRotationOffset = EditorGUILayout.FloatField("Dead End Offset", deadEndRotationOffset);
    }

    private void DrawBuildingSection()
    {
        EditorGUILayout.LabelField("Buildings", EditorStyles.boldLabel);
        generateBuildings = EditorGUILayout.Toggle("Generate Buildings", generateBuildings);
        clearBuildingsBeforeGenerate = EditorGUILayout.Toggle("Clear Buildings First", clearBuildingsBeforeGenerate);
        buildingSpawnChance = EditorGUILayout.Slider("Spawn Chance", buildingSpawnChance, 0f, 1f);
        buildingSetbackFromRoad = Mathf.Clamp(EditorGUILayout.FloatField("Setback From Road", buildingSetbackFromRoad), 0f, cellSize * 0.49f);
        buildingTowardRoadOffset = Mathf.Max(0f, EditorGUILayout.FloatField("Toward Road Offset", buildingTowardRoadOffset));
        buildingHeightOffset = EditorGUILayout.FloatField("Height Offset", buildingHeightOffset);
        randomizeBuildingYaw = EditorGUILayout.Toggle("Randomize Yaw", randomizeBuildingYaw);
        buildingRandomYawRange = Mathf.Max(0f, EditorGUILayout.FloatField("Yaw Range (+/-)", buildingRandomYawRange));
        randomizeBuildingScale = EditorGUILayout.Toggle("Randomize Scale", randomizeBuildingScale);
        buildingScaleRange = EditorGUILayout.Vector2Field("Scale Range", buildingScaleRange);
        buildingScaleRange.x = Mathf.Clamp(buildingScaleRange.x, 0.05f, 5f);
        buildingScaleRange.y = Mathf.Clamp(buildingScaleRange.y, buildingScaleRange.x, 5f);
        autoScaleBuildingsToFitLot = EditorGUILayout.Toggle("Auto Scale To Fit Lot", autoScaleBuildingsToFitLot);
        buildingMinSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Min Spacing", buildingMinSpacing));
        buildingFootprintPadding = Mathf.Max(0f, EditorGUILayout.FloatField("Footprint Padding", buildingFootprintPadding));

        if (GUILayout.Button("Auto Load Building Prefabs", GUILayout.Height(24)))
        {
            AutoLoadBuildingPrefabs();
        }

        EditorGUILayout.LabelField($"Building Prefabs ({buildingPrefabs.Count})");
        int removeIndex = -1;
        for (int i = 0; i < buildingPrefabs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                buildingPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(buildingPrefabs[i], typeof(GameObject), false);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    removeIndex = i;
                }
            }
        }

        if (removeIndex >= 0)
        {
            buildingPrefabs.RemoveAt(removeIndex);
        }

        if (GUILayout.Button("Add Building Slot", GUILayout.Height(20)))
        {
            buildingPrefabs.Add(null);
        }
    }

    private void DrawNeighborhoodSection()
    {
        EditorGUILayout.LabelField("Neighborhoods", EditorStyles.boldLabel);
        generateNeighborhoods = EditorGUILayout.Toggle("Generate Neighborhoods", generateNeighborhoods);
        clearNeighborhoodsBeforeGenerate = EditorGUILayout.Toggle("Clear Neighborhoods First", clearNeighborhoodsBeforeGenerate);
        neighborhoodSize = Mathf.Max(1, EditorGUILayout.IntField("Neighborhood Size (NxN)", neighborhoodSize));
        neighborhoodZoneHeight = Mathf.Max(1f, EditorGUILayout.FloatField("Zone Height", neighborhoodZoneHeight));

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Neighborhood Names ({neighborhoodNames.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("Auto Fill Names", GUILayout.Width(120)))
            {
                AutoFillNeighborhoodNames();
            }
            if (GUILayout.Button("Clear Names", GUILayout.Width(100)))
            {
                neighborhoodNames.Clear();
            }
        }

        int removeNameIndex = -1;
        for (int i = 0; i < neighborhoodNames.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                neighborhoodNames[i] = EditorGUILayout.TextField($"Name {i + 1}", neighborhoodNames[i]);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    removeNameIndex = i;
                }
            }
        }

        if (removeNameIndex >= 0)
        {
            neighborhoodNames.RemoveAt(removeNameIndex);
        }

        if (GUILayout.Button("Add Neighborhood Name", GUILayout.Height(20)))
        {
            neighborhoodNames.Add("Yeni Mahalle");
        }
    }

    private void GenerateRoadGrid()
    {
        if (straightPrefab == null || cornerPrefab == null || tIntersectionPrefab == null || crossIntersectionPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Prefabs",
                "Straight, Corner, T Intersection and Cross Intersection prefabs must be assigned.",
                "OK");
            return;
        }

        if (targetTerrain == null)
        {
            targetTerrain = FindFirstObjectByType<Terrain>();
        }

        if (roadParent == null)
        {
            GameObject parent = new GameObject("SimplePoly_RoadGrid");
            Undo.RegisterCreatedObjectUndo(parent, "Create Road Parent");
            roadParent = parent.transform;
        }

        if (randomizeSeedEachBuild)
        {
            randomSeed = System.Environment.TickCount;
        }

        Random.InitState(randomSeed);
        ClearRoads();
        EnsureBuildingParent();

        if (clearBuildingsBeforeGenerate)
        {
            ClearBuildings();
        }

        float effectiveCellSize = ResolveCellSize();
        int width = gridWidth;
        int height = gridHeight;
        FitGridToTerrain(ref width, ref height, effectiveCellSize);

        bool[,] roadMask = BuildRoadMask(width, height, cleanupGaps, cleanupIterations);
        int createdCount = 0;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!roadMask[x, z])
                {
                    continue;
                }

                int neighborMask = GetNeighborMask(roadMask, x, z, width, height);
                RoadTileChoice choice = ResolveTile(neighborMask);
                if (choice.prefab == null)
                {
                    continue;
                }

                Vector3 pos = CalculateWorldPosition(x, z, width, height, effectiveCellSize);
                Quaternion rot = Quaternion.Euler(0f, choice.yaw, 0f);

                GameObject instance = PrefabUtility.InstantiatePrefab(choice.prefab, roadParent) as GameObject;
                if (instance == null)
                {
                    instance = Instantiate(choice.prefab, roadParent);
                }

                Undo.RegisterCreatedObjectUndo(instance, "Create Road Tile");
                instance.transform.position = pos;
                instance.transform.rotation = rot;
                createdCount++;
            }
        }

        int createdBuildings = 0;
        if (generateBuildings)
        {
            createdBuildings = GenerateBuildings(roadMask, width, height, effectiveCellSize);
        }

        int createdNeighborhoods = 0;
        if (generateNeighborhoods)
        {
            createdNeighborhoods = GenerateNeighborhoods(width, height, effectiveCellSize);
        }

        Selection.activeTransform = roadParent;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CityRoadGridTool] Generated {createdCount} roads, {createdBuildings} buildings, and {createdNeighborhoods} neighborhoods. Seed: {randomSeed}, Cell Size: {effectiveCellSize:F2}");
    }

    private int GenerateBuildings(bool[,] roadMask, int width, int height, float currentCellSize)
    {
        EnsureBuildingParent();

        if (buildingPrefabs == null || buildingPrefabs.Count == 0)
        {
            Debug.LogWarning("[CityRoadGridTool] Building generation enabled but no building prefabs are assigned.");
            return 0;
        }

        List<GameObject> validPrefabs = new List<GameObject>(buildingPrefabs.Count);
        for (int i = 0; i < buildingPrefabs.Count; i++)
        {
            if (buildingPrefabs[i] != null)
            {
                validPrefabs.Add(buildingPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[CityRoadGridTool] Building generation skipped because all building prefab slots are empty.");
            return 0;
        }

        int invalidPrefabSkips = 0;
        int noNeighborSkips = 0;
        int chanceSkips = 0;
        int tooLargeSkips = 0;
        int overlapSkips = 0;

        int created = 0;
        float halfCell = currentCellSize * 0.5f;
        float roadClearance = Mathf.Max(0f, buildingSetbackFromRoad);
        float safeHalfCell = Mathf.Max(0.01f, halfCell - roadClearance);
        List<PlacedBuildingInfo> placedBuildings = new List<PlacedBuildingInfo>();

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (roadMask[x, z])
                {
                    continue;
                }

                int roadNeighbors = GetNeighborMask(roadMask, x, z, width, height);
                if (roadNeighbors == 0)
                {
                    noNeighborSkips++;
                    continue;
                }

                if (Random.value > buildingSpawnChance)
                {
                    chanceSkips++;
                    continue;
                }

                if (!TryPickFittingBuilding(validPrefabs, safeHalfCell, currentCellSize, out GameObject prefab, out float scale, out float radius))
                {
                    // Skip lots where no assigned prefab can fit.
                    tooLargeSkips++;
                    continue;
                }

                Vector3 lotCenter = CalculateWorldPosition(x, z, width, height, currentCellSize);
                Vector3 toRoadDirection = GetDirectionToRoad(roadNeighbors);
                Vector3 spawnPos = lotCenter;
                float maxTowardRoadOffset = Mathf.Max(0f, safeHalfCell - radius);
                float towardRoadOffset = Mathf.Min(buildingTowardRoadOffset, maxTowardRoadOffset);
                spawnPos += toRoadDirection * towardRoadOffset;
                spawnPos.y = SampleY(spawnPos.x, spawnPos.z, buildingHeightOffset);

                if (IsOverlappingPlacedBuildings(spawnPos, radius, placedBuildings))
                {
                    overlapSkips++;
                    continue;
                }

                float yaw = Mathf.Atan2(toRoadDirection.x, toRoadDirection.z) * Mathf.Rad2Deg;
                if (randomizeBuildingYaw && buildingRandomYawRange > 0f)
                {
                    yaw += Random.Range(-buildingRandomYawRange, buildingRandomYawRange);
                }

                GameObject instance = InstantiateForScene(prefab, buildingParent);
                if (instance == null)
                {
                    continue;
                }

                if (!HasRenderableGeometry(instance))
                {
                    Undo.DestroyObjectImmediate(instance);
                    invalidPrefabSkips++;
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Create Building");
                instance.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, yaw, 0f));

                instance.transform.localScale *= scale;

                placedBuildings.Add(new PlacedBuildingInfo(new Vector2(spawnPos.x, spawnPos.z), radius));
                created++;
            }
        }

        if (invalidPrefabSkips > 0)
        {
            Debug.LogWarning($"[CityRoadGridTool] Skipped {invalidPrefabSkips} spawned buildings because instantiated object had no renderable geometry.");
        }

        Debug.Log(
            $"[CityRoadGridTool] Building pass summary: created={created}, " +
            $"noRoadNeighbor={noNeighborSkips}, chanceSkip={chanceSkips}, tooLarge={tooLargeSkips}, overlap={overlapSkips}, invalidPrefab={invalidPrefabSkips}, validPrefabs={validPrefabs.Count}");

        return created;
    }

    private float EstimateBuildingRadius(GameObject prefab, float scale, float fallbackCellSize)
    {
        if (prefab != null && TryGetPrefabFootprint(prefab, out Vector2 footprint))
        {
            // Use max axis half-extent instead of diagonal radius to avoid over-rejecting valid buildings.
            float paddedX = footprint.x * scale + buildingFootprintPadding * 2f;
            float paddedZ = footprint.y * scale + buildingFootprintPadding * 2f;
            return Mathf.Max(paddedX, paddedZ) * 0.5f;
        }

        float fallbackSize = fallbackCellSize * 0.45f;
        return fallbackSize * 0.5f;
    }

    private bool TryPickFittingBuilding(
        List<GameObject> validPrefabs,
        float safeHalfCell,
        float fallbackCellSize,
        out GameObject selectedPrefab,
        out float selectedScale,
        out float selectedRadius)
    {
        List<BuildingChoice> choices = new List<BuildingChoice>(validPrefabs.Count);

        for (int i = 0; i < validPrefabs.Count; i++)
        {
            GameObject prefab = validPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            float scale = randomizeBuildingScale ? Random.Range(buildingScaleRange.x, buildingScaleRange.y) : 1f;
            if (autoScaleBuildingsToFitLot)
            {
                float maxScale = GetMaxScaleToFit(prefab, safeHalfCell, fallbackCellSize);
                if (maxScale <= 0f)
                {
                    continue;
                }

                scale = Mathf.Min(scale, maxScale);
            }

            float radius = EstimateBuildingRadius(prefab, scale, fallbackCellSize);
            if (radius >= safeHalfCell)
            {
                continue;
            }

            choices.Add(new BuildingChoice(prefab, scale, radius));
        }

        if (choices.Count == 0)
        {
            selectedPrefab = null;
            selectedScale = 1f;
            selectedRadius = 0f;
            return false;
        }

        BuildingChoice choice = choices[Random.Range(0, choices.Count)];
        selectedPrefab = choice.prefab;
        selectedScale = choice.scale;
        selectedRadius = choice.radius;
        return true;
    }

    private float GetMaxScaleToFit(GameObject prefab, float safeHalfCell, float fallbackCellSize)
    {
        if (prefab != null && TryGetPrefabFootprint(prefab, out Vector2 footprint))
        {
            float maxFootprint = Mathf.Max(footprint.x, footprint.y);
            if (maxFootprint <= 0.001f)
            {
                return 1f;
            }

            float allowedWidth = safeHalfCell * 2f - buildingFootprintPadding * 2f;
            if (allowedWidth <= 0.001f)
            {
                return 0f;
            }

            return Mathf.Clamp(allowedWidth / maxFootprint, 0f, 5f);
        }

        float fallbackRadius = EstimateBuildingRadius(prefab, 1f, fallbackCellSize);
        if (fallbackRadius <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Clamp(safeHalfCell / fallbackRadius, 0f, 5f);
    }

    private bool IsOverlappingPlacedBuildings(Vector3 worldPos, float radius, List<PlacedBuildingInfo> placedBuildings)
    {
        Vector2 position2D = new Vector2(worldPos.x, worldPos.z);
        for (int i = 0; i < placedBuildings.Count; i++)
        {
            float minDistance = radius + placedBuildings[i].radius + buildingMinSpacing;
            if ((position2D - placedBuildings[i].position).sqrMagnitude < minDistance * minDistance)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject InstantiateForScene(GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = null;
        if (PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        }

        if (instance == null)
        {
            instance = Instantiate(prefab);
        }

        if (instance != null)
        {
            instance.transform.SetParent(parent, true);
        }

        return instance;
    }

    private bool HasRenderableGeometry(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureBuildingParent()
    {
        if (buildingParent != null)
        {
            return;
        }

        GameObject parent = new GameObject("SimplePoly_Buildings");
        Undo.RegisterCreatedObjectUndo(parent, "Create Building Parent");
        buildingParent = parent.transform;
    }

    private Vector3 GetDirectionToRoad(int roadNeighborsMask)
    {
        List<Vector3> options = new List<Vector3>(4);
        if ((roadNeighborsMask & North) != 0) options.Add(Vector3.forward);
        if ((roadNeighborsMask & East) != 0) options.Add(Vector3.right);
        if ((roadNeighborsMask & South) != 0) options.Add(Vector3.back);
        if ((roadNeighborsMask & West) != 0) options.Add(Vector3.left);

        if (options.Count == 0)
        {
            return Vector3.forward;
        }

        return options[Random.Range(0, options.Count)];
    }

    private float SampleY(float worldX, float worldZ, float extraOffset)
    {
        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            float y = targetTerrain.SampleHeight(new Vector3(worldX, targetTerrain.transform.position.y, worldZ));
            return y + targetTerrain.transform.position.y + extraOffset;
        }

        return extraOffset;
    }

    private float ResolveCellSize()
    {
        if (!autoCellSizeFromStraightPrefab || straightPrefab == null)
        {
            return Mathf.Max(1f, cellSize);
        }

        if (!TryGetPrefabFootprint(straightPrefab, out Vector2 footprint))
        {
            return Mathf.Max(1f, cellSize);
        }

        float suggested = Mathf.Max(footprint.x, footprint.y) + cellSizePadding;
        return Mathf.Max(1f, suggested);
    }

    private bool TryGetPrefabFootprint(GameObject prefab, out Vector2 footprint)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            footprint = default;
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        footprint = new Vector2(bounds.size.x, bounds.size.z);
        return true;
    }

    private void FitGridToTerrain(ref int width, ref int height, float currentCellSize)
    {
        if (!autoFitGridToTerrain || targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        Vector3 size = targetTerrain.terrainData.size;
        float usableX = Mathf.Max(1f, size.x - terrainInset * 2f);
        float usableZ = Mathf.Max(1f, size.z - terrainInset * 2f);

        int maxWidth = Mathf.Max(2, Mathf.FloorToInt(usableX / currentCellSize));
        int maxHeight = Mathf.Max(2, Mathf.FloorToInt(usableZ / currentCellSize));

        if (width > maxWidth || height > maxHeight)
        {
            width = Mathf.Min(width, maxWidth);
            height = Mathf.Min(height, maxHeight);
            Debug.Log($"[CityRoadGridTool] Grid clamped to terrain: {width}x{height}");
        }
    }

    private bool[,] BuildRoadMask(int width, int height, bool cleanGapsEnabled, int iterations)
    {
        bool[,] mask = new bool[width, height];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                bool onMainAxis = (x % avenueSpacingX == 0) || (z % avenueSpacingZ == 0);
                if (!onMainAxis)
                {
                    continue;
                }

                bool isMainCross = (x % avenueSpacingX == 0) && (z % avenueSpacingZ == 0);
                if (!isMainCross && Random.value < skipChance)
                {
                    continue;
                }

                mask[x, z] = true;
            }
        }

        if (cleanGapsEnabled)
        {
            CleanupRoadMask(mask, width, height, iterations);
        }

        return mask;
    }

    private void CleanupRoadMask(bool[,] mask, int width, int height, int iterations)
    {
        bool[,] current = mask;

        for (int i = 0; i < iterations; i++)
        {
            bool changed = false;
            bool[,] next = (bool[,])current.Clone();

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int neighbors = GetNeighborMask(current, x, z, width, height);
                    int count = CountBits(neighbors);

                    if (!current[x, z])
                    {
                        bool bridgeHorizontal = (neighbors & East) != 0 && (neighbors & West) != 0;
                        bool bridgeVertical = (neighbors & North) != 0 && (neighbors & South) != 0;
                        bool strongJunction = count >= 3;
                        if (bridgeHorizontal || bridgeVertical || strongJunction)
                        {
                            next[x, z] = true;
                            changed = true;
                        }
                    }
                    else
                    {
                        if (count == 0)
                        {
                            next[x, z] = false;
                            changed = true;
                        }
                    }
                }
            }

            if (!changed)
            {
                current = next;
                break;
            }

            current = next;
        }

        if (!ReferenceEquals(current, mask))
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask[x, z] = current[x, z];
                }
            }
        }
    }

    private int GetNeighborMask(bool[,] mask, int x, int z, int width, int height)
    {
        int result = 0;
        if (z < height - 1 && mask[x, z + 1]) result |= North;
        if (x < width - 1 && mask[x + 1, z]) result |= East;
        if (z > 0 && mask[x, z - 1]) result |= South;
        if (x > 0 && mask[x - 1, z]) result |= West;
        return result;
    }

    private RoadTileChoice ResolveTile(int mask)
    {
        int connectionCount = CountBits(mask);

        if (connectionCount >= 4)
        {
            return new RoadTileChoice(crossIntersectionPrefab, crossRotationOffset);
        }

        if (connectionCount == 3)
        {
            // Base T assumes N+E+W are connected (South missing).
            float yaw = FindRotationForMask(North | East | West, mask) + tIntersectionRotationOffset;
            return new RoadTileChoice(tIntersectionPrefab, yaw);
        }

        if (connectionCount == 2)
        {
            bool opposite = mask == (North | South) || mask == (East | West);
            if (opposite)
            {
                float yaw = mask == (North | South) ? 0f : 90f;
                return new RoadTileChoice(straightPrefab, yaw + straightRotationOffset);
            }

            // Base corner assumes North+East are connected.
            float yawCorner = FindRotationForMask(North | East, mask) + cornerRotationOffset;
            return new RoadTileChoice(cornerPrefab, yawCorner);
        }

        if (connectionCount == 1)
        {
            float yawDeadEnd = FindRotationForMask(North, mask) + deadEndRotationOffset;
            if (deadEndPrefab != null)
            {
                return new RoadTileChoice(deadEndPrefab, yawDeadEnd);
            }

            return new RoadTileChoice(straightPrefab, yawDeadEnd + straightRotationOffset);
        }

        return new RoadTileChoice(deadEndPrefab, deadEndRotationOffset);
    }

    private Vector3 CalculateWorldPosition(int x, int z, int width, int height, float currentCellSize)
    {
        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            Vector3 terrainPos = targetTerrain.transform.position;
            Vector3 terrainSize = targetTerrain.terrainData.size;

            float startX = terrainPos.x + terrainInset + currentCellSize * 0.5f;
            float startZ = terrainPos.z + terrainInset + currentCellSize * 0.5f;

            if (!autoFitGridToTerrain)
            {
                float centeredOffsetX = (terrainSize.x - width * currentCellSize) * 0.5f;
                float centeredOffsetZ = (terrainSize.z - height * currentCellSize) * 0.5f;
                startX = terrainPos.x + Mathf.Max(terrainInset, centeredOffsetX) + currentCellSize * 0.5f;
                startZ = terrainPos.z + Mathf.Max(terrainInset, centeredOffsetZ) + currentCellSize * 0.5f;
            }

            float worldX = startX + x * currentCellSize;
            float worldZ = startZ + z * currentCellSize;
            float sampledY = targetTerrain.SampleHeight(new Vector3(worldX, terrainPos.y, worldZ)) + terrainPos.y + heightOffset;
            return new Vector3(worldX, sampledY, worldZ);
        }

        return new Vector3(x * currentCellSize, heightOffset, z * currentCellSize);
    }

    private float FindRotationForMask(int baseMask, int targetMask)
    {
        int rotated = baseMask;
        for (int i = 0; i < 4; i++)
        {
            if (rotated == targetMask)
            {
                return i * 90f;
            }
            rotated = RotateMaskClockwise(rotated);
        }

        return 0f;
    }

    private int RotateMaskClockwise(int mask)
    {
        int rotated = 0;
        if ((mask & North) != 0) rotated |= East;
        if ((mask & East) != 0) rotated |= South;
        if ((mask & South) != 0) rotated |= West;
        if ((mask & West) != 0) rotated |= North;
        return rotated;
    }

    private int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private void ClearRoads()
    {
        if (roadParent == null)
        {
            return;
        }

        for (int i = roadParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(roadParent.GetChild(i).gameObject);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void ClearBuildings()
    {
        if (buildingParent == null)
        {
            return;
        }

        for (int i = buildingParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(buildingParent.GetChild(i).gameObject);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void AutoLoadPrefabs()
    {
        straightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Lane_01.prefab");
        cornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Corner_01.prefab");
        tIntersectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road T_Intersection_01.prefab");
        crossIntersectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Intersection_01.prefab");
        deadEndPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Lane Half.prefab");
        AutoLoadBuildingPrefabs();
    }

    private void AutoLoadBuildingPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings" });
        buildingPrefabs.Clear();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                buildingPrefabs.Add(prefab);
            }
        }

        Debug.Log($"[CityRoadGridTool] Auto loaded {buildingPrefabs.Count} building prefabs.");
    }

    private void AutoFillNeighborhoodNames()
    {
        float effectiveCellSize = ResolveCellSize();
        int width = gridWidth;
        int height = gridHeight;
        FitGridToTerrain(ref width, ref height, effectiveCellSize);

        int neighborhoodsX = Mathf.CeilToInt((float)width / neighborhoodSize);
        int neighborhoodsZ = Mathf.CeilToInt((float)height / neighborhoodSize);
        int totalNeighborhoods = neighborhoodsX * neighborhoodsZ;

        neighborhoodNames.Clear();
        NeighborhoodNameGenerator.ResetUsedNames();

        for (int i = 0; i < totalNeighborhoods; i++)
        {
            neighborhoodNames.Add(NeighborhoodNameGenerator.GetRandomName(false));
        }

        Debug.Log($"[CityRoadGridTool] Auto-filled {totalNeighborhoods} neighborhood names.");
    }

    private int GenerateNeighborhoods(int width, int height, float currentCellSize)
    {
        EnsureNeighborhoodParent();

        if (clearNeighborhoodsBeforeGenerate)
        {
            ClearNeighborhoods();
        }

        if (neighborhoodNames.Count == 0)
        {
            Debug.LogWarning("[CityRoadGridTool] No neighborhood names defined. Use 'Auto Fill Names' button.");
            return 0;
        }

        int neighborhoodsX = Mathf.CeilToInt((float)width / neighborhoodSize);
        int neighborhoodsZ = Mathf.CeilToInt((float)height / neighborhoodSize);
        int createdCount = 0;
        int nameIndex = 0;

        for (int nz = 0; nz < neighborhoodsZ; nz++)
        {
            for (int nx = 0; nx < neighborhoodsX; nx++)
            {
                int startX = nx * neighborhoodSize;
                int startZ = nz * neighborhoodSize;
                int endX = Mathf.Min(startX + neighborhoodSize, width);
                int endZ = Mathf.Min(startZ + neighborhoodSize, height);

                List<Vector2Int> cells = new List<Vector2Int>();
                for (int z = startZ; z < endZ; z++)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        cells.Add(new Vector2Int(x, z));
                    }
                }

                string neighborhoodName = nameIndex < neighborhoodNames.Count
                    ? neighborhoodNames[nameIndex]
                    : $"Mahalle {nameIndex + 1}";

                Neighborhood neighborhood = new Neighborhood(neighborhoodName);
                neighborhood.AddGridCells(cells);

                Bounds bounds = neighborhood.GetBounds(currentCellSize, GetGridOrigin(width, height, currentCellSize));
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;

                GameObject zoneObj = new GameObject($"Zone_{neighborhoodName}");
                zoneObj.transform.SetParent(neighborhoodParent);
                zoneObj.transform.position = center;

                NeighborhoodZone zone = zoneObj.AddComponent<NeighborhoodZone>();
                zone.NeighborhoodName = neighborhoodName;
                zone.DebugColor = neighborhood.DebugColor;

                BoxCollider boxCollider = zoneObj.GetComponent<BoxCollider>();
                boxCollider.center = Vector3.up * (neighborhoodZoneHeight * 0.5f);
                boxCollider.size = new Vector3(size.x, neighborhoodZoneHeight, size.z);
                boxCollider.isTrigger = true;

                Undo.RegisterCreatedObjectUndo(zoneObj, "Create Neighborhood Zone");

                createdCount++;
                nameIndex++;
            }
        }

        Debug.Log($"[CityRoadGridTool] Created {createdCount} neighborhoods ({neighborhoodsX}x{neighborhoodsZ}).");
        return createdCount;
    }

    private void EnsureNeighborhoodParent()
    {
        if (neighborhoodParent != null)
        {
            return;
        }

        GameObject parent = new GameObject("Neighborhoods");
        Undo.RegisterCreatedObjectUndo(parent, "Create Neighborhood Parent");
        neighborhoodParent = parent.transform;
    }

    private void ClearNeighborhoods()
    {
        if (neighborhoodParent == null)
        {
            return;
        }

        for (int i = neighborhoodParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(neighborhoodParent.GetChild(i).gameObject);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private Vector3 GetGridOrigin(int width, int height, float currentCellSize)
    {
        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            Vector3 terrainPos = targetTerrain.transform.position;
            Vector3 terrainSize = targetTerrain.terrainData.size;

            float startX = terrainPos.x + terrainInset;
            float startZ = terrainPos.z + terrainInset;

            if (!autoFitGridToTerrain)
            {
                float centeredOffsetX = (terrainSize.x - width * currentCellSize) * 0.5f;
                float centeredOffsetZ = (terrainSize.z - height * currentCellSize) * 0.5f;
                startX = terrainPos.x + Mathf.Max(terrainInset, centeredOffsetX);
                startZ = terrainPos.z + Mathf.Max(terrainInset, centeredOffsetZ);
            }

            return new Vector3(startX, terrainPos.y, startZ);
        }

        return Vector3.zero;
    }

    private readonly struct RoadTileChoice
    {
        public readonly GameObject prefab;
        public readonly float yaw;

        public RoadTileChoice(GameObject prefab, float yaw)
        {
            this.prefab = prefab;
            this.yaw = yaw;
        }
    }

    private readonly struct PlacedBuildingInfo
    {
        public readonly Vector2 position;
        public readonly float radius;

        public PlacedBuildingInfo(Vector2 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    private readonly struct BuildingChoice
    {
        public readonly GameObject prefab;
        public readonly float scale;
        public readonly float radius;

        public BuildingChoice(GameObject prefab, float scale, float radius)
        {
            this.prefab = prefab;
            this.scale = scale;
            this.radius = radius;
        }
    }
}
