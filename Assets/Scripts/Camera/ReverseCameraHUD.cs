using DeliveryDriver.Vehicle;
using UnityEngine;

/// <summary>
/// Bağımsız geri görüş kamerası. CameraFollow sahnedeyse bu script kendini
/// otomatik devre dışı bırakır; geri görüş kamerası CameraFollow tarafından yönetilir.
/// CameraFollow yoksa bu script devreye girer.
/// </summary>
public class ReverseCameraHUD : MonoBehaviour
{
    [Header("Hedef Araç")]
    [Tooltip("Takip edilecek araç. Boş bırakılırsa CarController olan objeyi bulur.")]
    [SerializeField] private Transform carTarget;

    [Header("Kamera Pozisyonu (Araç Üzerinde Lokal)")]
    [Tooltip("Arabanın arkasındaki kamera konumu (lokal). Z negatif = aracın arkası.")]
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 1.1f, -2.0f);
    [Tooltip("Kamera açısı (lokal). Y=180 geriye bakar, X pozitif = aşağı eğimli.")]
    [SerializeField] private Vector3 cameraLocalEuler = new Vector3(12f, 180f, 0f);
    [SerializeField] private float fieldOfView = 95f;

    [Header("Ekran Konumu (Viewport 0-1)")]
    [SerializeField] private float vpX = 0.2f;
    [SerializeField] private float vpY = 0.74f;
    [SerializeField] private float vpWidth = 0.60f;
    [SerializeField] private float vpHeight = 0.24f;

    [Header("Tetikleyici")]
    [SerializeField] private float reverseVelocityThreshold = -0.1f;
    [SerializeField] private float stationarySpeedThreshold = 1.0f;

    [Header("Frame Style")]
    [SerializeField] private Color borderColor = new Color(0.1f, 0.9f, 1f, 0.85f);
    [SerializeField] private float borderWidth = 2.5f;
    [SerializeField] private float framePadding = 6f;
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float gradientHeight = 28f;

    private Camera gameplayCamera;
    private Camera reverseCam;
    private CarController carController;
    private Rigidbody carRb;
    private VehicleCameraAnchors vehicleCameraAnchors;
    private Vector3 cachedFallbackOffset;
    private bool reverseCamShowing;
    private float reverseCamFadeAlpha;
    private Texture2D whiteTexture;
    private GUIStyle labelStyle;

    void Start()
    {
        if (carTarget != null)
        {
            SetTarget(carTarget);
            return;
        }

        SetTarget(VehicleCameraTargetResolver.ResolveDefaultTarget());
    }

    void CreateCamera()
    {
        var go = new GameObject("_ReverseCameraHUD");
        reverseCam = go.AddComponent<Camera>();
        reverseCam.nearClipPlane = 0.15f;
        reverseCam.farClipPlane = 300f;
        reverseCam.depth = 2f;
        reverseCam.rect = new Rect(vpX, vpY, vpWidth, vpHeight);
        reverseCam.enabled = false;
        CopyGameplayCameraSettings();
    }

    public void SetGameplayCamera(Camera sourceCamera)
    {
        gameplayCamera = sourceCamera;
        if (reverseCam != null)
        {
            CopyGameplayCameraSettings();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
        carTarget = binding.Target;
        carController = binding.CarController;
        carRb = binding.Rigidbody;
        vehicleCameraAnchors = binding.CameraAnchors;
        cachedFallbackOffset = ResolveFallbackOffset();

        if (carTarget == null)
        {
            reverseCamFadeAlpha = 0f;
            if (reverseCam != null)
            {
                reverseCam.enabled = false;
            }
            return;
        }

        if (reverseCam == null)
        {
            CreateCamera();
        }
    }

    public void Configure(
        Vector3 localOffset,
        Vector3 localEuler,
        float cameraFov,
        Rect viewport,
        float reverseThreshold,
        float stationaryThresholdValue,
        Color frameColor,
        float frameLineWidth,
        float frameInset,
        float fadeSpeedValue,
        float gradientHeightValue)
    {
        cameraLocalOffset = localOffset;
        cameraLocalEuler = localEuler;
        fieldOfView = cameraFov;
        vpX = viewport.x;
        vpY = viewport.y;
        vpWidth = viewport.width;
        vpHeight = viewport.height;
        reverseVelocityThreshold = reverseThreshold;
        stationarySpeedThreshold = stationaryThresholdValue;
        borderColor = frameColor;
        borderWidth = frameLineWidth;
        framePadding = frameInset;
        fadeSpeed = fadeSpeedValue;
        gradientHeight = gradientHeightValue;
        cachedFallbackOffset = ResolveFallbackOffset();

        if (reverseCam != null)
        {
            CopyGameplayCameraSettings();
        }
    }

    void CopyGameplayCameraSettings()
    {
        if (reverseCam == null)
        {
            return;
        }

        Camera source = gameplayCamera != null ? gameplayCamera : Camera.main;
        reverseCam.fieldOfView = fieldOfView;
        reverseCam.clearFlags = source != null ? source.clearFlags : CameraClearFlags.Skybox;
        reverseCam.backgroundColor = source != null ? source.backgroundColor : Color.black;
        reverseCam.cullingMask = source != null ? source.cullingMask : ~0;
        reverseCam.allowHDR = source != null && source.allowHDR;
        reverseCam.allowMSAA = source != null && source.allowMSAA;

        Skybox sourceSkybox = source != null ? source.GetComponent<Skybox>() : null;
        Material skyboxMaterial = sourceSkybox != null && sourceSkybox.material != null
            ? sourceSkybox.material
            : RenderSettings.skybox;
        if (skyboxMaterial != null)
        {
            Skybox reverseSkybox = reverseCam.GetComponent<Skybox>();
            if (reverseSkybox == null)
            {
                reverseSkybox = reverseCam.gameObject.AddComponent<Skybox>();
            }
            reverseSkybox.material = skyboxMaterial;
        }
    }

    void LateUpdate()
    {
        if (carTarget == null)
        {
            Transform resolvedTarget = VehicleCameraTargetResolver.ResolveDefaultTarget();
            if (resolvedTarget != null)
            {
                SetTarget(resolvedTarget);
            }
            return;
        }

        if (reverseCam == null)
        {
            CreateCamera();
        }

        bool shouldShow = IsReversing();
        float targetAlpha = shouldShow ? 1f : 0f;
        reverseCamFadeAlpha = Mathf.MoveTowards(reverseCamFadeAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        bool shouldEnableCamera = reverseCamFadeAlpha > 0.01f;
        if (shouldEnableCamera != reverseCamShowing)
        {
            reverseCamShowing = shouldEnableCamera;
            reverseCam.enabled = reverseCamShowing;
        }

        if (reverseCamShowing)
        {
            PositionCamera();
        }
    }

    bool IsReversing()
    {
        if (carRb == null)
            return carController != null && carController.IsReverseInputActive;

        float localZ = carTarget.InverseTransformDirection(carRb.linearVelocity).z;
        if (localZ < reverseVelocityThreshold) return true;

        if (carRb.linearVelocity.magnitude < stationarySpeedThreshold
            && carController != null
            && carController.IsReverseInputActive)
        {
            return true;
        }

        return false;
    }

    void PositionCamera()
    {
        Transform reverseAnchor = vehicleCameraAnchors != null ? vehicleCameraAnchors.ReverseCameraAnchor : null;
        Quaternion yaw = Quaternion.Euler(0f, carTarget.eulerAngles.y, 0f);

        if (reverseAnchor != null)
        {
            reverseCam.transform.position = reverseAnchor.position;
            reverseCam.transform.rotation = reverseAnchor.rotation * Quaternion.Euler(cameraLocalEuler);
            return;
        }

        reverseCam.transform.position = carTarget.TransformPoint(cachedFallbackOffset);
        reverseCam.transform.rotation = yaw * Quaternion.Euler(cameraLocalEuler);
    }

    Vector3 ResolveFallbackOffset()
    {
        if (carTarget == null)
        {
            return cameraLocalOffset;
        }

        Renderer[] renderers = carTarget.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform == null || renderer.transform.name.Contains("Wheel"))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            for (int j = 0; j < corners.Length; j++)
            {
                Vector3 localPoint = carTarget.InverseTransformPoint(corners[j]);
                if (!hasBounds)
                {
                    min = localPoint;
                    max = localPoint;
                    hasBounds = true;
                    continue;
                }

                min = Vector3.Min(min, localPoint);
                max = Vector3.Max(max, localPoint);
            }
        }

        if (!hasBounds)
        {
            return cameraLocalOffset;
        }

        float height = Mathf.Max(max.y + 0.35f, 0.85f);
        float rearZ = min.z - 0.45f;
        return new Vector3((min.x + max.x) * 0.5f, height, rearZ);
    }

    void OnGUI()
    {
        if (reverseCamFadeAlpha < 0.01f)
        {
            return;
        }

        EnsureGuiResources();

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float x = vpX * screenWidth;
        float y = (1f - vpY - vpHeight) * screenHeight;
        float width = vpWidth * screenWidth;
        float height = vpHeight * screenHeight;

        Color previousColor = GUI.color;
        float alpha = reverseCamFadeAlpha;

        GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.DrawTexture(new Rect(x - framePadding, y - framePadding, width + framePadding * 2f, framePadding), whiteTexture);
        GUI.DrawTexture(new Rect(x - framePadding, y + height, width + framePadding * 2f, framePadding), whiteTexture);
        GUI.DrawTexture(new Rect(x - framePadding, y, framePadding, height), whiteTexture);
        GUI.DrawTexture(new Rect(x + width, y, framePadding, height), whiteTexture);

        Color frameColor = borderColor;
        frameColor.a *= alpha;
        GUI.color = frameColor;
        GUI.DrawTexture(new Rect(x - borderWidth, y - borderWidth, width + borderWidth * 2f, borderWidth), whiteTexture);
        GUI.DrawTexture(new Rect(x - borderWidth, y + height, width + borderWidth * 2f, borderWidth), whiteTexture);
        GUI.DrawTexture(new Rect(x - borderWidth, y, borderWidth, height), whiteTexture);
        GUI.DrawTexture(new Rect(x + width, y, borderWidth, height), whiteTexture);

        float maxGradientHeight = Mathf.Min(gradientHeight, height * 0.4f);
        for (int i = 0; i < 8; i++)
        {
            float t = i / 8f;
            GUI.color = new Color(0f, 0f, 0f, (1f - t) * 0.45f * alpha);
            GUI.DrawTexture(new Rect(x, y + t * maxGradientHeight, width, maxGradientHeight / 8f), whiteTexture);
        }

        GUI.color = new Color(1f, 1f, 1f, 0.9f * alpha);
        labelStyle.fontSize = Mathf.Max(10, (int)(screenHeight * 0.014f));
        GUI.Label(new Rect(x, y + 2f, width, labelStyle.fontSize + 6f), "REAR VIEW", labelStyle);

        GUI.color = previousColor;
    }

    void EnsureGuiResources()
    {
        if (whiteTexture == null)
        {
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.85f, 0.95f, 1f, 1f);
        }
    }

    void OnDestroy()
    {
        if (reverseCam != null) Destroy(reverseCam.gameObject);
        if (whiteTexture != null) Destroy(whiteTexture);
    }
}
