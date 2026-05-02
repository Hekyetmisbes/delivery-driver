using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Company;
using DeliveryDriver.Optimization;
using DeliveryDriver.Vehicle;

namespace TrafficSystem
{
    /// <summary>
    /// Spawns NPC vehicles on the road network with proper spacing
    /// Supports object pooling for performance
    /// </summary>
    public class NpcSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Road graph builder that contains the road network")]
        [SerializeField] private RoadGraphBuilder roadGraphBuilder;

        [Header("Spawn Settings")]
        [Tooltip("NPC vehicle prefabs to spawn (should have NpcCarAgent + NpcRecovery)")]
        [SerializeField] private GameObject[] npcVehiclePrefabs;
        [Tooltip("Number of NPCs to spawn")]
        [SerializeField] private int spawnCount = 10;
        [Tooltip("Minimum distance between spawned vehicles (meters)")]
        [SerializeField] private float minimumSpawnSpacing = 30f;  // Increased to prevent overlaps
        [Tooltip("Check radius for existing vehicles/obstacles at spawn point (meters)")]
        [SerializeField] private float spawnCheckRadius = 8f;  // Increased for better detection
        [Tooltip("Small radius used to reject static obstacles directly overlapping the spawn pose")]
        [SerializeField] private float staticSpawnObstacleRadius = 2.4f;
        [Tooltip("Layer mask for checking obstacles at spawn point")]
        [SerializeField] private LayerMask spawnObstacleCheckMask = ~0;
        [Tooltip("Spawn vehicles on Start()")]
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Initial delay before starting spawn (seconds, to let road graph build)")]
        [SerializeField] private float initialSpawnDelay = 0.5f;
        [Tooltip("Delay between each NPC spawn (seconds)")]
        [SerializeField] private float spawnDelay = 0.8f;  // Increased from 0.3f to give time to accelerate
        [Tooltip("Treat spawn spacing as planar road distance (ignores Y)")]
        [SerializeField] private bool usePlanarSpawnSpacing = true;
        [Tooltip("Extra clearance multiplier around each spawned vehicle")]
        [SerializeField] private float spawnClearanceMultiplier = 1.15f;
        [Tooltip("Raycast mask for grounding spawn position")]
        [SerializeField] private LayerMask spawnGroundMask = ~0;
        [Tooltip("Raycast height above waypoint for grounding")]
        [SerializeField] private float spawnRaycastHeight = 5f;
        [Tooltip("Raycast distance downward for grounding")]
        [SerializeField] private float spawnRaycastDistance = 10f;
        [Tooltip("Reject spawn points that are currently visible in the player's camera")]
        [SerializeField] private bool avoidSpawningInCameraView = true;
        [Tooltip("Viewport padding used when rejecting visible spawn points")]
        [SerializeField, Range(0f, 0.35f)] private float cameraSpawnViewportMargin = 0.08f;
        [Tooltip("Only reject visible spawn points within this camera distance")]
        [SerializeField] private float cameraVisibleSpawnRejectDistance = 160f;
        [Tooltip("Line of sight mask for visible spawn rejection")]
        [SerializeField] private LayerMask cameraVisibilityMask = ~0;

        [Header("Pooling")]
        [Tooltip("Enable object pooling (disable/enable instead of destroy/instantiate)")]
        [SerializeField] private bool enablePooling = true;
        [Tooltip("Warm the NPC pool gradually after scene start. Disabled by default to avoid startup freezes.")]
        [SerializeField] private bool warmPoolOnStart = false;
        [Tooltip("Delay before optional gradual pool warmup starts")]
        [SerializeField] private float poolWarmupDelay = 1.5f;
        [Tooltip("How many NPC prefabs may be instantiated per warmup frame")]
        [SerializeField] private int poolWarmupPerFrame = 1;
        [Tooltip("Pre-pool extra vehicles")]
        [SerializeField] private int poolExtraCount = 5;

