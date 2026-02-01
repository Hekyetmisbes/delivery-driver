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

        [Header("Wheel Visuals")]
        [SerializeField] private Transform frontLeftWheelVisual;
        [SerializeField] private Transform frontRightWheelVisual;
        [SerializeField] private Transform rearLeftWheelVisual;
        [SerializeField] private Transform rearRightWheelVisual;
        [Tooltip("Auto-assign wheel visuals by searching children")]
        [SerializeField] private bool autoSetupWheelVisuals = true;
        [Tooltip("If a wheel collider shares a mesh, create a visual child at runtime")]
        [SerializeField] private bool autoCreateWheelVisualsIfMissing = true;

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
        [Tooltip("Enable advanced obstacle detection and avoidance")]
        [SerializeField] private bool enableObstacleAvoidance = true;
        [Tooltip("Forward raycast distance (meters)")]
        [SerializeField] private float avoidanceRayDistance = 15f;
        [Tooltip("Side raycast distance for lane checking (meters)")]
        [SerializeField] private float sideRayDistance = 10f;
        [Tooltip("Lateral offset for side raycasts (meters)")]
        [SerializeField] private float sideRayOffset = 1.5f;
        [Tooltip("Safe following distance (meters)")]
        [SerializeField] private float safeFollowingDistance = 8f;
        [Tooltip("Critical braking distance (meters)")]
        [SerializeField] private float criticalBrakingDistance = 4f;
        [Tooltip("Brake strength when obstacle detected (0-1)")]
        [SerializeField] private float avoidanceBrakeStrength = 0.7f;
        [Tooltip("Enable lane changing to avoid obstacles")]
        [SerializeField] private bool enableLaneChange = true;
        [Tooltip("Lane change speed (how fast to move laterally)")]
        [SerializeField] private float laneChangeSpeed = 2f;
        [Tooltip("Layer mask for obstacle detection")]
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Following Distance (Priority 1)")]
        [Tooltip("Following time in seconds (2-3 second rule)")]
        [SerializeField] private float followingTimeSeconds = 2.5f;
        [Tooltip("Minimum gap when following (meters)")]
        [SerializeField] private float minimumFollowingGap = 3f;

        [Header("Lane Change Logic (Priority 1)")]
        [Tooltip("Minimum time between lane changes (seconds)")]
        [SerializeField] private float laneChangeCooldown = 5f;
        [Tooltip("Minimum gap required for safe lane change (meters)")]
        [SerializeField] private float laneChangeMinGap = 10f;
        [Tooltip("Side check distance for lane safety (meters)")]
        [SerializeField] private float laneCheckDistance = 15f;

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
        private float obstacleDistance;
        private bool isPotentiallyOffRoad;
        private float wheelBase;

        // Advanced obstacle avoidance state
        private bool leftLaneClear;
        private bool rightLaneClear;
        private float targetLateralOffset;
        private float currentLateralOffset;

        // Randomized behavior parameters
        private float lateralOffset; // Lateral offset from centerline (for lane variation)
        private float personalityLookAhead; // Randomized lookahead distance
        private float personalityAcceleration; // Randomized acceleration
        private float personalitySteerSpeed; // Randomized steering speed

        // Priority 1: Following Distance System
        private NpcCarAgent vehicleAhead;
        private float distanceToVehicleAhead;
        private float relativeSpeedToVehicleAhead;
        private bool isFollowing;

        // Priority 1: Lane Change System
        private float lastLaneChangeTime;
        private bool isChangingLanes;

        // Priority 1: Turn Signal System
        private TurnSignalController turnSignalController;

        // Priority 2: Personality System
        private DrivingPersonality personality;

        // Priority 2: Predictive Behavior
        private LookaheadData currentLookaheadData;
        private TurnInfo currentTurnInfo;

        // Public accessors
        public RoadSegment CurrentSegment => currentSegment;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f; // km/h
        public bool IsInitialized => isInitialized;

        public float GetGroundClearanceOffset()
        {
            float offset = minGroundClearance;
            bool used = false;

            offset = Mathf.Max(offset, GetWheelRootOffset(frontLeftCollider, ref used));
            offset = Mathf.Max(offset, GetWheelRootOffset(frontRightCollider, ref used));
            offset = Mathf.Max(offset, GetWheelRootOffset(rearLeftCollider, ref used));
            offset = Mathf.Max(offset, GetWheelRootOffset(rearRightCollider, ref used));

            if (!used)
            {
                offset = Mathf.Max(offset, Mathf.Clamp(GetBoundsClearance(), minGroundClearance, maxGroundClearance));
            }

            offset = Mathf.Clamp(offset, minGroundClearance, maxGroundClearance);

            Debug.Log($"[NpcCarAgent] {name} - Ground clearance: {offset:F2}m");
            return offset;
        }

        private float GetWheelClearance(WheelCollider wheel)
        {
            if (wheel == null) return 0f;
            return wheel.radius; // Just radius, suspension will compress
        }

        private float GetWheelRootOffset(WheelCollider wheel, ref bool used)
        {
            if (wheel == null) return 0f;
            Vector3 localPos = transform.InverseTransformPoint(wheel.transform.position);
            float required = wheel.radius - localPos.y;
            used = true;
            if (float.IsNaN(required) || float.IsInfinity(required))
                return 0f;

            if (required < minGroundClearance)
                return minGroundClearance;

            return Mathf.Clamp(required, minGroundClearance, maxGroundClearance);
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
            SetupWheelVisuals();
            SetupTurnSignals(); // Priority 1: Turn Signal System
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
            // Priority 2: Initialize personality system
            InitializePersonality();

            // Random cruise speed for this NPC with larger variation
            targetSpeed = Random.Range(cruiseSpeedRange.x, cruiseSpeedRange.y);

            // Add 30% random variation to make each car more unique
            float variation = targetSpeed * Random.Range(-0.3f, 0.3f);
            targetSpeed += variation;
            targetSpeed = Mathf.Max(20f, targetSpeed); // Minimum 20 km/h

            // Randomize driving personality
            lateralOffset = Random.Range(-2.5f, 2.5f); // Random lane position (-2.5 to 2.5 meters)
            targetLateralOffset = lateralOffset; // Initialize target
            currentLateralOffset = lateralOffset;
            personalityLookAhead = lookAheadDistance * Random.Range(0.6f, 1.4f); // Vary lookahead 60-140%
            personalityAcceleration = acceleration * Random.Range(0.7f, 1.3f); // Vary acceleration 70-130%
            personalitySteerSpeed = steeringSmoothSpeed * Random.Range(0.7f, 1.3f); // Vary steering 70-130%

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} - Speed: {targetSpeed:F1} km/h, Offset: {lateralOffset:F1}m, Lookahead: {personalityLookAhead:F1}m, Personality: {personality.GetDescription()}");
            }

            // Priority 2: Register with traffic communication system
            RegisterWithTrafficSystem();
        }

        private void SetupRigidbody()
        {
            // Randomize physics properties slightly for variation
            rb.mass = Random.Range(1300f, 1700f); // 1300-1700 kg
            rb.linearDamping = Random.Range(0.04f, 0.06f);
            rb.angularDamping = Random.Range(1.0f, 1.5f); // Higher for better stability
            rb.centerOfMass = new Vector3(0, -0.8f, 0); // Lower center of mass prevents rollovers
            rb.sleepThreshold = 0.0f; // Prevent stutter from sleeping rigidbodies

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

            // Priority 2: Predictive behavior
            AnalyzeUpcomingPath();
            DetectUpcomingTurn();
            CheckPredictiveCollisions();

            CheckObstacles();
            UpdateLaneChange();
            ApplySteering();
            ApplyStability();
            ApplyThrottle();
        }

        /// <summary>
        /// Smoothly update lateral offset for lane changes
        /// </summary>
        private void UpdateLaneChange()
        {
            if (!enableLaneChange) return;

            // Smoothly transition to target lateral offset
            lateralOffset = Mathf.Lerp(lateralOffset, targetLateralOffset, Time.fixedDeltaTime * laneChangeSpeed);

            // Priority 1: Check if lane change is complete
            if (isChangingLanes && Mathf.Abs(lateralOffset - targetLateralOffset) < 0.2f)
            {
                isChangingLanes = false;

                // Deactivate turn signals
                if (turnSignalController != null)
                {
                    turnSignalController.DeactivateAll();
                }
            }

            // Priority 1: Check if we should initiate a lane change
            if (ShouldChangeLane() && !isChangingLanes)
            {
                // Determine direction based on which lane is safer
                if (IsLaneSafe(-1)) // Left lane
                {
                    ExecuteLaneChange(-1);
                }
                else if (IsLaneSafe(1)) // Right lane
                {
                    ExecuteLaneChange(1);
                }
            }

            // Reset to original lane if no obstacle and lane change complete
            if (!isObstacleDetected && Mathf.Abs(lateralOffset - targetLateralOffset) < 0.1f)
            {
                // Gradually return to center/original position
                if (Mathf.Abs(currentLateralOffset) > 0.1f)
                {
                    targetLateralOffset = Mathf.Lerp(targetLateralOffset, 0f, Time.fixedDeltaTime * 0.5f);
                }
            }

            currentLateralOffset = lateralOffset;
        }

        private void LateUpdate()
        {
            UpdateWheelVisuals();
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
        /// Advanced obstacle detection with multiple raycasts
        /// </summary>
        private void CheckObstacles()
        {
            isObstacleDetected = false;
            leftLaneClear = true;
            rightLaneClear = true;
            obstacleDistance = float.MaxValue;

            if (!enableObstacleAvoidance) return;

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Priority 1: Detect vehicle ahead for following distance system
            DetectVehicleAhead(rayOrigin, forward, out float distance, out float relativeSpeed);

            // Center raycast - main forward detection
            if (Physics.Raycast(rayOrigin, forward, out RaycastHit centerHit, avoidanceRayDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsVehicleCollider(centerHit.collider))
                {
                    isObstacleDetected = true;
                    obstacleDistance = centerHit.distance;

                    // Decide lane change direction if enabled
                    if (enableLaneChange && obstacleDistance < safeFollowingDistance)
                    {
                        CheckLaneAvailability(rayOrigin, forward, right);
                        DecideLaneChange();
                    }
                }
            }

            // Left raycast - check left lane
            Vector3 leftOrigin = rayOrigin - right * sideRayOffset;
            if (Physics.Raycast(leftOrigin, forward, out RaycastHit leftHit, sideRayDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsVehicleCollider(leftHit.collider))
                {
                    leftLaneClear = false;
                }
            }

            // Right raycast - check right lane
            Vector3 rightOrigin = rayOrigin + right * sideRayOffset;
            if (Physics.Raycast(rightOrigin, forward, out RaycastHit rightHit, sideRayDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsVehicleCollider(rightHit.collider))
                {
                    rightLaneClear = false;
                }
            }
        }

        /// <summary>
        /// Check if collider belongs to a vehicle
        /// </summary>
        private bool IsVehicleCollider(Collider collider)
        {
            if (collider == null) return false;

            // Check self
            if (collider.transform.IsChildOf(transform)) return false;

            return collider.GetComponent<NpcCarAgent>() != null ||
                   collider.GetComponentInParent<NpcCarAgent>() != null ||
                   collider.GetComponent<CarController>() != null ||
                   collider.GetComponentInParent<CarController>() != null ||
                   (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic);
        }

        /// <summary>
        /// Check lane availability with additional side checks
        /// </summary>
        private void CheckLaneAvailability(Vector3 origin, Vector3 forward, Vector3 right)
        {
            // Additional side checks for safer lane changes
            float checkDistance = 5f;

            // Check left-forward diagonal
            Vector3 leftForward = (forward - right * 0.5f).normalized;
            if (Physics.Raycast(origin - right * sideRayOffset, leftForward, checkDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                leftLaneClear = false;
            }

            // Check right-forward diagonal
            Vector3 rightForward = (forward + right * 0.5f).normalized;
            if (Physics.Raycast(origin + right * sideRayOffset, rightForward, checkDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                rightLaneClear = false;
            }
        }

        /// <summary>
        /// Decide which direction to change lane
        /// </summary>
        private void DecideLaneChange()
        {
            // If already changing lane, continue
            if (Mathf.Abs(targetLateralOffset - lateralOffset) > 0.5f)
                return;

            // Try to move to clearer lane
            if (leftLaneClear && !rightLaneClear)
            {
                targetLateralOffset = Mathf.Clamp(lateralOffset - 2f, -3.5f, 3.5f);
            }
            else if (rightLaneClear && !leftLaneClear)
            {
                targetLateralOffset = Mathf.Clamp(lateralOffset + 2f, -3.5f, 3.5f);
            }
            else if (leftLaneClear && rightLaneClear)
            {
                // Both clear, choose randomly
                targetLateralOffset = Random.value > 0.5f ?
                    Mathf.Clamp(lateralOffset - 2f, -3.5f, 3.5f) :
                    Mathf.Clamp(lateralOffset + 2f, -3.5f, 3.5f);
            }
        }

        // ============================================================================
        // PRIORITY 1: Following Distance System (Step 1.1)
        // ============================================================================

        /// <summary>
        /// Detect vehicle ahead and calculate distance and relative speed
        /// </summary>
        private bool DetectVehicleAhead(Vector3 rayOrigin, Vector3 forward, out float distance, out float relativeSpeed)
        {
            distance = float.MaxValue;
            relativeSpeed = 0f;
            vehicleAhead = null;
            isFollowing = false;

            // Raycast ahead to detect vehicles
            if (Physics.Raycast(rayOrigin, forward, out RaycastHit hit, avoidanceRayDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsVehicleCollider(hit.collider))
                {
                    distance = hit.distance;

                    // Try to get the NpcCarAgent component
                    NpcCarAgent otherAgent = hit.collider.GetComponent<NpcCarAgent>();
                    if (otherAgent == null)
                    {
                        otherAgent = hit.collider.GetComponentInParent<NpcCarAgent>();
                    }

                    if (otherAgent != null)
                    {
                        vehicleAhead = otherAgent;

                        // Calculate relative speed
                        float mySpeed = CurrentSpeed / 3.6f; // Convert to m/s
                        float theirSpeed = otherAgent.CurrentSpeed / 3.6f; // Convert to m/s
                        relativeSpeed = mySpeed - theirSpeed; // Positive means we're catching up

                        isFollowing = distance < CalculateSafeFollowingDistance();
                        distanceToVehicleAhead = distance;
                        relativeSpeedToVehicleAhead = relativeSpeed;

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate safe following distance based on speed (2-3 second rule)
        /// </summary>
        private float CalculateSafeFollowingDistance()
        {
            float currentSpeedMs = CurrentSpeed / 3.6f; // Convert km/h to m/s

            // Base formula: speed * following time
            float baseDistance = currentSpeedMs * followingTimeSeconds;

            // Add personality multiplier (some drivers follow closer, others farther)
            float personalityMultiplier = Mathf.Lerp(0.8f, 1.2f, Random.value);

            // Add buffer for reaction time
            float reactionBuffer = 2f; // 2 meters buffer

            // Ensure minimum gap
            return Mathf.Max(minimumFollowingGap, baseDistance * personalityMultiplier + reactionBuffer);
        }

        /// <summary>
        /// Adjust speed to maintain safe following distance
        /// </summary>
        private float AdjustSpeedForTraffic(float desiredSpeed)
        {
            if (!isFollowing || vehicleAhead == null)
            {
                return desiredSpeed;
            }

            float safeDistance = CalculateSafeFollowingDistance();

            if (distanceToVehicleAhead < safeDistance)
            {
                // Too close - need to slow down
                float leadVehicleSpeed = vehicleAhead.CurrentSpeed;

                // Calculate how much slower we should go based on distance
                float distanceRatio = distanceToVehicleAhead / safeDistance;

                // If very close, match or go slower than lead vehicle
                if (distanceRatio < 0.5f)
                {
                    // Very close - go slower than lead vehicle
                    return Mathf.Max(0, leadVehicleSpeed - 5f);
                }
                else
                {
                    // Moderately close - gradually match speed
                    return Mathf.Lerp(leadVehicleSpeed, desiredSpeed, distanceRatio);
                }
            }
            else if (distanceToVehicleAhead < safeDistance * 1.5f)
            {
                // Within comfortable range - match lead vehicle speed
                return vehicleAhead.CurrentSpeed;
            }
            else
            {
                // Far enough - can go desired speed
                return desiredSpeed;
            }
        }

        // ============================================================================
        // PRIORITY 1: Enhanced Lane Change Logic (Step 1.2)
        // ============================================================================

        /// <summary>
        /// Check if adjacent lane is safe for lane change
        /// </summary>
        private bool IsLaneSafe(int laneDirection) // -1 = left, +1 = right
        {
            // Check cooldown
            if (Time.time - lastLaneChangeTime < laneChangeCooldown)
            {
                return false;
            }

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 checkDirection = right * laneDirection;

            // Check forward in adjacent lane
            Vector3 checkOrigin = rayOrigin + checkDirection * sideRayOffset;
            if (Physics.Raycast(checkOrigin, forward, laneCheckDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false; // Vehicle ahead in target lane
            }

            // Check backward in adjacent lane (blind spot)
            if (Physics.Raycast(checkOrigin, -forward, laneChangeMinGap, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false; // Vehicle behind in target lane
            }

            // Check diagonal forward
            Vector3 diagonalForward = (forward + checkDirection * 0.5f).normalized;
            if (Physics.Raycast(rayOrigin, diagonalForward, laneChangeMinGap, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Decide if lane change should be initiated
        /// </summary>
        private bool ShouldChangeLane()
        {
            // Don't change if already changing
            if (isChangingLanes)
            {
                return false;
            }

            // Check cooldown
            if (Time.time - lastLaneChangeTime < laneChangeCooldown)
            {
                return false;
            }

            // Reasons to change lanes:

            // 1. Following too close and can't maintain desired speed
            if (isFollowing && vehicleAhead != null)
            {
                float speedDifference = targetSpeed - vehicleAhead.CurrentSpeed;
                if (speedDifference > 10f) // We want to go at least 10 km/h faster
                {
                    // Try left lane first (passing lane)
                    if (IsLaneSafe(-1))
                    {
                        return true;
                    }
                    // Try right lane
                    if (IsLaneSafe(1))
                    {
                        return true;
                    }
                }
            }

            // 2. Current lane blocked ahead
            if (isObstacleDetected && obstacleDistance < safeFollowingDistance)
            {
                if (IsLaneSafe(-1) || IsLaneSafe(1))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Execute lane change with turn signal
        /// </summary>
        private void ExecuteLaneChange(int direction) // -1 = left, +1 = right
        {
            if (!IsLaneSafe(direction))
            {
                return;
            }

            // Activate turn signal
            if (turnSignalController != null)
            {
                if (direction < 0)
                {
                    turnSignalController.ActivateLeft();
                }
                else
                {
                    turnSignalController.ActivateRight();
                }
            }

            // Set target lateral offset
            targetLateralOffset = Mathf.Clamp(lateralOffset + (direction * 3f), -3.5f, 3.5f);

            // Mark as changing lanes
            isChangingLanes = true;
            lastLaneChangeTime = Time.time;

            // Cancel signal after a delay (handled in UpdateLaneChange)
        }

        // ============================================================================
        // PRIORITY 2: Personality System (Step 5.1)
        // ============================================================================

        /// <summary>
        /// Initialize driving personality for this NPC
        /// </summary>
        private void InitializePersonality()
        {
            personality = DrivingPersonality.CreateRandomVaried();

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} - Personality: {personality.GetDescription()}");
            }
        }

        /// <summary>
        /// Apply personality to behavior parameters
        /// </summary>
        private void ApplyPersonalityToParameters()
        {
            // Speed calculations
            targetSpeed *= personality.speedMultiplier;
            targetSpeed *= personality.speedLimitCompliance;

            // Following distance
            followingTimeSeconds *= personality.followingDistanceMultiplier;

            // Lane changes
            laneChangeCooldown /= personality.laneChangeFrequency;

            // Acceleration/braking
            acceleration *= personality.accelerationAggression;
            braking *= personality.brakingAggression;

            // Reaction delays
            // Could be used in future implementations
        }

        // ============================================================================
        // PRIORITY 2: Predictive Behavior - Multi-Waypoint Lookahead (Step 3.1)
        // ============================================================================

        /// <summary>
        /// Analyze upcoming path for turns, intersections, and obstacles
        /// </summary>
        private void AnalyzeUpcomingPath()
        {
            if (currentSegment == null || currentWaypointIndex >= currentSegment.waypoints.Count)
            {
                currentLookaheadData = new LookaheadData();
                return;
            }

            LookaheadData data = new LookaheadData();
            int waypointCount = 15; // Look ahead 15 waypoints
            int endIndex = Mathf.Min(currentWaypointIndex + waypointCount, currentSegment.waypoints.Count - 1);

            // Analyze waypoints ahead
            float maxAngleChange = 0f;
            bool hasIntersection = false;

            for (int i = currentWaypointIndex; i < endIndex - 1; i++)
            {
                Waypoint current = currentSegment.waypoints[i];
                Waypoint next = currentSegment.waypoints[i + 1];

                // Check for sharp turns
                float angleChange = Vector3.Angle(current.forward, next.forward);
                if (angleChange > maxAngleChange)
                {
                    maxAngleChange = angleChange;
                }
            }

            // Check if approaching end of segment (intersection)
            if (currentSegment.connections != null && currentSegment.connections.Count > 0)
            {
                int distanceToEnd = currentSegment.waypoints.Count - currentWaypointIndex;
                if (distanceToEnd < waypointCount)
                {
                    hasIntersection = true;
                }
            }

            data.hasSharpTurn = maxAngleChange > 15f;
            data.turnSharpness = maxAngleChange;
            data.hasIntersection = hasIntersection;
            data.recommendedSpeed = CalculatePlannedSpeed(data);

            currentLookaheadData = data;
        }

        /// <summary>
        /// Calculate optimal speed based on upcoming path
        /// </summary>
        private float CalculatePlannedSpeed(LookaheadData ahead)
        {
            float speed = targetSpeed;

            // Reduce for sharp turns
            if (ahead.hasSharpTurn)
            {
                float turnFactor = Mathf.Clamp01(1f - (ahead.turnSharpness / 90f));
                float turnSpeed = targetSpeed * Mathf.Lerp(0.5f, 1f, turnFactor);
                speed = Mathf.Min(speed, turnSpeed);
            }

            // Reduce for intersections
            if (ahead.hasIntersection)
            {
                speed = Mathf.Min(speed, targetSpeed * 0.7f);
            }

            return speed;
        }

        // ============================================================================
        // PRIORITY 2: Predictive Behavior - Turn Planning (Step 3.3)
        // ============================================================================

        /// <summary>
        /// Detect upcoming turns and plan deceleration
        /// </summary>
        private void DetectUpcomingTurn()
        {
            if (currentSegment == null || currentWaypointIndex >= currentSegment.waypoints.Count - 3)
            {
                currentTurnInfo = new TurnInfo();
                return;
            }

            TurnInfo turnInfo = new TurnInfo();

            // Look ahead 5-10 waypoints
            int lookAheadCount = 10;
            int endIndex = Mathf.Min(currentWaypointIndex + lookAheadCount, currentSegment.waypoints.Count - 1);

            Vector3 currentPos = transform.position;
            float distanceToTurn = 0f;
            float maxAngle = 0f;

            for (int i = currentWaypointIndex; i < endIndex - 1; i++)
            {
                Waypoint wp1 = currentSegment.waypoints[i];
                Waypoint wp2 = currentSegment.waypoints[i + 1];

                float angle = Vector3.Angle(wp1.forward, wp2.forward);

                if (angle > 15f) // Significant turn
                {
                    turnInfo.isTurn = true;
                    distanceToTurn = Vector3.Distance(currentPos, wp1.position);
                    maxAngle = Mathf.Max(maxAngle, angle);
                }
            }

            if (turnInfo.isTurn)
            {
                turnInfo.distanceToTurn = distanceToTurn;
                turnInfo.turnAngle = maxAngle;
                turnInfo.recommendedSpeed = CalculateTurnSpeed(maxAngle);
            }

            currentTurnInfo = turnInfo;
        }

        /// <summary>
        /// Calculate safe speed for a turn based on angle
        /// </summary>
        private float CalculateTurnSpeed(float turnAngle)
        {
            // Sharp turns require slower speeds
            float turnFactor = Mathf.Clamp01(1f - (turnAngle / 90f));
            float minSpeed = targetSpeed * 0.4f; // At least 40% of target speed
            float maxSpeed = targetSpeed;

            return Mathf.Lerp(minSpeed, maxSpeed, turnFactor);
        }

        /// <summary>
        /// Prepare for upcoming turn by adjusting speed
        /// </summary>
        private float PrepareForTurn(float desiredSpeed)
        {
            if (!currentTurnInfo.isTurn)
            {
                return desiredSpeed;
            }

            // Calculate braking distance needed
            float currentSpeedMs = CurrentSpeed / 3.6f;
            float targetSpeedMs = currentTurnInfo.recommendedSpeed / 3.6f;

            if (currentSpeedMs <= targetSpeedMs)
            {
                return desiredSpeed; // Already slow enough
            }

            // Check if we need to start braking
            float brakingDistance = CalculateBrakingDistance(CurrentSpeed, currentTurnInfo.recommendedSpeed);

            if (currentTurnInfo.distanceToTurn < brakingDistance)
            {
                // Start decelerating
                return currentTurnInfo.recommendedSpeed;
            }

            return desiredSpeed;
        }

        /// <summary>
        /// Calculate braking distance needed to reach target speed
        /// </summary>
        private float CalculateBrakingDistance(float currentSpeed, float targetSpeed)
        {
            float deceleration = 4f; // m/s² - moderate braking
            deceleration *= personality != null ? personality.brakingAggression : 1f;

            float currentSpeedMs = currentSpeed / 3.6f;
            float targetSpeedMs = targetSpeed / 3.6f;

            float speedDiff = currentSpeedMs - targetSpeedMs;
            if (speedDiff <= 0) return 0f;

            // Physics: d = (v1² - v2²) / (2 * a)
            float distance = (currentSpeedMs * currentSpeedMs - targetSpeedMs * targetSpeedMs) / (2 * deceleration);

            // Add reaction distance
            float reactionTime = personality != null ? personality.reactionTime : 0.5f;
            float reactionDistance = currentSpeedMs * reactionTime;

            return distance + reactionDistance + 2f; // +2m safety buffer
        }

        // ============================================================================
        // PRIORITY 2: Predictive Behavior - Trajectory Prediction (Step 3.2)
        // ============================================================================

        /// <summary>
        /// Check for predicted collisions with nearby vehicles
        /// </summary>
        private void CheckPredictiveCollisions()
        {
            if (TrafficCommunicationSystem.Instance == null) return;

            // Get nearby vehicles
            var nearbyVehicles = TrafficCommunicationSystem.Instance.GetNearbyVehicles(transform.position, 30f);

            foreach (var otherVehicle in nearbyVehicles)
            {
                if (otherVehicle == this || otherVehicle == null) continue;

                // Check if we'll collide in next 3 seconds
                if (VehicleTrajectoryPredictor.WillCollide(this, otherVehicle, 3f))
                {
                    // Take evasive action
                    float timeToCollision = VehicleTrajectoryPredictor.CalculateTimeToCollision(this, otherVehicle);

                    if (timeToCollision < 2f)
                    {
                        // Urgent - slow down
                        targetSpeed *= 0.7f;
                    }
                    else if (timeToCollision < 3f && ShouldChangeLane())
                    {
                        // Consider lane change if safe
                        if (IsLaneSafe(-1))
                        {
                            ExecuteLaneChange(-1);
                        }
                        else if (IsLaneSafe(1))
                        {
                            ExecuteLaneChange(1);
                        }
                    }
                }
            }
        }

        // ============================================================================
        // PRIORITY 2: Cooperative Behavior - Vehicle Communication (Step 4.1)
        // ============================================================================

        /// <summary>
        /// Register this vehicle with the traffic communication system
        /// </summary>
        private void RegisterWithTrafficSystem()
        {
            if (TrafficCommunicationSystem.Instance != null)
            {
                TrafficCommunicationSystem.Instance.RegisterVehicle(this);
            }
        }

        /// <summary>
        /// Unregister from traffic communication system
        /// </summary>
        private void OnDestroy()
        {
            if (TrafficCommunicationSystem.Instance != null)
            {
                TrafficCommunicationSystem.Instance.UnregisterVehicle(this);
            }
        }

        // ============================================================================
        // PRIORITY 2: Cooperative Behavior - Merge Assistance (Step 4.2)
        // ============================================================================

        /// <summary>
        /// Detect vehicles trying to merge into our lane
        /// </summary>
        private bool DetectMergingVehicle(out NpcCarAgent mergingVehicle, out float mergePoint)
        {
            mergingVehicle = null;
            mergePoint = 0f;

            if (TrafficCommunicationSystem.Instance == null) return false;

            // Get nearby vehicles
            var nearbyVehicles = TrafficCommunicationSystem.Instance.GetNearbyVehicles(transform.position, 20f);

            foreach (var otherVehicle in nearbyVehicles)
            {
                if (otherVehicle == this || otherVehicle == null) continue;

                // Check if they're changing lanes towards us
                VehicleState state = TrafficCommunicationSystem.Instance.GetVehicleState(otherVehicle);
                if (state != null && state.isChangingLanes)
                {
                    // Check if they're in adjacent lane
                    Vector3 toOther = otherVehicle.transform.position - transform.position;
                    float lateralDistance = Vector3.Dot(toOther, transform.right);

                    if (Mathf.Abs(lateralDistance) > 2f && Mathf.Abs(lateralDistance) < 5f)
                    {
                        // They're in adjacent lane and changing lanes
                        float forwardDistance = Vector3.Dot(toOther, transform.forward);

                        if (forwardDistance > -5f && forwardDistance < 15f)
                        {
                            // They're in merge range
                            mergingVehicle = otherVehicle;
                            mergePoint = forwardDistance;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Assist merging vehicle by creating space
        /// </summary>
        private void AssistMerge()
        {
            if (DetectMergingVehicle(out NpcCarAgent mergingVehicle, out float mergePoint))
            {
                // Create gap by slightly adjusting speed
                if (mergePoint > 0 && mergePoint < 10f)
                {
                    // They're ahead - can speed up to clear gap
                    if (personality != null && personality.riskTolerance > 0.4f)
                    {
                        targetSpeed *= 1.05f;
                    }
                }
                else if (mergePoint < 0 && mergePoint > -5f)
                {
                    // They're behind - slow down to create gap
                    targetSpeed *= 0.95f;
                }
            }
        }

        // ============================================================================
        // Helper Structures
        // ============================================================================

        /// <summary>
        /// Data structure for lookahead analysis
        /// </summary>
        private struct LookaheadData
        {
            public bool hasSharpTurn;
            public float turnSharpness;
            public bool hasIntersection;
            public bool hasTrafficControl;
            public float recommendedSpeed;
        }

        /// <summary>
        /// Data structure for turn information
        /// </summary>
        private struct TurnInfo
        {
            public bool isTurn;
            public float distanceToTurn;
            public float turnAngle;
            public float recommendedSpeed;
        }

        /// <summary>
        /// Apply throttle and braking based on target speed and obstacles
        /// </summary>
        private void ApplyThrottle()
        {
            float currentSpeed = CurrentSpeed;
            float effectiveTargetSpeed = GetTurnLimitedTargetSpeed();

            // Priority 1: Adjust speed for traffic (following distance system)
            effectiveTargetSpeed = AdjustSpeedForTraffic(effectiveTargetSpeed);

            // Priority 2: Prepare for upcoming turns
            effectiveTargetSpeed = PrepareForTurn(effectiveTargetSpeed);

            // Priority 2: Apply lookahead-based speed adjustments
            if (currentLookaheadData.hasSharpTurn || currentLookaheadData.hasIntersection)
            {
                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, currentLookaheadData.recommendedSpeed);
            }

            // Priority 2: Assist merging vehicles
            AssistMerge();

            // Advanced obstacle-based speed adjustment
            if (isObstacleDetected)
            {
                if (obstacleDistance < criticalBrakingDistance)
                {
                    // Emergency braking - very close obstacle
                    effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed * 0.2f, 10f);
                }
                else if (obstacleDistance < safeFollowingDistance)
                {
                    // Maintain safe following distance
                    float distanceFactor = (obstacleDistance - criticalBrakingDistance) /
                                          (safeFollowingDistance - criticalBrakingDistance);
                    effectiveTargetSpeed *= Mathf.Lerp(0.3f, 0.7f, distanceFactor);
                }
                else
                {
                    // Obstacle ahead but at safe distance - slight speed reduction
                    effectiveTargetSpeed *= 0.85f;
                }
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
                float brakeTorque;
                if (isObstacleDetected && obstacleDistance < criticalBrakingDistance)
                {
                    // Emergency braking
                    brakeTorque = braking * 1.2f;
                }
                else if (isObstacleDetected)
                {
                    brakeTorque = braking * avoidanceBrakeStrength;
                }
                else
                {
                    brakeTorque = braking * 0.3f;
                }

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

        private void SetupWheelVisuals()
        {
            if (!autoSetupWheelVisuals) return;

            frontLeftWheelVisual = GetWheelVisualTransform(frontLeftCollider, frontLeftWheelVisual);
            frontRightWheelVisual = GetWheelVisualTransform(frontRightCollider, frontRightWheelVisual);
            rearLeftWheelVisual = GetWheelVisualTransform(rearLeftCollider, rearLeftWheelVisual);
            rearRightWheelVisual = GetWheelVisualTransform(rearRightCollider, rearRightWheelVisual);
        }

        /// <summary>
        /// Priority 1: Setup turn signal controller
        /// </summary>
        private void SetupTurnSignals()
        {
            turnSignalController = GetComponent<TurnSignalController>();
            if (turnSignalController == null)
            {
                turnSignalController = gameObject.AddComponent<TurnSignalController>();
            }
        }

        private Transform GetWheelVisualTransform(WheelCollider wheel, Transform currentVisual)
        {
            if (currentVisual != null) return currentVisual;
            if (wheel == null) return null;

            Transform wheelRoot = wheel.transform;

            // Prefer a child mesh (so we don't rotate the collider transform)
            for (int i = 0; i < wheelRoot.childCount; i++)
            {
                Transform child = wheelRoot.GetChild(i);
                if (child.GetComponent<MeshRenderer>() != null)
                    return child;
            }

            // If collider shares the mesh, create a visual proxy child at runtime
            if (autoCreateWheelVisualsIfMissing)
            {
                MeshRenderer renderer = wheelRoot.GetComponent<MeshRenderer>();
                MeshFilter filter = wheelRoot.GetComponent<MeshFilter>();
                if (renderer != null && filter != null && filter.sharedMesh != null)
                {
                    GameObject visual = new GameObject("WheelVisual");
                    visual.transform.SetParent(wheelRoot, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;

                    MeshFilter visualFilter = visual.AddComponent<MeshFilter>();
                    MeshRenderer visualRenderer = visual.AddComponent<MeshRenderer>();
                    visualFilter.sharedMesh = filter.sharedMesh;
                    visualRenderer.sharedMaterials = renderer.sharedMaterials;

                    renderer.enabled = false;
                    return visual.transform;
                }
            }

            return null;
        }

        private void UpdateWheelVisuals()
        {
            UpdateSingleWheelVisual(frontLeftCollider, frontLeftWheelVisual);
            UpdateSingleWheelVisual(frontRightCollider, frontRightWheelVisual);
            UpdateSingleWheelVisual(rearLeftCollider, rearLeftWheelVisual);
            UpdateSingleWheelVisual(rearRightCollider, rearRightWheelVisual);
        }

        private void UpdateSingleWheelVisual(WheelCollider wheelCollider, Transform wheelVisual)
        {
            if (wheelCollider == null || wheelVisual == null) return;

            wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheelVisual.position = pos;
            wheelVisual.rotation = rot;
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

            // Draw obstacle detection rays
            if (enableObstacleAvoidance)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
                Vector3 forward = transform.forward;
                Vector3 right = transform.right;

                // Center ray
                Gizmos.color = isObstacleDetected ? Color.red : Color.blue;
                Gizmos.DrawLine(rayOrigin, rayOrigin + forward * avoidanceRayDistance);

                // Left ray
                Gizmos.color = leftLaneClear ? Color.green : Color.yellow;
                Vector3 leftOrigin = rayOrigin - right * sideRayOffset;
                Gizmos.DrawLine(leftOrigin, leftOrigin + forward * sideRayDistance);

                // Right ray
                Gizmos.color = rightLaneClear ? Color.green : Color.yellow;
                Vector3 rightOrigin = rayOrigin + right * sideRayOffset;
                Gizmos.DrawLine(rightOrigin, rightOrigin + forward * sideRayDistance);

                // Draw obstacle distance indicator
                if (isObstacleDetected && obstacleDistance < float.MaxValue)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(rayOrigin + forward * obstacleDistance, 0.5f);
                }

                // Priority 1: Draw following distance
                if (isFollowing && vehicleAhead != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, vehicleAhead.transform.position);

                    float safeDistance = CalculateSafeFollowingDistance();
                    Gizmos.color = distanceToVehicleAhead < safeDistance ? Color.red : Color.green;
                    Gizmos.DrawWireSphere(transform.position + forward * safeDistance, 0.5f);
                }

                // Priority 1: Draw lane change indicators
                if (isChangingLanes)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 targetPos = transform.position + transform.right * (targetLateralOffset - lateralOffset);
                    Gizmos.DrawLine(transform.position + Vector3.up, targetPos + Vector3.up);
                    Gizmos.DrawWireSphere(targetPos + Vector3.up, 0.3f);
                }

                // Priority 2: Draw upcoming turn indicator
                if (currentTurnInfo.isTurn)
                {
                    Gizmos.color = Color.magenta;
                    Vector3 turnPos = transform.position + forward * currentTurnInfo.distanceToTurn;
                    Gizmos.DrawWireSphere(turnPos, 1f);
                    Gizmos.DrawLine(transform.position + Vector3.up * 2f, turnPos + Vector3.up * 2f);
                }

                // Priority 2: Draw predicted trajectory
                if (Application.isPlaying)
                {
                    Gizmos.color = Color.cyan;
                    for (int i = 1; i <= 5; i++)
                    {
                        float t = i * 0.5f; // 0.5s intervals
                        Vector3 predictedPos = VehicleTrajectoryPredictor.PredictPositionWithPath(this, t);
                        Gizmos.DrawWireSphere(predictedPos, 0.2f);
                    }
                }
            }
        }
    }
}
