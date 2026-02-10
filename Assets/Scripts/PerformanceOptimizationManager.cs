using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central manager for performance optimizations
/// Handles layer-based culling, quality adjustments, and update throttling
/// Based on "Unity Large Grid City on Terrain Optimization Playbook"
/// </summary>
public class PerformanceOptimizationManager : MonoBehaviour
{
    public static PerformanceOptimizationManager Instance { get; private set; }

    [Header("Culling Distances")]
    [Tooltip("Layer culling distances (meters) - assign layers in inspector")]
    [SerializeField] private LayerCullingDistance[] layerCullingDistances = new LayerCullingDistance[]
    {
        new LayerCullingDistance { layerName = "Default", distance = 500f },
        new LayerCullingDistance { layerName = "SmallProps", distance = 50f },
        new LayerCullingDistance { layerName = "MediumProps", distance = 150f },
        new LayerCullingDistance { layerName = "Buildings", distance = 800f },
        new LayerCullingDistance { layerName = "Terrain", distance = 1000f }
    };

    [Header("NPC Update Throttling")]
    [Tooltip("Enable distance-based NPC update throttling")]
    [SerializeField] private bool enableNpcThrottling = true;
    [Tooltip("Near distance - update every frame (meters)")]
    [SerializeField] private float nearDistance = 50f;
    [Tooltip("Mid distance - update every N frames (meters)")]
    [SerializeField] private float midDistance = 150f;
    [Tooltip("Far distance - update every N frames (meters)")]
    [SerializeField] private float farDistance = 300f;
    [Tooltip("Update frequency for mid-distance NPCs (frames)")]
    [SerializeField] private int midDistanceUpdateInterval = 2;
    [Tooltip("Update frequency for far-distance NPCs (frames)")]
    [SerializeField] private int farDistanceUpdateInterval = 4;

