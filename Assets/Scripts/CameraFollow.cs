using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using TrafficSystem;
using DeliveryDriver.Quest.UI;
using DeliveryDriver.Vehicle;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Takip edilecek arac")]
    public Transform target;

    [Header("Offset Settings")]
    [Tooltip("Kameranin araca gore konumu (X, Y, Z). Z negatif olmali ki arkada dursun.")]
    [SerializeField] private Vector3 offset = new Vector3(0, 4f, -8f);
    [SerializeField] private Vector3 lookAtTargetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Smooth Settings")]
    [Tooltip("Kamera takip yumusakligi (Dusuk = daha siki, Yuksek = daha gevek)")]
    [SerializeField] private float translateSmoothTime = 0.2f;
    [Tooltip("Yuksek hizda takip gecikmesini azaltmak icin minimum smooth time")]
    [SerializeField] private float minTranslateSmoothTime = 0.08f;
    [Tooltip("Bu hizdan sonra kamera takipte sikilasir (km/h)")]
    [SerializeField] private float tightenFollowStartSpeedKmh = 70f;
    [Tooltip("Bu hizda minimum smooth time degerine ulasir (km/h)")]
    [SerializeField] private float tightenFollowFullSpeedKmh = 170f;
    [Tooltip("Donus yumusakligi (Dusuk = daha gecikmeli, Yuksek = daha hizli)")]
    [SerializeField] private float rotationSmoothSpeed = 1.5f;
    [Tooltip("Kamera rotasyonu tamamen arabayi takip etsin mi?")]
    [SerializeField] private bool followRotation = false;

    [Header("Dinamik Mesafe")]
    [Tooltip("İleri giderken kameranın geri çekildiği maksimum ekstra mesafe (m)")]
    [SerializeField] private float forwardZoomOutExtra = 2.5f;
    [Tooltip("Bu hızda (km/h) maksimum geri çekilme sağlanır")]
    [SerializeField] private float forwardZoomOutSpeedKmh = 80f;
    [Tooltip("Mesafe değişim yumuşaklığı (düşük = daha yavaş uzaklaşma)")]
    [SerializeField] private float dynamicZoomLerpSpeed = 1.2f;

    [Header("Speed Feel")]
    [Tooltip("Arac hizina gore kamera FOV degissin mi")]
    [SerializeField] private bool enableSpeedFov = true;
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxSpeedFov = 78f;
    [SerializeField] private float fovMaxSpeedKmh = 190f;
    [SerializeField] private float fovLerpSpeed = 6f;

    [Header("MiniMap Settings")]
    [Tooltip("Sol altta minimap goster")]
    [SerializeField] private bool enableMiniMap = true;
    [SerializeField] private bool allowMiniMapToggleKey = true;
    [Tooltip("Minimap boyutu (ekran oranina gore, 0-1)")]
    [SerializeField] private float miniMapViewportSize = 0.3f;
    [Tooltip("Minimapin soldan ve alttan bosluk degeri (0-1)")]
    [SerializeField] private Vector2 miniMapViewportMargin = new Vector2(0.02f, 0.02f);
    [Tooltip("Minimap kamerasi arac uzerinden ne kadar yuksekte olsun")]
    [SerializeField] private float miniMapHeight = 35f;
    [Tooltip("Minimapin ortografik gorus capi")]
    [SerializeField] private float miniMapOrthoSize = 16f;
    [Tooltip("Minimap takip yumusakligi (kucuk = daha sabit/hizli)")]
    [SerializeField] private float miniMapFollowSmoothTime = 0.06f;
    [Tooltip("Minimap kamera acisi arac yonune donsun mu")]
    [SerializeField] private bool miniMapRotateWithTarget = false;
    [Tooltip("Minimapin gosterecegi layerlar")]
    [SerializeField] private LayerMask miniMapCullingMask = ~0;
    [SerializeField] private Color miniMapBackgroundColor = new Color(0.18f, 0.22f, 0.24f, 1f);
    [Tooltip("Haritayi bir kez yakalayıp hafif bir zemin olarak kullan")]
    [SerializeField] private bool useCachedMiniMapSurface = true;
    [Tooltip("Cached minimap texture cozunurlugu")]
    [SerializeField] private int cachedMiniMapResolution = 1024;
    [Tooltip("Cached minimap almadan once kac frame beklensin")]
    [SerializeField] private int cachedMiniMapWarmupFrames = 2;
    [Tooltip("Procedural minimapte yol rengi")]
    [SerializeField] private Color cachedMiniMapRoadColor = new Color(0.72f, 0.76f, 0.8f, 1f);
    [Tooltip("Procedural minimapte yol dis cizgi rengi")]
    [SerializeField] private Color cachedMiniMapRoadOutlineColor = new Color(0.14f, 0.16f, 0.18f, 1f);
    [Tooltip("Procedural minimapte yol kalinligi (piksel)")]
    [SerializeField] private int cachedMiniMapRoadWidthPixels = 4;
    [Tooltip("Minimapi sadece secili layerlar ile sinirlar")]
    [SerializeField] private bool autoConfigureMiniMapFilter = true;
    [SerializeField] private string miniMapBuildingLayerName = "MiniMapBuilding";
    [SerializeField] private string miniMapRoadLayerName = "Road";
    [SerializeField] private string miniMapMarkerLayerName = "MiniMapMarker";
    [SerializeField] private bool includeRoadLayerInMiniMap = true;
    [SerializeField] private bool autoAssignBuildingsToMiniMapLayer = true;
    [SerializeField] private string[] miniMapBuildingNameKeywords =
    {
        "building", "house", "shop", "market", "restaurant", "factory",
        "stadium", "residential", "apartment", "office", "hospital", "school"
    };
    [Tooltip("Minimap sahne disini gostermesin diye bu alan icine sinirla")]
    [SerializeField] private bool limitMiniMapToBounds = true;
    [Tooltip("Minimap kamera merkezi bu collider sinirlari icinde kalir")]
    [SerializeField] private BoxCollider miniMapBounds;
    [Header("Geri Görüş Kamerası (HUD)")]
    [Tooltip("Araç geri giderken ekranın üstünde geri görüş kamerası göster")]
    [SerializeField] private bool enableReverseCamera = true;
    [Tooltip("Araç arkasındaki kamera konumu (lokal). Z negatif = aracın arkası.")]
    [SerializeField] private Vector3 reverseCamOffset = new Vector3(0f, 1.1f, -2.0f);
    [Tooltip("Kamera açısı. Y=180 geriye bakar, X pozitif = aşağı eğimli.")]
    [SerializeField] private Vector3 reverseCamEuler = new Vector3(12f, 180f, 0f);
    [SerializeField] private float reverseCamFov = 95f;
    [Tooltip("Viewport sol kenarı (0-1)")]
    [SerializeField] private float reverseCamVpX = 0.2f;
    [Tooltip("Viewport alt kenarı (0=alt, 1=üst). 0.74 → ekranın üst kısmı.")]
    [SerializeField] private float reverseCamVpY = 0.74f;
    [SerializeField] private float reverseCamVpW = 0.60f;
    [SerializeField] private float reverseCamVpH = 0.24f;
    [Tooltip("Bu hızın (m/s) altında araç durağan sayılır; geri tuşu kamerayı açar.")]
    [SerializeField] private float reverseCamStationaryThreshold = 1.0f;

    [Header("Geri Görüş Kamerası Stili")]
    [Tooltip("Çerçeve rengi")]
    [SerializeField] private Color reverseCamBorderColor = new Color(0.1f, 0.9f, 1f, 0.85f);
    [Tooltip("Çerçeve kalınlığı (piksel)")]
    [SerializeField] private float reverseCamBorderWidth = 2.5f;
    [Tooltip("Arka plan karartma çerçevesi kalınlığı (piksel)")]
    [SerializeField] private float reverseCamFramePadding = 6f;
    [Tooltip("Fade animasyon hızı")]
    [SerializeField] private float reverseCamFadeSpeed = 6f;
    [Tooltip("Üst gradient yüksekliği (piksel)")]
    [SerializeField] private float reverseCamGradientHeight = 28f;

    [Header("MiniMap Marker")]
    [SerializeField] private bool showMiniMapPlayerMarker = true;
    [SerializeField] private float miniMapPlayerMarkerHeight = 20f;
    [SerializeField] private Vector3 miniMapPlayerMarkerScale = new Vector3(3.5f, 8f, 3.5f);
    [SerializeField] private float miniMapPlayerMarkerSpinSpeed = 130f;
    [SerializeField] private Color miniMapPlayerMarkerColor = new Color(0.15f, 1f, 0.35f, 1f);

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private CarController carController;
    private Camera mainCamera;
    private Camera miniMapCamera;
    private Vector3 miniMapVelocity;
    private bool hasMiniMapRuntimeBounds;
    private Bounds miniMapRuntimeBounds;
    private GameObject miniMapPlayerMarker;
    private Material miniMapPlayerMarkerMaterial;
    private int cachedMiniMapMarkerLayer = int.MinValue;
    private Texture2D cachedMiniMapTexture;
    private GameObject cachedMiniMapSurface;
    private Material cachedMiniMapSurfaceMaterial;
    private Coroutine cachedMiniMapBuildRoutine;
    private RoadGraphBuilder cachedMiniMapRoadGraphBuilder;
    private Camera reverseCamHUD;
    private bool reverseCamShowing;
    private float reverseCamFadeAlpha;
    private Texture2D reverseCamWhiteTex;
    private GUIStyle reverseCamLabelStyle;
    private float currentZoomOffset;
    private VehicleCameraAnchors vehicleCameraAnchors;
    private CinemachineBrain cinemachineBrain;
    private CinemachineCamera gameplayCamera;
    private CinemachineFollow cinemachineFollow;
    private CinemachineRotationComposer rotationComposer;
    private ReverseCameraHUD reverseCameraController;
    private MinimapCamera minimapCameraController;
    private float currentGameplayFov;
    private bool warnedMissingGameplayCamera;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        cinemachineBrain = GetComponent<CinemachineBrain>();
        currentGameplayFov = baseFov;

        ResolveGameplayRig();
        EnsureExternalCameraControllers();
    }

    void Start()
    {
        if (target != null)
        {
            SetTarget(target);
            return;
        }

        TryResolveTarget();

        if (target == null)
        {
            Debug.LogWarning("[CameraFollow] Target (Arac) bulunamadi!");
        }
    }

    public void SetTarget(Transform newTarget)
    {
        VehicleCameraBinding binding = VehicleCameraTargetResolver.Resolve(newTarget);
        target = binding.Target;
        targetRb = binding.Rigidbody;
        carController = binding.CarController;
        vehicleCameraAnchors = binding.CameraAnchors;

        ResolveGameplayRig();
        EnsureExternalCameraControllers();

        if (target == null)
        {
            if (cinemachineBrain != null)
            {
                cinemachineBrain.WorldUpOverride = null;
            }

            reverseCameraController?.SetTarget(null);
            minimapCameraController?.SetPlayer(null);
            return;
        }

        currentZoomOffset = 0f;
        currentGameplayFov = ResolveCurrentLensFov();

        BindGameplayRig();
        reverseCameraController?.SetGameplayCamera(mainCamera);
        reverseCameraController?.SetTarget(target);
        minimapCameraController?.SetPlayer(target);
        ApplyCinemachineRigSettings(true);

        Debug.Log($"[CameraFollow] Target updated: target={target.name}, targetRb={targetRb != null}, carController={carController != null}, cinemachine={gameplayCamera != null}");
    }

    void LateUpdate()
    {
        if (target == null)
        {
            TryResolveTarget();
            return;
        }

        UpdateSpeedFov(Time.deltaTime);
        UpdateCinemachineFollowOffset(Time.deltaTime);
        ApplyCinemachineRigSettings(false);
    }

    void UpdateSpeedFov(float deltaTime)
    {
        float desiredFov = baseFov;
        if (enableSpeedFov && targetRb != null)
        {
            float speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
            float t = fovMaxSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / fovMaxSpeedKmh) : 0f;
            desiredFov = Mathf.Lerp(baseFov, maxSpeedFov, t);
        }

        currentGameplayFov = Mathf.Lerp(currentGameplayFov, desiredFov, Mathf.Max(0.01f, fovLerpSpeed) * deltaTime);
    }

    void TryResolveTarget()
    {
        Transform resolvedTarget = VehicleCameraTargetResolver.ResolveDefaultTarget();
        if (resolvedTarget != null && resolvedTarget != target)
        {
            SetTarget(resolvedTarget);
        }
    }

    void ResolveGameplayRig()
    {
        if (mainCamera == null)
        {
            mainCamera = GetComponent<Camera>();
        }

        if (cinemachineBrain == null)
        {
            cinemachineBrain = GetComponent<CinemachineBrain>();
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        cinemachineFollow = gameplayCamera != null ? gameplayCamera.GetComponent<CinemachineFollow>() : null;
        rotationComposer = gameplayCamera != null ? gameplayCamera.GetComponent<CinemachineRotationComposer>() : null;

        if (gameplayCamera == null && !warnedMissingGameplayCamera)
        {
            warnedMissingGameplayCamera = true;
            Debug.LogWarning("[CameraFollow] No CinemachineCamera found. Main gameplay follow will stay unbound.");
        }
    }

    void EnsureExternalCameraControllers()
    {
        int minimapMarkerLayer = ResolveMiniMapMarkerLayer();
        if (mainCamera != null && minimapMarkerLayer >= 0)
        {
            mainCamera.cullingMask &= ~(1 << minimapMarkerLayer);
        }

        if (enableReverseCamera)
        {
            if (reverseCameraController == null)
            {
                reverseCameraController = FindFirstObjectByType<ReverseCameraHUD>();
            }

            if (reverseCameraController == null)
            {
                reverseCameraController = GetComponent<ReverseCameraHUD>();
                if (reverseCameraController == null)
                {
                    reverseCameraController = gameObject.AddComponent<ReverseCameraHUD>();
                }
            }

            reverseCameraController.Configure(
                reverseCamOffset,
                reverseCamEuler,
                reverseCamFov,
                new Rect(reverseCamVpX, reverseCamVpY, reverseCamVpW, reverseCamVpH),
                -0.1f,
                reverseCamStationaryThreshold,
                reverseCamBorderColor,
                reverseCamBorderWidth,
                reverseCamFramePadding,
                reverseCamFadeSpeed,
                reverseCamGradientHeight);
            reverseCameraController.SetGameplayCamera(mainCamera);
        }

        if (!enableMiniMap)
        {
            return;
        }

        if (minimapCameraController == null)
        {
            minimapCameraController = FindFirstObjectByType<MinimapCamera>();
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
            miniMapHeight,
            miniMapOrthoSize,
            miniMapRotateWithTarget,
            allowMiniMapToggleKey,
            useStandaloneOverlay,
            miniMapViewportSize,
            miniMapViewportMargin,
            miniMapCullingMask,
            miniMapBackgroundColor);

        if (minimapUi != null && target != null)
        {
            minimapUi.SetPlayerTransform(target);
        }
    }

    void BindGameplayRig()
    {
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

    void UpdateCinemachineFollowOffset(float deltaTime)
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
                float zoomT = Mathf.Clamp01(forwardKmh / Mathf.Max(1f, forwardZoomOutSpeedKmh));
                desiredZoomOffset = -zoomT * forwardZoomOutExtra;
            }
        }

        currentZoomOffset = Mathf.Lerp(currentZoomOffset, desiredZoomOffset, Mathf.Max(0.01f, dynamicZoomLerpSpeed) * deltaTime);
    }

    void ApplyCinemachineRigSettings(bool snap)
    {
        if (cinemachineFollow != null)
        {
            Vector3 desiredOffset = new Vector3(offset.x, offset.y, offset.z + currentZoomOffset);
            if ((cinemachineFollow.FollowOffset - desiredOffset).sqrMagnitude > 0.0001f)
            {
                cinemachineFollow.FollowOffset = desiredOffset;
            }
        }

        if (rotationComposer != null)
        {
            rotationComposer.TargetOffset = lookAtTargetOffset;
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

    float ResolveCurrentLensFov()
    {
        if (gameplayCamera != null)
        {
            return gameplayCamera.Lens.FieldOfView;
        }

        if (mainCamera != null)
        {
            return mainCamera.fieldOfView;
        }

        return baseFov;
    }

    void HandleCameraMovement(float deltaTime)
    {
        // --- Geri/fren tespiti ve dinamik Z offset ---
        bool reverseInput = carController != null && carController.IsReverseInputActive;
        bool isReversing = false;
        if (targetRb != null)
        {
            float localVelZ = target.InverseTransformDirection(targetRb.linearVelocity).z;
            // Geri tuşuna basılıyorsa VEYA araç gerçekten geri gidiyorsa → kamera sabit
            isReversing = reverseInput || localVelZ < -0.3f;

            if (isReversing)
            {
                currentVelocity = Vector3.zero;
                currentZoomOffset = Mathf.Lerp(currentZoomOffset, 0f, 10f * deltaTime);
            }
            else
            {
                // İleri giderken: hıza göre kamerayı yavaşça geri çek
                float forwardKmh = Mathf.Max(0f, localVelZ * 3.6f);
                float zoomT = Mathf.Clamp01(forwardKmh / Mathf.Max(1f, forwardZoomOutSpeedKmh));
                float targetZoom = -zoomT * forwardZoomOutExtra;
                currentZoomOffset = Mathf.Lerp(currentZoomOffset, targetZoom, dynamicZoomLerpSpeed * deltaTime);
            }
        }

        Vector3 targetOffset = new Vector3(offset.x, offset.y, offset.z + currentZoomOffset);

        // --- Pozisyon hesaplama ---
        Vector3 desiredPosition;
        if (followRotation)
        {
            desiredPosition = target.TransformPoint(targetOffset);
        }
        else
        {
            Quaternion yawRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            desiredPosition = target.position + (yawRotation * targetOffset);
        }

        // --- Pozisyonu uygula ---
        if (isReversing)
        {
            // Geri/fren: SmoothDamp ataleti sıfırla, pozisyona anında snap yap
            currentVelocity = Vector3.zero;
            transform.position = desiredPosition;
        }
        else
        {
            float smoothTime = translateSmoothTime;
            if (targetRb != null)
            {
                float speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
                float tightenT = Mathf.InverseLerp(tightenFollowStartSpeedKmh, tightenFollowFullSpeedKmh, speedKmh);
                smoothTime = Mathf.Lerp(translateSmoothTime, minTranslateSmoothTime, tightenT);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                Mathf.Max(0.01f, smoothTime),
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime));
        }

        // 6. Rotasyon ayari — her zaman arabanin biraz ustune bak
        Vector3 lookAtTarget = target.position + Vector3.up * 1.5f;
        Vector3 direction = lookAtTarget - transform.position;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            float currentRotSpeed = rotationSmoothSpeed;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotSpeed * deltaTime);
        }
    }

    void SetupMiniMapCamera()
    {
        if (!enableMiniMap || miniMapCamera != null)
        {
            return;
        }

        GameObject miniMapCameraObj = new GameObject("MiniMapCamera");
        miniMapCamera = miniMapCameraObj.AddComponent<Camera>();

        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = miniMapOrthoSize;
        miniMapCamera.cullingMask = GetMiniMapRuntimeCullingMask();
        miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
        miniMapCamera.backgroundColor = miniMapBackgroundColor;
        miniMapCamera.nearClipPlane = 0.1f;
        miniMapCamera.farClipPlane = 500f;
        miniMapCamera.depth = 10f;
        miniMapCamera.rect = BuildMiniMapRect();
    }

    void ConfigureMiniMapLayerFilter()
    {
        if (!autoConfigureMiniMapFilter)
        {
            return;
        }

        int buildingLayer = LayerMask.NameToLayer(miniMapBuildingLayerName);
        int roadLayer = LayerMask.NameToLayer(miniMapRoadLayerName);
        int markerLayer = ResolveMiniMapMarkerLayer();
        if (buildingLayer < 0 || markerLayer < 0)
        {
            Debug.LogWarning("CameraFollow: MiniMapBuilding veya MiniMapMarker layer tanimli degil. MiniMap filtresi uygulanamadi.");
            return;
        }

        if (autoAssignBuildingsToMiniMapLayer)
        {
            AssignBuildingsToMiniMapLayer(buildingLayer);
        }

        int cullingMask = (1 << buildingLayer) | (1 << markerLayer);
        if (includeRoadLayerInMiniMap && roadLayer >= 0)
        {
            cullingMask |= 1 << roadLayer;
        }

        miniMapCullingMask = cullingMask;
    }

    void EnsureMiniMapRuntimeSetup()
    {
        if (useCachedMiniMapSurface)
        {
            RequestCachedMiniMapSurfaceBuild();
        }
        else
        {
            ConfigureMiniMapLayerFilter();
        }

        int markerLayer = ResolveMiniMapMarkerLayer();
        if (mainCamera != null && markerLayer >= 0)
        {
            mainCamera.cullingMask &= ~(1 << markerLayer);
        }
    }

    int ResolveMiniMapMarkerLayer()
    {
        if (cachedMiniMapMarkerLayer == int.MinValue)
        {
            cachedMiniMapMarkerLayer = LayerMask.NameToLayer(miniMapMarkerLayerName);
        }

        return cachedMiniMapMarkerLayer;
    }

    void AssignBuildingsToMiniMapLayer(int buildingLayer)
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        HashSet<int> processedRoots = new HashSet<int>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == null)
            {
                continue;
            }

            Transform buildingRoot = FindBuildingRoot(renderer.transform);
            if (buildingRoot == null)
            {
                continue;
            }

            int rootId = buildingRoot.GetInstanceID();
            if (!processedRoots.Add(rootId))
            {
                continue;
            }

            SetLayerRecursively(buildingRoot, buildingLayer);
        }
    }

    Transform FindBuildingRoot(Transform current)
    {
        Transform bestMatch = null;
        while (current != null)
        {
            if (IsBuildingName(current.name))
            {
                bestMatch = current;
            }

            current = current.parent;
        }

        return bestMatch;
    }

    bool IsBuildingName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || miniMapBuildingNameKeywords == null)
        {
            return false;
        }

        string lowerName = objectName.ToLowerInvariant();
        for (int i = 0; i < miniMapBuildingNameKeywords.Length; i++)
        {
            string keyword = miniMapBuildingNameKeywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        Stack<Transform> stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            current.gameObject.layer = layer;

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }
    }

    void ResolveMiniMapBounds()
    {
        hasMiniMapRuntimeBounds = false;

        if (miniMapBounds != null)
        {
            return;
        }

        GameObject boundsObject = GameObject.Find("MiniMapBounds");
        if (boundsObject != null)
        {
            miniMapBounds = boundsObject.GetComponent<BoxCollider>();
            if (miniMapBounds != null)
            {
                return;
            }
        }

        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null && activeTerrain.terrainData != null)
        {
            Vector3 terrainSize = activeTerrain.terrainData.size;
            Vector3 terrainCenter = activeTerrain.transform.position + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f);
            miniMapRuntimeBounds = new Bounds(terrainCenter, terrainSize);
            hasMiniMapRuntimeBounds = true;
            EnsureRuntimeMiniMapBoundsObject(miniMapRuntimeBounds);
        }
    }

    void EnsureRuntimeMiniMapBoundsObject(Bounds runtimeBounds)
    {
        if (miniMapBounds != null)
        {
            return;
        }

        GameObject existing = GameObject.Find("MiniMapBounds");
        if (existing != null)
        {
            miniMapBounds = existing.GetComponent<BoxCollider>();
            if (miniMapBounds != null)
            {
                return;
            }
        }

        GameObject boundsObject = new GameObject("MiniMapBounds");
        boundsObject.transform.position = runtimeBounds.center;
        BoxCollider box = boundsObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = runtimeBounds.size;
        miniMapBounds = box;
    }

    void UpdateMiniMapCamera()
    {
        if (!enableMiniMap)
        {
            if (miniMapCamera != null)
            {
                miniMapCamera.gameObject.SetActive(false);
            }
            return;
        }

        if (miniMapCamera == null)
        {
            EnsureMiniMapRuntimeSetup();
            SetupMiniMapCamera();
            if (miniMapCamera == null)
            {
                return;
            }
        }

        miniMapCamera.gameObject.SetActive(true);

        if (useCachedMiniMapSurface && cachedMiniMapSurface == null)
        {
            RequestCachedMiniMapSurfaceBuild();
        }

        Rect desiredRect = BuildMiniMapRect();
        if (miniMapCamera.rect != desiredRect)
        {
            miniMapCamera.rect = desiredRect;
        }

        if (!Mathf.Approximately(miniMapCamera.orthographicSize, miniMapOrthoSize))
        {
            miniMapCamera.orthographicSize = miniMapOrthoSize;
        }

        int desiredMask = GetMiniMapRuntimeCullingMask();
        if (miniMapCamera.cullingMask != desiredMask)
        {
            miniMapCamera.cullingMask = desiredMask;
        }

        Vector3 followPosition = targetRb != null ? targetRb.position : target.position;
        Vector3 mapPosition = followPosition + Vector3.up * miniMapHeight;
        mapPosition = ClampMiniMapPositionToBounds(mapPosition);
        miniMapCamera.transform.position = Vector3.SmoothDamp(
            miniMapCamera.transform.position,
            mapPosition,
            ref miniMapVelocity,
            miniMapFollowSmoothTime,
            Mathf.Infinity,
            Mathf.Max(0.0001f, Time.deltaTime));

        float yRotation = miniMapRotateWithTarget ? target.eulerAngles.y : 0f;
        miniMapCamera.transform.rotation = Quaternion.Euler(90f, yRotation, 0f);
        UpdateCachedMiniMapSurface(mapPosition);
        UpdateMiniMapPlayerMarker(followPosition);
    }

    void UpdateMiniMapPlayerMarker(Vector3 followPosition)
    {
        if (!showMiniMapPlayerMarker)
        {
            RemoveMiniMapPlayerMarker();
            return;
        }

        if (miniMapPlayerMarker == null)
        {
            CreateMiniMapPlayerMarker();
            if (miniMapPlayerMarker == null)
            {
                return;
            }
        }

        miniMapPlayerMarker.SetActive(true);
        miniMapPlayerMarker.transform.position = followPosition + Vector3.up * miniMapPlayerMarkerHeight;
        miniMapPlayerMarker.transform.localScale = miniMapPlayerMarkerScale;
        miniMapPlayerMarker.transform.Rotate(Vector3.up, miniMapPlayerMarkerSpinSpeed * Time.deltaTime, Space.World);

        if (miniMapPlayerMarkerMaterial != null)
        {
            miniMapPlayerMarkerMaterial.color = miniMapPlayerMarkerColor;
        }
    }

    void CreateMiniMapPlayerMarker()
    {
        miniMapPlayerMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        miniMapPlayerMarker.name = "MiniMapPlayerMarker";
        int markerLayer = ResolveMiniMapMarkerLayer();
        if (markerLayer >= 0)
        {
            miniMapPlayerMarker.layer = markerLayer;
        }

        Collider markerCollider = miniMapPlayerMarker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        MeshRenderer renderer = miniMapPlayerMarker.GetComponent<MeshRenderer>();
        miniMapPlayerMarkerMaterial = MinimapShaderHelper.CreateColorMaterial(miniMapPlayerMarkerColor, renderer);
        if (miniMapPlayerMarkerMaterial != null && renderer != null)
        {
            renderer.material = miniMapPlayerMarkerMaterial;
        }
    }

    void RemoveMiniMapPlayerMarker()
    {
        if (miniMapPlayerMarker != null)
        {
            Destroy(miniMapPlayerMarker);
            miniMapPlayerMarker = null;
        }

        if (miniMapPlayerMarkerMaterial != null)
        {
            Destroy(miniMapPlayerMarkerMaterial);
            miniMapPlayerMarkerMaterial = null;
        }
    }

    Vector3 ClampMiniMapPositionToBounds(Vector3 mapPosition)
    {
        if (!limitMiniMapToBounds || miniMapBounds == null || miniMapCamera == null)
        {
            if (!limitMiniMapToBounds || miniMapCamera == null || !hasMiniMapRuntimeBounds)
            {
                return mapPosition;
            }
        }

        Bounds bounds = miniMapBounds != null ? miniMapBounds.bounds : miniMapRuntimeBounds;

        float halfHeight = miniMapCamera.orthographicSize;
        float halfWidth = miniMapCamera.orthographicSize * miniMapCamera.aspect;

        // Donen minimapta da sinir disi gostermemek icin daha guvenli yaricap kullan.
        float horizontalPadding = miniMapRotateWithTarget ? Mathf.Max(halfWidth, halfHeight) : halfWidth;
        float verticalPadding = miniMapRotateWithTarget ? Mathf.Max(halfWidth, halfHeight) : halfHeight;

        float clampedX = mapPosition.x;
        float clampedZ = mapPosition.z;

        if (bounds.size.x > horizontalPadding * 2f)
        {
            clampedX = Mathf.Clamp(mapPosition.x, bounds.min.x + horizontalPadding, bounds.max.x - horizontalPadding);
        }
        else
        {
            clampedX = bounds.center.x;
        }

        if (bounds.size.z > verticalPadding * 2f)
        {
            clampedZ = Mathf.Clamp(mapPosition.z, bounds.min.z + verticalPadding, bounds.max.z - verticalPadding);
        }
        else
        {
            clampedZ = bounds.center.z;
        }

        return new Vector3(clampedX, mapPosition.y, clampedZ);
    }

    Rect BuildMiniMapRect()
    {
        float size = Mathf.Clamp(Mathf.Max(miniMapViewportSize, 0.3f), 0.3f, 0.45f);
        float x = Mathf.Clamp01(miniMapViewportMargin.x);
        float y = Mathf.Clamp01(miniMapViewportMargin.y);
        return new Rect(x, y, size, size);
    }

    int GetMiniMapRuntimeCullingMask()
    {
        int markerLayer = ResolveMiniMapMarkerLayer();
        if (useCachedMiniMapSurface && cachedMiniMapSurface != null && markerLayer >= 0)
        {
            return 1 << markerLayer;
        }

        if (useCachedMiniMapSurface)
        {
            int fallbackMask = miniMapCullingMask;
            if (markerLayer >= 0)
            {
                fallbackMask |= 1 << markerLayer;
            }
            return fallbackMask;
        }

        return miniMapCullingMask;
    }

    void BuildCachedMiniMapSurface()
    {
        if (cachedMiniMapSurface != null)
        {
            return;
        }

        Bounds bounds;
        if (!TryGetMiniMapWorldBounds(out bounds))
        {
            return;
        }

        Texture2D roadTexture = BuildProceduralMiniMapTexture(bounds);
        if (roadTexture == null)
        {
            return;
        }

        cachedMiniMapTexture = roadTexture;

        cachedMiniMapSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cachedMiniMapSurface.name = "MiniMapCachedSurface";

        int markerLayer = ResolveMiniMapMarkerLayer();
        if (markerLayer >= 0)
        {
            cachedMiniMapSurface.layer = markerLayer;
        }

        if (miniMapCamera != null)
        {
            cachedMiniMapSurface.transform.SetParent(miniMapCamera.transform, false);
            cachedMiniMapSurface.transform.localPosition = new Vector3(0f, 0f, 100f);
            cachedMiniMapSurface.transform.localRotation = Quaternion.identity;
            cachedMiniMapSurface.transform.localScale = Vector3.one;
        }
        else
        {
            cachedMiniMapSurface.transform.localPosition = new Vector3(0f, 0f, 100f);
            cachedMiniMapSurface.transform.localRotation = Quaternion.identity;
            cachedMiniMapSurface.transform.localScale = Vector3.one;
        }

        Collider cachedSurfaceCollider = cachedMiniMapSurface.GetComponent<Collider>();
        if (cachedSurfaceCollider != null)
        {
            Destroy(cachedSurfaceCollider);
        }

        MeshRenderer renderer = cachedMiniMapSurface.GetComponent<MeshRenderer>();
        cachedMiniMapSurfaceMaterial = CreateCachedMiniMapMaterial(cachedMiniMapTexture, renderer);
        if (renderer != null && cachedMiniMapSurfaceMaterial != null)
        {
            renderer.sharedMaterial = cachedMiniMapSurfaceMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        UpdateCachedMiniMapSurface(targetRb != null ? targetRb.position + Vector3.up * miniMapHeight : target.position + Vector3.up * miniMapHeight);
    }

    void RequestCachedMiniMapSurfaceBuild()
    {
        if (cachedMiniMapSurface != null || cachedMiniMapBuildRoutine != null)
        {
            return;
        }

        cachedMiniMapBuildRoutine = StartCoroutine(BuildCachedMiniMapSurfaceWhenReady());
    }

    IEnumerator BuildCachedMiniMapSurfaceWhenReady()
    {
        int warmupFrames = Mathf.Max(1, cachedMiniMapWarmupFrames);
        for (int i = 0; i < warmupFrames; i++)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        BuildCachedMiniMapSurface();
        cachedMiniMapBuildRoutine = null;
    }

    Texture2D BuildProceduralMiniMapTexture(Bounds bounds)
    {
        if (!TryResolveMiniMapRoadGraph(out RoadGraph graph))
        {
            return null;
        }

        return MinimapRoadTextureBuilder.Build(
            graph,
            bounds,
            cachedMiniMapResolution,
            miniMapBackgroundColor,
            cachedMiniMapRoadColor,
            cachedMiniMapRoadOutlineColor,
            cachedMiniMapRoadWidthPixels);
    }

    bool TryResolveMiniMapRoadGraph(out RoadGraph graph)
    {
        if (cachedMiniMapRoadGraphBuilder == null)
        {
            cachedMiniMapRoadGraphBuilder = FindFirstObjectByType<RoadGraphBuilder>();
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

    Material CreateCachedMiniMapMaterial(Texture mapTexture, MeshRenderer fallbackRenderer)
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            if (fallbackRenderer == null || fallbackRenderer.sharedMaterial == null)
            {
                return null;
            }

            shader = fallbackRenderer.sharedMaterial.shader;
        }

        Material material = new Material(shader);
        material.color = Color.white;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", mapTexture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", mapTexture);
        }
        material.mainTexture = mapTexture;
        material.mainTextureScale = Vector2.one;
        material.mainTextureOffset = Vector2.zero;
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }
        return material;
    }

    void UpdateCachedMiniMapSurface(Vector3 mapPosition)
    {
        if (!useCachedMiniMapSurface || cachedMiniMapSurface == null || cachedMiniMapSurfaceMaterial == null || miniMapCamera == null)
        {
            return;
        }

        if (!TryGetMiniMapWorldBounds(out Bounds bounds))
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

    bool TryGetMiniMapWorldBounds(out Bounds bounds)
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

    void OnGUI()
    {
        if (!enableReverseCamera || reverseCamFadeAlpha < 0.01f) return;

        EnsureReverseCamGUIResources();

        // Viewport → ekran pikseli
        float sw = Screen.width;
        float sh = Screen.height;
        float rx = reverseCamVpX * sw;
        float ry = (1f - reverseCamVpY - reverseCamVpH) * sh; // GUI: sol üst orijin
        float rw = reverseCamVpW * sw;
        float rh = reverseCamVpH * sh;

        Color prevColor = GUI.color;
        float a = reverseCamFadeAlpha;

        // 1) Dış karartma çerçevesi (padding)
        float pad = reverseCamFramePadding;
        GUI.color = new Color(0f, 0f, 0f, 0.7f * a);
        // Üst
        GUI.DrawTexture(new Rect(rx - pad, ry - pad, rw + pad * 2f, pad), reverseCamWhiteTex);
        // Alt
        GUI.DrawTexture(new Rect(rx - pad, ry + rh, rw + pad * 2f, pad), reverseCamWhiteTex);
        // Sol
        GUI.DrawTexture(new Rect(rx - pad, ry, pad, rh), reverseCamWhiteTex);
        // Sağ
        GUI.DrawTexture(new Rect(rx + rw, ry, pad, rh), reverseCamWhiteTex);

        // 2) Parlak kenarlık çizgileri
        float bw = reverseCamBorderWidth;
        Color borderCol = reverseCamBorderColor;
        borderCol.a *= a;
        GUI.color = borderCol;
        // Üst
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, rw + bw * 2f, bw), reverseCamWhiteTex);
        // Alt
        GUI.DrawTexture(new Rect(rx - bw, ry + rh, rw + bw * 2f, bw), reverseCamWhiteTex);
        // Sol
        GUI.DrawTexture(new Rect(rx - bw, ry, bw, rh), reverseCamWhiteTex);
        // Sağ
        GUI.DrawTexture(new Rect(rx + rw, ry, bw, rh), reverseCamWhiteTex);

        // 3) Üst gradient overlay (karartma)
        float gradH = Mathf.Min(reverseCamGradientHeight, rh * 0.4f);
        for (int i = 0; i < 8; i++)
        {
            float t = i / 8f;
            float lineH = gradH / 8f;
            GUI.color = new Color(0f, 0f, 0f, (1f - t) * 0.45f * a);
            GUI.DrawTexture(new Rect(rx, ry + t * gradH, rw, lineH), reverseCamWhiteTex);
        }

        // 4) "REAR VIEW" etiketi
        GUI.color = new Color(1f, 1f, 1f, 0.9f * a);
        reverseCamLabelStyle.fontSize = Mathf.Max(10, (int)(sh * 0.014f));
        Rect labelRect = new Rect(rx, ry + 2f, rw, reverseCamLabelStyle.fontSize + 6f);
        GUI.Label(labelRect, "REAR VIEW", reverseCamLabelStyle);

        // 5) Köşe aksan çizgileri (küçük L şekilleri)
        float cornerLen = Mathf.Min(rw, rh) * 0.08f;
        float cornerW = bw * 1.5f;
        Color cornerCol = reverseCamBorderColor;
        cornerCol.a = Mathf.Min(1f, cornerCol.a * 1.4f) * a;
        GUI.color = cornerCol;
        // Sol üst
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx - bw, ry - bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sağ üst
        GUI.DrawTexture(new Rect(rx + rw - cornerLen + bw, ry - bw, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx + rw, ry - bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sol alt
        GUI.DrawTexture(new Rect(rx - bw, ry + rh, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx - bw, ry + rh - cornerLen + bw, cornerW, cornerLen), reverseCamWhiteTex);
        // Sağ alt
        GUI.DrawTexture(new Rect(rx + rw - cornerLen + bw, ry + rh, cornerLen, cornerW), reverseCamWhiteTex);
        GUI.DrawTexture(new Rect(rx + rw, ry + rh - cornerLen + bw, cornerW, cornerLen), reverseCamWhiteTex);

        GUI.color = prevColor;
    }

    void EnsureReverseCamGUIResources()
    {
        if (reverseCamWhiteTex == null)
        {
            reverseCamWhiteTex = new Texture2D(1, 1);
            reverseCamWhiteTex.SetPixel(0, 0, Color.white);
            reverseCamWhiteTex.Apply();
        }

        if (reverseCamLabelStyle == null)
        {
            reverseCamLabelStyle = new GUIStyle(GUI.skin.label);
            reverseCamLabelStyle.alignment = TextAnchor.UpperCenter;
            reverseCamLabelStyle.fontStyle = FontStyle.Bold;
            reverseCamLabelStyle.normal.textColor = new Color(0.85f, 0.95f, 1f, 1f);
        }
    }

    void OnDestroy()
    {
        RemoveMiniMapPlayerMarker();
        if (cachedMiniMapSurface != null) Destroy(cachedMiniMapSurface);
        if (cachedMiniMapSurfaceMaterial != null) Destroy(cachedMiniMapSurfaceMaterial);
        if (cachedMiniMapTexture != null) Destroy(cachedMiniMapTexture);
        if (cachedMiniMapBuildRoutine != null) StopCoroutine(cachedMiniMapBuildRoutine);
        if (reverseCamHUD != null) Destroy(reverseCamHUD.gameObject);
        if (reverseCamWhiteTex != null) Destroy(reverseCamWhiteTex);
    }

    // ── Geri Görüş Kamerası ──────────────────────────────────────────────────

    void SetupReverseCameraHUD()
    {
        if (!enableReverseCamera) return;

        var go = new GameObject("_ReverseCameraHUD");
        reverseCamHUD = go.AddComponent<Camera>();
        reverseCamHUD.fieldOfView = reverseCamFov;
        reverseCamHUD.nearClipPlane = 0.15f;
        reverseCamHUD.farClipPlane = 300f;
        reverseCamHUD.depth = 2f;   // Main cam (0) üzerinde, minimap (10) altında
        reverseCamHUD.rect = new Rect(reverseCamVpX, reverseCamVpY, reverseCamVpW, reverseCamVpH);
        reverseCamHUD.cullingMask = mainCamera != null ? mainCamera.cullingMask : ~0;
        reverseCamHUD.backgroundColor = mainCamera != null ? mainCamera.backgroundColor : Color.black;
        reverseCamHUD.clearFlags = ResolveSecondaryCameraClearFlags();
        reverseCamHUD.allowHDR = mainCamera != null && mainCamera.allowHDR;
        reverseCamHUD.allowMSAA = mainCamera != null && mainCamera.allowMSAA;

        Skybox mainSkybox = mainCamera != null ? mainCamera.GetComponent<Skybox>() : null;
        Material skyboxMaterial = mainSkybox != null && mainSkybox.material != null
            ? mainSkybox.material
            : RenderSettings.skybox;
        if (skyboxMaterial != null)
        {
            Skybox reverseSkybox = go.AddComponent<Skybox>();
            reverseSkybox.material = skyboxMaterial;
        }

        reverseCamHUD.enabled = false;
    }

    CameraClearFlags ResolveSecondaryCameraClearFlags()
    {
        if (mainCamera != null)
        {
            return mainCamera.clearFlags;
        }

        return RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
    }

    void UpdateReverseCameraHUD()
    {
        if (!enableReverseCamera || reverseCamHUD == null) return;

        bool shouldShow = IsCarReversing();

        // Smooth fade
        float targetAlpha = shouldShow ? 1f : 0f;
        reverseCamFadeAlpha = Mathf.MoveTowards(reverseCamFadeAlpha, targetAlpha,
            reverseCamFadeSpeed * Time.deltaTime);

        bool camActive = reverseCamFadeAlpha > 0.01f;
        if (camActive != reverseCamShowing)
        {
            reverseCamShowing = camActive;
            reverseCamHUD.enabled = reverseCamShowing;
        }

        if (!reverseCamShowing) return;

        Transform reverseAnchor = vehicleCameraAnchors != null ? vehicleCameraAnchors.ReverseCameraAnchor : null;
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        reverseCamHUD.transform.position = reverseAnchor != null
            ? reverseAnchor.position
            : target.position + yaw * reverseCamOffset;
        reverseCamHUD.transform.rotation = yaw * Quaternion.Euler(reverseCamEuler);
    }

    bool IsCarReversing()
    {
        if (targetRb == null)
            return carController != null && carController.IsReverseInputActive;

        float localZ = target.InverseTransformDirection(targetRb.linearVelocity).z;

        // Araç gerçekten geri gidiyorsa
        if (localZ < -0.1f) return true;

        // Araç durağan haldeyken geri tuşuna basılıyorsa
        if (targetRb.linearVelocity.magnitude < reverseCamStationaryThreshold
            && carController != null
            && carController.IsReverseInputActive)
        {
            return true;
        }

        return false;
    }
}
