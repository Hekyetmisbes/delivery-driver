using UnityEngine;

namespace TrafficSystem
{
    /// <summary>
    /// NPC car agent that follows road paths using Pure Pursuit steering
    /// Handles path following, intersection navigation, and obstacle avoidance
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NpcCarAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoadGraphBuilder roadGraphBuilder;
        [SerializeField] private WheelCollider frontLeftCollider;
        [SerializeField] private WheelCollider frontRightCollider;
        [SerializeField] private WheelCollider rearLeftCollider;
        [SerializeField] private WheelCollider rearRightCollider;

        [Header("Steering Settings")]
        [Tooltip("Lookahead distance for Pure Pursuit (meters)")]
        [SerializeField] private float lookAheadDistance = 10f;
        [Tooltip("Maximum steering angle (degrees)")]
        [SerializeField] private float maxSteerAngle = 35f;
        [Tooltip("Steering smoothing speed")]
        [SerializeField] private float steeringSmoothSpeed = 5f;

        [Header("Speed Settings")]
        [Tooltip("Cruise speed range (km/h)")]
        [SerializeField] private Vector2 cruiseSpeedRange = new Vector2(30f, 50f);
        [Tooltip("Acceleration force")]
        [SerializeField] private float acceleration = 800f;
        [Tooltip("Braking force")]
        [SerializeField] private float braking = 2000f;
        [Tooltip("Speed tolerance (km/h) before applying throttle/brake")]
        [SerializeField] private float speedTolerance = 5f;

        [Header("Obstacle Avoidance")]
        [Tooltip("Enable simple raycast obstacle detection")]
        [SerializeField] private bool enableObstacleAvoidance = true;
        [Tooltip("Forward raycast distance (meters)")]
        [SerializeField] private float avoidanceRayDistance = 15f;
        [Tooltip("Brake strength when obstacle detected (0-1)")]
        [SerializeField] private float avoidanceBrakeStrength = 0.7f;
        [Tooltip("Layer mask for obstacle detection")]
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool logPathChanges = false;

        // Runtime state
        private Rigidbody rb;
        private RoadSegment currentSegment;
        private int currentWaypointIndex;
        private float targetSpeed; // km/h
        private float currentSteerAngle;
        private bool isInitialized;
        private Vector3 currentLookAheadPoint;
        private bool isObstacleDetected;

