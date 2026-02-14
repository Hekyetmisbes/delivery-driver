using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Takip edilecek araç")]
    public Transform target;

    [Header("Offset Settings")]
    [Tooltip("Kameranın araca göre konumu (X, Y, Z). Z negatif olmalı ki arkada dursun.")]
    [SerializeField] private Vector3 offset = new Vector3(0, 4f, -8f);

    [Header("Smooth Settings")]
    [Tooltip("Kamera takip yumuşaklığı (Düşük = daha sıkı, Yüksek = daha gevşek)")]
    [SerializeField] private float translateSmoothTime = 0.2f;
    [Tooltip("Dönüş yumuşaklığı (Düşük = daha gecikmeli, Yüksek = daha hızlı)")]
    [SerializeField] private float rotationSmoothSpeed = 1.5f;
    [Tooltip("Kamera rotasyonu tamamen arabayı takip etsin mi?")]
    [SerializeField] private bool followRotation = false;

    [Header("Reverse Settings")]
    [Tooltip("Geri gidildiğinde kameranın öne geçme özelliği")]
    [SerializeField] private bool enableReverseView = true;
    [Tooltip("Hangi hızdan sonra geri görüşe geçsin (Negatif değer)")]
    [SerializeField] private float reverseSpeedThreshold = -1f;

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private bool isReversing = false;
    private float currentLocalZVelocity = 0f;

    void Start()
    {
        // Eğer target atanmamışsa otomatik bulmaya çalış
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
                if (car != null) target = car.transform;
            }
        }

        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogWarning("CameraFollow: Target (Araç) bulunamadı!");
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleCameraMovement(Time.deltaTime);
    }

    void HandleCameraMovement(float deltaTime)
    {
        // 1. Aracın yerel hızına bak (İleri mi gidiyor geri mi?)
        float localZVelocity = 0f;
        if (targetRb != null)
        {
            // Dünya koordinatındaki hızı, aracın yerel koordinatına çevir
            // Not: Yeni Unity versiyonlarında linearVelocity, eski versiyonlarda velocity
            Vector3 rbVelocity = targetRb.linearVelocity;
            localZVelocity = target.InverseTransformDirection(rbVelocity).z;
            currentLocalZVelocity = localZVelocity; // Debug için sakla
        }

        // 2. Geri gitme durumunu kontrol et
        if (enableReverseView)
        {
            // Eğer belirli bir hızın üzerinde geri gidiyorsa mod değiştir
            if (localZVelocity < reverseSpeedThreshold)
            {
                isReversing = true;
            }
            // İleri gidiyorsa veya duruyorsa normal moda dön
            else if (localZVelocity > -0.5f)
            {
                isReversing = false;
            }
        }

        // 3. Hedef pozisyonu belirle
        Vector3 targetOffset = offset;

        if (isReversing)
        {
            // Geri giderken Z offsetini tersine çevir (Arabanın önüne geç)
            targetOffset = new Vector3(offset.x, offset.y, -offset.z);
        }

        // 4. Pozisyon hesaplama
        Vector3 desiredPosition;

        if (followRotation)
        {
            // Kamera arabayı dönerek takip eder (eski davranış)
            desiredPosition = target.TransformPoint(targetOffset);
        }
        else
        {
            // Kamera düz kalır, sadece araba hareket edince hareket eder
            // Dünya koordinatlarında sabit yön kullan
            desiredPosition = target.position + new Vector3(targetOffset.x, targetOffset.y, targetOffset.z);
        }

        // 5. Pozisyonu yumuşatarak uygula
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            translateSmoothTime,
            Mathf.Infinity,
            Mathf.Max(0.0001f, deltaTime));

        // 6. Rotasyon ayarı
        if (followRotation)
        {
            // Kameranın arabaya bakmasını sağla (gecikmeli)
            Vector3 lookAtTarget = target.position + Vector3.up * 1.5f;
            Vector3 direction = lookAtTarget - transform.position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * deltaTime);
            }
        }
        else
        {
            // Kamera düz kalır, sadece arabaya bakar (yavaşça)
            Vector3 lookAtTarget = target.position + Vector3.up * 1.5f;
            Vector3 direction = lookAtTarget - transform.position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * deltaTime);
            }
        }
    }

}
