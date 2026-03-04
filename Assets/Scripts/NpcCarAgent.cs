using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Optimization;

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
        [Tooltip("Minimum distance threshold for considering a waypoint passed")]
        [SerializeField] private float waypointPassDistance = 3.0f;
        [Tooltip("Dot threshold for considering a waypoint passed when it falls behind the vehicle")]
        [SerializeField] private float waypointPassedDotThreshold = -0.15f;
        [Tooltip("Enable periodic path reacquire to recover from path drift")]
        [SerializeField] private bool enablePathReacquire = true;
        [Tooltip("Seconds between waypoint reacquire checks")]
        [SerializeField] private float reacquireInterval = 0.25f;
        [Tooltip("Maximum distance allowed for waypoint reacquire")]
        [SerializeField] private float reacquireMaxDistance = 12f;
        [Tooltip("How many waypoints ahead to include in reacquire window")]
        [SerializeField] private int reacquireSearchAhead = 25;
        [Tooltip("How many waypoints behind to include in reacquire window")]
        [SerializeField] private int reacquireSearchBehind = 6;

        [Header("Speed Settings")]
        [Tooltip("Cruise speed range (km/h)")]
        [SerializeField] private Vector2 cruiseSpeedRange = new Vector2(40f, 60f);
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
        [SerializeField] private float avoidanceRayDistance = 20f;
        [Tooltip("Side raycast distance for lane checking (meters)")]
        [SerializeField] private float sideRayDistance = 15f;
        [Tooltip("Lateral offset for side raycasts (meters)")]
        [SerializeField] private float sideRayOffset = 2f;  // Wider for better lane detection
        [Tooltip("Safe following distance (meters)")]
        [SerializeField] private float safeFollowingDistance = 15f;  // Increased for smoother flow
        [Tooltip("Critical braking distance (meters)")]
        [SerializeField] private float criticalBrakingDistance = 4f;  // Reduced - only brake hard when very close
        [Tooltip("Brake strength when obstacle detected (0-1)")]
        [SerializeField] private float avoidanceBrakeStrength = 0.5f;  // Reduced for gentler braking
        [Tooltip("Minimum separation distance (meters)")]
        [SerializeField] private float minimumSeparationDistance = 2f;
        [Tooltip("Separation force strength")]
        [SerializeField] private float separationForceStrength = 5f;
        [Tooltip("Enable lane changing to avoid obstacles")]
        [SerializeField] private bool enableLaneChange = false;
        [Tooltip("Lane change speed (how fast to move laterally)")]
        [SerializeField] private float laneChangeSpeed = 3f;  // Faster lane changes for smoother overtakes
        [Tooltip("Layer mask for obstacle detection")]
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [Tooltip("Radius used for forward vehicle detection casts")]
        [SerializeField] private float vehicleDetectionRadius = 1.1f;
        [Tooltip("Forward awareness angle used to ignore non-threatening side traffic")]
        [SerializeField] private float forwardAwarenessAngle = 70f;
        [Tooltip("Predictive collision lookahead horizon in seconds")]
        [SerializeField] private float predictiveCollisionHorizon = 3.5f;
        [Tooltip("Number of trajectory samples for predictive collision checks")]
        [SerializeField] private int predictiveCollisionSamples = 4;
        [Tooltip("TTC threshold for urgent braking")]
        [SerializeField] private float hardBrakeTimeToCollision = 1.2f;
        [Tooltip("TTC threshold for cautious deceleration")]
        [SerializeField] private float cautionTimeToCollision = 2.8f;
        [Tooltip("Extra clearance buffer added to safe gap calculations")]
        [SerializeField] private float collisionBufferDistance = 2f;

        [Header("Reverse Recovery")]
        [Tooltip("Enable short reverse maneuvers when blocked by obstacles ahead")]
        [SerializeField] private bool enableReverseRecovery = true;
        [Tooltip("How long the vehicle must remain blocked before reversing (seconds)")]
        [SerializeField] private float reverseTriggerDelay = 1.2f;
        [Tooltip("Max absolute speed counted as stuck (km/h)")]
        [SerializeField] private float reverseStuckSpeedThreshold = 1.5f;
        [Tooltip("How long reverse maneuver lasts (seconds)")]
        [SerializeField] private float reverseDuration = 1.0f;
        [Tooltip("Cooldown before another reverse attempt (seconds)")]
        [SerializeField] private float reverseCooldown = 2.0f;
        [Tooltip("Target reverse speed (km/h)")]
        [SerializeField] private float reverseTargetSpeed = 8f;
        [Tooltip("Rear obstacle check distance before reversing (meters)")]
        [SerializeField] private float reverseRearCheckDistance = 5f;

        [Header("Following Distance (Priority 1)")]
        [Tooltip("Following time in seconds (2-3 second rule)")]
        [SerializeField] private float followingTimeSeconds = 2.5f;
        [Tooltip("Minimum gap when following (meters)")]
        [SerializeField] private float minimumFollowingGap = 3f;
        [Tooltip("Minimum bumper gap at very low speed / stop-go traffic (meters)")]
        [SerializeField] private float standstillFollowingGap = 5.5f;
        [Tooltip("Extra following gap multiplier at low speed to avoid bumper-to-bumper clumping")]
        [SerializeField] private float lowSpeedFollowingMultiplier = 1.35f;
        [Tooltip("Minimum speed when constrained by traffic ahead (km/h)")]
        [SerializeField] private float trafficConstrainedMinSpeed = 2f;
        [Tooltip("Minimum cruising speed when road ahead is clear (km/h)")]
        [SerializeField] private float freeFlowMinSpeed = 15f;

        [Header("Lane Change Logic (Priority 1)")]
        [Tooltip("Minimum time between lane changes (seconds)")]
        [SerializeField] private float laneChangeCooldown = 3f;  // Reduced for more frequent overtakes
        [Tooltip("Minimum gap required for safe lane change (meters)")]
        [SerializeField] private float laneChangeMinGap = 8f;  // Reduced for easier overtakes
        [Tooltip("Side check distance for lane safety (meters)")]
        [SerializeField] private float laneCheckDistance = 20f;  // Increased for safer overtakes
        [Tooltip("Speed difference threshold to trigger overtake (km/h)")]
        [SerializeField] private float overtakeSpeedThreshold = 5f;  // Overtake if 5+ km/h faster
        [Tooltip("Lateral distance used for one lane change step (meters)")]
        [SerializeField] private float laneChangeOffset = 1.5f;
        [Tooltip("Clamp for allowed lateral lane offset (meters)")]
        [SerializeField] private float maxLateralLaneOffset = 4.5f;

        [Header("Lane Centering")]
        [Tooltip("Keep NPCs on fixed lane centers instead of centerline wandering")]
        [SerializeField] private bool useFixedLaneCenters = true;
        [Tooltip("Lane center distance from road centerline (meters)")]
        [SerializeField] private float laneCenterOffset = 1.5f;
        [Tooltip("Choose random left/right lane at spawn")]
        [SerializeField] private bool randomizeInitialLane = false;

        [Header("Environmental Awareness (Priority 3)")]
        [Tooltip("Enable weather-based behavior adjustments")]
        [SerializeField] private bool enableWeatherEffects = true;
        [Tooltip("Enable time of day behavior adjustments")]
        [SerializeField] private bool enableTimeOfDayEffects = true;
        [Tooltip("Enable type-specific obstacle responses")]
        [SerializeField] private bool enableObstacleClassification = true;

        [Header("Stability")]
        [Tooltip("Enable stability helpers (turn speed limit, downforce, anti-roll)")]
        [SerializeField] private bool enableStability = true;
        [Tooltip("Max lateral acceleration (m/s^2) used to limit speed in turns")]
        [SerializeField] private float maxLateralAcceleration = 6f;
        [Tooltip("Minimum speed factor at full steering lock")]
        [SerializeField] private float minTurnSpeedFactor = 0.35f;
        [Tooltip("Downforce coefficient (N per (m/s)^2) - reduced to prevent suspension collapse")]
        [SerializeField] private float downforceCoefficient = 20f; // Reduced from 100f
        [Tooltip("Constant downforce baseline (N)")]
        [SerializeField] private float baseDownforce = 120f;
        [Tooltip("Clamp extra downforce as a fraction of vehicle weight (0.3 = max 30% of weight)")]
        [SerializeField] private float maxDownforceWeightFraction = 0.35f;
        [Tooltip("Apply downforce along vehicle local down instead of world down to reduce slope penetration")]
        [SerializeField] private bool useLocalDownforceDirection = true;
        [Tooltip("Anti-roll stiffness - higher prevents rollovers")]
        [SerializeField] private float antiRollStiffness = 12000f;

        [Header("Physics")]
        [Tooltip("Rigidbody interpolation mode")]
        [SerializeField] private RigidbodyInterpolation rbInterpolation = RigidbodyInterpolation.Interpolate;
        [Tooltip("Rigidbody collision detection mode")]
        [SerializeField] private CollisionDetectionMode rbCollisionDetection = CollisionDetectionMode.Discrete;
        [Tooltip("Solver iterations (higher = more stable)")]
        [SerializeField] private int solverIterations = 6;
        [Tooltip("Solver velocity iterations (higher = more stable)")]
        [SerializeField] private int solverVelocityIterations = 4;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool logPathChanges = false;
        [SerializeField] private bool logCollisions = false;
        [SerializeField] private bool logOvertakes = false;  // Log overtaking behavior

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
        [Tooltip("Normalize wheel collider heights at runtime to keep all wheels on same contact plane")]
        [SerializeField] private bool autoNormalizeWheelColliderHeights = true;

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

        // Performance optimization
        private static int nextNpcId = 0;
        private int npcId;
        private int fixedUpdateCounter;
        private const int ObstacleCheckInterval = 3;    // Check obstacles every 3 FixedUpdates
        private const int OffRoadCheckInterval = 10;     // Check off-road every 10 FixedUpdates
        private const int NearbyVehicleCheckInterval = 2;  // 25Hz at 50Hz physics
        private const int WeatherCheckInterval = 30;       // ~1.67Hz - weather changes slowly
        private const int StabilityCheckInterval = 3;      // Anti-roll every 3 FixedUpdates
        private float cachedWeight;                        // rb.mass * gravity, cached on init

        // Collision handling
        private static Collider[] sharedOverlapBuffer = new Collider[32];
        private static RaycastHit[] sharedSphereCastBuffer = new RaycastHit[16];
        private bool isColliding;
        private float collisionTimer;
        private Vector3 separationDirection;
        private List<Collider> nearbyVehicles = new List<Collider>();
        private readonly List<NpcCarAgent> nearbyVehicleBuffer = new List<NpcCarAgent>(24);

        // Advanced obstacle avoidance state
        private bool leftLaneClear;
        private bool rightLaneClear;
        private float targetLateralOffset;
        private float currentLateralOffset;

        // Randomized behavior parameters
        private float lateralOffset; // Lateral offset from centerline (for lane variation)
        private float cruisingLaneOffset;
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
        private bool isOvertaking;  // Track if currently overtaking

        // Priority 1: Turn Signal System
        private TurnSignalController turnSignalController;

        private float nextReacquireTime = 0f;

        // Priority 2: Personality System
        private DrivingPersonality personality;

        // Priority 2: Predictive Behavior
        private LookaheadData currentLookaheadData;
        private TurnInfo currentTurnInfo;

        // Priority 3: Environmental Awareness
        private float weatherSpeedMultiplier = 1f;
        private float timeOfDaySpeedMultiplier = 1f;
        private float environmentalFollowingDistanceMultiplier = 1f;
        private float predictiveSpeedLimiter = 1f;
        private bool imminentCollisionRisk;
        private bool isReversing;
        private float reverseTimer;
        private float reverseCooldownTimer;
        private float blockedTimer;
        private bool rearObstacleDetected;
        private float rearObstacleDistance;

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

            // Debug.Log($"[NpcCarAgent] {name} - Ground clearance: {offset:F2}m");
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
            // Assign unique NPC ID for update throttling
            npcId = nextNpcId++;

            rb = GetComponent<Rigidbody>();
            cachedWeight = rb.mass * Mathf.Abs(Physics.gravity.y);
            SetupRigidbody();
            AutoFixWheelAssignments();
            if (autoNormalizeWheelColliderHeights)
            {
                NormalizeWheelColliderHeightsRuntime();
            }
            ConfigureSuspension(); // New method to tune physics
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
            if (useFixedLaneCenters)
            {
                float laneSign = randomizeInitialLane && Random.value < 0.5f ? -1f : 1f;
                cruisingLaneOffset = laneSign * Mathf.Max(0.1f, laneCenterOffset);
                lateralOffset = cruisingLaneOffset;
            }
            else
            {
                cruisingLaneOffset = 0f;
                lateralOffset = 0f; // Follow lane center from road graph directly
            }
            targetLateralOffset = lateralOffset; // Initialize target
            currentLateralOffset = lateralOffset;
            personalityLookAhead = lookAheadDistance * Random.Range(0.9f, 1.1f); // Keep path progression stable
            personalityAcceleration = acceleration * Random.Range(0.9f, 1.1f);
            personalitySteerSpeed = steeringSmoothSpeed * Random.Range(0.9f, 1.1f);

            // Initialize overtaking flags
            isOvertaking = false;
            isChangingLanes = false;

            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} - Speed: {targetSpeed:F1} km/h, Offset: {lateralOffset:F1}m, Lookahead: {personalityLookAhead:F1}m, Personality: {personality.GetDescription()}");
            }

            // Priority 2: Register with shared traffic systems
            RegisterWithTrafficSystem();
            RegisterWithOptimizationSystem();
        }

        private void OnEnable()
        {
            RegisterWithTrafficSystem();
            RegisterWithOptimizationSystem();
        }

        private void OnDisable()
        {
            if (TrafficCommunicationSystem.Instance != null)
            {
                TrafficCommunicationSystem.Instance.UnregisterVehicle(this);
            }

            if (TrafficSimulationOptimizer.Instance != null)
            {
                TrafficSimulationOptimizer.Instance.UnregisterNPC(this);
            }
        }

        private void SetupRigidbody()
        {
            // Randomize physics properties slightly for variation
            rb.mass = Random.Range(1300f, 1700f); // 1300-1700 kg
            rb.linearDamping = Random.Range(0.04f, 0.06f);
            rb.angularDamping = Random.Range(1.0f, 1.5f); // Higher for better stability
            rb.centerOfMass = new Vector3(0, -0.8f, 0); // Lower center of mass prevents rollovers
            rb.sleepThreshold = 0.005f; // Allow very slow NPCs to sleep, saving physics CPU

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

            // Always log initialization for debugging
            // Debug.Log($"[NpcCarAgent] {name} initialized on segment '{segment.name}' (waypoints: {segment.waypoints.Count}) at index {waypointIndex}");

            if (segment.waypoints.Count == 0)
            {
                // Debug.LogError($"[NpcCarAgent] {name} - Segment '{segment.name}' has ZERO waypoints!");
            }
        }

        /// <summary>
        /// Initialize on random road position
        /// </summary>
        public void InitializeRandom(RoadGraphBuilder builder)
        {
            if (builder == null || builder.RoadGraph == null)
            {
                // Debug.LogError($"[NpcCarAgent] {name} - RoadGraphBuilder or RoadGraph is null!");
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
            // Performance optimization: Distance-based update throttling (single check)
            if (TrafficSimulationOptimizer.Instance != null &&
                !TrafficSimulationOptimizer.Instance.ShouldNPCUpdate(this))
            {
                return;
            }

            if (!isInitialized)
            {
                if (Time.frameCount % 120 == 0) // Log every 2 seconds
                {
                    // Debug.LogWarning($"[NpcCarAgent] {name} - Not initialized!");
                }
                return;
            }

            if (currentSegment == null)
            {
                if (Time.frameCount % 120 == 0) // Log every 2 seconds
                {
                    // Debug.LogWarning($"[NpcCarAgent] {name} - No current segment!");
                }
                return;
            }

            if (currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
            {
                if (Time.frameCount % 120 == 0) // Log every 2 seconds
                {
                    // Debug.LogWarning($"[NpcCarAgent] {name} - Segment '{currentSegment.name}' has no waypoints!");
                }
                return;
            }

            predictiveSpeedLimiter = 1f;
            imminentCollisionRisk = false;
            fixedUpdateCounter++;

            // FIX: Periodically reacquire a valid nearby forward waypoint before progressing path.
            ReacquireWaypointIfNeeded();
            UpdatePath();

            // Throttle expensive checks: off-road every 10 frames
            if (fixedUpdateCounter % OffRoadCheckInterval == 0)
            {
                UpdateOffRoadStatus();
            }

            // Priority 3: Environmental awareness (throttled - weather changes slowly)
            if ((fixedUpdateCounter + npcId) % WeatherCheckInterval == 0)
            {
                UpdateWeatherEffects();
                UpdateTimeOfDayEffects();
            }

            // Priority 2: Predictive behavior (throttled with obstacles)
            if ((fixedUpdateCounter + npcId) % ObstacleCheckInterval == 0)
            {
                AnalyzeUpcomingPath();
                DetectUpcomingTurn();
                CheckPredictiveCollisions();
                CheckObstacles();
                CheckRearObstacles();
            }

            UpdateReverseRecoveryState();

            // Throttle nearby vehicle checks to 25Hz (every 2 physics frames)
            if ((fixedUpdateCounter + npcId) % NearbyVehicleCheckInterval == 0)
            {
                CheckNearbyVehicles();
            }
            ApplySeparationForce();
            UpdateLaneChange();
            ApplySteering();
            ApplyStability();
            ApplyThrottle();
        }

        /// <summary>
        /// Check for nearby vehicles using physics overlap
        /// </summary>
        private void CheckNearbyVehicles()
        {
            nearbyVehicles.Clear();
            isColliding = false;

            float dynamicCheckRadius = Mathf.Lerp(3f, 6f, Mathf.Clamp01(CurrentSpeed / 80f));
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, dynamicCheckRadius, sharedOverlapBuffer, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            Vector3 selfReference = GetReferencePosition();
            float immediateCollisionDistanceSqr = 1.2f * 1.2f;
            float minimumSeparationDistanceSqr = minimumSeparationDistance * minimumSeparationDistance;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = sharedOverlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform))
                    continue;

                if (IsVehicleCollider(col))
                {
                    nearbyVehicles.Add(col);

                    Vector3 closestPoint = col.ClosestPoint(selfReference);
                    Vector3 toOther = closestPoint - selfReference;
                    float distanceSqr = toOther.sqrMagnitude;

                    Rigidbody otherRb = col.attachedRigidbody;
                    Vector3 otherVel = (otherRb != null && !otherRb.isKinematic) ? otherRb.linearVelocity : Vector3.zero;
                    float closingSpeed = 0f;
                    if (distanceSqr > 0.001f)
                    {
                        float invDistance = 1f / Mathf.Sqrt(distanceSqr);
                        closingSpeed = Vector3.Dot(rb.linearVelocity - otherVel, toOther * invDistance);
                    }

                    if (distanceSqr < immediateCollisionDistanceSqr || (distanceSqr < minimumSeparationDistanceSqr && closingSpeed > 1.5f))
                    {
                        isColliding = true;
                    }
                }
            }

            if (isColliding)
            {
                collisionTimer += Time.fixedDeltaTime;
            }
            else
            {
                // Reset collision timer quickly when separated
                collisionTimer = Mathf.Max(0, collisionTimer - Time.fixedDeltaTime * 3f);
            }
        }

        /// <summary>
        /// Apply separation force to push away from nearby vehicles
        /// </summary>
        private void ApplySeparationForce()
        {
            if (nearbyVehicles.Count == 0) return;

            Vector3 separationForce = Vector3.zero;
            int separationCount = 0;
            Vector3 selfReference = GetReferencePosition();
            float minimumSeparationDistanceSqr = minimumSeparationDistance * minimumSeparationDistance;

            foreach (Collider nearbyVehicle in nearbyVehicles)
            {
                if (nearbyVehicle == null) continue;

                Vector3 closestPoint = nearbyVehicle.ClosestPoint(selfReference);
                Vector3 toOther = closestPoint - selfReference;
                float distanceSqr = toOther.sqrMagnitude;

                // Skip if too far
                if (distanceSqr > minimumSeparationDistanceSqr) continue;

                // Apply gentle separation force when close
                if (distanceSqr > 0.0025f)
                {
                    float distance = Mathf.Sqrt(distanceSqr);
                    float invDistance = 1f / distance;

                    Rigidbody otherRb = nearbyVehicle.attachedRigidbody;
                    Vector3 otherVel = (otherRb != null && !otherRb.isKinematic) ? otherRb.linearVelocity : Vector3.zero;
                    float closingSpeed = Vector3.Dot(rb.linearVelocity - otherVel, toOther * invDistance);

                    float proximityFactor = 1f - (distance / minimumSeparationDistance);
                    float closingFactor = Mathf.Clamp01(closingSpeed / 8f);
                    float strength = proximityFactor * (1f + closingFactor);
                    Vector3 repulsion = -(toOther * invDistance) * strength;
                    separationForce += repulsion;
                    separationCount++;
                }
            }

            if (separationCount > 0)
            {
                separationForce /= separationCount;
                separationDirection = separationForce.normalized;

                // Apply gentle lateral force to separate while maintaining forward motion
                Vector3 lateralForce = Vector3.ProjectOnPlane(separationForce * separationForceStrength, transform.forward);
                rb.AddForce(lateralForce, ForceMode.Acceleration);

                // Keep moving forward even when separating
                // Ensure minimum forward velocity to prevent getting stuck
                Vector3 forwardVel = Vector3.Project(rb.linearVelocity, transform.forward);
                if (forwardVel.magnitude < 4f && !isColliding)
                {
                    rb.AddForce(transform.forward * 1.5f, ForceMode.Acceleration);
                }
            }
        }

        /// <summary>
        /// Smoothly update lateral offset for lane changes
        /// </summary>
        private void UpdateLaneChange()
        {
            if (!enableLaneChange || isReversing) return;

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
                if (IsLaneSafe(-1)) // Left lane (passing lane)
                {
                    ExecuteLaneChange(-1);
                }
                else if (IsLaneSafe(1)) // Right lane
                {
                    ExecuteLaneChange(1);
                }
            }

            // REALISTIC TRAFFIC: Return to RIGHT lane after overtaking
            if (isOvertaking && !isObstacleDetected && !isFollowing)
            {
                // Check if we've completed the overtake
                if (Mathf.Abs(lateralOffset - targetLateralOffset) < 0.3f)
                {
                    // Check if right lane is clear to return
                    if (lateralOffset < -0.5f && IsLaneSafe(1)) // We're in left lane, return to right
                    {
                        if (logOvertakes)
                        {
                            Debug.Log($"[NpcCarAgent] {name} completing overtake (returning to right lane)");
                        }

                        targetLateralOffset = cruisingLaneOffset; // Return to cruise lane center
                        isOvertaking = false;

                        // Signal right turn
                        if (turnSignalController != null)
                        {
                            turnSignalController.ActivateRight();
                        }
                    }
                }
            }

            // Gradually return to right lane when clear (default driving lane)
            if (!isObstacleDetected && !isFollowing && !isOvertaking && Mathf.Abs(lateralOffset - targetLateralOffset) < 0.1f)
            {
                if (lateralOffset < (cruisingLaneOffset - 0.5f)) // In left of cruise lane, return
                {
                    targetLateralOffset = Mathf.Lerp(targetLateralOffset, cruisingLaneOffset, Time.fixedDeltaTime * 0.3f);
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
            // FIX: Guard path state to avoid invalid waypoint access.
            if (currentSegment == null || currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
                return;

            int waypointCount = currentSegment.waypoints.Count;
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypointCount - 1);

            Vector3 flatForward = GetWorldForward();
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                flatForward.Normalize();
            }
            else
            {
                flatForward = Vector3.forward;
            }

            float dynamicPassDist = Mathf.Max(waypointPassDistance, personalityLookAhead * 0.25f);

            // FIX: Advance waypoint both when close enough and when the waypoint is already behind us.
            while (currentWaypointIndex < waypointCount)
            {
                Waypoint currentWp = currentSegment.waypoints[currentWaypointIndex];
                Vector3 toWp = currentWp.position - transform.position;
                toWp.y = 0f;

                float distanceToWaypoint = toWp.magnitude;
                bool isCloseEnough = distanceToWaypoint <= dynamicPassDist;

                bool isBehind = false;
                if (toWp.sqrMagnitude > 0.0001f)
                {
                    float dot = Vector3.Dot(flatForward, toWp.normalized);
                    isBehind = dot < waypointPassedDotThreshold;
                }

                if (!isCloseEnough && !isBehind)
                {
                    break;
                }

                currentWaypointIndex++;

                if (currentWaypointIndex >= waypointCount)
                {
                    HandleEndOfSegment();
                    break;
                }
            }
        }

        // FIX: Reacquire nearby forward waypoint to recover from temporary path drift.
        private void ReacquireWaypointIfNeeded()
        {
            if (!enablePathReacquire)
                return;

            if (Time.time < nextReacquireTime)
                return;

            nextReacquireTime = Time.time + reacquireInterval;

            if (currentSegment == null || currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
                return;

            int waypointCount = currentSegment.waypoints.Count;
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypointCount - 1);

            Vector3 pos = GetReferencePosition();
            Vector3 flatForward = GetWorldForward();
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                flatForward.Normalize();
            }
            else
            {
                flatForward = Vector3.forward;
            }

            int start = Mathf.Clamp(currentWaypointIndex - reacquireSearchBehind, 0, waypointCount - 1);
            int end = Mathf.Clamp(currentWaypointIndex + reacquireSearchAhead, 0, waypointCount - 1);
            if (end < start)
            {
                end = start;
            }

            float bestScore = float.NegativeInfinity;
            float bestDist = float.MaxValue;
            int bestIdx = currentWaypointIndex;

            for (int i = start; i <= end; i++)
            {
                Vector3 to = currentSegment.waypoints[i].position - pos;
                to.y = 0f;

                float dist = to.magnitude;
                float dot = 1f;
                if (dist > 0.0001f)
                {
                    dot = Vector3.Dot(flatForward, to / dist);
                }

                float score = dot * 2f - dist * 0.08f;
                if (dot < -0.2f)
                {
                    score -= 100f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            if (bestDist <= reacquireMaxDistance)
            {
                currentWaypointIndex = Mathf.Max(bestIdx, currentWaypointIndex);
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
                RoadConnection connection = SelectBestConnection(currentSegment.connections);
                if (connection == null || connection.toSegment == null)
                {
                    if (TryGetSequentialNextSegment(out RoadSegment fallbackSegment))
                    {
                        currentSegment = fallbackSegment;
                        currentWaypointIndex = 0;
                    }
                    else
                    {
                        currentWaypointIndex = 0;
                    }
                    return;
                }

                currentSegment = connection.toSegment;
                currentWaypointIndex = 0;

                if (logPathChanges)
                {
                    Debug.Log($"[NpcCarAgent] {name} transitioning to segment '{currentSegment.name}' at waypoint {currentWaypointIndex}");
                }
            }
            else
            {
                // No direct connection: continue with the next segment's first waypoint.
                if (TryGetSequentialNextSegment(out RoadSegment nextSegment))
                {
                    currentSegment = nextSegment;
                    currentWaypointIndex = 0;

                    if (logPathChanges)
                    {
                        Debug.Log($"[NpcCarAgent] {name} continuing to next segment '{currentSegment.name}' at waypoint 0");
                    }
                }
                else
                {
                    currentWaypointIndex = 0;
                    if (logPathChanges)
                    {
                        Debug.Log($"[NpcCarAgent] {name} looping on segment '{currentSegment.name}' (no outgoing connection)");
                    }
                }
            }
        }

        private bool TryGetSequentialNextSegment(out RoadSegment nextSegment)
        {
            nextSegment = null;

            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
                return false;

            List<RoadSegment> segments = roadGraphBuilder.RoadGraph.roadSegments;
            if (segments == null || segments.Count == 0 || currentSegment == null)
                return false;

            int currentIndex = segments.IndexOf(currentSegment);
            if (currentIndex < 0)
                return false;

            for (int offset = 1; offset <= segments.Count; offset++)
            {
                int candidateIndex = (currentIndex + offset) % segments.Count;
                RoadSegment candidate = segments[candidateIndex];
                if (candidate != null && candidate.waypoints != null && candidate.waypoints.Count > 0)
                {
                    nextSegment = candidate;
                    return true;
                }
            }

            return false;
        }

        private RoadConnection SelectBestConnection(List<RoadConnection> connections)
        {
            if (connections == null || connections.Count == 0)
            {
                return null;
            }

            Vector3 currentForward = GetWorldForward();
            currentForward.y = 0f;
            if (currentForward.sqrMagnitude < 0.0001f)
            {
                currentForward = Vector3.forward;
            }
            else
            {
                currentForward.Normalize();
            }

            RoadConnection best = connections[0];
            float bestDot = -999f;

            for (int i = 0; i < connections.Count; i++)
            {
                RoadConnection connection = connections[i];
                if (connection == null || connection.toSegment == null || connection.toSegment.waypoints == null || connection.toSegment.waypoints.Count == 0)
                {
                    continue;
                }

                int toIndex = Mathf.Clamp(connection.toWaypointIndex, 0, connection.toSegment.waypoints.Count - 1);
                Vector3 candidateForward = connection.toSegment.waypoints[toIndex].forward;
                candidateForward.y = 0f;

                if (candidateForward.sqrMagnitude < 0.0001f && toIndex < connection.toSegment.waypoints.Count - 1)
                {
                    candidateForward = connection.toSegment.waypoints[toIndex + 1].position - connection.toSegment.waypoints[toIndex].position;
                    candidateForward.y = 0f;
                }

                if (candidateForward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                candidateForward.Normalize();
                float dot = Vector3.Dot(currentForward, candidateForward);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = connection;
                }
            }

            return best;
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
            if (currentSegment == null || currentSegment.waypoints == null ||
                currentWaypointIndex < 0 || currentWaypointIndex >= currentSegment.waypoints.Count)
            {
                return transform.position + GetWorldForward() * personalityLookAhead;
            }

            int waypointCount = currentSegment.waypoints.Count;
            int startIndex = Mathf.Clamp(currentWaypointIndex, 0, waypointCount - 1);
            Vector3 carPos = transform.position;

            // FIX: Skip clearly behind waypoints so lookahead starts from a forward anchor.
            Vector3 flatForward = GetWorldForward();
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                flatForward.Normalize();
            }
            else
            {
                flatForward = Vector3.forward;
            }

            for (int i = startIndex; i < waypointCount; i++)
            {
                Vector3 toWaypoint = currentSegment.waypoints[i].position - carPos;
                toWaypoint.y = 0f;

                if (toWaypoint.sqrMagnitude < 0.0001f)
                {
                    startIndex = i;
                    break;
                }

                float dotProduct = Vector3.Dot(flatForward, toWaypoint.normalized);
                if (dotProduct > 0.3f)
                {
                    startIndex = i;
                    break;
                }
            }

            // FIX: Traverse path polyline distance instead of car-to-waypoint distances.
            float remaining = Mathf.Max(1f, personalityLookAhead);
            Vector3 prev = currentSegment.waypoints[startIndex].position;
            Vector3 targetPoint = prev;
            Vector3 targetTangent = flatForward;
            bool targetFound = false;

            for (int i = startIndex; i < waypointCount - 1; i++)
            {
                Vector3 next = currentSegment.waypoints[i + 1].position;
                Vector3 segment = next - prev;
                float segmentLength = segment.magnitude;

                if (segmentLength > 0.0001f)
                {
                    Vector3 tangent = segment / segmentLength;
                    if (remaining <= segmentLength)
                    {
                        float t = remaining / segmentLength;
                        targetPoint = Vector3.Lerp(prev, next, t);
                        targetTangent = tangent;
                        targetFound = true;
                        break;
                    }

                    remaining -= segmentLength;
                    targetTangent = tangent;
                }

                prev = next;
            }

            if (!targetFound)
            {
                targetPoint = currentSegment.waypoints[waypointCount - 1].position;

                if (waypointCount >= 2)
                {
                    Vector3 lastSegment = currentSegment.waypoints[waypointCount - 1].position - currentSegment.waypoints[waypointCount - 2].position;
                    if (lastSegment.sqrMagnitude > 0.0001f)
                    {
                        targetTangent = lastSegment.normalized;
                    }
                }
            }

            if (Mathf.Abs(lateralOffset) > 0.1f)
            {
                // FIX: Preserve existing lane offset sign semantics (Cross(tangent, up)).
                Vector3 side = Vector3.Cross(targetTangent, Vector3.up);
                if (side.sqrMagnitude > 0.0001f)
                {
                    side.Normalize();
                    targetPoint += side * lateralOffset;
                }
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

        private void CheckRearObstacles()
        {
            rearObstacleDetected = false;
            rearObstacleDistance = float.MaxValue;

            if (!enableReverseRecovery) return;

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 backward = -GetWorldForward();
            float checkRadius = Mathf.Max(0.3f, vehicleDetectionRadius * 0.9f);

            if (Physics.SphereCast(rayOrigin, checkRadius, backward, out RaycastHit rearHit, reverseRearCheckDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (rearHit.collider != null && !rearHit.collider.transform.IsChildOf(transform))
                {
                    rearObstacleDetected = true;
                    rearObstacleDistance = rearHit.distance;
                }
            }
        }

        private void UpdateReverseRecoveryState()
        {
            if (!enableReverseRecovery || rb == null)
            {
                isReversing = false;
                blockedTimer = 0f;
                return;
            }

            if (reverseCooldownTimer > 0f)
            {
                reverseCooldownTimer -= Time.fixedDeltaTime;
            }

            if (isReversing)
            {
                reverseTimer -= Time.fixedDeltaTime;
                bool rearTooClose = rearObstacleDetected && rearObstacleDistance < 1.2f;
                if (reverseTimer <= 0f || rearTooClose)
                {
                    StopReverse();
                }
                return;
            }

            bool frontBlocked = isObstacleDetected && obstacleDistance < Mathf.Max(criticalBrakingDistance + 1f, safeFollowingDistance * 0.45f);
            if (isColliding) frontBlocked = true;

            float signedSpeed = Vector3.Dot(rb.linearVelocity, GetWorldForward()) * 3.6f;
            bool nearlyStopped = Mathf.Abs(signedSpeed) <= Mathf.Max(0.1f, reverseStuckSpeedThreshold);

            if (frontBlocked && nearlyStopped)
            {
                blockedTimer += Time.fixedDeltaTime;
            }
            else
            {
                blockedTimer = Mathf.Max(0f, blockedTimer - Time.fixedDeltaTime * 1.5f);
            }

            if (reverseCooldownTimer > 0f || blockedTimer < reverseTriggerDelay)
                return;

            bool rearClearEnough = !rearObstacleDetected || rearObstacleDistance > Mathf.Max(1.5f, reverseRearCheckDistance * 0.7f);
            if (rearClearEnough)
            {
                StartReverse();
            }
        }

        private void StartReverse()
        {
            isReversing = true;
            reverseTimer = Mathf.Max(0.2f, reverseDuration);
            blockedTimer = 0f;

            if (turnSignalController != null)
            {
                turnSignalController.DeactivateAll();
            }
        }

        private void StopReverse()
        {
            isReversing = false;
            reverseTimer = 0f;
            reverseCooldownTimer = Mathf.Max(0f, reverseCooldown);
            SetMotorTorque(0f);
            SetBrakeTorque(0f);
        }

        /// <summary>
        /// Check if collider belongs to a vehicle
        /// </summary>
        private bool IsVehicleCollider(Collider collider)
        {
            if (collider == null) return false;

            // Check self
            if (collider.transform.IsChildOf(transform)) return false;

            return HasVehicleController(collider) ||
                   (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic);
        }

        // ============================================================================
        // PRIORITY 3: Obstacle Classification (Step 6.3)
        // ============================================================================

        /// <summary>
        /// Classify obstacle type for appropriate response
        /// </summary>
        private ObstacleType ClassifyObstacle(Collider obstacle)
        {
            if (obstacle == null) return ObstacleType.Unknown;

            // Check for vehicles
            if (SafeCompareTag(obstacle.gameObject, "NPC") || SafeCompareTag(obstacle.gameObject, "Player"))
                return ObstacleType.Vehicle;

            // Check for emergency vehicles
            if (SafeCompareTag(obstacle.gameObject, "Emergency"))
                return ObstacleType.EmergencyVehicle;

            // Check for pedestrians
            if (SafeCompareTag(obstacle.gameObject, "Pedestrian"))
                return ObstacleType.Pedestrian;

            // Check if it's a vehicle by component
            if (HasVehicleController(obstacle))
                return ObstacleType.Vehicle;

            // Check if it's static
            Rigidbody rb = obstacle.attachedRigidbody;
            if (rb == null || rb.isKinematic)
                return ObstacleType.StaticObject;

            return ObstacleType.Unknown;
        }

        private static bool HasVehicleController(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.TryGetComponent<NpcCarAgent>(out _) || collider.TryGetComponent<CarController>(out _))
            {
                return true;
            }

            Rigidbody attachedRigidbody = collider.attachedRigidbody;
            if (attachedRigidbody != null &&
                (attachedRigidbody.TryGetComponent<NpcCarAgent>(out _) || attachedRigidbody.TryGetComponent<CarController>(out _)))
            {
                return true;
            }

            Transform root = collider.transform.root;
            if (root != null && root != collider.transform &&
                (root.TryGetComponent<NpcCarAgent>(out _) || root.TryGetComponent<CarController>(out _)))
            {
                return true;
            }

            return false;
        }

        private static bool SafeCompareTag(GameObject gameObject, string tag)
        {
            if (gameObject == null || string.IsNullOrEmpty(tag))
                return false;

            return string.Equals(gameObject.tag, tag, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Respond to obstacle based on its type
        /// </summary>
        private float RespondToObstacle(Collider obstacle, float distance, float currentTargetSpeed)
        {
            if (!enableObstacleClassification)
            {
                // Default behavior - treat all as vehicles
                return currentTargetSpeed;
            }

            ObstacleType type = ClassifyObstacle(obstacle);

            switch (type)
            {
                case ObstacleType.Vehicle:
                    return HandleVehicleObstacle(distance, currentTargetSpeed);

                case ObstacleType.Pedestrian:
                    return HandlePedestrianObstacle(distance, currentTargetSpeed);

                case ObstacleType.EmergencyVehicle:
                    return HandleEmergencyVehicle(distance, currentTargetSpeed);

                case ObstacleType.StaticObject:
                    return HandleStaticObstacle(distance, currentTargetSpeed);

                default:
                    return HandleVehicleObstacle(distance, currentTargetSpeed);
            }
        }

        /// <summary>
        /// Handle vehicle obstacle - maintain distance, match speed
        /// </summary>
        private float HandleVehicleObstacle(float distance, float currentTargetSpeed)
        {
            if (distance < criticalBrakingDistance)
            {
                return currentTargetSpeed * 0.3f;
            }
            else if (distance < safeFollowingDistance)
            {
                float distanceFactor = distance / safeFollowingDistance;
                return currentTargetSpeed * Mathf.Lerp(0.5f, 0.9f, distanceFactor);
            }
            return currentTargetSpeed;
        }

        /// <summary>
        /// Handle pedestrian - ALWAYS stop, large safety margin
        /// </summary>
        private float HandlePedestrianObstacle(float distance, float currentTargetSpeed)
        {
            if (distance < 15f) // Large safety margin
            {
                return 0f; // Full stop
            }
            else if (distance < 25f)
            {
                return currentTargetSpeed * 0.3f; // Slow down significantly
            }
            return currentTargetSpeed;
        }

        /// <summary>
        /// Handle emergency vehicle - pull over and stop
        /// </summary>
        private float HandleEmergencyVehicle(float distance, float currentTargetSpeed)
        {
            if (distance < 30f)
            {
                // Pull over to the right (if possible)
                if (IsLaneSafe(1)) // Right lane
                {
                    ExecuteLaneChange(1);
                }

                // Slow down significantly
                return currentTargetSpeed * 0.4f;
            }
            return currentTargetSpeed;
        }

        /// <summary>
        /// Handle static obstacle - navigate around or stop
        /// </summary>
        private float HandleStaticObstacle(float distance, float currentTargetSpeed)
        {
            if (distance < criticalBrakingDistance)
            {
                return 0f; // Stop for very close static obstacles
            }
            else if (distance < safeFollowingDistance * 1.5f)
            {
                // Try to change lanes
                if (enableLaneChange)
                {
                    if (IsLaneSafe(-1))
                    {
                        ExecuteLaneChange(-1);
                    }
                    else if (IsLaneSafe(1))
                    {
                        ExecuteLaneChange(1);
                    }
                }
                return currentTargetSpeed * 0.6f;
            }
            return currentTargetSpeed;
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

            // REALISTIC TRAFFIC: Prefer LEFT lane for overtaking
            if (isObstacleDetected || isFollowing)
            {
                // Try left lane first (passing lane)
                if (leftLaneClear)
                {
                    targetLateralOffset = Mathf.Clamp(lateralOffset - laneChangeOffset, -maxLateralLaneOffset, maxLateralLaneOffset);
                    isOvertaking = true;
                    return;
                }
                // Only use right if left is blocked
                else if (rightLaneClear)
                {
                    targetLateralOffset = Mathf.Clamp(lateralOffset + laneChangeOffset, -maxLateralLaneOffset, maxLateralLaneOffset);
                    return;
                }
            }

            // General lane selection when both clear
            if (leftLaneClear && !rightLaneClear)
            {
                targetLateralOffset = Mathf.Clamp(lateralOffset - laneChangeOffset, -maxLateralLaneOffset, maxLateralLaneOffset);
            }
            else if (rightLaneClear && !leftLaneClear)
            {
                targetLateralOffset = Mathf.Clamp(lateralOffset + laneChangeOffset, -maxLateralLaneOffset, maxLateralLaneOffset);
            }
            else if (leftLaneClear && rightLaneClear)
            {
                // Both clear, prefer left for overtaking
                targetLateralOffset = Mathf.Clamp(lateralOffset - laneChangeOffset, -maxLateralLaneOffset, maxLateralLaneOffset);
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

            int hitCount = Physics.SphereCastNonAlloc(rayOrigin, vehicleDetectionRadius, forward, sharedSphereCastBuffer, avoidanceRayDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            if (hitCount == 0)
            {
                return false;
            }

            // Find the closest valid vehicle hit without sorting (zero-alloc)
            float cosAwareness = Mathf.Cos(forwardAwarenessAngle * Mathf.Deg2Rad);
            float bestDistance = float.MaxValue;
            NpcCarAgent bestAgent = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sharedSphereCastBuffer[i];
                if (hit.distance >= bestDistance) continue;
                if (!IsVehicleCollider(hit.collider)) continue;

                NpcCarAgent otherAgent = hit.collider.GetComponent<NpcCarAgent>() ?? hit.collider.GetComponentInParent<NpcCarAgent>();
                if (otherAgent == null || otherAgent == this) continue;

                Vector3 toOther = hit.point - rayOrigin;
                if (toOther.sqrMagnitude < 0.001f) continue;

                if (Vector3.Dot(forward, toOther.normalized) < cosAwareness) continue;

                float lateralOffsetFromLane = Mathf.Abs(Vector3.Dot((hit.point - transform.position), transform.right));
                if (lateralOffsetFromLane > sideRayOffset + 2.5f) continue;

                bestDistance = hit.distance;
                bestAgent = otherAgent;
            }

            if (bestAgent == null) return false;

            distance = bestDistance;
            float mySpeed = CurrentSpeed / 3.6f;
            float theirSpeed = bestAgent.CurrentSpeed / 3.6f;
            relativeSpeed = mySpeed - theirSpeed;

            float dynamicSafeDistance = CalculateSafeFollowingDistance() + Mathf.Max(0f, relativeSpeed * 1.2f);
            isFollowing = distance < dynamicSafeDistance;
            distanceToVehicleAhead = distance;
            relativeSpeedToVehicleAhead = relativeSpeed;
            vehicleAhead = bestAgent;
            return true;
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
            float personalityMultiplier = personality != null ? personality.followingDistanceMultiplier : 1f;

            // Priority 3: Increase following distance in bad weather
            float weatherMultiplier = environmentalFollowingDistanceMultiplier;

            // Add buffer for reaction time
            float reactionBuffer = 2f; // 2 meters buffer

            float dynamicGap = baseDistance * personalityMultiplier * weatherMultiplier + reactionBuffer;

            // In stop-go traffic, keep an explicit standing gap to avoid "glued" convoys.
            if (currentSpeedMs < 8f) // ~29 km/h and below
            {
                dynamicGap *= lowSpeedFollowingMultiplier;
                dynamicGap = Mathf.Max(dynamicGap, standstillFollowingGap);
            }

            return Mathf.Max(minimumFollowingGap, dynamicGap);
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
                float distanceRatio = Mathf.Clamp01(distanceToVehicleAhead / Mathf.Max(0.1f, safeDistance));
                float closingKmh = Mathf.Max(0f, relativeSpeedToVehicleAhead * 3.6f);

                // If very close, brake much harder than lead speed to reopen a gap.
                if (distanceRatio < 0.35f)
                {
                    float aggressiveDrop = Mathf.Lerp(10f, 22f, 1f - distanceRatio) + closingKmh * 0.35f;
                    return Mathf.Max(0f, leadVehicleSpeed - aggressiveDrop);
                }
                else if (distanceRatio < 0.6f)
                {
                    float moderatedDrop = Mathf.Lerp(4f, 10f, 0.6f - distanceRatio) + closingKmh * 0.2f;
                    return Mathf.Max(0f, Mathf.Min(leadVehicleSpeed, desiredSpeed) - moderatedDrop);
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

            // 1. OVERTAKING: Following a slower vehicle (realistic traffic behavior)
            if (isFollowing && vehicleAhead != null)
            {
                float speedDifference = targetSpeed - vehicleAhead.CurrentSpeed;
                // Overtake if we're even slightly faster (5+ km/h)
                if (speedDifference > overtakeSpeedThreshold)
                {
                    // ALWAYS try LEFT lane first (passing lane in traffic)
                    if (IsLaneSafe(-1))
                    {
                        isOvertaking = true;
                        return true;
                    }
                }
            }

            // 2. Current lane blocked ahead
            if (isObstacleDetected && obstacleDistance < safeFollowingDistance)
            {
                // Prefer left lane for overtaking
                if (IsLaneSafe(-1))
                {
                    isOvertaking = true;
                    return true;
                }
                // Only use right if left is blocked
                if (IsLaneSafe(1))
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

            // Set target lateral offset (wider range for better lane separation)
            targetLateralOffset = Mathf.Clamp(lateralOffset + (direction * laneChangeOffset), -maxLateralLaneOffset, maxLateralLaneOffset);

            // Mark as changing lanes
            isChangingLanes = true;
            lastLaneChangeTime = Time.time;

            // Mark as overtaking if moving left with vehicle ahead
            if (direction < 0 && (isFollowing || isObstacleDetected))
            {
                isOvertaking = true;

                if (logOvertakes)
                {
                    Debug.Log($"[NpcCarAgent] {name} starting overtake (moving to left lane)");
                }
            }

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

            float speedMs = CurrentSpeed / 3.6f;
            float queryRadius = Mathf.Max(30f, speedMs * predictiveCollisionHorizon + 10f);
            TrafficCommunicationSystem.Instance.GetNearbyVehicles(transform.position, queryRadius, nearbyVehicleBuffer);
            float minSpeedLimiter = 1f;
            bool urgentRisk = false;

            for (int i = 0; i < nearbyVehicleBuffer.Count; i++)
            {
                NpcCarAgent otherVehicle = nearbyVehicleBuffer[i];
                if (otherVehicle == this || otherVehicle == null) continue;

                float closestPredictedDistance = EstimateClosestPredictedDistance(otherVehicle, predictiveCollisionHorizon, predictiveCollisionSamples);
                float timeToCollision = VehicleTrajectoryPredictor.CalculateTimeToCollision(this, otherVehicle);
                float relativeClosingSpeed = CalculateRelativeClosingSpeed(otherVehicle);
                float dynamicSafeGap = CalculateSafeFollowingDistance() + Mathf.Max(0f, relativeClosingSpeed * 1.5f) + collisionBufferDistance;

                bool hasPredictiveRisk = closestPredictedDistance < dynamicSafeGap || timeToCollision < cautionTimeToCollision;
                if (!hasPredictiveRisk)
                {
                    continue;
                }

                if (timeToCollision < hardBrakeTimeToCollision || closestPredictedDistance < minimumSeparationDistance * 0.8f)
                {
                    minSpeedLimiter = Mathf.Min(minSpeedLimiter, 0.35f);
                    urgentRisk = true;
                }
                else if (timeToCollision < cautionTimeToCollision)
                {
                    minSpeedLimiter = Mathf.Min(minSpeedLimiter, 0.55f);
                }
                else
                {
                    float clearanceFactor = Mathf.Clamp01(closestPredictedDistance / Mathf.Max(1f, dynamicSafeGap));
                    minSpeedLimiter = Mathf.Min(minSpeedLimiter, Mathf.Lerp(0.6f, 0.9f, clearanceFactor));
                }

                if (timeToCollision < cautionTimeToCollision && ShouldChangeLane())
                {
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

            predictiveSpeedLimiter = Mathf.Clamp(minSpeedLimiter, 0.25f, 1f);
            imminentCollisionRisk = urgentRisk;
        }

        private float EstimateClosestPredictedDistance(NpcCarAgent otherVehicle, float horizonSeconds, int samples)
        {
            if (otherVehicle == null) return float.MaxValue;

            int sampleCount = Mathf.Max(3, samples);
            float timeStep = horizonSeconds / sampleCount;
            float closestDistance = Vector3.Distance(transform.position, otherVehicle.transform.position);

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i * timeStep;
                Vector3 posA = VehicleTrajectoryPredictor.PredictPositionWithPath(this, t);
                Vector3 posB = VehicleTrajectoryPredictor.PredictPositionWithPath(otherVehicle, t);
                float distance = Vector3.Distance(posA, posB);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            return closestDistance;
        }

        private float CalculateRelativeClosingSpeed(NpcCarAgent otherVehicle)
        {
            if (otherVehicle == null) return 0f;

            Rigidbody otherRb = otherVehicle.GetComponent<Rigidbody>();
            if (otherRb == null) return 0f;

            Vector3 relativePos = otherVehicle.transform.position - transform.position;
            if (relativePos.sqrMagnitude < 0.01f) return 0f;

            Vector3 relativeVel = rb.linearVelocity - otherRb.linearVelocity;
            return Vector3.Dot(relativeVel, relativePos.normalized);
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

        private void RegisterWithOptimizationSystem()
        {
            if (TrafficSimulationOptimizer.Instance != null)
            {
                TrafficSimulationOptimizer.Instance.RegisterNPC(this);
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

            if (TrafficSimulationOptimizer.Instance != null)
            {
                TrafficSimulationOptimizer.Instance.UnregisterNPC(this);
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
            TrafficCommunicationSystem.Instance.GetNearbyVehicles(transform.position, 20f, nearbyVehicleBuffer);

            for (int i = 0; i < nearbyVehicleBuffer.Count; i++)
            {
                NpcCarAgent otherVehicle = nearbyVehicleBuffer[i];
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
        // PRIORITY 3: Weather Effects (Step 6.1)
        // ============================================================================

        /// <summary>
        /// Update behavior based on weather conditions
        /// </summary>
        private void UpdateWeatherEffects()
        {
            if (!enableWeatherEffects || WeatherManager.Instance == null)
            {
                weatherSpeedMultiplier = 1f;
                environmentalFollowingDistanceMultiplier = 1f;
                return;
            }

            // Apply weather-based speed reduction
            weatherSpeedMultiplier = WeatherManager.Instance.GetSpeedReduction();

            // Increase following distance in bad weather
            environmentalFollowingDistanceMultiplier = WeatherManager.Instance.GetFollowingDistanceMultiplier();

            // Apply steering smoothing in bad weather
            if (WeatherManager.Instance.IsBadWeather())
            {
                float smoothingMultiplier = WeatherManager.Instance.GetSteeringSmoothingMultiplier();
                // This affects steering through the personality steering speed
                // Could be applied directly if needed
            }
        }

        /// <summary>
        /// Apply weather effects to speed
        /// </summary>
        private float ApplyWeatherEffects(float baseSpeed)
        {
            return baseSpeed * weatherSpeedMultiplier;
        }

        // ============================================================================
        // PRIORITY 3: Time of Day Effects (Step 6.2)
        // ============================================================================

        /// <summary>
        /// Update behavior based on time of day
        /// </summary>
        private void UpdateTimeOfDayEffects()
        {
            if (!enableTimeOfDayEffects)
            {
                timeOfDaySpeedMultiplier = 1f;
                return;
            }

            timeOfDaySpeedMultiplier = GetTimeOfDayMultiplier();
        }

        /// <summary>
        /// Get speed multiplier based on time of day
        /// </summary>
        private float GetTimeOfDayMultiplier()
        {
            int hour = System.DateTime.Now.Hour;

            // Rush hour (7-9 AM, 5-7 PM): more aggressive, higher speeds
            if ((hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19))
            {
                // Adjust based on personality - aggressive drivers go faster in rush hour
                if (personality != null && personality.riskTolerance > 0.6f)
                {
                    return 1.15f; // Aggressive drivers speed up in rush hour
                }
                return 1.05f; // Normal drivers slightly faster
            }

            // Night (10 PM - 6 AM): slower, more cautious
            if (hour >= 22 || hour <= 6)
            {
                return 0.85f; // 15% slower at night
            }

            // Daytime (normal)
            return 1.0f;
        }

        /// <summary>
        /// Check if it's currently rush hour
        /// </summary>
        private bool IsRushHour()
        {
            int hour = System.DateTime.Now.Hour;
            return (hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19);
        }

        /// <summary>
        /// Check if it's currently night time
        /// </summary>
        private bool IsNightTime()
        {
            int hour = System.DateTime.Now.Hour;
            return hour >= 22 || hour <= 6;
        }

        /// <summary>
        /// Apply time of day effects to speed
        /// </summary>
        private float ApplyTimeOfDayEffects(float baseSpeed)
        {
            return baseSpeed * timeOfDaySpeedMultiplier;
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
            if (isReversing)
            {
                ApplyReverseThrottle();
                return;
            }

            float currentSpeed = CurrentSpeed;
            float effectiveTargetSpeed = GetTurnLimitedTargetSpeed();

            // OVERTAKING BOOST: Maintain or increase speed when overtaking
            if (isOvertaking && isChangingLanes)
            {
                // Don't reduce speed during overtake - maintain target speed
                effectiveTargetSpeed = Mathf.Max(effectiveTargetSpeed, targetSpeed * 1.1f);
            }

            // Priority 1: Adjust speed for traffic (following distance system)
            // But not during active overtake
            if (!isOvertaking || !isChangingLanes)
            {
                effectiveTargetSpeed = AdjustSpeedForTraffic(effectiveTargetSpeed);
            }

            // Priority 2: Prepare for upcoming turns
            effectiveTargetSpeed = PrepareForTurn(effectiveTargetSpeed);

            // Priority 2: Apply lookahead-based speed adjustments
            if (currentLookaheadData.hasSharpTurn || currentLookaheadData.hasIntersection)
            {
                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, currentLookaheadData.recommendedSpeed);
            }

            // Priority 2: Assist merging vehicles
            AssistMerge();

            // Predictive collision response (TTC and trajectory based)
            effectiveTargetSpeed *= predictiveSpeedLimiter;
            if (imminentCollisionRisk)
            {
                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, 12f);
            }

            // Advanced obstacle-based speed adjustment
            // Be less aggressive when overtaking
            if (isObstacleDetected && !isOvertaking)
            {
                if (obstacleDistance < criticalBrakingDistance)
                {
                    // Very close obstacle - allow near-stop to prevent bumper lock.
                    effectiveTargetSpeed = Mathf.Max(effectiveTargetSpeed * 0.35f, trafficConstrainedMinSpeed);
                }
                else if (obstacleDistance < safeFollowingDistance)
                {
                    // Maintain safe following distance
                    float distanceFactor = (obstacleDistance - criticalBrakingDistance) /
                                          (safeFollowingDistance - criticalBrakingDistance);
                    effectiveTargetSpeed *= Mathf.Lerp(0.5f, 0.8f, distanceFactor);
                }
                else
                {
                    // Obstacle ahead but at safe distance - slight speed reduction
                    effectiveTargetSpeed *= 0.85f;
                }
            }

            // Enforce low minimum only in clear road conditions.
            bool trafficConstrained = isFollowing || isObstacleDetected || imminentCollisionRisk || isColliding;
            float minAllowedSpeed = trafficConstrained ? trafficConstrainedMinSpeed : freeFlowMinSpeed;
            effectiveTargetSpeed = Mathf.Max(effectiveTargetSpeed, minAllowedSpeed);

            float speedDifference = effectiveTargetSpeed - currentSpeed;
            float currentSafeDistance = CalculateSafeFollowingDistance();
            bool tooCloseToLead = isFollowing && distanceToVehicleAhead < currentSafeDistance;

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
            else if (speedDifference < -speedTolerance || isObstacleDetected || tooCloseToLead)
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

                if (tooCloseToLead)
                {
                    float gapRatio = Mathf.Clamp01(distanceToVehicleAhead / Mathf.Max(0.1f, currentSafeDistance));
                    float closeBrakeBoost = Mathf.Lerp(1.45f, 0.95f, gapRatio);
                    float closingBoost = Mathf.Clamp01(relativeSpeedToVehicleAhead / 6f) * 0.35f;
                    brakeTorque = Mathf.Max(brakeTorque, braking * (closeBrakeBoost + closingBoost));
                }

                if (imminentCollisionRisk)
                {
                    brakeTorque = Mathf.Max(brakeTorque, braking * 1.3f);
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

            // Priority 3: Apply environmental effects
            baseSpeed = ApplyWeatherEffects(baseSpeed);
            baseSpeed = ApplyTimeOfDayEffects(baseSpeed);

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

            // Priority 3: Adjust for weather traction
            if (WeatherManager.Instance != null && enableWeatherEffects)
            {
                float traction = WeatherManager.Instance.GetTractionMultiplier();
                maxSpeedKmh *= traction;
            }

            return Mathf.Min(baseSpeed * steerFactor, maxSpeedKmh);
        }

        private void ApplyStability()
        {
            if (!enableStability || rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            
            // Apply downforce - always apply some base downforce plus speed-based
            if (downforceCoefficient > 0f)
            {
                float speedDownforce = Mathf.Max(0f, downforceCoefficient) * speed * speed;
                float requestedDownforce = Mathf.Max(0f, baseDownforce) + speedDownforce;

                float maxDownforce = cachedWeight * Mathf.Max(0f, maxDownforceWeightFraction);
                if (maxDownforce > 0f)
                {
                    requestedDownforce = Mathf.Min(requestedDownforce, maxDownforce);
                }

                Vector3 downDirection = useLocalDownforceDirection ? -GetWorldUp() : Vector3.down;
                rb.AddForce(downDirection * requestedDownforce, ForceMode.Force);
            }

            if (antiRollStiffness > 0f && (fixedUpdateCounter + npcId) % StabilityCheckInterval == 0)
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

        private void NormalizeWheelColliderHeightsRuntime()
        {
            if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
                return;

            WheelCollider[] wheels = { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
            float minContactY = float.MaxValue;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i];
                if (wheel == null) return;

                Vector3 localPos = transform.InverseTransformPoint(wheel.transform.position);
                float contactY = localPos.y - wheel.radius;
                if (contactY < minContactY)
                    minContactY = contactY;
            }

            if (Mathf.Abs(minContactY) < 0.001f)
                return;

            for (int i = 0; i < wheels.Length; i++)
            {
                Transform wt = wheels[i].transform;
                Vector3 localPos = wt.localPosition;
                localPos.y -= minContactY;
                wt.localPosition = localPos;
            }
        }

        private void ApplyReverseThrottle()
        {
            float signedSpeedKmh = Vector3.Dot(rb.linearVelocity, GetWorldForward()) * 3.6f;
            float desiredReverseSpeedKmh = -Mathf.Max(1f, reverseTargetSpeed);

            bool rearTooClose = rearObstacleDetected && rearObstacleDistance < 1.2f;
            if (rearTooClose)
            {
                SetMotorTorque(0f);
                SetBrakeTorque(braking * 0.8f);
                return;
            }

            if (signedSpeedKmh > desiredReverseSpeedKmh + 0.5f)
            {
                float speedError = Mathf.Abs(desiredReverseSpeedKmh - signedSpeedKmh);
                float reverseTorque = personalityAcceleration * 0.45f * Mathf.Clamp01(speedError / 10f);
                reverseTorque = Mathf.Max(80f, reverseTorque);
                SetBrakeTorque(0f);
                SetMotorTorque(-reverseTorque);
            }
            else
            {
                SetMotorTorque(0f);
                SetBrakeTorque(braking * 0.2f);
            }
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

        /// <summary>
        /// Handle collision with other vehicles
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (IsVehicleCollider(collision.collider))
            {
                isColliding = true;
                collisionTimer = 0f;

                // Log collision for debugging
                if (logCollisions)
                {
                    Debug.LogWarning($"[NpcCarAgent] {name} collided with {collision.collider.name}");
                }

                // Calculate separation direction
                Vector3 collisionNormal = Vector3.zero;
                foreach (ContactPoint contact in collision.contacts)
                {
                    collisionNormal += contact.normal;
                }
                if (collisionNormal.sqrMagnitude > 0.01f)
                {
                    separationDirection = collisionNormal.normalized;
                }
            }
        }

        /// <summary>
        /// Handle continuous collision
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            if (IsVehicleCollider(collision.collider))
            {
                isColliding = true;

                // Keep moving even during collision to allow natural separation
                // Don't intervene too aggressively - let the separation force handle it
                if (collisionTimer > 8f)
                {
                    // Only slow down if stuck for a very long time
                    // Keep minimum speed of 15 km/h to ensure movement
                    targetSpeed = Mathf.Max(15f, Mathf.Min(targetSpeed, 25f));
                }
            }
        }

        /// <summary>
        /// Handle collision exit
        /// </summary>
        private void OnCollisionExit(Collision collision)
        {
            if (IsVehicleCollider(collision.collider))
            {
                // Check if still colliding with other vehicles
                bool stillColliding = false;
                foreach (Collider col in nearbyVehicles)
                {
                    if (col != null && col != collision.collider)
                    {
                        float distance = Vector3.Distance(transform.position, col.transform.position);
                        if (distance < minimumSeparationDistance * 0.7f)
                        {
                            stillColliding = true;
                            break;
                        }
                    }
                }

                if (!stillColliding)
                {
                    isColliding = false;
                }
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

                if (enableReverseRecovery)
                {
                    Gizmos.color = rearObstacleDetected ? new Color(1f, 0.5f, 0f) : Color.gray;
                    Gizmos.DrawLine(rayOrigin, rayOrigin - forward * reverseRearCheckDistance);
                    if (rearObstacleDetected && rearObstacleDistance < float.MaxValue)
                    {
                        Gizmos.DrawWireSphere(rayOrigin - forward * rearObstacleDistance, 0.35f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Obstacle type classification for appropriate responses
    /// Priority 3: Environmental Awareness
    /// </summary>
    public enum ObstacleType
    {
        Unknown,
        Vehicle,
        Pedestrian,
        StaticObject,
        EmergencyVehicle
    }
}
