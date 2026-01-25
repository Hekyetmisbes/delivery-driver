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
        [Tooltip("Model local forward axis (set if model is not +Z forward)")]
        [SerializeField] private Vector3 modelForwardLocal = Vector3.forward;
        [Tooltip("Model local up axis (set if model is not +Y up)")]
        [SerializeField] private Vector3 modelUpLocal = Vector3.up;
        [Tooltip("Auto-detect model forward axis from wheel collider layout")]
        [SerializeField] private bool autoDetectModelForward = true;

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

        [Header("Stability")]
        [Tooltip("Enable stability helpers (turn speed limit, downforce, anti-roll)")]
        [SerializeField] private bool enableStability = true;
        [Tooltip("Max lateral acceleration (m/s^2) used to limit speed in turns")]
        [SerializeField] private float maxLateralAcceleration = 6f;
        [Tooltip("Minimum speed factor at full steering lock")]
        [SerializeField] private float minTurnSpeedFactor = 0.35f;
        [Tooltip("Downforce coefficient (N per (m/s)^2)")]
        [SerializeField] private float downforceCoefficient = 50f;
        [Tooltip("Anti-roll stiffness")]
        [SerializeField] private float antiRollStiffness = 6000f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool logPathChanges = false;

        [Header("Grounding")]
        [Tooltip("Raycast mask for grounding the vehicle to the road surface")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Raycast height above target point")]
        [SerializeField] private float groundRaycastHeight = 5f;
        [Tooltip("Raycast distance downward")]
        [SerializeField] private float groundRaycastDistance = 10f;

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
        private float wheelBase;

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

        public float GetGroundClearanceOffset()
        {
            float offset = 0.3f;
            bool usedWheelOffsets = false;

            offset = Mathf.Max(offset, GetWheelRootOffset(frontLeftCollider, ref usedWheelOffsets));
            offset = Mathf.Max(offset, GetWheelRootOffset(frontRightCollider, ref usedWheelOffsets));
            offset = Mathf.Max(offset, GetWheelRootOffset(rearLeftCollider, ref usedWheelOffsets));
            offset = Mathf.Max(offset, GetWheelRootOffset(rearRightCollider, ref usedWheelOffsets));

            if (!usedWheelOffsets)
            {
                offset = Mathf.Max(offset, GetWheelClearance(frontLeftCollider));
                offset = Mathf.Max(offset, GetWheelClearance(frontRightCollider));
                offset = Mathf.Max(offset, GetWheelClearance(rearLeftCollider));
                offset = Mathf.Max(offset, GetWheelClearance(rearRightCollider));
            }

            return offset + 0.05f;
        }

        private float GetWheelClearance(WheelCollider wheel)
        {
            if (wheel == null) return 0f;
            return wheel.radius + wheel.suspensionDistance;
        }

        private float GetWheelRootOffset(WheelCollider wheel, ref bool used)
        {
            if (wheel == null) return 0f;
            Vector3 localPos = transform.InverseTransformPoint(wheel.transform.position);
            float required = wheel.radius - localPos.y;
            used = true;
            return required;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            SetupRigidbody();
            AutoFixWheelAssignments();
            CacheWheelGeometry();
            NormalizeModelAxes();
            ConfigureGroundMaskIfNeeded();
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
                Vector3 desiredPos = wp.position + Vector3.up * GetGroundClearanceOffset();
                transform.position = GetGroundedPosition(desiredPos);

                // Safe rotation - flatten to horizontal plane for road following
                Vector3 forward = GetFlattenedForward(wp.forward);
                ApplyAlignedRotation(forward);

                // Start with random initial forward velocity (30-70% of target speed)
                float initialSpeedFactor = Random.Range(0.3f, 0.7f);
                rb.linearVelocity = GetWorldForward() * (targetSpeed / 3.6f * initialSpeedFactor);
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void FixedUpdate()
        {
            if (!isInitialized || currentSegment == null) return;

            UpdatePath();
            CheckObstacles();
            ApplySteering();
            ApplyStability();
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
                Vector3 newPos = wp.position + Vector3.up * GetGroundClearanceOffset();

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
            Vector3 localTarget = Quaternion.Inverse(GetVehicleRotation()) * (flatLookAhead - transform.position);
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
                float dotProduct = Vector3.Dot(toWaypoint.normalized, GetWorldForward());
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
            float effectiveTargetSpeed = GetTurnLimitedTargetSpeed();

            // Reduce speed if obstacle detected
            if (isObstacleDetected)
            {
                effectiveTargetSpeed *= (1f - avoidanceBrakeStrength);
            }

            float speedDifference = effectiveTargetSpeed - currentSpeed;

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

        private float GetTurnLimitedTargetSpeed()
        {
            if (!enableStability)
                return targetSpeed;

            float steerAbs = Mathf.Abs(currentSteerAngle);
            if (steerAbs < 1f || wheelBase < 0.5f)
                return targetSpeed;

            float steerRad = Mathf.Deg2Rad * Mathf.Clamp(steerAbs, 1f, maxSteerAngle);
            float turnRadius = wheelBase / Mathf.Tan(steerRad);
            if (turnRadius < 0.1f) turnRadius = 0.1f;

            float maxSpeedMs = Mathf.Sqrt(Mathf.Max(0.1f, maxLateralAcceleration * Mathf.Abs(turnRadius)));
            float maxSpeedKmh = maxSpeedMs * 3.6f;

            float steerFactor = Mathf.Lerp(1f, minTurnSpeedFactor, steerAbs / maxSteerAngle);
            return Mathf.Min(targetSpeed * steerFactor, maxSpeedKmh);
        }

        private void ApplyStability()
        {
            if (!enableStability || rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            if (speed > 1f && downforceCoefficient > 0f)
            {
                rb.AddForce(-transform.up * downforceCoefficient * speed * speed);
            }

            if (antiRollStiffness > 0f)
            {
                ApplyAntiRoll(frontLeftCollider, frontRightCollider, antiRollStiffness);
                ApplyAntiRoll(rearLeftCollider, rearRightCollider, antiRollStiffness);
            }
        }

        private void ApplyAntiRoll(WheelCollider left, WheelCollider right, float stiffness)
        {
            if (left == null || right == null) return;

            bool leftGrounded = left.GetGroundHit(out WheelHit hitL);
            bool rightGrounded = right.GetGroundHit(out WheelHit hitR);

            float leftTravel = 1f;
            float rightTravel = 1f;

            if (leftGrounded)
            {
                Vector3 local = left.transform.InverseTransformPoint(hitL.point);
                leftTravel = (-local.y - left.radius) / left.suspensionDistance;
            }

            if (rightGrounded)
            {
                Vector3 local = right.transform.InverseTransformPoint(hitR.point);
                rightTravel = (-local.y - right.radius) / right.suspensionDistance;
            }

            float antiRollForce = (leftTravel - rightTravel) * stiffness;

            if (leftGrounded)
                rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
            if (rightGrounded)
                rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }

        private void CacheWheelGeometry()
        {
            if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
            {
                wheelBase = 2.5f;
                return;
            }

            Vector3 frontAxle = (frontLeftCollider.transform.position + frontRightCollider.transform.position) * 0.5f;
            Vector3 rearAxle = (rearLeftCollider.transform.position + rearRightCollider.transform.position) * 0.5f;
            wheelBase = Vector3.Distance(frontAxle, rearAxle);
            if (wheelBase < 0.5f) wheelBase = 2.5f;

            if (autoDetectModelForward)
            {
                Vector3 frontLocal = transform.InverseTransformPoint(frontAxle);
                Vector3 rearLocal = transform.InverseTransformPoint(rearAxle);
                Vector3 localForward = (frontLocal - rearLocal);
                if (localForward.sqrMagnitude > 0.001f)
                {
                    modelForwardLocal = localForward.normalized;
                }
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
            transform.position = GetGroundedPosition(position);

            // Align to road forward using model axes
            Vector3 forward = rotation * Vector3.forward;
            forward = GetFlattenedForward(forward);
            ApplyAlignedRotation(forward);

            rb.linearVelocity = GetWorldForward() * (targetSpeed / 3.6f);
            rb.angularVelocity = Vector3.zero;

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} force repositioned to segment '{segment.name}' at waypoint {waypointIndex}");
            }
        }

        private Vector3 GetGroundedPosition(Vector3 desiredPosition)
        {
            Vector3 origin = desiredPosition + Vector3.up * groundRaycastHeight;
            float maxDistance = groundRaycastHeight + groundRaycastDistance;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null) continue;
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    return hit.point + Vector3.up * GetGroundClearanceOffset();
                }
            }

            return desiredPosition;
        }

        private void ConfigureGroundMaskIfNeeded()
        {
            if (groundMask.value != ~0)
                return;

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer >= 0)
                groundMask = 1 << roadLayer;
        }

        private void AutoFixWheelAssignments()
        {
            if (!autoDetectModelForward)
                return;

            if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
                return;

            WheelCollider[] wheels = { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
            Vector3[] localPositions = new Vector3[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                localPositions[i] = transform.InverseTransformPoint(wheels[i].transform.position);

            float minX = localPositions[0].x;
            float maxX = localPositions[0].x;
            float minZ = localPositions[0].z;
            float maxZ = localPositions[0].z;
            for (int i = 1; i < localPositions.Length; i++)
            {
                Vector3 p = localPositions[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            float spanX = maxX - minX;
            float spanZ = maxZ - minZ;
            Vector3 axis = spanZ >= spanX ? Vector3.forward : Vector3.right;

            List<(WheelCollider wheel, Vector3 localPos, float proj)> list = new List<(WheelCollider, Vector3, float)>();
            foreach (WheelCollider wheel in wheels)
            {
                Vector3 lp = transform.InverseTransformPoint(wheel.transform.position);
                list.Add((wheel, lp, Vector3.Dot(lp, axis)));
            }

            list.Sort((a, b) => b.proj.CompareTo(a.proj));
            var frontA = list[0];
            var frontB = list[1];
            var rearA = list[2];
            var rearB = list[3];

            Vector3 frontAvg = (frontA.localPos + frontB.localPos) * 0.5f;
            float sign = Vector3.Dot(frontAvg, axis) >= 0f ? 1f : -1f;
            modelForwardLocal = axis * sign;

            Vector3 rightAxis = Vector3.Cross(Vector3.up, modelForwardLocal).normalized;
            if (rightAxis.sqrMagnitude < 0.001f)
                rightAxis = Vector3.right;

            float frontADot = Vector3.Dot(frontA.localPos, rightAxis);
            float frontBDot = Vector3.Dot(frontB.localPos, rightAxis);
            float rearADot = Vector3.Dot(rearA.localPos, rightAxis);
            float rearBDot = Vector3.Dot(rearB.localPos, rightAxis);

            frontLeftCollider = frontADot <= frontBDot ? frontA.wheel : frontB.wheel;
            frontRightCollider = frontADot <= frontBDot ? frontB.wheel : frontA.wheel;
            rearLeftCollider = rearADot <= rearBDot ? rearA.wheel : rearB.wheel;
            rearRightCollider = rearADot <= rearBDot ? rearB.wheel : rearA.wheel;
        }

        private void NormalizeModelAxes()
        {
            if (modelForwardLocal.sqrMagnitude < 0.001f)
                modelForwardLocal = Vector3.forward;
            if (modelUpLocal.sqrMagnitude < 0.001f)
                modelUpLocal = Vector3.up;

            modelForwardLocal.Normalize();
            modelUpLocal.Normalize();
        }

        private Vector3 GetWorldForward()
        {
            return transform.TransformDirection(modelForwardLocal).normalized;
        }

        private Vector3 GetWorldUp()
        {
            return transform.TransformDirection(modelUpLocal).normalized;
        }

        private Quaternion GetVehicleRotation()
        {
            Vector3 worldForward = GetWorldForward();
            Vector3 worldUp = GetWorldUp();

            if (worldForward.sqrMagnitude < 0.01f)
                worldForward = transform.forward;
            if (worldUp.sqrMagnitude < 0.01f)
                worldUp = Vector3.up;

            return Quaternion.LookRotation(worldForward, worldUp);
        }

        private Vector3 GetFlattenedForward(Vector3 forward)
        {
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            return forward;
        }

        private void ApplyAlignedRotation(Vector3 desiredForward)
        {
            Quaternion modelBasis = Quaternion.LookRotation(modelForwardLocal, modelUpLocal);
            Quaternion targetBasis = Quaternion.LookRotation(desiredForward, Vector3.up);
            transform.rotation = targetBasis * Quaternion.Inverse(modelBasis);
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
