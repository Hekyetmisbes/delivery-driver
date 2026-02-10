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
        [SerializeField] private float offRoadThreshold = 8f; // Catch cars before they go too far
        [Tooltip("Max vertical distance from road before considered off-road (meters)")]
        [SerializeField] private float verticalOffRoadThreshold = 3f;
        [Tooltip("Check interval (seconds)")]
        [SerializeField] private float checkInterval = 0.5f; // Faster detection
        [Tooltip("Seconds off-road before recovery triggers")]
        [SerializeField] private float offRoadRequiredSeconds = 2.0f; // Less aggressive
        [Tooltip("Cooldown after a recovery (seconds)")]
        [SerializeField] private float recoveryCooldownSeconds = 5f; // Longer cooldown to prevent loops
        [Tooltip("Disable all recovery logic for this NPC")]
        [SerializeField] private bool disableRecovery = false;
        [Tooltip("Disable recovery if name contains any of these (case-insensitive)")]
        [SerializeField] private string[] disableRecoveryNameContains = new string[0];

        [Header("Scene Boundary")]
        [Tooltip("Enable scene boundary safety net")]
        [SerializeField] private bool enableSceneBoundary = true;
        [Tooltip("Maximum distance from boundary center before emergency recovery (meters)")]
        [SerializeField] private float sceneBoundaryRadius = 150f;
        [Tooltip("Emergency recovery ignores cooldown")]
        [SerializeField] private bool emergencyRecoveryIgnoresCooldown = true;

        [Header("Stuck Detection")]
        [Tooltip("Minimum speed before considered stuck (km/h)")]
        [SerializeField] private float stuckSpeedThreshold = 2f;  // More sensitive
        [Tooltip("Time at low speed before triggering recovery (seconds)")]
        [SerializeField] private float stuckTimeSeconds = 5f;  // Increased to reduce false positives

        [Header("Recovery Settings")]
        [Tooltip("Height offset when snapping back to road (meters)")]
        [SerializeField] private float snapHeightOffset = 1f;
        [Tooltip("Speed after recovery (km/h)")]
        [SerializeField] private float recoverySpeed = 20f;
        [Tooltip("Raycast mask for snapping to road/ground")]
        [SerializeField] private LayerMask snapGroundMask = ~0;
        [Tooltip("Raycast height above snap point")]
        [SerializeField] private float snapRaycastHeight = 5f;
        [Tooltip("Raycast distance downward")]
        [SerializeField] private float snapRaycastDistance = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logRecoveryEvents = false;

        // Components
        private NpcCarAgent carAgent;
        private Rigidbody rb;
        private RoadGraphBuilder roadGraphBuilder;

        // Runtime state
        private float nextCheckTime;
        private float stuckTimer;
        private Vector3 lastPosition;
        private bool isRecovering;
        private float offRoadTimer;
        private float lastRecoveryTime;
        private Vector3 boundaryCenter;

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
            boundaryCenter = transform.position;

            // FORCE FIX: Enable recovery for all vehicles
            // This overrides any prefab settings that might be stuck
            if (disableRecovery)
            {
                // Debug.LogWarning($"[NpcRecovery] {name} - FORCE ENABLING recovery (was disabled in prefab)");
                disableRecovery = false;
            }

            if (roadGraphBuilder != null)
            {
                RecalculateBoundaryCenterAndRadius();
            }

            // Debug.Log($"[NpcRecovery] {name} - Recovery ENABLED, Boundary radius: {sceneBoundaryRadius:F1}m");
        }

        /// <summary>
        /// Set road graph builder reference
        /// </summary>
        public void Initialize(RoadGraphBuilder builder)
        {
            roadGraphBuilder = builder;
            RecalculateBoundaryCenterAndRadius();
        }

        private void Update()
        {
            if (!carAgent.IsInitialized || roadGraphBuilder == null) return;

            // Debug log to see if recovery is disabled
            if (showDebugInfo && Time.frameCount % 120 == 0) // Log every 120 frames
            {
                Debug.Log($"[NpcRecovery] {name} - DisableRecovery: {disableRecovery}, DisableNameTokens: {(disableRecoveryNameContains?.Length ?? 0)}");
            }

            // Skip checks if disabled, but still log
            if (IsRecoveryDisabled()) return;

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
            var roadInfo = GetRoadDistanceInfo();

            bool isOffRoad = roadInfo.horizontalDistance > offRoadThreshold || roadInfo.verticalDistance > verticalOffRoadThreshold;
            if (isOffRoad)
            {
                offRoadTimer += checkInterval;
                if (logRecoveryEvents)
                {
                    Debug.Log($"[NpcRecovery] {name} is off-road! Horizontal: {roadInfo.horizontalDistance:F1}m, Vertical: {roadInfo.verticalDistance:F1}m");
                }
                if (offRoadTimer >= offRoadRequiredSeconds && Time.time - lastRecoveryTime >= recoveryCooldownSeconds)
                {
                    TriggerRecovery("Off-road");
                }
            }
            else
            {
                offRoadTimer = 0f;
            }

            // Check if vehicle is flipped
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            if (showDebugInfo && Time.frameCount % 60 == 0) // Log every 60 frames
            {
                Debug.Log($"[NpcRecovery] {name} - UpDot: {upDot:F2}, IsFlipped: {IsFlipped()}, DisableRecovery: {IsRecoveryDisabled()}");
            }

            if (IsFlipped())
            {
                if (Time.time - lastRecoveryTime >= recoveryCooldownSeconds)
                {
                    if (logRecoveryEvents)
                    {
                        Debug.Log($"[NpcRecovery] {name} is flipped! UpDot: {upDot:F2}");
                    }
                    TriggerRecovery("Flipped");
                }
            }

            // Check if vehicle is falling
            if (rb != null && rb.linearVelocity.y < -10f) // Falling fast
            {
                if (Time.time - lastRecoveryTime >= recoveryCooldownSeconds)
                {
                    if (logRecoveryEvents)
                    {
                        Debug.Log($"[NpcRecovery] {name} is falling! Velocity Y: {rb.linearVelocity.y:F1}m/s");
                    }
                    TriggerRecovery("Falling");
                }
            }

            // Check scene boundary
            if (enableSceneBoundary)
            {
                float distanceFromBoundaryCenter = Vector3.Distance(GetReferencePosition(), boundaryCenter);
                if (distanceFromBoundaryCenter > sceneBoundaryRadius)
                {
                    bool allowRecovery = emergencyRecoveryIgnoresCooldown || (Time.time - lastRecoveryTime >= recoveryCooldownSeconds);
                    if (allowRecovery)
                    {
                        if (logRecoveryEvents)
                        {
                            Debug.Log($"[NpcRecovery] {name} exceeded scene boundary! Distance: {distanceFromBoundaryCenter:F1}m (limit: {sceneBoundaryRadius:F1}m)");
                        }
                        TriggerRecovery("Scene Boundary");
                    }
                }
            }
        }

        /// <summary>
        /// Update stuck detection based on low speed
        /// </summary>
        private void UpdateStuckDetection()
        {
            if (IsRecoveryDisabled()) return;
            if (isRecovering) return;

            float currentSpeed = carAgent.CurrentSpeed;

            if (currentSpeed < stuckSpeedThreshold)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckTimeSeconds)
                {
                    if (logRecoveryEvents)
                    {
                        Debug.Log($"[NpcRecovery] {name} is stuck! Speed: {currentSpeed:F1} km/h for {stuckTimer:F1}s");
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
        private (float horizontalDistance, float verticalDistance) GetRoadDistanceInfo()
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
                return (0f, 0f);

            Vector3 referencePos = GetReferencePosition();
            var (segment, waypointIndex, projectedPoint, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(referencePos);
            if (segment == null)
                return (0f, 0f);

            Vector3 flatPos = referencePos;
            flatPos.y = projectedPoint.y;

            float horizontal = Vector3.Distance(flatPos, projectedPoint);
            float vertical = Mathf.Abs(referencePos.y - projectedPoint.y);
            return (horizontal, vertical);
        }

        /// <summary>
        /// Trigger recovery procedure
        /// </summary>
        private void TriggerRecovery(string reason)
        {
            // Don't check IsRecoveryDisabled here - let emergency recoveries work
            if (isRecovering) return;
            if (Time.time - lastRecoveryTime < recoveryCooldownSeconds) return;

            recoveryCount++;
            isRecovering = true;
            lastRecoveryTime = Time.time;

            if (logRecoveryEvents)
            {
                Debug.Log($"[NpcRecovery] {name} triggering recovery (Reason: {reason}, Count: {recoveryCount})");
            }

            // Find nearest road segment and project vehicle back
            Vector3 referencePos = GetReferencePosition();
            var (segment, waypointIndex, projectedPoint, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(referencePos);

            if (segment != null)
            {
                // Use projected point directly (it's on the road)
                float heightOffset = snapHeightOffset;
                if (carAgent != null)
                    heightOffset = carAgent.GetGroundClearanceOffset();

                Vector3 snapBase = GetGroundedPoint(projectedPoint);
                Vector3 snapPosition = snapBase + Vector3.up * heightOffset;

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
                    Debug.Log($"[NpcRecovery] {name} - No road segment found for recovery! Reinitializing randomly.");
                }
                carAgent.InitializeRandom(roadGraphBuilder);
            }

            isRecovering = false;
            offRoadTimer = 0f;
        }

        /// <summary>
        /// Snap vehicle to road position and rotation
        /// </summary>
        private void SnapToRoad(RoadSegment segment, int waypointIndex, Vector3 position, Quaternion rotation)
        {
            // CRITICAL FIX: Make rigidbody kinematic during snap to prevent physics explosions
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            // Reset all physics
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Set position and rotation using rigidbody (not transform)
            rb.position = position;
            rb.rotation = rotation;

            // Sync physics transforms immediately
            Physics.SyncTransforms();

            // Now use ForceReposition from NpcCarAgent
            carAgent.ForceReposition(segment, waypointIndex, position, rotation);

            // Re-enable physics and apply gentle velocity
            rb.isKinematic = wasKinematic;

            // Apply recovery velocity only for dynamic rigidbodies.
            Vector3 recoveryVelocity = rotation * Vector3.forward * (recoverySpeed / 3.6f);
            if (!rb.isKinematic)
            {
                rb.linearVelocity = recoveryVelocity;
                rb.angularVelocity = Vector3.zero;
            }

            // Reset timers
            stuckTimer = 0f;
            offRoadTimer = 0f;
            lastPosition = position;

            if (logRecoveryEvents)
            {
                Debug.Log($"[NpcRecovery] {name} snapped to segment '{segment.name}' at waypoint {waypointIndex}, pos: {position}, velocity: {recoveryVelocity.magnitude:F1}m/s");
            }
        }

        private Vector3 GetReferencePosition()
        {
            if (carAgent != null)
                return carAgent.GetReferencePosition();

            if (rb != null)
                return rb.worldCenterOfMass;

            return transform.position;
        }

        private Vector3 GetGroundedPoint(Vector3 desiredPoint)
        {
            Vector3 origin = desiredPoint + Vector3.up * snapRaycastHeight;
            float maxDistance = snapRaycastHeight + snapRaycastDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, snapGroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return desiredPoint;
        }

        /// <summary>
        /// Check if vehicle has flipped over
        /// </summary>
        private bool IsFlipped()
        {
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            // Less aggressive: flipped only if upDot < 0.3 (~70 degrees tilted)
            // This prevents false positives during normal cornering
            return upDot < 0.3f;
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
            if (IsRecoveryDisabled()) return;

            // Draw debug info above vehicle in world space
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3f);
            if (screenPos.z > 0)
            {
                var roadInfo = GetRoadDistanceInfo();
                float distance = roadInfo.horizontalDistance;
                string status = (roadInfo.horizontalDistance > offRoadThreshold || roadInfo.verticalDistance > verticalOffRoadThreshold) ? "OFF-ROAD" : "OK";
                Color textColor = status == "OFF-ROAD" ? Color.red : Color.green;

                GUI.color = textColor;
                GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 20, 100, 40),
                    $"{status}\nH {roadInfo.horizontalDistance:F1}m\nV {roadInfo.verticalDistance:F1}m\nRec: {recoveryCount}");
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

        public void SetRecoveryDisabled(bool disabled)
        {
            disableRecovery = disabled;
        }

        private bool IsRecoveryDisabled()
        {
            if (disableRecovery) return true;
            if (disableRecoveryNameContains == null || disableRecoveryNameContains.Length == 0) return false;

            string n = name.ToLowerInvariant();
            for (int i = 0; i < disableRecoveryNameContains.Length; i++)
            {
                string token = disableRecoveryNameContains[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (n.Contains(token.ToLowerInvariant())) return true;
            }

            return false;
        }

        private void RecalculateBoundaryCenterAndRadius()
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null || roadGraphBuilder.RoadGraph.roadSegments == null || roadGraphBuilder.RoadGraph.roadSegments.Count == 0)
            {
                boundaryCenter = transform.position;
                return;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            float maxDist = 0f;

            foreach (RoadSegment segment in roadGraphBuilder.RoadGraph.roadSegments)
            {
                if (segment == null || segment.waypoints == null) continue;

                foreach (Waypoint wp in segment.waypoints)
                {
                    sum += wp.position;
                    count++;
                }
            }

            if (count == 0)
            {
                boundaryCenter = transform.position;
                return;
            }

            boundaryCenter = sum / count;

            foreach (RoadSegment segment in roadGraphBuilder.RoadGraph.roadSegments)
            {
                if (segment == null || segment.waypoints == null) continue;

                foreach (Waypoint wp in segment.waypoints)
                {
                    float d = Vector3.Distance(wp.position, boundaryCenter);
                    if (d > maxDist) maxDist = d;
                }
            }

            // Keep user-defined small radii intact, but auto-expand if the road network is larger.
            sceneBoundaryRadius = Mathf.Max(sceneBoundaryRadius, maxDist + 30f);
        }
    }
}
