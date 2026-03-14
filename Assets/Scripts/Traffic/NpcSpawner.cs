using System.Collections.Generic;
using UnityEngine;

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

        [Header("Pooling")]
        [Tooltip("Enable object pooling (disable/enable instead of destroy/instantiate)")]
        [SerializeField] private bool enablePooling = true;
        [Tooltip("Pre-pool extra vehicles")]
        [SerializeField] private int poolExtraCount = 5;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Runtime data
        private List<GameObject> activeNpcs = new List<GameObject>();
        private List<GameObject> pooledNpcs = new List<GameObject>();
        private HashSet<GameObject> pooledNpcSet = new HashSet<GameObject>();
        private Transform npcContainer;
        private Coroutine spawnCoroutine;
        public bool HasPendingOrActiveSpawn => spawnCoroutine != null || activeNpcs.Count > 0;

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

            if (spawnOnStart)
            {
                SpawnNpcsDeferred(0f);
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
                PrePoolVehicles(spawnCount + poolExtraCount);
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
                        pooledNpcs.Add(npc);
                        pooledNpcSet.Add(npc);
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

        /// <summary>
        /// Despawn specific NPC
        /// </summary>
        public void DespawnNpc(GameObject npc)
        {
            if (npc == null) return;

            activeNpcs.Remove(npc);

            if (enablePooling)
            {
                npc.SetActive(false);
                pooledNpcs.Add(npc);
                pooledNpcSet.Add(npc);
            }
            else
            {
                Destroy(npc);
            }
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

                if (HasVehicleController(col))
                {
                    return false;
                }
            }

            return true;
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
    }
}
