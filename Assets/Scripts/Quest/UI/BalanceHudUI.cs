using System.Collections;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    public class BalanceHudUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private bool showBalance = true;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
        [SerializeField] private Vector2 panelSize = new Vector2(320f, 64f);

        [Header("Style")]
        [SerializeField] private Color panelColor = new Color(0.05f, 0.1f, 0.18f, 0.82f);
        [SerializeField] private Color textColor = new Color(0.3f, 0.95f, 0.45f, 1f);
        [SerializeField] private int fontSize = 34;

        private const string HudCanvasName = "GameplayHUDCanvas";
        private const string BalancePanelName = "BalanceHudPanel";

        private TextMeshProUGUI balanceText;
        private PlayerProgressionManager subscribedManager;
        private int lastKnownBalance;
        private RectTransform balancePanelRect;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureHud();
            TrySubscribeToProgression();
            RefreshBalanceText();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromProgression();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (subscribedManager == null)
            {
                TrySubscribeToProgression();
                RefreshBalanceText();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureHud();
            RefreshBalanceText();
        }

        private void EnsureHud()
        {
            if (!showBalance)
            {
                if (balanceText != null)
                {
                    balanceText.transform.parent.gameObject.SetActive(false);
                }
                return;
            }

            Canvas canvas = GetOrCreateHudCanvas();
            if (canvas == null)
            {
                return;
            }

            Transform existingPanel = canvas.transform.Find(BalancePanelName);
            if (existingPanel != null)
            {
                RectTransform existingRect = existingPanel as RectTransform;
                if (existingRect != null)
                {
                    ConfigurePanelRect(existingRect);
                }

                balanceText = existingPanel.GetComponentInChildren<TextMeshProUGUI>();
                if (balanceText != null)
                {
                    ApplyTextStyle(balanceText);
                }

                existingPanel.gameObject.SetActive(true);
                return;
            }

            GameObject panelObject = new GameObject(BalancePanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            ConfigurePanelRect(panelRect);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            panelImage.sprite = DeliveryUiSpriteHelper.GetFallbackSprite();
            panelImage.type = Image.Type.Simple;

            GameObject textObject = new GameObject("BalanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);

            balanceText = textObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(balanceText);
            balancePanelRect = panelRect;
        }

        private void ConfigurePanelRect(RectTransform panelRect)
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = panelSize;
        }

        private void ApplyTextStyle(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = Mathf.Max(18, fontSize);
            text.fontStyle = FontStyles.Bold;
            text.color = textColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        }

        private void TrySubscribeToProgression()
        {
            PlayerProgressionManager manager = PlayerProgressionManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (subscribedManager == manager)
            {
                return;
            }

            UnsubscribeFromProgression();
            subscribedManager = manager;
            subscribedManager.OnMoneyChanged.AddListener(OnMoneyChanged);
        }

        private void UnsubscribeFromProgression()
        {
            if (subscribedManager == null)
            {
                return;
            }

            subscribedManager.OnMoneyChanged.RemoveListener(OnMoneyChanged);
            subscribedManager = null;
        }

        private void OnMoneyChanged(int newAmount)
        {
            int delta = newAmount - lastKnownBalance;
            lastKnownBalance = newAmount;
            RefreshBalanceText(newAmount);

            if (delta != 0 && Application.isPlaying)
            {
                ShowBalanceDelta(delta);
                if (balancePanelRect != null)
                {
                    UIAnimationHelper.PulseScale(this, balancePanelRect, 1.08f, UIThemeConstants.PulseDuration);
                }
            }
        }

        private void ShowBalanceDelta(int delta)
        {
            if (balanceText == null) return;

            Canvas canvas = balanceText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform panelTransform = balanceText.transform.parent;
            if (panelTransform == null) return;

            GameObject deltaObj = new GameObject("BalanceDelta", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            deltaObj.transform.SetParent(panelTransform, false);
            RectTransform deltaRect = deltaObj.GetComponent<RectTransform>();
            deltaRect.anchorMin = new Vector2(1f, 0.5f);
            deltaRect.anchorMax = new Vector2(1f, 0.5f);
            deltaRect.pivot = new Vector2(0f, 0.5f);
            deltaRect.anchoredPosition = new Vector2(12f, 0f);
            deltaRect.sizeDelta = new Vector2(120f, 40f);

            TextMeshProUGUI deltaText = deltaObj.GetComponent<TextMeshProUGUI>();
            deltaText.text = delta > 0 ? $"+${delta}" : $"-${Mathf.Abs(delta)}";
            deltaText.fontSize = 26f;
            deltaText.fontStyle = FontStyles.Bold;
            deltaText.color = delta > 0 ? UIThemeConstants.Positive : UIThemeConstants.Negative;
            deltaText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                deltaText.font = TMP_Settings.defaultFontAsset;
            }

            CanvasGroup cg = deltaObj.GetComponent<CanvasGroup>();
            StartCoroutine(AnimateBalanceDelta(deltaRect, cg, deltaObj));
        }

        private IEnumerator AnimateBalanceDelta(RectTransform rect, CanvasGroup cg, GameObject obj)
        {
            float duration = 1.2f;
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = startPos + new Vector2(0f, t * 30f);
                cg.alpha = 1f - UIAnimationHelper.EaseOutCubic(t);
                yield return null;
            }

            Destroy(obj);
        }

        private void RefreshBalanceText()
        {
            int amount = PlayerProgressionManager.Instance != null ? PlayerProgressionManager.Instance.CurrentMoney : 0;
            RefreshBalanceText(amount);
        }

        private void RefreshBalanceText(int amount)
        {
            if (balanceText == null)
            {
                return;
            }

            balanceText.text = $"{LocalizationTable.Get("balance_label")}: ${amount:N0}";
        }

        private Canvas GetOrCreateHudCanvas()
        {
            GameObject existing = GameObject.Find(HudCanvasName);
            if (existing != null)
            {
                Canvas existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    EnsureHudCanvasSettings(existingCanvas.gameObject, existingCanvas);
                    return existingCanvas;
                }
            }

            GameObject canvasObject = new GameObject(HudCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            EnsureHudCanvasSettings(canvasObject, canvas);
            return canvas;
        }

        private static void EnsureHudCanvasSettings(GameObject canvasObject, Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }
    }
}
