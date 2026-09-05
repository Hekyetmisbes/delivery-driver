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
        float dynamicZoomLerpSpeed,
        float reverseZoomOutExtra = 1.6f,
        float reverseHeightExtra = 0.8f)
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
        ReverseZoomOutExtra = reverseZoomOutExtra;
        ReverseHeightExtra = reverseHeightExtra;
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
    public float ReverseZoomOutExtra { get; }
    public float ReverseHeightExtra { get; }
}

internal sealed class CameraFollowGameplayRig
{
    private readonly Camera mainCamera;
    private CinemachineCamera gameplayCamera;
    private CinemachineFollow cinemachineFollow;
    private CinemachineRotationComposer rotationComposer;
    private float currentGameplayFov;
    private float currentZoomOffset;
    private float currentHeightOffset;
    private bool isCurrentlyReversing;
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

        if (cinemachineFollow != null)
        {
            // İleri-geri Z gecikmesini 1.0 saniyeden 0.05 saniyeye indiriyoruz:
            // Böylece araba hızlandığında ötede kalmaz, geri gittiğinde de tavanına yapışmaz!
            cinemachineFollow.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.25f, 0.05f);
            cinemachineFollow.TrackerSettings.RotationDamping = new Vector3(0.15f, 0.2f, 0.15f);
        }

        if (rotationComposer != null)
        {
            rotationComposer.TargetOffset = new Vector3(0f, 1.2f, 0f);
            rotationComposer.Damping = new Vector2(0.2f, 0.2f);
        }

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
        currentHeightOffset = 0f;
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
        float desiredHeightOffset = 0f;
        isCurrentlyReversing = false;

        if (targetRb != null)
        {
            float localVelZ = target.InverseTransformDirection(targetRb.linearVelocity).z;
            isCurrentlyReversing = (carController != null && carController.IsReverseInputActive) || localVelZ < -0.2f;

            if (isCurrentlyReversing)
            {
                // Geri giderken kamera ekstra hareket etmesin, normal konumunu korusun
                desiredZoomOffset = 0f;
                desiredHeightOffset = 0f;
            }
            else
            {
                // İleri giderken: araba kameradan kopmasın, hafif ve dengeli geri çekilme
                float forwardKmh = Mathf.Max(0f, localVelZ * 3.6f);
                float zoomT = Mathf.Clamp01(forwardKmh / Mathf.Max(1f, settings.ForwardZoomOutSpeedKmh));
                desiredZoomOffset = -zoomT * settings.ForwardZoomOutExtra;
                desiredHeightOffset = 0f;
            }
        }

        float lerpSpeed = isCurrentlyReversing ? 8f : settings.DynamicZoomLerpSpeed;
        currentZoomOffset = Mathf.Lerp(currentZoomOffset, desiredZoomOffset, Mathf.Max(0.01f, lerpSpeed) * deltaTime);
        currentHeightOffset = Mathf.Lerp(currentHeightOffset, desiredHeightOffset, Mathf.Max(0.01f, lerpSpeed) * deltaTime);
    }

    private void ApplyRigSettings(CameraFollowGameplayRigSettings settings, bool snap)
    {
        if (cinemachineFollow != null)
        {
            // Geri giderken arabanın kameraya yapışmaması için Z damping'i sıkı tut ama kamera ofsetini değiştirme
            if (isCurrentlyReversing)
            {
                cinemachineFollow.TrackerSettings.PositionDamping = new Vector3(0.08f, 0.15f, 0.02f);
            }
            else
            {
                cinemachineFollow.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.25f, 0.05f);
            }

            Vector3 desiredOffset = new Vector3(settings.Offset.x, settings.Offset.y + currentHeightOffset, settings.Offset.z + currentZoomOffset);
            if ((cinemachineFollow.FollowOffset - desiredOffset).sqrMagnitude > 0.0001f)
            {
                cinemachineFollow.FollowOffset = desiredOffset;
            }
        }

        if (rotationComposer != null)
        {
            // Sabit ve dengeli lookAt: geri giderken tavana veya yere eğilme yapmaz
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
