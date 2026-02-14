using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("--- INPUT SYSTEM ---")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("--- WHEEL COLLIDERS (Physics) ---")]
    [Tooltip("Ön Sol Tekerlek Collider'ı")]
    public WheelCollider frontLeftCollider;
    [Tooltip("Ön Sağ Tekerlek Collider'ı")]
    public WheelCollider frontRightCollider;
    [Tooltip("Arka Sol Tekerlek Collider'ı (Çeker)")]
    public WheelCollider rearLeftCollider;
    [Tooltip("Arka Sağ Tekerlek Collider'ı (Çeker)")]
    public WheelCollider rearRightCollider;

    [Header("--- WHEEL MESHES (Visuals) ---")]
    [Tooltip("Ön Sol Tekerlek Görseli")]
    public Transform frontLeftMesh;
    [Tooltip("Ön Sağ Tekerlek Görseli")]
    public Transform frontRightMesh;
    [Tooltip("Arka Sol Tekerlek Görseli")]
    public Transform rearLeftMesh;
    [Tooltip("Arka Sağ Tekerlek Görseli")]
    public Transform rearRightMesh;

    [Header("--- CAR SETTINGS ---")]
    [Tooltip("Motorun maksimum tork gücü (Newton-metre). Araç gitmiyorsa bunu artır.")]
    [SerializeField] private float motorTorque = 1500f;
    [Tooltip("Fren yapıldığında uygulanan tork gücü.")]
    [SerializeField] private float brakeTorque = 3000f;
    [Tooltip("Maksimum direksiyon açısı.")]
    [SerializeField] private float maxSteeringAngle = 30f;
    [Tooltip("El freni çekildiğinde arka tekerlere uygulanan sürtünme.")]
    [SerializeField] private float handbrakeFrictionMultiplier = 2f;
    [Tooltip("Aracın ağırlık merkezi dengesi (Yere yakın olmalı).")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0.3f);
    [Tooltip("Kargo yuklendiginde agirlik merkezine uygulanacak ek yukseklik.")]
    [SerializeField] private float cargoCenterOfMassYOffset = 0.2f;
    
    [Header("--- STABILITY ---")]
    [Tooltip("Araci yere bastirma kuvveti katsayisi (N per m/s).")]
    [SerializeField] private float downforcePerSpeed = 35f;
    [Tooltip("Downforce maksimumu: aracin agirliginin bu oranini gecmez.")]
    [SerializeField] private float maxDownforceWeightFraction = 0.3f;
    [Tooltip("Baslangicta wheel collider yuksekliklerini ayni temas duzlemine hizalar.")]
    [SerializeField] private bool autoNormalizeWheelColliderHeights = true;
    [Tooltip("Hiz arttikca direksiyon etkinligini azaltir.")]
    [SerializeField] private bool speedSensitiveSteering = true;
    [Tooltip("Bu hizda (km/s) direksiyon azaltmasi maksimuma ulasir.")]
    [SerializeField] private float steeringReductionFullSpeedKmh = 120f;
    [Tooltip("Maksimum hizda direksiyon acisinin korunacak orani.")]
    [Range(0.15f, 1f)]
    [SerializeField] private float steeringAtHighSpeedFactor = 0.45f;
    [Tooltip("On aks anti-roll sertligi.")]
    [SerializeField] private float frontAntiRollStiffness = 6000f;
    [Tooltip("Arka aks anti-roll sertligi.")]
    [SerializeField] private float rearAntiRollStiffness = 6500f;
    [Tooltip("Rigidbody'de surekli carpisma tespiti kullanir (zemine girme/firlamayi azaltir).")]
    [SerializeField] private bool useContinuousCollisionDetection = true;
    [Tooltip("Rigidbody interpolasyonunu acarak daha stabil temas saglar.")]
    [SerializeField] private bool useInterpolation = true;
    [Tooltip("Arac govdesindeki MeshCollider'lari devre disi birakip tek BoxCollider kullanir.")]
    [SerializeField] private bool useSimpleBodyCollider = true;
    [SerializeField] private Vector3 simpleBodyColliderCenter = new Vector3(0f, 0.75f, 0.05f);
    [SerializeField] private Vector3 simpleBodyColliderSize = new Vector3(1.85f, 1.35f, 4.35f);

    [Header("--- INPUT SMOOTHING ---")]
    [Tooltip("Gaz/fren girdisinin ne kadar hizli artacagi (birim/s).")]
    [SerializeField] private float throttleRiseRate = 4f;
    [Tooltip("Gaz/fren girdisinin ne kadar hizli azalacagi (birim/s).")]
    [SerializeField] private float throttleFallRate = 6f;
    [Tooltip("Direksiyon girdisinin ne kadar hizli degisecegi (birim/s).")]
    [SerializeField] private float steeringInputRate = 5f;
    
    [Header("--- DEBUG ---")]
    [SerializeField] private bool showDebugGUI = true;

    // Private Runtime Variables
    private Rigidbody rb;
    private float currentSteerAngle;
    private float currentBrakeTorque;
    private bool isBraking;
    private bool isHandbraking;
    private Vector2 moveInput;
    private float baseRigidbodyMass;
    private float currentCargoWeight;
    private float smoothedThrottleInput;
    private float smoothedSteerInput;
    
    // Input Action References
    private InputAction moveAction;
    private InputAction handbrakeAction;

    private void Awake()
    {
        if (autoNormalizeWheelColliderHeights)
        {
            NormalizeWheelColliderHeightsRuntime();
        }

        rb = GetComponent<Rigidbody>();
        SetupRigidbody();
        ConfigureBodyCollidersForStability();
        SetupInput();
    }

    private void SetupRigidbody()
    {
        // BUG FIX: CenterOfMass yanlışsa araba takla atar veya tekerler boşa döner.
        rb.centerOfMass = centerOfMassOffset;
        rb.mass = 1500f; // Standart bir araba ağırlığı
        baseRigidbodyMass = rb.mass;
        rb.linearDamping = 0.05f; // Hava direnci (çok yüksek olursa araç gitmez)
        rb.angularDamping = 0.5f; 
        // BUG FIX: Uyuyan fizik motoru sorunu için
        rb.sleepThreshold = 0.0f; 
        if (useContinuousCollisionDetection)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        if (useInterpolation)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        rb.maxAngularVelocity = 30f;
    }

    public void AddCargoWeight(float weight)
    {
        currentCargoWeight = Mathf.Max(0f, weight);
        rb.mass = baseRigidbodyMass + currentCargoWeight;
        rb.centerOfMass = centerOfMassOffset + Vector3.up * cargoCenterOfMassYOffset;
    }

    public void RemoveCargoWeight()
    {
        currentCargoWeight = 0f;
        rb.mass = baseRigidbodyMass;
        rb.centerOfMass = centerOfMassOffset;
    }

    private void SetupInput()
    {
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            moveAction = playerMap.FindAction("Move");
            handbrakeAction = playerMap.FindAction("Jump"); // Space tuşu genellikle Jump'tır
            
            moveAction.Enable();
            handbrakeAction.Enable();
        }
        else
        {
            Debug.LogError("CarController: Input Actions atanmamış!");
        }
    }

    private void Update()
    {
        HandleInput();
    }

    private void FixedUpdate()
    {
        UpdateSmoothedInputs(Time.fixedDeltaTime);
        HandleMotor();
        HandleSteering();
        ApplyAntiRollBars();
        UpdateWheels();
        ApplyDownforce(); // Yuksek hizda yol tutusu icin
    }

    private void UpdateSmoothedInputs(float deltaTime)
    {
        float targetThrottle = Mathf.Clamp(moveInput.y, -1f, 1f);
        float targetSteer = Mathf.Clamp(moveInput.x, -1f, 1f);

        float throttleRate = Mathf.Abs(targetThrottle) > Mathf.Abs(smoothedThrottleInput)
            ? throttleRiseRate
            : throttleFallRate;

        smoothedThrottleInput = Mathf.MoveTowards(smoothedThrottleInput, targetThrottle, throttleRate * deltaTime);
        smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, targetSteer, steeringInputRate * deltaTime);
    }

    private void HandleInput()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
            isHandbraking = handbrakeAction.IsPressed();
        }
    }

    private void HandleMotor()
    {
        float accelInput = smoothedThrottleInput;
        
        // BUG FIX: "Teker dönüyor ama gitmiyor" sorununun ana kaynaklarından biri:
        // Fren torku 0 değilse motor torku çalışmaz. Gaz veriyorsak freni kesinlikle 0 yapmalıyız.
        
        bool isMovingForward = Vector3.Dot(transform.forward, rb.linearVelocity) > 0.5f;
        bool isMovingReverse = Vector3.Dot(transform.forward, rb.linearVelocity) < -0.5f;

        // Yön değiştirme mantığı (İleri giderken geriye basılırsa fren yap)
        if (accelInput > 0 && isMovingReverse)
        {
            isBraking = true;
        }
        else if (accelInput < 0 && isMovingForward)
        {
            isBraking = true;
        }
        else
        {
            isBraking = false;
        }

        // --- MOTOR GÜCÜ (Sadece Arka Tekerler - RWD) ---
        if (!isBraking && !isHandbraking)
        {
            rearLeftCollider.motorTorque = accelInput * motorTorque;
            rearRightCollider.motorTorque = accelInput * motorTorque;
            currentBrakeTorque = 0f;
        }
        else
        {
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
        }

        // --- FRENLEME ---
        if (isHandbraking)
        {
            currentBrakeTorque = brakeTorque * handbrakeFrictionMultiplier; // El freni daha sert
            // Ön tekerler el freninde kilitlenmez
            frontLeftCollider.brakeTorque = 0f;
            frontRightCollider.brakeTorque = 0f;
            // Arka tekerler kilitlenir
            rearLeftCollider.brakeTorque = currentBrakeTorque;
            rearRightCollider.brakeTorque = currentBrakeTorque;
        }
        else if (isBraking || Mathf.Abs(accelInput) < 0.05f) // Gaz verilmiyorsa da hafif fren (drag)
        {
            currentBrakeTorque = isBraking ? brakeTorque : 100f; // Drag braking
            
            frontLeftCollider.brakeTorque = currentBrakeTorque;
            frontRightCollider.brakeTorque = currentBrakeTorque;
            rearLeftCollider.brakeTorque = currentBrakeTorque;
            rearRightCollider.brakeTorque = currentBrakeTorque;
        }
        else
        {
            // Hareket halindeyiz
            frontLeftCollider.brakeTorque = 0f;
            frontRightCollider.brakeTorque = 0f;
            rearLeftCollider.brakeTorque = 0f;
            rearRightCollider.brakeTorque = 0f;
        }
    }

    private void HandleSteering()
    {
        float steerInput = smoothedSteerInput;

        float steerFactor = 1f;
        if (speedSensitiveSteering)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float t = steeringReductionFullSpeedKmh > 0.1f
                ? Mathf.Clamp01(speedKmh / steeringReductionFullSpeedKmh)
                : 1f;
            steerFactor = Mathf.Lerp(1f, steeringAtHighSpeedFactor, t);
        }

        currentSteerAngle = maxSteeringAngle * steerInput * steerFactor;

        // Sadece ön tekerler döner
        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void ApplyAntiRollBars()
    {
        ApplyAntiRollOnAxle(frontLeftCollider, frontRightCollider, frontAntiRollStiffness);
        ApplyAntiRollOnAxle(rearLeftCollider, rearRightCollider, rearAntiRollStiffness);
    }

    private void ApplyAntiRollOnAxle(WheelCollider left, WheelCollider right, float stiffness)
    {
        if (left == null || right == null || stiffness <= 0f)
        {
            return;
        }

        float leftTravel = 1f;
        float rightTravel = 1f;
        bool leftGrounded = false;
        bool rightGrounded = false;
        float leftSuspension = Mathf.Max(0.001f, left.suspensionDistance);
        float rightSuspension = Mathf.Max(0.001f, right.suspensionDistance);

        WheelHit hit;
        if (left.GetGroundHit(out hit))
        {
            leftGrounded = true;
            leftTravel = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / leftSuspension;
            leftTravel = Mathf.Clamp01(leftTravel);
        }

        if (right.GetGroundHit(out hit))
        {
            rightGrounded = true;
            rightTravel = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / rightSuspension;
            rightTravel = Mathf.Clamp01(rightTravel);
        }

        float antiRollForce = (leftTravel - rightTravel) * stiffness;

        if (leftGrounded)
        {
            rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position, ForceMode.Force);
        }

        if (rightGrounded)
        {
            rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position, ForceMode.Force);
        }
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelCollider == null || wheelTransform == null) return;

        // WheelCollider'ın fiziksel pozisyonunu al
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);

        // Görsel mesh'i bu pozisyona eşitle
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    private void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;
        float requested = Mathf.Max(0f, downforcePerSpeed) * speed;
        float weight = rb.mass * Mathf.Abs(Physics.gravity.y);
        float maxAllowed = Mathf.Max(0f, maxDownforceWeightFraction) * weight;

        if (maxAllowed > 0f)
        {
            requested = Mathf.Min(requested, maxAllowed);
        }

        rb.AddForce(-transform.up * requested, ForceMode.Force);
    }

    private void ConfigureBodyCollidersForStability()
    {
        if (!useSimpleBodyCollider)
        {
            return;
        }

        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < meshColliders.Length; i++)
        {
            if (meshColliders[i] != null && !meshColliders[i].isTrigger)
            {
                meshColliders[i].enabled = false;
            }
        }

        BoxCollider bodyBox = GetComponent<BoxCollider>();
        if (bodyBox == null)
        {
            bodyBox = gameObject.AddComponent<BoxCollider>();
        }

        bodyBox.center = simpleBodyColliderCenter;
        bodyBox.size = simpleBodyColliderSize;
        bodyBox.isTrigger = false;
        bodyBox.enabled = true;
    }

    private void NormalizeWheelColliderHeightsRuntime()
    {
        if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
        {
            return;
        }

        WheelCollider[] wheels = { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
        float minContactY = float.MaxValue;

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            if (wheel == null)
            {
                return;
            }

            Vector3 localPos = transform.InverseTransformPoint(wheel.transform.position);
            float contactY = localPos.y - wheel.radius;
            if (contactY < minContactY)
            {
                minContactY = contactY;
            }
        }

        if (Mathf.Abs(minContactY) < 0.001f)
        {
            return;
        }

        for (int i = 0; i < wheels.Length; i++)
        {
            Transform wt = wheels[i].transform;
            Vector3 localPos = wt.localPosition;
            localPos.y -= minContactY;
            wt.localPosition = localPos;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!showDebugGUI) return;

        GUI.color = Color.green;
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"<b>CAR DEBUG SYSTEM</b>");
        GUILayout.Label($"Speed: {(rb.linearVelocity.magnitude * 3.6f):F0} km/h");
        GUILayout.Label($"Motor Torque: {rearLeftCollider.motorTorque:F0} / {motorTorque}");
        GUILayout.Label($"Brake Torque: {rearLeftCollider.brakeTorque:F0}");
        GUILayout.Label($"Handbrake: {isHandbraking}");
        GUILayout.Label($"Is Grounded (RL): {rearLeftCollider.isGrounded}");
        GUILayout.EndArea();
    }
#endif

    private void OnDrawGizmos()
    {
        // Ağırlık merkezini göster
        if (rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(rb.centerOfMass), 0.25f);
            Gizmos.DrawLine(transform.TransformPoint(rb.centerOfMass), transform.TransformPoint(rb.centerOfMass) + transform.forward);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        DeliveryDriver.Quest.QuestManager.Instance?.OnVehicleCollision(collision);
    }

}