        [Header("Dynamic Traffic Ring")]
        [Tooltip("Keep traffic centered around the player instead of filling the whole map at once")]
        [SerializeField] private bool keepTrafficAroundPlayer = true;
        [Tooltip("Desired active NPC count around the player")]
        [SerializeField] private int targetActiveNpcCount = 12;
        [Tooltip("Do not spawn traffic too close to the player")]
        [SerializeField] private float playerSpawnMinDistance = 45f;
        [Tooltip("Maximum distance from the player where new traffic can spawn")]
        [SerializeField] private float playerSpawnMaxDistance = 120f;
        [Tooltip("NPCs farther than this are recycled back into the pool")]
        [SerializeField] private float playerDespawnDistance = 180f;
        [Tooltip("How often to try filling missing nearby traffic")]
        [SerializeField] private float dynamicSpawnInterval = 0.35f;
        [Tooltip("How often to recycle distant traffic")]
        [SerializeField] private float dynamicDespawnInterval = 1.0f;
        [Tooltip("Max spawn attempts made in one maintenance tick")]
        [SerializeField] private int maxSpawnAttemptsPerTick = 8;
        [Tooltip("Max successful spawns performed in one maintenance tick")]
        [SerializeField] private int maxSpawnsPerTick = 2;
        [Tooltip("Resolve the player transform automatically when missing")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private Transform playerTransform;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Runtime data
        private List<GameObject> activeNpcs = new List<GameObject>();
        private List<GameObject> pooledNpcs = new List<GameObject>();
        private HashSet<GameObject> pooledNpcSet = new HashSet<GameObject>();
        private Transform npcContainer;
        private Coroutine spawnCoroutine;
        private Coroutine poolWarmupCoroutine;
        public bool HasPendingOrActiveSpawn => spawnCoroutine != null || activeNpcs.Count > 0;
        private float nextDynamicSpawnTime;
        private float nextDynamicDespawnTime;
        private float nextPlayerResolveTime;

        // Cached allocations to avoid GC pressure
        private static readonly Collider[] spawnOverlapBuffer = new Collider[32];
        private WaitForSeconds cachedSpawnDelay;

        private void Start()
        {
            // Create container for organization
            npcContainer = new GameObject("NPC Vehicles").transform;
            npcContainer.SetParent(transform);

            // Priority 2: Ensure traffic communication system exists
            EnsureTrafficCommunicationSystem();

            if (enablePooling && warmPoolOnStart)
            {
                poolWarmupCoroutine = StartCoroutine(WarmPoolGradually(GetDesiredPoolSize()));
            }

            if (spawnOnStart)
            {
                if (keepTrafficAroundPlayer)
                {
                    float startDelay = Mathf.Max(0f, initialSpawnDelay);
                    nextDynamicSpawnTime = Time.time + startDelay;
                    nextDynamicDespawnTime = Time.time + startDelay;
                }
                else
                {
                    SpawnNpcsDeferred(0f);
                }
            }
        }

        private void Update()
        {
            if (!spawnOnStart || !keepTrafficAroundPlayer)
            {
                return;
            }

            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null || roadGraphBuilder.RoadGraph.roadSegments.Count == 0)
            {
                return;
            }

            if (!TryResolvePlayerTransform(out Transform player))
            {
                return;
            }

            if (Time.time >= nextDynamicDespawnTime)
            {
                nextDynamicDespawnTime = Time.time + Mathf.Max(0.25f, dynamicDespawnInterval);
                RecycleFarTraffic(player.position);
            }

            if (Time.time >= nextDynamicSpawnTime)
            {
                nextDynamicSpawnTime = Time.time + Mathf.Max(0.1f, dynamicSpawnInterval);
                MaintainTrafficRing(player.position);
            }
        }

        /// <summary>
        /// Ensure traffic communication system exists in scene
        /// </summary>
        private void EnsureTrafficCommunicationSystem()
        {
            if (TrafficCommunicationSystem.Instance == null)
            {
                GameObject commsObj = new GameObject("TrafficCommunicationSystem");
                commsObj.AddComponent<TrafficCommunicationSystem>();
                // Debug.Log("[NpcSpawner] Created TrafficCommunicationSystem");
            }

            // Priority 3: Ensure weather manager exists
            if (WeatherManager.Instance == null)
            {
                GameObject weatherObj = new GameObject("WeatherManager");
                weatherObj.AddComponent<WeatherManager>();
                // Debug.Log("[NpcSpawner] Created WeatherManager");
            }
        }

        /// <summary>
        /// Spawn NPCs on the road network
        /// </summary>
        [ContextMenu("Spawn NPCs")]
        public void SpawnNpcs()
        {
            SpawnNpcsDeferred(0f);
        }

