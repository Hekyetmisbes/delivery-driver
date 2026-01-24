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
    
    [Header("--- DEBUG ---")]
    [SerializeField] private bool showDebugGUI = true;

    // Private Runtime Variables
    private Rigidbody rb;
    private float currentSteerAngle;
    private float currentBrakeTorque;
    private bool isBraking;
    private bool isHandbraking;
    private Vector2 moveInput;
    
    // Input Action References
    private InputAction moveAction;
    private InputAction handbrakeAction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SetupRigidbody();
        SetupInput();
    }

    private void SetupRigidbody()
    {
        // BUG FIX: CenterOfMass yanlışsa araba takla atar veya tekerler boşa döner.
        rb.centerOfMass = centerOfMassOffset;
        rb.mass = 1500f; // Standart bir araba ağırlığı
        rb.linearDamping = 0.05f; // Hava direnci (çok yüksek olursa araç gitmez)
        rb.angularDamping = 0.5f; 
        // BUG FIX: Uyuyan fizik motoru sorunu için
        rb.sleepThreshold = 0.0f; 
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
        HandleMotor();
        HandleSteering();
        UpdateWheels();
        ApplyDownforce(); // Yüksek hızda yol tutuşu için
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
        float accelInput = moveInput.y;
        
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
        float steerInput = moveInput.x;
        currentSteerAngle = maxSteeringAngle * steerInput;

        // Sadece ön tekerler döner
        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
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
        // Yüksek hızda uçmayı engellemek için yere bastırma kuvveti
        rb.AddForce(-transform.up * rb.linearVelocity.magnitude * 50f);
    }

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
}