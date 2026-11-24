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
        [Tooltip("Extra forward lookahead time for vehicle/player hard-stop checks")]
        [SerializeField] private float vehicleStopLookaheadTime = 1.6f;
        [Tooltip("Maximum forward cast distance used for vehicle/player hard-stop checks")]
        [SerializeField] private float maxVehicleStopCastDistance = 45f;
        [Tooltip("Gap where NPCs command a full stop before touching another vehicle")]
        [SerializeField] private float fullStopGapDistance = 6f;
        [Tooltip("Time-to-collision threshold that forces a full stop")]
        [SerializeField] private float fullStopTimeToCollision = 1.5f;
        [Tooltip("Assumed deceleration used to decide when a vehicle must stop")]
        [SerializeField] private float assumedEmergencyDeceleration = 6f;

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

        [Header("Static Obstacle Recovery")]
        [Tooltip("Treat buildings and other static world colliders as blockers instead of trying to drive through them")]
        [SerializeField] private bool enableStaticObstacleRecovery = true;
        [Tooltip("Reverse duration used after touching a building/static obstacle")]
        [SerializeField] private float staticObstacleReverseDuration = 1.5f;
        [Tooltip("Seconds of continued static collision before snapping back to the road")]
        [SerializeField] private float staticObstacleSnapDelay = 2.5f;
        [Tooltip("Cooldown between static-obstacle snap recoveries")]
        [SerializeField] private float staticObstacleSnapCooldown = 6f;

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
        [Tooltip("Force NPC traffic to stay in a single right-hand lane")]
        [SerializeField] private bool enforceRightHandSingleLane = true;

        [Header("Intersection Navigation")]
        [Tooltip("Treat near-aligned outgoing connections as straight continuation instead of a turn choice")]
        [SerializeField] private float straightConnectionAngleThreshold = 30f;
        [Tooltip("Fallback search radius for continuing onto the next aligned segment when no explicit connection exists")]
        [SerializeField] private float nearbyContinuationSearchRadius = 7.5f;
        [Tooltip("Reject route changes that would require turns sharper than this angle")]
        [SerializeField] private float maxConnectionTurnAngle = 90f;
        [Tooltip("Let lookahead continue into the next chosen segment so vehicles begin turning before the segment ends")]
        [SerializeField] private bool previewUpcomingConnections = true;
        [Tooltip("Distance before an intersection where the next segment decision is locked")]
        [SerializeField] private float intersectionCommitDistance = 18f;
        [Tooltip("Extra lookahead multiplier while committed to an intersection path")]
        [SerializeField] private float intersectionLookAheadBoost = 1.35f;
        [Tooltip("Maximum target steering angle change per second")]
        [SerializeField] private float maxSteerTargetDeltaPerSecond = 140f;

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
        [Tooltip("Optional artificial self-leveling torque. Basic NPC physics uses WheelCollider suspension and angular damping without this assist.")]
        [SerializeField] private bool enableSupplementalTiltCorrection = false;

        [Header("Physics")]
        [Tooltip("Rigidbody interpolation mode")]
        [SerializeField] private RigidbodyInterpolation rbInterpolation = RigidbodyInterpolation.Interpolate;
        [Tooltip("Rigidbody collision detection mode")]
        [SerializeField] private CollisionDetectionMode rbCollisionDetection = CollisionDetectionMode.Discrete;
        [Tooltip("Upgrade discrete collision detection to ContinuousDynamic for safer vehicle contacts")]
        [SerializeField] private bool upgradeDiscreteCollisionDetection = true;
        [Tooltip("Solver iterations (higher = more stable)")]
        [SerializeField] private int solverIterations = 6;
        [Tooltip("Solver velocity iterations (higher = more stable)")]
        [SerializeField] private int solverVelocityIterations = 4;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool logPathChanges = false;
        [SerializeField] private bool logCollisions = false;
        [SerializeField] private bool logOvertakes = false;  // Log overtaking behavior

        [Header("Simulation Decimation")]
        [Tooltip("Scale expensive perception checks using optimizer distance buckets")]
        [SerializeField] private bool useAdaptiveSimulationDecimation = true;

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
        private bool mustStopForVehicle;
        private float vehicleStopDistance = float.MaxValue;
        private bool isPotentiallyOffRoad;
        private float wheelBase;

        // Performance optimization
        private static int nextNpcId = 0;
        private int npcId;
        private int fixedUpdateCounter;
        private uint physicsTick;
        internal uint PhysicsTick => physicsTick;
        internal uint PhysicsPhase => unchecked((uint)npcId);
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
        private TrafficCommunicationSystem predictionQuerySource;
        private Vector3 predictionQueryOrigin;
        private readonly List<Vector3> selfPredictionSamples = new List<Vector3>(8);

        // Advanced obstacle avoidance state
        private bool leftLaneClear;
        private bool rightLaneClear;
        private float targetLateralOffset;
        private float currentLateralOffset;

        // Randomized behavior parameters
        private bool runtimeDrivingInitialized;
        private float lateralOffset; // Lateral offset from centerline (for lane variation)
        private float cruisingLaneOffset;
        private float preferredCruiseLaneSign = 1f;
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
        private NpcCarWheelVisualService wheelVisualService;
        private readonly List<RoadConnection> endConnectionBuffer = new List<RoadConnection>(8);

        private float nextReacquireTime = 0f;

        // Priority 2: Personality System
        private DrivingPersonality personality;

        // Priority 2: Predictive Behavior
        private LookaheadData currentLookaheadData;
        private TurnInfo currentTurnInfo;
        private RoadConnection plannedConnection;
        private RoadSegment plannedContinuationSegment;
        private int plannedContinuationWaypointIndex = -1;
        private int plannedContinuationSourceSegmentId = -1;
        private bool hasPlannedContinuation;
        private float smoothedTargetSteerAngle;
        private bool hasSmoothedTargetSteerAngle;
        private bool hasDrivePlan;
        private bool hasSteeringTarget;
        private float requestedSteerAngle;
        private float plannedTargetSpeed;
        private bool waypointLod;
        private float lodSpeedMs;
        private float lodHeightOffset;
        private float nextLodEntryTime;
        private int lodWheelMask;
        private RigidbodyInterpolation savedLodInterpolation;
        private CollisionDetectionMode savedLodCollisionMode;
        private uint lastSafetyTick, lastPathTick, lastNearbyTick, lastOffRoadTick, lastWeatherTick;

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
        private bool isStaticObstacleCollision;
        private float staticObstacleCollisionTimer;
        private float lastStaticObstacleSnapTime;

        // Public accessors
        public RoadSegment CurrentSegment => currentSegment;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public bool IsWaypointLod => waypointLod;
        public Vector3 SimulationVelocity => waypointLod ? GetWorldForward() * lodSpeedMs :
            rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
        public float CurrentSpeed => SimulationVelocity.magnitude * 3.6f; // km/h
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
            wheelVisualService = new NpcCarWheelVisualService(autoSetupWheelVisuals, autoCreateWheelVisualsIfMissing);
            SetupRigidbody();
            cachedWeight = rb.mass * Mathf.Abs(Physics.gravity.y);
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
            EnsureDrivingRuntimeInitialized();

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
            ExitWaypointLod(false);
            InvalidateDrivePlan();
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
            rb.collisionDetectionMode = upgradeDiscreteCollisionDetection && rbCollisionDetection == CollisionDetectionMode.Discrete
                ? CollisionDetectionMode.ContinuousDynamic
                : rbCollisionDetection;
            if (solverIterations > 0) rb.solverIterations = solverIterations;
            if (solverVelocityIterations > 0) rb.solverVelocityIterations = solverVelocityIterations;
        }

        /// <summary>
        /// Initialize NPC on a specific road segment and waypoint
        /// </summary>
        public void Initialize(RoadGraphBuilder builder, RoadSegment segment, int waypointIndex)
        {
            EnsureDrivingRuntimeInitialized();

            roadGraphBuilder = builder;
            currentSegment = segment;
            currentWaypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            ClearPlannedContinuation();
            ResetTransientSpawnState();
            hasSmoothedTargetSteerAngle = false;
            ApplyLaneCenteringPolicyForCurrentSegment();
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
            if (!isInitialized || rb == null || currentSegment?.waypoints == null ||
                currentSegment.waypoints.Count == 0)
            {
                ExitWaypointLod(false);
                InvalidateDrivePlan();
                return;
            }

            unchecked { physicsTick++; }
            var optimizer = TrafficSimulationOptimizer.Instance;
            float distance = 0f;
            bool useLod = optimizer != null && optimizer.ShouldUseWaypointLod(this, waypointLod, out distance);
            if (optimizer != null && !useLod)
                optimizer.TryGetPlayerDistance(this, out distance);

            if (waypointLod && (!useLod || NeedsHighFrequencySimulation()))
                ExitWaypointLod(true);

            if (!waypointLod && useLod && Time.time >= nextLodEntryTime &&
                hasDrivePlan && !NeedsHighFrequencySimulation() && !IsManeuvering())
                TryEnterWaypointLod();

            if (waypointLod)
            {
                if (AdvanceWaypointLod())
                    return;
                ExitWaypointLod(true);
            }

            // Other systems may briefly own a kinematic body (e.g. recovery).
            if (rb.isKinematic)
            {
                InvalidateDrivePlan();
                return;
            }

            UpdateDynamicDriving(distance);
        }

        private void UpdateDynamicDriving(float distance)
        {
            bool fresh = !hasDrivePlan;
            bool urgent = NeedsHighFrequencySimulation();
            bool maneuver = IsManeuvering();
            bool adaptive = useAdaptiveSimulationDecimation;
            int pathInterval = NpcTrafficLodPolicy.PathInterval(distance, urgent, maneuver, adaptive);
            int safetyInterval = NpcTrafficLodPolicy.SafetyInterval(distance, urgent, maneuver, adaptive);
            // Bound free-flow sensing by available forward cast range and stopping distance.
            float speed = CurrentSpeed / 3.6f;
            float stoppingDistance = speed * speed / (2f * Mathf.Max(1f, assumedEmergencyDeceleration));
            float available = GetForwardVehicleAwarenessDistance() - stoppingDistance - fullStopGapDistance - collisionBufferDistance;
            int reactionTicks = Mathf.FloorToInt(Mathf.Max(0f, available) /
                Mathf.Max(0.1f, speed) / Mathf.Max(0.001f, Time.fixedDeltaTime));
            safetyInterval = Mathf.Min(safetyInterval, Mathf.Max(1, reactionTicks));

            bool pathDue = fresh || NpcTrafficLodPolicy.IsDue(physicsTick, lastPathTick, pathInterval);
            bool safetyDue = fresh || NpcTrafficLodPolicy.IsDue(physicsTick, lastSafetyTick, safetyInterval);
            predictionQuerySource = null;
            if (pathDue)
            {
                lastPathTick = physicsTick;
                ReacquireWaypointIfNeeded();
                UpdatePath();
                UpdateIntersectionCommitment();
                AnalyzeUpcomingPathAndTurn();
            }

            if (fresh || NpcTrafficLodPolicy.IsDue(physicsTick, lastOffRoadTick, distance < 150f ? 10 : 20))
            {
                lastOffRoadTick = physicsTick;
                UpdateOffRoadStatus();
            }

            if (fresh || NpcTrafficLodPolicy.IsDue(physicsTick, lastWeatherTick, 30))
            {
                lastWeatherTick = physicsTick;
                UpdateWeatherEffects();
                UpdateTimeOfDayEffects();
            }

            if (safetyDue)
            {
                lastSafetyTick = physicsTick;
                // Retain the last known hazard between valid sensing updates.
                predictiveSpeedLimiter = 1f;
                imminentCollisionRisk = false;
                CheckPredictiveCollisions();
                CheckObstacles();
                CheckRearObstacles();
            }
            else if (isReversing)
                CheckRearObstacles();

            if (fresh || NpcTrafficLodPolicy.IsDue(physicsTick, lastNearbyTick, urgent ? 1 : safetyInterval))
            {
                lastNearbyTick = physicsTick;
                CheckNearbyVehicles();
            }

            UpdateReverseRecoveryState();
            ApplySeparationForce();
            if (pathDue || safetyDue)
            {
                UpdateLaneChange();
                UpdateSteeringTarget();
            }

            ApplySteering();
            ApplyStability();
            if (pathDue || safetyDue || fresh)
            {
                plannedTargetSpeed = isReversing ? 0f : CalculateEffectiveTargetSpeed();
                hasDrivePlan = true;
            }

            float target = !pathDue && !safetyDue && !isReversing
                ? Mathf.Min(plannedTargetSpeed, GetTurnLimitedTargetSpeed())
                : plannedTargetSpeed;
            ApplyThrottle(target);
        }

        private bool IsManeuvering()
        {
            if (isChangingLanes || isOvertaking || isReversing || isPotentiallyOffRoad ||
                currentTurnInfo.isTurn || currentLookaheadData.hasSharpTurn || hasPlannedContinuation)
                return true;

            // A cheap conservative junction proximity test, without scanning the whole road.
            Vector3 delta = currentSegment.waypoints[currentSegment.waypoints.Count - 1].position - transform.position;
            return delta.sqrMagnitude <= 25f * 25f;
        }

        private bool TryEnterWaypointLod()
        {
            if (rb.isKinematic || currentWaypointIndex < 0 ||
                currentWaypointIndex >= currentSegment.waypoints.Count)
                return false;

            Vector3 pos = rb.position;
            Vector3 forward = GetWorldForward();
            int index = currentWaypointIndex;
            // Skip passed targets; never jump backwards to a previous waypoint.
            while (index < currentSegment.waypoints.Count - 1 &&
                Vector3.Dot(currentSegment.waypoints[index].position - pos, forward) <= 0f)
                index++;

            Vector3 waypoint = currentSegment.waypoints[index].position;
            Vector3 from = currentSegment.waypoints[Mathf.Max(0, index - 1)].position;
            Vector3 edge = waypoint - from;
            float t = edge.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(pos - from, edge) / edge.sqrMagnitude) : 0f;
            Vector3 projection = from + edge * t;
            Vector3 offset = pos - projection;
            if (Mathf.Abs(offset.y) > 2f || offset.x * offset.x + offset.z * offset.z > 16f ||
                Vector3.Dot(waypoint - pos, forward) <= 0f)
                return false;

            currentWaypointIndex = index;
            lodHeightOffset = offset.y;
            lodSpeedMs = Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, forward));
            savedLodInterpolation = rb.interpolation;
            savedLodCollisionMode = rb.collisionDetectionMode;
            SetMotorTorque(0f);
            SetBrakeTorque(0f);
            lodWheelMask = CaptureAndDisableLodWheels();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
            // Interpolate small waypoint steps for visible distant traffic.
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            waypointLod = true;
            InvalidateDrivePlan();
            return true;
        }

        private bool AdvanceWaypointLod()
        {
            if (!rb.isKinematic || currentWaypointIndex < 0 ||
                currentWaypointIndex >= currentSegment.waypoints.Count)
                return false;

            Vector3 position = rb.position;
            Vector3 target = GetLodWaypointTarget(currentWaypointIndex);
            if ((target - position).sqrMagnitude <= 0.0025f)
            {
                if (currentWaypointIndex < currentSegment.waypoints.Count - 1)
                    currentWaypointIndex++;
                else if (!TryAdvanceLodConnection(position))
                    return false;
                target = GetLodWaypointTarget(currentWaypointIndex);
            }

            Vector3 delta = target - position;
            float remaining = delta.magnitude;
            if (remaining <= 0.0001f)
                return true;

            Vector3 direction = delta / remaining;
            // One simple heading-based speed cap instead of wheel/turn prediction.
            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return false;
            flat.Normalize();
            float alignment = Vector3.Dot(GetPlanarTravelForward(), flat);
            float cruise = Mathf.Max(0f, ApplyTimeOfDayEffects(ApplyWeatherEffects(targetSpeed))) / 3.6f;
            float desired = cruise * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(alignment));
            if (currentWaypointIndex >= currentSegment.waypoints.Count - 2)
                desired = Mathf.Min(desired, cruise * 0.55f);

            float dt = Time.fixedDeltaTime;
            float nextSpeed = Mathf.MoveTowards(lodSpeedMs, desired, 3f * dt);
            Vector3 next = NpcTrafficLodPolicy.StepTowards(position, target, nextSpeed, dt);
            float travel = Vector3.Distance(position, next);
            // Conservative short sweep. On a blocked or saturated query, hand back to
            // dynamic sensing instead of passing through traffic or static geometry.
            if (travel > 0f && IsLodMovementBlocked(position, direction, travel))
            {
                lodSpeedMs = 0f;
                return false;
            }

            lodSpeedMs = travel / Mathf.Max(0.001f, dt);
            rb.MovePosition(next);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(flat, Vector3.up), 90f * dt));
            return true;
        }

        private Vector3 GetLodWaypointTarget(int index)
        {
            Vector3 forward = GetConnectionTravelForward(currentSegment, index);
            Vector3 side = Vector3.Cross(forward, Vector3.up).normalized;
            return currentSegment.waypoints[index].position + side * cruisingLaneOffset + Vector3.up * lodHeightOffset;
        }

        private bool TryAdvanceLodConnection(Vector3 position)
        {
            CollectEndConnections(endConnectionBuffer);
            // Only explicit directed links; no nearest-road fallback or wraparound.
            for (int i = endConnectionBuffer.Count - 1; i >= 0; i--)
            {
                RoadConnection link = endConnectionBuffer[i];
                if (link.toSegment == null || link.toSegment == currentSegment ||
                    link.toSegment.waypoints == null || link.toWaypointIndex < 0 ||
                    link.toWaypointIndex >= link.toSegment.waypoints.Count ||
                    (link.toSegment.waypoints[link.toWaypointIndex].position - position).sqrMagnitude > 64f)
                    endConnectionBuffer.RemoveAt(i);
            }

            RoadConnection connection = SelectBestConnection(endConnectionBuffer);
            if (connection == null)
                return false;

            currentSegment = connection.toSegment;
            currentWaypointIndex = connection.toWaypointIndex;
            ClearPlannedContinuation();
            ApplyLaneCenteringPolicyForCurrentSegment();
            return true;
        }

        private bool IsLodMovementBlocked(Vector3 position, Vector3 direction, float travel)
        {
            Vector3 origin = position + Vector3.up * 0.7f;
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.5f, vehicleDetectionRadius),
                direction, sharedSphereCastBuffer, travel + Mathf.Max(2f, fullStopGapDistance),
                obstacleLayerMask, QueryTriggerInteraction.Ignore);
            if (count >= sharedSphereCastBuffer.Length)
                return true;
            for (int i = 0; i < count; i++)
            {
                Collider collider = sharedSphereCastBuffer[i].collider;
                if (IsVehicleCollider(collider) || IsStaticWorldObstacle(collider))
                    return true;
            }
            return false;
        }

        private int CaptureAndDisableLodWheels()
        {
            int mask = 0;
            DisableLodWheel(frontLeftCollider, 1, ref mask);
            DisableLodWheel(frontRightCollider, 2, ref mask);
            DisableLodWheel(rearLeftCollider, 4, ref mask);
            DisableLodWheel(rearRightCollider, 8, ref mask);
            return mask;
        }

        private static void DisableLodWheel(WheelCollider wheel, int bit, ref int mask)
        {
            if (wheel != null && wheel.enabled)
            {
                mask |= bit;
                wheel.enabled = false;
            }
        }

        private static void RestoreLodWheel(WheelCollider wheel, int bit, int mask)
        {
            if (wheel != null)
                wheel.enabled = (mask & bit) != 0;
        }

        private void ExitWaypointLod(bool resumeMotion)
        {
            if (!waypointLod)
                return;
            waypointLod = false;
            nextLodEntryTime = Time.time + 3f;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = savedLodCollisionMode;
                rb.interpolation = savedLodInterpolation;
                RestoreLodWheel(frontLeftCollider, 1, lodWheelMask);
                RestoreLodWheel(frontRightCollider, 2, lodWheelMask);
                RestoreLodWheel(rearLeftCollider, 4, lodWheelMask);
                RestoreLodWheel(rearRightCollider, 8, lodWheelMask);
                rb.linearVelocity = resumeMotion ? GetWorldForward() * lodSpeedMs : Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }
            InvalidateDrivePlan();
        }

        private void InvalidateDrivePlan()
        {
            hasDrivePlan = false;
            hasSteeringTarget = false;
            hasSmoothedTargetSteerAngle = false;
            predictionQuerySource = null;
        }

        private bool NeedsHighFrequencySimulation() =>
            isObstacleDetected || mustStopForVehicle || isFollowing || imminentCollisionRisk ||
            isChangingLanes || isOvertaking || isReversing || rearObstacleDetected ||
            isColliding || isStaticObstacleCollision;

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

                    float closingSpeed = distanceSqr > 0.001f
                        ? GetClosingSpeed(col, toOther * (1f / Mathf.Sqrt(distanceSqr))) : 0f;

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

                    float closingSpeed = GetClosingSpeed(nearbyVehicle, toOther * invDistance);

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

        private float GetClosingSpeed(Collider other, Vector3 direction)
        {
            Rigidbody otherBody = other.attachedRigidbody;
            Vector3 velocity = otherBody != null && !otherBody.isKinematic ? otherBody.linearVelocity : Vector3.zero;
            return Vector3.Dot(rb.linearVelocity - velocity, direction);
        }

        /// <summary>
        /// Smoothly update lateral offset for lane changes
        /// </summary>
        private void UpdateLaneChange()
        {
            if (!enableLaneChange || isReversing)
            {
                if (enforceRightHandSingleLane)
                {
                    targetLateralOffset = cruisingLaneOffset;
                    lateralOffset = Mathf.Lerp(lateralOffset, cruisingLaneOffset, Time.fixedDeltaTime * laneChangeSpeed);
                    currentLateralOffset = lateralOffset;

                    if (turnSignalController != null && !isChangingLanes)
                    {
                        turnSignalController.DeactivateAll();
                    }
                }

                return;
            }

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
            if (waypointLod)
                return;
            wheelVisualService?.UpdateWheelVisuals(
                frontLeftCollider,
                frontLeftWheelVisual,
                frontRightCollider,
                frontRightWheelVisual,
                rearLeftCollider,
                rearLeftWheelVisual,
                rearRightCollider,
                rearRightWheelVisual);
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

            Vector3 flatForward = GetPlanarTravelForward();

            float dynamicPassDist = Mathf.Max(waypointPassDistance, personalityLookAhead * 0.25f);
            Vector3 position = transform.position;

            // FIX: Advance waypoint both when close enough and when the waypoint is already behind us.
            while (currentWaypointIndex < waypointCount)
            {
                Waypoint currentWp = currentSegment.waypoints[currentWaypointIndex];
                Vector3 toWp = currentWp.position - position;
                toWp.y = 0f;

                float distanceSqr = toWp.sqrMagnitude;
                bool isCloseEnough = Mathf.Sqrt(distanceSqr) <= dynamicPassDist;
                if (!isCloseEnough && !(distanceSqr > 0.0001f &&
                    Vector3.Dot(flatForward, toWp.normalized) < waypointPassedDotThreshold))
                    break;

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
            Vector3 flatForward = GetPlanarTravelForward();

            int searchBehind = hasPlannedContinuation ? 0 : reacquireSearchBehind;
            int start = Mathf.Clamp(currentWaypointIndex - searchBehind, 0, waypointCount - 1);
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
            if (TryGetPlannedContinuation(out RoadSegment nextSegment, out int nextWaypointIndex))
            {
                currentSegment = nextSegment;
                currentWaypointIndex = Mathf.Clamp(nextWaypointIndex, 0, currentSegment.waypoints.Count - 1);
                ApplyLaneCenteringPolicyForCurrentSegment();

                if (logPathChanges)
                {
                    string transitionType = plannedConnection != null ? "transitioning" : "continuing";
                    Debug.Log($"[NpcCarAgent] {name} {transitionType} to segment '{currentSegment.name}' at waypoint {currentWaypointIndex}");
                }

                ClearPlannedContinuation();
                return;
            }

            ClearPlannedContinuation();
            currentWaypointIndex = 0;
            if (logPathChanges)
            {
                Debug.Log($"[NpcCarAgent] {name} looping on segment '{currentSegment.name}' (no outgoing connection)");
            }
        }

        private void EnsureDrivingRuntimeInitialized()
        {
            if (runtimeDrivingInitialized)
            {
                return;
            }

            if (enforceRightHandSingleLane)
            {
                useFixedLaneCenters = true;
                randomizeInitialLane = false;
                enableLaneChange = false;
            }

            InitializePersonality();

            targetSpeed = Random.Range(cruiseSpeedRange.x, cruiseSpeedRange.y);
            float variation = targetSpeed * Random.Range(-0.3f, 0.3f);
            targetSpeed = Mathf.Max(20f, targetSpeed + variation);

            preferredCruiseLaneSign = randomizeInitialLane && Random.value < 0.5f ? -1f : 1f;
            if (enforceRightHandSingleLane)
            {
                // GetLookAheadPoint offsets by Cross(tangent, up), so negative keeps traffic on road-right.
                preferredCruiseLaneSign = -1f;
            }

            personalityLookAhead = Mathf.Max(2f, lookAheadDistance * Random.Range(0.9f, 1.1f));
            personalityAcceleration = Mathf.Max(1f, acceleration * Random.Range(0.9f, 1.1f));
            personalitySteerSpeed = Mathf.Max(1f, steeringSmoothSpeed * Random.Range(0.9f, 1.1f));
            isOvertaking = false;
            isChangingLanes = false;
            runtimeDrivingInitialized = true;
        }

        private void ResetTransientSpawnState()
        {
            ExitWaypointLod(false);
            InvalidateDrivePlan();
            isObstacleDetected = false;
            obstacleDistance = float.MaxValue;
            mustStopForVehicle = false;
            vehicleStopDistance = float.MaxValue;
            predictiveSpeedLimiter = 1f;
            imminentCollisionRisk = false;
            isFollowing = false;
            vehicleAhead = null;
            distanceToVehicleAhead = 0f;
            relativeSpeedToVehicleAhead = 0f;
            isColliding = false;
            collisionTimer = 0f;
            separationDirection = Vector3.zero;
            nearbyVehicles.Clear();
            isReversing = false;
            reverseTimer = 0f;
            blockedTimer = 0f;
            rearObstacleDetected = false;
            rearObstacleDistance = float.MaxValue;
            isStaticObstacleCollision = false;
            staticObstacleCollisionTimer = 0f;
            currentSteerAngle = 0f;
            hasSmoothedTargetSteerAngle = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }

            SetMotorTorque(0f);
            SetBrakeTorque(0f);
        }

        private void CollectEndConnections(List<RoadConnection> buffer)
        {
            buffer.Clear();

            if (currentSegment == null || currentSegment.connections == null || currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
            {
                return;
            }

            int lastIdx = currentSegment.waypoints.Count - 1;
            for (int i = 0; i < currentSegment.connections.Count; i++)
            {
                RoadConnection connection = currentSegment.connections[i];
                if (connection == null)
                {
                    continue;
                }

                if (connection.fromWaypointIndex >= lastIdx - 1)
                {
                    buffer.Add(connection);
                }
            }
        }

        private bool TryGetPlannedContinuation(out RoadSegment nextSegment, out int nextWaypointIndex)
        {
            nextSegment = null;
            nextWaypointIndex = 0;

            if (currentSegment == null || currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
            {
                ClearPlannedContinuation();
                return false;
            }

            if (hasPlannedContinuation &&
                plannedContinuationSourceSegmentId == currentSegment.id &&
                plannedContinuationSegment != null &&
                plannedContinuationSegment != currentSegment &&
                plannedContinuationSegment.waypoints != null &&
                plannedContinuationSegment.waypoints.Count > 0)
            {
                nextSegment = plannedContinuationSegment;
                nextWaypointIndex = Mathf.Clamp(plannedContinuationWaypointIndex, 0, plannedContinuationSegment.waypoints.Count - 1);
                UpdateCommittedTurnSignal();
                return true;
            }

            ClearPlannedContinuation();
            plannedContinuationSourceSegmentId = currentSegment.id;

            CollectEndConnections(endConnectionBuffer);
            if (endConnectionBuffer.Count > 0)
            {
                plannedConnection = SelectBestConnection(endConnectionBuffer);
                if (plannedConnection != null && plannedConnection.toSegment != null && plannedConnection.toSegment.waypoints != null && plannedConnection.toSegment.waypoints.Count > 0)
                {
                    plannedContinuationSegment = plannedConnection.toSegment;
                    plannedContinuationWaypointIndex = Mathf.Clamp(plannedConnection.toWaypointIndex, 0, plannedContinuationSegment.waypoints.Count - 1);
                    hasPlannedContinuation = true;
                }
            }

            if (!hasPlannedContinuation &&
                TryGetNearbyContinuation(out RoadSegment fallbackSegment, out int fallbackWaypointIndex))
            {
                plannedConnection = null;
                plannedContinuationSegment = fallbackSegment;
                plannedContinuationWaypointIndex = fallbackWaypointIndex;
                hasPlannedContinuation = true;
            }

            if (!hasPlannedContinuation || plannedContinuationSegment == null)
            {
                return false;
            }

            nextSegment = plannedContinuationSegment;
            nextWaypointIndex = Mathf.Clamp(plannedContinuationWaypointIndex, 0, plannedContinuationSegment.waypoints.Count - 1);
            UpdateCommittedTurnSignal();
            return true;
        }

        private void UpdateIntersectionCommitment()
        {
            if (!previewUpcomingConnections ||
                currentSegment == null ||
                currentSegment.waypoints == null ||
                currentSegment.waypoints.Count < 2)
            {
                return;
            }

            if (hasPlannedContinuation &&
                plannedContinuationSourceSegmentId == currentSegment.id &&
                plannedContinuationSegment != null)
            {
                return;
            }

            float distanceToEnd = GetDistanceFromWaypointToSegmentEnd(currentSegment, currentWaypointIndex);
            float commitDistance = Mathf.Max(intersectionCommitDistance, personalityLookAhead * intersectionLookAheadBoost);
            if (distanceToEnd <= commitDistance)
            {
                TryGetPlannedContinuation(out _, out _);
            }
        }

        private void ClearPlannedContinuation()
        {
            plannedConnection = null;
            plannedContinuationSegment = null;
            plannedContinuationWaypointIndex = -1;
            plannedContinuationSourceSegmentId = -1;
            hasPlannedContinuation = false;

            if (turnSignalController != null && !isChangingLanes && !isOvertaking)
            {
                turnSignalController.DeactivateAll();
            }
        }

        private bool TryGetNearbyContinuation(out RoadSegment nextSegment, out int nextWaypointIndex)
        {
            nextSegment = null;
            nextWaypointIndex = 0;

            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
                return false;

            List<RoadSegment> segments = roadGraphBuilder.RoadGraph.roadSegments;
            if (segments == null || segments.Count == 0 || currentSegment == null)
                return false;

            if (currentSegment.waypoints == null || currentSegment.waypoints.Count == 0)
                return false;

            int lastIndex = currentSegment.waypoints.Count - 1;
            Vector3 exitPosition = currentSegment.waypoints[lastIndex].position;
            Vector3 currentForward = GetConnectionTravelForward(currentSegment, Mathf.Max(0, lastIndex - 1));
            float maxDistanceSqr = nearbyContinuationSearchRadius * nearbyContinuationSearchRadius;
            float straightDotThreshold = Mathf.Cos(straightConnectionAngleThreshold * Mathf.Deg2Rad);
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < segments.Count; i++)
            {
                RoadSegment candidate = segments[i];
                if (candidate == null || candidate == currentSegment || candidate.waypoints == null || candidate.waypoints.Count < 2)
                {
                    continue;
                }

                Vector3 delta = candidate.waypoints[0].position - exitPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > maxDistanceSqr)
                {
                    continue;
                }

                Vector3 candidateForward = GetConnectionTravelForward(candidate, 0);
                float dot = Vector3.Dot(currentForward, candidateForward);
                if (dot < straightDotThreshold)
                {
                    continue;
                }

                float score = dot * 10f - Mathf.Sqrt(distanceSqr);
                if (score > bestScore)
                {
                    bestScore = score;
                    nextSegment = candidate;
                    nextWaypointIndex = 0;
                }
            }

            return nextSegment != null;
        }

        private float GetDistanceFromWaypointToSegmentEnd(RoadSegment segment, int startIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count < 2)
            {
                return float.MaxValue;
            }

            startIndex = Mathf.Clamp(startIndex, 0, segment.waypoints.Count - 1);
            float distance = 0f;
            for (int i = startIndex; i < segment.waypoints.Count - 1; i++)
            {
                distance += Vector3.Distance(segment.waypoints[i].position, segment.waypoints[i + 1].position);
            }

            return distance;
        }

        private RoadConnection SelectBestConnection(List<RoadConnection> connections)
        {
            if (connections == null || connections.Count == 0)
            {
                return null;
            }

            // Filter to only valid, forward-facing connections (exclude U-turns)
            Vector3 currentForward = GetSegmentExitForward(currentSegment);
            float straightDotThreshold = Mathf.Cos(straightConnectionAngleThreshold * Mathf.Deg2Rad);
            RoadConnection bestConnection = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < connections.Count; i++)
            {
                RoadConnection connection = connections[i];
                if (!TryGetConnectionAlignment(connection, currentForward, out float dot))
                {
                    continue;
                }

                float lanePreference = GetLanePreferenceMultiplier(connection.toSegment);
                float score = ScoreConnection(connection, currentForward, dot, lanePreference, straightDotThreshold);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestConnection = connection;
                }
            }

            return bestConnection;
        }

        private float ScoreConnection(RoadConnection connection, Vector3 currentForward, float dot, float lanePreference, float straightDotThreshold)
        {
            float score = dot * 8f;
            score += lanePreference;

            if (dot >= straightDotThreshold)
            {
                score += 8f;
            }

            if (connection == null || connection.toSegment == null)
            {
                return score;
            }

            Vector3 fromPos = currentSegment.waypoints[Mathf.Clamp(connection.fromWaypointIndex, 0, currentSegment.waypoints.Count - 1)].position;
            Vector3 toPos = connection.toSegment.waypoints[Mathf.Clamp(connection.toWaypointIndex, 0, connection.toSegment.waypoints.Count - 1)].position;
            Vector3 delta = toPos - fromPos;
            delta.y = 0f;
            score -= delta.magnitude * 0.15f;

            Vector3 right = Vector3.Cross(Vector3.up, currentForward);
            if (right.sqrMagnitude > 0.0001f)
            {
                right.Normalize();
                float side = Vector3.Dot(delta, right);
                if (enforceRightHandSingleLane && side > 0f)
                {
                    score += 0.5f;
                }
            }

            return score;
        }

        private void UpdateCommittedTurnSignal()
        {
            if (turnSignalController == null ||
                isChangingLanes ||
                isOvertaking ||
                !hasPlannedContinuation ||
                plannedContinuationSegment == null)
            {
                return;
            }

            Vector3 currentForward = GetSegmentExitForward(currentSegment);
            Vector3 nextForward = GetConnectionTravelForward(plannedContinuationSegment, plannedContinuationWaypointIndex);
            float turnAngle = Vector3.Angle(currentForward, nextForward);
            if (turnAngle <= 15f)
            {
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, currentForward);
            if (right.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float side = Vector3.Dot(nextForward, right.normalized);
            if (side >= 0f)
            {
                turnSignalController.ActivateRight();
            }
            else
            {
                turnSignalController.ActivateLeft();
            }
        }

        private float GetLanePreferenceMultiplier(RoadSegment segment)
        {
            if (!enforceRightHandSingleLane || segment == null || string.IsNullOrEmpty(segment.name))
            {
                return 1f;
            }

            string lowerName = segment.name.ToLowerInvariant();
            if (lowerName.Contains("lane_right") || lowerName.Contains("right_lane"))
            {
                return 1.25f;
            }

            if (lowerName.Contains("lane_left") || lowerName.Contains("left_lane"))
            {
                return 0.75f;
            }

            return 1f;
        }

        private Vector3 GetSegmentExitForward(RoadSegment segment)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                Vector3 worldForward = GetWorldForward();
                worldForward.y = 0f;
                return worldForward.sqrMagnitude < 0.0001f ? Vector3.forward : worldForward.normalized;
            }

            int exitIndex = Mathf.Max(0, segment.waypoints.Count - 2);
            if (exitIndex >= segment.waypoints.Count - 1)
            {
                exitIndex = Mathf.Max(0, segment.waypoints.Count - 1);
            }

            return GetConnectionTravelForward(segment, exitIndex);
        }

        private bool TryGetConnectionAlignment(RoadConnection connection, Vector3 currentForward, out float dot)
        {
            dot = -1f;

            if (connection == null || connection.toSegment == null || connection.toSegment == currentSegment)
            {
                return false;
            }

            if (connection.toSegment.waypoints == null || connection.toSegment.waypoints.Count < 2)
            {
                return false;
            }

            int toIndex = Mathf.Clamp(connection.toWaypointIndex, 0, connection.toSegment.waypoints.Count - 1);

            // Avoid transitions that enter at the segment end. Those create immediate reroutes and steering jitter.
            if (toIndex >= connection.toSegment.waypoints.Count - 1)
            {
                return false;
            }

            Vector3 candidateForward = GetConnectionTravelForward(connection.toSegment, toIndex);
            dot = Vector3.Dot(currentForward, candidateForward);

            float maxTurnDot = Mathf.Cos(Mathf.Clamp(maxConnectionTurnAngle, 0f, 180f) * Mathf.Deg2Rad);
            return dot >= maxTurnDot;
        }

        private Vector3 GetConnectionTravelForward(RoadSegment segment, int waypointIndex)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                return Vector3.forward;
            }

            waypointIndex = Mathf.Clamp(waypointIndex, 0, segment.waypoints.Count - 1);
            Vector3 forward = segment.waypoints[waypointIndex].forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                if (waypointIndex < segment.waypoints.Count - 1)
                {
                    forward = segment.waypoints[waypointIndex + 1].position - segment.waypoints[waypointIndex].position;
                }
                else if (waypointIndex > 0)
                {
                    forward = segment.waypoints[waypointIndex].position - segment.waypoints[waypointIndex - 1].position;
                }
            }

            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        private void ApplyLaneCenteringPolicyForCurrentSegment()
        {
            float nextCruiseOffset = GetCruisingLaneOffsetForCurrentSegment();
            cruisingLaneOffset = nextCruiseOffset;

            if (!isChangingLanes && !isOvertaking)
            {
                lateralOffset = cruisingLaneOffset;
                targetLateralOffset = cruisingLaneOffset;
                currentLateralOffset = cruisingLaneOffset;
            }
        }

        private float GetCruisingLaneOffsetForCurrentSegment()
        {
            if (!useFixedLaneCenters)
            {
                return 0f;
            }

            if (SegmentProvidesLaneCenter(currentSegment))
            {
                return 0f;
            }

            return preferredCruiseLaneSign * Mathf.Max(0.1f, laneCenterOffset);
        }

        private static bool SegmentProvidesLaneCenter(RoadSegment segment)
        {
            if (segment == null || string.IsNullOrEmpty(segment.name))
            {
                return false;
            }

            string lowerName = segment.name.ToLowerInvariant();
            if (lowerName.StartsWith("route_"))
            {
                return true;
            }

            return lowerName.Contains("lane_left") ||
                   lowerName.Contains("lane_right") ||
                   lowerName.Contains("right_lane") ||
                   lowerName.Contains("left_lane");
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
        /// Pure Pursuit steering algorithm
        /// </summary>
        private void UpdateSteeringTarget()
        {
            hasSteeringTarget = false;
            if (currentSegment?.waypoints == null || currentWaypointIndex < 0 ||
                currentWaypointIndex >= currentSegment.waypoints.Count)
                return;

            // Find lookahead point
            Vector3 lookAheadPoint = GetLookAheadPoint();
            currentLookAheadPoint = lookAheadPoint;

            // Project lookahead point to horizontal plane for better steering
            Vector3 flatLookAhead = lookAheadPoint;
            flatLookAhead.y = transform.position.y;

            // Calculate steering angle using Pure Pursuit
            Vector3 localTarget = Quaternion.Inverse(GetVehicleRotation()) * (flatLookAhead - transform.position);
            float safeMaxSteerAngle = Mathf.Max(1f, maxSteerAngle);
            float targetSteerAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            requestedSteerAngle = Mathf.Clamp(targetSteerAngle, -safeMaxSteerAngle, safeMaxSteerAngle);
            hasSteeringTarget = true;
        }

        private void ApplySteering()
        {
            if (!hasSteeringTarget)
                return;

            float safeMaxSteerAngle = Mathf.Max(1f, maxSteerAngle);
            float targetSteerAngle = Mathf.Clamp(requestedSteerAngle, -safeMaxSteerAngle, safeMaxSteerAngle);
            if (!hasSmoothedTargetSteerAngle)
            {
                smoothedTargetSteerAngle = targetSteerAngle;
                hasSmoothedTargetSteerAngle = true;
            }
            else
            {
                float maxDelta = Mathf.Max(1f, maxSteerTargetDeltaPerSecond) * Time.fixedDeltaTime;
                smoothedTargetSteerAngle = Mathf.MoveTowards(smoothedTargetSteerAngle, targetSteerAngle, maxDelta);
            }

            // Smooth steering with adaptive speed based on turn sharpness and personality
            float steerSpeed = personalitySteerSpeed * (1f + Mathf.Abs(smoothedTargetSteerAngle) / safeMaxSteerAngle);
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, smoothedTargetSteerAngle, Time.fixedDeltaTime * steerSpeed);

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
            Vector3 flatForward = GetPlanarTravelForward();

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
            bool previewCommittedPath =
                previewUpcomingConnections &&
                hasPlannedContinuation &&
                plannedContinuationSourceSegmentId == currentSegment.id &&
                plannedContinuationSegment != null;
            float remaining = Mathf.Max(1f, personalityLookAhead);
            if (previewCommittedPath)
            {
                float distanceToEnd = GetDistanceFromWaypointToSegmentEnd(currentSegment, startIndex);
                float nextSegmentPreviewDistance = Mathf.Max(2f, personalityLookAhead * 0.35f);
                float boostedLookAhead = personalityLookAhead * Mathf.Max(1f, intersectionLookAheadBoost);
                remaining = Mathf.Max(boostedLookAhead, distanceToEnd + nextSegmentPreviewDistance);
            }

            Vector3 targetPoint = currentSegment.waypoints[startIndex].position;
            Vector3 targetTangent = flatForward;
            bool targetFound = TryAdvanceLookAheadOnSegment(currentSegment, startIndex, ref remaining, ref targetPoint, ref targetTangent);

            if (!targetFound &&
                previewUpcomingConnections &&
                TryGetPlannedContinuation(out RoadSegment nextSegment, out int nextWaypointIndex))
            {
                Vector3 continuationStart = nextSegment.waypoints[nextWaypointIndex].position;
                Vector3 bridge = continuationStart - targetPoint;
                float bridgeLength = bridge.magnitude;

                if (bridgeLength > 0.0001f)
                {
                    Vector3 bridgeTangent = bridge / bridgeLength;
                    if (remaining <= bridgeLength)
                    {
                        float t = remaining / bridgeLength;
                        targetPoint = Vector3.Lerp(targetPoint, continuationStart, t);
                        targetTangent = bridgeTangent;
                        targetFound = true;
                    }
                    else
                    {
                        remaining -= bridgeLength;
                        targetPoint = continuationStart;
                        targetTangent = bridgeTangent;
                    }
                }

                if (!targetFound)
                {
                    targetFound = TryAdvanceLookAheadOnSegment(nextSegment, nextWaypointIndex, ref remaining, ref targetPoint, ref targetTangent);
                }
            }

            if (!targetFound)
            {
                targetPoint += targetTangent * remaining;
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

        private bool TryAdvanceLookAheadOnSegment(
            RoadSegment segment,
            int startIndex,
            ref float remaining,
            ref Vector3 targetPoint,
            ref Vector3 targetTangent)
        {
            if (segment == null || segment.waypoints == null || segment.waypoints.Count == 0)
            {
                return false;
            }

            int waypointCount = segment.waypoints.Count;
            startIndex = Mathf.Clamp(startIndex, 0, waypointCount - 1);
            Vector3 prev = segment.waypoints[startIndex].position;
            targetPoint = prev;
            targetTangent = GetConnectionTravelForward(segment, startIndex);

            for (int i = startIndex; i < waypointCount - 1; i++)
            {
                Vector3 next = segment.waypoints[i + 1].position;
                Vector3 pathSegment = next - prev;
                float segmentLength = pathSegment.magnitude;

                if (segmentLength > 0.0001f)
                {
                    Vector3 tangent = pathSegment / segmentLength;
                    if (remaining <= segmentLength)
                    {
                        float t = remaining / segmentLength;
                        targetPoint = Vector3.Lerp(prev, next, t);
                        targetTangent = tangent;
                        return true;
                    }

                    remaining -= segmentLength;
                    targetPoint = next;
                    targetTangent = tangent;
                }

                prev = next;
            }

            return false;
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
            mustStopForVehicle = false;
            vehicleStopDistance = float.MaxValue;

            if (!enableObstacleAvoidance) return;

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float forwardCastDistance = GetForwardVehicleAwarenessDistance();

            ScanForwardVehicles(rayOrigin, forward, right, forwardCastDistance);

            // Center raycast - main forward detection
            if (Physics.Raycast(rayOrigin, forward, out RaycastHit centerHit, forwardCastDistance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                bool centerBlocked = true;
                if (IsVehicleCollider(centerHit.collider))
                {
                    isObstacleDetected = true;
                    obstacleDistance = Mathf.Min(obstacleDistance, centerHit.distance);
                    EvaluateVehicleStopThreat(centerHit.collider, centerHit.distance);
                }
                else if (IsStaticWorldObstacle(centerHit.collider))
                {
                    isObstacleDetected = true;
                    obstacleDistance = Mathf.Min(obstacleDistance, centerHit.distance);
                    imminentCollisionRisk |= centerHit.distance < safeFollowingDistance;
                    mustStopForVehicle |= centerHit.distance < criticalBrakingDistance;
                    predictiveSpeedLimiter = Mathf.Min(predictiveSpeedLimiter, centerHit.distance < criticalBrakingDistance ? 0f : 0.35f);
                }
                else
                {
                    centerBlocked = false;
                }

                if (centerBlocked && enableLaneChange && obstacleDistance < safeFollowingDistance)
                {
                    CheckLaneAvailability(rayOrigin, forward, right);
                    DecideLaneChange();
                }
            }

            if (IsSideLaneBlocked(rayOrigin - right * sideRayOffset, forward))
                leftLaneClear = false;
            if (IsSideLaneBlocked(rayOrigin + right * sideRayOffset, forward))
                rightLaneClear = false;
        }

        private bool IsSideLaneBlocked(Vector3 origin, Vector3 forward) =>
            Physics.Raycast(origin, forward, out RaycastHit hit, sideRayDistance,
                obstacleLayerMask, QueryTriggerInteraction.Ignore) &&
            (IsVehicleCollider(hit.collider) || IsStaticWorldObstacle(hit.collider));

        private float GetForwardVehicleAwarenessDistance()
        {
            float speedMs = rb != null ? Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, GetWorldForward())) : 0f;
            float dynamicDistance = safeFollowingDistance + speedMs * Mathf.Max(0.1f, vehicleStopLookaheadTime);
            return Mathf.Clamp(Mathf.Max(avoidanceRayDistance, dynamicDistance), avoidanceRayDistance, Mathf.Max(avoidanceRayDistance, maxVehicleStopCastDistance));
        }

        private void ScanForwardVehicles(Vector3 origin, Vector3 forward, Vector3 right, float distance)
        {
            vehicleAhead = null;
            isFollowing = false;

            float threatRadius = Mathf.Max(0.4f, vehicleDetectionRadius);
            int count = CastForwardVehicles(origin, forward, distance, vehicleDetectionRadius);
            bool share = threatRadius == vehicleDetectionRadius && count < sharedSphereCastBuffer.Length;
            ScanForwardHits(origin, forward, right, count, true, share,
                out NpcCarAgent lead, out float leadDistance, out Collider threat, out float threatDistance);

            if (lead != null)
            {
                float relativeSpeed = CurrentSpeed / 3.6f - lead.CurrentSpeed / 3.6f;
                isFollowing = leadDistance < CalculateSafeFollowingDistance() + Mathf.Max(0f, relativeSpeed * 1.2f);
                distanceToVehicleAhead = leadDistance;
                relativeSpeedToVehicleAhead = relativeSpeed;
                vehicleAhead = lead;
            }

            // Different radius or a full NonAlloc buffer: keep the previous independent threat query.
            if (!share)
            {
                count = CastForwardVehicles(origin, forward, distance, threatRadius);
                ScanForwardHits(origin, forward, right, count, false, true,
                    out _, out _, out threat, out threatDistance);
            }

            if (threat == null)
                return;

            isObstacleDetected = true;
            obstacleDistance = Mathf.Min(obstacleDistance, threatDistance);
            EvaluateVehicleStopThreat(threat, threatDistance);
        }

        private int CastForwardVehicles(Vector3 origin, Vector3 forward, float distance, float radius) =>
            Physics.SphereCastNonAlloc(origin, radius, forward, sharedSphereCastBuffer, distance,
                obstacleLayerMask, QueryTriggerInteraction.Ignore);

        private void ScanForwardHits(Vector3 origin, Vector3 forward, Vector3 right, int count,
            bool findLead, bool findThreat, out NpcCarAgent lead, out float leadDistance,
            out Collider threat, out float threatDistance)
        {
            lead = null;
            threat = null;
            leadDistance = threatDistance = float.MaxValue;
            float cosAwareness = Mathf.Cos(forwardAwarenessAngle * Mathf.Deg2Rad);
            Vector3 position = transform.position;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sharedSphereCastBuffer[i];
                Collider collider = hit.collider;
                bool leadCandidate = findLead && hit.distance < leadDistance;
                bool threatCandidate = findThreat && hit.distance < threatDistance;
                if ((!leadCandidate && !threatCandidate) || !IsVehicleCollider(collider))
                    continue;

                Vector3 toHit = hit.point - origin;
                if (toHit.sqrMagnitude < 0.001f || Vector3.Dot(forward, toHit.normalized) < cosAwareness)
                    continue;

                float lateral = Mathf.Abs(Vector3.Dot(hit.point - position, right));
                if (leadCandidate && lateral <= sideRayOffset + 2.5f)
                {
                    NpcCarAgent agent = collider.GetComponent<NpcCarAgent>() ?? collider.GetComponentInParent<NpcCarAgent>();
                    if (agent != null && agent != this)
                    {
                        lead = agent;
                        leadDistance = hit.distance;
                    }
                }

                if (threatCandidate && lateral <= sideRayOffset + vehicleDetectionRadius + 1.5f)
                {
                    threat = collider;
                    threatDistance = hit.distance;
                }
            }
        }

        private void EvaluateVehicleStopThreat(Collider vehicleCollider, float distance)
        {
            if (vehicleCollider == null || rb == null)
            {
                return;
            }

            Rigidbody otherRigidbody = ResolveAttachedRigidbody(vehicleCollider);
            Vector3 forward = GetWorldForward();
            float currentForwardSpeed = Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, forward));
            float otherForwardSpeed = otherRigidbody != null ? Vector3.Dot(otherRigidbody.linearVelocity, forward) : 0f;
            float closingSpeed = currentForwardSpeed - otherForwardSpeed;

            float safeStopDistance = CalculateVehicleStopDistance(closingSpeed, currentForwardSpeed);
            float timeToCollision = closingSpeed > 0.1f ? distance / closingSpeed : float.MaxValue;

            bool shouldStop =
                distance <= Mathf.Max(1f, fullStopGapDistance) ||
                distance <= safeStopDistance ||
                timeToCollision <= Mathf.Max(0.1f, fullStopTimeToCollision);

            if (!shouldStop)
            {
                return;
            }

            mustStopForVehicle = true;
            imminentCollisionRisk = true;
            predictiveSpeedLimiter = Mathf.Min(predictiveSpeedLimiter, 0.1f);
            vehicleStopDistance = Mathf.Min(vehicleStopDistance, distance);
        }

        private float CalculateVehicleStopDistance(float closingSpeed, float currentForwardSpeed)
        {
            float safeDeceleration = Mathf.Max(1f, assumedEmergencyDeceleration);
            float positiveClosingSpeed = Mathf.Max(0f, closingSpeed);
            float closingBrakingDistance = (positiveClosingSpeed * positiveClosingSpeed) / (2f * safeDeceleration);
            float reactionDistance = Mathf.Max(0f, currentForwardSpeed) * 0.2f;
            return Mathf.Max(fullStopGapDistance, fullStopGapDistance + collisionBufferDistance + reactionDistance + closingBrakingDistance);
        }

        private static Rigidbody ResolveAttachedRigidbody(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.attachedRigidbody != null)
            {
                return collider.attachedRigidbody;
            }

            return collider.GetComponentInParent<Rigidbody>();
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
            if (isStaticObstacleCollision) frontBlocked = true;

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
                StartReverse(isStaticObstacleCollision ? staticObstacleReverseDuration : reverseDuration);
            }
        }

        private void StartReverse(float durationOverride = -1f)
        {
            isReversing = true;
            float duration = durationOverride > 0f ? durationOverride : reverseDuration;
            reverseTimer = Mathf.Max(0.2f, duration);
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

        private bool IsStaticWorldObstacle(Collider collider)
        {
            if (!enableStaticObstacleRecovery || collider == null)
            {
                return false;
            }

            if (collider.isTrigger || collider.transform.IsChildOf(transform) || HasVehicleController(collider))
            {
                return false;
            }

            Rigidbody attachedRigidbody = collider.attachedRigidbody;
            if (attachedRigidbody != null && !attachedRigidbody.isKinematic)
            {
                return false;
            }

            if (collider.GetComponent<Terrain>() != null)
            {
                return false;
            }

            int layer = collider.gameObject.layer;
            int roadLayer = LayerMask.NameToLayer("Road");
            int groundLayer = LayerMask.NameToLayer("Ground");
            if ((roadLayer >= 0 && layer == roadLayer) || (groundLayer >= 0 && layer == groundLayer))
            {
                return false;
            }

            string lowerName = collider.name.ToLowerInvariant();
            if (lowerName.Contains("road") ||
                lowerName.Contains("street") ||
                lowerName.Contains("asphalt") ||
                lowerName.Contains("sidewalk") ||
                lowerName.Contains("terrain") ||
                lowerName.Contains("ground"))
            {
                return false;
            }

            return true;
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

        // ============================================================================
        // PRIORITY 2: Predictive Behavior - Multi-Waypoint Lookahead (Step 3.1)
        // ============================================================================

        /// <summary>
        /// Analyze upcoming path for turns, intersections, and obstacles
        /// </summary>
        private void AnalyzeUpcomingPathAndTurn()
        {
            currentLookaheadData = default;
            currentTurnInfo = default;
            if (currentSegment?.waypoints == null || currentWaypointIndex < 0 ||
                currentWaypointIndex >= currentSegment.waypoints.Count)
                return;

            Vector3 position = transform.position;
            bool committedTurn = currentSegment.waypoints.Count >= 2 &&
                TryGetCommittedTurnInfo(position, out currentTurnInfo);
            bool scanLocalTurn = !committedTurn && currentWaypointIndex < currentSegment.waypoints.Count - 3;
            NpcPathTurnScan scan = NpcPathTurnScanner.Scan(currentSegment.waypoints, currentWaypointIndex, scanLocalTurn);

            var data = new LookaheadData
            {
                hasSharpTurn = scan.MaxLookaheadAngle > 15f,
                turnSharpness = scan.MaxLookaheadAngle,
                hasIntersection = currentSegment.connections != null && currentSegment.connections.Count > 0 &&
                    currentSegment.waypoints.Count - currentWaypointIndex < 15
            };
            data.recommendedSpeed = CalculatePlannedSpeed(data);
            currentLookaheadData = data;

            if (scan.LastTurnIndex >= 0)
            {
                currentTurnInfo = new TurnInfo
                {
                    isTurn = true,
                    distanceToTurn = Vector3.Distance(position, currentSegment.waypoints[scan.LastTurnIndex].position),
                    turnAngle = scan.MaxLocalTurnAngle,
                    recommendedSpeed = CalculateTurnSpeed(scan.MaxLocalTurnAngle)
                };
            }
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

        private bool TryGetCommittedTurnInfo(Vector3 currentPos, out TurnInfo turnInfo)
        {
            turnInfo = new TurnInfo();

            if (!hasPlannedContinuation ||
                plannedContinuationSourceSegmentId != currentSegment.id ||
                plannedContinuationSegment == null ||
                plannedContinuationSegment.waypoints == null ||
                plannedContinuationSegment.waypoints.Count < 2)
            {
                return false;
            }

            Vector3 currentForward = GetSegmentExitForward(currentSegment);
            Vector3 nextForward = GetConnectionTravelForward(plannedContinuationSegment, plannedContinuationWaypointIndex);
            float turnAngle = Vector3.Angle(currentForward, nextForward);
            if (turnAngle <= 15f)
            {
                return false;
            }

            int lastIndex = currentSegment.waypoints.Count - 1;
            Vector3 turnPoint = currentSegment.waypoints[lastIndex].position;
            turnInfo.isTurn = true;
            turnInfo.distanceToTurn = Vector3.Distance(currentPos, turnPoint);
            turnInfo.turnAngle = turnAngle;
            turnInfo.recommendedSpeed = CalculateTurnSpeed(turnAngle);
            return true;
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
            var traffic = TrafficCommunicationSystem.Instance;
            if (traffic == null) return;

            float speedMs = CurrentSpeed / 3.6f;
            float queryRadius = Mathf.Max(30f, speedMs * predictiveCollisionHorizon + 10f);
            Vector3 origin = transform.position;
            traffic.GetNearbyVehicles(origin, queryRadius, nearbyVehicleBuffer);
            if (queryRadius >= 20f && !float.IsInfinity(queryRadius))
            {
                predictionQuerySource = traffic;
                predictionQueryOrigin = origin;
            }
            float minSpeedLimiter = 1f;
            bool urgentRisk = false;
            float safeFollowingGap = CalculateSafeFollowingDistance();
            int sampleCount = Mathf.Max(3, predictiveCollisionSamples);
            float timeStep = predictiveCollisionHorizon / sampleCount;
            selfPredictionSamples.Clear();

            for (int i = 0; i < nearbyVehicleBuffer.Count; i++)
            {
                NpcCarAgent otherVehicle = nearbyVehicleBuffer[i];
                if (otherVehicle == this || otherVehicle == null) continue;

                // This agent's path and velocity do not change during the neighbor scan.
                if (selfPredictionSamples.Count == 0)
                {
                    for (int sample = 1; sample <= sampleCount; sample++)
                        selfPredictionSamples.Add(VehicleTrajectoryPredictor.PredictPositionWithPath(this, sample * timeStep));
                }

                float closestPredictedDistance = EstimateClosestPredictedDistance(otherVehicle, timeStep);
                float timeToCollision = VehicleTrajectoryPredictor.CalculateTimeToCollision(this, otherVehicle);
                float relativeClosingSpeed = CalculateRelativeClosingSpeed(otherVehicle);
                float dynamicSafeGap = safeFollowingGap + Mathf.Max(0f, relativeClosingSpeed * 1.5f) + collisionBufferDistance;

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

        private float EstimateClosestPredictedDistance(NpcCarAgent otherVehicle, float timeStep)
        {
            float closestDistance = Vector3.Distance(transform.position, otherVehicle.transform.position);
            for (int i = 0; i < selfPredictionSamples.Count; i++)
            {
                Vector3 otherPosition = VehicleTrajectoryPredictor.PredictPositionWithPath(otherVehicle, (i + 1) * timeStep);
                float distance = Vector3.Distance(selfPredictionSamples[i], otherPosition);
                if (distance < closestDistance)
                    closestDistance = distance;
            }
            return closestDistance;
        }

        private float CalculateRelativeClosingSpeed(NpcCarAgent otherVehicle)
        {
            if (otherVehicle == null) return 0f;

            Rigidbody otherRb = otherVehicle.rb != null ? otherVehicle.rb : otherVehicle.GetComponent<Rigidbody>();
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

            var traffic = TrafficCommunicationSystem.Instance;
            if (traffic == null) return false;

            Vector3 origin = transform.position;
            // The wider prediction query already includes the 20 m merge radius.
            // Never reuse results across physics ticks or after repositioning.
            bool reusePrediction = predictionQuerySource == traffic && predictionQueryOrigin.Equals(origin);
            if (!reusePrediction)
                traffic.GetNearbyVehicles(origin, 20f, nearbyVehicleBuffer);

            for (int i = 0; i < nearbyVehicleBuffer.Count; i++)
            {
                NpcCarAgent otherVehicle = nearbyVehicleBuffer[i];
                if (otherVehicle == this || otherVehicle == null || !otherVehicle.gameObject.activeInHierarchy) continue;
                Vector3 toOther = otherVehicle.transform.position - origin;
                if (reusePrediction && toOther.sqrMagnitude > 400f) continue;

                // Check if they're changing lanes towards us
                VehicleState state = traffic.GetVehicleState(otherVehicle);
                if (state != null && state.isChangingLanes)
                {
                    // Check if they're in adjacent lane
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
        private float GetTimeOfDayMultiplier() =>
            NpcTimeOfDayPolicy.GetSpeedMultiplier(System.DateTime.Now.Hour, personality != null && personality.riskTolerance > 0.6f);

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
        private float CalculateEffectiveTargetSpeed()
        {
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
            if (mustStopForVehicle)
            {
                effectiveTargetSpeed = 0f;
            }

            if (imminentCollisionRisk)
            {
                effectiveTargetSpeed = Mathf.Min(effectiveTargetSpeed, mustStopForVehicle ? 0f : 12f);
            }

            // Advanced obstacle-based speed adjustment
            // Be less aggressive when overtaking
            if (isObstacleDetected && !isOvertaking && !mustStopForVehicle)
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
            float minAllowedSpeed = mustStopForVehicle ? 0f : (trafficConstrained ? trafficConstrainedMinSpeed : freeFlowMinSpeed);
            return Mathf.Max(effectiveTargetSpeed, minAllowedSpeed);
        }

        private void ApplyThrottle(float effectiveTargetSpeed)
        {
            if (isReversing)
            {
                ApplyReverseThrottle();
                return;
            }

            // Use the current rigidbody speed and honor stop flags even with a cached plan.
            float currentSpeed = CurrentSpeed;
            effectiveTargetSpeed = NpcDrivePhysicsPolicy.LimitForwardTarget(
                effectiveTargetSpeed, mustStopForVehicle, imminentCollisionRisk);

            float speedDifference = effectiveTargetSpeed - currentSpeed;
            float currentSafeDistance = CalculateSafeFollowingDistance();
            bool tooCloseToLead = isFollowing && distanceToVehicleAhead < currentSafeDistance;

            // Apply motor torque or braking
            if (NpcDrivePhysicsPolicy.CanAccelerate(speedDifference, speedTolerance, mustStopForVehicle))
            {
                // Need to accelerate - use personalized acceleration
                float motorTorque = personalityAcceleration * Mathf.Clamp01(speedDifference / 20f);
                SetMotorTorque(motorTorque);

                // Release brakes
                SetBrakeTorque(0f);
            }
            else if (speedDifference < -speedTolerance || isObstacleDetected || tooCloseToLead || mustStopForVehicle)
            {
                // Need to slow down
                float brakeTorque;
                if (mustStopForVehicle)
                {
                    float gapRatio = Mathf.Clamp01(vehicleStopDistance / Mathf.Max(0.1f, fullStopGapDistance + collisionBufferDistance));
                    brakeTorque = braking * Mathf.Lerp(1.8f, 1.25f, gapRatio);
                }
                else if (isObstacleDetected && obstacleDistance < criticalBrakingDistance)
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

                if (mustStopForVehicle && vehicleStopDistance <= fullStopGapDistance && currentSpeed < 3f && rb != null)
                {
                    Vector3 forwardVelocity = Vector3.Project(rb.linearVelocity, GetWorldForward());
                    rb.linearVelocity -= forwardVelocity;
                }
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

            // Squared speed is sufficient for the existing quadratic downforce.
            if (downforceCoefficient > 0f)
            {
                float speedDownforce = downforceCoefficient * rb.linearVelocity.sqrMagnitude;
                float requestedDownforce = Mathf.Max(0f, baseDownforce) + speedDownforce;

                float maxDownforce = cachedWeight * Mathf.Max(0f, maxDownforceWeightFraction);
                if (maxDownforce > 0f)
                {
                    requestedDownforce = Mathf.Min(requestedDownforce, maxDownforce);
                }

                Vector3 downDirection = useLocalDownforceDirection ? -GetWorldUp() : Vector3.down;
                rb.AddForce(downDirection * requestedDownforce, ForceMode.Force);
            }

            if (antiRollStiffness > 0f && TrafficSimulationOptimizer.IsScheduledPhysicsTick(physicsTick, PhysicsPhase, StabilityCheckInterval))
            {
                ApplyAntiRoll(frontLeftCollider, frontRightCollider, antiRollStiffness);
                ApplyAntiRoll(rearLeftCollider, rearRightCollider, antiRollStiffness);
            }

            // Keep basic angular damping; avoid repeated rigidbody reads/writes.
            Vector3 angular = rb.angularVelocity;
            Vector3 limitedAngular = Vector3.ClampMagnitude(angular, 2f);
            Vector3 up = transform.up;
            float upDot = up.y;
            if (upDot < 0.95f && upDot > 0.3f)
            {
                if (enableSupplementalTiltCorrection)
                {
                    Vector3 tiltAxis = Vector3.Cross(up, Vector3.up);
                    rb.AddTorque(tiltAxis.normalized * (3000f * (1f - upDot)), ForceMode.Force);
                }

                limitedAngular *= 0.9f;
            }
            else if (upDot < 0.3f)
            {
                limitedAngular *= 0.8f;
            }

            if (!limitedAngular.Equals(angular))
                rb.angularVelocity = limitedAngular;
        }

        private void ApplyAntiRoll(WheelCollider left, WheelCollider right, float stiffness)
        {
            if (left == null || right == null) return;

            bool leftGrounded = left.GetGroundHit(out WheelHit hitL);
            bool rightGrounded = right.GetGroundHit(out WheelHit hitR);

            float leftTravel = leftGrounded ? GetSuspensionTravel(left, hitL) : 1f;
            float rightTravel = rightGrounded ? GetSuspensionTravel(right, hitR) : 1f;

            float antiRollForce = (leftTravel - rightTravel) * stiffness;
            if (antiRollForce == 0f)
                return;

            if (leftGrounded)
                rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
            if (rightGrounded)
                rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }

        private static float GetSuspensionTravel(WheelCollider wheel, WheelHit hit) =>
            NpcDrivePhysicsPolicy.SuspensionTravel(wheel.transform.InverseTransformPoint(hit.point).y,
                wheel.radius, wheel.suspensionDistance);

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
            ExitWaypointLod(false);
            currentSegment = segment;
            currentWaypointIndex = waypointIndex;
            InvalidateDrivePlan();
            ClearPlannedContinuation();
            hasSmoothedTargetSteerAngle = false;
            ApplyLaneCenteringPolicyForCurrentSegment();
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

        private Vector3 GetGroundedPosition(Vector3 desiredPosition) =>
            NpcGroundingService.GetGroundedPosition(transform, desiredPosition, groundRaycastHeight,
                groundRaycastDistance, groundMask, GetGroundClearanceOffset());

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

        private Vector3 GetPlanarTravelForward()
        {
            Vector3 forward = GetWorldForward();
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
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
            if (wheelVisualService == null)
            {
                return;
            }

            NpcCarWheelVisualSet wheelVisuals = wheelVisualService.ResolveWheelVisuals(
                frontLeftCollider,
                frontLeftWheelVisual,
                frontRightCollider,
                frontRightWheelVisual,
                rearLeftCollider,
                rearLeftWheelVisual,
                rearRightCollider,
                rearRightWheelVisual);

            frontLeftWheelVisual = wheelVisuals.FrontLeft;
            frontRightWheelVisual = wheelVisuals.FrontRight;
            rearLeftWheelVisual = wheelVisuals.RearLeft;
            rearRightWheelVisual = wheelVisuals.RearRight;
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

        /// <summary>
        /// Handle collision with other vehicles and static world obstacles
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

                SetCollisionSeparationDirection(collision);
            }
            else if (IsStaticWorldObstacle(collision.collider))
            {
                isColliding = true;
                isStaticObstacleCollision = true;
                staticObstacleCollisionTimer = 0f;
                obstacleDistance = 0f;
                isObstacleDetected = true;
                imminentCollisionRisk = true;
                mustStopForVehicle = true;
                SetCollisionSeparationDirection(collision);

                if (logCollisions)
                {
                    Debug.LogWarning($"[NpcCarAgent] {name} hit static obstacle {collision.collider.name}");
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
                mustStopForVehicle = true;
                imminentCollisionRisk = true;
                vehicleStopDistance = 0f;
                SetMotorTorque(0f);
                SetBrakeTorque(braking * 1.8f);
            }
            else if (IsStaticWorldObstacle(collision.collider))
            {
                isColliding = true;
                isStaticObstacleCollision = true;
                staticObstacleCollisionTimer += Time.fixedDeltaTime;
                obstacleDistance = 0f;
                isObstacleDetected = true;
                imminentCollisionRisk = true;
                mustStopForVehicle = true;
                predictiveSpeedLimiter = 0f;
                vehicleStopDistance = 0f;
                SetCollisionSeparationDirection(collision);

                bool rearClearEnough = !rearObstacleDetected || rearObstacleDistance > Mathf.Max(1.5f, reverseRearCheckDistance * 0.55f);
                if (!isReversing && reverseCooldownTimer <= 0f && rearClearEnough)
                {
                    StartReverse(staticObstacleReverseDuration);
                }

                TrySnapAfterStaticObstacleCollision();
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
            else if (IsStaticWorldObstacle(collision.collider))
            {
                isStaticObstacleCollision = false;
                staticObstacleCollisionTimer = 0f;
                isColliding = false;
            }
        }

        private void SetCollisionSeparationDirection(Collision collision)
        {
            Vector3 collisionNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
                collisionNormal += collision.GetContact(i).normal;

            if (collisionNormal.sqrMagnitude > 0.01f)
            {
                separationDirection = collisionNormal.normalized;
            }
        }

        private void TrySnapAfterStaticObstacleCollision()
        {
            if (staticObstacleCollisionTimer < Mathf.Max(0.1f, staticObstacleSnapDelay))
            {
                return;
            }

            if (Time.time - lastStaticObstacleSnapTime < Mathf.Max(0f, staticObstacleSnapCooldown))
            {
                return;
            }

            NpcRecovery recovery = GetComponent<NpcRecovery>();
            if (recovery == null)
            {
                return;
            }

            lastStaticObstacleSnapTime = Time.time;
            staticObstacleCollisionTimer = 0f;
            isStaticObstacleCollision = false;
            recovery.ForceRecovery();
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
