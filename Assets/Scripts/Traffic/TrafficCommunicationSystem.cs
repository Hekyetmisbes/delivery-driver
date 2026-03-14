using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Global traffic communication system for vehicle-to-vehicle coordination
    /// Uses spatial grid for efficient nearby vehicle queries
    /// Priority 2: Cooperative Behavior - Vehicle Communication System
    /// </summary>
    public class TrafficCommunicationSystem : MonoBehaviour
    {
        public static TrafficCommunicationSystem Instance { get; private set; }

        [Header("Spatial Grid Settings")]
        [Tooltip("Size of each grid cell in meters")]
        [SerializeField] private float cellSize = 50f;

        [Tooltip("Update frequency for grid updates (seconds)")]
        [SerializeField] private float updateInterval = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Spatial grid for efficient queries
        private Dictionary<Vector2Int, List<NpcCarAgent>> spatialGrid;
        private List<NpcCarAgent> allVehicles;
        private float nextUpdateTime;

        // Vehicle state cache
        private Dictionary<NpcCarAgent, VehicleState> vehicleStates;
        private Dictionary<NpcCarAgent, Rigidbody> vehicleRigidbodies;

        // Incremental grid tracking: vehicle -> last cell key
        private Dictionary<NpcCarAgent, Vector2Int> vehicleCellKeys;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            spatialGrid = new Dictionary<Vector2Int, List<NpcCarAgent>>();
            allVehicles = new List<NpcCarAgent>();
            vehicleStates = new Dictionary<NpcCarAgent, VehicleState>();
            vehicleRigidbodies = new Dictionary<NpcCarAgent, Rigidbody>();
            vehicleCellKeys = new Dictionary<NpcCarAgent, Vector2Int>();
        }

        private void Update()
        {
            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateInterval;
                UpdateSpatialGrid();
            }
        }

        /// <summary>
        /// Register a vehicle with the communication system
        /// </summary>
        public void RegisterVehicle(NpcCarAgent vehicle)
        {
            if (vehicle == null) return;

            if (!allVehicles.Contains(vehicle))
            {
                allVehicles.Add(vehicle);
                vehicleStates[vehicle] = new VehicleState();
                vehicleRigidbodies[vehicle] = vehicle.GetComponent<Rigidbody>();
            }
        }

        /// <summary>
        /// Unregister a vehicle from the communication system
        /// </summary>
        public void UnregisterVehicle(NpcCarAgent vehicle)
        {
            if (vehicle == null) return;

            // Remove from spatial grid using cached cell key
            if (vehicleCellKeys.TryGetValue(vehicle, out Vector2Int oldKey))
            {
                if (spatialGrid.TryGetValue(oldKey, out List<NpcCarAgent> oldCell))
                {
                    oldCell.Remove(vehicle);
                }
                vehicleCellKeys.Remove(vehicle);
            }

            allVehicles.Remove(vehicle);
            vehicleStates.Remove(vehicle);
            vehicleRigidbodies.Remove(vehicle);
        }

        /// <summary>
        /// Copy all registered vehicles into caller-provided list (avoids FindObjectsByType)
        /// </summary>
        public void GetAllVehicles(List<NpcCarAgent> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < allVehicles.Count; i++)
            {
                if (allVehicles[i] != null)
                    results.Add(allVehicles[i]);
            }
        }

        /// <summary>
        /// Number of registered vehicles
        /// </summary>
        public int VehicleCount => allVehicles.Count;

        /// <summary>
        /// Incremental spatial grid update - only moves vehicles that changed cells
        /// </summary>
        private void UpdateSpatialGrid()
        {
            for (int i = allVehicles.Count - 1; i >= 0; i--)
            {
                NpcCarAgent vehicle = allVehicles[i];
                if (vehicle == null)
                {
                    allVehicles.RemoveAt(i);
                    continue;
                }

                if (!vehicle.gameObject.activeInHierarchy)
                    continue;

                Vector2Int newKey = GetCellKey(vehicle.transform.position);
                bool hadOldKey = vehicleCellKeys.TryGetValue(vehicle, out Vector2Int oldKey);

                // Only update grid if cell changed
                if (!hadOldKey || oldKey != newKey)
                {
                    // Remove from old cell
                    if (hadOldKey && spatialGrid.TryGetValue(oldKey, out List<NpcCarAgent> oldCell))
                    {
                        oldCell.Remove(vehicle);
                    }

                    // Add to new cell
                    if (!spatialGrid.TryGetValue(newKey, out List<NpcCarAgent> newCell))
                    {
                        newCell = new List<NpcCarAgent>(8);
                        spatialGrid[newKey] = newCell;
                    }
                    newCell.Add(vehicle);

                    vehicleCellKeys[vehicle] = newKey;
                }

                // Update vehicle state
                UpdateVehicleState(vehicle);
            }
        }

        /// <summary>
        /// Update cached state for a vehicle
        /// </summary>
        private void UpdateVehicleState(NpcCarAgent vehicle)
        {
            if (!vehicleStates.ContainsKey(vehicle))
            {
                vehicleStates[vehicle] = new VehicleState();
            }

            VehicleState state = vehicleStates[vehicle];
            state.position = vehicle.transform.position;
            if (!vehicleRigidbodies.TryGetValue(vehicle, out Rigidbody rb) || rb == null)
            {
                rb = vehicle.GetComponent<Rigidbody>();
                vehicleRigidbodies[vehicle] = rb;
            }

            state.velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            state.speed = vehicle.CurrentSpeed;
            state.forward = vehicle.transform.forward;
        }

        /// <summary>
        /// Get nearby vehicles within radius
        /// </summary>
        public List<NpcCarAgent> GetNearbyVehicles(Vector3 position, float radius)
        {
            List<NpcCarAgent> results = new List<NpcCarAgent>();
            GetNearbyVehicles(position, radius, results);
            return results;
        }

        /// <summary>
        /// Non-alloc nearby vehicle query into caller-provided list
        /// </summary>
        public void GetNearbyVehicles(Vector3 position, float radius, List<NpcCarAgent> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            int cellRadius = Mathf.CeilToInt(radius / cellSize);
            Vector2Int centerCell = GetCellKey(position);
            float radiusSqr = radius * radius;

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector2Int cellKey = new Vector2Int(centerCell.x + x, centerCell.y + z);

                    if (spatialGrid.TryGetValue(cellKey, out List<NpcCarAgent> cellVehicles))
                    {
                        for (int i = 0; i < cellVehicles.Count; i++)
                        {
                            NpcCarAgent vehicle = cellVehicles[i];
                            if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                                continue;

                            Vector3 delta = vehicle.transform.position - position;
                            if (delta.sqrMagnitude <= radiusSqr)
                            {
                                results.Add(vehicle);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get cached vehicle state
        /// </summary>
        public VehicleState GetVehicleState(NpcCarAgent vehicle)
        {
            if (vehicleStates.TryGetValue(vehicle, out VehicleState state))
            {
                return state;
            }

            return null;
        }

        /// <summary>
        /// Get cell key for a position
        /// </summary>
        private Vector2Int GetCellKey(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.z / cellSize)
            );
        }

        /// <summary>
        /// Get statistics for debugging
        /// </summary>
        public (int vehicleCount, int cellCount) GetStats()
        {
            return (allVehicles.Count, spatialGrid.Count);
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            var stats = GetStats();
            GUILayout.BeginArea(new Rect(10, 310, 300, 100));
            GUI.color = Color.green;
            GUILayout.Label("<b>TRAFFIC COMMUNICATION</b>");
            GUILayout.Label($"Vehicles: {stats.vehicleCount}");
            GUILayout.Label($"Grid Cells: {stats.cellCount}");
            GUILayout.Label($"Cell Size: {cellSize}m");
            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// Represents the current state of a vehicle
    /// </summary>
    [System.Serializable]
    public class VehicleState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 forward;
        public float speed;
        public bool isChangingLanes;
        public bool isTurning;
        public bool isStopping;
        public Vector3 intendedDestination;

        public VehicleState()
        {
            position = Vector3.zero;
            velocity = Vector3.zero;
            forward = Vector3.forward;
            speed = 0f;
            isChangingLanes = false;
            isTurning = false;
            isStopping = false;
            intendedDestination = Vector3.zero;
        }
    }
}
