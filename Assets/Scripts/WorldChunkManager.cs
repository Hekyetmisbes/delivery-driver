using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// Manages world chunking and streaming for large city environments
    /// Implements near/mid/far ring system for optimal performance
    /// Sprint 2: Chunking + Streaming system
    /// </summary>
    public class WorldChunkManager : MonoBehaviour
    {
        [Header("Chunk Configuration")]
        [Tooltip("Size of each chunk in world units")]
        public float chunkSize = 64f;

        [Tooltip("Auto-detect all WorldChunk components in scene on start")]
        public bool autoDetectChunks = true;

        [Header("Ring Distances")]
        [Tooltip("Near ring: Full detail (0 to nearRingDistance)")]
        public float nearRingDistance = 150f;

        [Tooltip("Mid ring: Proxy detail (nearRingDistance to midRingDistance)")]
        public float midRingDistance = 300f;

        [Tooltip("Far ring: Unloaded (beyond midRingDistance)")]
        public float farRingDistance = 500f;

        [Header("Player Tracking")]
        [Tooltip("Transform to track (usually player vehicle)")]
        public Transform playerTransform;

        [Tooltip("Auto-find player if not set")]
        public bool autoFindPlayer = true;

        [Header("Update Settings")]
        [Tooltip("How often to update chunk states (seconds)")]
        public float updateInterval = 0.5f;

        [Tooltip("Maximum chunks to update per frame (prevents spikes)")]
        public int maxChunkUpdatesPerFrame = 4;

        [Header("Debug")]
        public bool showDebugInfo = true;
        public bool drawGizmos = true;

        // Internal state
        private Dictionary<Vector2Int, WorldChunk> chunks = new Dictionary<Vector2Int, WorldChunk>();
        private float lastUpdateTime;
        private Queue<WorldChunk> chunksToUpdate = new Queue<WorldChunk>();
        private Vector2Int lastPlayerChunkPos = new Vector2Int(int.MinValue, int.MinValue);

        // Statistics
        private int chunksInNearRing;
        private int chunksInMidRing;
        private int chunksInFarRing;

        private void Start()
        {
            InitializeChunkManager();
        }

        private void InitializeChunkManager()
        {
            // Auto-find player if needed
            if (playerTransform == null && autoFindPlayer)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                    Debug.Log($"[WorldChunkManager] Auto-found player: {player.name}");
                }
                else
                {
                    Debug.LogWarning("[WorldChunkManager] No player found! Set playerTransform manually or add 'Player' tag.");
                }
            }

            // Auto-detect chunks in scene
            if (autoDetectChunks)
            {
                DiscoverChunks();
            }

            Debug.Log($"[WorldChunkManager] Initialized with {chunks.Count} chunks");
        }

        /// <summary>
        /// Find all WorldChunk components in the scene and register them
        /// </summary>
        public void DiscoverChunks()
        {
            chunks.Clear();
            WorldChunk[] foundChunks = FindObjectsOfType<WorldChunk>();

            foreach (var chunk in foundChunks)
            {
                RegisterChunk(chunk);
            }

            Debug.Log($"[WorldChunkManager] Discovered {chunks.Count} chunks in scene");
        }

        /// <summary>
        /// Register a chunk with the manager
        /// </summary>
        public void RegisterChunk(WorldChunk chunk)
        {
            if (chunk == null) return;

            // Calculate grid position if not set
            if (chunk.gridPosition == Vector2Int.zero)
            {
                Vector3 pos = chunk.transform.position;
                chunk.gridPosition = new Vector2Int(
                    Mathf.FloorToInt(pos.x / chunkSize),
                    Mathf.FloorToInt(pos.z / chunkSize)
                );
            }

            chunk.chunkSize = chunkSize;

            if (!chunks.ContainsKey(chunk.gridPosition))
            {
                chunks[chunk.gridPosition] = chunk;
            }
        }

        /// <summary>
        /// Unregister a chunk from the manager
        /// </summary>
        public void UnregisterChunk(WorldChunk chunk)
        {
            if (chunk != null && chunks.ContainsKey(chunk.gridPosition))
            {
                chunks.Remove(chunk.gridPosition);
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Update chunks at specified interval
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateChunkStates();
            }

            // Process queued chunk updates (budget per frame)
            ProcessChunkUpdateQueue();
        }

        private void UpdateChunkStates()
        {
            if (chunks.Count == 0) return;

            Vector3 playerPos = playerTransform.position;
            Vector2Int currentPlayerChunk = WorldPositionToChunkCoord(playerPos);

            // Only rebuild queue if player moved to a different chunk
            if (currentPlayerChunk != lastPlayerChunkPos)
            {
                lastPlayerChunkPos = currentPlayerChunk;
                BuildChunkUpdateQueue(playerPos);
            }
        }

        private void BuildChunkUpdateQueue(Vector3 playerPos)
        {
            chunksToUpdate.Clear();
            chunksInNearRing = 0;
            chunksInMidRing = 0;
            chunksInFarRing = 0;

            // Sort chunks by distance from player
            var sortedChunks = chunks.Values.OrderBy(chunk =>
                Vector3.Distance(playerPos, chunk.GetWorldCenter())
            );

            foreach (var chunk in sortedChunks)
            {
                chunksToUpdate.Enqueue(chunk);
            }
        }

        private void ProcessChunkUpdateQueue()
        {
            if (playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;
            int updatesThisFrame = 0;

            while (chunksToUpdate.Count > 0 && updatesThisFrame < maxChunkUpdatesPerFrame)
            {
                WorldChunk chunk = chunksToUpdate.Dequeue();
                if (chunk == null) continue;

                float distance = Vector3.Distance(playerPos, chunk.GetWorldCenter());
                WorldChunk.ChunkState targetState = DetermineChunkState(distance);

                // Update chunk state if changed
                if (chunk.currentState != targetState)
                {
                    chunk.SetState(targetState);
                    updatesThisFrame++;
                }

                // Update statistics
                switch (targetState)
                {
                    case WorldChunk.ChunkState.LoadedFull:
                        chunksInNearRing++;
                        break;
                    case WorldChunk.ChunkState.LoadedProxy:
                        chunksInMidRing++;
                        break;
                    case WorldChunk.ChunkState.Unloaded:
                        chunksInFarRing++;
                        break;
                }
            }
        }

        private WorldChunk.ChunkState DetermineChunkState(float distance)
        {
            if (distance <= nearRingDistance)
                return WorldChunk.ChunkState.LoadedFull;
            else if (distance <= midRingDistance)
                return WorldChunk.ChunkState.LoadedProxy;
            else
                return WorldChunk.ChunkState.Unloaded;
        }

        /// <summary>
        /// Convert world position to chunk grid coordinates
        /// </summary>
        public Vector2Int WorldPositionToChunkCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / chunkSize),
                Mathf.FloorToInt(worldPos.z / chunkSize)
            );
        }

        /// <summary>
        /// Get chunk at specific grid position
        /// </summary>
        public WorldChunk GetChunkAtGridPosition(Vector2Int gridPos)
        {
            chunks.TryGetValue(gridPos, out WorldChunk chunk);
            return chunk;
        }

        /// <summary>
        /// Get chunk containing a world position
        /// </summary>
        public WorldChunk GetChunkAtWorldPosition(Vector3 worldPos)
        {
            Vector2Int gridPos = WorldPositionToChunkCoord(worldPos);
            return GetChunkAtGridPosition(gridPos);
        }

        /// <summary>
        /// Get all chunks within a certain distance from a point
        /// </summary>
        public List<WorldChunk> GetChunksInRadius(Vector3 center, float radius)
        {
            List<WorldChunk> result = new List<WorldChunk>();

            foreach (var chunk in chunks.Values)
            {
                if (Vector3.Distance(center, chunk.GetWorldCenter()) <= radius)
                {
                    result.Add(chunk);
                }
            }

            return result;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;

            // Draw ring boundaries
            Gizmos.color = Color.green;
            DrawCircle(playerPos, nearRingDistance);

            Gizmos.color = Color.yellow;
            DrawCircle(playerPos, midRingDistance);

            Gizmos.color = Color.red;
            DrawCircle(playerPos, farRingDistance);
        }

        private void DrawCircle(Vector3 center, float radius, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 150, 300, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>World Chunk Manager</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Total Chunks: {chunks.Count}");
            GUILayout.Label($"Near Ring (Full): {chunksInNearRing}");
            GUILayout.Label($"Mid Ring (Proxy): {chunksInMidRing}");
            GUILayout.Label($"Far Ring (Unloaded): {chunksInFarRing}");
            GUILayout.Label($"Update Queue: {chunksToUpdate.Count}");

            if (playerTransform != null)
            {
                Vector2Int playerChunk = WorldPositionToChunkCoord(playerTransform.position);
                GUILayout.Label($"Player Chunk: ({playerChunk.x}, {playerChunk.y})");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
