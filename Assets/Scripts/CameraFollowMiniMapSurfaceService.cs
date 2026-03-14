using System.Collections;
using TrafficSystem;
using UnityEngine;

internal readonly struct CameraFollowMiniMapSurfaceSettings
{
    public CameraFollowMiniMapSurfaceSettings(
        bool useCachedMiniMapSurface,
        int cachedMiniMapResolution,
        int cachedMiniMapWarmupFrames,
        Color miniMapBackgroundColor,
        Color cachedMiniMapRoadColor,
        Color cachedMiniMapRoadOutlineColor,
        int cachedMiniMapRoadWidthPixels,
        float miniMapHeight)
    {
        UseCachedMiniMapSurface = useCachedMiniMapSurface;
        CachedMiniMapResolution = cachedMiniMapResolution;
        CachedMiniMapWarmupFrames = cachedMiniMapWarmupFrames;
        MiniMapBackgroundColor = miniMapBackgroundColor;
        CachedMiniMapRoadColor = cachedMiniMapRoadColor;
        CachedMiniMapRoadOutlineColor = cachedMiniMapRoadOutlineColor;
        CachedMiniMapRoadWidthPixels = cachedMiniMapRoadWidthPixels;
        MiniMapHeight = miniMapHeight;
    }

    public bool UseCachedMiniMapSurface { get; }
    public int CachedMiniMapResolution { get; }
    public int CachedMiniMapWarmupFrames { get; }
    public Color MiniMapBackgroundColor { get; }
    public Color CachedMiniMapRoadColor { get; }
    public Color CachedMiniMapRoadOutlineColor { get; }
    public int CachedMiniMapRoadWidthPixels { get; }
    public float MiniMapHeight { get; }
}

internal sealed class CameraFollowMiniMapSurfaceService
{
    private Texture2D cachedMiniMapTexture;
    private GameObject cachedMiniMapSurface;
    private Material cachedMiniMapSurfaceMaterial;
    private Coroutine cachedMiniMapBuildRoutine;
    private RoadGraphBuilder cachedMiniMapRoadGraphBuilder;

    public bool HasSurface => cachedMiniMapSurface != null;

    public void RequestBuild(
        MonoBehaviour owner,
        Camera miniMapCamera,
        BoxCollider miniMapBounds,
        bool hasMiniMapRuntimeBounds,
        Bounds miniMapRuntimeBounds,
        int markerLayer,
        CameraFollowMiniMapSurfaceSettings settings,
        Vector3 mapPosition)
    {
        if (cachedMiniMapSurface != null || cachedMiniMapBuildRoutine != null)
        {
            return;
        }

        cachedMiniMapBuildRoutine = owner.StartCoroutine(BuildWhenReady(
            owner,
            miniMapCamera,
            miniMapBounds,
            hasMiniMapRuntimeBounds,
            miniMapRuntimeBounds,
            markerLayer,
            settings,
            mapPosition));
    }

