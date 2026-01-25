using System.Collections.Generic;
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
        [Tooltip("Downforce coefficient (N per (m/s)^2) - reduced to prevent suspension collapse")]
        [SerializeField] private float downforceCoefficient = 20f; // Reduced from 100f
        [Tooltip("Anti-roll stiffness - higher prevents rollovers")]
        [SerializeField] private float antiRollStiffness = 12000f;

        [Header("Physics")]
        [Tooltip("Rigidbody interpolation mode")]
        [SerializeField] private RigidbodyInterpolation rbInterpolation = RigidbodyInterpolation.Interpolate;
        [Tooltip("Rigidbody collision detection mode")]
        [SerializeField] private CollisionDetectionMode rbCollisionDetection = CollisionDetectionMode.ContinuousDynamic;
        [Tooltip("Solver iterations (higher = more stable)")]
        [SerializeField] private int solverIterations = 12;
        [Tooltip("Solver velocity iterations (higher = more stable)")]
        [SerializeField] private int solverVelocityIterations = 12;

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
        [Tooltip("Prefer Road layer for grounding when groundMask is set to Everything")]
        [SerializeField] private bool preferRoadLayerIfPresent = false;
        [Tooltip("Minimum clearance used for spawn/recovery height")]
        [SerializeField] private float minGroundClearance = 0.1f;
        [Tooltip("Maximum clearance used for spawn/recovery height")]
        [SerializeField] private float maxGroundClearance = 0.5f;

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
        private bool isPotentiallyOffRoad;
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
            // Start with minimal offset
            float offset = 0.1f;

            // Use wheel radius only (suspension will compress when grounded)
            // But cap it at 0.4m to prevent spawning too high
            if (frontLeftCollider != null) offset = Mathf.Max(offset, Mathf.Min(frontLeftCollider.radius, 0.4f));
            if (frontRightCollider != null) offset = Mathf.Max(offset, Mathf.Min(frontRightCollider.radius, 0.4f));
            if (rearLeftCollider != null) offset = Mathf.Max(offset, Mathf.Min(rearLeftCollider.radius, 0.4f));
            if (rearRightCollider != null) offset = Mathf.Max(offset, Mathf.Min(rearRightCollider.radius, 0.4f));

            // CRITICAL: Clamp to very low values
            offset = Mathf.Clamp(offset, 0.1f, 0.5f);

            Debug.Log($"[NpcCarAgent] {name} - Ground clearance: {offset:F2}m");
            return offset;
        }

        private float GetWheelClearance(WheelCollider wheel)
        {
            if (wheel == null) return 0f;
            return wheel.radius; // Just radius, suspension will compress
        }

        private float GetWheelRootOffsetSafe(WheelCollider wheel, ref bool used)
        {
            if (wheel == null) return 0f;
            Vector3 localPos = transform.InverseTransformPoint(wheel.transform.position);
            float required = wheel.radius - localPos.y;
            used = true;
            float clearance = wheel.radius + wheel.suspensionDistance;
            float maxReasonable = Mathf.Max(1.5f, wheel.radius * 4f + 0.2f);
            if (required < 0.05f || required > maxReasonable)
            {
                return clearance;
            }
            return required;
        }

        private float GetBoundsClearance()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            if (colliders == null || colliders.Length == 0) return 0f;

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            float clearance = transform.position.y - bounds.min.y;
            return Mathf.Max(0f, clearance);
        }

        public Vector3 GetReferencePosition()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            if (frontLeftCollider != null) { sum += frontLeftCollider.transform.position; count++; }
            if (frontRightCollider != null) { sum += frontRightCollider.transform.position; count++; }
            if (rearLeftCollider != null) { sum += rearLeftCollider.transform.position; count++; }
            if (rearRightCollider != null) { sum += rearRightCollider.transform.position; count++; }

            if (count > 0)
                return sum / count;

            if (rb != null)
                return rb.worldCenterOfMass;

            Collider col = GetComponentInChildren<Collider>();
            if (col != null)
                return col.bounds.center;

            return transform.position;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            SetupRigidbody();
            ConfigureSuspension(); // New method to tune physics
            AutoFixWheelAssignments();
            CacheWheelGeometry();
            NormalizeModelAxes();
            ConfigureGroundMaskIfNeeded();
        }

        private void ConfigureSuspension()
        {
            // Auto-tune suspension interactions
            float totalMass = rb.mass;
            float wheelParams = totalMass * 0.25f; // approx mass per wheel

            // Calculate reasonable spring rate
            // target sag ~0.15m at rest
            float targetSag = 0.15f; 
            float springForce = (wheelParams * Mathf.Abs(Physics.gravity.y)) / targetSag;
            
            // Calculate critical damping
            // Damping ratio 1.0 = critical, 0.3-0.5 is good for slightly bouncy but stable cars
            float dampingRatio = 0.6f; // Slightly stiffer for stability
            float damperForce = 2f * dampingRatio * Mathf.Sqrt(springForce * wheelParams);

            JointSpring spring = new JointSpring
            {
                spring = springForce,
                damper = damperForce,
                targetPosition = 0.5f
            };

            // Apply to all wheels
            ApplySuspensionSettings(frontLeftCollider, spring);
            ApplySuspensionSettings(frontRightCollider, spring);
            ApplySuspensionSettings(rearLeftCollider, spring);
            ApplySuspensionSettings(rearRightCollider, spring);
        }

        private void ApplySuspensionSettings(WheelCollider wheel, JointSpring spring)
        {
            if (wheel == null) return;
            wheel.suspensionSpring = spring;
            wheel.suspensionDistance = 0.3f; // Ensure reasonable travel
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
            rb.angularDamping = Random.Range(1.0f, 1.5f); // Higher for better stability
            rb.centerOfMass = new Vector3(0, -0.8f, 0); // Lower center of mass prevents rollovers

            rb.interpolation = rbInterpolation;
            rb.collisionDetectionMode = rbCollisionDetection;
            if (solverIterations > 0) rb.solverIterations = solverIterations;
            if (solverVelocityIterations > 0) rb.solverVelocityIterations = solverVelocityIterations;
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
            UpdateOffRoadStatus();
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
        /// Update off-road status based on proximity to road
        /// </summary>
        private void UpdateOffRoadStatus()
        {
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
            {
                isPotentiallyOffRoad = false;
                return;
            }

            Vector3 referencePos = GetReferencePosition();
            var (segment, waypointIndex, projectedPoint, tangent) = roadGraphBuilder.RoadGraph.ProjectPointOnRoad(referencePos);

            if (segment == null)
            {
                isPotentiallyOffRoad = true;
                return;
            }

            Vector3 flatPos = referencePos;
            flatPos.y = projectedPoint.y;
            float horizontalDistance = Vector3.Distance(flatPos, projectedPoint);

            // Consider off-road if more than 5m from road
            isPotentiallyOffRoad = horizontalDistance > 5f;
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

            // Reduce steering when off-road to prevent wild maneuvers
            float finalSteerAngle = currentSteerAngle;
            if (isPotentiallyOffRoad)
            {
                finalSteerAngle *= 0.5f;
            }

            // Apply to front wheels
            if (frontLeftCollider != null) frontLeftCollider.steerAngle = finalSteerAngle;
            if (frontRightCollider != null) frontRightCollider.steerAngle = finalSteerAngle;
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
            float baseSpeed = targetSpeed;

            // Reduce speed when off-road
            if (isPotentiallyOffRoad)
            {
                baseSpeed *= 0.6f;
            }

            if (!enableStability)
                return baseSpeed;

            float steerAbs = Mathf.Abs(currentSteerAngle);
            if (steerAbs < 1f || wheelBase < 0.5f)
                return baseSpeed;

            float steerRad = Mathf.Deg2Rad * Mathf.Clamp(steerAbs, 1f, maxSteerAngle);
            float turnRadius = wheelBase / Mathf.Tan(steerRad);
            if (turnRadius < 0.1f) turnRadius = 0.1f;

            float maxSpeedMs = Mathf.Sqrt(Mathf.Max(0.1f, maxLateralAcceleration * Mathf.Abs(turnRadius)));
            float maxSpeedKmh = maxSpeedMs * 3.6f;

            float steerFactor = Mathf.Lerp(1f, minTurnSpeedFactor, steerAbs / maxSteerAngle);
            return Mathf.Min(baseSpeed * steerFactor, maxSpeedKmh);
        }

        private void ApplyStability()
        {
            if (!enableStability || rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            
            // Apply downforce - always apply some base downforce plus speed-based
            if (downforceCoefficient > 0f)
            {
                float baseDownforce = 500f; // Constant downforce to keep vehicle grounded
                float speedDownforce = downforceCoefficient * speed * speed;
                rb.AddForce(Vector3.down * (baseDownforce + speedDownforce));
            }

            if (antiRollStiffness > 0f)
            {
                ApplyAntiRoll(frontLeftCollider, frontRightCollider, antiRollStiffness);
                ApplyAntiRoll(rearLeftCollider, rearRightCollider, antiRollStiffness);
            }

            // Limit angular velocity to prevent excessive spinning
            if (rb.angularVelocity.magnitude > 2f)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * 2f;
            }

            // Active tilt correction - apply corrective torque when tilted
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            if (upDot < 0.95f && upDot > 0.3f) // Between 18 and 70 degrees tilt
            {
                // Calculate corrective torque to level the vehicle
                Vector3 tiltAxis = Vector3.Cross(transform.up, Vector3.up);
                float tiltAngle = Mathf.Acos(Mathf.Clamp(upDot, -1f, 1f)) * Mathf.Rad2Deg;
                float correctionStrength = 3000f * (1f - upDot); // Stronger correction for more tilt
                rb.AddTorque(tiltAxis.normalized * correctionStrength, ForceMode.Force);
                
                // Also dampen angular velocity when tilted
                rb.angularVelocity *= 0.9f;
            }
            else if (upDot < 0.3f)
            {
                // Severely tilted - strong damping
                rb.angularVelocity *= 0.8f;
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

            if (!rb.isKinematic)
            {
                rb.linearVelocity = GetWorldForward() * (targetSpeed / 3.6f);
                rb.angularVelocity = Vector3.zero;
            }

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

            if (!preferRoadLayerIfPresent)
                return;

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer >= 0)
                groundMask = 1 << roadLayer;
        }

        private void AutoFixWheelAssignments()
        {
            // CRITICAL FIX: Always enforce Z-forward for physics vehicles
            // WheelColliders REQUIRE the vehicle to move along the Z-axis
            modelForwardLocal = Vector3.forward;
            modelUpLocal = Vector3.up;

            if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
                return;
            
            // Just ensure colliders are assigned, but don't try to guess orientation
            // as that was causing the sideways movement bug
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
            // Forces Z-forward
            return transform.forward;
        }

        private Vector3 GetWorldUp()
        {
            return transform.up;
        }

        private Quaternion GetVehicleRotation()
        {
            return transform.rotation;
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
            // Simply look in the desired direction
            // Any visual offset should be handled by rotating the child mesh, not the root rigidbody
            if (desiredForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(desiredForward, Vector3.up);
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
