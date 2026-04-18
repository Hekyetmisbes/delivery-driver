using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeliveryDriver.UI;

public class CreditsSceneRuntimeUI : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string developerName = "hekye";
    [SerializeField] private string backgroundResourcesPath = "UI/MainMenu/mainmenu_bg";
    [SerializeField] private string backgroundAssetPath = "Assets/Images/MainMenuImage.png";

    private void Start()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();

        CreateBackground(canvas.transform);

        GameObject panelObject = new GameObject("CreditsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 760f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);

        VerticalLayoutGroup panelLayout = panelObject.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 24, 24);
        panelLayout.spacing = 16f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateHeader(panelObject.transform, LocalizationTable.Get("credits_title"));
        CreateCreditsText(panelObject.transform);

        Button backButton = CreateButton(panelObject.transform, LocalizationTable.Get("credits_back_to_menu"), new Color(0.12f, 0.55f, 0.83f, 0.95f));
        backButton.onClick.AddListener(() => SceneManager.LoadScene(mainMenuSceneName));
    }

    private void CreateBackground(Transform parent)
    {
        GameObject backgroundObject = new GameObject("CreditsBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = backgroundObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.5f);
        image.sprite = RuntimeUiSkinLoader.LoadSprite(backgroundResourcesPath, backgroundAssetPath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    private void CreateHeader(Transform parent, string title)
    {
        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        headerObject.transform.SetParent(parent, false);

        LayoutElement layout = headerObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 70f;

        TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
        text.text = title;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 54f;
        text.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void CreateCreditsText(Transform parent)
    {
        GameObject scrollObject = new GameObject("CreditsScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect), typeof(LayoutElement));
        scrollObject.transform.SetParent(parent, false);

        LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
        scrollLayout.preferredHeight = 560f;

        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.35f);

        Mask mask = scrollObject.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(scrollObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(24f, 0f);
        contentRect.offsetMax = new Vector2(-24f, 0f);

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI credits = contentObject.GetComponent<TextMeshProUGUI>();
        credits.text = BuildCreditsText();
        credits.alignment = TextAlignmentOptions.TopLeft;
        credits.color = Color.white;
        credits.fontSize = 30f;
        credits.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset != null)
        {
            credits.font = TMP_Settings.defaultFontAsset;
        }

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = scrollObject.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
    }

    private string BuildCreditsText()
    {
        return
            LocalizationTable.Get("credits_game_by") + ": " + developerName + "\n\n" +
            LocalizationTable.Get("credits_used_assets") + ":\n" +
            "- Main Menu Artwork (Assets/Images/MainMenuImage.png)\n" +
            "- SimplePoly City - Low Poly Assets\n" +
            "- Nebula - Free low poly car pack\n" +
            "- EasyRoads3D\n" +
            "- Cardboard Box (Rigged)\n" +
            "- Kenney UI Pack\n" +
            "- HQP Studios Low Poly 3D Icons - Pack Lite\n" +
            "- Keypad\n" +
            "- TextMesh Pro\n\n" +
            LocalizationTable.Get("credits_thanks");
    }

    private Button CreateButton(Transform parent, string label, Color color)
    {
        GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 320f;
        layoutElement.preferredHeight = 68f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.GetComponent<Button>();

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.fontSize = 36f;
        labelText.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
        {
            labelText.font = TMP_Settings.defaultFontAsset;
        }

        return button;
    }

    private static Canvas EnsureCanvas()
    {
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject("CreditsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
