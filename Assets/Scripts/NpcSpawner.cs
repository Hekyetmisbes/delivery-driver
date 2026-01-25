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
        [SerializeField] private float minimumSpawnSpacing = 20f;
        [Tooltip("Spawn vehicles on Start()")]
        [SerializeField] private bool spawnOnStart = true;

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
                SpawnNpcs();
            }
        }

        /// <summary>
        /// Spawn NPCs on the road network
        /// </summary>
        [ContextMenu("Spawn NPCs")]
        public void SpawnNpcs()
        {
            if (roadGraphBuilder == null)
            {
                Debug.LogError("[NpcSpawner] RoadGraphBuilder is not assigned!");
                return;
            }

            if (roadGraphBuilder.RoadGraph == null || roadGraphBuilder.RoadGraph.roadSegments.Count == 0)
            {
                Debug.LogError("[NpcSpawner] Road graph is empty! Make sure RoadGraphBuilder has built the graph.");
                return;
            }

            if (npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0)
            {
                Debug.LogError("[NpcSpawner] No NPC vehicle prefabs assigned!");
                return;
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

            int spawnedCount = 0;
            int attempts = 0;
            int maxAttempts = spawnCount * 10; // Prevent infinite loop

            while (spawnedCount < spawnCount && attempts < maxAttempts)
            {
                attempts++;

                // Get random spawn position
                var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();

                if (segment == null || segment.waypoints.Count == 0)
                {
                    Debug.LogWarning("[NpcSpawner] Failed to get random waypoint!");
                    continue;
                }

                Waypoint spawnWaypoint = segment.waypoints[waypointIndex];
                Vector3 spawnPos = spawnWaypoint.position;

                // Check spacing with existing spawns
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

                // Spawn NPC
                GameObject npcVehicle = GetOrCreateNpc();
                if (npcVehicle == null)
                {
                    Debug.LogError("[NpcSpawner] Failed to instantiate NPC vehicle!");
                    continue;
                }

                // Position and orient vehicle
                npcVehicle.transform.position = spawnPos + Vector3.up * 0.5f;

                // Safe rotation with zero vector check
                Vector3 forward = spawnWaypoint.forward;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                npcVehicle.transform.rotation = Quaternion.LookRotation(forward);

                // Initialize NPC components
                NpcCarAgent carAgent = npcVehicle.GetComponent<NpcCarAgent>();
                if (carAgent != null)
                {
                    carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);
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
                spawnedCount++;
            }

            Debug.Log($"[NpcSpawner] Spawned {spawnedCount} NPCs (attempts: {attempts})");
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
            var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
            if (segment == null) return null;

            GameObject npc = GetOrCreateNpc();
            Waypoint wp = segment.waypoints[waypointIndex];

            npc.transform.position = wp.position + Vector3.up * 0.5f;

            // Safe rotation
            Vector3 forward = wp.forward;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            npc.transform.rotation = Quaternion.LookRotation(forward);

            NpcCarAgent carAgent = npc.GetComponent<NpcCarAgent>();
            if (carAgent != null)
            {
                carAgent.Initialize(roadGraphBuilder, segment, waypointIndex);
            }

            NpcRecovery recovery = npc.GetComponent<NpcRecovery>();
            if (recovery != null)
            {
                recovery.Initialize(roadGraphBuilder);
            }

            activeNpcs.Add(npc);
            return npc;
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
    }
}