    public void UpdateSurface(Camera miniMapCamera, BoxCollider miniMapBounds, bool hasMiniMapRuntimeBounds, Bounds miniMapRuntimeBounds, bool useCachedMiniMapSurface, Vector3 mapPosition)
    {
        if (!useCachedMiniMapSurface || cachedMiniMapSurface == null || cachedMiniMapSurfaceMaterial == null || miniMapCamera == null)
        {
            return;
        }

        if (!TryGetWorldBounds(miniMapBounds, hasMiniMapRuntimeBounds, miniMapRuntimeBounds, out Bounds bounds))
        {
            return;
        }

        Transform surfaceTransform = cachedMiniMapSurface.transform;
        if (surfaceTransform.parent != miniMapCamera.transform)
        {
            surfaceTransform.SetParent(miniMapCamera.transform, false);
        }

        float visibleWorldHeight = miniMapCamera.orthographicSize * 2f;
        float visibleWorldWidth = visibleWorldHeight * miniMapCamera.aspect;
        surfaceTransform.localPosition = new Vector3(0f, 0f, 100f);
        surfaceTransform.localRotation = Quaternion.identity;
        surfaceTransform.localScale = new Vector3(visibleWorldWidth, visibleWorldHeight, 1f);

        float widthFraction = bounds.size.x > 0.01f ? Mathf.Clamp01(visibleWorldWidth / bounds.size.x) : 1f;
        float heightFraction = bounds.size.z > 0.01f ? Mathf.Clamp01(visibleWorldHeight / bounds.size.z) : 1f;
        float centerU = bounds.size.x > 0.01f ? Mathf.InverseLerp(bounds.min.x, bounds.max.x, mapPosition.x) : 0.5f;
        float centerV = bounds.size.z > 0.01f ? Mathf.InverseLerp(bounds.min.z, bounds.max.z, mapPosition.z) : 0.5f;

        float offsetU = Mathf.Clamp(centerU - widthFraction * 0.5f, 0f, Mathf.Max(0f, 1f - widthFraction));
        float offsetV = Mathf.Clamp(centerV - heightFraction * 0.5f, 0f, Mathf.Max(0f, 1f - heightFraction));
        Vector2 textureScale = new Vector2(widthFraction, heightFraction);
        Vector2 textureOffset = new Vector2(offsetU, offsetV);

        cachedMiniMapSurfaceMaterial.mainTextureScale = textureScale;
        cachedMiniMapSurfaceMaterial.mainTextureOffset = textureOffset;

        if (cachedMiniMapSurfaceMaterial.HasProperty("_BaseMap"))
        {
            cachedMiniMapSurfaceMaterial.SetTextureScale("_BaseMap", textureScale);
            cachedMiniMapSurfaceMaterial.SetTextureOffset("_BaseMap", textureOffset);
        }
        if (cachedMiniMapSurfaceMaterial.HasProperty("_MainTex"))
        {
            cachedMiniMapSurfaceMaterial.SetTextureScale("_MainTex", textureScale);
            cachedMiniMapSurfaceMaterial.SetTextureOffset("_MainTex", textureOffset);
        }
    }

    public void Cleanup(MonoBehaviour owner)
    {
        if (cachedMiniMapSurface != null) Object.Destroy(cachedMiniMapSurface);
        if (cachedMiniMapSurfaceMaterial != null) Object.Destroy(cachedMiniMapSurfaceMaterial);
        if (cachedMiniMapTexture != null) Object.Destroy(cachedMiniMapTexture);
        if (cachedMiniMapBuildRoutine != null && owner != null) owner.StopCoroutine(cachedMiniMapBuildRoutine);
    }

    private IEnumerator BuildWhenReady(
        MonoBehaviour owner,
        Camera miniMapCamera,
        BoxCollider miniMapBounds,
        bool hasMiniMapRuntimeBounds,
        Bounds miniMapRuntimeBounds,
        int markerLayer,
        CameraFollowMiniMapSurfaceSettings settings,
        Vector3 mapPosition)
    {
        int warmupFrames = Mathf.Max(1, settings.CachedMiniMapWarmupFrames);
        for (int i = 0; i < warmupFrames; i++)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        BuildSurface(miniMapCamera, miniMapBounds, hasMiniMapRuntimeBounds, miniMapRuntimeBounds, markerLayer, settings, mapPosition);
        cachedMiniMapBuildRoutine = null;
    }

