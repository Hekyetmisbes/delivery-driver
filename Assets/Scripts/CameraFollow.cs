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
    [Tooltip("Donus yumusakligi (Dusuk = daha gecikmeli, Yuksek = daha hizli)")]
    [SerializeField] private float rotationSmoothSpeed = 1.5f;
    [Tooltip("Kamera rotasyonu tamamen arabayi takip etsin mi?")]
    [SerializeField] private bool followRotation = false;

    [Header("Reverse Settings")]
    [Tooltip("Geri gidildiginde kameranin one gecme ozelligi")]
    [SerializeField] private bool enableReverseView = true;
    [Tooltip("Hangi hizdan sonra geri goruse gecsin (Negatif deger)")]
    [SerializeField] private float reverseSpeedThreshold = -1f;

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
    [Header("MiniMap Marker")]
    [SerializeField] private bool showMiniMapPlayerMarker = true;
    [SerializeField] private float miniMapPlayerMarkerHeight = 20f;
    [SerializeField] private Vector3 miniMapPlayerMarkerScale = new Vector3(3.5f, 8f, 3.5f);
    [SerializeField] private float miniMapPlayerMarkerSpinSpeed = 130f;
    [SerializeField] private Color miniMapPlayerMarkerColor = new Color(0.15f, 1f, 0.35f, 1f);

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private Camera mainCamera;
    private bool isReversing = false;
    private float currentLocalZVelocity = 0f;
    private Camera miniMapCamera;
    private Vector3 miniMapVelocity;
    private bool hasMiniMapRuntimeBounds;
    private Bounds miniMapRuntimeBounds;
    private GameObject miniMapPlayerMarker;
    private Material miniMapPlayerMarkerMaterial;
    private int cachedMiniMapMarkerLayer = int.MinValue;

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
            limitMiniMapToBounds = true;
            ResolveMiniMapBounds();
            ConfigureMiniMapLayerFilter();
            SetupMiniMapCamera();
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
        // 1. Aracin yerel hizina bak (Ileri mi gidiyor geri mi?)
        float localZVelocity = 0f;
        if (targetRb != null)
        {
            // Dunya koordinatindaki hizi, aracin yerel koordinatina cevir
            // Not: Yeni Unity versiyonlarinda linearVelocity, eski versiyonlarda velocity
            Vector3 rbVelocity = targetRb.linearVelocity;
            localZVelocity = target.InverseTransformDirection(rbVelocity).z;
            currentLocalZVelocity = localZVelocity; // Debug icin sakla
        }

        // 2. Geri gitme durumunu kontrol et
        if (enableReverseView)
        {
            // Eger belirli bir hizin uzerinde geri gidiyorsa mod degistir
            if (localZVelocity < reverseSpeedThreshold)
            {
                isReversing = true;
            }
            // Ileri gidiyorsa veya duruyorsa normal moda don
            else if (localZVelocity > -0.5f)
            {
                isReversing = false;
            }
        }

        // 3. Hedef pozisyonu belirle
        Vector3 targetOffset = offset;

        if (isReversing)
        {
            // Geri giderken Z offsetini tersine cevir (Arabanin onune gec)
            targetOffset = new Vector3(offset.x, offset.y, -offset.z);
        }

        // 4. Pozisyon hesaplama
        Vector3 desiredPosition;

        if (followRotation)
        {
            // Kamera arabayi donerek takip eder (eski davranis)
            desiredPosition = target.TransformPoint(targetOffset);
        }
        else
        {
            // Kamera duz kalir, sadece araba hareket edince hareket eder
            // Dunya koordinatlarinda sabit yon kullan
            desiredPosition = target.position + new Vector3(targetOffset.x, targetOffset.y, targetOffset.z);
        }

        // 5. Pozisyonu yumusatarak uygula
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            translateSmoothTime,
            Mathf.Infinity,
            Mathf.Max(0.0001f, deltaTime));

        // 6. Rotasyon ayari
        Vector3 lookAtTarget = target.position + Vector3.up * 1.5f;
        Vector3 direction = lookAtTarget - transform.position;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * deltaTime);
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

    void OnDestroy()
    {
        RemoveMiniMapPlayerMarker();
    }
}
