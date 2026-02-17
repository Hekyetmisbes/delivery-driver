using UnityEngine;

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

    [Header("MiniMap Settings")]
    [Tooltip("Sol altta minimap goster")]
    [SerializeField] private bool enableMiniMap = true;
    [Tooltip("Minimap boyutu (ekran oranina gore, 0-1)")]
    [SerializeField] private float miniMapViewportSize = 0.22f;
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

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private bool isReversing = false;
    private float currentLocalZVelocity = 0f;
    private Camera miniMapCamera;
    private Vector3 miniMapVelocity;

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
            targetRb = target.GetComponent<Rigidbody>();
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
        UpdateMiniMapCamera();
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
        miniMapCamera.transform.position = Vector3.SmoothDamp(
            miniMapCamera.transform.position,
            mapPosition,
            ref miniMapVelocity,
            miniMapFollowSmoothTime,
            Mathf.Infinity,
            Mathf.Max(0.0001f, Time.deltaTime));

        float yRotation = miniMapRotateWithTarget ? target.eulerAngles.y : 0f;
        miniMapCamera.transform.rotation = Quaternion.Euler(90f, yRotation, 0f);
    }

    Rect BuildMiniMapRect()
    {
        float size = Mathf.Clamp(miniMapViewportSize, 0.12f, 0.45f);
        float x = Mathf.Clamp01(miniMapViewportMargin.x);
        float y = Mathf.Clamp01(miniMapViewportMargin.y);
        return new Rect(x, y, size, size);
    }
}