    private void BuildSurface(
        Camera miniMapCamera,
        BoxCollider miniMapBounds,
        bool hasMiniMapRuntimeBounds,
        Bounds miniMapRuntimeBounds,
        int markerLayer,
        CameraFollowMiniMapSurfaceSettings settings,
        Vector3 mapPosition)
    {
        if (cachedMiniMapSurface != null)
        {
            return;
        }

        if (!TryGetWorldBounds(miniMapBounds, hasMiniMapRuntimeBounds, miniMapRuntimeBounds, out Bounds bounds))
        {
            return;
        }

        Texture2D roadTexture = BuildProceduralMiniMapTexture(bounds, settings);
        if (roadTexture == null)
        {
            return;
        }

        cachedMiniMapTexture = roadTexture;
        cachedMiniMapSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cachedMiniMapSurface.name = "MiniMapCachedSurface";

        if (markerLayer >= 0)
        {
            cachedMiniMapSurface.layer = markerLayer;
        }

        if (miniMapCamera != null)
        {
            cachedMiniMapSurface.transform.SetParent(miniMapCamera.transform, false);
        }

        cachedMiniMapSurface.transform.localPosition = new Vector3(0f, 0f, 100f);
        cachedMiniMapSurface.transform.localRotation = Quaternion.identity;
        cachedMiniMapSurface.transform.localScale = Vector3.one;

        Collider cachedSurfaceCollider = cachedMiniMapSurface.GetComponent<Collider>();
        if (cachedSurfaceCollider != null)
        {
            Object.Destroy(cachedSurfaceCollider);
        }

        MeshRenderer renderer = cachedMiniMapSurface.GetComponent<MeshRenderer>();
        cachedMiniMapSurfaceMaterial = CreateMaterial(cachedMiniMapTexture, renderer);
        if (renderer != null && cachedMiniMapSurfaceMaterial != null)
        {
            renderer.sharedMaterial = cachedMiniMapSurfaceMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        UpdateSurface(miniMapCamera, miniMapBounds, hasMiniMapRuntimeBounds, miniMapRuntimeBounds, settings.UseCachedMiniMapSurface, mapPosition);
    }

    private Texture2D BuildProceduralMiniMapTexture(Bounds bounds, CameraFollowMiniMapSurfaceSettings settings)
    {
        if (!TryResolveRoadGraph(out RoadGraph graph))
        {
            return null;
        }

        return MinimapRoadTextureBuilder.Build(
            graph,
            bounds,
            settings.CachedMiniMapResolution,
            settings.MiniMapBackgroundColor,
            settings.CachedMiniMapRoadColor,
            settings.CachedMiniMapRoadOutlineColor,
            settings.CachedMiniMapRoadWidthPixels);
    }

    private bool TryResolveRoadGraph(out RoadGraph graph)
    {
        if (cachedMiniMapRoadGraphBuilder == null)
        {
            cachedMiniMapRoadGraphBuilder = Object.FindFirstObjectByType<RoadGraphBuilder>();
        }

        if (cachedMiniMapRoadGraphBuilder == null)
        {
            graph = null;
            return false;
        }

        if (!cachedMiniMapRoadGraphBuilder.HasBuiltRoadGraph)
        {
            if (!cachedMiniMapRoadGraphBuilder.HasPendingBuild)
            {
                cachedMiniMapRoadGraphBuilder.BeginBuildWithDelay(0f);
            }

            graph = null;
            return false;
        }

        graph = cachedMiniMapRoadGraphBuilder.RoadGraph;
        return graph != null && graph.roadSegments != null && graph.roadSegments.Count > 0;
    }

    private static Material CreateMaterial(Texture mapTexture, MeshRenderer fallbackRenderer)
    {
        Shader shader = Shader.Find("Unlit/Texture") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Sprites/Default");

        if (shader == null)
        {
            if (fallbackRenderer == null || fallbackRenderer.sharedMaterial == null)
            {
                return null;
            }

            shader = fallbackRenderer.sharedMaterial.shader;
        }

        Material material = new Material(shader)
        {
            color = Color.white,
            mainTexture = mapTexture,
            mainTextureScale = Vector2.one,
            mainTextureOffset = Vector2.zero
        };

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", mapTexture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", mapTexture);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }

        return material;
    }

    private static bool TryGetWorldBounds(BoxCollider miniMapBounds, bool hasMiniMapRuntimeBounds, Bounds miniMapRuntimeBounds, out Bounds bounds)
    {
        if (miniMapBounds != null)
        {
            bounds = miniMapBounds.bounds;
            return true;
        }

        if (hasMiniMapRuntimeBounds)
        {
            bounds = miniMapRuntimeBounds;
            return true;
        }

        bounds = default;
        return false;
    }
}
