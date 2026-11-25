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
        [Tooltip("Near distance: update every physics tick")]
        public float nearDistance = 50f;

        [Tooltip("Mid distance: update every 2 physics ticks")]
        public float midDistance = 150f;

        [Tooltip("Far distance: update every 4 physics ticks")]
        public float farDistance = 300f;

        [Tooltip("Beyond far distance: update every 8-16 physics ticks")]
        public float veryFarDistance = 500f;

        [Header("Physics LOD")]
        [Tooltip("Distance to enter lightweight waypoint movement; independent of decision tiers.")]
        public float waypointLodEnterDistance = 130f;
        [Tooltip("Return to dynamic physics before entering the player interaction area.")]
        public float waypointLodExitDistance = 100f;

        [Header("Simulation Quality")]
        [Tooltip("Allow far NPCs to use lightweight movement when distant physics simplification is enabled.")]
        public bool disableVeryFarAI = true;

        [Tooltip("Use lightweight waypoint movement for distant, eligible NPCs.")]
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

        [Header("Update Budget")]
        [Tooltip("How often to refresh NPC distance/state data (seconds)")]
        public float stateUpdateInterval = 0.12f;

        [Header("Performance Monitoring")]
        public bool showPerformanceStats = false;
        public int totalNPCs = 0;
        public int activeNPCs = 0;
        public int throttledNPCs = 0;
        public int disabledNPCs = 0;

        // NPC tracking
        private List<NpcCarAgent> registeredNPCs = new List<NpcCarAgent>();
        private Dictionary<NpcCarAgent, int> npcUpdateIntervals = new Dictionary<NpcCarAgent, int>();
        private Dictionary<NpcCarAgent, float> npcDistances = new Dictionary<NpcCarAgent, float>();

        // Cached component lookups per NPC
        private Dictionary<NpcCarAgent, TurnSignalController> npcTurnSignals = new Dictionary<NpcCarAgent, TurnSignalController>();
        private Dictionary<NpcCarAgent, NpcOptimizationState> npcOptimizationStates = new Dictionary<NpcCarAgent, NpcOptimizationState>();

        // Spatial partitioning - delegated to TrafficCommunicationSystem (single grid)

        // Statistics
        private float lastStatsUpdate = 0f;
        private float lastStateUpdate = 0f;
        private GUIStyle perfHeaderStyle;

        private enum NpcOptimizationState
        {
            Full,
            Simplified,
            Disabled
        }

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

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
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
            npcTurnSignals[npc] = npc.GetComponent<TurnSignalController>();
            npcOptimizationStates[npc] = NpcOptimizationState.Full;

            totalNPCs = registeredNPCs.Count;
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
            npcTurnSignals.Remove(npc);
            npcOptimizationStates.Remove(npc);

            totalNPCs = registeredNPCs.Count;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            if (Time.time - lastStateUpdate >= stateUpdateInterval)
            {
                lastStateUpdate = Time.time;
                UpdateNPCStates();
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
            float nearSqr = nearDistance * nearDistance;
            float midSqr = midDistance * midDistance;
            float farSqr = farDistance * farDistance;
            float veryFarSqr = veryFarDistance * veryFarDistance;

            for (int i = registeredNPCs.Count - 1; i >= 0; i--)
            {
                NpcCarAgent npc = registeredNPCs[i];
                if (npc == null) continue;

                // Calculate distance
                Vector3 npcPos = npc.transform.position;
                float dx = npcPos.x - playerPos.x;
                float dz = npcPos.z - playerPos.z;
                float sqrDistance = dx * dx + dz * dz;
                npcDistances[npc] = Mathf.Sqrt(sqrDistance);

                // Determine update interval and state
                int updateInterval = DetermineUpdateIntervalSqr(sqrDistance, nearSqr, midSqr, farSqr, veryFarSqr);
                npcUpdateIntervals[npc] = updateInterval;

                // Apply optimizations based on distance
                ApplyNPCOptimizations(npc, sqrDistance, farSqr, veryFarSqr);

                // Count statistics
                if (npc.IsWaypointLod)
                    disabledNPCs++;
                else if (updateInterval > 1)
                    throttledNPCs++;
                else
                    activeNPCs++;
            }

            totalNPCs = registeredNPCs.Count;
        }

        private int DetermineUpdateIntervalSqr(float sqrDistance, float nearSqr, float midSqr, float farSqr, float veryFarSqr)
        {
            if (sqrDistance < nearSqr)
                return 1; // Every physics tick
            else if (sqrDistance < midSqr)
                return 2; // Every 2 physics ticks
            else if (sqrDistance < farSqr)
                return 4; // Every 4 physics ticks
            else if (sqrDistance < veryFarSqr)
                return 8; // Every 8 physics ticks
            else
                return 16; // Every 16 physics ticks
        }

        private void ApplyNPCOptimizations(NpcCarAgent npc, float sqrDistance, float farSqr, float veryFarSqr)
        {
            NpcOptimizationState targetState;

            // Very far: disable most behaviors
            if (sqrDistance > veryFarSqr && disableVeryFarAI)
            {
                targetState = NpcOptimizationState.Disabled;
            }
            else if (sqrDistance > farSqr)
            {
                targetState = NpcOptimizationState.Simplified;
            }
            else
            {
                targetState = NpcOptimizationState.Full;
            }

            if (npcOptimizationStates.TryGetValue(npc, out NpcOptimizationState currentState) && currentState == targetState)
            {
                return;
            }

            npcOptimizationStates[npc] = targetState;

            if (targetState == NpcOptimizationState.Disabled)
            {
                DisableNPCBehaviors(npc);
            }
            else if (targetState == NpcOptimizationState.Simplified)
            {
                SimplifyNpcBehaviors(npc);
            }
            else
            {
                EnableNPCBehaviors(npc);
            }
        }

        private void SimplifyNpcBehaviors(NpcCarAgent npc)
        {
            if (disableFarTurnSignals)
            {
                npcTurnSignals.TryGetValue(npc, out TurnSignalController turnSignal);
                if (turnSignal != null && turnSignal.enabled)
                {
                    turnSignal.enabled = false;
                }
            }

        }

        private void DisableNPCBehaviors(NpcCarAgent npc)
        {
            // Far visual quality only. NpcCarAgent exclusively owns rigidbody LOD transitions.
            SimplifyNpcBehaviors(npc);
        }

        private void EnableNPCBehaviors(NpcCarAgent npc)
        {
            // Enable turn signals
            npcTurnSignals.TryGetValue(npc, out TurnSignalController turnSignal);
            if (turnSignal != null && !turnSignal.enabled)
            {
                turnSignal.enabled = true;
            }

        }

        // Live position instead of the 120 ms statistics cache: protect fast player approaches.
        public bool TryGetPlayerDistance(NpcCarAgent npc, out float distance)
        {
            distance = 0f;
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy || npc == null)
                return false;

            Vector3 delta = playerTransform.position - npc.transform.position;
            delta.y = 0f;
            distance = delta.magnitude;
            if (!float.IsNaN(distance) && !float.IsInfinity(distance)) return true;
            distance = 0f;
            return false;
        }

        public bool ShouldUseWaypointLod(NpcCarAgent npc, bool alreadyLightweight, out float distance)
        {
            if (!TryGetPlayerDistance(npc, out distance) || !isActiveAndEnabled || !simplifyDistantPhysics)
                return false;

            float exit = Mathf.Max(nearDistance, waypointLodExitDistance);
            float enter = Mathf.Max(exit + 10f, waypointLodEnterDistance);
            // The legacy very-far toggle no longer freezes traffic; it only permits the LOD there.
            if (!disableVeryFarAI && distance >= veryFarDistance)
                return false;
            return NpcTrafficLodPolicy.UseWaypointMode(distance, alreadyLightweight, enter, exit);
        }

        /// <summary>
        /// Check if an NPC is scheduled for its current physics tick.
        /// </summary>
        public bool ShouldNPCUpdate(NpcCarAgent npc)
        {
            return !npcUpdateIntervals.TryGetValue(npc, out int interval) ||
                   IsScheduledPhysicsTick(npc.PhysicsTick, npc.PhysicsPhase, interval);
        }

        public static bool IsScheduledPhysicsTick(uint tick, uint phase, int interval)
        {
            if (interval <= 1)
                return true;

            // Stagger agents across ticks to avoid synchronized bursts.
            uint period = (uint)interval;
            return ((ulong)(tick % period) + phase % period) % period == 0;
        }

        /// <summary>
        /// Get NPC's cached distance to player
        /// </summary>
        public float GetNPCDistance(NpcCarAgent npc)
        {
            return npcDistances.TryGetValue(npc, out float distance) ? distance : float.MaxValue;
        }

        /// <summary>
        /// Get NPCs in nearby grid cells - delegates to TrafficCommunicationSystem's single spatial grid
        /// </summary>
        public List<NpcCarAgent> GetNearbyNPCs(Vector3 position, int cellRadius = 1)
        {
            float radius = cellRadius * gridCellSize;
            if (TrafficCommunicationSystem.Instance != null)
            {
                return TrafficCommunicationSystem.Instance.GetNearbyVehicles(position, radius);
            }
            return new List<NpcCarAgent>(registeredNPCs);
        }

        /// <summary>
        /// Get NPCs in nearby grid cells into a reusable list - delegates to TrafficCommunicationSystem
        /// </summary>
        public void GetNearbyNPCs(Vector3 position, List<NpcCarAgent> results, int cellRadius = 1)
        {
            float radius = cellRadius * gridCellSize;
            if (TrafficCommunicationSystem.Instance != null)
            {
                TrafficCommunicationSystem.Instance.GetNearbyVehicles(position, radius, results);
                return;
            }
            results.Clear();
            results.AddRange(registeredNPCs);
        }

        private void UpdateStatistics()
        {
            // Statistics are updated in UpdateNPCStates
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showPerformanceStats) return;

            GUILayout.BeginArea(new Rect(10, 670, 300, 150));
            GUILayout.BeginVertical("box");

            if (perfHeaderStyle == null)
            {
                perfHeaderStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }

            GUILayout.Label($"<b>Traffic Simulation Optimizer</b>", perfHeaderStyle);
            GUILayout.Label($"Total NPCs: {totalNPCs}");
            GUILayout.Label($"Active (Every Frame): {activeNPCs}");
            GUILayout.Label($"Throttled: {throttledNPCs}");
            GUILayout.Label($"Waypoint LOD: {disabledNPCs}");

            float savings = totalNPCs > 0 ? ((float)(throttledNPCs + disabledNPCs) / totalNPCs) * 100f : 0;
            GUILayout.Label($"Throttled / LOD share: {savings:F1}%");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif

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