    [Header("Quality Level Adjustments")]
    [Tooltip("Automatically adjust quality based on FPS")]
    [SerializeField] private bool autoAdjustQuality = false;
    [Tooltip("Target FPS for auto-adjustment")]
    [SerializeField] private float targetFPS = 60f;
    [Tooltip("FPS threshold before downgrading quality")]
    [SerializeField] private float fpsDowngradeThreshold = 45f;
    [Tooltip("FPS threshold before upgrading quality")]
    [SerializeField] private float fpsUpgradeThreshold = 70f;
    [Tooltip("Time before quality adjustment (seconds)")]
    [SerializeField] private float qualityAdjustmentDelay = 5f;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerTransform;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Runtime data
    private Dictionary<int, float> layerCullingMap = new Dictionary<int, float>();
    private float[] recentFPS = new float[60];
    private int fpsIndex = 0;
    private float lastQualityAdjustTime = 0f;
    private int frameCount = 0;

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
        InitializeOptimizations();
    }

    private void InitializeOptimizations()
    {
        // Find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Setup layer culling distances
        SetupLayerCulling();

        Debug.Log($"[PerformanceOptimizationManager] Initialized - NPC Throttling: {enableNpcThrottling}, Auto Quality: {autoAdjustQuality}");
    }

    /// <summary>
    /// Configure layer-based culling distances on the main camera
    /// </summary>
    private void SetupLayerCulling()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("[PerformanceOptimizationManager] Main camera not found, skipping layer culling setup");
            return;
        }

        layerCullingMap.Clear();

        foreach (var layerCulling in layerCullingDistances)
        {
            int layerId = LayerMask.NameToLayer(layerCulling.layerName);
            if (layerId >= 0)
            {
                layerCullingMap[layerId] = layerCulling.distance;
            }
            else
            {
                Debug.LogWarning($"[PerformanceOptimizationManager] Layer '{layerCulling.layerName}' not found in project");
            }
        }

        // Apply culling distances to camera
        float[] distances = new float[32];
        for (int i = 0; i < 32; i++)
        {
            distances[i] = layerCullingMap.ContainsKey(i) ? layerCullingMap[i] : mainCamera.farClipPlane;
        }

        mainCamera.layerCullDistances = distances;

        if (showDebugInfo)
        {
            Debug.Log($"[PerformanceOptimizationManager] Applied culling distances to {layerCullingMap.Count} layers");
        }
    }

    private void Update()
    {
        frameCount++;

        // Track FPS for auto-adjustment
        if (autoAdjustQuality)
        {
            TrackFPS();
            CheckQualityAdjustment();
        }
    }

    /// <summary>
    /// Determine update interval for an NPC based on distance from player
    /// Returns: 1 = every frame, 2 = every 2 frames, 4 = every 4 frames, etc.
    /// </summary>
    public int GetNpcUpdateInterval(Vector3 npcPosition)
    {
        if (!enableNpcThrottling || playerTransform == null)
        {
            return 1; // Update every frame
        }

        float distance = Vector3.Distance(npcPosition, playerTransform.position);

        if (distance <= nearDistance)
        {
            return 1; // Near: every frame
        }
        else if (distance <= midDistance)
        {
            return midDistanceUpdateInterval; // Mid: every 2 frames (default)
        }
        else if (distance <= farDistance)
        {
            return farDistanceUpdateInterval; // Far: every 4 frames (default)
        }
        else
        {
            return farDistanceUpdateInterval * 2; // Very far: every 8 frames
        }
    }

    /// <summary>
    /// Check if NPC should update this frame based on throttling
    /// </summary>
    public bool ShouldNpcUpdate(Vector3 npcPosition, int npcId)
    {
        if (!enableNpcThrottling)
        {
            return true;
        }

        int interval = GetNpcUpdateInterval(npcPosition);
        if (interval == 1)
        {
            return true;
        }

        // Use npcId to distribute updates across frames
        return (frameCount + npcId) % interval == 0;
    }

    /// <summary>
    /// Track FPS for auto-adjustment
    /// </summary>
    private void TrackFPS()
    {
        float currentFPS = 1f / Time.unscaledDeltaTime;
        recentFPS[fpsIndex] = currentFPS;
        fpsIndex = (fpsIndex + 1) % recentFPS.Length;
    }

    /// <summary>
    /// Get average FPS over recent frames
    /// </summary>
    private float GetAverageFPS()
    {
        float sum = 0f;
        foreach (float fps in recentFPS)
        {
            sum += fps;
        }
        return sum / recentFPS.Length;
    }

    /// <summary>
    /// Check if quality level should be adjusted based on FPS
    /// </summary>
    private void CheckQualityAdjustment()
    {
        if (Time.time - lastQualityAdjustTime < qualityAdjustmentDelay)
        {
            return;
        }

        float avgFPS = GetAverageFPS();
        int currentQuality = QualitySettings.GetQualityLevel();

        // Downgrade if FPS is too low
        if (avgFPS < fpsDowngradeThreshold && currentQuality > 0)
        {
            QualitySettings.DecreaseLevel(true);
            lastQualityAdjustTime = Time.time;
            Debug.Log($"[PerformanceOptimizationManager] Quality decreased to {QualitySettings.names[QualitySettings.GetQualityLevel()]} (FPS: {avgFPS:F1})");
        }
        // Upgrade if FPS is high
        else if (avgFPS > fpsUpgradeThreshold && currentQuality < QualitySettings.names.Length - 1)
        {
            QualitySettings.IncreaseLevel(true);
            lastQualityAdjustTime = Time.time;
            Debug.Log($"[PerformanceOptimizationManager] Quality increased to {QualitySettings.names[QualitySettings.GetQualityLevel()]} (FPS: {avgFPS:F1})");
        }
    }

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    public PerformanceMetrics GetMetrics()
    {
        return new PerformanceMetrics
        {
            averageFPS = GetAverageFPS(),
            currentQualityLevel = QualitySettings.GetQualityLevel(),
            qualityLevelName = QualitySettings.names[QualitySettings.GetQualityLevel()],
            frameCount = frameCount
        };
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 350, 350, 200));
        GUI.color = Color.green;
        GUILayout.Label("<b>PERFORMANCE OPTIMIZATION</b>");
        GUILayout.Label($"FPS: {GetAverageFPS():F1}");
        GUILayout.Label($"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        GUILayout.Label($"NPC Throttling: {(enableNpcThrottling ? "ON" : "OFF")}");
        GUILayout.Label($"Frame: {frameCount}");

        if (playerTransform != null)
        {
            GUILayout.Label($"Player: {playerTransform.position}");
        }

        if (GUILayout.Button("Decrease Quality"))
        {
            if (QualitySettings.GetQualityLevel() > 0)
            {
                QualitySettings.DecreaseLevel(true);
            }
        }
        if (GUILayout.Button("Increase Quality"))
        {
            if (QualitySettings.GetQualityLevel() < QualitySettings.names.Length - 1)
            {
                QualitySettings.IncreaseLevel(true);
            }
        }

        GUILayout.EndArea();
    }

    [System.Serializable]
    public class LayerCullingDistance
    {
        public string layerName;
        public float distance;
    }

    public struct PerformanceMetrics
    {
        public float averageFPS;
        public int currentQualityLevel;
        public string qualityLevelName;
        public int frameCount;
    }
}
