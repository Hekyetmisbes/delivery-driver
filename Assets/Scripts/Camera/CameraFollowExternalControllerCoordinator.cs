using UnityEngine;

internal readonly struct CameraFollowExternalControllerSettings
{
    public CameraFollowExternalControllerSettings(
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
        float reverseCamGradientHeight)
    {
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
    }

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
}

internal sealed class CameraFollowExternalControllerCoordinator
{
    private ReverseCameraHUD reverseCameraController;

    public void EnsureControllers(Camera mainCamera, GameObject owner, Transform target, CameraFollowExternalControllerSettings settings)
    {
        if (settings.EnableReverseCamera)
        {
            EnsureReverseCameraController(mainCamera, owner, target, settings);
        }
    }

    public void ClearTargets()
    {
        reverseCameraController?.SetTarget(null);
    }

    public void ApplyTarget(Camera mainCamera, Transform target)
    {
        reverseCameraController?.SetGameplayCamera(mainCamera);
        reverseCameraController?.SetTarget(target);
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
}
