using UnityEngine;
using System.Collections.Generic;
using TrafficSystem;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// Advanced traffic and NPC simulation optimization
    /// Implements tick decimation, spatial partitioning, and behavior throttling
    /// Sprint 3: Enhanced simulation throttling
    /// </summary>
    public class TrafficSimulationOptimizer : MonoBehaviour
    {
        [Header("Update Throttling")]
        [Tooltip("Near distance: update every frame")]
        public float nearDistance = 50f;

        [Tooltip("Mid distance: update every 2-4 frames")]
        public float midDistance = 150f;

        [Tooltip("Far distance: update every 8-10 frames")]
        public float farDistance = 300f;

        [Tooltip("Very far distance: event-driven only")]
        public float veryFarDistance = 500f;

        [Header("Simulation Quality")]
        [Tooltip("Disable AI behavior for very far NPCs")]
        public bool disableVeryFarAI = true;

        [Tooltip("Simplify physics for distant NPCs")]
        public bool simplifyDistantPhysics = true;

        [Tooltip("Disable turn signals for far NPCs")]
        public bool disableFarTurnSignals = true;

        [Header("Spatial Partitioning")]
        [Tooltip("Use grid-based spatial partitioning for queries")]
        public bool useSpatialPartitioning = true;

        [Tooltip("Grid cell size for spatial partition")]
        public float gridCellSize = 50f;

        [Header("Player Tracking")]
        public Transform playerTransform;
        public bool autoFindPlayer = true;

        [Header("Performance Monitoring")]
        public bool showPerformanceStats = true;
        public int totalNPCs = 0;
        public int activeNPCs = 0;
        public int throttledNPCs = 0;
        public int disabledNPCs = 0;

        // NPC tracking
        private List<NpcCarAgent> registeredNPCs = new List<NpcCarAgent>();
        private Dictionary<NpcCarAgent, int> npcUpdateIntervals = new Dictionary<NpcCarAgent, int>();
        private Dictionary<NpcCarAgent, float> npcDistances = new Dictionary<NpcCarAgent, float>();

        // Spatial partitioning
        private Dictionary<Vector2Int, List<NpcCarAgent>> spatialGrid = new Dictionary<Vector2Int, List<NpcCarAgent>>();

        // Statistics
        private int frameCounter = 0;
        private float lastStatsUpdate = 0f;

        private static TrafficSimulationOptimizer instance;

        public static TrafficSimulationOptimizer Instance
        {
            get { return instance; }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Debug.LogWarning("[TrafficSimulationOptimizer] Multiple instances detected");
            }
        }

        private void Start()
        {
            if (playerTransform == null && autoFindPlayer)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                    Debug.Log("[TrafficSimulationOptimizer] Auto-found player");
                }
            }

            // Auto-register existing NPCs
            AutoRegisterNPCs();
        }

        private void AutoRegisterNPCs()
        {
            NpcCarAgent[] npcs = FindObjectsByType<NpcCarAgent>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                RegisterNPC(npc);
            }
            Debug.Log($"[TrafficSimulationOptimizer] Auto-registered {npcs.Length} NPCs");
        }

        /// <summary>
        /// Register an NPC for optimization
        /// </summary>
        public void RegisterNPC(NpcCarAgent npc)
        {
            if (npc == null || registeredNPCs.Contains(npc)) return;

            registeredNPCs.Add(npc);
            npcUpdateIntervals[npc] = 1; // Start with every frame
            npcDistances[npc] = float.MaxValue;

            totalNPCs++;
        }

        /// <summary>
        /// Unregister an NPC
        /// </summary>
        public void UnregisterNPC(NpcCarAgent npc)
        {
            if (npc == null) return;

            registeredNPCs.Remove(npc);
            npcUpdateIntervals.Remove(npc);
            npcDistances.Remove(npc);

            totalNPCs--;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            frameCounter++;

            // Update NPC distances and intervals
            UpdateNPCStates();

            // Update spatial partitioning
            if (useSpatialPartitioning)
            {
                UpdateSpatialPartitioning();
            }

            // Update statistics periodically
            if (Time.time - lastStatsUpdate >= 1f)
            {
                lastStatsUpdate = Time.time;
                UpdateStatistics();
            }
        }

        private void UpdateNPCStates()
        {
            Vector3 playerPos = playerTransform.position;
            activeNPCs = 0;
            throttledNPCs = 0;
            disabledNPCs = 0;

            foreach (var npc in registeredNPCs)
            {
                if (npc == null) continue;

                // Calculate distance
                float distance = Vector3.Distance(playerPos, npc.transform.position);
                npcDistances[npc] = distance;

                // Determine update interval and state
                int updateInterval = DetermineUpdateInterval(distance);
                npcUpdateIntervals[npc] = updateInterval;

                // Apply optimizations based on distance
                ApplyNPCOptimizations(npc, distance, updateInterval);

                // Count statistics
                if (distance > veryFarDistance && disableVeryFarAI)
                    disabledNPCs++;
                else if (updateInterval > 1)
                    throttledNPCs++;
                else
                    activeNPCs++;
            }
        }

        private int DetermineUpdateInterval(float distance)
        {
            if (distance < nearDistance)
                return 1; // Every frame
            else if (distance < midDistance)
                return 2; // Every 2 frames
            else if (distance < farDistance)
                return 4; // Every 4 frames
            else if (distance < veryFarDistance)
                return 8; // Every 8 frames
            else
                return 16; // Every 16 frames (event-driven)
        }

        private void ApplyNPCOptimizations(NpcCarAgent npc, float distance, int updateInterval)
        {
            // Very far: disable most behaviors
            if (distance > veryFarDistance && disableVeryFarAI)
            {
                DisableNPCBehaviors(npc);
                return;
            }

            // Far: simplify behaviors
            if (distance > farDistance)
            {
                if (disableFarTurnSignals)
                {
                    TurnSignalController turnSignal = npc.GetComponent<TurnSignalController>();
                    if (turnSignal != null)
                    {
                        turnSignal.enabled = false;
                    }
                }

                if (simplifyDistantPhysics)
                {
                    Rigidbody rb = npc.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.interpolation = RigidbodyInterpolation.None;
                    }
                }
            }
            else
            {
                // Near/Mid: enable full behaviors
                EnableNPCBehaviors(npc);
            }
        }

        private void DisableNPCBehaviors(NpcCarAgent npc)
        {
            // Disable turn signals
            TurnSignalController turnSignal = npc.GetComponent<TurnSignalController>();
            if (turnSignal != null)
            {
                turnSignal.enabled = false;
            }

            // Simplify physics
            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true; // Make kinematic at very far distance
            }
        }

        private void EnableNPCBehaviors(NpcCarAgent npc)
        {
            // Enable turn signals
            TurnSignalController turnSignal = npc.GetComponent<TurnSignalController>();
            if (turnSignal != null)
            {
                turnSignal.enabled = true;
            }

            // Restore physics
            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        /// <summary>
        /// Check if an NPC should update this frame
        /// </summary>
        public bool ShouldNPCUpdate(NpcCarAgent npc)
        {
            if (!npcUpdateIntervals.ContainsKey(npc))
                return true;

            int interval = npcUpdateIntervals[npc];
            return frameCounter % interval == 0;
        }

        /// <summary>
        /// Get NPC's cached distance to player
        /// </summary>
        public float GetNPCDistance(NpcCarAgent npc)
        {
            return npcDistances.ContainsKey(npc) ? npcDistances[npc] : float.MaxValue;
        }

        private void UpdateSpatialPartitioning()
        {
            spatialGrid.Clear();

            foreach (var npc in registeredNPCs)
            {
                if (npc == null) continue;

                Vector2Int gridCell = WorldToGridCell(npc.transform.position);

                if (!spatialGrid.ContainsKey(gridCell))
                {
                    spatialGrid[gridCell] = new List<NpcCarAgent>();
                }

                spatialGrid[gridCell].Add(npc);
            }
        }

        private Vector2Int WorldToGridCell(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / gridCellSize),
                Mathf.FloorToInt(worldPos.z / gridCellSize)
            );
        }

        /// <summary>
        /// Get NPCs in nearby grid cells (for collision avoidance, etc.)
        /// </summary>
        public List<NpcCarAgent> GetNearbyNPCs(Vector3 position, int cellRadius = 1)
        {
            if (!useSpatialPartitioning)
                return registeredNPCs;

            List<NpcCarAgent> nearby = new List<NpcCarAgent>();
            Vector2Int centerCell = WorldToGridCell(position);

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector2Int cell = centerCell + new Vector2Int(x, z);
                    if (spatialGrid.ContainsKey(cell))
                    {
                        nearby.AddRange(spatialGrid[cell]);
                    }
                }
            }

            return nearby;
        }

        private void UpdateStatistics()
        {
            // Statistics are updated in UpdateNPCStates
        }

        private void OnGUI()
        {
            if (!showPerformanceStats) return;

            GUILayout.BeginArea(new Rect(10, 670, 300, 150));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>Traffic Simulation Optimizer</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Total NPCs: {totalNPCs}");
            GUILayout.Label($"Active (Every Frame): {activeNPCs}");
            GUILayout.Label($"Throttled: {throttledNPCs}");
            GUILayout.Label($"Disabled: {disabledNPCs}");

            float savings = totalNPCs > 0 ? ((float)(throttledNPCs + disabledNPCs) / totalNPCs) * 100f : 0;
            GUILayout.Label($"CPU Savings: {savings:F1}%");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            // Draw distance rings
            Gizmos.color = Color.green;
            DrawCircle(playerTransform.position, nearDistance);

            Gizmos.color = Color.yellow;
            DrawCircle(playerTransform.position, midDistance);

            Gizmos.color = new Color(1, 0.5f, 0);
            DrawCircle(playerTransform.position, farDistance);

            Gizmos.color = Color.red;
            DrawCircle(playerTransform.position, veryFarDistance);
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
#endif
    }
}
