using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 10f;

    [Header("Input Settings")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    private float currentSpeed = 0f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("CarController: No Rigidbody found. Adding Rigidbody component.");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = 1000f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        float verticalInput = Input.GetAxis(verticalAxis);

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
        float horizontalInput = Input.GetAxis(horizontalAxis);

        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            float turn = horizontalInput * turnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
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
