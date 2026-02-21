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
    public Transform carTarget;

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

    // Runtime
    private Camera reverseCam;
    private CarController carController;
    private Rigidbody carRb;
    private bool isShowing;

    void Start()
    {
        // CameraFollow zaten geri kamerayı yönetiyorsa bu script çalışmaz
        CameraFollow cf = FindFirstObjectByType<CameraFollow>();
        if (cf != null)
        {
            enabled = false;
            return;
        }

        if (carTarget == null)
        {
            var cc = FindFirstObjectByType<CarController>();
            if (cc != null) carTarget = cc.transform;
        }

        if (carTarget == null)
        {
            Debug.LogError("[ReverseCameraHUD] Sahnede CarController bulunamadı, devre dışı bırakılıyor.");
            enabled = false;
            return;
        }

        carController = carTarget.GetComponent<CarController>()
                     ?? carTarget.GetComponentInParent<CarController>()
                     ?? carTarget.GetComponentInChildren<CarController>();

        carRb = carTarget.GetComponent<Rigidbody>()
             ?? carTarget.GetComponentInParent<Rigidbody>();

        CreateCamera();
    }

    void CreateCamera()
    {
        var go = new GameObject("_ReverseCameraHUD");
        reverseCam = go.AddComponent<Camera>();
        reverseCam.fieldOfView = fieldOfView;
        reverseCam.nearClipPlane = 0.15f;
        reverseCam.farClipPlane = 300f;
        reverseCam.depth = 2f;
        reverseCam.rect = new Rect(vpX, vpY, vpWidth, vpHeight);
        reverseCam.enabled = false;
    }

    void LateUpdate()
    {
        if (carTarget == null || reverseCam == null) return;

        bool shouldShow = IsReversing();
        if (shouldShow != isShowing)
        {
            isShowing = shouldShow;
            reverseCam.enabled = isShowing;
        }

        if (isShowing) PositionCamera();
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
        Quaternion yaw = Quaternion.Euler(0f, carTarget.eulerAngles.y, 0f);
        reverseCam.transform.position = carTarget.position + yaw * cameraLocalOffset;
        reverseCam.transform.rotation = yaw * Quaternion.Euler(cameraLocalEuler);
    }

    void OnDestroy()
    {
        if (reverseCam != null) Destroy(reverseCam.gameObject);
    }
}
