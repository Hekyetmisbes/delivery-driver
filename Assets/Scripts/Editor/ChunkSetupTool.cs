using UnityEngine;
using UnityEditor;
using DeliveryDriver.Optimization;

namespace DeliveryDriver.Editor
{
    /// <summary>
    /// Editor tool for setting up world chunking system
    /// Tools > Performance > Chunk Setup
    /// </summary>
    public class ChunkSetupTool : EditorWindow
    {
        private float chunkSize = 64f;
        private Vector2Int gridSize = new Vector2Int(10, 10);
        private Vector3 gridOrigin = Vector3.zero;
        private bool createProxyObjects = true;
        private GameObject chunkParent;

        [MenuItem("Tools/Performance/Chunk Setup")]
        public static void ShowWindow()
        {
            GetWindow<ChunkSetupTool>("Chunk Setup Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("World Chunk Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool helps you set up the world chunking system for performance optimization.\n" +
                "Sprint 2: Chunking + Streaming",
                MessageType.Info
            );

            EditorGUILayout.Space();

            // Configuration
            GUILayout.Label("Chunk Configuration", EditorStyles.boldLabel);
            chunkSize = EditorGUILayout.FloatField("Chunk Size (meters)", chunkSize);
            gridSize = EditorGUILayout.Vector2IntField("Grid Size (chunks)", gridSize);
            gridOrigin = EditorGUILayout.Vector3Field("Grid Origin", gridOrigin);
            createProxyObjects = EditorGUILayout.Toggle("Create Proxy Objects", createProxyObjects);
            chunkParent = EditorGUILayout.ObjectField("Chunk Parent (optional)", chunkParent, typeof(GameObject), true) as GameObject;

            EditorGUILayout.Space();

            // Quick Setup Buttons
            GUILayout.Label("Quick Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Add WorldChunkManager to Scene", GUILayout.Height(30)))
            {
                AddChunkManagerToScene();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Auto-Setup Chunks from Terrain", GUILayout.Height(30)))
            {
                AutoSetupChunksFromTerrain();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Manual Chunk Grid", GUILayout.Height(30)))
            {
                CreateManualChunkGrid();
            }

            EditorGUILayout.Space();

            // Utility Buttons
            GUILayout.Label("Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Find and Register All Chunks"))
            {
                FindAndRegisterChunks();
            }

            if (GUILayout.Button("Validate Chunk Setup"))
            {
                ValidateChunkSetup();
            }
        }

        private void AddChunkManagerToScene()
        {
            // Check if already exists
            WorldChunkManager existing = FindFirstObjectByType<WorldChunkManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "WorldChunkManager already exists in the scene.",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new manager
            GameObject managerObj = new GameObject("WorldChunkManager");
            WorldChunkManager manager = managerObj.AddComponent<WorldChunkManager>();
            manager.chunkSize = chunkSize;
            manager.nearRingDistance = 150f;
            manager.midRingDistance = 300f;
            manager.farRingDistance = 500f;
            manager.autoDetectChunks = true;
            manager.autoFindPlayer = true;

            Undo.RegisterCreatedObjectUndo(managerObj, "Create WorldChunkManager");
            Selection.activeGameObject = managerObj;

            Debug.Log("[ChunkSetupTool] WorldChunkManager created successfully!");
            EditorUtility.DisplayDialog("Success",
                "WorldChunkManager has been added to the scene.\n\n" +
                "Next steps:\n" +
                "1. Enter Play Mode to auto-detect player\n" +
                "2. Create chunks using the other tools\n" +
                "3. The system will automatically manage chunk states",
                "OK");
        }

        private void AutoSetupChunksFromTerrain()
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("No Terrain",
                    "No terrain found in the scene. Use 'Create Manual Chunk Grid' instead.",
                    "OK");
                return;
            }

            int totalChunks = 0;
            GameObject parentObj = chunkParent != null ? chunkParent : new GameObject("World Chunks");

            foreach (Terrain terrain in terrains)
            {
                Vector3 terrainSize = terrain.terrainData.size;
                Vector3 terrainPos = terrain.transform.position;

                int chunksX = Mathf.CeilToInt(terrainSize.x / chunkSize);
                int chunksZ = Mathf.CeilToInt(terrainSize.z / chunkSize);

                for (int x = 0; x < chunksX; x++)
                {
                    for (int z = 0; z < chunksZ; z++)
                    {
                        Vector3 chunkPos = terrainPos + new Vector3(x * chunkSize, 0, z * chunkSize);
                        CreateChunkAtPosition(chunkPos, new Vector2Int(x, z), parentObj);
                        totalChunks++;
                    }
                }
            }

