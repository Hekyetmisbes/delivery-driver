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
        [SerializeField] private float minimumSpawnSpacing = 8f;
        [Tooltip("Check radius for existing vehicles/obstacles at spawn point (meters)")]
        [SerializeField] private float spawnCheckRadius = 3f;
        [Tooltip("Layer mask for checking obstacles at spawn point")]
        [SerializeField] private LayerMask spawnObstacleCheckMask = ~0;
        [Tooltip("Spawn vehicles on Start()")]
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Initial delay before starting spawn (seconds, to let road graph build)")]
        [SerializeField] private float initialSpawnDelay = 0.5f;
        [Tooltip("Delay between each NPC spawn (seconds)")]
        [SerializeField] private float spawnDelay = 0.3f;
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
        [SerializeField] private bool showDebugInfo = true;

        // Runtime data
        private List<GameObject> activeNpcs = new List<GameObject>();
        private List<GameObject> pooledNpcs = new List<GameObject>();
        private Transform npcContainer;

        private void Start()
        {
            // Create container for organization
            npcContainer = new GameObject("NPC Vehicles").transform;
            npcContainer.SetParent(transform);

            if (spawnOnStart)
            {
                StartCoroutine(SpawnNpcsCoroutine());
            }
        }

        /// <summary>
        /// Spawn NPCs on the road network
        /// </summary>
        [ContextMenu("Spawn NPCs")]
        public void SpawnNpcs()
        {
            StartCoroutine(SpawnNpcsCoroutine());
        }

        /// <summary>
        /// Coroutine to spawn NPCs sequentially with delay
        /// </summary>
        private System.Collections.IEnumerator SpawnNpcsCoroutine()
        {
            // Wait for road graph to build
            yield return new WaitForSeconds(initialSpawnDelay);

            if (roadGraphBuilder == null)
            {
                Debug.LogError("[NpcSpawner] RoadGraphBuilder is not assigned!");
                yield break;
            }

            if (roadGraphBuilder.RoadGraph == null || roadGraphBuilder.RoadGraph.roadSegments.Count == 0)
            {
                Debug.LogError("[NpcSpawner] Road graph is empty! Make sure RoadGraphBuilder has built the graph.");
                yield break;
            }

            if (npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0)
            {
                Debug.LogError("[NpcSpawner] No NPC vehicle prefabs assigned!");
                yield break;
            }

            // Clear existing NPCs
            ClearAllNpcs();

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

            Debug.Log($"[NpcSpawner] Distributing {spawnCount} NPCs across {totalSegments} road segments (max {maxPerSegment} per segment)");

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
                    Debug.LogWarning("[NpcSpawner] Failed to get random waypoint!");
                    continue;
                }

                Waypoint spawnWaypoint = segment.waypoints[waypointIndex];
                Vector3 spawnPos = spawnWaypoint.position;

                // Skip invalid positions (0,0,0 or near origin)
                if (spawnPos.sqrMagnitude < 1f)
                {
                    Debug.LogWarning($"[NpcSpawner] Invalid spawn position {spawnPos} on segment '{segment.name}', skipping");
                    continue;
                }

                // Check spacing with previously spawned vehicles in this batch
                bool validSpacing = true;
                foreach (Vector3 existingPos in spawnPositions)
                {
                    if (Vector3.Distance(spawnPos, existingPos) < minimumSpawnSpacing)
                    {
                        validSpacing = false;
                        break;
                    }
                }

                if (!validSpacing)
                    continue;

                // Check spacing with all active vehicles (including already spawned ones)
                foreach (GameObject activeNpc in activeNpcs)
                {
                    if (activeNpc != null && Vector3.Distance(spawnPos, activeNpc.transform.position) < minimumSpawnSpacing)
                    {
                        validSpacing = false;
                        break;
                    }
                }

                if (!validSpacing)
                    continue;

                // Check if spawn position is clear of physical obstacles
                if (!IsSpawnPositionClear(spawnPos))
                {
                    if (showDebugInfo && attempts % 10 == 0)
                    {
                        Debug.LogWarning($"[NpcSpawner] Spawn position {spawnPos} blocked by obstacles, trying another location");
                    }
                    continue;
                }

                // Spawn NPC
                GameObject npcVehicle = GetOrCreateNpc();
                if (npcVehicle == null)
                {
                    Debug.LogError("[NpcSpawner] Failed to instantiate NPC vehicle!");
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
                npcVehicle.transform.position = finalSpawnPos;

                if (showDebugInfo)
                {
                    Debug.Log($"[NpcSpawner] Spawning {npcVehicle.name} at {finalSpawnPos} on segment '{segment.name}'");
                }

                // Safe rotation with zero vector check - flatten to horizontal
                Vector3 forward = spawnWaypoint.forward;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                forward.y = 0; // Keep car level on road
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                else
                {
                    forward.Normalize();
                }
                npcVehicle.transform.rotation = Quaternion.LookRotation(forward);

                // Initialize NPC components
                if (carAgent != null)
                {
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);

                    // Give random initial velocity (20-40 km/h range)
                    Rigidbody rb = npcVehicle.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float initialSpeed = Random.Range(5f, 11f); // 5-11 m/s = ~18-40 km/h
                        rb.linearVelocity = forward * initialSpeed;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
                else
                {
                    Debug.LogError($"[NpcSpawner] NPC vehicle '{npcVehicle.name}' missing NpcCarAgent component!");
                }

                NpcRecovery recovery = npcVehicle.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.Initialize(roadGraphBuilder);
                }

                // Add to tracking
                spawnPositions.Add(spawnPos);
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

                // Wait before spawning next NPC
                yield return new WaitForSeconds(spawnDelay);
            }

            // Log distribution summary
            string distributionSummary = $"[NpcSpawner] Spawned {spawnedCount} NPCs across {segmentUsageCount.Count} road segments (attempts: {attempts})\n";
            foreach (var kvp in segmentUsageCount)
            {
                distributionSummary += $"  - {kvp.Key.name}: {kvp.Value} NPCs\n";
            }
            Debug.Log(distributionSummary);
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
                npc.SetActive(true);
            }
            else
            {
                // Create new instance
                GameObject prefab = npcVehiclePrefabs[Random.Range(0, npcVehiclePrefabs.Length)];
                npc = Instantiate(prefab, npcContainer);
                npc.name = $"NPC_{prefab.name}_{activeNpcs.Count}";
            }

            return npc;
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
            }

            Debug.Log($"[NpcSpawner] Pre-pooled {count} vehicles");
        }

        /// <summary>
        /// Clear all spawned NPCs
        /// </summary>
        [ContextMenu("Clear All NPCs")]
        public void ClearAllNpcs()
        {
            if (enablePooling)
            {
                // Return to pool
                foreach (GameObject npc in activeNpcs)
                {
                    if (npc != null)
                    {
                        npc.SetActive(false);
                        pooledNpcs.Add(npc);
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
            Debug.Log("[NpcSpawner] Cleared all NPCs");
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

                // Check spacing with all active vehicles
                bool validSpacing = true;
                foreach (GameObject activeNpc in activeNpcs)
                {
                    if (activeNpc != null && Vector3.Distance(spawnPos, activeNpc.transform.position) < minimumSpawnSpacing)
                    {
                        validSpacing = false;
                        break;
                    }
                }

                if (!validSpacing)
                    continue;

                // Check if spawn position is clear
                if (!IsSpawnPositionClear(spawnPos))
                    continue;

                // Valid spawn position found
                GameObject npc = GetOrCreateNpc();

                // Use waypoint position directly (it's already on the road)
                float heightOffset = 0.2f;
                NpcCarAgent carAgent = npc.GetComponent<NpcCarAgent>();
                if (carAgent != null)
                    heightOffset = carAgent.GetGroundClearanceOffset();
                npc.transform.position = GetGroundedSpawnPosition(wp.position, heightOffset);

                // Safe rotation - flatten to horizontal
                Vector3 forward = wp.forward;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                forward.y = 0; // Keep car level
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                else
                {
                    forward.Normalize();
                }
                npc.transform.rotation = Quaternion.LookRotation(forward);

                if (carAgent != null)
                {
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);

                    // Give random initial velocity (20-40 km/h range)
                    Rigidbody rb = npc.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        float initialSpeed = Random.Range(5f, 11f); // 5-11 m/s = ~18-40 km/h
                        rb.linearVelocity = forward * initialSpeed;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                NpcRecovery recovery = npc.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.Initialize(roadGraphBuilder);
                }

                activeNpcs.Add(npc);

                if (showDebugInfo)
                {
                    Debug.Log($"[NpcSpawner] Spawned single NPC at {npc.transform.position} (attempts: {attempts})");
                }

                return npc;
            }

            Debug.LogWarning($"[NpcSpawner] Failed to find valid spawn position after {maxAttempts} attempts");
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

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 150, 300, 150));
            GUI.color = Color.cyan;
            GUILayout.Label("<b>NPC SPAWNER</b>");
            GUILayout.Label($"Active NPCs: {activeNpcs.Count}");
            GUILayout.Label($"Pooled NPCs: {pooledNpcs.Count}");
            GUILayout.Label($"Total Roads: {(roadGraphBuilder?.RoadGraph?.roadSegments.Count ?? 0)}");

            if (GUILayout.Button("Spawn NPCs"))
            {
                SpawnNpcs();
            }
            if (GUILayout.Button("Clear NPCs"))
            {
                ClearAllNpcs();
            }
            GUILayout.EndArea();
        }

        /// <summary>
        /// Check if spawn position is clear of obstacles using sphere overlap
        /// </summary>
        private bool IsSpawnPositionClear(Vector3 position)
        {
            // Check for colliders in the spawn radius
            Collider[] colliders = Physics.OverlapSphere(position, spawnCheckRadius, spawnObstacleCheckMask, QueryTriggerInteraction.Ignore);

            if (colliders == null || colliders.Length == 0)
                return true;

            // Filter out ground/road colliders - only care about vehicles and obstacles
            foreach (Collider col in colliders)
            {
                if (col == null) continue;

                // Check if it's a vehicle (NPC or player)
                if (col.GetComponent<NpcCarAgent>() != null ||
                    col.GetComponentInParent<NpcCarAgent>() != null ||
                    col.GetComponent<CarController>() != null ||
                    col.GetComponentInParent<CarController>() != null)
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
    }
}
