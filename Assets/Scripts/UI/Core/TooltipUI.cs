using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public class TooltipUI : MonoBehaviour
    {
        private static TooltipUI instance;
        private const string TooltipRootName = "TooltipRoot";

        private Canvas tooltipCanvas;
        private RectTransform tooltipRootRect;
        private GameObject tooltipPanel;
        private TextMeshProUGUI tooltipText;
        private CanvasGroup canvasGroup;
        private RectTransform panelRect;

        private float showTimer;
        private bool isShowing;
        private string pendingText;

        private const float HoverDelay = 0.3f;
        private const float FadeInDuration = 0.15f;
        private const float FadeOutDuration = 0.1f;
        private const float MaxWidth = 300f;
        private const float Padding = 12f;
        private static readonly Vector2 MouseOffset = new Vector2(16f, -16f);

        public static TooltipUI Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("TooltipUI", typeof(TooltipUI));
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildTooltip();
        }

        private void Update()
        {
            if (!isShowing && !string.IsNullOrEmpty(pendingText))
            {
                showTimer += Time.unscaledDeltaTime;
                if (showTimer >= HoverDelay)
                {
                    ShowImmediate(pendingText);
                }
            }

            if (isShowing && panelRect != null)
            {
                UpdatePosition();
            }
        }

        public static void RequestShow(string text)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;
            instance.pendingText = text;
            instance.showTimer = 0f;
        }

        public static void RequestHide()
        {
            if (instance == null) return;
            instance.pendingText = null;
            instance.showTimer = 0f;
            if (instance.isShowing)
            {
                instance.HideImmediate();
            }
        }

        private void ShowImmediate(string text)
        {
            if (tooltipPanel == null) BuildTooltip();

            tooltipText.text = text;
            tooltipPanel.SetActive(true);
            isShowing = true;

            // Fit content
            tooltipText.ForceMeshUpdate();
            Vector2 textSize = tooltipText.GetRenderedValues(true);
            float width = Mathf.Min(textSize.x + Padding * 2f, MaxWidth);
            float height = textSize.y + Padding * 2f;
            panelRect.sizeDelta = new Vector2(width, height);

            UIAnimationHelper.FadeIn(this, canvasGroup, FadeInDuration);
            UpdatePosition();
        }

        private void HideImmediate()
        {
            isShowing = false;
            if (canvasGroup != null)
            {
                UIAnimationHelper.FadeOut(this, canvasGroup, FadeOutDuration, () =>
                {
                    if (tooltipPanel != null) tooltipPanel.SetActive(false);
                });
            }
        }

        private void UpdatePosition()
        {
            Vector3 mousePos = Input.mousePosition;
            panelRect.position = mousePos + (Vector3)MouseOffset;
        }

        private void BuildTooltip()
        {
            Transform uiParent = GlobalUiCoordinator.CanvasGroupRoot ?? GlobalUiCoordinator.PrimaryCanvas?.transform;
            if (uiParent != null)
            {
                Transform existingRoot = uiParent.Find(TooltipRootName);
                if (existingRoot == null)
                {
                    GameObject rootObject = new GameObject(TooltipRootName, typeof(RectTransform));
                    rootObject.transform.SetParent(uiParent, false);
                    tooltipRootRect = rootObject.GetComponent<RectTransform>();
                    tooltipRootRect.anchorMin = Vector2.zero;
                    tooltipRootRect.anchorMax = Vector2.one;
                    tooltipRootRect.offsetMin = Vector2.zero;
                    tooltipRootRect.offsetMax = Vector2.zero;
                }
                else
                {
                    tooltipRootRect = existingRoot as RectTransform;
                }
            }
            else
            {
                GameObject canvasObj = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler));
                canvasObj.transform.SetParent(transform, false);

                tooltipCanvas = canvasObj.GetComponent<Canvas>();
                tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                tooltipCanvas.sortingOrder = 950;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                tooltipRootRect = canvasObj.GetComponent<RectTransform>();
            }

            // Panel
            tooltipPanel = new GameObject("TooltipPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            tooltipPanel.transform.SetParent(tooltipRootRect, false);

            panelRect = tooltipPanel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(200f, 40f);
            panelRect.SetAsLastSibling();

            Image bg = tooltipPanel.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            bg.raycastTarget = false;

            canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Text
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(tooltipPanel.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(Padding, Padding);
            textRect.offsetMax = new Vector2(-Padding, -Padding);

            tooltipText = textObj.GetComponent<TextMeshProUGUI>();
            tooltipText.fontSize = 14f;
            tooltipText.color = Color.white;
            tooltipText.alignment = TextAlignmentOptions.TopLeft;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null) tooltipText.font = TMP_Settings.defaultFontAsset;

            tooltipPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Attach to any UI element to show a tooltip on hover.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string tooltipText;

        public void SetText(string text)
        {
            tooltipText = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipUI.RequestShow(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.RequestHide();
        }

        private void OnDisable()
        {
            TooltipUI.RequestHide();
        }
    }
}
