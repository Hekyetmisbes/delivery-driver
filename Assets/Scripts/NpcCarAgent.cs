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

        [System.NonSerialized] // Prevent serialization cycle
        private RoadSegment currentSegment;
        private int currentWaypointIndex;
        private float targetSpeed; // km/h
        private float currentSteerAngle;
        private bool isInitialized;
        private Vector3 currentLookAheadPoint;
        private bool isObstacleDetected;

        // Randomized behavior parameters
        private float lateralOffset; // Lateral offset from centerline (for lane variation)
        private float personalityLookAhead; // Randomized lookahead distance
        private float personalityAcceleration; // Randomized acceleration
        private float personalitySteerSpeed; // Randomized steering speed

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
            // Random cruise speed for this NPC with larger variation
            targetSpeed = Random.Range(cruiseSpeedRange.x, cruiseSpeedRange.y);

            // Add 30% random variation to make each car more unique
            float variation = targetSpeed * Random.Range(-0.3f, 0.3f);
            targetSpeed += variation;
            targetSpeed = Mathf.Max(20f, targetSpeed); // Minimum 20 km/h

            // Randomize driving personality
            lateralOffset = Random.Range(-2.5f, 2.5f); // Random lane position (-2.5 to 2.5 meters)
            personalityLookAhead = lookAheadDistance * Random.Range(0.6f, 1.4f); // Vary lookahead 60-140%
            personalityAcceleration = acceleration * Random.Range(0.7f, 1.3f); // Vary acceleration 70-130%
            personalitySteerSpeed = steeringSmoothSpeed * Random.Range(0.7f, 1.3f); // Vary steering 70-130%

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} - Speed: {targetSpeed:F1} km/h, Offset: {lateralOffset:F1}m, Lookahead: {personalityLookAhead:F1}m");
            }
        }

        private void SetupRigidbody()
        {
            // Randomize physics properties slightly for variation
            rb.mass = Random.Range(1300f, 1700f); // 1300-1700 kg
            rb.linearDamping = Random.Range(0.04f, 0.06f);
            rb.angularDamping = Random.Range(0.4f, 0.6f);
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

                // Position vehicle at waypoint (waypoint is already on the road)
                Waypoint wp = segment.waypoints[waypointIndex];
                transform.position = wp.position + Vector3.up * 0.2f;

                // Safe rotation - flatten to horizontal plane for road following
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
                transform.rotation = Quaternion.LookRotation(forward);

                // Start with random initial forward velocity (30-70% of target speed)
                float initialSpeedFactor = Random.Range(0.3f, 0.7f);
                rb.linearVelocity = forward * (targetSpeed / 3.6f * initialSpeedFactor);
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

            // Move to next waypoint if close enough - use personalized lookahead for variation
            if (distanceToWaypoint < personalityLookAhead * 0.5f)
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
            if (currentSegment.connections != null && currentSegment.connections.Count > 0)
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
                // No connections - randomly choose to loop or teleport
                float choice = Random.value;

                if (choice < 0.3f && currentSegment.waypoints.Count > 1) // 30% loop back
                {
                    currentWaypointIndex = 0;
                    if (logPathChanges)
                    {
                        Debug.Log($"[NpcCarAgent] {name} looping back on segment '{currentSegment.name}'");
                    }
                }
                else // 70% teleport to random location
                {
                    if (logPathChanges)
                    {
                        Debug.Log($"[NpcCarAgent] {name} teleporting to random road");
                    }
                    TeleportToRandomRoad();
                }
            }
        }

        /// <summary>
        /// Teleport vehicle to random road position
        /// </summary>
        private void TeleportToRandomRoad()
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
                return;

            var (segment, waypointIndex) = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
            if (segment != null && segment.waypoints.Count > 0)
            {
                Waypoint wp = segment.waypoints[waypointIndex];

                // Use waypoint position directly (it's on the road)
                Vector3 newPos = wp.position + Vector3.up * 0.2f;

                // Flatten forward direction
                Vector3 forward = wp.forward;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                forward.y = 0;
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.forward;
                }
                else
                {
                    forward.Normalize();
                }
                Quaternion newRot = Quaternion.LookRotation(forward);

                ForceReposition(segment, waypointIndex, newPos, newRot);
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

            // Project lookahead point to horizontal plane for better steering
            Vector3 flatLookAhead = lookAheadPoint;
            flatLookAhead.y = transform.position.y;

            // Calculate steering angle using Pure Pursuit
            Vector3 localTarget = transform.InverseTransformPoint(flatLookAhead);
            float targetSteerAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            targetSteerAngle = Mathf.Clamp(targetSteerAngle, -maxSteerAngle, maxSteerAngle);

            // Smooth steering with adaptive speed based on turn sharpness and personality
            float steerSpeed = personalitySteerSpeed * (1f + Mathf.Abs(targetSteerAngle) / maxSteerAngle);
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steerSpeed);

            // Apply to front wheels
            if (frontLeftCollider != null) frontLeftCollider.steerAngle = currentSteerAngle;
            if (frontRightCollider != null) frontRightCollider.steerAngle = currentSteerAngle;
        }

        /// <summary>
        /// Get lookahead point for Pure Pursuit with lateral offset for lane variation
        /// </summary>
        private Vector3 GetLookAheadPoint()
        {
            if (currentSegment == null || currentWaypointIndex >= currentSegment.waypoints.Count)
                return transform.position + transform.forward * personalityLookAhead;

            // Start from current waypoint
            Vector3 carPos = transform.position;
            float remainingDistance = personalityLookAhead; // Use personalized lookahead
            int searchIndex = currentWaypointIndex;

            // Skip waypoints that are behind us
            while (searchIndex < currentSegment.waypoints.Count)
            {
                Vector3 wpPos = currentSegment.waypoints[searchIndex].position;
                Vector3 toWaypoint = wpPos - carPos;

                // Check if waypoint is ahead of us
                float dotProduct = Vector3.Dot(toWaypoint.normalized, transform.forward);
                if (dotProduct > 0.3f) // At least somewhat in front
                {
                    break;
                }

                searchIndex++;
            }

            // Find lookahead point
            Vector3 targetPoint = Vector3.zero;
            while (searchIndex < currentSegment.waypoints.Count)
            {
                Waypoint wp = currentSegment.waypoints[searchIndex];
                float distToWaypoint = Vector3.Distance(carPos, wp.position);

                if (distToWaypoint >= remainingDistance)
                {
                    targetPoint = wp.position;
                    break;
                }

                remainingDistance -= distToWaypoint;
                searchIndex++;
            }

            // Return last waypoint if we've run out
            if (targetPoint == Vector3.zero)
            {
                if (currentSegment.waypoints.Count > 0)
                {
                    targetPoint = currentSegment.waypoints[currentSegment.waypoints.Count - 1].position;
                }
                else
                {
                    // Ultimate fallback
                    return transform.position + transform.forward * personalityLookAhead;
                }
            }

            // Apply lateral offset for lane variation
            if (Mathf.Abs(lateralOffset) > 0.1f && searchIndex < currentSegment.waypoints.Count)
            {
                Waypoint wp = currentSegment.waypoints[searchIndex];
                Vector3 right = Vector3.Cross(wp.forward, Vector3.up).normalized;
                targetPoint += right * lateralOffset;
            }

            return targetPoint;
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
                // Need to accelerate - use personalized acceleration
                float motorTorque = personalityAcceleration * Mathf.Clamp01(speedDifference / 20f);
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

            // Flatten rotation to keep car level
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0;
            if (forward.sqrMagnitude > 0.01f)
            {
                forward.Normalize();
                transform.rotation = Quaternion.LookRotation(forward);
            }
            else
            {
                transform.rotation = rotation;
            }

            rb.linearVelocity = transform.forward * (targetSpeed / 3.6f);
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
