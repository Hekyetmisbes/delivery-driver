using UnityEngine;
using UnityEngine.InputSystem;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float turnSpeed = 200f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;

    [Header("Input Settings (Optional)")]
    [SerializeField] private InputActionAsset inputActions;

    private float currentSpeed = 0f;
    private Vector2 moveInput;
    private Rigidbody rb;
    private InputAction moveAction;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("CarController: No Rigidbody found. Adding Rigidbody component.");
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
        }

        SetupInput();
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
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        float verticalInput = moveInput.y;

        if (verticalInput != 0)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, verticalInput * moveSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.fixedDeltaTime);
        }

        Vector3 movement = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    private void HandleRotation()
    {
        float horizontalInput = moveInput.x;

        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            float turn = horizontalInput * turnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }
    }

    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    public void SetTurnSpeed(float speed)
    {
        turnSpeed = speed;
    }
}
