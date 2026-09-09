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

    public enum DriveTrainType
    {
        AWD,
        FWD,
        RWD
    }

    [Header("--- DRIVETRAIN ---")]
    [Tooltip("Çekiş tipi: AWD (Dört teker - en dengeli ve cozy), FWD (Önden çekiş), RWD (Arkadan itiş).")]
    [SerializeField] private DriveTrainType driveTrain = DriveTrainType.AWD;
    [Tooltip("AWD modunda ön tekerleklere verilen tork oranı (0.55 = %55 ön, %45 arka).")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float frontDriveBias = 0.55f;

    [Header("--- CAR SETTINGS ---")]
    [Tooltip("Motorun maksimum tork gücü (Newton-metre). Araç gitmiyorsa bunu artır.")]
    [SerializeField] private float motorTorque = 2200f;
    [Tooltip("Fren yapıldığında uygulanan tork gücü.")]
    [SerializeField] private float brakeTorque = 4000f;
    [Tooltip("Maksimum direksiyon açısı (Cozy ve rahat dönüşler için 36-40 derece önerilir).")]
    [SerializeField] private float maxSteeringAngle = 38f;
    [Tooltip("El freni çekildiğinde arka tekerlere uygulanan sürtünme.")]
    [SerializeField] private float handbrakeFrictionMultiplier = 2f;
    [SerializeField] private float maxForwardSpeedKmh = 210f;
    [Range(0.05f, 1f)]
    [SerializeField] private float topSpeedMinTorqueFactor = 0.18f;
    [SerializeField] private float coastBrakeTorque = 45f;
    [Tooltip("Aracın ağırlık merkezi dengesi (Yere yakın olmalı).")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.6f, 0.1f);
    [Tooltip("Kargo yuklendiginde agirlik merkezine uygulanacak ek yukseklik.")]
    [SerializeField] private float cargoCenterOfMassYOffset = 0.2f;

    [Header("--- STABILITY ---")]
    [Tooltip("Araci yere bastirma kuvveti katsayisi (N per m/s).")]
    [SerializeField] private float downforcePerSpeed = 40f;
    [Tooltip("Downforce maksimumu: aracin agirliginin bu oranini gecmez.")]
    [SerializeField] private float maxDownforceWeightFraction = 0.3f;
    [Tooltip("Baslangicta wheel collider yuksekliklerini ayni temas duzlemine hizalar.")]
    [SerializeField] private bool autoNormalizeWheelColliderHeights = true;
    [Tooltip("Hiz arttikca direksiyon etkinligini azaltir.")]
    [SerializeField] private bool speedSensitiveSteering = true;
    [Tooltip("Direksiyon acisinin kisilmaya baslayacagi minimum hiz (km/s). Bu hizin altinda tam donus acisi korunur.")]
    [SerializeField] private float steeringReductionStartSpeedKmh = 20f;
    [Tooltip("Bu hizda (km/s) direksiyon azaltmasi maksimuma ulasir.")]
    [SerializeField] private float steeringReductionFullSpeedKmh = 95f;
    [Tooltip("Maksimum hizda direksiyon acisinin korunacak orani.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float steeringAtHighSpeedFactor = 0.52f;
    [Tooltip("Viraj icindeki tekerlegin donus acisi carpani (Ackermann). 1.05 - 1.08 arasi dogal ve yumusak donus saglar.")]
    [Range(1f, 1.25f)]
    [SerializeField] private float ackermannSteerMultiplier = 1.06f;
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

    [Header("--- COZY DRIVING ASSISTS ---")]
    [Tooltip("Normal suruste aracin yana sabun gibi kaymasini onleyen yanal tutus onleyici (0=kapali, 1=tam rayda tutus).")]
    [Range(0f, 1f)]
    [SerializeField] private float antiSlideGripFactor = 0.85f;
    [Tooltip("Virajlarda donerken hiz ve momentum kaybini onleyen katsayi (0=kapali, 1=tam momentum koruma).")]
    [Range(0f, 1f)]
    [SerializeField] private float corneringSpeedRetention = 0.90f;
    [Tooltip("Viraj alirken gaza basiliyorsa motor gucune uygulanan cekis ve tork destegi.")]
    [Range(0f, 1f)]
    [SerializeField] private float corneringPowerAssist = 0.30f;
    [Tooltip("Oyuncu direksiyon cevirdiginde aracin viraji rahat ve cevik almasi icin yaw donus destegi.")]
    [Range(0f, 1f)]
    [SerializeField] private float agileTurnAssist = 0.22f;
    [Tooltip("Direksiyon bosken ve arac kayarken spin atmasini onleyen dengeleyici (0=kapali, 1=tam destek).")]
    [Range(0f, 1f)]
    [SerializeField] private float antiSpinStability = 0.42f;

    [Header("--- BODY PITCH DYNAMICS (Squat & Dive) ---")]
    [Tooltip("Gaza basildiginda aracin onunun havaya kalkmasi (squat) kuvveti.")]
    [SerializeField] private float accelerationSquatTorque = 1800f;
    [Tooltip("Frene basildiginda aracin onunun yere basilmasi (brake dive) kuvveti.")]
    [SerializeField] private float brakeDiveTorque = 2600f;
    [Tooltip("Pitch dinamiginin tepki ve yumusama hizi.")]
    [SerializeField] private float pitchDamping = 8f;

    [Header("--- INPUT SMOOTHING ---")]
    [Tooltip("Gaz/fren girdisinin ne kadar hizli artacagi (birim/s).")]
    [SerializeField] private float throttleRiseRate = 12f;
    [Tooltip("Gaz/fren girdisinin ne kadar hizli azalacagi (birim/s).")]
    [SerializeField] private float throttleFallRate = 16f;
    [Tooltip("Direksiyon girdisinin ne kadar hizli hedefe ulasacagi (birim/s). Dusuk deger daha yumusak donus saglar.")]
    [SerializeField] private float steeringInputRate = 3.8f;
    [Tooltip("Direksiyon birakildiginda merkeze donus hizi (birim/s).")]
    [SerializeField] private float steeringFallRate = 5.5f;

    [Header("--- HANDBRAKE DRIFT ---")]
    [Tooltip("El freni birakildiginda kaymanin aninda kesilmeyip tatli ve akici sekilde toparlanma suresi (s).")]
    [Range(0.1f, 4f)]
    [SerializeField] private float driftRecoveryDuration = 2.0f;
    [Tooltip("El freni aktifken arka teker yan tutusunun korunacak orani.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float handbrakeRearSidewaysGripFactor = 0.20f;
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
    private float currentPitchTorque;
    private float currentDriftGripBlend;
    private float currentSteerAssistTorque;
    private float previousSpeedMetersPerSec;
    private float lastHardBrakeNotifyTime = -999f;
    private float accumulatedDriftScore;
    private int committedDriftScore;
    private WheelFrictionCurve rearLeftSidewaysFrictionBase;
    private WheelFrictionCurve rearRightSidewaysFrictionBase;
    private bool rearFrictionCached;
    private Vector3 frontLeftVisualOffset;
    private Vector3 frontRightVisualOffset;
    private Vector3 rearLeftVisualOffset;
    private Vector3 rearRightVisualOffset;
    private Quaternion frontLeftVisualRotationOffset = Quaternion.identity;
    private Quaternion frontRightVisualRotationOffset = Quaternion.identity;
    private Quaternion rearLeftVisualRotationOffset = Quaternion.identity;
    private Quaternion rearRightVisualRotationOffset = Quaternion.identity;

    // Input Action References
    private InputAction moveAction;
    private InputAction handbrakeAction;

    private void Awake()
    {
        if (autoNormalizeWheelColliderHeights)
        {
            NormalizeWheelColliderHeightsRuntime();
        }

        // Eski sert/hizli direksiyon ve drift degerlerini daha yumusak ve dogal surus icin guncelle
        if (steeringReductionStartSpeedKmh >= 48f && steeringReductionFullSpeedKmh >= 135f)
        {
            steeringReductionStartSpeedKmh = 20f;
            steeringReductionFullSpeedKmh = 95f;
            steeringAtHighSpeedFactor = 0.52f;
        }
        if (steeringInputRate > 6f && steeringFallRate > 9f)
        {
            steeringInputRate = 3.8f;
            steeringFallRate = 5.5f;
        }
        if (agileTurnAssist > 0.3f)
        {
            agileTurnAssist = 0.22f;
        }
        if (antiSpinStability > 0.55f)
        {
            antiSpinStability = 0.42f;
        }

        rb = GetComponent<Rigidbody>();
        SetupRigidbody();
        ConfigureBodyCollidersForStability();
        CacheWheelVisualOffsets();
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
        ApplyBodyPitchDynamics(Time.fixedDeltaTime); // Gaza basinca kalkma (squat), frende basilma (dive)
        ApplyAntiSlideDamping(); // Cozy yanal tutus / kayma onleyici
        ApplySteerAssist(); // Akilli viraj yonlendirme ve spin onleme
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

        float steerRate = Mathf.Abs(targetSteer) > Mathf.Abs(smoothedSteerInput)
            ? steeringInputRate
            : steeringFallRate;

        smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, targetSteer, steerRate * deltaTime);
    }

    private void HandleInput()
    {
        Vector2 input = Vector2.zero;
        if (moveAction != null)
        {
            input = moveAction.ReadValue<Vector2>();
        }

        // Direct Keyboard Fallback (Input Action bir şekilde sinyal vermezse veya focus kaybolursa asla durmasın)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            float y = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;

            float x = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;

            if (Mathf.Abs(y) > 0.01f || Mathf.Abs(x) > 0.01f)
            {
                input = new Vector2(x, y);
            }
        }

        moveInput = input;

        bool handbrake = handbrakeAction != null && handbrakeAction.IsPressed();
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            handbrake = handbrake || UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed;
        }

        // Gaz veriliyorsa ve el freni tuşuna basılmıyorsa el frenini serbest bırak
        if (Mathf.Abs(input.y) > 0.1f && !handbrake)
        {
            isHandbraking = false;
        }
        else
        {
            isHandbraking = handbrake;
        }
    }

    private void HandleMotor()
    {
        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
        }

        float accelInput = smoothedThrottleInput;

        // Fuel check: engine blocked if out of fuel
        FuelSystem fuelSystem = GetComponent<FuelSystem>();
        if (fuelSystem != null && fuelSystem.ShouldBlockEngine())
        {
            ApplyMotorTorque(0f);
            float stopBrake = coastBrakeTorque * 2f;
            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = stopBrake;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = stopBrake;
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = stopBrake;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = stopBrake;
            return;
        }

        // BUG FIX: "Teker dönüyor ama gitmiyor" sorununun ana kaynaklarından biri:
        // Fren torku 0 değilse motor torku çalışmaz. Gaz veriyorsak freni kesinlikle 0 yapmalıyız.

        float forwardVelocity = Vector3.Dot(transform.forward, rb.linearVelocity);
        bool isMovingForward = forwardVelocity > 0.6f;
        bool isMovingReverse = forwardVelocity < -0.6f;

        // Yön değiştirme mantığı (İleri giderken geriye basılırsa fren yap, durduktan sonra geri git)
        if (accelInput > 0.05f && isMovingReverse)
        {
            isBraking = true;
        }
        else if (accelInput < -0.05f && isMovingForward)
        {
            isBraking = true;
        }
        else
        {
            isBraking = false;
        }

        // --- MOTOR GÜCÜ (AWD / FWD / RWD Çekiş Dağılımı) ---
        if (!isBraking && !isHandbraking && Mathf.Abs(accelInput) > 0.02f)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float topSpeedRatio = maxForwardSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / maxForwardSpeedKmh) : 0f;
            float speedTorqueFactor = Mathf.Lerp(1f, topSpeedMinTorqueFactor, topSpeedRatio);

            // Virajda direksiyon dönmüşken tekerlek sürtünme direncini yenmek için ek tork desteği
            // Drift esnasında tork artışı yapılmaz; normal motor torku korunarak kontrolsüz hızlanma önlenir
            float steerRatio = maxSteeringAngle > 0.1f ? Mathf.Clamp01(Mathf.Abs(currentSteerAngle) / maxSteeringAngle) : 0f;
            float corneringBoost = 1f;
            if (currentDriftGripBlend <= 0.05f && !isHandbraking)
            {
                corneringBoost += (steerRatio * corneringPowerAssist);
            }

            float appliedTorque = accelInput * motorTorque * speedTorqueFactor * corneringBoost;

            if (accelInput > 0f && speedKmh >= maxForwardSpeedKmh)
            {
                appliedTorque = 0f;
            }

            ApplyMotorTorque(appliedTorque);

            // Gaz verilirken tekerleklerin frenini KESİNLİKLE anında sıfırla
            currentBrakeTorque = 0f;
            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = 0f;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = 0f;
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = 0f;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = 0f;
        }
        else
        {
            ApplyMotorTorque(0f);
        }

        // --- FRENLEME ---
        if (isHandbraking)
        {
            currentBrakeTorque = brakeTorque * handbrakeFrictionMultiplier; // El freni daha sert
            // Ön tekerler el freninde kilitlenmez (yön kontrolü korunur)
            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = 0f;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = 0f;
            // Arka tekerler kilitlenir
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = currentBrakeTorque;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = currentBrakeTorque;
        }
        else if (isBraking || Mathf.Abs(accelInput) < 0.05f) // Gaz verilmiyorsa da hafif süzülme freni (drag)
        {
            // Drift esnasinda ve drift cikisinda kaymanin birden kesilmemesi icin suzulme freni yumusatilir
            float effectiveCoastBrake = coastBrakeTorque * (1f - currentDriftGripBlend);
            currentBrakeTorque = isBraking ? brakeTorque : effectiveCoastBrake;

            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = currentBrakeTorque;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = currentBrakeTorque;
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = currentBrakeTorque;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = currentBrakeTorque;
        }
        else
        {
            // Normal hareket halindeyiz - frenler tamamen serbest
            if (frontLeftCollider != null) frontLeftCollider.brakeTorque = 0f;
            if (frontRightCollider != null) frontRightCollider.brakeTorque = 0f;
            if (rearLeftCollider != null) rearLeftCollider.brakeTorque = 0f;
            if (rearRightCollider != null) rearRightCollider.brakeTorque = 0f;
        }
    }

    private void ApplyMotorTorque(float torque)
    {
        switch (driveTrain)
        {
            case DriveTrainType.AWD:
                float front = torque * frontDriveBias * 0.5f;
                float rear = torque * (1f - frontDriveBias) * 0.5f;
                if (frontLeftCollider != null) frontLeftCollider.motorTorque = front;
                if (frontRightCollider != null) frontRightCollider.motorTorque = front;
                if (rearLeftCollider != null) rearLeftCollider.motorTorque = rear;
                if (rearRightCollider != null) rearRightCollider.motorTorque = rear;
                break;
            case DriveTrainType.FWD:
                float fwdPerWheel = torque * 0.5f;
                if (frontLeftCollider != null) frontLeftCollider.motorTorque = fwdPerWheel;
                if (frontRightCollider != null) frontRightCollider.motorTorque = fwdPerWheel;
                if (rearLeftCollider != null) rearLeftCollider.motorTorque = 0f;
                if (rearRightCollider != null) rearRightCollider.motorTorque = 0f;
                break;
            case DriveTrainType.RWD:
            default:
                float rwdPerWheel = torque * 0.5f;
                if (frontLeftCollider != null) frontLeftCollider.motorTorque = 0f;
                if (frontRightCollider != null) frontRightCollider.motorTorque = 0f;
                if (rearLeftCollider != null) rearLeftCollider.motorTorque = rwdPerWheel;
                if (rearRightCollider != null) rearRightCollider.motorTorque = rwdPerWheel;
                break;
        }
    }

    private void HandleSteering()
    {
        float steerInput = smoothedSteerInput;

        float steerFactor = 1f;
        if (speedSensitiveSteering)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            if (speedKmh > steeringReductionStartSpeedKmh)
            {
                float t = Mathf.InverseLerp(steeringReductionStartSpeedKmh, steeringReductionFullSpeedKmh, speedKmh);
                // SmoothStep: yumusak kavis, dusuk ve orta hizlarda (0-50 km/h) tam aci korunur
                float smoothT = t * t * (3f - 2f * t);
                steerFactor = Mathf.Lerp(1f, steeringAtHighSpeedFactor, smoothT);
            }
        }

        currentSteerAngle = maxSteeringAngle * steerInput * steerFactor;

        // --- ACKERMANN STEERING APPROXIMATION ---
        // Viraj icindeki tekerlek biraz daha fazla donerek daha keskin ve temiz donus saglar
        float ackermannFactor = ackermannSteerMultiplier;
        if (currentSteerAngle > 0) // Turning Right
        {
            frontRightCollider.steerAngle = currentSteerAngle * ackermannFactor;
            frontLeftCollider.steerAngle = currentSteerAngle;
        }
        else if (currentSteerAngle < 0) // Turning Left
        {
            frontLeftCollider.steerAngle = currentSteerAngle * ackermannFactor;
            frontRightCollider.steerAngle = currentSteerAngle;
        }
        else
        {
            frontLeftCollider.steerAngle = 0f;
            frontRightCollider.steerAngle = 0f;
        }
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
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh, frontLeftVisualOffset, frontLeftVisualRotationOffset);
        UpdateSingleWheel(frontRightCollider, frontRightMesh, frontRightVisualOffset, frontRightVisualRotationOffset);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh, rearLeftVisualOffset, rearLeftVisualRotationOffset);
        UpdateSingleWheel(rearRightCollider, rearRightMesh, rearRightVisualOffset, rearRightVisualRotationOffset);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform, Vector3 localPositionOffset, Quaternion localRotationOffset)
    {
        if (wheelCollider == null || wheelTransform == null) return;

        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);

        Quaternion offsetRotation = GetWheelOffsetRotation(wheelCollider);
        wheelTransform.position = pos + (offsetRotation * localPositionOffset);
        wheelTransform.rotation = rot * localRotationOffset;
    }

    private void CacheWheelVisualOffsets()
    {
        CacheWheelVisualOffset(frontLeftCollider, frontLeftMesh, out frontLeftVisualOffset, out frontLeftVisualRotationOffset);
        CacheWheelVisualOffset(frontRightCollider, frontRightMesh, out frontRightVisualOffset, out frontRightVisualRotationOffset);
        CacheWheelVisualOffset(rearLeftCollider, rearLeftMesh, out rearLeftVisualOffset, out rearLeftVisualRotationOffset);
        CacheWheelVisualOffset(rearRightCollider, rearRightMesh, out rearRightVisualOffset, out rearRightVisualRotationOffset);
    }

    private static void CacheWheelVisualOffset(
        WheelCollider wheelCollider,
        Transform wheelTransform,
        out Vector3 localPositionOffset,
        out Quaternion localRotationOffset)
    {
        localPositionOffset = Vector3.zero;
        localRotationOffset = Quaternion.identity;

        if (wheelCollider == null || wheelTransform == null)
        {
            return;
        }

        Transform wheelRoot = wheelCollider.transform;
        localPositionOffset = Quaternion.Inverse(wheelRoot.rotation) * (wheelTransform.position - wheelRoot.position);
        localRotationOffset = Quaternion.Inverse(wheelRoot.rotation) * wheelTransform.rotation;
    }

    private static Quaternion GetWheelOffsetRotation(WheelCollider wheelCollider)
    {
        Transform wheelRoot = wheelCollider.transform;
        return wheelRoot.rotation * Quaternion.Euler(0f, wheelCollider.steerAngle, 0f);
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

    private void ApplyBodyPitchDynamics(float deltaTime)
    {
        if (rb == null || deltaTime <= 0f)
        {
            return;
        }

        float groundedRatio = GetGroundedWheelsRatio();
        if (groundedRatio <= 0.25f)
        {
            // Havada takla atmayi onlemek icin sadece tekerlekler yere basiyorken calisir
            currentPitchTorque = Mathf.MoveTowards(currentPitchTorque, 0f, pitchDamping * 1000f * deltaTime);
            return;
        }

        float targetPitchTorque = 0f;
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);

        if (isHandbraking)
        {
            // El freni: on yere basilir (dive) -> pozitif X torku Unity'de burnu asagi eger
            targetPitchTorque = brakeDiveTorque * 0.75f;
        }
        else if (isBraking)
        {
            // Normal frenleme: on guclu bir sekilde yere basilir (brake dive) -> pozitif X torku
            targetPitchTorque = brakeDiveTorque;
        }
        else if (smoothedThrottleInput > 0.05f)
        {
            // Gaza basiliyor: burun havaya kalkar, arka basilir (acceleration squat) -> negatif X torku Unity'de burnu yukari kaldirir
            targetPitchTorque = -smoothedThrottleInput * accelerationSquatTorque;
        }
        else if (smoothedThrottleInput < -0.05f && forwardSpeed < 0.2f)
        {
            // Geri viteste gaza basiliyor: burun hafifce basilir, arka kalkar
            targetPitchTorque = -smoothedThrottleInput * accelerationSquatTorque * 0.6f;
        }

        currentPitchTorque = Mathf.MoveTowards(currentPitchTorque, targetPitchTorque, pitchDamping * 2000f * deltaTime);

        if (Mathf.Abs(currentPitchTorque) > 1f)
        {
            // transform.right ekseninde tork:
            // Negatif tork burnu YUKARI kaldirir (Squat)
            // Pozitif tork burnu ASAGI basar (Dive)
            Vector3 pitchTorqueVec = transform.right * (currentPitchTorque * groundedRatio);
            rb.AddTorque(pitchTorqueVec, ForceMode.Force);
        }
    }

    private void ApplyAntiSlideDamping()
    {
        if (antiSlideGripFactor <= 0f || rb == null)
        {
            return;
        }

        float groundedRatio = GetGroundedWheelsRatio();
        if (groundedRatio <= 0.05f)
        {
            return;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralSpeed = localVelocity.x;
        float forwardSpeed = localVelocity.z;

        if (Mathf.Abs(lateralSpeed) < 0.05f)
        {
            return;
        }

        // Drift halindeyken ve el freni birakildiginda yanal kaymanin aninda kesilmeyip tatli sekilde suzulmesi
        float gripRecoveryCurve = Mathf.Pow(Mathf.Clamp01(1f - currentDriftGripBlend), 2.2f);
        float effectiveGripFactor = antiSlideGripFactor * gripRecoveryCurve;
        if (effectiveGripFactor <= 0.01f)
        {
            return;
        }

        // Yanal kaymayi kademeli sonumle: küt oturmayi onler, el freni sonrasi tatli kaymaya izin verir
        float dynamicDampingRate = Mathf.Lerp(3.2f, 12f, gripRecoveryCurve);
        float dampingAcceleration = -lateralSpeed * (effectiveGripFactor * dynamicDampingRate);
        Vector3 worldForce = transform.right * (dampingAcceleration * groundedRatio * rb.mass);
        rb.AddForce(worldForce, ForceMode.Force);

        // --- VIRAJDA HIZ KORUMA (Cornering Speed Retention) ---
        // Normal virajlarda kinetik enerjinin tamamen yok olmasını ve aracın bayılmasını önler.
        // DRIFT KONTROLÜ: El freni çekildiğinde veya araç drift halindeyken (lateralSpeed > 1.2m/s veya currentDriftGripBlend > 0.05f)
        // ileri itiş desteği KESİNLİKLE UYGULANMAZ. Gaza basıldığında araç roket gibi fırlamaz, doğal drift patinajı korunur.
        float absForwardSpeed = Mathf.Abs(forwardSpeed);
        bool isDrifting = currentDriftGripBlend > 0.05f || isHandbraking || Mathf.Abs(lateralSpeed) > 1.2f;

        if (!isDrifting && corneringSpeedRetention > 0f && absForwardSpeed > 0.5f)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            if (speedKmh < maxForwardSpeedKmh)
            {
                float planeSpeed = Mathf.Sqrt(lateralSpeed * lateralSpeed + forwardSpeed * forwardSpeed);

                // Normal virajda hafif hız koruma (asla 0.98 gibi roket değerlerine çıkmaz)
                float effectiveRetention = Mathf.Clamp(corneringSpeedRetention, 0f, 0.65f);
                float targetSpeed = Mathf.Lerp(absForwardSpeed, planeSpeed, effectiveRetention);
                float speedDeficit = targetSpeed - absForwardSpeed;

                if (speedDeficit > 0.01f)
                {
                    // İvme sınırını makul bir seviyeye (maksimum 3.5 m/s²) çekiyoruz (önceden 28 m/s² gibi roket ivmesiydi!)
                    float forwardAccel = (speedDeficit / Time.fixedDeltaTime) * (effectiveGripFactor * 0.4f);
                    forwardAccel = Mathf.Min(forwardAccel, 3.5f);

                    Vector3 forwardDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
                    Vector3 forwardForce = forwardDir * (forwardAccel * groundedRatio * rb.mass);
                    rb.AddForce(forwardForce, ForceMode.Force);
                }
            }
        }
    }

    private void ApplySteerAssist()
    {
        if (rb == null || rb.linearVelocity.magnitude < 2f)
        {
            return;
        }

        bool isMovingForward = Vector3.Dot(transform.forward, rb.linearVelocity) > 0.3f;
        if (!isMovingForward) return;

        float steerMagnitude = Mathf.Abs(smoothedSteerInput);

        // DURUM 1: Oyuncu aktif olarak viraj aliyor (Agile Turn Assist)
        // Viraji rahat, yumusak ve cevik almasi icin kademeli artan yaw tork destegi ver
        if (steerMagnitude > 0.05f && agileTurnAssist > 0f)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float speedScale = Mathf.Clamp01(speedKmh / 50f);

            // Sert küt donusleri onlemek icin torku yumusat
            float targetTurnTorque = smoothedSteerInput * agileTurnAssist * speedScale * (rb.mass * 4.2f);

            // Acisal hiz (yaw rate) cok yuksekse torku kisarak ani savrulmayi ve küt donusu onle
            float currentYaw = rb.angularVelocity.y;
            if ((smoothedSteerInput > 0f && currentYaw > 1.6f) || (smoothedSteerInput < 0f && currentYaw < -1.6f))
            {
                targetTurnTorque *= 0.2f;
            }

            currentSteerAssistTorque = Mathf.MoveTowards(currentSteerAssistTorque, targetTurnTorque, (rb.mass * 10f) * Time.fixedDeltaTime);
            rb.AddTorque(Vector3.up * currentSteerAssistTorque, ForceMode.Force);
            return;
        }

        currentSteerAssistTorque = Mathf.MoveTowards(currentSteerAssistTorque, 0f, (rb.mass * 15f) * Time.fixedDeltaTime);

        // DURUM 2: Direksiyon notr (Anti-Spin Stability)
        // Oyuncu direksiyon cevirmiyorken arka savruluyorsa, burnu hareket yonune toparla
        // Drift esnasinda veya drift cikisinda arac küt bicimde duzeltilmesin diye yumusak toparlama
        if (steerMagnitude <= 0.05f && antiSpinStability > 0f)
        {
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            flatForward.Normalize();

            Vector3 flatVelocity = rb.linearVelocity;
            flatVelocity.y = 0f;
            flatVelocity.Normalize();

            float angleDiff = Vector3.SignedAngle(flatForward, flatVelocity, Vector3.up);

            if (Mathf.Abs(angleDiff) > 1.5f && Mathf.Abs(angleDiff) < 65f)
            {
                // PD kontrolcu: Aci farki (P) ile acisal hizi (D) dengeleyerek küt toparlamayi onler
                float angleRad = angleDiff * Mathf.Deg2Rad;
                float currentYaw = rb.angularVelocity.y;
                float dampingD = currentYaw * 650f;
                float targetTorque = (angleRad * 2400f - dampingD) * antiSpinStability;

                // Drift esnasinda ve cikisinda yumusak toparlama (küt duzeltmeyi onler)
                float blendScale = Mathf.Pow(Mathf.Clamp01(1f - currentDriftGripBlend), 1.8f);
                targetTorque *= blendScale;
                rb.AddTorque(Vector3.up * targetTorque, ForceMode.Force);
            }
        }
    }

    private float GetGroundedWheelsRatio()
    {
        int groundedCount = 0;
        int totalWheels = 0;

        CheckWheelGrounded(frontLeftCollider, ref groundedCount, ref totalWheels);
        CheckWheelGrounded(frontRightCollider, ref groundedCount, ref totalWheels);
        CheckWheelGrounded(rearLeftCollider, ref groundedCount, ref totalWheels);
        CheckWheelGrounded(rearRightCollider, ref groundedCount, ref totalWheels);

        return totalWheels > 0 ? (float)groundedCount / totalWheels : 0f;
    }

    private static void CheckWheelGrounded(WheelCollider wheel, ref int groundedCount, ref int totalWheels)
    {
        if (wheel == null) return;
        totalWheels++;
        if (wheel.isGrounded)
        {
            groundedCount++;
        }
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
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralSpeed = Mathf.Abs(localVel.x);

        bool isInitiatingDrift = isHandbraking && speedKmh >= handbrakeDriftMinSpeedKmh;
        bool isResidualSlide = lateralSpeed > 0.8f && speedKmh >= 8f;

        // Drift blend: El freni basildiginda hizlica drifte girer (targetBlend = 1f).
        // El freni birakildiginda eger arac hala yana kayiyorsa hemen 0'a cakilmaz,
        // kayma hizina bagli olarak blend'i bir sure yuksek tutar ve yumusakca 0'a iner.
        float targetBlend = 0f;
        if (isInitiatingDrift)
        {
            targetBlend = 1f;
        }
        else if (isResidualSlide && currentDriftGripBlend > 0.1f)
        {
            targetBlend = Mathf.Clamp01(lateralSpeed / 4f) * 0.65f;
        }

        float blendRate = isInitiatingDrift
            ? 8f
            : (driftRecoveryDuration > 0.01f ? (1f / driftRecoveryDuration) : 10f);

        currentDriftGripBlend = Mathf.MoveTowards(currentDriftGripBlend, targetBlend, blendRate * Time.fixedDeltaTime);

        // Arka lastik yanal tutusunu kademeli blend ile uygula (smooth step gecis)
        float smoothGripBlend = Mathf.SmoothStep(0f, 1f, currentDriftGripBlend);
        float gripFactor = Mathf.Lerp(1f, handbrakeRearSidewaysGripFactor, smoothGripBlend);
        gripFactor = Mathf.Clamp(gripFactor, 0.15f, 1f);
        ApplyRearSidewaysGrip(gripFactor);

        if (currentDriftGripBlend > 0.05f && Mathf.Abs(smoothedSteerInput) > 0.05f)
        {
            float steerSign = Mathf.Sign(smoothedSteerInput);
            float assistScale = Mathf.InverseLerp(handbrakeDriftMinSpeedKmh, handbrakeDriftMinSpeedKmh + 40f, speedKmh);
            float yawTorque = handbrakeDriftAssistTorque * steerSign * assistScale * currentDriftGripBlend;
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

        bool driftScoringActive = (isHandbraking || currentDriftGripBlend > 0.2f)
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
    public void ApplyUpgrades(int engineLevel, int brakeLevel, int handlingLevel)
    {
        // %10 increase per engine level
        motorTorque = 2200f * (1f + (engineLevel * 0.12f));
        maxForwardSpeedKmh = 210f + (engineLevel * 15f);

        // %15 increase per brake level
        brakeTorque = 3600f * (1f + (brakeLevel * 0.15f));
        handbrakeRearSidewaysGripFactor = Mathf.Max(0.2f, 0.55f - (brakeLevel * 0.05f)); // Better drifts

        // Handling improvements
        downforcePerSpeed = 35f * (1f + (handlingLevel * 0.2f));
        steeringAtHighSpeedFactor = Mathf.Min(0.75f, 0.52f + (handlingLevel * 0.04f));
        corneringSpeedRetention = Mathf.Min(0.98f, 0.90f + (handlingLevel * 0.02f));

        Debug.Log($"[CarController] Upgrades Applied - Engine: {engineLevel}, Brake: {brakeLevel}, Handling: {handlingLevel}");
    }

}
