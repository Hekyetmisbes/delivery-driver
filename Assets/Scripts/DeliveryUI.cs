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

    [Header("Distance Display")]
    [SerializeField] private float distanceDisplayMultiplier = 10f;

    [Header("Kenney Skin")]
    [SerializeField] private bool useKenneySkin = true;
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Sprite statusBackgroundSprite;
    [SerializeField] private Sprite distanceBackgroundSprite;
    [SerializeField] private Sprite neighborhoodBackgroundSprite;

    private DeliveryManager deliveryManager;
    private Transform playerTransform;
    private float nextPlayerResolveTime;

    private void Awake()
    {
        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(false);
        }
    }

    private void Start()
    {
        deliveryManager = FindFirstObjectByType<DeliveryManager>();
        ResolvePlayerTransform();
        ResolveSkinSprites();
        ApplyKenneySkin();

        // Hide panel initially
        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (deliveryManager == null)
        {
            return;
        }

        if (playerTransform == null && Time.time >= nextPlayerResolveTime)
        {
            nextPlayerResolveTime = Time.time + 1f;
            ResolvePlayerTransform();
        }

        if (playerTransform == null)
        {
            return;
        }

        UpdateUI();
    }

    private void ResolvePlayerTransform()
    {
        CarController car = FindFirstObjectByType<CarController>();
        if (car != null)
        {
            playerTransform = car.transform;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void UpdateUI()
    {
        bool hasObjective = deliveryManager.ShouldShowObjectiveUI;

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
            float d = distance * distanceDisplayMultiplier;
            distanceText.text = d >= 1000f
                ? $"Mesafe: {d / 1000f:F1}km"
                : $"Mesafe: {d:F0}m";
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
            float d = distance * distanceDisplayMultiplier;
            distanceText.text = d >= 1000f
                ? $"Mesafe: {d / 1000f:F1}km"
                : $"Mesafe: {d:F0}m";
        }
    }

    /// <summary>
    /// Reset UI after delivery
    /// </summary>
    public void OnDeliveryComplete()
    {
        if (deliveryPanel != null)
        {
            deliveryPanel.SetActive(false);
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

    private void ApplyKenneySkin()
    {
        if (!useKenneySkin || deliveryPanel == null)
        {
            return;
        }

        Image panelImage = deliveryPanel.GetComponent<Image>();
        if (panelImage != null && panelBackgroundSprite != null)
        {
            panelImage.sprite = panelBackgroundSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.97f);
        }

        ApplyTextBlockStyle(statusText, statusBackgroundSprite, new Color(0.94f, 0.96f, 1f, 1f));
        ApplyTextBlockStyle(distanceText, distanceBackgroundSprite, new Color(0.86f, 0.96f, 1f, 1f));
        ApplyTextBlockStyle(neighborhoodText, neighborhoodBackgroundSprite, new Color(0.9f, 0.95f, 1f, 1f));
    }

    private void ApplyTextBlockStyle(TextMeshProUGUI text, Sprite backgroundSprite, Color textColor)
    {
        if (text == null)
        {
            return;
        }

        text.color = textColor;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0.07f, 0.11f, 0.16f, 1f);
        text.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        RectTransform parentRect = text.transform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Image blockImage = parentRect.GetComponent<Image>();
        if (blockImage == null)
        {
            blockImage = parentRect.gameObject.AddComponent<Image>();
        }

        if (backgroundSprite != null)
        {
            blockImage.sprite = backgroundSprite;
            blockImage.type = Image.Type.Sliced;
            blockImage.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }

    private void ResolveSkinSprites()
    {
        if (!useKenneySkin)
        {
            return;
        }

        panelBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/panel_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png");

        statusBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");

        distanceBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_upgrade",
            "Assets/kenney_ui-pack/PNG/Blue/Default/button_rectangle_depth_flat.png");

        neighborhoodBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");
    }
}
