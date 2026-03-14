using DeliveryDriver.Quest.UI;
using UnityEngine;

internal readonly struct CameraFollowExternalControllerSettings
{
    public CameraFollowExternalControllerSettings(
        int miniMapMarkerLayer,
        bool enableReverseCamera,
        Vector3 reverseCamOffset,
        Vector3 reverseCamEuler,
        float reverseCamFov,
        Rect reverseCameraViewport,
        float reverseCamStationaryThreshold,
        Color reverseCamBorderColor,
        float reverseCamBorderWidth,
        float reverseCamFramePadding,
        float reverseCamFadeSpeed,
        float reverseCamGradientHeight,
        bool enableMiniMap,
        float miniMapHeight,
        float miniMapOrthoSize,
        bool miniMapRotateWithTarget,
        bool allowMiniMapToggleKey,
        float miniMapViewportSize,
        Vector2 miniMapViewportMargin,
        LayerMask miniMapCullingMask,
        Color miniMapBackgroundColor)
    {
        MiniMapMarkerLayer = miniMapMarkerLayer;
        EnableReverseCamera = enableReverseCamera;
        ReverseCamOffset = reverseCamOffset;
        ReverseCamEuler = reverseCamEuler;
        ReverseCamFov = reverseCamFov;
        ReverseCameraViewport = reverseCameraViewport;
        ReverseCamStationaryThreshold = reverseCamStationaryThreshold;
        ReverseCamBorderColor = reverseCamBorderColor;
        ReverseCamBorderWidth = reverseCamBorderWidth;
        ReverseCamFramePadding = reverseCamFramePadding;
        ReverseCamFadeSpeed = reverseCamFadeSpeed;
        ReverseCamGradientHeight = reverseCamGradientHeight;
        EnableMiniMap = enableMiniMap;
        MiniMapHeight = miniMapHeight;
        MiniMapOrthoSize = miniMapOrthoSize;
        MiniMapRotateWithTarget = miniMapRotateWithTarget;
        AllowMiniMapToggleKey = allowMiniMapToggleKey;
        MiniMapViewportSize = miniMapViewportSize;
        MiniMapViewportMargin = miniMapViewportMargin;
        MiniMapCullingMask = miniMapCullingMask;
        MiniMapBackgroundColor = miniMapBackgroundColor;
    }

    public int MiniMapMarkerLayer { get; }
    public bool EnableReverseCamera { get; }
    public Vector3 ReverseCamOffset { get; }
    public Vector3 ReverseCamEuler { get; }
    public float ReverseCamFov { get; }
    public Rect ReverseCameraViewport { get; }
    public float ReverseCamStationaryThreshold { get; }
    public Color ReverseCamBorderColor { get; }
    public float ReverseCamBorderWidth { get; }
    public float ReverseCamFramePadding { get; }
    public float ReverseCamFadeSpeed { get; }
    public float ReverseCamGradientHeight { get; }
    public bool EnableMiniMap { get; }
    public float MiniMapHeight { get; }
    public float MiniMapOrthoSize { get; }
    public bool MiniMapRotateWithTarget { get; }
    public bool AllowMiniMapToggleKey { get; }
    public float MiniMapViewportSize { get; }
    public Vector2 MiniMapViewportMargin { get; }
    public LayerMask MiniMapCullingMask { get; }
    public Color MiniMapBackgroundColor { get; }
}

internal sealed class CameraFollowExternalControllerCoordinator
{
    private ReverseCameraHUD reverseCameraController;
    private MinimapCamera minimapCameraController;

    public void EnsureControllers(Camera mainCamera, GameObject owner, Transform target, CameraFollowExternalControllerSettings settings)
    {
        if (mainCamera != null && settings.MiniMapMarkerLayer >= 0)
        {
            mainCamera.cullingMask &= ~(1 << settings.MiniMapMarkerLayer);
        }

        if (settings.EnableReverseCamera)
        {
            EnsureReverseCameraController(mainCamera, owner, target, settings);
        }

        if (!settings.EnableMiniMap)
        {
            return;
        }

        EnsureMiniMapController(mainCamera, target, settings);
    }

    public void ClearTargets()
    {
        reverseCameraController?.SetTarget(null);
        minimapCameraController?.SetPlayer(null);
    }

    public void ApplyTarget(Camera mainCamera, Transform target)
    {
        reverseCameraController?.SetGameplayCamera(mainCamera);
        reverseCameraController?.SetTarget(target);
        minimapCameraController?.SetPlayer(target);
    }

    private void EnsureReverseCameraController(Camera mainCamera, GameObject owner, Transform target, CameraFollowExternalControllerSettings settings)
    {
        if (reverseCameraController == null)
        {
            reverseCameraController = Object.FindFirstObjectByType<ReverseCameraHUD>();
        }

        if (reverseCameraController == null)
        {
            reverseCameraController = owner.GetComponent<ReverseCameraHUD>();
            if (reverseCameraController == null)
            {
                reverseCameraController = owner.AddComponent<ReverseCameraHUD>();
            }
        }

        reverseCameraController.Configure(
            settings.ReverseCamOffset,
            settings.ReverseCamEuler,
            settings.ReverseCamFov,
            settings.ReverseCameraViewport,
            -0.1f,
            settings.ReverseCamStationaryThreshold,
            settings.ReverseCamBorderColor,
            settings.ReverseCamBorderWidth,
            settings.ReverseCamFramePadding,
            settings.ReverseCamFadeSpeed,
            settings.ReverseCamGradientHeight);
        reverseCameraController.SetGameplayCamera(mainCamera);
        reverseCameraController.SetTarget(target);
    }

    private void EnsureMiniMapController(Camera mainCamera, Transform target, CameraFollowExternalControllerSettings settings)
    {
        if (minimapCameraController == null)
        {
            minimapCameraController = Object.FindFirstObjectByType<MinimapCamera>();
        }

        if (minimapCameraController == null)
        {
            GameObject miniMapCameraObject = new GameObject("MinimapCamera");
            miniMapCameraObject.AddComponent<Camera>();
            minimapCameraController = miniMapCameraObject.AddComponent<MinimapCamera>();
        }

        MinimapUI minimapUi = MinimapUI.EnsureSceneInstance();
        bool useStandaloneOverlay = minimapUi == null;
        minimapCameraController.ConfigureRuntime(
            settings.MiniMapHeight,
            settings.MiniMapOrthoSize,
            settings.MiniMapRotateWithTarget,
            settings.AllowMiniMapToggleKey,
            useStandaloneOverlay,
            settings.MiniMapViewportSize,
            settings.MiniMapViewportMargin,
            settings.MiniMapCullingMask,
            settings.MiniMapBackgroundColor);

        if (minimapUi != null && target != null)
        {
            minimapUi.SetPlayerTransform(target);
        }

        minimapCameraController.SetPlayer(target);
        reverseCameraController?.SetGameplayCamera(mainCamera);
    }
}
