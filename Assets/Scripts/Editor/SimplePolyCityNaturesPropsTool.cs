using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class SimplePolyCityNaturesPropsTool : EditorWindow
{
    private const int North = 1;
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;

    private const string NaturesFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Natures";
    private const string PropsFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Props";

    [Header("Scene References")]
    [SerializeField] private List<Terrain> targetTerrains = new List<Terrain>();
    [SerializeField] private Transform roadParent;
    [SerializeField] private Transform buildingParent;
    [SerializeField] private Transform natureParent;
    [SerializeField] private Transform propsParent;

    [Header("Grid (match Road Grid Tool)")]
    [SerializeField] private float cellSize = 8f;
    [SerializeField] private float terrainInset = 4f;
    [SerializeField] private float heightOffset = 0.03f;
    [SerializeField] private bool autoFitGridToTerrain = true;

    [Header("Density")]
    [SerializeField, Range(0f, 1f)] private float natureDensity = 0.6f;
    [SerializeField, Range(0f, 1f)] private float propsDensity = 0.5f;
    [SerializeField, Range(0f, 1f)] private float bushFillDensity = 0.7f;

    [Header("Spacing")]
    [SerializeField] private int streetLightSpacing = 3;
    [SerializeField] private float treeMinDistance = 3f;
    [SerializeField] private float propsMinDistance = 1.5f;

    [Header("Randomization")]
    [SerializeField] private int randomSeed = 54321;
    [SerializeField] private bool randomizeSeedEachBuild;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private bool randomizeYRotation = true;

    // Nature prefabs
    [Header("Nature Prefabs")]
    [SerializeField] private List<GameObject> treePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> bushPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> grassPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> rockPrefabs = new List<GameObject>();

    // Props prefabs
    [Header("Roadside Props")]
    [SerializeField] private GameObject streetLightPrefab;
    [SerializeField] private List<GameObject> benchPrefabs = new List<GameObject>();
    [SerializeField] private GameObject hydrantPrefab;
    [SerializeField] private GameObject busStopPrefab;
    [SerializeField] private GameObject dustbinPrefab;
    [SerializeField] private GameObject fencePrefab;
    [SerializeField] private GameObject trafficConePrefab;
    [SerializeField] private List<GameObject> trafficSignPrefabs = new List<GameObject>();

    [Header("Building Accent Props")]
    [SerializeField] private List<GameObject> billboardPrefabs = new List<GameObject>();
    [SerializeField] private GameObject coffeeChairPrefab;
    [SerializeField] private List<GameObject> potBushPrefabs = new List<GameObject>();

    private Vector2 scrollPosition;
    private bool[,] cachedRoadMask;
    private int cachedGridWidth;
    private int cachedGridHeight;
    private Vector3 cachedGridOrigin;
    private bool hasCachedGridState;

    [MenuItem("Tools/SimplePoly/Nature & Props Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<SimplePolyCityNaturesPropsTool>("Nature & Props Tool");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("SimplePoly Nature & Props Placement", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawSceneSection();
        EditorGUILayout.Space();
        DrawGridSection();
        EditorGUILayout.Space();
        DrawDensitySection();
        EditorGUILayout.Space();
        DrawSpacingSection();
        EditorGUILayout.Space();
        DrawRandomSection();
        EditorGUILayout.Space();
        DrawNaturePrefabSection();
        EditorGUILayout.Space();
        DrawPropsPrefabSection();
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Load Prefabs", GUILayout.Height(28)))
            {
                AutoLoadPrefabs();
            }
            if (GUILayout.Button("Auto Find Scene Refs", GUILayout.Height(28)))
            {
                AutoFindSceneRefs();
            }
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Nature", GUILayout.Height(28)))
            {
                ClearChildren(natureParent, "Nature");
            }
            if (GUILayout.Button("Clear Props", GUILayout.Height(28)))
            {
                ClearChildren(propsParent, "Props");
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(28)))
            {
                ClearChildren(natureParent, "Nature");
                ClearChildren(propsParent, "Props");
            }
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Generate Nature & Props", GUILayout.Height(42)))
        {
            Generate();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneSection()
    {
        EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"Target Terrains ({targetTerrains.Count})");
        if (GUILayout.Button("Auto Find All Terrains", GUILayout.Height(22)))
        {
            AutoFindTerrains();
        }

        int removeIdx = -1;
        for (int i = 0; i < targetTerrains.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                targetTerrains[i] = (Terrain)EditorGUILayout.ObjectField($"Terrain {i + 1}", targetTerrains[i], typeof(Terrain), true);
                if (GUILayout.Button("X", GUILayout.Width(24))) removeIdx = i;
            }
        }
        if (removeIdx >= 0) targetTerrains.RemoveAt(removeIdx);
        if (GUILayout.Button("Add Terrain Slot", GUILayout.Height(20))) targetTerrains.Add(null);

        EditorGUILayout.Space(4);
        roadParent = (Transform)EditorGUILayout.ObjectField("Road Parent", roadParent, typeof(Transform), true);
        buildingParent = (Transform)EditorGUILayout.ObjectField("Building Parent", buildingParent, typeof(Transform), true);
        natureParent = (Transform)EditorGUILayout.ObjectField("Nature Parent", natureParent, typeof(Transform), true);
        propsParent = (Transform)EditorGUILayout.ObjectField("Props Parent", propsParent, typeof(Transform), true);
    }

    private void DrawGridSection()
    {
        EditorGUILayout.LabelField("Grid (match Road Grid Tool settings)", EditorStyles.boldLabel);
        cellSize = Mathf.Max(1f, EditorGUILayout.FloatField("Cell Size", cellSize));
        terrainInset = Mathf.Max(0f, EditorGUILayout.FloatField("Terrain Inset", terrainInset));
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);
        autoFitGridToTerrain = EditorGUILayout.Toggle("Auto Fit To Terrain", autoFitGridToTerrain);
    }

    private void DrawDensitySection()
    {
        EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
        natureDensity = EditorGUILayout.Slider("Nature Density", natureDensity, 0f, 1f);
        propsDensity = EditorGUILayout.Slider("Props Density", propsDensity, 0f, 1f);
        bushFillDensity = EditorGUILayout.Slider("Bush Fill Density", bushFillDensity, 0f, 1f);
    }

    private void DrawSpacingSection()
    {
        EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
        streetLightSpacing = Mathf.Max(1, EditorGUILayout.IntField("Street Light Every N Cells", streetLightSpacing));
        treeMinDistance = Mathf.Max(0.5f, EditorGUILayout.FloatField("Tree Min Distance", treeMinDistance));
        propsMinDistance = Mathf.Max(0.3f, EditorGUILayout.FloatField("Props Min Distance", propsMinDistance));
    }

    private void DrawRandomSection()
    {
        EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
        randomizeSeedEachBuild = EditorGUILayout.Toggle("Randomize Seed Each Build", randomizeSeedEachBuild);
        randomSeed = EditorGUILayout.IntField("Seed", randomSeed);
        scaleRange = EditorGUILayout.Vector2Field("Scale Range", scaleRange);
        scaleRange.x = Mathf.Clamp(scaleRange.x, 0.1f, 3f);
        scaleRange.y = Mathf.Clamp(scaleRange.y, scaleRange.x, 3f);
        randomizeYRotation = EditorGUILayout.Toggle("Randomize Y Rotation", randomizeYRotation);
    }

    private void DrawNaturePrefabSection()
    {
        EditorGUILayout.LabelField("Nature Prefabs", EditorStyles.boldLabel);
        DrawPrefabList("Trees", treePrefabs);
        DrawPrefabList("Bushes", bushPrefabs);
        DrawPrefabList("Grass", grassPrefabs);
        DrawPrefabList("Rocks", rockPrefabs);
    }

    private void DrawPropsPrefabSection()
    {
        EditorGUILayout.LabelField("Props Prefabs", EditorStyles.boldLabel);
        streetLightPrefab = (GameObject)EditorGUILayout.ObjectField("Street Light", streetLightPrefab, typeof(GameObject), false);
        hydrantPrefab = (GameObject)EditorGUILayout.ObjectField("Hydrant", hydrantPrefab, typeof(GameObject), false);
        busStopPrefab = (GameObject)EditorGUILayout.ObjectField("Bus Stop", busStopPrefab, typeof(GameObject), false);
        dustbinPrefab = (GameObject)EditorGUILayout.ObjectField("Dustbin", dustbinPrefab, typeof(GameObject), false);
        fencePrefab = (GameObject)EditorGUILayout.ObjectField("Fence", fencePrefab, typeof(GameObject), false);
        trafficConePrefab = (GameObject)EditorGUILayout.ObjectField("Traffic Cone", trafficConePrefab, typeof(GameObject), false);
        coffeeChairPrefab = (GameObject)EditorGUILayout.ObjectField("Coffee Chair", coffeeChairPrefab, typeof(GameObject), false);

        DrawPrefabList("Benches", benchPrefabs);
        DrawPrefabList("Traffic Signs", trafficSignPrefabs);
        DrawPrefabList("Billboards", billboardPrefabs);
        DrawPrefabList("Pot Bushes", potBushPrefabs);
    }

    private void DrawPrefabList(string label, List<GameObject> list)
    {
        EditorGUILayout.LabelField($"  {label} ({list.Count})");
        int removeIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                list[i] = (GameObject)EditorGUILayout.ObjectField(list[i], typeof(GameObject), false);
                if (GUILayout.Button("X", GUILayout.Width(24))) removeIdx = i;
            }
        }
        if (removeIdx >= 0) list.RemoveAt(removeIdx);
        if (GUILayout.Button($"Add {label} Slot", GUILayout.Height(18))) list.Add(null);
    }

    // ─── Generation ───────────────────────────────────────────────────

    private void Generate()
    {
        if (targetTerrains == null || targetTerrains.Count == 0)
        {
            AutoFindTerrains();
        }

        if (roadParent == null || buildingParent == null)
        {
            AutoFindSceneRefs();
        }

        if (roadParent == null)
        {
            EditorUtility.DisplayDialog("Missing Reference", "Road parent (SimplePoly_RoadGrid) not found in scene. Generate roads first.", "OK");
            return;
        }

        if (randomizeSeedEachBuild)
        {
            randomSeed = System.Environment.TickCount;
        }

        Random.InitState(randomSeed);

        EnsureParent(ref natureParent, "SimplePoly_Natures");
        EnsureParent(ref propsParent, "SimplePoly_Props");
        ClearChildren(natureParent, "Nature");
        ClearChildren(propsParent, "Props");

        // Rebuild grid from scene
        int gridWidth, gridHeight;
        bool[,] roadMask;
        bool[,] buildingMask;
        RebuildGridState(out gridWidth, out gridHeight, out roadMask, out buildingMask);

        if (gridWidth == 0 || gridHeight == 0)
        {
            EditorUtility.DisplayDialog("Grid Error", "Could not determine grid dimensions. Check cell size and terrain settings.", "OK");
            return;
        }

        cachedRoadMask = roadMask;
        cachedGridWidth = gridWidth;
        cachedGridHeight = gridHeight;
        cachedGridOrigin = GetGridOrigin(gridWidth, gridHeight);
        hasCachedGridState = true;

        // Collect placed item positions for overlap checking
        List<PlacedItem> placedItems = new List<PlacedItem>();

        int natureCount = 0;
        int propsCount = 0;

        // Pass 1: Street lights along roads
        propsCount += PlaceStreetLights(roadMask, gridWidth, gridHeight, placedItems);

        // Pass 2: Intersection props (hydrants, traffic signs)
        propsCount += PlaceIntersectionProps(roadMask, gridWidth, gridHeight, placedItems);

        // Pass 3: Place grass ground cover on ALL non-road cells (hides terrain)
        int grassCoverCount = 0;
        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (roadMask[x, z]) continue;
                grassCoverCount += PlaceGrassGroundCover(x, z, gridWidth, gridHeight);
            }
        }
        natureCount += grassCoverCount;

        // Pass 4: Process each non-road cell for trees, bushes, props
        for (int z = 0; z < gridHeight; z++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (roadMask[x, z]) continue;

                int roadNeighbors = GetNeighborMask(roadMask, x, z, gridWidth, gridHeight);
                bool hasBuilding = buildingMask[x, z];

                if (hasBuilding)
                {
                    // Building accent: pot bushes, dustbins near edges
                    int placed = PlaceBuildingAccents(x, z, gridWidth, gridHeight, roadNeighbors, placedItems);
                    natureCount += placed;
                }
                else if (roadNeighbors != 0)
                {
                    // Empty cell next to road: roadside props + some nature
                    propsCount += PlaceRoadsideProps(x, z, gridWidth, gridHeight, roadNeighbors, placedItems);
                    natureCount += PlaceNatureFill(x, z, gridWidth, gridHeight, placedItems, 0.5f);
                }
                else
                {
                    // Empty interior cell: fill with nature
                    natureCount += PlaceNatureFill(x, z, gridWidth, gridHeight, placedItems, 1f);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[NaturesPropsTool] Generated {natureCount} nature items ({grassCoverCount} grass cover) and {propsCount} props. Seed: {randomSeed}");
    }

    // ─── Grid Reconstruction ──────────────────────────────────────────

    private void RebuildGridState(out int width, out int height, out bool[,] roadMask, out bool[,] buildingMask)
    {
        width = 0;
        height = 0;
        roadMask = new bool[0, 0];
        buildingMask = new bool[0, 0];

        if (targetTerrains == null || targetTerrains.Count == 0) return;

        Bounds combinedBounds = GetCombinedTerrainBounds();
        float usableX = Mathf.Max(1f, combinedBounds.size.x - terrainInset * 2f);
        float usableZ = Mathf.Max(1f, combinedBounds.size.z - terrainInset * 2f);

        if (autoFitGridToTerrain)
        {
            width = Mathf.Max(2, Mathf.FloorToInt(usableX / cellSize));
            height = Mathf.Max(2, Mathf.FloorToInt(usableZ / cellSize));
        }
        else
        {
            width = Mathf.Max(2, Mathf.FloorToInt(usableX / cellSize));
            height = Mathf.Max(2, Mathf.FloorToInt(usableZ / cellSize));
        }

        roadMask = new bool[width, height];
        buildingMask = new bool[width, height];

        Vector3 gridOrigin = GetGridOrigin(width, height);

        // Map road children to grid cells
        if (roadParent != null)
        {
            for (int i = 0; i < roadParent.childCount; i++)
            {
                Vector3 pos = roadParent.GetChild(i).position;
                WorldToGrid(pos, gridOrigin, out int gx, out int gz);
                if (gx >= 0 && gx < width && gz >= 0 && gz < height)
                {
                    roadMask[gx, gz] = true;
                }
            }
        }

        // Map building children to grid cells
        if (buildingParent != null)
        {
            for (int i = 0; i < buildingParent.childCount; i++)
            {
                Vector3 pos = buildingParent.GetChild(i).position;
                WorldToGrid(pos, gridOrigin, out int gx, out int gz);
                if (gx >= 0 && gx < width && gz >= 0 && gz < height)
                {
                    buildingMask[gx, gz] = true;
                }
            }
        }
    }

    private void WorldToGrid(Vector3 worldPos, Vector3 gridOrigin, out int gx, out int gz)
    {
        gx = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        gz = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
    }

    // ─── Street Lights ────────────────────────────────────────────────

    private int PlaceStreetLights(bool[,] roadMask, int width, int height, List<PlacedItem> placedItems)
    {
        if (streetLightPrefab == null) return 0;

        int count = 0;
        float halfCell = cellSize * 0.5f;
        float edgeOffset = halfCell * 1.1f;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!roadMask[x, z]) continue;

                // Place every N cells along road edges
                bool isSpacingCell = (x % streetLightSpacing == 0) && (z % streetLightSpacing == 0);
                if (!isSpacingCell) continue;

                // Find a non-road neighbor side to place the light
                Vector3 cellCenter = CalculateWorldPosition(x, z, width, height);

                if (z > 0 && !roadMask[x, z - 1])
                {
                    if (TryPlaceProp(streetLightPrefab, cellCenter + new Vector3(0, 0, -edgeOffset), 0f, placedItems, propsMinDistance))
                        count++;
                }
                else if (x < width - 1 && !roadMask[x + 1, z])
                {
                    if (TryPlaceProp(streetLightPrefab, cellCenter + new Vector3(edgeOffset, 0, 0), 90f, placedItems, propsMinDistance))
                        count++;
                }
                else if (z < height - 1 && !roadMask[x, z + 1])
                {
                    if (TryPlaceProp(streetLightPrefab, cellCenter + new Vector3(0, 0, edgeOffset), 180f, placedItems, propsMinDistance))
                        count++;
                }
                else if (x > 0 && !roadMask[x - 1, z])
                {
                    if (TryPlaceProp(streetLightPrefab, cellCenter + new Vector3(-edgeOffset, 0, 0), 270f, placedItems, propsMinDistance))
                        count++;
                }
            }
        }

        return count;
    }

    // ─── Intersection Props ───────────────────────────────────────────

    private int PlaceIntersectionProps(bool[,] roadMask, int width, int height, List<PlacedItem> placedItems)
    {
        int count = 0;
        float halfCell = cellSize * 0.5f;
        float cornerOffset = halfCell * 1.1f;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!roadMask[x, z]) continue;

                int neighbors = GetNeighborMask(roadMask, x, z, width, height);
                int connectionCount = CountBits(neighbors);

                // Only at intersections (3+ connections)
                if (connectionCount < 3) continue;

                Vector3 cellCenter = CalculateWorldPosition(x, z, width, height);

                // Hydrant at one corner
                if (hydrantPrefab != null && Random.value < propsDensity * 0.6f)
                {
                    // Find a corner that's not road
                    Vector3 offset = FindFreeCorner(roadMask, x, z, width, height, cornerOffset);
                    if (offset.sqrMagnitude > 0.01f)
                    {
                        if (TryPlaceProp(hydrantPrefab, cellCenter + offset, Random.Range(0f, 360f), placedItems, propsMinDistance))
                            count++;
                    }
                }

                // Traffic sign
                if (trafficSignPrefabs.Count > 0 && Random.value < propsDensity * 0.5f)
                {
                    GameObject signPrefab = PickRandom(trafficSignPrefabs);
                    if (signPrefab != null)
                    {
                        Vector3 offset = FindFreeCorner(roadMask, x, z, width, height, cornerOffset);
                        if (offset.sqrMagnitude > 0.01f)
                        {
                            if (TryPlaceProp(signPrefab, cellCenter + offset, Random.Range(0f, 360f), placedItems, propsMinDistance))
                                count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    private Vector3 FindFreeCorner(bool[,] roadMask, int x, int z, int width, int height, float offset)
    {
        // Check diagonal neighbors - pick a corner where two non-road sides meet
        bool northFree = z >= height - 1 || !roadMask[x, z + 1];
        bool southFree = z <= 0 || !roadMask[x, z - 1];
        bool eastFree = x >= width - 1 || !roadMask[x + 1, z];
        bool westFree = x <= 0 || !roadMask[x - 1, z];

        List<Vector3> corners = new List<Vector3>(4);
        if (northFree || eastFree) corners.Add(new Vector3(offset, 0, offset));
        if (northFree || westFree) corners.Add(new Vector3(-offset, 0, offset));
        if (southFree || eastFree) corners.Add(new Vector3(offset, 0, -offset));
        if (southFree || westFree) corners.Add(new Vector3(-offset, 0, -offset));

        if (corners.Count == 0) return Vector3.zero;
        return corners[Random.Range(0, corners.Count)];
    }

    // ─── Roadside Props ───────────────────────────────────────────────

    private int PlaceRoadsideProps(int x, int z, int gridWidth, int gridHeight, int roadNeighbors, List<PlacedItem> placedItems)
    {
        int count = 0;
        float halfCell = cellSize * 0.5f;
        float edgeOffset = halfCell * 0.8f;
        Vector3 cellCenter = CalculateWorldPosition(x, z, gridWidth, gridHeight);

        // Bench near road
        if (benchPrefabs.Count > 0 && Random.value < propsDensity * 0.15f)
        {
            GameObject benchPrefab = PickRandom(benchPrefabs);
            if (benchPrefab != null)
            {
                Vector3 dir = GetDirectionFromMask(roadNeighbors);
                Vector3 pos = cellCenter + dir * edgeOffset;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                if (TryPlaceProp(benchPrefab, pos, yaw, placedItems, propsMinDistance))
                    count++;
            }
        }

        // Bus stop (sparse)
        if (busStopPrefab != null && Random.value < propsDensity * 0.03f)
        {
            Vector3 dir = GetDirectionFromMask(roadNeighbors);
            Vector3 pos = cellCenter + dir * edgeOffset;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (TryPlaceProp(busStopPrefab, pos, yaw, placedItems, propsMinDistance * 2f))
                count++;
        }

        // Dustbin
        if (dustbinPrefab != null && Random.value < propsDensity * 0.12f)
        {
            Vector3 offset = new Vector3(Random.Range(-halfCell * 0.4f, halfCell * 0.4f), 0, Random.Range(-halfCell * 0.4f, halfCell * 0.4f));
            Vector3 dir = GetDirectionFromMask(roadNeighbors);
            Vector3 pos = cellCenter + dir * (edgeOffset * 0.6f) + offset;
            if (TryPlaceProp(dustbinPrefab, pos, Random.Range(0f, 360f), placedItems, propsMinDistance))
                count++;
        }

        // Traffic cone (occasional)
        if (trafficConePrefab != null && Random.value < propsDensity * 0.05f)
        {
            Vector3 dir = GetDirectionFromMask(roadNeighbors);
            Vector3 pos = cellCenter + dir * edgeOffset * 0.5f;
            if (TryPlaceProp(trafficConePrefab, pos, Random.Range(0f, 360f), placedItems, propsMinDistance * 0.5f))
                count++;
        }

        // Fence between properties
        if (fencePrefab != null && Random.value < propsDensity * 0.1f)
        {
            // Place perpendicular to road
            Vector3 dir = GetDirectionFromMask(roadNeighbors);
            Vector3 perpendicular = new Vector3(dir.z, 0, -dir.x);
            Vector3 pos = cellCenter + perpendicular * (halfCell * 0.9f);
            float yaw = Mathf.Atan2(perpendicular.x, perpendicular.z) * Mathf.Rad2Deg;
            if (TryPlaceProp(fencePrefab, pos, yaw, placedItems, propsMinDistance))
                count++;
        }

        return count;
    }

    // ─── Building Accents ─────────────────────────────────────────────

    private int PlaceBuildingAccents(int x, int z, int gridWidth, int gridHeight, int roadNeighbors, List<PlacedItem> placedItems)
    {
        int count = 0;
        float halfCell = cellSize * 0.5f;
        Vector3 cellCenter = CalculateWorldPosition(x, z, gridWidth, gridHeight);

        // Pot bushes near building edges
        if (potBushPrefabs.Count > 0 && Random.value < natureDensity * 0.5f)
        {
            int potCount = Random.Range(1, 3);
            for (int i = 0; i < potCount; i++)
            {
                GameObject potPrefab = PickRandom(potBushPrefabs);
                if (potPrefab == null) continue;

                Vector3 offset = new Vector3(
                    Random.Range(-halfCell * 0.8f, halfCell * 0.8f),
                    0,
                    Random.Range(-halfCell * 0.8f, halfCell * 0.8f));
                Vector3 pos = cellCenter + offset;
                if (TryPlaceNature(potPrefab, pos, placedItems, propsMinDistance))
                    count++;
            }
        }

        // Dustbin near building
        if (dustbinPrefab != null && Random.value < propsDensity * 0.15f)
        {
            Vector3 dir = roadNeighbors != 0 ? GetDirectionFromMask(roadNeighbors) : RandomHorizontalDir();
            Vector3 pos = cellCenter + dir * (halfCell * 0.7f);
            if (TryPlaceProp(dustbinPrefab, pos, Random.Range(0f, 360f), placedItems, propsMinDistance))
                count++;
        }

        // Billboard on commercial buildings (random chance)
        if (billboardPrefabs.Count > 0 && Random.value < propsDensity * 0.08f)
        {
            GameObject bbPrefab = PickRandom(billboardPrefabs);
            if (bbPrefab != null)
            {
                Vector3 dir = roadNeighbors != 0 ? GetDirectionFromMask(roadNeighbors) : RandomHorizontalDir();
                Vector3 pos = cellCenter + dir * (halfCell * 0.95f);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                if (TryPlaceProp(bbPrefab, pos, yaw, placedItems, propsMinDistance * 2f))
                    count++;
            }
        }

        // Coffee chair near building
        if (coffeeChairPrefab != null && Random.value < propsDensity * 0.06f)
        {
            Vector3 dir = roadNeighbors != 0 ? GetDirectionFromMask(roadNeighbors) : RandomHorizontalDir();
            Vector3 pos = cellCenter + dir * (halfCell * 0.6f);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 180f;
            if (TryPlaceProp(coffeeChairPrefab, pos, yaw, placedItems, propsMinDistance))
                count++;
        }

        return count;
    }

    // ─── Grass Ground Cover (guaranteed on every non-road cell) ──────

    private int PlaceGrassGroundCover(int x, int z, int gridWidth, int gridHeight)
    {
        if (grassPrefabs.Count == 0) return 0;

        int count = 0;
        Vector3 cellCenter = CalculateWorldPosition(x, z, gridWidth, gridHeight);

        // Always place one grass tile at cell center to guarantee terrain coverage
        GameObject grassPrefab = PickRandom(grassPrefabs);
        if (grassPrefab != null)
        {
            Vector3 pos = cellCenter;
            pos.y = SampleY(pos.x, pos.z, heightOffset);

            GameObject instance = InstantiateForScene(grassPrefab, natureParent);
            if (instance != null)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Place Grass Cover");
                instance.transform.position = pos;
                float yaw = randomizeYRotation ? Random.Range(0f, 360f) : 0f;
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                count++;
            }
        }

        return count;
    }

    // ─── Nature Fill ──────────────────────────────────────────────────

    private int PlaceNatureFill(int x, int z, int gridWidth, int gridHeight, List<PlacedItem> placedItems, float densityMultiplier)
    {
        int count = 0;
        float halfCell = cellSize * 0.5f;
        Vector3 cellCenter = CalculateWorldPosition(x, z, gridWidth, gridHeight);
        float effectiveDensity = natureDensity * densityMultiplier;

        // Tree (one per cell max) - keep offset small so tree stays within this cell
        if (treePrefabs.Count > 0 && Random.value < effectiveDensity * 0.4f)
        {
            GameObject treePrefab = PickRandom(treePrefabs);
            if (treePrefab != null)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-halfCell * 0.35f, halfCell * 0.35f),
                    0,
                    Random.Range(-halfCell * 0.35f, halfCell * 0.35f));
                Vector3 pos = cellCenter + offset;
                if (TryPlaceNature(treePrefab, pos, placedItems, treeMinDistance))
                    count++;
            }
        }

        // Bushes (multiple per cell) - clamp to cell bounds
        if (bushPrefabs.Count > 0)
        {
            int maxBushes = Mathf.RoundToInt(bushFillDensity * effectiveDensity * 4);
            for (int i = 0; i < maxBushes; i++)
            {
                if (Random.value > bushFillDensity * effectiveDensity) continue;

                GameObject bushPrefab = PickRandom(bushPrefabs);
                if (bushPrefab == null) continue;

                Vector3 offset = new Vector3(
                    Random.Range(-halfCell * 0.75f, halfCell * 0.75f),
                    0,
                    Random.Range(-halfCell * 0.75f, halfCell * 0.75f));
                Vector3 pos = cellCenter + offset;
                if (TryPlaceNature(bushPrefab, pos, placedItems, propsMinDistance * 0.8f))
                    count++;
            }
        }

        // Rocks (occasional accent)
        if (rockPrefabs.Count > 0 && Random.value < effectiveDensity * 0.1f)
        {
            GameObject rockPrefab = PickRandom(rockPrefabs);
            if (rockPrefab != null)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-halfCell * 0.5f, halfCell * 0.5f),
                    0,
                    Random.Range(-halfCell * 0.5f, halfCell * 0.5f));
                Vector3 pos = cellCenter + offset;
                if (TryPlaceNature(rockPrefab, pos, placedItems, propsMinDistance))
                    count++;
            }
        }

        return count;
    }

    // ─── Placement Helpers ────────────────────────────────────────────

    private bool TryPlaceProp(GameObject prefab, Vector3 pos, float yaw, List<PlacedItem> placedItems, float minDist)
    {
        if (prefab == null) return false;

        pos.y = SampleY(pos.x, pos.z, heightOffset);

        if (IsWorldPositionOnRoadCell(pos)) return false;

        if (IsOverlapping(pos, placedItems, minDist)) return false;

        GameObject instance = InstantiateForScene(prefab, propsParent);
        if (instance == null) return false;

        Undo.RegisterCreatedObjectUndo(instance, "Place Prop");
        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (scaleRange.x < scaleRange.y)
        {
            float s = Random.Range(scaleRange.x, scaleRange.y);
            instance.transform.localScale = Vector3.one * s;
        }

        placedItems.Add(new PlacedItem(new Vector2(pos.x, pos.z), minDist * 0.5f));
        return true;
    }

    private bool TryPlaceNature(GameObject prefab, Vector3 pos, List<PlacedItem> placedItems, float minDist)
    {
        if (prefab == null) return false;

        pos.y = SampleY(pos.x, pos.z, heightOffset);

        if (IsWorldPositionOnRoadCell(pos)) return false;

        if (IsOverlapping(pos, placedItems, minDist)) return false;

        GameObject instance = InstantiateForScene(prefab, natureParent);
        if (instance == null) return false;

        Undo.RegisterCreatedObjectUndo(instance, "Place Nature");
        instance.transform.position = pos;

        float yaw = randomizeYRotation ? Random.Range(0f, 360f) : 0f;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (scaleRange.x < scaleRange.y)
        {
            float s = Random.Range(scaleRange.x, scaleRange.y);
            instance.transform.localScale = Vector3.one * s;
        }

        placedItems.Add(new PlacedItem(new Vector2(pos.x, pos.z), minDist * 0.5f));
        return true;
    }

    private bool IsOverlapping(Vector3 worldPos, List<PlacedItem> placedItems, float minDist)
    {
        Vector2 pos2D = new Vector2(worldPos.x, worldPos.z);
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < placedItems.Count; i++)
        {
            float combinedDist = minDist + placedItems[i].radius;
            if ((pos2D - placedItems[i].position).sqrMagnitude < combinedDist * combinedDist)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWorldPositionOnRoadCell(Vector3 worldPos)
    {
        if (!hasCachedGridState || cachedRoadMask == null) return false;

        int gx = Mathf.FloorToInt((worldPos.x - cachedGridOrigin.x) / cellSize);
        int gz = Mathf.FloorToInt((worldPos.z - cachedGridOrigin.z) / cellSize);

        if (gx < 0 || gz < 0 || gx >= cachedGridWidth || gz >= cachedGridHeight) return false;
        return cachedRoadMask[gx, gz];
    }

    private GameObject InstantiateForScene(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;

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

    // ─── Grid & Terrain Utilities ─────────────────────────────────────

    private Vector3 CalculateWorldPosition(int x, int z, int width, int height)
    {
        if (targetTerrains != null && targetTerrains.Count > 0)
        {
            Bounds combinedBounds = GetCombinedTerrainBounds();
            Vector3 boundsMin = combinedBounds.min;
            Vector3 boundsSize = combinedBounds.size;

            float startX = boundsMin.x + terrainInset + cellSize * 0.5f;
            float startZ = boundsMin.z + terrainInset + cellSize * 0.5f;

            if (!autoFitGridToTerrain)
            {
                float centeredOffsetX = (boundsSize.x - width * cellSize) * 0.5f;
                float centeredOffsetZ = (boundsSize.z - height * cellSize) * 0.5f;
                startX = boundsMin.x + Mathf.Max(terrainInset, centeredOffsetX) + cellSize * 0.5f;
                startZ = boundsMin.z + Mathf.Max(terrainInset, centeredOffsetZ) + cellSize * 0.5f;
            }

            float worldX = startX + x * cellSize;
            float worldZ = startZ + z * cellSize;
            float sampledY = SampleY(worldX, worldZ, heightOffset);
            return new Vector3(worldX, sampledY, worldZ);
        }

        return new Vector3(x * cellSize, heightOffset, z * cellSize);
    }

    private Vector3 GetGridOrigin(int width, int height)
    {
        if (targetTerrains != null && targetTerrains.Count > 0)
        {
            Bounds combinedBounds = GetCombinedTerrainBounds();
            Vector3 boundsMin = combinedBounds.min;
            Vector3 boundsSize = combinedBounds.size;

            float startX = boundsMin.x + terrainInset;
            float startZ = boundsMin.z + terrainInset;

            if (!autoFitGridToTerrain)
            {
                float centeredOffsetX = (boundsSize.x - width * cellSize) * 0.5f;
                float centeredOffsetZ = (boundsSize.z - height * cellSize) * 0.5f;
                startX = boundsMin.x + Mathf.Max(terrainInset, centeredOffsetX);
                startZ = boundsMin.z + Mathf.Max(terrainInset, centeredOffsetZ);
            }

            return new Vector3(startX, boundsMin.y, startZ);
        }

        return Vector3.zero;
    }

    private float SampleY(float worldX, float worldZ, float extraOffset)
    {
        Terrain terrain = GetTerrainAt(worldX, worldZ);
        if (terrain != null && terrain.terrainData != null)
        {
            float y = terrain.SampleHeight(new Vector3(worldX, terrain.transform.position.y, worldZ));
            return y + terrain.transform.position.y + extraOffset;
        }

        return extraOffset;
    }

    private Terrain GetTerrainAt(float worldX, float worldZ)
    {
        if (targetTerrains == null || targetTerrains.Count == 0) return null;

        foreach (Terrain terrain in targetTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;

            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            if (worldX >= terrainPos.x && worldX <= terrainPos.x + terrainSize.x &&
                worldZ >= terrainPos.z && worldZ <= terrainPos.z + terrainSize.z)
            {
                return terrain;
            }
        }

        return targetTerrains.Count > 0 ? targetTerrains[0] : null;
    }

    private Bounds GetCombinedTerrainBounds()
    {
        if (targetTerrains == null || targetTerrains.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one * 100f);
        }

        Terrain firstValid = null;
        foreach (Terrain t in targetTerrains)
        {
            if (t != null && t.terrainData != null) { firstValid = t; break; }
        }

        if (firstValid == null)
        {
            return new Bounds(Vector3.zero, Vector3.one * 100f);
        }

        Vector3 min = firstValid.transform.position;
        Vector3 max = min + firstValid.terrainData.size;

        foreach (Terrain terrain in targetTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 tMin = terrain.transform.position;
            Vector3 tMax = tMin + terrain.terrainData.size;
            min = Vector3.Min(min, tMin);
            max = Vector3.Max(max, tMax);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        return new Bounds(center, size);
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

    private int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }

    private Vector3 GetDirectionFromMask(int roadNeighborsMask)
    {
        List<Vector3> options = new List<Vector3>(4);
        if ((roadNeighborsMask & North) != 0) options.Add(Vector3.forward);
        if ((roadNeighborsMask & East) != 0) options.Add(Vector3.right);
        if ((roadNeighborsMask & South) != 0) options.Add(Vector3.back);
        if ((roadNeighborsMask & West) != 0) options.Add(Vector3.left);

        if (options.Count == 0) return RandomHorizontalDir();
        return options[Random.Range(0, options.Count)];
    }

    private Vector3 RandomHorizontalDir()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;

        // Try a few times to find a non-null entry
        for (int attempt = 0; attempt < 5; attempt++)
        {
            GameObject pick = list[Random.Range(0, list.Count)];
            if (pick != null) return pick;
        }

        // Fallback: find first non-null
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) return list[i];
        }

        return null;
    }

    // ─── Scene Management ─────────────────────────────────────────────

    private void EnsureParent(ref Transform parent, string name)
    {
        if (parent != null) return;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        parent = go.transform;
    }

    private void ClearChildren(Transform parent, string label)
    {
        if (parent == null) return;

        int count = parent.childCount;
        for (int i = count - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[NaturesPropsTool] Cleared {count} {label} objects.");
        }
    }

    private void AutoFindTerrains()
    {
        Terrain[] allTerrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        targetTerrains.Clear();
        foreach (Terrain terrain in allTerrains)
        {
            if (terrain != null) targetTerrains.Add(terrain);
        }
        Debug.Log($"[NaturesPropsTool] Found {targetTerrains.Count} terrains.");
    }

    private void AutoFindSceneRefs()
    {
        AutoFindTerrains();

        GameObject roadObj = GameObject.Find("SimplePoly_RoadGrid");
        if (roadObj != null) roadParent = roadObj.transform;

        GameObject buildingObj = GameObject.Find("SimplePoly_Buildings");
        if (buildingObj != null) buildingParent = buildingObj.transform;

        GameObject natureObj = GameObject.Find("SimplePoly_Natures");
        if (natureObj != null) natureParent = natureObj.transform;

        GameObject propsObj = GameObject.Find("SimplePoly_Props");
        if (propsObj != null) propsParent = propsObj.transform;

        Debug.Log($"[NaturesPropsTool] Auto-found scene refs. Roads: {roadParent != null}, Buildings: {buildingParent != null}");
    }

    // ─── Auto Load Prefabs ────────────────────────────────────────────

    private void AutoLoadPrefabs()
    {
        // Nature
        treePrefabs.Clear();
        bushPrefabs.Clear();
        grassPrefabs.Clear();
        rockPrefabs.Clear();
        potBushPrefabs.Clear();

        LoadPrefab(treePrefabs, "Natures_Big Tree");
        LoadPrefab(treePrefabs, "Natures_Cube Tree");
        LoadPrefab(treePrefabs, "Natures_Fir Tree");

        LoadPrefab(bushPrefabs, "Natures_Bush_01");
        LoadPrefab(bushPrefabs, "Natures_Bush_02");
        LoadPrefab(bushPrefabs, "Natures_Bush_03");

        LoadPrefab(potBushPrefabs, "Natures_Pot Bush_big");
        LoadPrefab(potBushPrefabs, "Natures_Pot Bush_small");

        LoadPrefab(grassPrefabs, "Natures_Grass Tile");
        LoadPrefab(grassPrefabs, "Natures_Grass Tile Small");
        LoadPrefab(grassPrefabs, "Natures_Grass Bar");
        LoadPrefab(grassPrefabs, "Natures_Grass Fence");

        LoadPrefab(rockPrefabs, "Natures_Rock_Big");
        LoadPrefab(rockPrefabs, "Natures_Rock_small");

        // Props
        benchPrefabs.Clear();
        trafficSignPrefabs.Clear();
        billboardPrefabs.Clear();

        streetLightPrefab = LoadSinglePrefab("Props_Street Light");
        hydrantPrefab = LoadSinglePrefab("Props_Hydrant");
        busStopPrefab = LoadSinglePrefab("Props_Bus Stop");
        dustbinPrefab = LoadSinglePrefab("Props_Dustbin");
        fencePrefab = LoadSinglePrefab("Props_Fence");
        trafficConePrefab = LoadSinglePrefab("Props_Traffic cone");
        coffeeChairPrefab = LoadSinglePrefab("Props_Coffee shop chair");

        LoadPrefab(benchPrefabs, "Props_Bench_1");
        LoadPrefab(benchPrefabs, "Props_Bench_2");

        LoadPrefab(trafficSignPrefabs, "Props_Traffic Sign_stop");
        LoadPrefab(trafficSignPrefabs, "Props_Traffic Sign_speed limit");

        LoadPrefab(billboardPrefabs, "Props_BillBoard_large");
        LoadPrefab(billboardPrefabs, "Props_BillBoard_medium");
        LoadPrefab(billboardPrefabs, "Props_BillBoard_small");

        int totalNature = treePrefabs.Count + bushPrefabs.Count + grassPrefabs.Count + rockPrefabs.Count + potBushPrefabs.Count;
        int totalProps = benchPrefabs.Count + trafficSignPrefabs.Count + billboardPrefabs.Count;
        totalProps += (streetLightPrefab != null ? 1 : 0) + (hydrantPrefab != null ? 1 : 0) +
                      (busStopPrefab != null ? 1 : 0) + (dustbinPrefab != null ? 1 : 0) +
                      (fencePrefab != null ? 1 : 0) + (trafficConePrefab != null ? 1 : 0) +
                      (coffeeChairPrefab != null ? 1 : 0);

        Debug.Log($"[NaturesPropsTool] Auto loaded {totalNature} nature prefabs and {totalProps} props prefabs.");
    }

    private void LoadPrefab(List<GameObject> list, string prefabName)
    {
        // Search in Natures folder first, then Props
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{NaturesFolder}/{prefabName}.prefab");
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{prefabName}.prefab");
        }

        if (prefab != null)
        {
            list.Add(prefab);
        }
        else
        {
            Debug.LogWarning($"[NaturesPropsTool] Could not find prefab: {prefabName}");
        }
    }

    private GameObject LoadSinglePrefab(string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{prefabName}.prefab");
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{NaturesFolder}/{prefabName}.prefab");
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[NaturesPropsTool] Could not find prefab: {prefabName}");
        }

        return prefab;
    }

    // ─── Data Structures ──────────────────────────────────────────────

    private readonly struct PlacedItem
    {
        public readonly Vector2 position;
        public readonly float radius;

        public PlacedItem(Vector2 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }
}
