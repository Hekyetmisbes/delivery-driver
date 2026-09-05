using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrafficSystem.Spline;

namespace TrafficSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class NpcCarAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WheelCollider frontLeftCollider;
        [SerializeField] private WheelCollider frontRightCollider;
        [SerializeField] private WheelCollider rearLeftCollider;
        [SerializeField] private WheelCollider rearRightCollider;

        [Header("Wheel Visuals")]
        [SerializeField] private Transform frontLeftWheelVisual;
        [SerializeField] private Transform frontRightWheelVisual;
        [SerializeField] private Transform rearLeftWheelVisual;
        [SerializeField] private Transform rearRightWheelVisual;
        [SerializeField] private bool autoResolveWheelReferences = true;
        [SerializeField] private bool autoCreateWheelVisualsIfMissing = true;

        [Header("Steering Settings")]
        [Tooltip("Lookahead distance for Pure Pursuit (meters)")]
        [SerializeField] private float lookAheadDistance = 10f;
        [Tooltip("Maximum steering angle (degrees)")]
        [SerializeField] private float maxSteerAngle = 35f;

        [Header("Speed Settings")]
        [Tooltip("Target cruise speed (km/h)")]
        [SerializeField] private float targetSpeed = 45f;
        [Tooltip("Speed when turning or near intersections (km/h)")]
        [SerializeField] private float curveSpeed = 25f;
        [Tooltip("Acceleration force")]
        [SerializeField] private float acceleration = 600f;
        [Tooltip("Braking force")]
        [SerializeField] private float braking = 1500f;
        [Tooltip("Torque ramp speed (anti-spin)")]
        [SerializeField] private float torqueRampTime = 0.5f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private float avoidanceRayDistance = 28f;
        [SerializeField] private float followDistance = 12f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        // Spline Navigation State
        private SplineRoad currentRoad;
        private bool isDrivingForwardOnRoad = true;
        private float currentT = 0f;

        // Physics State
        private float currentMotorTorque = 0f;
        private float currentBrakeTorque = 0f;

        // Path Planning
        private List<SplineRoad> plannedRoute = new List<SplineRoad>();
        private SplineRoad finalDestinationRoad;
        private const int MaxPlannedRouteSize = 20;
        private float nextPlanTime = 0f;
        private const float PlanCooldown = 2.0f;

        private Rigidbody rb;
        private float currentSteerAngle;
        private NpcCarWheelVisualService wheelVisualService;

        private bool isWaitingAtIntersection = false;
        private bool isWaitingInTraffic = false;
        private RoadIntersection currentIntersectionReserved = null;

        public SplineRoad CurrentRoad => currentRoad;
        public float CurrentT => currentT;
        public bool IsDrivingForwardOnRoad => isDrivingForwardOnRoad;
        public float CurrentSpeedKmh => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
        public bool IsWaitingAtIntersection => isWaitingAtIntersection;
        public bool IsWaitingInTraffic => isWaitingInTraffic;

        private static readonly Collider[] obstacleOverlapBuffer = new Collider[24];

        [Header("External Control")]
        public bool IsControlledExternally = false;

        public void SetDriveInputs(float motor, float brake, float steer)
        {
            if (frontLeftCollider == null) return;
            
            ApplyTorqueToWheels(motor, brake);
            
            frontLeftCollider.steerAngle = Mathf.Clamp(steer, -maxSteerAngle, maxSteerAngle);
            frontRightCollider.steerAngle = Mathf.Clamp(steer, -maxSteerAngle, maxSteerAngle);
            
            UpdateWheelVisuals();
        }

        private WheelCollider[] allWheelColliders;

        private void ApplyTorqueToWheels(float motor, float brake)
        {
            if (frontLeftCollider != null) frontLeftCollider.motorTorque = motor;
            if (frontRightCollider != null) frontRightCollider.motorTorque = motor;

            if (allWheelColliders != null && allWheelColliders.Length > 0)
            {
                for (int i = 0; i < allWheelColliders.Length; i++)
                {
                    if (allWheelColliders[i] != null)
                    {
                        allWheelColliders[i].brakeTorque = brake;
                    }
                }
            }
            else
            {
                if (frontLeftCollider != null) frontLeftCollider.brakeTorque = brake;
                if (frontRightCollider != null) frontRightCollider.brakeTorque = brake;
                if (rearLeftCollider != null) rearLeftCollider.brakeTorque = brake;
                if (rearRightCollider != null) rearRightCollider.brakeTorque = brake;
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            ResolveWheelCollidersIfNeeded();
            allWheelColliders = GetComponentsInChildren<WheelCollider>();

            // Calculate dynamic Center of Mass based on actual wheel axle heights (prevents flipping / sinking)
            float avgWheelY = 0.35f;
            if (allWheelColliders != null && allWheelColliders.Length > 0)
            {
                float sumY = 0f;
                for (int i = 0; i < allWheelColliders.Length; i++)
                {
                    sumY += transform.InverseTransformPoint(allWheelColliders[i].transform.position).y;
                }
                avgWheelY = sumY / allWheelColliders.Length;
            }

            // Stabilize CoM slightly below the wheel axle level
            rb.centerOfMass = new Vector3(0f, avgWheelY - 0.12f, 0.05f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            EnsureBodyCollider(avgWheelY);

            wheelVisualService = new NpcCarWheelVisualService(autoResolveWheelReferences, autoCreateWheelVisualsIfMissing);
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

        private void Start()
        {
            InitializeOnNetwork();
        }

        public void InitializeOnNetwork()
        {
            if (RoadNetworkManager.Instance == null || RoadNetworkManager.Instance.roads.Count == 0) return;

            if (currentRoad == null)
            {
                InitializeOnRoad(RoadNetworkManager.Instance.GetRandomRoad(), Random.value, Random.value > 0.5f, true);
            }
        }

        public void InitializeOnRoad(SplineRoad road, float t, bool isForwardDirection, bool snapToRoad)
        {
            if (road == null || !road.IsUsable)
            {
                return;
            }

            currentRoad = road;
            currentT = Mathf.Clamp01(t);
            isDrivingForwardOnRoad = isForwardDirection;
            nextPlanTime = 0f;

            plannedRoute.Clear();
            plannedRoute.Add(currentRoad);
            finalDestinationRoad = null;
            PlanRouteToRandomDestination();

            Vector3 forward = currentRoad.GetTangent(currentT);
            if (!isDrivingForwardOnRoad)
            {
                forward = -forward;
            }

            if (snapToRoad)
            {
                transform.position = currentRoad.GetLanePosition(currentT, isDrivingForwardOnRoad) + Vector3.up * GetGroundClearanceOffset();
            }

            if (forward.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private void PlanRouteToRandomDestination()
        {
            if (Time.time < nextPlanTime) return;
            if (RoadNetworkManager.Instance == null || RoadNetworkManager.Instance.roads.Count == 0) return;

            // If we have a destination and a healthy path to it, don't change anything
            if (finalDestinationRoad != null && plannedRoute.Count > 5) return;

            // If we reached our destination, clear it to pick a new one
            if (currentRoad == finalDestinationRoad)
            {
                finalDestinationRoad = null;
            }

            SplineRoad startRoad = plannedRoute.Count > 0 ? plannedRoute[plannedRoute.Count - 1] : currentRoad;
            if (startRoad == null) return;

            RoadIntersection exitIntersection = null;
            
            // Calculate which intersection to exit from at the end of our current plan
            if (startRoad == currentRoad)
            {
                exitIntersection = isDrivingForwardOnRoad ? startRoad.endIntersection : startRoad.startIntersection;
            }
            else
            {
                // We need to know how we entered startRoad to know where to exit
                SplineRoad prevRoad = plannedRoute.Count > 1 ? plannedRoute[plannedRoute.Count - 2] : currentRoad;
                RoadIntersection entryIntersection = (startRoad.startIntersection == prevRoad.startIntersection || startRoad.startIntersection == prevRoad.endIntersection)
                    ? startRoad.startIntersection : startRoad.endIntersection;
                
                exitIntersection = (entryIntersection == startRoad.startIntersection) ? startRoad.endIntersection : startRoad.startIntersection;
            }

            if (exitIntersection == null) 
            {
                AddRandomNeighborToRoute();
                return;
            }

            if (finalDestinationRoad == null)
            {
                finalDestinationRoad = RoadNetworkManager.Instance.GetRandomRoad();
            }

            if (finalDestinationRoad == null || finalDestinationRoad == startRoad) return;

            nextPlanTime = Time.time + PlanCooldown;
            List<SplineRoad> path = SplinePathfinder.FindRoadPath(startRoad, exitIntersection, finalDestinationRoad);
            
            if (path != null && path.Count > 0)
            {
                // APPEND instead of Clear to maintain continuity and prevent "forgetting" the next junction
                int startIndex = (path[0] == startRoad) ? 1 : 0;
                for (int i = startIndex; i < path.Count; i++)
                {
                    // Avoid cycles or redundant planning
                    if (!plannedRoute.Contains(path[i]))
                    {
                        plannedRoute.Add(path[i]);
                    }
                    if (plannedRoute.Count >= MaxPlannedRouteSize) break;
                }
            }
            else
            {
                AddRandomNeighborToRoute();
            }
        }

        private void AddRandomNeighborToRoute()
        {
            SplineRoad lastRoad = plannedRoute.Count > 0 ? plannedRoute[plannedRoute.Count - 1] : currentRoad;
            if (lastRoad == null) return;

            RoadIntersection targetIntersection = null;
            if (lastRoad == currentRoad)
            {
                targetIntersection = isDrivingForwardOnRoad ? lastRoad.endIntersection : lastRoad.startIntersection;
            }
            else
            {
                SplineRoad prevRoad = plannedRoute.Count > 1 ? plannedRoute[plannedRoute.Count - 2] : currentRoad;
                RoadIntersection entryIntersection = (lastRoad.startIntersection == prevRoad.startIntersection || lastRoad.startIntersection == prevRoad.endIntersection)
                    ? lastRoad.startIntersection : lastRoad.endIntersection;
                targetIntersection = (entryIntersection == lastRoad.startIntersection) ? lastRoad.endIntersection : lastRoad.startIntersection;
            }

            if (targetIntersection == null) return;

            List<SplineRoad> potentialNext = new List<SplineRoad>(targetIntersection.connectedRoads);
            potentialNext.Remove(lastRoad);

            if (potentialNext.Count > 0)
            {
                plannedRoute.Add(potentialNext[Random.Range(0, potentialNext.Count)]);
            }
        }

        private SplineRoad GetNextRoadForIntersection(RoadIntersection intersection)
        {
            if (intersection == null) return null;

            // 1. Check plannedRoute
            if (plannedRoute.Count > 1 && plannedRoute[0] == currentRoad)
            {
                SplineRoad plannedNext = plannedRoute[1];
                if (plannedNext != null && plannedNext != currentRoad && IsRoadConnectedTo(plannedNext, intersection))
                {
                    return plannedNext;
                }
            }
            else if (plannedRoute.Count > 0 && plannedRoute[0] != currentRoad)
            {
                SplineRoad plannedNext = plannedRoute[0];
                if (plannedNext != null && IsRoadConnectedTo(plannedNext, intersection))
                {
                    return plannedNext;
                }
            }

            // 2. Fallback to connected roads of intersection
            if (intersection.connectedRoads != null && intersection.connectedRoads.Count > 0)
            {
                List<SplineRoad> options = new List<SplineRoad>(intersection.connectedRoads.Count);
                for (int i = 0; i < intersection.connectedRoads.Count; i++)
                {
                    SplineRoad r = intersection.connectedRoads[i];
                    if (r != null && r != currentRoad && r.IsUsable)
                    {
                        options.Add(r);
                    }
                }

                if (options.Count > 0)
                {
                    SplineRoad picked = options[Random.Range(0, options.Count)];
                    if (plannedRoute.Count > 0 && plannedRoute[0] == currentRoad)
                    {
                        plannedRoute.Insert(1, picked);
                    }
                    else
                    {
                        plannedRoute.Add(picked);
                    }
                    return picked;
                }
            }

            return null;
        }

        private bool IsRoadConnectedTo(SplineRoad road, RoadIntersection intersection)
        {
            if (road == null || intersection == null) return false;
            if (road.startIntersection == intersection || road.endIntersection == intersection) return true;

            Vector3 jPos = intersection.transform.position;
            return Vector3.Distance(road.GetPoint(0f), jPos) < 10f || Vector3.Distance(road.GetPoint(1f), jPos) < 10f;
        }

        private bool DetermineEnterForward(SplineRoad nextRoad, RoadIntersection junction)
        {
            Vector3 junctionPos = junction != null ? junction.transform.position : transform.position;
            float distToStart = Vector3.Distance(nextRoad.GetPoint(0f), junctionPos);
            float distToEnd = Vector3.Distance(nextRoad.GetPoint(1f), junctionPos);
            return distToStart <= distToEnd;
        }

        public float GetGroundClearanceOffset()
        {
            ResolveWheelCollidersIfNeeded();

            float lowestWheelBottomLocalY = float.MaxValue;
            AddWheelBottomLocalY(frontLeftCollider, ref lowestWheelBottomLocalY);
            AddWheelBottomLocalY(frontRightCollider, ref lowestWheelBottomLocalY);
            AddWheelBottomLocalY(rearLeftCollider, ref lowestWheelBottomLocalY);
            AddWheelBottomLocalY(rearRightCollider, ref lowestWheelBottomLocalY);

            if (lowestWheelBottomLocalY == float.MaxValue)
            {
                return 0.5f;
            }

            return Mathf.Clamp(-lowestWheelBottomLocalY + 0.05f, 0.05f, 3f);
        }

        private void FixedUpdate()
        {
            if (IsControlledExternally) return;

            if (currentRoad == null)
            {
                InitializeOnNetwork();
                if (currentRoad == null) return;
            }

            if (!HasRequiredWheelColliders())
            {
                return;
            }

            if (plannedRoute.Count < 2 && Time.time >= nextPlanTime)
            {
                PlanRouteToRandomDestination();
            }

            // 1. Position on spline
            currentT = currentRoad.GetClosestT(transform.position);

            // Subsurface anti-fall protection: if car sinks below the road lane, rescue it immediately
            Vector3 expectedLanePos = currentRoad.GetLanePosition(currentT, isDrivingForwardOnRoad);
            if (transform.position.y < expectedLanePos.y - 1.4f || transform.position.y < -15f)
            {
                transform.position = expectedLanePos + Vector3.up * (GetGroundClearanceOffset() + 0.35f);
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.Project(rb.linearVelocity, transform.forward);
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // 2. Navigation State
            float distanceOnRoad = currentRoad.TToDistance(currentT);
            float totalLength = currentRoad.GetTotalLength();
            float distanceRemaining = isDrivingForwardOnRoad ? (totalLength - distanceOnRoad) : distanceOnRoad;

            // 3. Find lookahead target (Pure Pursuit) and handle intersection navigation
            float lookAheadDist = distanceOnRoad + (isDrivingForwardOnRoad ? lookAheadDistance : -lookAheadDistance);
            Vector3 targetPosition;
            RoadIntersection approachingIntersection = null;

            bool isApproachingEnd = (lookAheadDist > totalLength || lookAheadDist < 0f || distanceRemaining < lookAheadDistance);
            if (isApproachingEnd)
            {
                approachingIntersection = isDrivingForwardOnRoad ? currentRoad.endIntersection : currentRoad.startIntersection;

                if (approachingIntersection != null)
                {
                    SplineRoad nextRoad = GetNextRoadForIntersection(approachingIntersection);

                    if (nextRoad != null)
                    {
                        bool enterNextForward = DetermineEnterForward(nextRoad, approachingIntersection);
                        Vector3 exitPoint = currentRoad.GetLanePosition(isDrivingForwardOnRoad ? 1f : 0f, isDrivingForwardOnRoad);
                        Vector3 entryPoint = nextRoad.GetLanePosition(enterNextForward ? 0f : 1f, enterNextForward);

                        float distToExit = Vector3.Distance(transform.position, exitPoint);
                        float distToEntry = Vector3.Distance(transform.position, entryPoint);

                        // Robust handover condition:
                        // Trigger handover if near the exit, near the entry, or closer to the entry than exit
                        bool shouldTransition = (distanceRemaining < 2.5f) ||
                                                 (distToEntry < 3.5f) ||
                                                 (distToEntry < distToExit && distanceRemaining < 5.0f);

                        if (shouldTransition)
                        {
                            if (currentIntersectionReserved != null)
                            {
                                currentIntersectionReserved.ExitIntersection(this);
                                currentIntersectionReserved = null;
                            }
                            isWaitingAtIntersection = false;

                            if (plannedRoute.Count > 0 && plannedRoute[0] == currentRoad)
                                plannedRoute.RemoveAt(0);

                            currentRoad = nextRoad;
                            isDrivingForwardOnRoad = enterNextForward;
                            currentT = enterNextForward ? 0.02f : 0.98f;

                            // Immediate next-road target
                            float newTotalLen = currentRoad.GetTotalLength();
                            float newTargetDist = enterNextForward ? Mathf.Min(newTotalLen, lookAheadDistance) : Mathf.Max(0f, newTotalLen - lookAheadDistance);
                            targetPosition = currentRoad.GetLanePosition(currentRoad.DistanceToT(newTargetDist), enterNextForward);
                        }
                        else
                        {
                            // Vehicle is still approaching or traversing the intersection:
                            if (distanceRemaining > 3.0f && distToExit > 3.0f)
                            {
                                // Follow lane straight to intersection exit (avoids cutting corner curb)
                                float exitTargetDist = isDrivingForwardOnRoad ? Mathf.Min(totalLength, distanceOnRoad + lookAheadDistance) : Mathf.Max(0f, distanceOnRoad - lookAheadDistance);
                                targetPosition = currentRoad.GetLanePosition(currentRoad.DistanceToT(exitTargetDist), isDrivingForwardOnRoad);
                            }
                            else
                            {
                                // Smooth curve through intersection from exitPoint to entryPoint
                                Vector3 junctionCenter = approachingIntersection.transform.position;
                                Vector3 cornerMid = Vector3.Lerp(exitPoint, entryPoint, 0.5f);
                                Vector3 controlPoint = Vector3.Lerp(cornerMid, junctionCenter, 0.4f);

                                float tInter = Mathf.Clamp01(1f - (distToEntry / Mathf.Max(0.1f, distToExit + distToEntry)));
                                targetPosition = BezierUtility.EvaluateQuadratic(exitPoint, controlPoint, entryPoint, Mathf.Clamp01(tInter + 0.35f));
                            }
                        }
                    }
                    else
                    {
                        // No connecting road (dead end): reverse direction when reaching the end
                        if (distanceRemaining < 2.5f)
                        {
                            isDrivingForwardOnRoad = !isDrivingForwardOnRoad;
                            plannedRoute.Clear();
                            PlanRouteToRandomDestination();
                        }
                        float safeDist = isDrivingForwardOnRoad ? Mathf.Min(totalLength, distanceOnRoad + lookAheadDistance) : Mathf.Max(0f, distanceOnRoad - lookAheadDistance);
                        targetPosition = currentRoad.GetLanePosition(currentRoad.DistanceToT(safeDist), isDrivingForwardOnRoad);
                    }
                }
                else
                {
                    // No intersection reference: if reached dead end, reverse
                    if (distanceRemaining < 2.5f)
                    {
                        isDrivingForwardOnRoad = !isDrivingForwardOnRoad;
                        plannedRoute.Clear();
                        PlanRouteToRandomDestination();
                    }
                    float safeDist = isDrivingForwardOnRoad ? Mathf.Min(totalLength, distanceOnRoad + lookAheadDistance) : Mathf.Max(0f, distanceOnRoad - lookAheadDistance);
                    targetPosition = currentRoad.GetLanePosition(currentRoad.DistanceToT(safeDist), isDrivingForwardOnRoad);
                }
            }
            else
            {
                targetPosition = currentRoad.GetLanePosition(currentRoad.DistanceToT(lookAheadDist), isDrivingForwardOnRoad);
            }

            // 4. Intersection Yielding & Right of Way
            bool yieldForIntersection = false;
            if (approachingIntersection != null && distanceRemaining < 12f)
            {
                if (currentIntersectionReserved != approachingIntersection)
                {
                    bool canEnter = approachingIntersection.RequestEntry(this);
                    if (!canEnter)
                    {
                        yieldForIntersection = true;
                        isWaitingAtIntersection = true;
                    }
                    else
                    {
                        currentIntersectionReserved = approachingIntersection;
                        isWaitingAtIntersection = false;
                    }
                }
            }
            else if (distanceRemaining >= 12f && currentIntersectionReserved != null)
            {
                // Reset if car turned back or route changed
                currentIntersectionReserved.ExitIntersection(this);
                currentIntersectionReserved = null;
                isWaitingAtIntersection = false;
            }

            // 5. Dynamic Speed Adjustment
            float commandedSpeed = targetSpeed;
            if (distanceRemaining < 15f || yieldForIntersection)
            {
                commandedSpeed = curveSpeed;
            }

            if (yieldForIntersection)
            {
                // Decelerate smoothly to the stop line before intersection
                float stopBuffer = Mathf.Max(0f, distanceRemaining - 2.5f);
                if (stopBuffer <= 0.8f)
                {
                    commandedSpeed = 0f;
                }
                else
                {
                    commandedSpeed = Mathf.Min(commandedSpeed, stopBuffer * 3f);
                }
            }

            // 6. Multi-Sensor Obstacle Avoidance & Adaptive Cruise Control (ACC)
            bool emergencyStop = yieldForIntersection && (distanceRemaining <= 3f);
            isWaitingInTraffic = false;

            if (CheckObstaclesAhead(targetPosition, out float closestDist, out Rigidbody leadVehicleRb))
            {
                if (closestDist < followDistance * 0.45f)
                {
                    commandedSpeed = 0f;
                    emergencyStop = true;
                    isWaitingInTraffic = true;
                }
                else if (closestDist < followDistance)
                {
                    if (leadVehicleRb != null)
                    {
                        float leadSpeedKmh = leadVehicleRb.linearVelocity.magnitude * 3.6f;
                        if (leadSpeedKmh < 2f)
                        {
                            // Lead vehicle stopped (in traffic or red light)
                            float gapToStop = closestDist - (followDistance * 0.45f);
                            if (gapToStop <= 1.2f)
                            {
                                commandedSpeed = 0f;
                                emergencyStop = true;
                                isWaitingInTraffic = true;
                            }
                            else
                            {
                                commandedSpeed = Mathf.Min(commandedSpeed, gapToStop * 3.5f);
                            }
                        }
                        else
                        {
                            // Match lead speed smoothly with proportional following distance
                            float gapRatio = (closestDist - followDistance * 0.45f) / (followDistance * 0.55f);
                            float matchedSpeed = Mathf.Lerp(leadSpeedKmh * 0.85f, leadSpeedKmh, gapRatio);
                            commandedSpeed = Mathf.Min(commandedSpeed, matchedSpeed);
                        }
                    }
                    else
                    {
                        float factor = (closestDist - followDistance * 0.45f) / (followDistance * 0.55f);
                        commandedSpeed *= Mathf.Clamp01(factor);
                    }
                }
                else if (closestDist < avoidanceRayDistance)
                {
                    float t_approach = (closestDist - followDistance) / (avoidanceRayDistance - followDistance);
                    commandedSpeed = Mathf.Lerp(curveSpeed, targetSpeed, t_approach);
                }
            }

            // 7. Steering (Pure Pursuit with Anti-Donut Safeguard)
            Vector3 localTarget = transform.InverseTransformPoint(targetPosition);

            // ANTI-DONUT SAFEGUARD:
            // If the target point is behind the vehicle's front axle, advance forward along spline
            if (localTarget.z < 0.5f)
            {
                float forwardLookahead = distanceOnRoad + (isDrivingForwardOnRoad ? (lookAheadDistance + 8f) : -(lookAheadDistance + 8f));
                forwardLookahead = Mathf.Clamp(forwardLookahead, 0f, totalLength);
                Vector3 altTarget = currentRoad.GetLanePosition(currentRoad.DistanceToT(forwardLookahead), isDrivingForwardOnRoad);
                Vector3 altLocal = transform.InverseTransformPoint(altTarget);
                if (altLocal.z > 0.5f)
                {
                    targetPosition = altTarget;
                    localTarget = altLocal;
                }
            }

            // Using Mathf.Max(0.1f, localTarget.z) ensures the steering angle is always calculated
            // in the forward hemisphere, preventing 360-degree donut orbits.
            float forwardZ = Mathf.Max(0.1f, localTarget.z);
            float targetAngle = Mathf.Atan2(localTarget.x, forwardZ) * Mathf.Rad2Deg;
            currentSteerAngle = Mathf.Clamp(targetAngle, -maxSteerAngle, maxSteerAngle);

            frontLeftCollider.steerAngle = currentSteerAngle;
            frontRightCollider.steerAngle = currentSteerAngle;

            // 8. Dynamic Cornering Speed & Motor / Brakes
            float absAngle = Mathf.Abs(targetAngle);
            if (absAngle > 15f)
            {
                float turnSeverity = Mathf.Clamp01((absAngle - 15f) / 35f);
                float maxCornerSpeed = Mathf.Lerp(curveSpeed, 10f, turnSeverity); // 10 to 18 km/h on sharp turns
                commandedSpeed = Mathf.Min(commandedSpeed, maxCornerSpeed);
            }

            // If car is facing backwards relative to target (localTarget.z < 0.2f):
            if (localTarget.z < 0.2f)
            {
                // Brake gently to avoid runaway spin
                commandedSpeed = Mathf.Min(commandedSpeed, 4f);
            }

            float speedDiff = commandedSpeed - CurrentSpeedKmh;
            float targetMotor = 0f;
            float targetBrake = 0f;

            if (emergencyStop || speedDiff < -2.5f)
            {
                targetBrake = braking;
                targetMotor = 0f;
            }
            else if (speedDiff > 1f && localTarget.z > 0.1f)
            {
                targetMotor = acceleration;
                targetBrake = 0f;
            }
            else if (localTarget.z <= 0.1f)
            {
                targetBrake = braking * 0.3f;
                targetMotor = 0f;
            }

            // Smoothly ramp torque to prevent skidding
            currentMotorTorque = Mathf.MoveTowards(currentMotorTorque, targetMotor, (acceleration / torqueRampTime) * Time.fixedDeltaTime);
            currentBrakeTorque = Mathf.MoveTowards(currentBrakeTorque, targetBrake, (braking / 0.2f) * Time.fixedDeltaTime);

            ApplyTorqueToWheels(currentMotorTorque, currentBrakeTorque);
            ApplyAntiRollBars();
            ApplyUprightStability();
            UpdateWheelVisuals();
        }

        private bool CheckObstaclesAhead(Vector3 targetPosition, out float closestDistance, out Rigidbody leadVehicleRb)
        {
            closestDistance = float.MaxValue;
            leadVehicleRb = null;

            Vector3 origin = transform.position + Vector3.up * 0.6f;
            Vector3 forward = transform.forward;
            Vector3 toTarget = (targetPosition - origin).normalized;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f) toTarget.Normalize();
            else toTarget = forward;

            // 1. Direct Forward SphereCast
            CheckRay(origin, forward, avoidanceRayDistance, 0.85f, ref closestDistance, ref leadVehicleRb);

            // 2. Curve Lookahead Cast (follows the spline lane ahead!)
            if (Vector3.Angle(forward, toTarget) > 4f)
            {
                CheckRay(origin, toTarget, avoidanceRayDistance * 0.85f, 0.75f, ref closestDistance, ref leadVehicleRb);
            }

            // 3 & 4. Left and Right Whiskers (±12 degrees, 12m length) for cut-ins and merging
            Vector3 leftWhisker = Quaternion.Euler(0f, -12f, 0f) * forward;
            Vector3 rightWhisker = Quaternion.Euler(0f, 12f, 0f) * forward;
            CheckRay(origin, leftWhisker, 12f, 0.6f, ref closestDistance, ref leadVehicleRb);
            CheckRay(origin, rightWhisker, 12f, 0.6f, ref closestDistance, ref leadVehicleRb);

            // 5. Flank / Lateral Proximity Detection (Yielding for Side-by-Side & Merging Traffic)
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, 4.2f, obstacleOverlapBuffer, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = obstacleOverlapBuffer[i];
                if (!IsBlockingObstacle(col)) continue;

                Rigidbody otherRb = col.attachedRigidbody;
                if (otherRb == null || otherRb == rb) continue;

                Vector3 localOther = transform.InverseTransformPoint(otherRb.position);
                // Within lateral distance of 3.8m and longitudinal zone (-1.8m to +5.5m)
                if (Mathf.Abs(localOther.x) < 3.8f && localOther.z > -1.8f && localOther.z < 5.5f)
                {
                    // Check if other is the Player vehicle: NPC always yields to the Player!
                    bool otherIsPlayer = otherRb.GetComponent<CarController>() != null || col.GetComponentInParent<CarController>() != null;

                    // If other is ahead of us, or side-by-side and has priority
                    bool shouldYield = otherIsPlayer || localOther.z > 0.25f || (Mathf.Abs(localOther.z) <= 0.25f && otherRb.GetInstanceID() > rb.GetInstanceID());

                    if (shouldYield)
                    {
                        float effectiveDist = Mathf.Max(1.0f, localOther.z + 1.2f);
                        if (effectiveDist < closestDistance)
                        {
                            closestDistance = effectiveDist;
                            leadVehicleRb = otherRb;
                        }
                    }
                }
            }

            return closestDistance < float.MaxValue;
        }

        private void CheckRay(Vector3 origin, Vector3 dir, float distance, float radius, ref float closestDist, ref Rigidbody closestRb)
        {
            if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, distance, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsBlockingObstacle(hit.collider))
                {
                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        closestRb = hit.rigidbody;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (currentIntersectionReserved != null)
            {
                currentIntersectionReserved.ExitIntersection(this);
                currentIntersectionReserved = null;
            }
            isWaitingAtIntersection = false;
            isWaitingInTraffic = false;
        }

        private void UpdateWheelVisuals()
        {
            if (wheelVisualService != null)
            {
                wheelVisualService.UpdateWheelVisuals(
                    frontLeftCollider,
                    frontLeftWheelVisual,
                    frontRightCollider,
                    frontRightWheelVisual,
                    rearLeftCollider,
                    rearLeftWheelVisual,
                    rearRightCollider,
                    rearRightWheelVisual);
                return;
            }

            UpdateWheelPose(frontLeftCollider, frontLeftWheelVisual);
            UpdateWheelPose(frontRightCollider, frontRightWheelVisual);
            UpdateWheelPose(rearLeftCollider, rearLeftWheelVisual);
            UpdateWheelPose(rearRightCollider, rearRightWheelVisual);
        }

        private void UpdateWheelPose(WheelCollider col, Transform vis)
        {
            if (col == null || vis == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            vis.position = pos;
            vis.rotation = rot;
        }

        private bool IsBlockingObstacle(Collider collider)
        {
            if (collider == null || collider.isTrigger || collider.transform.root == transform.root)
            {
                return false;
            }

            if (collider is TerrainCollider)
            {
                return false;
            }

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer >= 0 && collider.gameObject.layer == roadLayer)
            {
                return false;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                string objectName = current.name;
                if (!string.IsNullOrEmpty(objectName) &&
                    objectName.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                current = current.parent;
            }

            return true;
        }

        private bool HasRequiredWheelColliders()
        {
            return frontLeftCollider != null &&
                   frontRightCollider != null &&
                   rearLeftCollider != null &&
                   rearRightCollider != null;
        }

        private void ResolveWheelCollidersIfNeeded()
        {
            if (!autoResolveWheelReferences || HasRequiredWheelColliders())
            {
                return;
            }

            WheelCollider[] wheelColliders = GetComponentsInChildren<WheelCollider>();
            if (wheelColliders == null || wheelColliders.Length < 4)
            {
                return;
            }

            List<WheelCollider> sortedColliders = new List<WheelCollider>(wheelColliders);
            sortedColliders.Sort((a, b) =>
            {
                Vector3 localA = transform.InverseTransformPoint(a.transform.position);
                Vector3 localB = transform.InverseTransformPoint(b.transform.position);
                int zCompare = localB.z.CompareTo(localA.z);
                if (zCompare != 0)
                {
                    return zCompare;
                }

                return localA.x.CompareTo(localB.x);
            });

            AssignWheelPair(sortedColliders, 0, ref frontLeftCollider, ref frontRightCollider);
            AssignWheelPair(sortedColliders, sortedColliders.Count - 2, ref rearLeftCollider, ref rearRightCollider);
        }

        private void AssignWheelPair(List<WheelCollider> sortedColliders, int startIndex, ref WheelCollider left, ref WheelCollider right)
        {
            if (startIndex < 0 || startIndex + 1 >= sortedColliders.Count)
            {
                return;
            }

            WheelCollider first = sortedColliders[startIndex];
            WheelCollider second = sortedColliders[startIndex + 1];
            float firstX = transform.InverseTransformPoint(first.transform.position).x;
            float secondX = transform.InverseTransformPoint(second.transform.position).x;

            if (firstX <= secondX)
            {
                if (left == null) left = first;
                if (right == null) right = second;
            }
            else
            {
                if (left == null) left = second;
                if (right == null) right = first;
            }
        }

        private void AddWheelBottomLocalY(WheelCollider wheelCollider, ref float lowestWheelBottomLocalY)
        {
            if (wheelCollider == null)
            {
                return;
            }

            Vector3 wheelCenterWorld = wheelCollider.transform.TransformPoint(wheelCollider.center);
            float wheelCenterLocalY = transform.InverseTransformPoint(wheelCenterWorld).y;
            float wheelBottomLocalY = wheelCenterLocalY - Mathf.Max(0.01f, wheelCollider.radius);
            if (wheelBottomLocalY < lowestWheelBottomLocalY)
            {
                lowestWheelBottomLocalY = wheelBottomLocalY;
            }
        }

        private void ApplyAntiRollBars()
        {
            ApplyAntiRollOnAxle(frontLeftCollider, frontRightCollider, 7500f);
            ApplyAntiRollOnAxle(rearLeftCollider, rearRightCollider, 7500f);
        }

        private void ApplyAntiRollOnAxle(WheelCollider left, WheelCollider right, float stiffness)
        {
            if (left == null || right == null || stiffness <= 0f) return;

            float leftTravel = 1f;
            float rightTravel = 1f;
            bool leftGrounded = false;
            bool rightGrounded = false;
            float leftSuspension = Mathf.Max(0.001f, left.suspensionDistance);
            float rightSuspension = Mathf.Max(0.001f, right.suspensionDistance);

            if (left.GetGroundHit(out WheelHit hit))
            {
                leftGrounded = true;
                leftTravel = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / leftSuspension;
                leftTravel = Mathf.Clamp01(leftTravel);
            }

            if (right.GetGroundHit(out hit))
            {
                rightGrounded = true;
                rightTravel = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / rightSuspension;
                rightTravel = Mathf.Clamp01(rightTravel);
            }

            float antiRollForce = (leftTravel - rightTravel) * stiffness;

            if (leftGrounded)
            {
                rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position, ForceMode.Force);
            }

            if (rightGrounded)
            {
                rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position, ForceMode.Force);
            }
        }

        private void ApplyUprightStability()
        {
            if (rb == null) return;
            Vector3 currentUp = transform.up;
            float tiltAngle = Vector3.Angle(currentUp, Vector3.up);
            if (tiltAngle > 10f)
            {
                Vector3 restoreTorque = Vector3.Cross(currentUp, Vector3.up) * (rb.mass * 32f);
                rb.AddTorque(restoreTorque, ForceMode.Force);
            }
        }

        private void EnsureBodyCollider(float avgWheelY)
        {
            // Check if there is already a non-trigger body collider (BoxCollider or MeshCollider)
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger && !(colliders[i] is WheelCollider))
                {
                    return; // Body collider already present
                }
            }

            // If missing (like Ambulance or Lorry), dynamically generate a safe BoxCollider from renderers
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer) continue;
                if (!initialized)
                {
                    bounds = renderers[i].bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!initialized) return;

            BoxCollider bodyBox = gameObject.AddComponent<BoxCollider>();
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            // Raise the bottom of the body box slightly above wheel contact to prevent dragging on road
            float bottomLimit = avgWheelY - 0.10f;
            float boxBottom = localCenter.y - (localSize.y * 0.5f);
            if (boxBottom < bottomLimit)
            {
                float deficit = bottomLimit - boxBottom;
                localSize.y = Mathf.Max(0.5f, localSize.y - deficit);
                localCenter.y += deficit * 0.5f;
            }

            // Inset X slightly to avoid wheel rubbing
            localSize.x = Mathf.Max(0.5f, localSize.x * 0.90f);

            bodyBox.center = localCenter;
            bodyBox.size = localSize;
            bodyBox.isTrigger = false;
        }

        private void OnDrawGizmos()
        {
            if (currentRoad != null)
            {
                float dist = currentRoad.TToDistance(currentT) + (isDrivingForwardOnRoad ? lookAheadDistance : -lookAheadDistance);
                Vector3 lookPoint = currentRoad.GetLanePosition(currentRoad.DistanceToT(dist), isDrivingForwardOnRoad);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(lookPoint, 0.5f);
                Gizmos.DrawLine(transform.position, lookPoint);
            }
        }
    }
}
