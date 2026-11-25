using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrafficSystem.Spline;

namespace DeliveryDriver.City
{
    public enum CitySectorType
    {
        Ankara,
        HighwaysAndBridges,
        Istanbul,
        Custom
    }

    /// <summary>
    /// Manages city-level streaming and the transition corridor (bridges/highways).
    /// Optimizes performance by activating only the current city and its adjacent connectors,
    /// culling the distant city entirely (0 draw calls, 0 physics cost).
    /// </summary>
    public class CityStreamingManager : MonoBehaviour
    {
        public static CityStreamingManager Instance { get; private set; }

        public static event System.Action<CitySectorType> OnSectorChanged;
        public static string CurrentSectorName => Instance != null ? Instance.currentSector.ToString() : "Ankara";

        [Header("Player Tracking")]
        [Tooltip("Transform to track (e.g. player vehicle). If null, auto-finds with tag 'Player' or CarController.")]
        [SerializeField] private Transform playerTransform;

        [Header("City Roots")]
        [Tooltip("Root GameObject for Ankara content (placed roads, buildings, props)")]
        [SerializeField] private GameObject ankaraRoot;

        [Tooltip("Root GameObject for Istanbul content (placed roads, buildings, props)")]
        [SerializeField] private GameObject istanbulRoot;

        [Tooltip("Root GameObject for connecting bridges and highways (the transition corridor)")]
        [SerializeField] private GameObject highwaysAndBridgesRoot;

        [Header("Streaming Settings")]
        [Tooltip("How often to check player sector position (seconds)")]
        [SerializeField] private float updateInterval = 0.5f;

        [Tooltip("Center position for Ankara zone (used if zone triggers are not present)")]
        [SerializeField] private Vector3 ankaraCenter = Vector3.zero;

        [Tooltip("Radius around Ankara center where player is considered inside Ankara")]
        [SerializeField] private float ankaraRadius = 600f;

        [Tooltip("Center position for Istanbul zone")]
        [SerializeField] private Vector3 istanbulCenter = new Vector3(1500f, 0f, 0f);

        [Tooltip("Radius around Istanbul center where player is considered inside Istanbul")]
        [SerializeField] private float istanbulRadius = 600f;

        [Tooltip("Buffer distance to prevent rapid toggling near boundaries")]
        [SerializeField] private float hysteresisBuffer = 50f;

        [Header("Current State")]
        [SerializeField] private CitySectorType currentSector = CitySectorType.Ankara;
        [SerializeField] private bool inTransitionCorridor = false;

        public CitySectorType CurrentSector => currentSector;
        public bool InTransitionCorridor => inTransitionCorridor;

        private float nextCheckTime;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Enforce safe radii to prevent premature city deactivation
            if (ankaraRadius < 1000f) ankaraRadius = 1200f;
            if (istanbulRadius < 1000f) istanbulRadius = 1200f;

            AutoFindRootsIfNull();
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Apply initial state
            EvaluateSector(true);
        }

