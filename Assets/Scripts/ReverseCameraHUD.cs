using UnityEngine;

/// <summary>
/// Araba geri giderken (veya geri tusuna basilinca) ekranin ustunde
/// geri goruntusu gosteren HUD kamerasi.
/// Herhangi bir sahne objesine (ornegin Main Camera) component olarak ekle.
/// </summary>
public class ReverseCameraHUD : MonoBehaviour
{
    [Header("Hedef Arac")]
    [Tooltip("Takip edilecek arac. Bos birakilirsa CarController olan objeyi bulur.")]
    public Transform carTarget;

    [Header("Kamera Pozisyonu (Arac Uzerinde Lokal)")]
    [Tooltip("Arabanin arkasindaki kamera konumu (lokal). Z negatif = aracin arkasi.")]
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 1.1f, -2.0f);
    [Tooltip("Kamera acisi (lokal). Y=180 geriye bakar, X pozitif = asagi egimli.")]
    [SerializeField] private Vector3 cameraLocalEuler = new Vector3(12f, 180f, 0f);
    [SerializeField] private float fieldOfView = 95f;

    [Header("Ekran Konumu (Viewport 0-1)")]
    [Tooltip("Sol kenar (0 = sol, 0.5 = orta)")]
    [SerializeField] private float vpX = 0.2f;
    [Tooltip("Alt kenar (0 = alt, 1 = ust). 0.74 ile 0.24 yukseklik -> usttte gozukur.")]
    [SerializeField] private float vpY = 0.74f;
    [Tooltip("Genislik")]
    [SerializeField] private float vpWidth = 0.60f;
    [Tooltip("Yukseklik")]
    [SerializeField] private float vpHeight = 0.24f;

    [Header("Tetikleyici")]
    [Tooltip("Geri gitme hizi esigi (m/s). Bu degerden daha hizli geri gidince gozukur.")]
    [SerializeField] private float reverseVelocityThreshold = -0.1f;

    // Runtime
    private Camera reverseCam;
    private CarController carController;
    private Rigidbody carRb;
    private bool isShowing;

    void Start()
    {
        Debug.Log($"[ReverseHUD] Start() calistirildi. gameObject={gameObject.name}, enabled={enabled}");

        if (carTarget == null)
        {
            var cc = FindFirstObjectByType<CarController>();
            if (cc != null)
            {
                carTarget = cc.transform;
                Debug.Log($"[ReverseHUD] carTarget otomatik bulundu: {carTarget.name}");
            }
            else
            {
                Debug.LogError("[ReverseHUD] Sahnede CarController bulunamadi!");
            }
        }
        else
        {
            Debug.Log($"[ReverseHUD] carTarget Inspector'dan atanmis: {carTarget.name}");
        }

        if (carTarget == null)
        {
            Debug.LogError("ReverseCameraHUD: Arac bulunamadi, devre disi birakiliyor.");
            enabled = false;
            return;
        }

        carController = carTarget.GetComponent<CarController>()
                     ?? carTarget.GetComponentInParent<CarController>()
                     ?? carTarget.GetComponentInChildren<CarController>();

        carRb = carTarget.GetComponent<Rigidbody>()
             ?? carTarget.GetComponentInParent<Rigidbody>();

        Debug.Log($"[ReverseHUD] carController={carController != null}, carRb={carRb != null}");

        CreateCamera();
        Debug.Log($"[ReverseHUD] reverseCam olusturuldu: {reverseCam != null}, rect=({vpX},{vpY},{vpWidth},{vpHeight})");
    }

    void CreateCamera()
    {
        var go = new GameObject("_ReverseCameraHUD");
        reverseCam = go.AddComponent<Camera>();
        reverseCam.fieldOfView = fieldOfView;
        reverseCam.nearClipPlane = 0.15f;
        reverseCam.farClipPlane = 300f;
        reverseCam.depth = 2f;          // Main cam (0) uzerinde, minimap (10) altinda
        reverseCam.rect = new Rect(vpX, vpY, vpWidth, vpHeight);
        reverseCam.enabled = false;
    }

    private float debugLogTimer = 0f;
    private const float DEBUG_LOG_INTERVAL = 2f;

    void LateUpdate()
    {
        if (carTarget == null || reverseCam == null)
        {
            Debug.LogError($"[ReverseHUD] LateUpdate: carTarget={carTarget != null}, reverseCam={reverseCam != null} — ERKEN CIKIS");
            return;
        }

        bool shouldShow = IsReversing();

        if (shouldShow != isShowing)
        {
            isShowing = shouldShow;
            reverseCam.enabled = isShowing;
            Debug.Log($"[ReverseHUD] HUD durumu degisti -> isShowing={isShowing}, reverseCam.enabled={reverseCam.enabled}");
        }

        // Her 2 saniyede bir durum raporla
        debugLogTimer += Time.deltaTime;
        if (debugLogTimer >= DEBUG_LOG_INTERVAL)
        {
            debugLogTimer = 0f;
            bool inputActive = carController != null && carController.IsReverseInputActive;
            float localZ = carRb != null ? carTarget.InverseTransformDirection(carRb.linearVelocity).z : 0f;
            Debug.Log($"[ReverseHUD] Durum: isShowing={isShowing} | reverseInput={inputActive} | localZ={localZ:F2} (esik={reverseVelocityThreshold}) | camEnabled={reverseCam.enabled}");
        }

        if (isShowing)
        {
            PositionCamera();
        }
    }

    bool IsReversing()
    {
        // Geri tusuna basiliyorsa
        if (carController != null && carController.IsReverseInputActive) return true;

        // Veya araç gerçekten geri gidiyorsa
        if (carRb != null)
        {
            float localZ = carTarget.InverseTransformDirection(carRb.linearVelocity).z;
            if (localZ < reverseVelocityThreshold) return true;
        }

        return false;
    }

    void PositionCamera()
    {
        // Sadece yaw (Y ekseni) kullan - pitch/roll'da titreme olmaz
        Quaternion yaw = Quaternion.Euler(0f, carTarget.eulerAngles.y, 0f);
        reverseCam.transform.position = carTarget.position + yaw * cameraLocalOffset;
        reverseCam.transform.rotation = yaw * Quaternion.Euler(cameraLocalEuler);
    }

    void OnDestroy()
    {
        if (reverseCam != null) Destroy(reverseCam.gameObject);
    }
}
