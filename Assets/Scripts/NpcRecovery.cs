using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// Monitors NPC vehicle for off-road or stuck conditions
    /// Snaps vehicle back to nearest road segment when needed
    /// </summary>
    [RequireComponent(typeof(NpcCarAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class NpcRecovery : MonoBehaviour
    {
        [Header("Off-Road Detection")]
        [Tooltip("Max distance from road before considered off-road (meters)")]
        [SerializeField] private float offRoadThreshold = 15f; // Increased to prevent false positives
        [Tooltip("Check interval (seconds)")]
        [SerializeField] private float checkInterval = 1f; // Check less frequently

        [Header("Stuck Detection")]
        [Tooltip("Minimum speed before considered stuck (km/h)")]
        [SerializeField] private float stuckSpeedThreshold = 3f;
        [Tooltip("Time at low speed before triggering recovery (seconds)")]
        [SerializeField] private float stuckTimeSeconds = 3f;

        [Header("Recovery Settings")]
        [Tooltip("Height offset when snapping back to road (meters)")]
        [SerializeField] private float snapHeightOffset = 1f;
        [Tooltip("Speed after recovery (km/h)")]
        [SerializeField] private float recoverySpeed = 20f;
        [Tooltip("Smooth recovery rotation speed")]
        [SerializeField] private float recoveryRotationSpeed = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logRecoveryEvents = true;

        // Components
        private NpcCarAgent carAgent;
        private Rigidbody rb;
        private RoadGraphBuilder roadGraphBuilder;

        // Runtime state
        private float nextCheckTime;
        private float stuckTimer;
        private Vector3 lastPosition;
        private bool isRecovering;

        // Stats
        private int recoveryCount;

        private void Awake()
        {
            carAgent = GetComponent<NpcCarAgent>();
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            lastPosition = transform.position;
            nextCheckTime = Time.time + checkInterval;
        }

        /// <summary>
        /// Set road graph builder reference
        /// </summary>
        public void Initialize(RoadGraphBuilder builder)
        {
            roadGraphBuilder = builder;
        }

        private void Update()
        {
            if (!carAgent.IsInitialized || roadGraphBuilder == null) return;

            // Periodic checks
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                PerformChecks();
            }

            // Update stuck timer
            UpdateStuckDetection();
        }

        /// <summary>
        /// Perform off-road and recovery checks
        /// </summary>
        private void PerformChecks()
        {
            // Check if off-road
            float distanceFromRoad = GetDistanceFromRoad();

            if (distanceFromRoad > offRoadThreshold)
            {
                if (logRecoveryEvents)
                {
                    Debug.LogWarning($"[NpcRecovery] {name} is off-road! Distance: {distanceFromRoad:F1}m");
                }
                TriggerRecovery("Off-road");
            }
        }

        /// <summary>
        /// Update stuck detection based on low speed
        /// </summary>
        private void UpdateStuckDetection()
        {
            float currentSpeed = carAgent.CurrentSpeed;

            if (currentSpeed < stuckSpeedThreshold)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckTimeSeconds)
                {
                    if (logRecoveryEvents)
                    {
                        Debug.LogWarning($"[NpcRecovery] {name} is stuck! Speed: {currentSpeed:F1} km/h for {stuckTimer:F1}s");
                    }
                    TriggerRecovery("Stuck");
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        /// <summary>
        /// Get distance from nearest road point
        /// </summary>
        private float GetDistanceFromRoad()
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
                return 0f;

            var (segment, waypointIndex, distance) = roadGraphBuilder.RoadGraph.FindNearestPoint(transform.position);
            return distance;
        }

        /// <summary>
        /// Trigger recovery procedure
        /// </summary>
        private void TriggerRecovery(string reason)
        {
            if (isRecovering) return;

            recoveryCount++;
            isRecovering = true;

            if (logRecoveryEvents)
            {
                Debug.Log($"[NpcRecovery] {name} triggering recovery (Reason: {reason}, Count: {recoveryCount})");
            }

            // Find nearest road segment and project vehicle back
            var (segment, waypointIndex, projectedPoint, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(transform.position);

            if (segment != null)
            {
                // Use projected point directly (it's on the road)
                Vector3 snapPosition = projectedPoint + Vector3.up * snapHeightOffset;

                // Safe rotation with zero vector check - flatten to horizontal
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = transform.forward; // Use current forward as fallback
                }
                tangent.y = 0;
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.forward;
                }
                else
                {
                    tangent.Normalize();
                }
                Quaternion snapRotation = Quaternion.LookRotation(tangent);

                // Perform snap
                SnapToRoad(segment, waypointIndex, snapPosition, snapRotation);
            }
            else
            {
                // Fallback: reinitialize at random position
                if (logRecoveryEvents)
                {
                    Debug.LogWarning($"[NpcRecovery] {name} - No road segment found for recovery! Reinitializing randomly.");
                }
                carAgent.InitializeRandom(roadGraphBuilder);
            }

            isRecovering = false;
        }

        /// <summary>
        /// Snap vehicle to road position and rotation
        /// </summary>
        private void SnapToRoad(RoadSegment segment, int waypointIndex, Vector3 position, Quaternion rotation)
        {
            // Use ForceReposition from NpcCarAgent
            carAgent.ForceReposition(segment, waypointIndex, position, rotation);

            // Apply recovery speed
            rb.linearVelocity = rotation * Vector3.forward * (recoverySpeed / 3.6f);
            rb.angularVelocity = Vector3.zero;

            // Reset stuck timer
            stuckTimer = 0f;
            lastPosition = position;

            if (logRecoveryEvents)
            {
                Debug.Log($"[NpcRecovery] {name} snapped to segment '{segment.name}' at waypoint {waypointIndex}");
            }
        }

        /// <summary>
        /// Check if vehicle has flipped over
        /// </summary>
        private bool IsFlipped()
        {
            return Vector3.Dot(transform.up, Vector3.up) < 0.5f;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugInfo || !Application.isPlaying) return;

            // Draw distance to road
            if (roadGraphBuilder != null && roadGraphBuilder.RoadGraph != null)
            {
                var (segment, waypointIndex, projectedPoint, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(transform.position);

                if (segment != null)
                {
                    float distance = Vector3.Distance(transform.position, projectedPoint);
                    Color lineColor = distance > offRoadThreshold ? Color.red : Color.green;

                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(transform.position, projectedPoint);
                    Gizmos.DrawWireSphere(projectedPoint, 0.5f);

                    // Draw distance text would require Handles (Editor only)
                }
            }

            // Draw stuck indicator
            if (stuckTimer > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.5f + stuckTimer * 0.2f);
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            // Draw debug info above vehicle in world space
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3f);
            if (screenPos.z > 0)
            {
                float distance = GetDistanceFromRoad();
                string status = distance > offRoadThreshold ? "OFF-ROAD" : "OK";
                Color textColor = distance > offRoadThreshold ? Color.red : Color.green;

                GUI.color = textColor;
                GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 20, 100, 40),
                    $"{status}\n{distance:F1}m\nRecoveries: {recoveryCount}");
            }
        }

        /// <summary>
        /// Public method to force recovery (can be called externally)
        /// </summary>
        public void ForceRecovery()
        {
            TriggerRecovery("Manual");
        }

        /// <summary>
        /// Get recovery statistics
        /// </summary>
        public int GetRecoveryCount() => recoveryCount;
    }
}
