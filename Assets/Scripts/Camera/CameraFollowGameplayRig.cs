using DeliveryDriver.Vehicle;
using Unity.Cinemachine;
using UnityEngine;

internal readonly struct CameraFollowGameplayRigSettings
{
    public CameraFollowGameplayRigSettings(
        Vector3 offset,
        Vector3 lookAtTargetOffset,
        bool enableSpeedFov,
        float baseFov,
        float maxSpeedFov,
        float fovMaxSpeedKmh,
        float fovLerpSpeed,
        float forwardZoomOutExtra,
        float forwardZoomOutSpeedKmh,
        float dynamicZoomLerpSpeed)
    {
        Offset = offset;
        LookAtTargetOffset = lookAtTargetOffset;
        EnableSpeedFov = enableSpeedFov;
        BaseFov = baseFov;
        MaxSpeedFov = maxSpeedFov;
        FovMaxSpeedKmh = fovMaxSpeedKmh;
        FovLerpSpeed = fovLerpSpeed;
        ForwardZoomOutExtra = forwardZoomOutExtra;
        ForwardZoomOutSpeedKmh = forwardZoomOutSpeedKmh;
        DynamicZoomLerpSpeed = dynamicZoomLerpSpeed;
    }

    public Vector3 Offset { get; }
    public Vector3 LookAtTargetOffset { get; }
    public bool EnableSpeedFov { get; }
    public float BaseFov { get; }
    public float MaxSpeedFov { get; }
    public float FovMaxSpeedKmh { get; }
    public float FovLerpSpeed { get; }
    public float ForwardZoomOutExtra { get; }
    public float ForwardZoomOutSpeedKmh { get; }
    public float DynamicZoomLerpSpeed { get; }
}

internal sealed class CameraFollowGameplayRig
{
    private readonly Camera mainCamera;
    private CinemachineCamera gameplayCamera;
    private CinemachineFollow cinemachineFollow;
    private CinemachineRotationComposer rotationComposer;
    private float currentGameplayFov;
    private float currentZoomOffset;
    private bool warnedMissingGameplayCamera;

    public bool HasGameplayCamera => gameplayCamera != null;

    public CameraFollowGameplayRig(Camera mainCamera, float baseFov)
    {
        this.mainCamera = mainCamera;
        currentGameplayFov = baseFov;
    }

    public void ResolveRig()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        }

        cinemachineFollow = gameplayCamera != null ? gameplayCamera.GetComponent<CinemachineFollow>() : null;
        rotationComposer = gameplayCamera != null ? gameplayCamera.GetComponent<CinemachineRotationComposer>() : null;

        if (gameplayCamera == null && !warnedMissingGameplayCamera)
        {
            warnedMissingGameplayCamera = true;
            Debug.LogWarning("[CameraFollow] No CinemachineCamera found. Main gameplay follow will stay unbound.");
        }
    }

    public void ClearTarget(CinemachineBrain cinemachineBrain)
    {
        if (cinemachineBrain != null)
        {
            cinemachineBrain.WorldUpOverride = null;
        }
    }

    public void ResetRuntimeFov(float currentLensFov)
    {
        currentGameplayFov = currentLensFov;
        currentZoomOffset = 0f;
    }

    public float ResolveCurrentLensFov(float fallbackFov)
    {
        if (gameplayCamera != null)
        {
            return gameplayCamera.Lens.FieldOfView;
        }

        if (mainCamera != null)
        {
            return mainCamera.fieldOfView;
        }

        return fallbackFov;
    }

    public void BindTarget(Transform target, CinemachineBrain cinemachineBrain)
    {
        if (target == null)
        {
            return;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.Target.TrackingTarget = target;
            gameplayCamera.Target.LookAtTarget = null;
            gameplayCamera.Target.CustomLookAtTarget = false;
            gameplayCamera.CancelDamping(true);
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.WorldUpOverride = target;
        }
    }

    public void Update(Transform target, Rigidbody targetRb, CarController carController, CameraFollowGameplayRigSettings settings, float deltaTime)
    {
        if (target == null)
        {
            return;
        }

        UpdateSpeedFov(targetRb, settings, deltaTime);
        UpdateZoomOffset(target, targetRb, carController, settings, deltaTime);
        ApplyRigSettings(settings, deltaTime <= 0f);
    }

    private void UpdateSpeedFov(Rigidbody targetRb, CameraFollowGameplayRigSettings settings, float deltaTime)
    {
        float desiredFov = settings.BaseFov;
        if (settings.EnableSpeedFov && targetRb != null)
        {
            float speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
            float t = settings.FovMaxSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / settings.FovMaxSpeedKmh) : 0f;
            desiredFov = Mathf.Lerp(settings.BaseFov, settings.MaxSpeedFov, t);
        }

        currentGameplayFov = Mathf.Lerp(currentGameplayFov, desiredFov, Mathf.Max(0.01f, settings.FovLerpSpeed) * deltaTime);
    }

    private void UpdateZoomOffset(Transform target, Rigidbody targetRb, CarController carController, CameraFollowGameplayRigSettings settings, float deltaTime)
    {
        if (cinemachineFollow == null || target == null)
        {
            return;
        }

        float desiredZoomOffset = 0f;
        if (targetRb != null)
        {
            float localVelZ = target.InverseTransformDirection(targetRb.linearVelocity).z;
            bool isReversing = (carController != null && carController.IsReverseInputActive) || localVelZ < -0.3f;
            if (!isReversing)
            {
                float forwardKmh = Mathf.Max(0f, localVelZ * 3.6f);
                float zoomT = Mathf.Clamp01(forwardKmh / Mathf.Max(1f, settings.ForwardZoomOutSpeedKmh));
                desiredZoomOffset = -zoomT * settings.ForwardZoomOutExtra;
            }
        }

        currentZoomOffset = Mathf.Lerp(currentZoomOffset, desiredZoomOffset, Mathf.Max(0.01f, settings.DynamicZoomLerpSpeed) * deltaTime);
    }

    private void ApplyRigSettings(CameraFollowGameplayRigSettings settings, bool snap)
    {
        if (cinemachineFollow != null)
        {
            Vector3 desiredOffset = new Vector3(settings.Offset.x, settings.Offset.y, settings.Offset.z + currentZoomOffset);
            if ((cinemachineFollow.FollowOffset - desiredOffset).sqrMagnitude > 0.0001f)
            {
                cinemachineFollow.FollowOffset = desiredOffset;
            }
        }

        if (rotationComposer != null)
        {
            rotationComposer.TargetOffset = settings.LookAtTargetOffset;
        }

        if (gameplayCamera != null)
        {
            LensSettings lens = gameplayCamera.Lens;
            lens.FieldOfView = currentGameplayFov;
            gameplayCamera.Lens = lens;

            if (snap)
            {
                gameplayCamera.CancelDamping(true);
            }
        }
        else if (mainCamera != null)
        {
            mainCamera.fieldOfView = currentGameplayFov;
        }
    }
}
