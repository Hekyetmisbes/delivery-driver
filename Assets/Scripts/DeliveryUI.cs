using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI display for delivery mission info
/// </summary>
public class DeliveryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deliveryPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI distanceText;

    private DeliveryManager deliveryManager;
    private Transform playerTransform;

    private void Start()
    {
        deliveryManager = FindFirstObjectByType<DeliveryManager>();

        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) playerObj = car.gameObject;
        }
        if (playerObj != null) playerTransform = playerObj.transform;

        // Hide panel initially
        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (deliveryManager == null || playerTransform == null) return;

        UpdateUI();
    }

    private void UpdateUI()
    {
        // Show/hide panel based on delivery status
        bool hasActiveDelivery = deliveryManager != null; // You'll need to expose isDeliveryActive in DeliveryManager

        if (deliveryPanel != null)
        {
            // For now, always show if delivery manager exists
            deliveryPanel.SetActive(true);
        }

        // Update status text
        if (statusText != null)
        {
            statusText.text = "Find the delivery box!";
        }

        // Update distance and direction (this would need delivery manager to expose target position)
        // For now, just show placeholder
        if (distanceText != null)
        {
            distanceText.text = "Distance: --m";
        }
    }

    /// <summary>
    /// Update UI when box is picked up
    /// </summary>
    public void OnBoxPickedUp(Vector3 deliveryPoint)
    {
        if (statusText != null)
        {
            statusText.text = "Deliver the box!";
        }

        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Update distance display
    /// </summary>
    public void UpdateDistance(float distance)
    {
        if (distanceText != null)
        {
            distanceText.text = $"Distance: {distance:F0}m";
        }
    }

    /// <summary>
    /// Reset UI after delivery
    /// </summary>
    public void OnDeliveryComplete()
    {
        if (statusText != null)
        {
            statusText.text = "Delivery Complete!";
        }

        Invoke(nameof(ResetUI), 2f);
    }

    private void ResetUI()
    {
        if (statusText != null)
        {
            statusText.text = "Find the delivery box!";
        }

        if (distanceText != null)
        {
            distanceText.text = "";
        }
    }
}