        // Public accessors
        public RoadSegment CurrentSegment => currentSegment;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f; // km/h
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            SetupRigidbody();
        }

        private void Start()
        {
            // Random cruise speed for this NPC
            targetSpeed = Random.Range(cruiseSpeedRange.x, cruiseSpeedRange.y);
        }

        private void SetupRigidbody()
        {
            rb.mass = 1500f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.centerOfMass = new Vector3(0, -0.5f, 0);
        }

        /// <summary>
        /// Initialize NPC on a specific road segment and waypoint
        /// </summary>
        public void Initialize(RoadGraphBuilder builder, RoadSegment segment, int waypointIndex)
        {
            roadGraphBuilder = builder;
            currentSegment = segment;
            currentWaypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            isInitialized = true;

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} initialized on segment '{segment.name}' at waypoint {waypointIndex}");
            }
        }

        /// <summary>
        /// Initialize on random road position
        /// </summary>
        public void InitializeRandom(RoadGraphBuilder builder)
        {
            if (builder == null || builder.RoadGraph == null)
            {
                Debug.LogError($"[NpcCarAgent] {name} - RoadGraphBuilder or RoadGraph is null!");
                return;
            }

            var (segment, waypointIndex) = builder.RoadGraph.GetRandomWaypoint();
            if (segment != null)
            {
                Initialize(builder, segment, waypointIndex);

                // Position vehicle at waypoint
                Waypoint wp = segment.waypoints[waypointIndex];
                transform.position = wp.position + Vector3.up * 0.5f;
                transform.rotation = Quaternion.LookRotation(wp.forward);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void FixedUpdate()
        {
            if (!isInitialized || currentSegment == null) return;

            UpdatePath();
            CheckObstacles();
            ApplySteering();
            ApplyThrottle();
        }

        /// <summary>
        /// Update current path and waypoint progression
        /// </summary>
        private void UpdatePath()
        {
            if (currentSegment.waypoints.Count == 0) return;

            // Check if we've reached current waypoint
            Waypoint currentWp = currentSegment.waypoints[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(transform.position, currentWp.position);

            // Move to next waypoint if close enough
            if (distanceToWaypoint < lookAheadDistance * 0.5f)
            {
                currentWaypointIndex++;

                // Check if we've reached end of segment
                if (currentWaypointIndex >= currentSegment.waypoints.Count)
                {
                    HandleEndOfSegment();
                }
            }
        }

        /// <summary>
        /// Handle reaching end of road segment (intersection)
        /// </summary>
        private void HandleEndOfSegment()
        {
            // Check for connections (intersections)
            if (currentSegment.connections.Count > 0)
            {
                // Choose random connection
                RoadConnection connection = currentSegment.connections[Random.Range(0, currentSegment.connections.Count)];
                currentSegment = connection.toSegment;
                currentWaypointIndex = connection.toWaypointIndex;

                if (logPathChanges)
                {
                    Debug.Log($"[NpcCarAgent] {name} transitioning to segment '{currentSegment.name}' at waypoint {currentWaypointIndex}");
                }
            }
            else
            {
                // No connections - loop back or find new segment
                if (currentSegment.waypoints.Count > 1)
                {
                    // Simple loop back
                    currentWaypointIndex = 0;
                }
                else
                {
                    // Teleport to random road
                    InitializeRandom(roadGraphBuilder);
                }
            }
        }

        /// <summary>
        /// Pure Pursuit steering algorithm
        /// </summary>
        private void ApplySteering()
        {
            if (currentSegment == null || currentWaypointIndex >= currentSegment.waypoints.Count)
                return;

            // Find lookahead point
            Vector3 lookAheadPoint = GetLookAheadPoint();
            currentLookAheadPoint = lookAheadPoint;

            // Calculate steering angle using Pure Pursuit
            Vector3 localTarget = transform.InverseTransformPoint(lookAheadPoint);
            float targetSteerAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            targetSteerAngle = Mathf.Clamp(targetSteerAngle, -maxSteerAngle, maxSteerAngle);

            // Smooth steering
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steeringSmoothSpeed);

            // Apply to front wheels
            if (frontLeftCollider != null) frontLeftCollider.steerAngle = currentSteerAngle;
            if (frontRightCollider != null) frontRightCollider.steerAngle = currentSteerAngle;
        }

        /// <summary>
        /// Get lookahead point for Pure Pursuit
        /// </summary>
        private Vector3 GetLookAheadPoint()
        {
            float accumulatedDistance = 0f;
            int searchIndex = currentWaypointIndex;

            while (searchIndex < currentSegment.waypoints.Count)
            {
                Waypoint wp = currentSegment.waypoints[searchIndex];
                float distToWaypoint = Vector3.Distance(transform.position, wp.position);

                if (accumulatedDistance + distToWaypoint >= lookAheadDistance)
                {
                    return wp.position;
                }

                accumulatedDistance += distToWaypoint;
                searchIndex++;

                if (searchIndex >= currentSegment.waypoints.Count)
                {
                    // Return last waypoint if we've run out
                    return currentSegment.waypoints[currentSegment.waypoints.Count - 1].position;
                }
            }

            // Fallback to current waypoint
            return currentSegment.waypoints[currentWaypointIndex].position;
        }

        /// <summary>
        /// Simple raycast obstacle detection
        /// </summary>
        private void CheckObstacles()
        {
            isObstacleDetected = false;

            if (!enableObstacleAvoidance) return;

            // Forward raycast
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, avoidanceRayDistance, obstacleLayerMask))
            {
                // Check if we hit another vehicle or obstacle
                if (hit.collider.GetComponent<NpcCarAgent>() != null || hit.collider.GetComponent<CarController>() != null)
                {
                    isObstacleDetected = true;
                }
            }
        }

        /// <summary>
        /// Apply throttle and braking based on target speed
        /// </summary>
        private void ApplyThrottle()
        {
            float currentSpeed = CurrentSpeed;
            float speedDifference = targetSpeed - currentSpeed;

            // Reduce speed if obstacle detected
            float effectiveTargetSpeed = targetSpeed;
            if (isObstacleDetected)
            {
                effectiveTargetSpeed *= (1f - avoidanceBrakeStrength);
                speedDifference = effectiveTargetSpeed - currentSpeed;
            }

            // Apply motor torque or braking
            if (speedDifference > speedTolerance)
            {
                // Need to accelerate
                float motorTorque = acceleration * Mathf.Clamp01(speedDifference / 20f);
                if (rearLeftCollider != null) rearLeftCollider.motorTorque = motorTorque;
                if (rearRightCollider != null) rearRightCollider.motorTorque = motorTorque;

                // Release brakes
                SetBrakeTorque(0f);
            }
            else if (speedDifference < -speedTolerance || isObstacleDetected)
            {
                // Need to slow down
                float brakeTorque = isObstacleDetected ? braking * avoidanceBrakeStrength : braking * 0.3f;

                SetMotorTorque(0f);
                SetBrakeTorque(brakeTorque);
            }
            else
            {
                // Maintain speed (coast)
                SetMotorTorque(0f);
                SetBrakeTorque(100f); // Light drag
            }
        }

        private void SetMotorTorque(float torque)
        {
            if (rearLeftCollider != null) rearLeftCollider.motorTorque = torque;
            if (rearRightCollider != null) rearRightCollider.motorTorque = torque;
        }

        private void SetBrakeTorque(float torque)
        {
            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = torque;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = torque;
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = torque;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = torque;
        }

        /// <summary>
        /// Force reposition to specific segment and waypoint
        /// </summary>
        public void ForceReposition(RoadSegment segment, int waypointIndex, Vector3 position, Quaternion rotation)
        {
            currentSegment = segment;
            currentWaypointIndex = waypointIndex;
            transform.position = position;
            transform.rotation = rotation;
            rb.linearVelocity = rotation * Vector3.forward * (targetSpeed / 3.6f);
            rb.angularVelocity = Vector3.zero;

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} force repositioned to segment '{segment.name}' at waypoint {waypointIndex}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !isInitialized) return;

            // Draw current path
            if (currentSegment != null && currentWaypointIndex < currentSegment.waypoints.Count)
            {
                Gizmos.color = Color.green;
                Waypoint currentWp = currentSegment.waypoints[currentWaypointIndex];
                Gizmos.DrawLine(transform.position, currentWp.position);
                Gizmos.DrawWireSphere(currentWp.position, 1f);

                // Draw lookahead point
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentLookAheadPoint, 0.8f);
                Gizmos.DrawLine(transform.position, currentLookAheadPoint);
            }

            // Draw obstacle detection ray
            if (enableObstacleAvoidance)
            {
                Gizmos.color = isObstacleDetected ? Color.red : Color.blue;
                Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * avoidanceRayDistance);
            }
        }
    }
}
