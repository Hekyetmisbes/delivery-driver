using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float accelerationPower = 15f;
    [SerializeField] private float brakePower = 25f;
    [SerializeField] private float reverseSpeed = 10f;

    [Header("Steering Settings")]
    [SerializeField] private float steeringSpeed = 150f;
    [SerializeField] private float maxSteeringAngle = 35f;
    [SerializeField] private AnimationCurve steeringCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);

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
    private float wheelSpinAngle = 0f;
    private Vector2 moveInput;
    private Rigidbody rb;
    private InputAction moveAction;
    private Transform bodyTransform;

    // Store initial wheel rotations
    private Dictionary<Transform, Quaternion> wheelInitialRotations = new Dictionary<Transform, Quaternion>();

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

                    Debug.Log($"Found wheel: {wheelTransform.name} at position {wheelTransform.localPosition}");
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
        float targetSpeed = 0f;

        if (verticalInput > 0)
        {
            // Forward acceleration
            targetSpeed = maxSpeed * verticalInput;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationPower * Time.fixedDeltaTime);
        }
        else if (verticalInput < 0)
        {
            if (currentSpeed > 0.5f)
            {
                // Braking
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * Time.fixedDeltaTime);
            }
            else
            {
                // Reverse
                targetSpeed = reverseSpeed * verticalInput;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationPower * 0.5f * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Natural deceleration
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * 0.3f * Time.fixedDeltaTime);
        }

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

        // Smooth steering
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * 5f);

        // Apply rotation only when moving
        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            float turnAmount = currentSteerAngle * (currentSpeed / maxSpeed) * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
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

        // Apply rotation to front wheels (spin + steering)
        foreach (Transform wheel in frontWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                // Get initial rotation
                Quaternion initialRot = wheelInitialRotations[wheel];

                // Create spin rotation (around local X axis)
                Quaternion spinRotation = Quaternion.AngleAxis(wheelSpinAngle, Vector3.right);

                // Create steering rotation (around local Y axis)
                Quaternion steerRotation = Quaternion.AngleAxis(currentSteerAngle, Vector3.up);

                // Combine: initial -> spin -> steer
                wheel.localRotation = initialRot * spinRotation * steerRotation;
            }
        }

        // Apply rotation to rear wheels (spin only, no steering)
        foreach (Transform wheel in rearWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                // Get initial rotation
                Quaternion initialRot = wheelInitialRotations[wheel];

                // Create spin rotation (around local X axis)
                Quaternion spinRotation = Quaternion.AngleAxis(wheelSpinAngle, Vector3.right);

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
