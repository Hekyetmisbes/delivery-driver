using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime-safe phone style UI for showing one mission offer at a time.
/// </summary>
public class PhoneMissionUI : MonoBehaviour
{
    [Header("Optional Scene References")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    [Header("Runtime Build")]
    [SerializeField] private bool autoCreateRuntimeUi = true;
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 300f);
    [SerializeField] private Vector2 panelOffset = new Vector2(-24f, -24f);
    [SerializeField] private string acceptButtonLabel = "Kabul Et";
    [SerializeField] private string rejectButtonLabel = "Reddet";
    [SerializeField] private bool useKenneySkin = true;
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Sprite buttonBackgroundSprite;
    [SerializeField] private Sprite acceptIconSprite;
    [SerializeField] private Sprite rejectIconSprite;

    private Action onAccept;
    private Action onReject;

    private void Awake()
    {
        if (autoCreateRuntimeUi)
        {
            EnsureRuntimeUI();
        }

        WireButtons();
        HideOffer();
    }

    public void BindCallbacks(Action acceptAction, Action rejectAction)
    {
        onAccept = acceptAction;
        onReject = rejectAction;
        WireButtons();
    }

    public void ShowOffer(string title, string body, string reward)
    {
        EnsureRuntimeUI();

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Yeni Gorev Teklifi" : title;
        }

        if (bodyText != null)
        {
            bodyText.text = string.IsNullOrWhiteSpace(body)
                ? "Telefonuna yeni bir teslimat gorevi geldi."
                : body;
        }

        if (rewardText != null)
        {
            rewardText.text = string.IsNullOrWhiteSpace(reward) ? "Odul: -" : reward;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    public void HideOffer()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HandleAcceptClicked()
    {
        onAccept?.Invoke();
    }

    private void HandleRejectClicked()
    {
        onReject?.Invoke();
    }

    private void WireButtons()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
            acceptButton.onClick.AddListener(HandleAcceptClicked);
            SetButtonLabel(acceptButton, acceptButtonLabel);
        }

        if (rejectButton != null)
        {
            rejectButton.onClick.RemoveListener(HandleRejectClicked);
            rejectButton.onClick.AddListener(HandleRejectClicked);
            SetButtonLabel(rejectButton, rejectButtonLabel);
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = string.IsNullOrWhiteSpace(label) ? "Button" : label;
        }
    }

    private void EnsureRuntimeUI()
    {
        if (panelRoot != null && titleText != null && bodyText != null && rewardText != null && acceptButton != null && rejectButton != null)
        {
            return;
        }

        ResolveSkinSprites();
        EnsureEventSystem();

        if (rootCanvas == null)
        {
            rootCanvas = FindFirstObjectByType<Canvas>();
        }

        if (rootCanvas == null)
        {
            GameObject canvasObject = new GameObject("PhoneMissionCanvas");
            rootCanvas = canvasObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 120;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (panelRoot == null)
        {
            panelRoot = CreatePanel(rootCanvas.transform);
        }

        WireButtons();
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("PhoneMissionPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = panelOffset;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        panelImage.raycastTarget = true;
        if (panelBackgroundSprite != null)
        {
            panelImage.sprite = panelBackgroundSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 1f;
        }

        VerticalLayoutGroup vertical = panel.GetComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(18, 18, 18, 18);
        vertical.spacing = 10f;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        titleText = CreateText(panel.transform, "OfferTitle", 26, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, 52f);
        bodyText = CreateText(panel.transform, "OfferBody", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.9f, 0.94f, 1f, 1f), 130f);
        rewardText = CreateText(panel.transform, "OfferReward", 22, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.96f, 0.86f, 0.25f, 1f), 40f);

        GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonRow.transform.SetParent(panel.transform, false);

        HorizontalLayoutGroup horizontal = buttonRow.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 12f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;

        LayoutElement rowLayout = buttonRow.GetComponent<LayoutElement>();
        rowLayout.minHeight = 58f;
        rowLayout.flexibleHeight = 0f;

        acceptButton = CreateButton(buttonRow.transform, "AcceptButton", new Color(0.16f, 0.62f, 0.3f, 1f), acceptButtonLabel, acceptIconSprite);
        rejectButton = CreateButton(buttonRow.transform, "RejectButton", new Color(0.68f, 0.22f, 0.2f, 1f), rejectButtonLabel, rejectIconSprite);

        return panel;
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color,
        float preferredHeight)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = string.Empty;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.minHeight = preferredHeight;
        layout.flexibleHeight = 0f;

        return text;
    }

    private Button CreateButton(Transform parent, string objectName, Color color, string label, Sprite iconSprite)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = color;
        if (buttonBackgroundSprite != null)
        {
            buttonImage.sprite = buttonBackgroundSprite;
            buttonImage.type = Image.Type.Sliced;
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
        button.colors = colors;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minHeight = 50f;
        layout.flexibleWidth = 1f;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        if (iconSprite != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            iconRect.sizeDelta = new Vector2(22f, 22f);

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        return button;
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

        buttonBackgroundSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/button_bg",
            "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");

        acceptIconSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/icon_accept",
            "Assets/kenney_ui-pack/PNG/Green/Default/icon_checkmark.png");

        rejectIconSprite ??= RuntimeUiSkinLoader.LoadSprite(
            "UI/Kenney/icon_reject",
            "Assets/kenney_ui-pack/PNG/Red/Default/icon_cross.png");
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(null, false);
    }
}
