using UnityEngine;
using System.Collections.Generic;

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
        public float nearRingDistance = 90f;

        [Tooltip("Mid ring: Proxy detail (nearRingDistance to midRingDistance)")]
        public float midRingDistance = 150f;

        [Tooltip("Far ring: Unloaded (beyond midRingDistance)")]
        public float farRingDistance = 170f;

        [Header("Player Tracking")]
        [Tooltip("Transform to track (usually player vehicle)")]
        public Transform playerTransform;

        [Tooltip("Auto-find player if not set")]
        public bool autoFindPlayer = true;

        [Header("Update Settings")]
        [Tooltip("How often to update chunk states (seconds)")]
        public float updateInterval = 0.25f;

        [Tooltip("Maximum chunks to update per frame (prevents spikes)")]
        public int maxChunkUpdatesPerFrame = 16;

        [Header("Debug")]
        public bool showDebugInfo = false;
        public bool drawGizmos = true;

        [Header("Hard Radius Culling")]
        [Tooltip("Force-cull scene renderers outside far ring radius even if not wired to WorldChunk content")]
        public bool enableHardRadiusCulling = true;

        [Tooltip("Only cull static world renderers (disable if roads/buildings are not marked static)")]
        public bool cullStaticRenderersOnly = false;

        [Tooltip("Extra buffer to reduce pop-in near the radius boundary")]
        public float rendererCullingPadding = 8f;

        [Tooltip("How often to refresh dynamic renderer/NPC caches")]
        public float cacheRefreshInterval = 30f;

        // Internal state
        private Dictionary<Vector2Int, WorldChunk> chunks = new Dictionary<Vector2Int, WorldChunk>();
        private float lastUpdateTime;
        private Queue<WorldChunk> chunksToUpdate = new Queue<WorldChunk>();
        private float lastCacheRefreshTime;
        private readonly List<Renderer> hardCulledRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, bool> rendererVisibilityState = new Dictionary<Renderer, bool>();
        private readonly List<TrafficSystem.NpcCarAgent> npcAgents = new List<TrafficSystem.NpcCarAgent>();
        private readonly Dictionary<TrafficSystem.NpcCarAgent, Renderer[]> npcRendererCache = new Dictionary<TrafficSystem.NpcCarAgent, Renderer[]>();
        private readonly Dictionary<TrafficSystem.NpcCarAgent, bool> npcVisibilityState = new Dictionary<TrafficSystem.NpcCarAgent, bool>();
        private readonly HashSet<Transform> chunkRootSet = new HashSet<Transform>();
        private readonly List<ChunkQueueEntry> chunkQueueBuffer = new List<ChunkQueueEntry>();
        private Camera cachedMainCamera;
        private GUIStyle debugHeaderStyle;
        private Renderer[] cachedSceneRenderers;

        private struct ChunkQueueEntry
        {
            public WorldChunk chunk;
            public float sqrDistance;
            public bool prioritizeUnload;
        }

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
            cachedMainCamera = Camera.main;

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

            if (enableHardRadiusCulling)
            {
                RefreshCullingCaches(force: true);
            }

            Debug.Log($"[WorldChunkManager] Initialized with {chunks.Count} chunks");
        }

        /// <summary>
        /// Find all WorldChunk components in the scene and register them
        /// </summary>
        public void DiscoverChunks()
        {
            chunks.Clear();
            WorldChunk[] foundChunks = FindObjectsByType<WorldChunk>(FindObjectsSortMode.None);

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

                if (enableHardRadiusCulling)
                {
                    ApplyHardRadiusCulling(playerTransform.position);
                }
            }

            // Process queued chunk updates (budget per frame)
            ProcessChunkUpdateQueue();
        }

        private void UpdateChunkStates()
        {
            if (chunks.Count == 0) return;

            Vector3 playerPos = playerTransform.position;
            // Rebuild every interval so chunks outside the active circle are unloaded immediately.
            BuildChunkUpdateQueue(playerPos);
        }

        private void ApplyHardRadiusCulling(Vector3 playerPos)
        {
            RefreshCullingCaches(force: false);

            float rendererRadius = GetProxyDistanceLimit() + Mathf.Max(0f, rendererCullingPadding);
            float rendererRadiusSqr = rendererRadius * rendererRadius;
            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);

            for (int i = 0; i < hardCulledRenderers.Count; i++)
            {
                Renderer renderer = hardCulledRenderers[i];
                if (renderer == null) continue;

                bool shouldBeVisible = IsRendererWithinRadius(renderer, playerXZ, rendererRadiusSqr);
                if (rendererVisibilityState.TryGetValue(renderer, out bool currentVisible) && currentVisible == shouldBeVisible)
                {
                    continue;
                }

                renderer.enabled = shouldBeVisible;
                rendererVisibilityState[renderer] = shouldBeVisible;
            }

            float npcRadiusSqr = GetProxyDistanceLimit() * GetProxyDistanceLimit();
            for (int i = 0; i < npcAgents.Count; i++)
            {
                TrafficSystem.NpcCarAgent npc = npcAgents[i];
                if (npc == null) continue;

                Vector3 npcPos = npc.transform.position;
                float dx = npcPos.x - playerPos.x;
                float dz = npcPos.z - playerPos.z;
                bool npcVisible = dx * dx + dz * dz <= npcRadiusSqr;

                // Skip if visibility hasn't changed - avoids expensive renderer.enabled writes
                if (npcVisibilityState.TryGetValue(npc, out bool currentVisible) && currentVisible == npcVisible)
                {
                    continue;
                }

                npcVisibilityState[npc] = npcVisible;

                if (!npcRendererCache.TryGetValue(npc, out Renderer[] npcRenderers) || npcRenderers == null)
                {
                    npcRenderers = npc.GetComponentsInChildren<Renderer>(true);
                    npcRendererCache[npc] = npcRenderers;
                }

                for (int r = 0; r < npcRenderers.Length; r++)
                {
                    if (npcRenderers[r] != null)
                    {
                        npcRenderers[r].enabled = npcVisible;
                    }
                }
            }
        }

        private void RefreshCullingCaches(bool force)
        {
            if (!force && Time.time - lastCacheRefreshTime < cacheRefreshInterval)
            {
                return;
            }

            lastCacheRefreshTime = Time.time;

            // Preserve previous visibility states to avoid redundant renderer.enabled writes
            // that trigger expensive editor MaterialEditor::ApplyMaterialProperty calls
            var prevRendererStates = new Dictionary<Renderer, bool>(rendererVisibilityState);
            var prevNpcStates = new Dictionary<TrafficSystem.NpcCarAgent, bool>(npcVisibilityState);

            hardCulledRenderers.Clear();
            rendererVisibilityState.Clear();
            npcAgents.Clear();
            npcRendererCache.Clear();
            npcVisibilityState.Clear();
            chunkRootSet.Clear();

            foreach (WorldChunk chunk in chunks.Values)
            {
                if (chunk != null)
                {
                    chunkRootSet.Add(chunk.transform);
                }
            }

            // On first scan, use FindObjectsByType and cache; subsequent refreshes
            // only clean up destroyed renderers to avoid massive allocation.
            if (cachedSceneRenderers == null || force)
            {
                cachedSceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < cachedSceneRenderers.Length; i++)
            {
                Renderer renderer = cachedSceneRenderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer.transform == playerTransform || renderer.transform.IsChildOf(transform)) continue;
                if (playerTransform != null && renderer.transform.IsChildOf(playerTransform)) continue;
                if (cachedMainCamera != null && renderer.transform.IsChildOf(cachedMainCamera.transform)) continue;
                if (renderer is ParticleSystemRenderer) continue;
                if (cullStaticRenderersOnly && !renderer.gameObject.isStatic) continue;

                if (IsRendererManagedByChunk(renderer.transform))
                {
                    continue;
                }

                hardCulledRenderers.Add(renderer);
                // Restore previous visibility state if known, avoiding a full re-toggle
                if (prevRendererStates.TryGetValue(renderer, out bool prevVisible))
                {
                    rendererVisibilityState[renderer] = prevVisible;
                }
                else
                {
                    rendererVisibilityState[renderer] = renderer.enabled;
                }
            }

            // Use TrafficCommunicationSystem to get NPCs instead of FindObjectsByType
            if (TrafficSystem.TrafficCommunicationSystem.Instance != null)
            {
                TrafficSystem.TrafficCommunicationSystem.Instance.GetAllVehicles(npcAgents);
            }
            else
            {
                // Fallback only if TrafficCommunicationSystem not ready yet
                var foundNpcs = FindObjectsByType<TrafficSystem.NpcCarAgent>(FindObjectsSortMode.None);
                npcAgents.AddRange(foundNpcs);
            }

            for (int i = 0; i < npcAgents.Count; i++)
            {
                TrafficSystem.NpcCarAgent npc = npcAgents[i];
                if (npc == null) continue;

                npcRendererCache[npc] = npc.GetComponentsInChildren<Renderer>(true);
                // Restore previous NPC visibility state
                if (prevNpcStates.TryGetValue(npc, out bool prevNpcVisible))
                {
                    npcVisibilityState[npc] = prevNpcVisible;
                }
            }
        }

        private bool IsRendererManagedByChunk(Transform transformToCheck)
        {
            Transform current = transformToCheck;
            while (current != null)
            {
                if (chunkRootSet.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsRendererWithinRadius(Renderer renderer, Vector2 playerXZ, float radiusSqr)
        {
            Bounds b = renderer.bounds;
            Vector3 closest = b.ClosestPoint(new Vector3(playerXZ.x, b.center.y, playerXZ.y));
            float dx = closest.x - playerXZ.x;
            float dz = closest.z - playerXZ.y;
            return dx * dx + dz * dz <= radiusSqr;
        }

        private void BuildChunkUpdateQueue(Vector3 playerPos)
        {
            chunksToUpdate.Clear();
            chunkQueueBuffer.Clear();
            chunksInNearRing = 0;
            chunksInMidRing = 0;
            chunksInFarRing = 0;
            float proxyDistanceLimit = GetProxyDistanceLimit();
            float proxyDistanceLimitSqr = proxyDistanceLimit * proxyDistanceLimit;

            foreach (WorldChunk chunk in chunks.Values)
            {
                if (chunk == null) continue;

                Vector3 center = chunk.GetWorldCenter();
                float dx = center.x - playerPos.x;
                float dz = center.z - playerPos.z;
                float sqrDistance = dx * dx + dz * dz;

                chunkQueueBuffer.Add(new ChunkQueueEntry
                {
                    chunk = chunk,
                    sqrDistance = sqrDistance,
                    prioritizeUnload = sqrDistance > proxyDistanceLimitSqr && chunk.currentState != WorldChunk.ChunkState.Unloaded
                });
            }

            chunkQueueBuffer.Sort((a, b) =>
            {
                if (a.prioritizeUnload != b.prioritizeUnload)
                {
                    return a.prioritizeUnload ? -1 : 1;
                }

                return a.sqrDistance.CompareTo(b.sqrDistance);
            });

            for (int i = 0; i < chunkQueueBuffer.Count; i++)
            {
                chunksToUpdate.Enqueue(chunkQueueBuffer[i].chunk);
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
            {
                return WorldChunk.ChunkState.LoadedFull;
            }

            // Keep proxy chunks only inside the streaming circle; unload everything outside it.
            float proxyDistanceLimit = GetProxyDistanceLimit();
            if (distance <= proxyDistanceLimit)
            {
                return WorldChunk.ChunkState.LoadedProxy;
            }

            return WorldChunk.ChunkState.Unloaded;
        }

        private float GetProxyDistanceLimit()
        {
            return Mathf.Max(midRingDistance, farRingDistance);
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

            if (debugHeaderStyle == null)
            {
                debugHeaderStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }

            GUILayout.Label($"<b>World Chunk Manager</b>", debugHeaderStyle);
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
