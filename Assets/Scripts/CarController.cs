using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 100f; // Increased for physics based speed (approx km/h)
    [SerializeField] private float accelerationPower = 60000f; // Force in Newtons (Increased to overcome friction)
    [SerializeField] private float brakePower = 30000f;
    [SerializeField] private float reverseSpeed = 30f;
    [SerializeField] private float handbrakeDrag = 2f; // Drag multiplier when handbraking

    [Header("Steering Settings")]
    [SerializeField] private float turnSpeed = 150f; // Torque
    [SerializeField] private float maxSteeringAngle = 35f;
    [SerializeField] private float steeringInputSmoothness = 5f; // Faster response
    [SerializeField] private float driftFactor = 0.95f; // How much grip we lose when handbraking (0-1)
    [SerializeField] private float tractionControl = 3000f; // Lateral friction force

    [Header("Wheel Visual Settings")]
    [SerializeField] private float maxWheelVisualAngle = 30f;
    [SerializeField] private float wheelSteeringSmoothness = 10f;

    [Header("Physics Settings")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private float bodyTiltAmount = 5f;

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
    private bool handbrakeInput;
    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction handbrakeAction;
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

        rb.mass = 1000f; // Reduced mass for better handling
        rb.linearDamping = 0.05f; // Less air resistance for better coasting
        rb.angularDamping = 0.5f; // Prevent endless spinning
        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = false; // Ensure physics is enabled
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Prevent passing through objects at high speed

        // Freeze rotation on X and Z to prevent flipping, but allow Y for turning
        // We might want to allow some X/Z tilt later for suspension, but keeping it simple for now
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
                }
            }
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
                handbrakeAction = actionMap.FindAction("Jump"); // Using Jump (Space) as Handbrake

                if (moveAction != null)
                {
                    moveAction.Enable();
                    if (handbrakeAction != null) handbrakeAction.Enable();
                    return;
                }
            }
        }
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
            handbrakeInput = handbrakeAction != null && handbrakeAction.IsPressed();
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

                handbrakeInput = Keyboard.current.spaceKey.isPressed;
            }

            moveInput = new Vector2(horizontal, vertical);
        }
    }

    void FixedUpdate()
    {
        UpdateSpeed();
        ApplyPhysics();
    }

    void UpdateSpeed()
    {
        // Get forward speed in km/h roughly (magnitude of local Z velocity)
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        currentSpeed = localVelocity.z; // Use actual physics speed
    }

    private void ApplyPhysics()
    {
        float verticalInput = moveInput.y;
        float horizontalInput = moveInput.x;

        // 1. Acceleration / Braking
        if (verticalInput > 0.1f)
        {
            // Accelerate
            if (currentSpeed < maxSpeed)
            {
                rb.AddRelativeForce(Vector3.forward * verticalInput * accelerationPower);
            }
        }
        else if (verticalInput < -0.1f)
        {
            // Reverse or Brake
            if (currentSpeed > 1f)
            {
                // Moving forward, so this is braking
                rb.AddRelativeForce(Vector3.forward * verticalInput * brakePower);
            }
            else
            {
                // Moving reverse
                if (Mathf.Abs(currentSpeed) < reverseSpeed)
                {
                    rb.AddRelativeForce(Vector3.forward * verticalInput * accelerationPower);
                }
            }
        }

        // 2. Handbrake
        if (handbrakeInput)
        {
            // Apply strong drag to forward movement
            rb.linearDamping = handbrakeDrag;
        }
        else
        {
            rb.linearDamping = 0.05f;
        }

        // 3. Lateral Friction (Grip)
        // Calculate lateral velocity (sliding sideways)
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralSpeed = localVelocity.x;

        // Apply force opposite to lateral speed to simulate tire grip
        // If handbrake is on, reduce this force to allow sliding
        float currentTraction = tractionControl;
        if (handbrakeInput)
        {
            currentTraction *= (1f - driftFactor); // Reduce grip
        }

        // Apply the friction force
        // We cap the force to avoid instability
        Vector3 frictionForce = -transform.right * lateralSpeed * currentTraction;
        rb.AddForce(frictionForce);


        // 4. Steering
        // Steer angle accumulation
        float targetAngle = horizontalInput * maxSteeringAngle;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.fixedDeltaTime * steeringInputSmoothness);

        // Apply Rotation Torque
        // We only rotate if we are moving (or slipping)
        if (Mathf.Abs(currentSpeed) > 1f || handbrakeInput)
        {
            // If handbraking, we can rotate faster (drift entry)
            float turnMultiplier = handbrakeInput ? 2.5f : 1f;
            
            // Invert steering when reversing
            float direction = currentSpeed > 0 ? 1f : -1f; 
            
            // Add torque
            rb.AddTorque(Vector3.up * currentSteerAngle * turnSpeed * direction * turnMultiplier);
        }
    }

    private void AnimateWheels()
    {
        // Update wheel spin angle based on physics speed
        // Speed is m/s. Wheel circumference approx 2m? 
        // 360 deg per rotation. 
        wheelSpinAngle += currentSpeed * (360f / 2f) * Time.deltaTime; // Approximation

        // Keep angle in reasonable range
        if (wheelSpinAngle > 360f) wheelSpinAngle -= 360f;
        if (wheelSpinAngle < -360f) wheelSpinAngle += 360f;

        // Smooth visual steering angle
        float targetVisualAngle = moveInput.x * maxWheelVisualAngle;
        visualSteerAngle = Mathf.Lerp(visualSteerAngle, targetVisualAngle, Time.deltaTime * wheelSteeringSmoothness);

        // Apply rotation to front wheels (spin + steering)
        foreach (Transform wheel in frontWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                Quaternion initialRot = wheelInitialRotations[wheel];
                bool isLeftWheel = wheelIsOnLeft.ContainsKey(wheel) && wheelIsOnLeft[wheel];

                float spinDirection = isLeftWheel ? -wheelSpinAngle : wheelSpinAngle;
                Quaternion spinRotation = Quaternion.AngleAxis(spinDirection, Vector3.right);
                Quaternion steerRotation = Quaternion.AngleAxis(visualSteerAngle, Vector3.up);

                wheel.localRotation = initialRot * steerRotation * spinRotation;
            }
        }

        // Apply rotation to rear wheels
        foreach (Transform wheel in rearWheels)
        {
            if (wheel != null && wheelInitialRotations.ContainsKey(wheel))
            {
                Quaternion initialRot = wheelInitialRotations[wheel];
                bool isLeftWheel = wheelIsOnLeft.ContainsKey(wheel) && wheelIsOnLeft[wheel];

                // Stop rear wheels spinning if handbrake is on
                float effectiveSpin = handbrakeInput ? 0f : (isLeftWheel ? -wheelSpinAngle : wheelSpinAngle);
                
                Quaternion spinRotation = Quaternion.AngleAxis(effectiveSpin, Vector3.right);
                wheel.localRotation = initialRot * spinRotation;
            }
        }
    }

    private void AnimateBodyTilt()
    {
        if (bodyTransform == null) return;

        // Tilt body based on lateral velocity (physics drift) and steering
        // Calculate lateral G-force approximation
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralForce = localVelocity.x;

        float targetTilt = -lateralForce * bodyTiltAmount;
        Vector3 currentEuler = bodyTransform.localEulerAngles;
        float newZ = Mathf.LerpAngle(currentEuler.z, targetTilt, Time.deltaTime * 5f);
        bodyTransform.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, newZ);
    }

    void OnDisable()
    {
        if (moveAction != null) moveAction.Disable();
        if (handbrakeAction != null) handbrakeAction.Disable();
    }

    // Public getters
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentSteerAngle() => currentSteerAngle;
    public bool IsHandbraking() => handbrakeInput;

    // Debug visualization
    void OnDrawGizmos()
    {
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMassOffset), 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }
    }
}
