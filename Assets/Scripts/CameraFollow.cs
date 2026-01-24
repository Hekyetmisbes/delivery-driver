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
    [Tooltip("Dönüş yumuşaklığı")]
    [SerializeField] private float rotationSmoothSpeed = 5f;

    [Header("Reverse Settings")]
    [Tooltip("Geri gidildiğinde kameranın öne geçme özelliği")]
    [SerializeField] private bool enableReverseView = true;
    [Tooltip("Hangi hızdan sonra geri görüşe geçsin (Negatif değer)")]
    [SerializeField] private float reverseSpeedThreshold = -2f;

    // Runtime variables
    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private bool isReversing = false;

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
                CarController car = FindObjectOfType<CarController>();
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

    void FixedUpdate()
    {
        if (target == null) return;

        HandleCameraMovement();
    }

    void HandleCameraMovement()
    {
        // 1. Aracın yerel hızına bak (İleri mi gidiyor geri mi?)
        float localZVelocity = 0f;
        if (targetRb != null)
        {
            // Dünya koordinatındaki hızı, aracın yerel koordinatına çevir
            localZVelocity = target.InverseTransformDirection(targetRb.linearVelocity).z;
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
            // Ayrıca Y yüksekliğini biraz koru
            targetOffset = new Vector3(offset.x, offset.y, -offset.z);
        }

        // Hedef pozisyon: Arabanın rotasyonuna göre (TransformPoint) hesaplanır.
        // Bu sayede araba döndükçe kamera da arkasından döner.
        Vector3 desiredPosition = target.TransformPoint(targetOffset);

        // 4. Pozisyonu yumuşatarak uygula (SmoothDamp)
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, translateSmoothTime);

        // 5. Rotasyonu ayarla: Kameranın her zaman arabaya bakmasını sağla
        Vector3 lookAtTarget = target.position + Vector3.up * 1.5f; // Arabanın biraz üstüne bak
        Vector3 direction = lookAtTarget - transform.position;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}
