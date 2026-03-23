using UnityEngine;
using UnityEngine.Profiling;
using TrafficSystem;

namespace DeliveryDriver.Optimization
{
    public class UnifiedOptimizationController : MonoBehaviour
    {
        public static UnifiedOptimizationController Instance { get; private set; }

        [Header("Profile")]
        [SerializeField] private OptimizationProfile activeProfile;

        [Header("Dynamic Quality")]
        [SerializeField] private bool enableDynamicQuality = true;
        [SerializeField] private int downgradeThresholdFPS = 45;
        [SerializeField] private int upgradeThresholdFPS = 75;

        [Header("Memory Monitoring")]
        [SerializeField] private long memoryWarningThresholdMB = 1024;

        [Header("Monitoring")]
        [SerializeField] private float monitoringInterval = 1f;

        // Cached system references
        private TrafficSimulationOptimizer trafficOptimizer;
        private WorldChunkManager worldChunkManager;
        private TrafficCommunicationSystem trafficComms;
        private Camera mainCamera;

        // Performance tracking
        private float[] fpsHistory = new float[300];
        private int fpsHistoryIndex;
        private float nextMonitorTime;
        private float currentFPS;
        private float smoothedFPS;
        private long currentMemoryMB;
        private bool memoryPressure;
        private int currentQualityLevel;

        // FPS calculation
        private int frameCount;
        private float fpsAccumulator;
        private float fpsNextUpdate;
        private const float FPS_UPDATE_INTERVAL = 0.5f;

        public float CurrentFPS => currentFPS;
        public float SmoothedFPS => smoothedFPS;
        public long CurrentMemoryMB => currentMemoryMB;
        public bool MemoryPressure => memoryPressure;
        public float[] FPSHistory => fpsHistory;
        public int FPSHistoryIndex => fpsHistoryIndex;
        public OptimizationProfile ActiveProfile => activeProfile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            CacheSystemReferences();
            mainCamera = Camera.main;
            currentQualityLevel = QualitySettings.GetQualityLevel();

            if (activeProfile != null)
            {
                ApplyProfile(activeProfile);
            }
            else
            {
                ApplyAggressiveDefaults();
            }
        }

        private void Update()
        {
            // FPS calculation
            frameCount++;
            fpsAccumulator += Time.unscaledDeltaTime;

            if (Time.unscaledTime >= fpsNextUpdate)
            {
                currentFPS = frameCount / fpsAccumulator;
                smoothedFPS = Mathf.Lerp(smoothedFPS, currentFPS, 0.3f);
                frameCount = 0;
                fpsAccumulator = 0f;
                fpsNextUpdate = Time.unscaledTime + FPS_UPDATE_INTERVAL;
            }

            // Periodic monitoring
            if (Time.unscaledTime >= nextMonitorTime)
            {
                nextMonitorTime = Time.unscaledTime + monitoringInterval;
                UpdateMonitoring();
            }
        }

        private void UpdateMonitoring()
        {
            // Record FPS history
            fpsHistory[fpsHistoryIndex] = currentFPS;
            fpsHistoryIndex = (fpsHistoryIndex + 1) % fpsHistory.Length;

            // Memory check
            currentMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            memoryPressure = currentMemoryMB > memoryWarningThresholdMB;

            // Dynamic quality adjustment
            if (enableDynamicQuality)
            {
                AdjustQuality();
            }
        }

        private void AdjustQuality()
        {
            if (smoothedFPS < downgradeThresholdFPS && currentQualityLevel > 0)
            {
                currentQualityLevel--;
                QualitySettings.SetQualityLevel(currentQualityLevel, true);
                Debug.Log($"[OptController] Quality lowered to {QualitySettings.names[currentQualityLevel]} (FPS: {smoothedFPS:F0})");
            }
            else if (smoothedFPS > upgradeThresholdFPS && currentQualityLevel < QualitySettings.names.Length - 1)
            {
                currentQualityLevel++;
                QualitySettings.SetQualityLevel(currentQualityLevel, true);
                Debug.Log($"[OptController] Quality raised to {QualitySettings.names[currentQualityLevel]} (FPS: {smoothedFPS:F0})");
            }
        }

        public void CacheSystemReferences()
        {
            trafficOptimizer = FindAnyObjectByType<TrafficSimulationOptimizer>();
            worldChunkManager = FindAnyObjectByType<WorldChunkManager>();
            trafficComms = TrafficCommunicationSystem.Instance;
        }

        public void ApplyProfile(OptimizationProfile profile)
        {
            if (profile == null) return;
            activeProfile = profile;

            // Traffic
            if (trafficOptimizer != null)
            {
                trafficOptimizer.nearDistance = profile.nearDistance;
                trafficOptimizer.midDistance = profile.midDistance;
                trafficOptimizer.farDistance = profile.farDistance;
                trafficOptimizer.veryFarDistance = profile.veryFarDistance;
                trafficOptimizer.stateUpdateInterval = profile.stateUpdateInterval;
            }

            // World streaming
            if (worldChunkManager != null)
            {
                worldChunkManager.chunkSize = profile.chunkSize;
                worldChunkManager.nearRingDistance = profile.nearRing;
                worldChunkManager.midRingDistance = profile.midRing;
                worldChunkManager.farRingDistance = profile.farRing;
                worldChunkManager.updateInterval = profile.chunkUpdateInterval;
                worldChunkManager.maxChunkUpdatesPerFrame = profile.maxChunkUpdatesPerFrame;
                worldChunkManager.cacheRefreshInterval = profile.cacheRefreshInterval;
            }

            // Rendering
            QualitySettings.shadowDistance = profile.shadowDistance;
            QualitySettings.shadowCascades = profile.shadowCascades;
            QualitySettings.antiAliasing = profile.antiAliasing;

            // Layer culling
            ApplyLayerCulling(profile.layerCullingDistances);

            // Physics
            Time.fixedDeltaTime = profile.fixedTimestep;
            Physics.defaultSolverIterations = profile.solverIterations;
            Physics.defaultSolverVelocityIterations = profile.solverVelocityIterations;

            Debug.Log($"[OptController] Applied profile: {profile.name}");
        }

        private void ApplyAggressiveDefaults()
        {
            QualitySettings.shadowDistance = 25f;
            QualitySettings.shadowCascades = 1;
            ApplyLayerCulling(null);
            Debug.Log("[OptController] Applied aggressive defaults (no profile)");
        }

        private void ApplyLayerCulling(OptimizationProfile.LayerCullingEntry[] entries)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            float[] distances = new float[32];
            float defaultCull = mainCamera.farClipPlane;

            // Start with far clip plane for all layers
            for (int i = 0; i < 32; i++)
                distances[i] = defaultCull;

            if (entries != null && entries.Length > 0)
            {
                foreach (var entry in entries)
                {
                    int layerId = LayerMask.NameToLayer(entry.layerName);
                    if (layerId >= 0)
                        distances[layerId] = entry.distance;
                }
            }
            else
            {
                SetLayerCull(distances, "Water", 500f);
            }

            mainCamera.layerCullDistances = distances;
            mainCamera.layerCullSpherical = true;
            Debug.Log("[OptController] Layer culling applied");
        }

        private static void SetLayerCull(float[] distances, string layerName, float distance)
        {
            int id = LayerMask.NameToLayer(layerName);
            if (id >= 0) distances[id] = distance;
        }

        // Public accessors for dashboard
        public TrafficSimulationOptimizer TrafficOptimizer => trafficOptimizer;
        public WorldChunkManager ChunkManager => worldChunkManager;
        public TrafficCommunicationSystem TrafficComms => trafficComms;
    }
}
