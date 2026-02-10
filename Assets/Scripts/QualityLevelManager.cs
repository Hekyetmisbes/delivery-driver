using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages quality level settings and applies optimizations per level
/// Configures shadows, rendering, and terrain based on quality preset
/// </summary>
public class QualityLevelManager : MonoBehaviour
{
    public static QualityLevelManager Instance { get; private set; }

    [Header("Quality Presets")]
    [Tooltip("Shadow distance for each quality level (Low, Medium, High)")]
    [SerializeField] private float[] shadowDistances = new float[] { 30f, 50f, 75f };
    [Tooltip("Terrain pixel error for each quality level (Low, Medium, High)")]
    [SerializeField] private float[] terrainPixelErrors = new float[] { 8f, 5f, 3f };
    [Tooltip("Terrain detail distance for each quality level (Low, Medium, High)")]
    [SerializeField] private float[] terrainDetailDistances = new float[] { 50f, 80f, 120f };

    [Header("URP Settings")]
    [Tooltip("Main URP render pipeline asset")]
    [SerializeField] private UniversalRenderPipelineAsset urpAsset;
    [Tooltip("Apply URP optimizations")]
    [SerializeField] private bool applyUrpOptimizations = true;

    [Header("Runtime Adjustments")]
    [Tooltip("Apply settings on Start")]
    [SerializeField] private bool applyOnStart = true;
    [Tooltip("Detect quality level changes")]
    [SerializeField] private bool watchQualityChanges = true;

    private int lastQualityLevel = -1;

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
        if (applyOnStart)
        {
            ApplyQualitySettings();
        }
    }

    private void Update()
    {
        if (watchQualityChanges)
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            if (currentQuality != lastQualityLevel)
            {
                ApplyQualitySettings();
                lastQualityLevel = currentQuality;
            }
        }
    }

    /// <summary>
    /// Apply quality-specific settings to the scene
    /// </summary>
    public void ApplyQualitySettings()
    {
        int qualityLevel = QualitySettings.GetQualityLevel();
        qualityLevel = Mathf.Clamp(qualityLevel, 0, 2); // Support 3 levels: 0=Low, 1=Medium, 2=High

        Debug.Log($"[QualityLevelManager] Applying quality level {qualityLevel}: {QualitySettings.names[qualityLevel]}");

        // Apply shadow distance
        if (qualityLevel < shadowDistances.Length)
        {
            QualitySettings.shadowDistance = shadowDistances[qualityLevel];
            Debug.Log($"[QualityLevelManager] Shadow distance set to: {shadowDistances[qualityLevel]}m");
        }

        // Apply terrain settings
        ApplyTerrainSettings(qualityLevel);

        // Apply URP settings if available
        if (applyUrpOptimizations && urpAsset != null)
        {
            ApplyUrpSettings(qualityLevel);
        }

        lastQualityLevel = qualityLevel;
    }

    /// <summary>
    /// Apply terrain-specific settings for quality level
    /// </summary>
    private void ApplyTerrainSettings(int qualityLevel)
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        foreach (Terrain terrain in terrains)
        {
            if (qualityLevel < terrainPixelErrors.Length)
            {
                terrain.heightmapPixelError = terrainPixelErrors[qualityLevel];
            }

            if (qualityLevel < terrainDetailDistances.Length)
            {
                terrain.detailObjectDistance = terrainDetailDistances[qualityLevel];
            }

            // Adjust other terrain settings based on quality
            switch (qualityLevel)
            {
                case 0: // Low
                    terrain.basemapDistance = 500f;
                    terrain.treeDistance = 500f;
                    terrain.treeBillboardDistance = 150f;
                    break;
                case 1: // Medium
                    terrain.basemapDistance = 750f;
                    terrain.treeDistance = 1000f;
                    terrain.treeBillboardDistance = 200f;
                    break;
                case 2: // High
                    terrain.basemapDistance = 1000f;
                    terrain.treeDistance = 2000f;
                    terrain.treeBillboardDistance = 300f;
                    break;
            }
        }

        if (terrains.Length > 0)
        {
            Debug.Log($"[QualityLevelManager] Applied terrain settings to {terrains.Length} terrain(s)");
        }
    }

    /// <summary>
    /// Apply URP-specific settings for quality level
    /// </summary>
    private void ApplyUrpSettings(int qualityLevel)
    {
        // Note: URP asset settings are read-only at runtime
        // These settings should be configured in the URP asset directly per quality level
        // This method serves as documentation of recommended settings

        switch (qualityLevel)
        {
            case 0: // Low
                Debug.Log("[QualityLevelManager] Low Quality - Recommended URP: MSAA=Off, Shadows=1 Cascade, Render Scale=0.8");
                break;
            case 1: // Medium
                Debug.Log("[QualityLevelManager] Medium Quality - Recommended URP: MSAA=2x, Shadows=2 Cascades, Render Scale=1.0");
                break;
            case 2: // High
                Debug.Log("[QualityLevelManager] High Quality - Recommended URP: MSAA=4x, Shadows=2 Cascades, Render Scale=1.0");
                break;
        }
    }

    /// <summary>
    /// Change quality level
    /// </summary>
    public void SetQualityLevel(int level)
    {
        level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(level, true);
        ApplyQualitySettings();

        Debug.Log($"[QualityLevelManager] Quality level changed to: {QualitySettings.names[level]}");
    }

    /// <summary>
    /// Get current quality level info
    /// </summary>
    public QualityInfo GetCurrentQualityInfo()
    {
        int level = QualitySettings.GetQualityLevel();
        return new QualityInfo
        {
            level = level,
            name = QualitySettings.names[level],
            shadowDistance = QualitySettings.shadowDistance,
            shadowCascades = QualitySettings.shadowCascades,
            vSyncCount = QualitySettings.vSyncCount
        };
    }

    public struct QualityInfo
    {
        public int level;
        public string name;
        public float shadowDistance;
        public int shadowCascades;
        public int vSyncCount;
    }
}
