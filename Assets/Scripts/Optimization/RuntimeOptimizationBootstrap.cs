using System.Collections;
using DeliveryDriver.Optimization;
using TrafficSystem;
using TrafficSystem.Spline;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates missing optimization managers and runs heavy world systems in a delayed phase.
/// </summary>
public class RuntimeOptimizationBootstrap : MonoBehaviour
{
    private const string GameSceneName = "Game";

    [SerializeField] private float phaseTwoDelaySeconds = 2f;
    [SerializeField] private float npcExtraDelaySeconds = 1f;
    private static bool SupportsRuntimeDiagnostics => Application.isEditor || Debug.isDebugBuild;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        if (activeScene.name != GameSceneName)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("RuntimeOptimizationBootstrap");
        bootstrapObject.AddComponent<RuntimeOptimizationBootstrap>();
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        // Let scene objects finish Awake/Start before phase orchestration starts.
        yield return null;

        EnsureOptimizationManagers();
        StartPhaseTwoSystems();

        Destroy(gameObject);
    }

    private void EnsureOptimizationManagers()
    {
        // PerformanceOptimizationManager removed - TrafficSimulationOptimizer handles all NPC throttling

        // Keep diagnostics dormant in normal gameplay builds unless explicitly running a debug/editor build.
        if (SupportsRuntimeDiagnostics)
        {
            MemoryProfiler memoryProfiler = FindAnyObjectByType<MemoryProfiler>();
            if (memoryProfiler != null)
            {
                memoryProfiler.enableMonitoring = false;
                memoryProfiler.showMemoryOverlay = false;
            }

            PerformanceRegressionDetector regressionDetector = FindAnyObjectByType<PerformanceRegressionDetector>();
            if (regressionDetector != null)
            {
                regressionDetector.enableAutoDetection = false;
            }
        }

        WorldChunkManager chunkManager = FindAnyObjectByType<WorldChunkManager>();
        if (chunkManager == null)
        {
            WorldChunk anyChunk = FindAnyObjectByType<WorldChunk>(FindObjectsInactive.Include);
            if (anyChunk != null)
            {
                chunkManager = EnsureManager<WorldChunkManager>("[Optimization] WorldChunkManager");
                chunkManager.autoDetectChunks = true;
                chunkManager.autoFindPlayer = true;
                chunkManager.nearRingDistance = 180f;
                chunkManager.midRingDistance = 320f;
                chunkManager.farRingDistance = 450f;
            }
        }

        if (chunkManager != null)
        {
            chunkManager.showDebugInfo = false;
            chunkManager.drawGizmos = false;
            chunkManager.updateInterval = 0.25f;
            chunkManager.maxChunkUpdatesPerFrame = 24;
            chunkManager.enableHardRadiusCulling = true;
            chunkManager.cullStaticRenderersOnly = false;
        }

        TrafficSimulationOptimizer trafficOptimizer = FindAnyObjectByType<TrafficSimulationOptimizer>();
        if (trafficOptimizer != null)
        {
            trafficOptimizer.showPerformanceStats = false;
            trafficOptimizer.autoFindPlayer = true;
        }

        ApplyDefaultCameraLayerCulling();

        // Create unified optimization controller (persists across scenes)
        if (UnifiedOptimizationController.Instance == null)
        {
            GameObject controllerObj = new GameObject("UnifiedOptimizationController");
            controllerObj.AddComponent<UnifiedOptimizationController>();
        }
    }

    private void ApplyDefaultCameraLayerCulling()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        if (mainCam.farClipPlane < 650f)
        {
            mainCam.farClipPlane = 650f;
        }

        if (mainCam.layerCullDistances == null || mainCam.layerCullDistances.Length != 32 || mainCam.layerCullDistances[0] == 0f)
        {
            float[] distances = new float[32];
            float defaultCull = mainCam.farClipPlane > 0 ? mainCam.farClipPlane : 1000f;
            for (int i = 0; i < 32; i++) distances[i] = defaultCull;

            SetLayerDistance(distances, "Props", 120f);
            SetLayerDistance(distances, "Nature", 250f);
            SetLayerDistance(distances, "Trees", 250f);
            SetLayerDistance(distances, "Buildings", 600f);
            SetLayerDistance(distances, "MiniMapBuilding", 600f);
            SetLayerDistance(distances, "Road", defaultCull);

            mainCam.layerCullDistances = distances;
        }
    }

    private static void SetLayerDistance(float[] distances, string layerName, float distance)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0 && layer < 32)
        {
            distances[layer] = distance;
        }
    }

    private void StartPhaseTwoSystems()
    {
        RoadNetworkManager roadNetworkManager = FindAnyObjectByType<RoadNetworkManager>();
        if (roadNetworkManager != null)
        {
            roadNetworkManager.RefreshNetwork();
        }

        NpcSpawner npcSpawner = FindAnyObjectByType<NpcSpawner>();
        if (npcSpawner != null)
        {
            // Avoid double spawning when NpcSpawner already started spawning on scene Start.
            if (!npcSpawner.HasPendingOrActiveSpawn)
            {
                npcSpawner.SpawnNpcsDeferred(phaseTwoDelaySeconds + npcExtraDelaySeconds);
            }
        }
    }

    private static T EnsureManager<T>(string objectName) where T : Component
    {
        T existing = FindAnyObjectByType<T>();
        if (existing != null)
        {
            return existing;
        }

        GameObject managerObject = new GameObject(objectName);
        return managerObject.AddComponent<T>();
    }
}
