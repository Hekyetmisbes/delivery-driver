using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float accelerationPower = 8f;
    [SerializeField] private float brakePower = 15f;
    [SerializeField] private float reverseSpeed = 10f;
    [SerializeField] private float throttleResponseSpeed = 5f;
    [SerializeField] private float speedSmoothness = 3f;

    [Header("Steering Settings")]
    [SerializeField] private float turnSpeed = 80f;
    [SerializeField] private float maxSteeringAngle = 25f;
    [SerializeField] private float steeringInputSmoothness = 12f;
    [SerializeField] private float steeringReturnSpeed = 8f;
    [SerializeField] private float minSpeedToTurn = 2f;
    [SerializeField] private float rotationSmoothness = 8f;
    [SerializeField] private AnimationCurve steeringCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);

    [Header("Wheel Visual Settings")]
    [SerializeField] private float maxWheelVisualAngle = 30f;
    [SerializeField] private float wheelSteeringSmoothness = 10f;

    [Header("Physics Settings")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private float dragCoefficient = 0.98f;
    [SerializeField] private float bodyTiltAmount = 2f;

    [Header("Wheel Settings")]
    [SerializeField] private float wheelRotationSpeed = 360f;
    [SerializeField] private bool autoFindWheels = true;
    [SerializeField] private List<Transform> frontWheels = new List<Transform>();
    [SerializeField] private List<Transform> rearWheels = new List<Transform>();

    [Header("Input Settings (Optional)")]
    [SerializeField] private InputActionAsset inputActions;

    // Private variables
    private float currentSpeed = 0f;
    private float currentSteerAngle = 0f;
    private float visualSteerAngle = 0f;
    private float wheelSpinAngle = 0f;
    private Vector2 moveInput;
    private float smoothedThrottle = 0f;
    private Rigidbody rb;
    private InputAction moveAction;
    private Transform bodyTransform;

    // Store initial wheel rotations
    private Dictionary<Transform, Quaternion> wheelInitialRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, bool> wheelIsOnLeft = new Dictionary<Transform, bool>();

    void Start()
    {
        SetupRigidbody();
        SetupWheels();
        SetupInput();
    }

    void SetupRigidbody()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("CarController: No Rigidbody found. Adding Rigidbody component.");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = 1200f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.8f;
        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Freeze rotation on X and Z to prevent flipping
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void SetupWheels()
    {
        if (autoFindWheels)
        {
            frontWheels.Clear();
            rearWheels.Clear();
            wheelInitialRotations.Clear();

            // Find all transforms with MeshFilter (actual visual wheels)
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Transform wheelTransform = meshFilter.transform;
                string name = wheelTransform.name.ToLower();

                // Check if this is a wheel mesh
                if (name.Contains("wheel") && !name.Contains("collider"))
                {
                    // Store initial rotation
                    wheelInitialRotations[wheelTransform] = wheelTransform.localRotation;

                    // Determine if wheel is on left side (negative X position)
                    wheelIsOnLeft[wheelTransform] = wheelTransform.localPosition.x < 0;

                    // Categorize by position or name
                    if (name.Contains("front"))
                    {
                        frontWheels.Add(wheelTransform);
                    }
                    else if (name.Contains("rear") || name.Contains("back"))
                    {
                        rearWheels.Add(wheelTransform);
                    }
                    else
                    {
                        // Determine by Z position (front wheels have positive Z)
                        if (wheelTransform.localPosition.z > 0)
                            frontWheels.Add(wheelTransform);
                        else
                            rearWheels.Add(wheelTransform);
                    }

                    Debug.Log($"Found wheel: {wheelTransform.name} at position {wheelTransform.localPosition}, Left side: {wheelIsOnLeft[wheelTransform]}");
                }
            }

            Debug.Log($"CarController: Found {frontWheels.Count} front wheels and {rearWheels.Count} rear wheels.");
        }
        else
        {
            // Store initial rotations for manually assigned wheels
            foreach (Transform wheel in frontWheels)
            {
                if (wheel != null)
                    wheelInitialRotations[wheel] = wheel.localRotation;
            }
            foreach (Transform wheel in rearWheels)
            {
                if (wheel != null)
                    wheelInitialRotations[wheel] = wheel.localRotation;
            }
        }

        // Find body for tilting effect
        bodyTransform = transform.Find("Body");
        if (bodyTransform == null)
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Contains("body") || child.name.ToLower().Contains("chassis"))
                {
                    bodyTransform = child;
                    break;
                }
            }
        }
    }

    void SetupInput()
    {
        if (inputActions != null)
        {
            var actionMap = inputActions.FindActionMap("Player");
            if (actionMap != null)
            {
                moveAction = actionMap.FindAction("Move");
                if (moveAction != null)
                {
                    moveAction.Enable();
                    Debug.Log("CarController: Using InputActionAsset for controls.");
                    return;
                }
            }
        }

        Debug.Log("CarController: Using Keyboard direct input (WASD/Arrows).");
    }

    void Update()
    {
        GetInput();
        AnimateWheels();
        AnimateBodyTilt();
    }

    void GetInput()
    {
        if (moveAction != null && moveAction.enabled)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }
        else
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    vertical = 1f;
                else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    vertical = -1f;

                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    horizontal = -1f;
                else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    horizontal = 1f;
            }

            moveInput = new Vector2(horizontal, vertical);
        }
    }

    void FixedUpdate()
    {
        HandleAcceleration();
        HandleSteering();
        ApplyDrag();
    }

    private void HandleAcceleration()
    {
        float verticalInput = moveInput.y;

        // Smooth throttle input - pedal gibi, yavaşça basılıyor hissi
        smoothedThrottle = Mathf.Lerp(smoothedThrottle, verticalInput, Time.fixedDeltaTime * throttleResponseSpeed);

        float targetSpeed = 0f;

        if (smoothedThrottle > 0.01f)
        {
            // Forward acceleration - smooth throttle kullan
            targetSpeed = maxSpeed * smoothedThrottle;

            // Lerp kullanarak daha smooth hızlanma
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.fixedDeltaTime * speedSmoothness);
        }
        else if (smoothedThrottle < -0.01f)
        {
            if (currentSpeed > 0.5f)
            {
                // Braking - daha smooth fren
                currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.fixedDeltaTime * (brakePower * 0.4f));
            }
            else
            {
                // Reverse - smooth geri vites
                targetSpeed = reverseSpeed * smoothedThrottle;
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.fixedDeltaTime * (speedSmoothness * 0.5f));
            }
        }
        else
        {
            // Natural deceleration - yavaşça dur
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.fixedDeltaTime * (brakePower * 0.2f));
        }

        // Enforce hard speed limits - prevent exceeding max speed
        currentSpeed = Mathf.Clamp(currentSpeed, -reverseSpeed, maxSpeed);

        // Apply movement
        Vector3 movement = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    private void HandleSteering()
    {
        float horizontalInput = moveInput.x;

        // Calculate target steering angle based on speed
        float speedFactor = Mathf.Abs(currentSpeed) / maxSpeed;
        float steeringMultiplier = steeringCurve.Evaluate(speedFactor);
        float targetSteerAngle = horizontalInput * maxSteeringAngle * steeringMultiplier;

        // Smooth steering input for more realistic, gradual turning
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            // Gradual steering response - feels more natural and less rigid
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steeringInputSmoothness);
        }
        else
        {
            // Return to center when no input
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, 0f, Time.fixedDeltaTime * steeringReturnSpeed);
        }

        // Apply rotation ONLY when moving fast enough
        if (Mathf.Abs(currentSpeed) >= minSpeedToTurn)
        {
            // Calculate turn amount proportional to speed
            // This prevents spinning in place and makes turning feel realistic
            float speedBasedTurn = Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxSpeed);

            // More gradual turn application for smoother cornering
            // When reversing (currentSpeed < 0), reverse the steering direction
            float directionMultiplier = currentSpeed >= 0 ? 1f : -1f;
            float turnAmount = currentSteerAngle * turnSpeed * speedBasedTurn * directionMultiplier * Time.fixedDeltaTime;

            // Smooth rotation using Slerp instead of direct Euler rotation
            Quaternion targetRotation = rb.rotation * Quaternion.Euler(0f, turnAmount, 0f);
            Quaternion smoothRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSmoothness * Time.fixedDeltaTime);
            rb.MoveRotation(smoothRotation);
        }
    }

    private void ApplyDrag()
    {
        // Apply drag to simulate air resistance and rolling resistance
        currentSpeed *= dragCoefficient;
    }

    private void AnimateWheels()
    {
        // Update wheel spin angle based on speed
        wheelSpinAngle += currentSpeed * wheelRotationSpeed * Time.deltaTime;

        // Keep angle in reasonable range to prevent overflow
        if (wheelSpinAngle > 360f) wheelSpinAngle -= 360f;
        if (wheelSpinAngle < -360f) wheelSpinAngle += 360f;

        // Smooth visual steering angle (slower than actual steering for realism)
        float targetVisualAngle = moveInput.x * maxWheelVisualAngle;
        visualSteerAngle = Mathf.Lerp(visualSteerAngle, targetVisualAngle, Time.deltaTime * wheelSteeringSmoothness);

        // Apply rotation to front wheels (spin + steering)
        foreach (Transform wheel in frontWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                // Get initial rotation
                Quaternion initialRot = wheelInitialRotations[wheel];

                // Check if wheel is on left or right side
                bool isLeftWheel = wheelIsOnLeft.ContainsKey(wheel) && wheelIsOnLeft[wheel];

                // Create spin rotation (around local X axis)
                // Left wheels spin opposite direction
                float spinDirection = isLeftWheel ? -wheelSpinAngle : wheelSpinAngle;
                Quaternion spinRotation = Quaternion.AngleAxis(spinDirection, Vector3.right);

                // Create steering rotation (around local Y axis)
                Quaternion steerRotation = Quaternion.AngleAxis(visualSteerAngle, Vector3.up);

                // Combine rotations properly
                wheel.localRotation = initialRot * steerRotation * spinRotation;
            }
        }

        // Apply rotation to rear wheels (spin only, no steering)
        foreach (Transform wheel in rearWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                // Get initial rotation
                Quaternion initialRot = wheelInitialRotations[wheel];

                // Check if wheel is on left or right side
                bool isLeftWheel = wheelIsOnLeft.ContainsKey(wheel) && wheelIsOnLeft[wheel];

                // Create spin rotation (around local X axis)
                // Left wheels spin opposite direction
                float spinDirection = isLeftWheel ? -wheelSpinAngle : wheelSpinAngle;
                Quaternion spinRotation = Quaternion.AngleAxis(spinDirection, Vector3.right);

                // Combine: initial -> spin
                wheel.localRotation = initialRot * spinRotation;
            }
        }
    }

    private void AnimateBodyTilt()
    {
        if (bodyTransform == null) return;

        // Tilt body based on steering
        float targetTilt = -currentSteerAngle * bodyTiltAmount * 0.1f;
        Vector3 currentEuler = bodyTransform.localEulerAngles;
        float newZ = Mathf.LerpAngle(currentEuler.z, targetTilt, Time.deltaTime * 3f);
        bodyTransform.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, newZ);
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }
    }

    // Public getters
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentSteerAngle() => currentSteerAngle;

    // Debug visualization
    void OnDrawGizmos()
    {
        if (Application.isPlaying && rb != null)
        {
            // Draw center of mass
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMassOffset), 0.1f);

            // Draw velocity direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
