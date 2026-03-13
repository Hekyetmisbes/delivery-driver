using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public event System.Action<float> OnHardBrakeDetected;

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
    [SerializeField] private float motorTorque = 2200f;
    [Tooltip("Fren yapıldığında uygulanan tork gücü.")]
    [SerializeField] private float brakeTorque = 3600f;
    [Tooltip("Maksimum direksiyon açısı.")]
    [SerializeField] private float maxSteeringAngle = 33f;
    [Tooltip("El freni çekildiğinde arka tekerlere uygulanan sürtünme.")]
    [SerializeField] private float handbrakeFrictionMultiplier = 2f;
    [SerializeField] private float maxForwardSpeedKmh = 210f;
    [Range(0.05f, 1f)]
    [SerializeField] private float topSpeedMinTorqueFactor = 0.18f;
    [SerializeField] private float coastBrakeTorque = 45f;
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
    [SerializeField] private float steeringReductionFullSpeedKmh = 170f;
    [Tooltip("Maksimum hizda direksiyon acisinin korunacak orani.")]
    [Range(0.15f, 1f)]
    [SerializeField] private float steeringAtHighSpeedFactor = 0.58f;
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
    [SerializeField] private float throttleRiseRate = 6.5f;
    [Tooltip("Gaz/fren girdisinin ne kadar hizli azalacagi (birim/s).")]
    [SerializeField] private float throttleFallRate = 8f;
    [Tooltip("Direksiyon girdisinin ne kadar hizli degisecegi (birim/s).")]
    [SerializeField] private float steeringInputRate = 7.5f;

    [Header("--- HANDBRAKE DRIFT ---")]
    [Tooltip("El freni aktifken arka teker yan tutusunun korunacak orani.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float handbrakeRearSidewaysGripFactor = 0.55f;
    [Tooltip("El freni aktifken direksiyon yonune verilen hafif donus destegi.")]
    [SerializeField] private float handbrakeDriftAssistTorque = 1100f;
    [Tooltip("Drift desteginin devreye girmesi icin minimum hiz (km/s).")]
    [SerializeField] private float handbrakeDriftMinSpeedKmh = 15f;
    [Tooltip("Drift puani icin gereken minimum yan kayma hizi (m/s).")]
    [SerializeField] private float driftScoreMinLateralSpeed = 2.2f;
    [Tooltip("Drift puanini saymak icin gereken minimum direksiyon girdisi.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float driftScoreMinSteerInput = 0.2f;
    [Tooltip("Saniye basina temel drift puani.")]
    [SerializeField] private float driftScorePerSecond = 14f;

    [Header("--- FEEDBACK ---")]
    [Tooltip("Sert fren bildirimi icin minimum hiz (km/s).")]
    [SerializeField] private float hardBrakeMinSpeedKmh = 45f;
    [Tooltip("Sert fren bildirimi icin minimum yavaslama (m/s^2).")]
    [SerializeField] private float hardBrakeMinDeceleration = 10f;
    [Tooltip("Sert fren bildirimi icin minimum anlik hiz dususu (m/s).")]
    [SerializeField] private float hardBrakeMinSpeedDropMetersPerSec = 1.6f;
    [Tooltip("Sert fren bildirimleri arasindaki min sure (s).")]
    [SerializeField] private float hardBrakeNotifyCooldown = 1.8f;
    
    // Public geri vites input durumu - CameraFollow tarafindan okunur
    public bool IsReverseInputActive => moveInput.y < -0.1f;

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
    private float previousSpeedMetersPerSec;
    private float lastHardBrakeNotifyTime = -999f;
    private float accumulatedDriftScore;
    private int committedDriftScore;
    private WheelFrictionCurve rearLeftSidewaysFrictionBase;
    private WheelFrictionCurve rearRightSidewaysFrictionBase;
    private bool rearFrictionCached;
    
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
        CacheRearSidewaysFriction();
        SetupInput();
    }

    private void SetupRigidbody()
    {
        // BUG FIX: CenterOfMass yanlışsa araba takla atar veya tekerler boşa döner.
        rb.centerOfMass = centerOfMassOffset;
        rb.mass = 1500f; // Standart bir araba ağırlığı
        baseRigidbodyMass = rb.mass;
        rb.linearDamping = 0.02f; // Hava direnci (çok yüksek olursa araç gitmez)
        rb.angularDamping = 0.4f; 
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
        UpdateHandbrakeDriftState();
        HandleMotor();
        HandleSteering();
        ApplyAntiRollBars();
        UpdateWheels();
        ApplyDownforce(); // Yuksek hizda yol tutusu icin
        EvaluateDriftFeedback(Time.fixedDeltaTime);
        EvaluateHardBrakeFeedback(Time.fixedDeltaTime);
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
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        isHandbraking = handbrakeAction != null && handbrakeAction.IsPressed();
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
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float topSpeedRatio = maxForwardSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / maxForwardSpeedKmh) : 0f;
            float speedTorqueFactor = Mathf.Lerp(1f, topSpeedMinTorqueFactor, topSpeedRatio);
            float appliedTorque = accelInput * motorTorque * speedTorqueFactor;

            if (accelInput > 0f && speedKmh >= maxForwardSpeedKmh)
            {
                appliedTorque = 0f;
            }

            rearLeftCollider.motorTorque = appliedTorque;
            rearRightCollider.motorTorque = appliedTorque;
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
            currentBrakeTorque = isBraking ? brakeTorque : coastBrakeTorque; // Drag braking
            
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

    private void CacheRearSidewaysFriction()
    {
        if (rearLeftCollider == null || rearRightCollider == null)
        {
            return;
        }

        rearLeftSidewaysFrictionBase = rearLeftCollider.sidewaysFriction;
        rearRightSidewaysFrictionBase = rearRightCollider.sidewaysFriction;
        rearFrictionCached = true;
    }

    private void UpdateHandbrakeDriftState()
    {
        if (!rearFrictionCached || rb == null)
        {
            return;
        }

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        bool driftActive = isHandbraking && speedKmh >= handbrakeDriftMinSpeedKmh;

        float gripFactor = driftActive ? handbrakeRearSidewaysGripFactor : 1f;
        gripFactor = Mathf.Clamp(gripFactor, 0.2f, 1f);
        ApplyRearSidewaysGrip(gripFactor);

        if (driftActive && Mathf.Abs(smoothedSteerInput) > 0.05f)
        {
            float steerSign = Mathf.Sign(smoothedSteerInput);
            float assistScale = Mathf.InverseLerp(handbrakeDriftMinSpeedKmh, handbrakeDriftMinSpeedKmh + 40f, speedKmh);
            float yawTorque = handbrakeDriftAssistTorque * steerSign * assistScale;
            rb.AddTorque(Vector3.up * yawTorque, ForceMode.Force);
        }
    }

    private void ApplyRearSidewaysGrip(float gripFactor)
    {
        WheelFrictionCurve left = rearLeftSidewaysFrictionBase;
        WheelFrictionCurve right = rearRightSidewaysFrictionBase;

        left.stiffness = rearLeftSidewaysFrictionBase.stiffness * gripFactor;
        right.stiffness = rearRightSidewaysFrictionBase.stiffness * gripFactor;

        rearLeftCollider.sidewaysFriction = left;
        rearRightCollider.sidewaysFriction = right;
    }

    private void EvaluateDriftFeedback(float deltaTime)
    {
        if (rb == null || deltaTime <= 0f)
        {
            return;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralSpeed = Mathf.Abs(localVelocity.x);
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float steerMagnitude = Mathf.Abs(smoothedSteerInput);

        bool driftScoringActive = isHandbraking
            && speedKmh >= handbrakeDriftMinSpeedKmh
            && lateralSpeed >= driftScoreMinLateralSpeed
            && steerMagnitude >= driftScoreMinSteerInput;

        if (!driftScoringActive)
        {
            return;
        }

        float lateralFactor = Mathf.InverseLerp(driftScoreMinLateralSpeed, driftScoreMinLateralSpeed + 7f, lateralSpeed);
        float speedFactor = Mathf.InverseLerp(handbrakeDriftMinSpeedKmh, handbrakeDriftMinSpeedKmh + 70f, speedKmh);
        float gain = Mathf.Max(0f, driftScorePerSecond) * (0.35f + (lateralFactor * 0.65f)) * (0.5f + (speedFactor * 0.5f));

        accumulatedDriftScore += gain * deltaTime;
        int driftDelta = Mathf.FloorToInt(accumulatedDriftScore) - committedDriftScore;
        if (driftDelta <= 0)
        {
            return;
        }

        committedDriftScore += driftDelta;
        DeliveryDriver.Quest.QuestManager.Instance?.OnDriftDetected(driftDelta);
    }

    private void EvaluateHardBrakeFeedback(float deltaTime)
    {
        if (rb == null || deltaTime <= 0f)
        {
            return;
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        float previousSpeed = previousSpeedMetersPerSec;
        previousSpeedMetersPerSec = currentSpeed;
        float speedDrop = previousSpeed - currentSpeed;

        if (!isBraking && !isHandbraking)
        {
            return;
        }

        float previousSpeedKmh = previousSpeed * 3.6f;
        if (previousSpeedKmh < hardBrakeMinSpeedKmh)
        {
            return;
        }

        if (speedDrop < hardBrakeMinSpeedDropMetersPerSec)
        {
            return;
        }

        float deceleration = speedDrop / deltaTime;
        if (deceleration < hardBrakeMinDeceleration)
        {
            return;
        }

        if (Time.time - lastHardBrakeNotifyTime < hardBrakeNotifyCooldown)
        {
            return;
        }

        lastHardBrakeNotifyTime = Time.time;
        OnHardBrakeDetected?.Invoke(deceleration);
        DeliveryDriver.Quest.QuestManager.Instance?.OnHardBrakeDetected(deceleration);
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

