using System.Collections;
using DeliveryDriver.Optimization;
using TrafficSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates missing optimization managers and runs heavy world systems in a delayed phase.
/// </summary>
public class RuntimeOptimizationBootstrap : MonoBehaviour
{
    [SerializeField] private float phaseTwoDelaySeconds = 2f;
    [SerializeField] private float npcExtraDelaySeconds = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
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

        // Disable heavyweight runtime profilers by default in gameplay scenes.
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

        WorldChunkManager chunkManager = FindAnyObjectByType<WorldChunkManager>();
        if (chunkManager != null)
        {
            chunkManager.showDebugInfo = false;
            chunkManager.drawGizmos = false;
            chunkManager.updateInterval = 0.35f;
            chunkManager.maxChunkUpdatesPerFrame = 12;
            chunkManager.enableHardRadiusCulling = true;
            chunkManager.cullStaticRenderersOnly = false;
        }

        TrafficSimulationOptimizer trafficOptimizer = FindAnyObjectByType<TrafficSimulationOptimizer>();
        if (trafficOptimizer != null)
        {
            trafficOptimizer.showPerformanceStats = false;
            trafficOptimizer.autoFindPlayer = true;
        }

        // Create unified optimization controller (persists across scenes)
        if (UnifiedOptimizationController.Instance == null)
        {
            GameObject controllerObj = new GameObject("UnifiedOptimizationController");
            controllerObj.AddComponent<UnifiedOptimizationController>();
        }
    }

    private void StartPhaseTwoSystems()
    {
        RoadGraphBuilder roadGraphBuilder = FindAnyObjectByType<RoadGraphBuilder>();
        if (roadGraphBuilder != null)
        {
            // Avoid rebuilding if RoadGraphBuilder already built or has its own deferred build pending.
            if (!roadGraphBuilder.HasBuiltRoadGraph && !roadGraphBuilder.HasPendingBuild)
            {
                roadGraphBuilder.BeginBuildWithDelay(phaseTwoDelaySeconds);
            }
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
