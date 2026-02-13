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
    [SerializeField] private TextMeshProUGUI neighborhoodText;

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
        bool hasObjective = deliveryManager.HasObjectivePoint;

        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(hasObjective);
        }

        if (!hasObjective)
        {
            return;
        }

        Vector3 targetPoint = deliveryManager.CurrentObjectivePoint;
        float distance = Vector3.Distance(playerTransform.position, targetPoint);
        string pickupNeighborhood = GetNeighborhoodLabel(deliveryManager.CurrentPickupNeighborhoodName);
        string neighborhoodSummary = deliveryManager.IsDeliveryActive
            ? $"Mahalle: {pickupNeighborhood} -> {GetNeighborhoodLabel(deliveryManager.CurrentDeliveryNeighborhoodName)}"
            : $"Alim Mahallesi: {pickupNeighborhood}";

        if (statusText != null)
        {
            string baseStatus = deliveryManager.IsDeliveryActive
                ? "Paketi teslim et!"
                : "Paketi bul!";
            statusText.text = neighborhoodText == null
                ? $"{baseStatus}\n{neighborhoodSummary}"
                : baseStatus;
        }

        if (distanceText != null)
        {
            distanceText.text = $"Mesafe: {distance:F0}m";
        }

        if (neighborhoodText != null)
        {
            neighborhoodText.text = neighborhoodSummary;
        }
    }

    /// <summary>
    /// Update UI when box is picked up
    /// </summary>
    public void OnBoxPickedUp(Vector3 deliveryPoint, string pickupNeighborhood = "", string deliveryNeighborhood = "")
    {
        if (statusText != null)
        {
            statusText.text = "Paketi teslim et!";
        }

        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(true);
        }

        if (neighborhoodText != null)
        {
            neighborhoodText.text = $"Mahalle: {GetNeighborhoodLabel(pickupNeighborhood)} -> {GetNeighborhoodLabel(deliveryNeighborhood)}";
        }
    }

    /// <summary>
    /// Update distance display
    /// </summary>
    public void UpdateDistance(float distance)
    {
        if (distanceText != null)
        {
            distanceText.text = $"Mesafe: {distance:F0}m";
        }
    }

    /// <summary>
    /// Reset UI after delivery
    /// </summary>
    public void OnDeliveryComplete()
    {
        if (statusText != null)
        {
            statusText.text = "Teslimat tamamlandi!";
        }

        if (neighborhoodText != null)
        {
            neighborhoodText.text = "";
        }

        Invoke(nameof(ResetUI), 2f);
    }

    private void ResetUI()
    {
        if (statusText != null)
        {
            statusText.text = "Paketi bul!";
        }

        if (distanceText != null)
        {
            distanceText.text = "";
        }

        if (neighborhoodText != null)
        {
            neighborhoodText.text = "";
        }
    }

    private string GetNeighborhoodLabel(string neighborhoodName)
    {
        return string.IsNullOrWhiteSpace(neighborhoodName) ? "Bilinmiyor" : neighborhoodName;
    }
}
