using System.Collections;
using DeliveryDriver.Company;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BorderRespawnGuard : MonoBehaviour
{
    private const string GameSceneName = "Game";
    private const string BorderObjectName = "Border";
    private const string FadeOverlayName = "BorderRespawnFade";
    private const string WarningLabelName = "BorderWarningLabel";

    [SerializeField] private bool enforceBorder = true;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float holdBlackDuration = 0.15f;
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float warningDistance = 30f;
    [SerializeField] private float respawnProbeHeight = 32f;
    [SerializeField] private float respawnProbeDistance = 96f;
    [SerializeField] private float respawnHeightOffset = 0.6f;
    [SerializeField] private float respawnInsetDistance = 12f;
    [SerializeField] private float minGroundNormalY = 0.55f;
    [SerializeField] private float minUprightDotForSafePose = 0.6f;
    [SerializeField] private Color fadeColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color warningBackgroundColor = new Color(0.78f, 0.16f, 0.14f, 0.92f);
    [SerializeField] private Color warningTextColor = new Color(1f, 0.97f, 0.92f, 1f);

    private Collider borderCollider;
    private CarController activeVehicleController;
    private Rigidbody activeVehicleRigidbody;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private bool hasInitialSpawnPose;
    private bool isRecovering;

    private Image fadeOverlayImage;
    private CanvasGroup warningCanvasGroup;
    private TextMeshProUGUI warningText;
    private Coroutine recoveryCoroutine;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged += HandleActiveVehicleChanged;

        RefreshBindings(forceVehicleResolve: true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerVehicleManager.ActiveVehicleChanged -= HandleActiveVehicleChanged;
    }

    private void Update()
    {
        if (!Application.isPlaying || !enforceBorder || isRecovering || !IsGameSceneActive())
        {
            return;
        }

        if (borderCollider == null)
        {
            TryResolveBorderCollider();
            if (borderCollider == null)
            {
                return;
            }
        }

        if (activeVehicleController == null || activeVehicleController.gameObject == null)
        {
            ResolveActiveVehicle(forceRefresh: true);
            if (activeVehicleController == null)
            {
                SetWarningVisible(false);
                return;
            }
        }

        Vector3 vehiclePosition = activeVehicleController.transform.position;
        bool isInsideBorder = borderCollider.bounds.Contains(vehiclePosition);
        if (isInsideBorder)
        {
            UpdateWarning(vehiclePosition);
            return;
        }

        SetWarningVisible(false);
        StartRecovery();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBindings(forceVehicleResolve: true);
    }

    private void RefreshBindings(bool forceVehicleResolve)
    {
        TryResolveBorderCollider();
        ResolveFadeOverlay();
        ResolveWarningOverlay();
        if (forceVehicleResolve)
        {
            ResolveActiveVehicle(forceRefresh: true);
        }
    }

    private void TryResolveBorderCollider()
    {
        GameObject borderObject = GameObject.Find(BorderObjectName);
        borderCollider = borderObject != null ? borderObject.GetComponent<Collider>() : null;
    }

    private void ResolveActiveVehicle(bool forceRefresh)
    {
        if (!forceRefresh && activeVehicleController != null && activeVehicleController.gameObject != null)
        {
            return;
        }

        activeVehicleController = null;
        activeVehicleRigidbody = null;

        PlayerVehicleManager vehicleManager = PlayerVehicleManager.Instance ?? FindFirstObjectByType<PlayerVehicleManager>();
        if (vehicleManager != null && vehicleManager.ActiveVehicleController != null)
        {
            AssignActiveVehicle(vehicleManager.ActiveVehicleController);
            return;
        }

        CarController sceneController = FindFirstObjectByType<CarController>();
        if (sceneController != null)
        {
            AssignActiveVehicle(sceneController);
        }
    }

    private void HandleActiveVehicleChanged(CarController controller)
    {
        AssignActiveVehicle(controller);
    }

    private void AssignActiveVehicle(CarController controller)
    {
        bool vehicleChanged = activeVehicleController != controller;
        activeVehicleController = controller;
        activeVehicleRigidbody = controller != null ? controller.GetComponent<Rigidbody>() : null;

        if (controller != null && (vehicleChanged || !hasInitialSpawnPose))
        {
            UpdateSpawnPoseSnapshot(controller.transform.position, controller.transform.rotation);
            hasInitialSpawnPose = true;
        }
    }

    private void UpdateSafePoseFromCurrentVehicle()
    {
        if (activeVehicleController == null)
        {
            return;
        }

        Transform vehicleTransform = activeVehicleController.transform;
        if (Vector3.Dot(vehicleTransform.up, Vector3.up) < minUprightDotForSafePose)
        {
            return;
        }

        UpdateSpawnPoseSnapshot(vehicleTransform.position, vehicleTransform.rotation);
        hasInitialSpawnPose = true;
    }

    private void StartRecovery()
    {
        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
        }

        recoveryCoroutine = StartCoroutine(RecoveryRoutine());
    }

    private IEnumerator RecoveryRoutine()
    {
        isRecovering = true;
        ResolveFadeOverlay();
        SetWarningVisible(false);

        yield return FadeOverlay(0f, 1f, fadeOutDuration);

        TeleportVehicleToSpawn();

        if (holdBlackDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdBlackDuration);
        }

        yield return FadeOverlay(1f, 0f, fadeInDuration);

        if (fadeOverlayImage != null)
        {
            fadeOverlayImage.gameObject.SetActive(false);
        }

        recoveryCoroutine = null;
        isRecovering = false;
    }

    private void TeleportVehicleToSpawn()
    {
        if (!hasInitialSpawnPose || activeVehicleController == null)
        {
            return;
        }

        Vector3 targetPosition = initialSpawnPosition;
        Quaternion targetRotation = initialSpawnRotation;

        if (!TryResolveBorderRecoveryPose(out targetPosition, out targetRotation))
        {
            UpdateSpawnPoseSnapshot(initialSpawnPosition, initialSpawnRotation);
            targetPosition = initialSpawnPosition;
            targetRotation = initialSpawnRotation;
        }

        Transform vehicleTransform = activeVehicleController.transform;
        if (activeVehicleRigidbody != null)
        {
            activeVehicleRigidbody.linearVelocity = Vector3.zero;
            activeVehicleRigidbody.angularVelocity = Vector3.zero;
            activeVehicleRigidbody.position = targetPosition;
            activeVehicleRigidbody.rotation = targetRotation;
            Physics.SyncTransforms();
            return;
        }

        vehicleTransform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();
    }

    private bool TryResolveBorderRecoveryPose(out Vector3 safePosition, out Quaternion safeRotation)
    {
        safePosition = initialSpawnPosition;
        safeRotation = initialSpawnRotation;

        if (borderCollider == null || activeVehicleController == null)
        {
            return false;
        }

        Bounds bounds = borderCollider.bounds;
        Vector3 currentPosition = activeVehicleController.transform.position;
        float inset = Mathf.Max(respawnInsetDistance, EstimateVehicleClearance() * 1.25f);

        float distanceToMinX = Mathf.Abs(currentPosition.x - bounds.min.x);
        float distanceToMaxX = Mathf.Abs(bounds.max.x - currentPosition.x);
        float distanceToMinZ = Mathf.Abs(currentPosition.z - bounds.min.z);
        float distanceToMaxZ = Mathf.Abs(bounds.max.z - currentPosition.z);

        float nearestDistance = distanceToMinX;
        Vector3 inwardDirection = Vector3.right;
        safePosition = new Vector3(
            bounds.min.x + inset,
            currentPosition.y,
            Mathf.Clamp(currentPosition.z, bounds.min.z + inset, bounds.max.z - inset));

        if (distanceToMaxX < nearestDistance)
        {
            nearestDistance = distanceToMaxX;
            inwardDirection = Vector3.left;
            safePosition = new Vector3(
                bounds.max.x - inset,
                currentPosition.y,
                Mathf.Clamp(currentPosition.z, bounds.min.z + inset, bounds.max.z - inset));
        }

        if (distanceToMinZ < nearestDistance)
        {
            nearestDistance = distanceToMinZ;
            inwardDirection = Vector3.forward;
            safePosition = new Vector3(
                Mathf.Clamp(currentPosition.x, bounds.min.x + inset, bounds.max.x - inset),
                currentPosition.y,
                bounds.min.z + inset);
        }

        if (distanceToMaxZ < nearestDistance)
        {
            inwardDirection = Vector3.back;
            safePosition = new Vector3(
                Mathf.Clamp(currentPosition.x, bounds.min.x + inset, bounds.max.x - inset),
                currentPosition.y,
                bounds.max.z - inset);
        }

        ResolveSafeRespawnPose(safePosition, Quaternion.LookRotation(inwardDirection, Vector3.up), out safePosition, out safeRotation);
        UpdateSpawnPoseSnapshot(safePosition, safeRotation);
        hasInitialSpawnPose = true;
        return true;
    }

    private void UpdateSpawnPoseSnapshot(Vector3 basePosition, Quaternion baseRotation)
    {
        ResolveSafeRespawnPose(basePosition, baseRotation, out initialSpawnPosition, out initialSpawnRotation);
    }

    private void ResolveSafeRespawnPose(Vector3 basePosition, Quaternion baseRotation, out Vector3 safePosition, out Quaternion safeRotation)
    {
        Vector3 fallbackUp = Vector3.up;
        float vehicleClearance = EstimateVehicleClearance();

        if (TryFindGroundBelow(basePosition, out RaycastHit hit))
        {
            Vector3 surfaceNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : fallbackUp;
            safeRotation = BuildGroundAlignedRotation(baseRotation, surfaceNormal);
            safePosition = hit.point + surfaceNormal * vehicleClearance;
            return;
        }

        safePosition = basePosition + fallbackUp * vehicleClearance;
        safeRotation = BuildGroundAlignedRotation(baseRotation, fallbackUp);
    }

    private bool TryFindGroundBelow(Vector3 referencePosition, out RaycastHit groundHit)
    {
        Vector3 probeOrigin = referencePosition + Vector3.up * Mathf.Max(4f, respawnProbeHeight);
        float probeDistance = Mathf.Max(8f, respawnProbeHeight + respawnProbeDistance);
        RaycastHit[] hits = Physics.RaycastAll(probeOrigin, Vector3.down, probeDistance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger)
            {
                continue;
            }

            if (activeVehicleController != null && hit.collider.transform.IsChildOf(activeVehicleController.transform))
            {
                continue;
            }

            if (hit.normal.y < minGroundNormalY)
            {
                continue;
            }

            groundHit = hit;
            return true;
        }

        groundHit = default;
        return false;
    }

    private float EstimateVehicleClearance()
    {
        float clearance = 1.2f;
        if (activeVehicleController == null)
        {
            return clearance + Mathf.Max(0.1f, respawnHeightOffset);
        }

        Collider[] colliders = activeVehicleController.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            clearance = Mathf.Max(clearance, collider.bounds.extents.y);
        }

        return clearance + Mathf.Max(0.1f, respawnHeightOffset);
    }

    private static Quaternion BuildGroundAlignedRotation(Quaternion baseRotation, Vector3 surfaceNormal)
    {
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Quaternion yawRotation = Quaternion.Euler(0f, baseRotation.eulerAngles.y, 0f);
        Vector3 forward = Vector3.ProjectOnPlane(yawRotation * Vector3.forward, normal);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(yawRotation * Vector3.right, normal);
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, normal);
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (fadeOverlayImage == null)
        {
            yield break;
        }

        fadeOverlayImage.gameObject.SetActive(true);
        Color color = fadeOverlayImage.color;
        color.r = fadeColor.r;
        color.g = fadeColor.g;
        color.b = fadeColor.b;

        if (duration <= 0f)
        {
            color.a = to;
            fadeOverlayImage.color = color;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(from, to, t);
            fadeOverlayImage.color = color;
            yield return null;
        }

        color.a = to;
        fadeOverlayImage.color = color;
    }

    private void ResolveFadeOverlay()
    {
        if (fadeOverlayImage != null && fadeOverlayImage.gameObject != null)
        {
            return;
        }

        Canvas canvas = GlobalUiCoordinator.PrimaryCanvas;
        if (canvas == null)
        {
            return;
        }

        Transform existing = canvas.transform.Find(FadeOverlayName);
        if (existing != null)
        {
            fadeOverlayImage = existing.GetComponent<Image>();
            return;
        }

        GameObject overlayObject = new GameObject(FadeOverlayName, typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(canvas.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        fadeOverlayImage = overlayObject.GetComponent<Image>();
        fadeOverlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeOverlayImage.raycastTarget = true;
        overlayObject.SetActive(false);
    }

    private void ResolveWarningOverlay()
    {
        if (warningCanvasGroup != null && warningCanvasGroup.gameObject != null && warningText != null)
        {
            return;
        }

        Canvas canvas = GlobalUiCoordinator.PrimaryCanvas;
        if (canvas == null)
        {
            return;
        }

        Transform existing = canvas.transform.Find(WarningLabelName);
        if (existing != null)
        {
            warningCanvasGroup = existing.GetComponent<CanvasGroup>();
            warningText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            return;
        }

        GameObject warningObject = new GameObject(WarningLabelName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        warningObject.transform.SetParent(canvas.transform, false);

        RectTransform warningRect = warningObject.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 1f);
        warningRect.anchorMax = new Vector2(0.5f, 1f);
        warningRect.pivot = new Vector2(0.5f, 1f);
        warningRect.anchoredPosition = new Vector2(0f, -36f);
        warningRect.sizeDelta = new Vector2(460f, 52f);

        Image warningBackground = warningObject.GetComponent<Image>();
        warningBackground.color = warningBackgroundColor;
        warningBackground.raycastTarget = false;

        warningCanvasGroup = warningObject.GetComponent<CanvasGroup>();
        warningCanvasGroup.alpha = 0f;
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(warningObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        warningText = textObject.GetComponent<TextMeshProUGUI>();
        warningText.text = LocalizationTable.Get("border_warning");
        warningText.fontSize = 24f;
        warningText.fontStyle = FontStyles.Bold;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.color = warningTextColor;
        warningText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            warningText.font = TMP_Settings.defaultFontAsset;
        }

        warningObject.SetActive(true);
        SetWarningVisible(false);
    }

    private void UpdateWarning(Vector3 vehiclePosition)
    {
        if (borderCollider == null)
        {
            SetWarningVisible(false);
            return;
        }

        Bounds bounds = borderCollider.bounds;
        float distanceToEdge = Mathf.Min(
            vehiclePosition.x - bounds.min.x,
            bounds.max.x - vehiclePosition.x,
            vehiclePosition.z - bounds.min.z,
            bounds.max.z - vehiclePosition.z);

        SetWarningVisible(distanceToEdge <= Mathf.Max(1f, warningDistance));
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningCanvasGroup == null)
        {
            return;
        }

        if (visible && warningText != null)
        {
            warningText.text = LocalizationTable.Get("border_warning");
        }

        warningCanvasGroup.alpha = visible ? 1f : 0f;
    }

    private static bool IsGameSceneActive()
    {
        return SceneManager.GetActiveScene().name.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
