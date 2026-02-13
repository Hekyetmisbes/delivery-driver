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
        EnsureManager<PerformanceOptimizationManager>("PerformanceOptimizationManager");

        WorldChunkManager chunkManager = FindAnyObjectByType<WorldChunkManager>();
        if (chunkManager != null)
        {
            chunkManager.showDebugInfo = false;
            chunkManager.drawGizmos = false;
            chunkManager.updateInterval = 0.35f;
            chunkManager.maxChunkUpdatesPerFrame = 12;
            chunkManager.enableHardRadiusCulling = true;
            chunkManager.cullStaticRenderersOnly = true;
        }

        TrafficSimulationOptimizer trafficOptimizer = FindAnyObjectByType<TrafficSimulationOptimizer>();
        if (trafficOptimizer != null)
        {
            trafficOptimizer.showPerformanceStats = false;
            trafficOptimizer.autoFindPlayer = true;
        }
    }

    private void StartPhaseTwoSystems()
    {
        RoadGraphBuilder roadGraphBuilder = FindAnyObjectByType<RoadGraphBuilder>();
        if (roadGraphBuilder != null)
        {
            roadGraphBuilder.BeginBuildWithDelay(phaseTwoDelaySeconds);
        }

        NpcSpawner npcSpawner = FindAnyObjectByType<NpcSpawner>();
        if (npcSpawner != null)
        {
            npcSpawner.SpawnNpcsDeferred(phaseTwoDelaySeconds + npcExtraDelaySeconds);
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