            Debug.Log($"[ChunkSetupTool] Created {totalChunks} chunks from terrain");
            EditorUtility.DisplayDialog("Success",
                $"Created {totalChunks} chunks based on terrain size.\n\n" +
                $"Chunk Size: {chunkSize}m\n" +
                $"Total Chunks: {totalChunks}",
                "OK");
        }

        private void CreateManualChunkGrid()
        {
            if (gridSize.x <= 0 || gridSize.y <= 0)
            {
                EditorUtility.DisplayDialog("Invalid Grid Size",
                    "Grid size must be greater than 0.",
                    "OK");
                return;
            }

            GameObject parentObj = chunkParent != null ? chunkParent : new GameObject("World Chunks");
            int totalChunks = 0;

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    Vector3 chunkPos = gridOrigin + new Vector3(x * chunkSize, 0, z * chunkSize);
                    CreateChunkAtPosition(chunkPos, new Vector2Int(x, z), parentObj);
                    totalChunks++;
                }
            }

            Debug.Log($"[ChunkSetupTool] Created {totalChunks} chunks in manual grid");
            EditorUtility.DisplayDialog("Success",
                $"Created {totalChunks} chunks in a {gridSize.x}x{gridSize.y} grid.\n\n" +
                $"Chunk Size: {chunkSize}m\n" +
                $"Origin: {gridOrigin}",
                "OK");
        }

        private void CreateChunkAtPosition(Vector3 position, Vector2Int gridPos, GameObject parent)
        {
            GameObject chunkObj = new GameObject($"Chunk_{gridPos.x}_{gridPos.y}");
            chunkObj.transform.position = position;
            chunkObj.transform.parent = parent.transform;

            WorldChunk chunk = chunkObj.AddComponent<WorldChunk>();
            chunk.gridPosition = gridPos;
            chunk.chunkSize = chunkSize;

            // Create content containers
            GameObject fullContent = new GameObject("FullDetailContent");
            fullContent.transform.parent = chunkObj.transform;
            fullContent.transform.localPosition = Vector3.zero;
            chunk.fullDetailContent = fullContent;

            if (createProxyObjects)
            {
                GameObject proxyContent = new GameObject("ProxyContent");
                proxyContent.transform.parent = chunkObj.transform;
                proxyContent.transform.localPosition = Vector3.zero;
                proxyContent.SetActive(false); // Start disabled
                chunk.proxyContent = proxyContent;
            }

            Undo.RegisterCreatedObjectUndo(chunkObj, "Create World Chunk");
        }

        private void FindAndRegisterChunks()
        {
            WorldChunkManager manager = FindFirstObjectByType<WorldChunkManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("No Manager",
                    "No WorldChunkManager found in scene. Create one first.",
                    "OK");
                return;
            }

            manager.DiscoverChunks();
            EditorUtility.DisplayDialog("Success",
                "Chunk discovery completed. Check console for details.",
                "OK");
        }

        private void ValidateChunkSetup()
        {
            WorldChunkManager manager = FindFirstObjectByType<WorldChunkManager>();
            WorldChunk[] chunks = FindObjectsByType<WorldChunk>(FindObjectsSortMode.None);

            string report = "=== Chunk Setup Validation ===\n\n";

            // Check manager
            if (manager == null)
            {
                report += "❌ No WorldChunkManager found in scene\n";
            }
            else
            {
                report += "✓ WorldChunkManager found\n";
                report += $"  - Chunk Size: {manager.chunkSize}m\n";
                report += $"  - Near Ring: {manager.nearRingDistance}m\n";
                report += $"  - Mid Ring: {manager.midRingDistance}m\n";
                report += $"  - Far Ring: {manager.farRingDistance}m\n";
                report += $"  - Player: {(manager.playerTransform != null ? "Set" : "Not Set (will auto-find)")}\n\n";
            }

            // Check chunks
            report += $"Total Chunks: {chunks.Length}\n";
            int chunksWithFullContent = 0;
            int chunksWithProxyContent = 0;

            foreach (var chunk in chunks)
            {
                if (chunk.fullDetailContent != null) chunksWithFullContent++;
                if (chunk.proxyContent != null) chunksWithProxyContent++;
            }

            report += $"  - With Full Detail Content: {chunksWithFullContent}\n";
            report += $"  - With Proxy Content: {chunksWithProxyContent}\n\n";

            // Recommendations
            report += "=== Recommendations ===\n";
            if (manager == null)
                report += "• Add WorldChunkManager to scene\n";
            if (chunks.Length == 0)
                report += "• Create chunks using the setup tools\n";
            if (chunksWithProxyContent == 0)
                report += "• Consider creating proxy content for better performance\n";

            Debug.Log(report);
            EditorUtility.DisplayDialog("Validation Complete", report, "OK");
        }
    }
}
