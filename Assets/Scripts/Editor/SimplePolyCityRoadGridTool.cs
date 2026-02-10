using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SimplePolyCityRoadGridTool : EditorWindow
{
    private const int North = 1;
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;

    [Header("Scene References")]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private Transform roadParent;

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

    [MenuItem("Tools/SimplePoly/City Road Grid Tool")]
    public static void ShowWindow()
    {
        GetWindow<SimplePolyCityRoadGridTool>("City Road Grid Tool");
    }

    private void OnGUI()
    {
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
        }

        if (GUILayout.Button("Generate Grid Roads", GUILayout.Height(42)))
        {
            GenerateRoadGrid();
        }
    }

    private void DrawSceneSection()
    {
        EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
        roadParent = (Transform)EditorGUILayout.ObjectField("Road Parent", roadParent, typeof(Transform), true);
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

        Selection.activeTransform = roadParent;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CityRoadGridTool] Generated {createdCount} road tiles. Seed: {randomSeed}, Cell Size: {effectiveCellSize:F2}");
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

    private void AutoLoadPrefabs()
    {
        straightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Lane_01.prefab");
        cornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Corner_01.prefab");
        tIntersectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road T_Intersection_01.prefab");
        crossIntersectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Intersection_01.prefab");
        deadEndPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/Road Lane Half.prefab");
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
}
