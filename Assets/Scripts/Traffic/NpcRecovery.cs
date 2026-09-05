using UnityEngine;
using TrafficSystem.Spline;
using System.Collections;

namespace TrafficSystem
{
    /// <summary>
    /// Monitors NPC vehicle state and recovers it if stuck using maneuvers (reverse, steer).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(NpcCarAgent))]
    public class NpcRecovery : MonoBehaviour
    {
        private enum RecoveryState { Idle, Maneuvering }

        [Header("Stuck Detection")]
        [SerializeField] private float stuckSpeedThreshold = 2f;
        [SerializeField] private float stuckTimeSeconds = 8f;

        [Header("Maneuver Settings")]
        [SerializeField] private float reverseTorque = 600f;
        [SerializeField] private float forwardTorque = 800f;
        [SerializeField] private float maneuverSteerAngle = 30f;
        [SerializeField] private float phaseDuration = 1.5f;
        [SerializeField] private int maxManeuverAttempts = 3;

        [Header("Fallback Recovery")]
        [SerializeField] private float snapHeightOffset = 0.5f;
        [SerializeField] private float recoveryCooldownSeconds = 10f;

        private NpcCarAgent carAgent;
        private Rigidbody rb;
        private RoadNetworkManager networkManager;

        private RecoveryState currentState = RecoveryState.Idle;
        private float stuckTimer;
        private float lastRecoveryTime;
        private int maneuverAttempts;

        // Donut / Circling Detection
        private Vector3 donutAnchorPos;
        private float cumulativeYaw;
        private float lastYawAngle;
        private float donutTimer;

        private void Awake()
        {
            carAgent = GetComponent<NpcCarAgent>();
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            networkManager = RoadNetworkManager.Instance;
        }

        public void Initialize()
        {
            networkManager = RoadNetworkManager.Instance;
        }

        public void ConfigureForRuntime(bool enableDebug) { }

        private void Update()
        {
            if (currentState == RecoveryState.Maneuvering) return;

            UpdateStuckDetection();
            UpdateFlippedDetection();
            UpdateDonutDetection();
        }

        private void UpdateDonutDetection()
        {
            float currentYaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(lastYawAngle, currentYaw);
            lastYawAngle = currentYaw;

            // Check if vehicle has moved away from the anchor position
            if (Vector3.Distance(transform.position, donutAnchorPos) > 8f)
            {
                donutAnchorPos = transform.position;
                cumulativeYaw = 0f;
                donutTimer = 0f;
                return;
            }

            // If spinning continuously in one direction
            if (Mathf.Abs(deltaYaw) > 1.2f)
            {
                cumulativeYaw += deltaYaw;
                donutTimer += Time.deltaTime;

                if (Mathf.Abs(cumulativeYaw) >= 330f && donutTimer >= 2.5f)
                {
                    cumulativeYaw = 0f;
                    donutTimer = 0f;
                    TriggerTeleportRecovery("Circling / Donut Detected");
                }
            }
            else
            {
                cumulativeYaw = Mathf.MoveTowards(cumulativeYaw, 0f, 60f * Time.deltaTime);
                donutTimer = Mathf.MoveTowards(donutTimer, 0f, Time.deltaTime);
            }
        }

        private void UpdateStuckDetection()
        {
            if (carAgent.CurrentSpeedKmh < stuckSpeedThreshold && !carAgent.IsWaitingAtIntersection && !carAgent.IsWaitingInTraffic)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckTimeSeconds)
                {
                    StartCoroutine(PerformRecoveryManeuver());
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        private void UpdateFlippedDetection()
        {
            if (Vector3.Dot(transform.up, Vector3.up) < 0.2f)
            {
                TriggerTeleportRecovery("Flipped");
            }
        }

        private IEnumerator PerformRecoveryManeuver()
        {
            currentState = RecoveryState.Maneuvering;
            carAgent.IsControlledExternally = true;
            maneuverAttempts++;

            // Phase 1: Reverse and steer right
            yield return DriveStep(-reverseTorque, 0, maneuverSteerAngle, phaseDuration);

            // Phase 2: Forward and steer left
            yield return DriveStep(forwardTorque, 0, -maneuverSteerAngle, phaseDuration);

            // Check if still stuck
            yield return new WaitForSeconds(0.5f);
            
            if (carAgent.CurrentSpeedKmh < stuckSpeedThreshold && maneuverAttempts < maxManeuverAttempts)
            {
                // Try again with opposite steering
                yield return DriveStep(-reverseTorque, 0, -maneuverSteerAngle, phaseDuration);
                yield return DriveStep(forwardTorque, 0, maneuverSteerAngle, phaseDuration);
            }

            if (carAgent.CurrentSpeedKmh < stuckSpeedThreshold && maneuverAttempts >= maxManeuverAttempts)
            {
                TriggerTeleportRecovery("Persistent Stuck");
            }

            carAgent.IsControlledExternally = false;
            currentState = RecoveryState.Idle;
            maneuverAttempts = 0;
        }

        private IEnumerator DriveStep(float motor, float brake, float steer, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                carAgent.SetDriveInputs(motor, brake, steer);
                elapsed += Time.deltaTime;
                yield return null;
            }
            carAgent.SetDriveInputs(0, 2000, 0); // Brief brake
            yield return new WaitForSeconds(0.2f);
        }

        private void TriggerTeleportRecovery(string reason)
        {
            if (Time.time - lastRecoveryTime < recoveryCooldownSeconds) return;
            lastRecoveryTime = Time.time;

            Debug.Log($"[NpcRecovery] Teleporting {name} (Reason: {reason})");

            rb.isKinematic = true;
            if (networkManager != null && networkManager.roads.Count > 0)
            {
                var closestRoadInfo = networkManager.GetClosestRoadPoint(transform.position);
                SplineRoad road = closestRoadInfo.road ?? networkManager.GetRandomRoad();
                float t = closestRoadInfo.road != null ? closestRoadInfo.t : Random.value;
                Vector3 tangent = road != null ? road.GetTangent(t) : transform.forward;
                bool isForward = Vector3.Dot(transform.forward, tangent) >= 0f;
                carAgent.InitializeOnRoad(road, t, isForward, true);
            }
            transform.position += Vector3.up * snapHeightOffset;
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * 5f;
            rb.angularVelocity = Vector3.zero;
        }

        public void ForceRecovery() => TriggerTeleportRecovery("Manual");
    }
}