        private void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + updateInterval;

            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            EvaluateSector(false);
        }

        private void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                return;
            }

            var car = Object.FindFirstObjectByType<CarController>();
            if (car != null)
            {
                playerTransform = car.transform;
            }
        }

        public void AutoFindRootsIfNull()
        {
            if (ankaraRoot == null)
            {
                GameObject ankara = GameObject.Find("Ankara");
                if (ankara != null) ankaraRoot = ankara;
            }

            if (istanbulRoot == null)
            {
                GameObject istanbul = GameObject.Find("İstanbul") ?? GameObject.Find("Istanbul");
                if (istanbul != null) istanbulRoot = istanbul;
            }

            if (highwaysAndBridgesRoot == null)
            {
                GameObject hw = GameObject.Find("Highways & Bridges") ?? GameObject.Find("Highways");
                if (hw != null) highwaysAndBridgesRoot = hw;
            }
        }

        /// <summary>
        /// Called when the player physically enters or exits a TransitionCorridorZone (e.g. bridge trigger)
        /// </summary>
        public void SetTransitionCorridorState(bool insideCorridor, CitySectorType nextCityHint = CitySectorType.HighwaysAndBridges)
        {
            inTransitionCorridor = insideCorridor;
            if (insideCorridor)
            {
                SetSector(CitySectorType.HighwaysAndBridges);
            }
            else
            {
                // Re-evaluate based on actual player coordinates (never blindly cull based on heading vector)
                EvaluateSector(true);
            }
        }

        private void EvaluateSector(bool force)
        {
            if (inTransitionCorridor && !force) return;

            Vector3 pos = playerTransform != null ? playerTransform.position : Vector3.zero;
            float distToAnkara = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(ankaraCenter.x, 0, ankaraCenter.z));
            float distToIstanbul = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(istanbulCenter.x, 0, istanbulCenter.z));

            CitySectorType detectedSector = currentSector;

            if (currentSector == CitySectorType.Ankara)
            {
                // To leave Ankara, player must move beyond ankaraRadius + hysteresis
                if (distToAnkara > ankaraRadius + hysteresisBuffer)
                {
                    if (distToIstanbul < istanbulRadius)
                    {
                        detectedSector = CitySectorType.Istanbul;
                    }
                    else
                    {
                        detectedSector = CitySectorType.HighwaysAndBridges; // On the bridge/highway
                    }
                }
            }
            else if (currentSector == CitySectorType.Istanbul)
            {
                if (distToIstanbul > istanbulRadius + hysteresisBuffer)
                {
                    if (distToAnkara < ankaraRadius)
                    {
                        detectedSector = CitySectorType.Ankara;
                    }
                    else
                    {
                        detectedSector = CitySectorType.HighwaysAndBridges;
                    }
                }
            }
            else // Currently in Highways & Bridges
            {
                if (distToAnkara <= ankaraRadius)
                {
                    detectedSector = CitySectorType.Ankara;
                }
                else if (distToIstanbul <= istanbulRadius)
                {
                    detectedSector = CitySectorType.Istanbul;
                }
            }

            if (force || detectedSector != currentSector)
            {
                SetSector(detectedSector);
            }
        }

        private void SetSector(CitySectorType newSector)
        {
            CitySectorType previousSector = currentSector;
            currentSector = newSector;

            ApplyStreamingVisibility();

            if (previousSector != newSector)
            {
                OnSectorChanged?.Invoke(newSector);
                Debug.Log($"[CityStreamingManager] Aktif Sektör Değişti: {previousSector} -> {newSector}");
            }
        }

        private void ApplyStreamingVisibility()
        {
            // Transition Corridor (Bridge) is ALWAYS active when nearby or crossing
            if (highwaysAndBridgesRoot != null)
            {
                highwaysAndBridgesRoot.SetActive(true);
            }

            Vector3 pos = playerTransform != null ? playerTransform.position : Vector3.zero;
            float distToAnkara = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(ankaraCenter.x, 0, ankaraCenter.z));
            float distToIstanbul = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(istanbulCenter.x, 0, istanbulCenter.z));

            // CRITICAL SAFETY SHIELD:
            // Never deactivate a city if the player is physically closer to it,
            // or within that city's radius + safety buffer.
            bool shouldKeepAnkara = (currentSector == CitySectorType.Ankara) ||
                                    (currentSector == CitySectorType.HighwaysAndBridges) ||
                                    (distToAnkara <= distToIstanbul) ||
                                    (distToAnkara <= ankaraRadius + 200f);

            bool shouldKeepIstanbul = (currentSector == CitySectorType.Istanbul) ||
                                      (currentSector == CitySectorType.HighwaysAndBridges) ||
                                      (distToIstanbul < distToAnkara && distToIstanbul <= istanbulRadius + 200f);

            if (ankaraRoot != null)
            {
                ankaraRoot.SetActive(shouldKeepAnkara);
            }

            if (istanbulRoot != null)
            {
                istanbulRoot.SetActive(shouldKeepIstanbul);
            }

            // Refresh road network so AI & Pathfinder use the currently active roads
            if (RoadNetworkManager.Instance != null)
            {
                RoadNetworkManager.Instance.RefreshNetwork();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw Ankara zone
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(ankaraCenter, ankaraRadius);

            // Draw Istanbul zone
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.35f);
            Gizmos.DrawWireSphere(istanbulCenter, istanbulRadius);
        }
    }
}