        public void SpawnNpcsDeferred(float additionalDelaySeconds)
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }

            spawnCoroutine = StartCoroutine(SpawnNpcsCoroutine(Mathf.Max(0f, additionalDelaySeconds)));
        }

        /// <summary>
        /// Coroutine to spawn NPCs sequentially with delay
        /// </summary>
        private System.Collections.IEnumerator SpawnNpcsCoroutine(float additionalDelaySeconds)
        {
            // Wait for road graph to build
            yield return new WaitForSeconds(initialSpawnDelay + additionalDelaySeconds);

            if (roadGraphBuilder == null)
            {
                // Debug.LogError("[NpcSpawner] RoadGraphBuilder is not assigned!");
                spawnCoroutine = null;
                yield break;
            }

            if (roadGraphBuilder.RoadGraph == null || roadGraphBuilder.RoadGraph.roadSegments.Count == 0)
            {
                // Debug.LogError("[NpcSpawner] Road graph is empty! Make sure RoadGraphBuilder has built the graph.");
                spawnCoroutine = null;
                yield break;
            }

            if (npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0)
            {
                // Debug.LogError("[NpcSpawner] No NPC vehicle prefabs assigned!");
                spawnCoroutine = null;
                yield break;
            }

            // Cache WaitForSeconds to avoid per-iteration allocation
            cachedSpawnDelay = new WaitForSeconds(spawnDelay);

            // Clear existing NPCs without stopping this active spawn routine.
            ClearAllNpcs(stopSpawnRoutine: false);

            // Pre-pool if enabled
            if (enablePooling)
            {
                EnsurePoolCapacity(GetDesiredPoolSize());
            }

            // Track spawn positions to enforce spacing
            List<Vector3> spawnPositions = new List<Vector3>();

            // Track used segments to ensure distribution across different roads
            System.Collections.Generic.Dictionary<RoadSegment, int> segmentUsageCount =
                new System.Collections.Generic.Dictionary<RoadSegment, int>();

            int spawnedCount = 0;
            int attempts = 0;
            int maxAttempts = spawnCount * 30; // Prevent infinite loop

            int totalSegments = roadGraphBuilder.RoadGraph.roadSegments.Count;
            int maxPerSegment = Mathf.CeilToInt((float)spawnCount / Mathf.Max(1, totalSegments)) + 1;

            // Debug.Log($"[NpcSpawner] Distributing {spawnCount} NPCs across {totalSegments} road segments (max {maxPerSegment} per segment)");

            while (spawnedCount < spawnCount && attempts < maxAttempts)
            {
                attempts++;

                // Get random spawn position - try to distribute evenly across segments
                var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();

                if (segment == null || segment.waypoints.Count == 0)
                {
                    continue;
                }

                // Enforce max spawns per segment to distribute across all roads
                if (segmentUsageCount.ContainsKey(segment) && segmentUsageCount[segment] >= maxPerSegment)
                {
                    // This segment is full, try another one
                    if (attempts < maxAttempts - 10) // Only skip if we have attempts left
                    {
                        continue;
                    }
                }

                // Randomize waypoint index more to spread within segment
                waypointIndex = Random.Range(0, segment.waypoints.Count);

                if (segment == null || segment.waypoints.Count == 0)
                {
                    // Debug.LogWarning("[NpcSpawner] Failed to get random waypoint!");
                    continue;
                }

                Waypoint spawnWaypoint = segment.waypoints[waypointIndex];
                Vector3 spawnPos = spawnWaypoint.position;

                // Skip invalid positions (0,0,0 or near origin)
                if (spawnPos.sqrMagnitude < 1f)
                {
                    // Debug.LogWarning($"[NpcSpawner] Invalid spawn position {spawnPos} on segment '{segment.name}', skipping");
                    continue;
                }

                // Spawn NPC
                GameObject npcVehicle = GetOrCreateNpc();
                if (npcVehicle == null)
                {
                    // Debug.LogError("[NpcSpawner] Failed to instantiate NPC vehicle!");
                    continue;
                }

                // Use waypoint position directly (it's already on the road)
                // Add height offset based on wheel size to prevent clipping through road surface
                NpcCarAgent carAgent = npcVehicle.GetComponent<NpcCarAgent>();
                float heightOffset = 0.2f;
                if (carAgent != null)
                {
                    heightOffset = carAgent.GetGroundClearanceOffset();
                }

                Vector3 finalSpawnPos = GetGroundedSpawnPosition(spawnPos, heightOffset);

                if (IsSpawnVisibleToPlayer(finalSpawnPos))
                {
                    ReturnNpcToPool(npcVehicle);
                    continue;
                }

                // Check spacing with previously spawned vehicles in this batch
                if (!IsSpacingValid(finalSpawnPos, spawnPositions))
                {
                    ReturnNpcToPool(npcVehicle);
                    continue;
                }

                // Check spacing with all active vehicles (including already spawned ones)
                bool validSpacing = true;
                foreach (GameObject activeNpc in activeNpcs)
                {
                    if (activeNpc != null && !IsFarEnough(finalSpawnPos, GetVehicleReferencePosition(activeNpc), minimumSpawnSpacing))
                    {
                        validSpacing = false;
                        break;
                    }
                }

                if (!validSpacing)
                {
                    ReturnNpcToPool(npcVehicle);
                    continue;
                }

                // Safe rotation with zero vector check - flatten to horizontal
                Vector3 forward = ResolveSpawnForward(segment, waypointIndex, spawnWaypoint.forward);
                Quaternion spawnRotation = Quaternion.LookRotation(forward);

                // Check if spawn position is clear of physical obstacles using the real grounded pose.
                float clearanceRadius = GetSpawnClearanceRadius();
                if (!IsSpawnPositionClear(finalSpawnPos, clearanceRadius))
                {
                    if (showDebugInfo && attempts % 10 == 0)
                    {
                        Debug.LogWarning($"[NpcSpawner] Spawn pose blocked at {finalSpawnPos}, trying another location");
                    }

                    ReturnNpcToPool(npcVehicle);
                    continue;
                }

                npcVehicle.transform.position = finalSpawnPos;
                npcVehicle.transform.rotation = spawnRotation;
                if (!npcVehicle.activeSelf)
                {
                    npcVehicle.SetActive(true);
                }

                if (showDebugInfo)
                {
                    Debug.Log($"[NpcSpawner] Spawning {npcVehicle.name} at {finalSpawnPos} on segment '{segment.name}'");
                }

                // Initialize NPC components
                if (carAgent != null)
                {
                    ConfigureNpcExhaustSmoke(npcVehicle);
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);

                    // Give initial velocity to start moving
                    Rigidbody rb = npcVehicle.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float initialSpeed = Random.Range(8f, 12f); // 8-12 m/s = ~29-43 km/h (good starting speed)
                        rb.linearVelocity = forward * initialSpeed;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
                else
                {
                    // Debug.LogError($"[NpcSpawner] NPC vehicle '{npcVehicle.name}' missing NpcCarAgent component!");
                }

                NpcRecovery recovery = npcVehicle.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.ConfigureForRuntime(false);
                    recovery.Initialize(roadGraphBuilder);
                }

                // Add to tracking
                spawnPositions.Add(finalSpawnPos);
                activeNpcs.Add(npcVehicle);

                // Track segment usage for distribution
                if (!segmentUsageCount.ContainsKey(segment))
                {
                    segmentUsageCount[segment] = 0;
                }
                segmentUsageCount[segment]++;

                spawnedCount++;

                if (showDebugInfo && spawnedCount % 5 == 0)
                {
                    Debug.Log($"[NpcSpawner] Spawned {spawnedCount}/{spawnCount} NPCs");
                }

                // Wait before spawning next NPC (cached to avoid GC alloc)
                yield return cachedSpawnDelay;
            }

            // Log distribution summary
            string distributionSummary = $"[NpcSpawner] Spawned {spawnedCount} NPCs across {segmentUsageCount.Count} road segments (attempts: {attempts})\n";
            foreach (var kvp in segmentUsageCount)
            {
                distributionSummary += $"  - {kvp.Key.name}: {kvp.Value} NPCs\n";
            }
            // Debug.Log(distributionSummary);
            spawnCoroutine = null;
        }

        /// <summary>
        /// Get NPC from pool or create new one
        /// </summary>
        private GameObject GetOrCreateNpc()
        {
            GameObject npc = null;

            // Try to get from pool
            if (enablePooling && pooledNpcs.Count > 0)
            {
                npc = pooledNpcs[0];
                pooledNpcs.RemoveAt(0);
                pooledNpcSet.Remove(npc);
                if (npc.activeSelf)
                {
                    npc.SetActive(false);
                }
            }
            else
            {
                // Create new instance
                GameObject prefab = npcVehiclePrefabs[Random.Range(0, npcVehiclePrefabs.Length)];
                npc = Instantiate(prefab, npcContainer);
                npc.name = $"NPC_{prefab.name}_{activeNpcs.Count}";
                npc.SetActive(false);
            }

            ResetPhysicsState(npc);
            return npc;
        }

        private void ReturnNpcToPool(GameObject npc)
        {
            if (npc == null) return;

            if (enablePooling)
            {
                npc.SetActive(false);
                if (!pooledNpcSet.Contains(npc))
                {
                    pooledNpcs.Add(npc);
                    pooledNpcSet.Add(npc);
                }
            }
            else
            {
                Destroy(npc);
            }
        }

        /// <summary>
        /// Pre-pool vehicles for better performance
        /// </summary>
        private void PrePoolVehicles(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = npcVehiclePrefabs[Random.Range(0, npcVehiclePrefabs.Length)];
                GameObject npc = Instantiate(prefab, npcContainer);
                npc.name = $"NPC_{prefab.name}_Pooled_{i}";
                npc.SetActive(false);
                pooledNpcs.Add(npc);
                pooledNpcSet.Add(npc);
            }

            // Debug.Log($"[NpcSpawner] Pre-pooled {count} vehicles");
        }

        private System.Collections.IEnumerator WarmPoolGradually(int desiredCount)
        {
            if (poolWarmupDelay > 0f)
            {
                yield return new WaitForSeconds(poolWarmupDelay);
            }

            int perFrame = Mathf.Max(1, poolWarmupPerFrame);
            while (enablePooling && activeNpcs.Count + pooledNpcs.Count < desiredCount)
            {
                int missing = desiredCount - (activeNpcs.Count + pooledNpcs.Count);
                PrePoolVehicles(Mathf.Min(perFrame, missing));
                yield return null;
            }

            poolWarmupCoroutine = null;
        }

        private void EnsurePoolCapacity(int desiredCount)
        {
            int totalCount = activeNpcs.Count + pooledNpcs.Count;
            int missingCount = desiredCount - totalCount;
            if (missingCount > 0)
            {
                PrePoolVehicles(missingCount);
            }
        }

        /// <summary>
        /// Clear all spawned NPCs
        /// </summary>
        [ContextMenu("Clear All NPCs")]
        public void ClearAllNpcs()
        {
            ClearAllNpcs(stopSpawnRoutine: true);
        }

        private void ClearAllNpcs(bool stopSpawnRoutine)
        {
            if (stopSpawnRoutine && spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            if (enablePooling)
            {
                // Return to pool
                foreach (GameObject npc in activeNpcs)
                {
                    if (npc != null)
                    {
                        npc.SetActive(false);
                        if (!pooledNpcSet.Contains(npc))
                        {
                            pooledNpcs.Add(npc);
                            pooledNpcSet.Add(npc);
                        }
                    }
                }
            }
            else
            {
                // Destroy
                foreach (GameObject npc in activeNpcs)
                {
                    if (npc != null)
                    {
                        Destroy(npc);
                    }
                }
            }

            activeNpcs.Clear();
            // Debug.Log("[NpcSpawner] Cleared all NPCs");
        }

        /// <summary>
        /// Spawn additional NPC at random location
        /// </summary>
        public GameObject SpawnSingleNpc()
        {
            int maxAttempts = 30;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                attempts++;

                var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
                if (segment == null) continue;

                Waypoint wp = segment.waypoints[waypointIndex];
                Vector3 spawnPos = wp.position;

                // Check if spawn position is clear
                GameObject npc = GetOrCreateNpc();
                if (npc == null)
                {
                    continue;
                }

                // Use waypoint position directly (it's already on the road)
                float heightOffset = 0.2f;
                NpcCarAgent carAgent = npc.GetComponent<NpcCarAgent>();
                if (carAgent != null)
                    heightOffset = carAgent.GetGroundClearanceOffset();
                Vector3 finalSpawnPos = GetGroundedSpawnPosition(wp.position, heightOffset);

                if (IsSpawnVisibleToPlayer(finalSpawnPos))
                {
                    ReturnNpcToPool(npc);
                    continue;
                }

                // Check spacing with all active vehicles using final grounded position.
                bool validSpacing = true;
                foreach (GameObject activeNpc in activeNpcs)
                {
                    if (activeNpc != null && !IsFarEnough(finalSpawnPos, GetVehicleReferencePosition(activeNpc), minimumSpawnSpacing))
                    {
                        validSpacing = false;
                        break;
                    }
                }

                if (!validSpacing)
                {
                    ReturnNpcToPool(npc);
                    continue;
                }

                // Safe rotation - flatten to horizontal
                Vector3 forward = ResolveSpawnForward(segment, waypointIndex, wp.forward);
                Quaternion spawnRotation = Quaternion.LookRotation(forward);

                float clearanceRadius = GetSpawnClearanceRadius();
                if (!IsSpawnPositionClear(finalSpawnPos, clearanceRadius))
                {
                    ReturnNpcToPool(npc);
                    continue;
                }

                npc.transform.position = finalSpawnPos;
                npc.transform.rotation = spawnRotation;
                if (!npc.activeSelf)
                {
                    npc.SetActive(true);
                }

                if (carAgent != null)
                {
                    ConfigureNpcExhaustSmoke(npc);
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);

                    // Give initial velocity to start moving
                    Rigidbody rb = npc.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float initialSpeed = Random.Range(8f, 12f); // 8-12 m/s = ~29-43 km/h (good starting speed)
                        rb.linearVelocity = forward * initialSpeed;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                NpcRecovery recovery = npc.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.ConfigureForRuntime(false);
                    recovery.Initialize(roadGraphBuilder);
                }

                activeNpcs.Add(npc);

                if (showDebugInfo)
                {
                    Debug.Log($"[NpcSpawner] Spawned single NPC at {npc.transform.position} (attempts: {attempts})");
                }

                return npc;
            }

            // Debug.LogWarning($"[NpcSpawner] Failed to find valid spawn position after {maxAttempts} attempts");
            return null;
        }

        private void MaintainTrafficRing(Vector3 playerPosition)
        {
            int targetCount = Mathf.Max(0, targetActiveNpcCount);
            if (activeNpcs.Count >= targetCount)
            {
                return;
            }

            EnsurePoolCapacity(GetDesiredPoolSize());

            int successfulSpawns = 0;
            int attemptBudget = Mathf.Max(maxSpawnAttemptsPerTick, maxSpawnsPerTick);

            while (activeNpcs.Count < targetCount &&
                   successfulSpawns < Mathf.Max(1, maxSpawnsPerTick) &&
                   attemptBudget > 0)
            {
                attemptBudget--;

                if (TrySpawnNpcNearPlayer(playerPosition))
                {
                    successfulSpawns++;
                }
            }
        }

        private void RecycleFarTraffic(Vector3 playerPosition)
        {
            float despawnDistance = Mathf.Max(playerSpawnMaxDistance + 10f, playerDespawnDistance);
            float despawnDistanceSqr = despawnDistance * despawnDistance;

            for (int i = activeNpcs.Count - 1; i >= 0; i--)
            {
                GameObject npc = activeNpcs[i];
                if (npc == null)
                {
                    activeNpcs.RemoveAt(i);
                    continue;
                }

                Vector3 npcPosition = GetVehicleReferencePosition(npc);
                if (GetPlanarDistanceSqr(npcPosition, playerPosition) <= despawnDistanceSqr)
                {
                    continue;
                }

                DespawnNpc(npc);
            }
        }

        private bool TrySpawnNpcNearPlayer(Vector3 playerPosition)
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
            {
                return false;
            }

            float minDistance = Mathf.Max(0f, playerSpawnMinDistance);
            float maxDistance = Mathf.Max(minDistance + 5f, playerSpawnMaxDistance);
            int attempts = Mathf.Max(1, maxSpawnAttemptsPerTick);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
                if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
                {
                    continue;
                }

                Waypoint wp = segment.waypoints[waypointIndex];
                Vector3 spawnPos = wp.position;
                float playerDistance = GetPlanarDistance(spawnPos, playerPosition);
                if (playerDistance < minDistance || playerDistance > maxDistance)
                {
                    continue;
                }

                if (IsSpawnVisibleToPlayer(spawnPos))
                {
                    continue;
                }

                GameObject npc = GetOrCreateNpc();
                if (npc == null)
                {
                    continue;
                }

                NpcCarAgent carAgent = npc.GetComponent<NpcCarAgent>();
                float heightOffset = carAgent != null ? carAgent.GetGroundClearanceOffset() : 0.2f;
                Vector3 finalSpawnPos = GetGroundedSpawnPosition(spawnPos, heightOffset);

                if (!IsSpawnCandidateValid(finalSpawnPos, playerPosition))
                {
                    ReturnNpcToPool(npc);
                    continue;
                }

                Vector3 forward = ResolveSpawnForward(segment, waypointIndex, wp.forward);
                Quaternion spawnRotation = Quaternion.LookRotation(forward);
                float clearanceRadius = GetSpawnClearanceRadius();
                if (!IsSpawnPositionClear(finalSpawnPos, clearanceRadius))
                {
                    ReturnNpcToPool(npc);
                    continue;
                }

                npc.transform.position = finalSpawnPos;
                npc.transform.rotation = spawnRotation;
                if (!npc.activeSelf)
                {
                    npc.SetActive(true);
                }

                if (carAgent != null)
                {
                    ConfigureNpcExhaustSmoke(npc);
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);

                    Rigidbody rb = npc.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float initialSpeed = Random.Range(8f, 12f);
                        rb.linearVelocity = forward * initialSpeed;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                NpcRecovery recovery = npc.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.ConfigureForRuntime(false);
                    recovery.Initialize(roadGraphBuilder);
                }

                activeNpcs.Add(npc);

                if (showDebugInfo)
                {
                    Debug.Log($"[NpcSpawner] Dynamic spawn at {finalSpawnPos} ({activeNpcs.Count}/{targetActiveNpcCount})");
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Despawn specific NPC
        /// </summary>
        public void DespawnNpc(GameObject npc)
        {
            if (npc == null) return;

            activeNpcs.Remove(npc);
            ReturnNpcToPool(npc);
        }

        /// <summary>
        /// Get list of active NPCs
        /// </summary>
        public List<GameObject> GetActiveNpcs() => new List<GameObject>(activeNpcs);

        /// <summary>
        /// Get spawn statistics
        /// </summary>
        public (int active, int pooled) GetStats()
        {
            return (activeNpcs.Count, pooledNpcs.Count);
        }

        /// <summary>
        /// Check if spawn position is clear of obstacles using sphere overlap
        /// </summary>
        private bool IsSpawnPositionClear(Vector3 position)
        {
            // NonAlloc: reuse static buffer to avoid GC allocation
            int count = Physics.OverlapSphereNonAlloc(position, spawnCheckRadius, spawnOverlapBuffer, spawnObstacleCheckMask, QueryTriggerInteraction.Ignore);

            if (count == 0)
                return true;

            // Filter out ground/road colliders - only care about vehicles and obstacles
            for (int i = 0; i < count; i++)
            {
                Collider col = spawnOverlapBuffer[i];
                if (col == null) continue;
                if (IsNonBlockingSpawnCollider(col)) continue;

                // Check if it's a vehicle (NPC or player)
                if (HasVehicleController(col))
                {
                    return false;
                }

                // Check if it's a rigidbody vehicle (dynamic obstacles)
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // Exclude ground/terrain rigidbodies
                    if (col.gameObject.layer != LayerMask.NameToLayer("Road") &&
                        col.gameObject.layer != LayerMask.NameToLayer("Ground"))
                    {
                        return false;
                    }
                }
                else
                {
                    if (IsStaticColliderTooCloseToSpawn(col, position))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsSpawnPositionClear(Vector3 position, float spacingRadius)
        {
            if (!IsSpawnPositionClear(position))
            {
                return false;
            }

            float checkRadius = Mathf.Max(spawnCheckRadius, spacingRadius);
            int count = Physics.OverlapSphereNonAlloc(position, checkRadius, spawnOverlapBuffer, spawnObstacleCheckMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = spawnOverlapBuffer[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (IsNonBlockingSpawnCollider(col)) continue;

                if (HasVehicleController(col))
                {
                    return false;
                }

                if (IsStaticColliderTooCloseToSpawn(col, position))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSpawnVisibleToPlayer(Vector3 worldPosition)
        {
            if (!avoidSpawningInCameraView)
            {
                return false;
            }

            Camera camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled)
            {
                return false;
            }

            Vector3 cameraPosition = camera.transform.position;
            if (Vector3.Distance(cameraPosition, worldPosition) > Mathf.Max(1f, cameraVisibleSpawnRejectDistance))
            {
                return false;
            }

            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPosition + Vector3.up * 1.2f);
            if (viewportPoint.z <= 0f)
            {
                return false;
            }

            float margin = Mathf.Clamp01(cameraSpawnViewportMargin);
            bool inView =
                viewportPoint.x >= -margin &&
                viewportPoint.x <= 1f + margin &&
                viewportPoint.y >= -margin &&
                viewportPoint.y <= 1f + margin;

            if (!inView)
            {
                return false;
            }

            Vector3 direction = (worldPosition + Vector3.up * 1.2f) - cameraPosition;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
            {
                return true;
            }

            if (Physics.Raycast(cameraPosition, direction / distance, out RaycastHit hit, distance, cameraVisibilityMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsNonBlockingVisibilityCollider(hit.collider))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNonBlockingSpawnCollider(Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            if (collider.isTrigger || collider.GetComponent<Terrain>() != null)
            {
                return true;
            }

            int layer = collider.gameObject.layer;
            int roadLayer = LayerMask.NameToLayer("Road");
            int groundLayer = LayerMask.NameToLayer("Ground");
            if ((roadLayer >= 0 && layer == roadLayer) || (groundLayer >= 0 && layer == groundLayer))
            {
                return true;
            }

            string lowerName = collider.name.ToLowerInvariant();
            return lowerName.Contains("road") ||
                   lowerName.Contains("street") ||
                   lowerName.Contains("asphalt") ||
                   lowerName.Contains("sidewalk") ||
                   lowerName.Contains("terrain") ||
                   lowerName.Contains("ground");
        }

        private static bool IsNonBlockingVisibilityCollider(Collider collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return true;
            }

            return HasVehicleController(collider) || IsNonBlockingSpawnCollider(collider);
        }

        private bool IsStaticColliderTooCloseToSpawn(Collider collider, Vector3 spawnPosition)
        {
            if (collider == null)
            {
                return false;
            }

            Vector3 closestPoint = collider.ClosestPoint(spawnPosition);
            float dx = closestPoint.x - spawnPosition.x;
            float dz = closestPoint.z - spawnPosition.z;
            float planarDistanceSqr = (dx * dx) + (dz * dz);
            float radius = Mathf.Max(0.25f, staticSpawnObstacleRadius);
            return planarDistanceSqr <= radius * radius;
        }

        private static bool HasVehicleController(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.TryGetComponent<NpcCarAgent>(out _) || collider.TryGetComponent<CarController>(out _))
            {
                return true;
            }

            Rigidbody attachedRigidbody = collider.attachedRigidbody;
            if (attachedRigidbody != null &&
                (attachedRigidbody.TryGetComponent<NpcCarAgent>(out _) || attachedRigidbody.TryGetComponent<CarController>(out _)))
            {
                return true;
            }

            Transform root = collider.transform.root;
            if (root != null && root != collider.transform &&
                (root.TryGetComponent<NpcCarAgent>(out _) || root.TryGetComponent<CarController>(out _)))
            {
                return true;
            }

            return false;
        }

        private float GetSpawnClearanceRadius()
        {
            // Keep extra spawn reservation realistic to road width.
            // Large spacing values are still enforced separately via minimumSpawnSpacing.
            float scaledRadius = minimumSpawnSpacing * spawnClearanceMultiplier * 0.4f;
            return Mathf.Clamp(scaledRadius, spawnCheckRadius, 9f);
        }

        private bool IsSpawnCandidateValid(Vector3 candidatePosition, Vector3 playerPosition)
        {
            if (IsSpawnVisibleToPlayer(candidatePosition))
            {
                return false;
            }

            if (GetPlanarDistance(candidatePosition, playerPosition) < Mathf.Max(0f, playerSpawnMinDistance))
            {
                return false;
            }

            foreach (GameObject activeNpc in activeNpcs)
            {
                if (activeNpc == null)
                {
                    continue;
                }

                if (!IsFarEnough(candidatePosition, GetVehicleReferencePosition(activeNpc), minimumSpawnSpacing))
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 GetVehicleReferencePosition(GameObject npc)
        {
            if (npc == null)
            {
                return Vector3.zero;
            }

            NpcCarAgent agent = npc.GetComponent<NpcCarAgent>();
            if (agent != null)
            {
                return agent.GetReferencePosition();
            }

            return npc.transform.position;
        }

        private Vector3 GetGroundedSpawnPosition(Vector3 basePosition, float heightOffset)
        {
            Vector3 origin = basePosition + Vector3.up * spawnRaycastHeight;
            float maxDistance = spawnRaycastHeight + spawnRaycastDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, spawnGroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * heightOffset;
            }

            return basePosition + Vector3.up * heightOffset;
        }

        private Vector3 ResolveSpawnForward(RoadSegment segment, int waypointIndex, Vector3 fallbackForward)
        {
            if (segment != null && segment.waypoints != null && segment.waypoints.Count >= 2)
            {
                int nextIndex = Mathf.Min(segment.waypoints.Count - 1, waypointIndex + 1);
                int prevIndex = Mathf.Max(0, waypointIndex - 1);

                Vector3 tangent = (nextIndex != waypointIndex)
                    ? segment.waypoints[nextIndex].position - segment.waypoints[waypointIndex].position
                    : segment.waypoints[waypointIndex].position - segment.waypoints[prevIndex].position;

                tangent.y = 0f;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    return tangent.normalized;
                }
            }

            fallbackForward.y = 0f;
            if (fallbackForward.sqrMagnitude > 0.0001f)
            {
                return fallbackForward.normalized;
            }

            return Vector3.forward;
        }

        private bool IsSpacingValid(Vector3 candidatePosition, List<Vector3> existingPositions)
        {
            for (int i = 0; i < existingPositions.Count; i++)
            {
                if (!IsFarEnough(candidatePosition, existingPositions[i], minimumSpawnSpacing))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsFarEnough(Vector3 a, Vector3 b, float minDistance)
        {
            if (!usePlanarSpawnSpacing)
            {
                return Vector3.Distance(a, b) >= minDistance;
            }

            Vector2 axz = new Vector2(a.x, a.z);
            Vector2 bxz = new Vector2(b.x, b.z);
            return Vector2.Distance(axz, bxz) >= minDistance;
        }

        private void ResetPhysicsState(GameObject npc)
        {
            if (npc == null)
            {
                return;
            }

            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private static void ConfigureNpcExhaustSmoke(GameObject npc)
        {
            if (npc == null)
            {
                return;
            }

            VehicleExhaustSmoke exhaustSmoke = npc.GetComponent<VehicleExhaustSmoke>();
            if (exhaustSmoke == null)
            {
                exhaustSmoke = npc.AddComponent<VehicleExhaustSmoke>();
            }

            string normalizedName = npc.name.ToLowerInvariant();
            bool isHeavyVehicle =
                normalizedName.Contains("lorry") ||
                normalizedName.Contains("truck") ||
                normalizedName.Contains("cargo");

            exhaustSmoke.ConfigurePreset(isHeavyVehicle
                ? VehicleExhaustSmokePreset.NpcTruckLight
                : VehicleExhaustSmokePreset.NpcLight);
        }

        private int GetDesiredPoolSize()
        {
            int desiredActiveCount = keepTrafficAroundPlayer
                ? Mathf.Max(0, targetActiveNpcCount)
                : Mathf.Max(0, spawnCount);

            return desiredActiveCount + Mathf.Max(0, poolExtraCount);
        }

        private bool TryResolvePlayerTransform(out Transform resolvedPlayer)
        {
            resolvedPlayer = playerTransform;
            if (resolvedPlayer != null && resolvedPlayer.gameObject.activeInHierarchy)
            {
                SyncOptimizerPlayer(resolvedPlayer);
                return true;
            }

            if (!autoFindPlayer && playerTransform == null)
            {
                return false;
            }

            if (Time.time < nextPlayerResolveTime)
            {
                return false;
            }

            nextPlayerResolveTime = Time.time + 1f;

            if (PlayerVehicleManager.Instance != null && PlayerVehicleManager.Instance.ActiveVehicleController != null)
            {
                playerTransform = PlayerVehicleManager.Instance.ActiveVehicleController.transform;
            }
            else if (TrafficSimulationOptimizer.Instance != null && TrafficSimulationOptimizer.Instance.playerTransform != null)
            {
                playerTransform = TrafficSimulationOptimizer.Instance.playerTransform;
            }
            else
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                {
                    playerTransform = taggedPlayer.transform;
                }
                else
                {
                    CarController carController = FindFirstObjectByType<CarController>();
                    if (carController != null)
                    {
                        playerTransform = carController.transform;
                    }
                }
            }

            resolvedPlayer = playerTransform;
            if (resolvedPlayer != null)
            {
                SyncOptimizerPlayer(resolvedPlayer);
                return true;
            }

            return false;
        }

        private static void SyncOptimizerPlayer(Transform resolvedPlayer)
        {
            if (resolvedPlayer == null || TrafficSimulationOptimizer.Instance == null)
            {
                return;
            }

            if (TrafficSimulationOptimizer.Instance.playerTransform != resolvedPlayer)
            {
                TrafficSimulationOptimizer.Instance.playerTransform = resolvedPlayer;
            }
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(GetPlanarDistanceSqr(a, b));
        }

        private static float GetPlanarDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }
    }
}
