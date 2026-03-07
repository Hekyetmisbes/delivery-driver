using UnityEngine;
using System.Collections.Generic;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Takip edilecek arac")]
    public Transform target;

    [Header("Offset Settings")]
    [Tooltip("Kameranin araca gore konumu (X, Y, Z). Z negatif olmali ki arkada dursun.")]
    [SerializeField] private Vector3 offset = new Vector3(0, 4f, -8f);

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
    private Camera reverseCamHUD;
    private bool reverseCamShowing;
    private float reverseCamFadeAlpha;
    private Texture2D reverseCamWhiteTex;
    private GUIStyle reverseCamLabelStyle;
    private float currentZoomOffset;

    void Start()
    {
        // Eger target atanmamissa otomatik bulmaya calis
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                // CarController olan objeyi bul
                CarController car = FindFirstObjectByType<CarController>();
                if (car != null)
                {
                    target = car.transform;
                }
            }
        }

        if (target != null)
        {
            mainCamera = GetComponent<Camera>();
            targetRb = target.GetComponent<Rigidbody>();
            // Rigidbody target'in tam uzerinde yoksa ust/alt hiyerarside ara
            if (targetRb == null) targetRb = target.GetComponentInParent<Rigidbody>();
            if (targetRb == null) targetRb = target.GetComponentInChildren<Rigidbody>();
            // CarController referansini al (geri tus inputunu okumak icin)
            carController = target.GetComponent<CarController>();
            if (carController == null) carController = target.GetComponentInParent<CarController>();
            if (carController == null) carController = target.GetComponentInChildren<CarController>();

            Debug.Log($"[CameraFollow] Start: target={target.name}, targetRb={targetRb != null}, carController={carController != null}");

            limitMiniMapToBounds = true;
            ResolveMiniMapBounds();
            ConfigureMiniMapLayerFilter();
            SetupMiniMapCamera();
            SetupReverseCameraHUD();
        }
        else
        {
            Debug.LogWarning("CameraFollow: Target (Arac) bulunamadi!");
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        HandleCameraMovement(Time.deltaTime);
        UpdateSpeedFov(Time.deltaTime);
        UpdateMiniMapCamera();
        UpdateReverseCameraHUD();
    }

    void UpdateSpeedFov(float deltaTime)
    {
        if (!enableSpeedFov || mainCamera == null || targetRb == null)
        {
            return;
        }

        float speedKmh = targetRb.linearVelocity.magnitude * 3.6f;
        float t = fovMaxSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / fovMaxSpeedKmh) : 0f;
        float desiredFov = Mathf.Lerp(baseFov, maxSpeedFov, t);
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, desiredFov, Mathf.Max(0.01f, fovLerpSpeed) * deltaTime);
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
        miniMapCamera.cullingMask = miniMapCullingMask;
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
            SetupMiniMapCamera();
            if (miniMapCamera == null)
            {
                return;
            }
        }

        miniMapCamera.gameObject.SetActive(true);
        miniMapCamera.rect = BuildMiniMapRect();
        miniMapCamera.orthographicSize = miniMapOrthoSize;
        miniMapCamera.cullingMask = miniMapCullingMask;

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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader != null)
        {
            miniMapPlayerMarkerMaterial = new Material(shader);
            MeshRenderer renderer = miniMapPlayerMarker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = miniMapPlayerMarkerMaterial;
            }
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
        reverseCamHUD.enabled = false;
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

        // Yalnızca yaw (Y ekseni) — pitch/roll titremesini önler
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        reverseCamHUD.transform.position = target.position + yaw * reverseCamOffset;
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
