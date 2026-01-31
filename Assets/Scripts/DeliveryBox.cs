using UnityEngine;

/// <summary>
/// Delivery box that can be picked up by the player
/// </summary>
public class DeliveryBox : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject pickupIndicator;
    [SerializeField] private float indicatorRotationSpeed = 50f;

    private bool isPickedUp = false;
    private Transform playerTransform;

    public bool IsPickedUp => isPickedUp;

    private void Update()
    {
        // Rotate indicator
        if (pickupIndicator != null && !isPickedUp)
        {
            pickupIndicator.transform.Rotate(Vector3.up, indicatorRotationSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;

        // Check if player picked up the box
        if (other.CompareTag("Player") || other.GetComponent<CarController>() != null)
        {
            PickupBox(other.transform);
        }
    }

    private void PickupBox(Transform player)
    {
        isPickedUp = true;
        playerTransform = player;

        // Hide indicator
        if (pickupIndicator != null)
        {
            pickupIndicator.SetActive(false);
        }

        // Notify delivery manager
        DeliveryManager deliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (deliveryManager != null)
        {
            deliveryManager.OnBoxPickedUp(this);
        }

        Debug.Log("[DeliveryBox] Box picked up by player!");
    }

    /// <summary>
    /// Deliver the box at target location
    /// </summary>
    public void DeliverBox()
    {
        if (!isPickedUp) return;

        // Notify delivery manager
        DeliveryManager deliveryManager = FindFirstObjectByType<DeliveryManager>();
        if (deliveryManager != null)
        {
            deliveryManager.OnBoxDelivered(this);
        }

        // Destroy box
        Destroy(gameObject);
        Debug.Log("[DeliveryBox] Box delivered!");
    }
}
