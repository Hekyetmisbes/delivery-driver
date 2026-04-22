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

        private const string BalancePanelName = "BalanceHudPanel";
        private TextMeshProUGUI balanceText;
        private PlayerProgressionManager subscribedManager;
        private int lastKnownBalance;
        private RectTransform balancePanelRect;
        private Coroutine bindingRetryCoroutine;
        private bool loggedMissingManagerWarning;
        private bool loggedMissingTextWarning;
        private bool loggedMissingUiRootWarning;
        private static Sprite panelSprite;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureHud();
            TrySubscribeToProgression(false);
            StartBindingRetryIfNeeded();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopBindingRetry();
            UnsubscribeFromProgression();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureHud();
            TrySubscribeToProgression(false);
            RefreshBalanceText();

            StartBindingRetryIfNeeded();
        }

        private IEnumerator RetryBindProgressionRoutine()
        {
            WaitForSeconds retryDelay = new WaitForSeconds(0.25f);
            while (Application.isPlaying && subscribedManager == null)
            {
                if (!ShouldAttemptProgressionBinding())
                {
                    bindingRetryCoroutine = null;
                    yield break;
                }

                EnsureHud();
                TrySubscribeToProgression(false);
                if (subscribedManager != null)
                {
                    bindingRetryCoroutine = null;
                    yield break;
                }

                yield return retryDelay;
            }

            bindingRetryCoroutine = null;
        }

        private void EnsureHud()
        {
            if (!ShouldDisplayHud())
            {
                SetHudVisible(false);
                return;
            }

            Transform hudParent = ResolveHudParent();
            if (hudParent == null)
            {
                if (!loggedMissingUiRootWarning)
                {
                    Debug.LogError("[BalanceHudUI] Global UI root bulunamadi. Balance HUD authoritative UI parent'ina baglanamadi.");
                    loggedMissingUiRootWarning = true;
                }

                SetHudVisible(false);
                return;
            }

            loggedMissingUiRootWarning = false;
            Transform existingPanel = hudParent.Find(BalancePanelName);
            if (existingPanel != null)
            {
                RectTransform existingRect = existingPanel as RectTransform;
                if (existingRect != null)
                {
                    ConfigurePanelRect(existingRect);
                }

                balanceText = EnsureBalanceText(existingPanel, existingRect);

                existingPanel.gameObject.SetActive(true);
                balancePanelRect = existingRect;
                return;
            }

            GameObject panelObject = new GameObject(BalancePanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(hudParent, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            ConfigurePanelRect(panelRect);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            panelImage.sprite = GetPanelSprite();
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
            loggedMissingTextWarning = false;
        }

        private bool TryEnsureHudBindings()
        {
            if (balancePanelRect == null)
            {
                Transform hudParent = ResolveHudParent();
                Transform existingPanel = hudParent != null ? hudParent.Find(BalancePanelName) : null;
                if (existingPanel is RectTransform existingRect)
                {
                    balancePanelRect = existingRect;
                }
            }

            if (balanceText == null && balancePanelRect != null)
            {
                balanceText = EnsureBalanceText(balancePanelRect, balancePanelRect);
            }

            if (balanceText == null)
            {
                EnsureHud();
            }

            if (balanceText == null && balancePanelRect != null)
            {
                balanceText = EnsureBalanceText(balancePanelRect, balancePanelRect);
            }

            return balanceText != null;
        }

        private TextMeshProUGUI EnsureBalanceText(Transform panelTransform, RectTransform panelRect)
        {
            if (panelTransform == null)
            {
                return null;
            }

            TextMeshProUGUI text = panelTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                ApplyTextStyle(text);
                loggedMissingTextWarning = false;
                return text;
            }

            if (!loggedMissingTextWarning)
            {
                Debug.LogError("[BalanceHudUI] Existing balance panel text binding eksikti. Runtime authoritative text child yeniden olusturuluyor.");
                loggedMissingTextWarning = true;
            }

            GameObject textObject = new GameObject("BalanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelTransform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);

            text = textObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(text);
            if (panelRect != null)
            {
                balancePanelRect = panelRect;
            }

            return text;
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

        private void TrySubscribeToProgression(bool logIfMissing)
        {
            PlayerProgressionManager manager = ResolveProgressionManager();
            if (manager == null)
            {
                if (logIfMissing && ShouldAttemptProgressionBinding() && !loggedMissingManagerWarning)
                {
                    Debug.LogWarning("[BalanceHudUI] PlayerProgressionManager authoritative source henuz hazir degil.");
                    loggedMissingManagerWarning = true;
                }

                SetHudVisible(false);
                return;
            }

            if (subscribedManager == manager)
            {
                if (ShouldDisplayHud())
                {
                    EnsureHud();
                    SetHudVisible(true);
                    RefreshBalanceText(subscribedManager.CurrentMoney);
                }
                else
                {
                    SetHudVisible(false);
                }

                return;
            }

            UnsubscribeFromProgression();
            subscribedManager = manager;
            lastKnownBalance = subscribedManager.CurrentMoney;
            subscribedManager.OnMoneyChanged.AddListener(OnMoneyChanged);
            loggedMissingManagerWarning = false;
            if (ShouldDisplayHud())
            {
                EnsureHud();
                SetHudVisible(true);
                RefreshBalanceText(subscribedManager.CurrentMoney);
            }
            else
            {
                SetHudVisible(false);
            }
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
            if (!ShouldDisplayHud())
            {
                lastKnownBalance = newAmount;
                SetHudVisible(false);
                return;
            }

            EnsureHud();
            TryEnsureHudBindings();
            SetHudVisible(true);
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
            if (subscribedManager == null)
            {
                SetHudVisible(false);
                return;
            }

            if (!ShouldDisplayHud())
            {
                SetHudVisible(false);
                return;
            }

            int amount = subscribedManager.CurrentMoney;
            lastKnownBalance = amount;
            RefreshBalanceText(amount);
        }

        private void RefreshBalanceText(int amount)
        {
            if (!ShouldDisplayHud())
            {
                SetHudVisible(false);
                return;
            }

            if (!TryEnsureHudBindings())
            {
                if (!loggedMissingTextWarning)
                {
                    Debug.LogError("[BalanceHudUI] Cannot render balance because the text component is missing.");
                    loggedMissingTextWarning = true;
                }

                return;
            }

            loggedMissingTextWarning = false;
            balanceText.text = $"{LocalizationTable.Get("balance_label")}: ${amount:N0}";
        }

        private bool ShouldDisplayHud()
        {
            return showBalance && !IsMainMenuScene();
        }

        private static bool IsMainMenuScene()
        {
            return SceneManager.GetActiveScene().name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase);
        }

        private static Transform ResolveHudParent()
        {
            Transform canvasGroupRoot = GlobalUiCoordinator.CanvasGroupRoot;
            if (canvasGroupRoot != null)
            {
                return canvasGroupRoot;
            }

            Canvas primaryCanvas = GlobalUiCoordinator.PrimaryCanvas;
            return primaryCanvas != null ? primaryCanvas.transform : null;
        }

        private void StartBindingRetryIfNeeded()
        {
            if (!Application.isPlaying || subscribedManager != null || bindingRetryCoroutine != null || !ShouldAttemptProgressionBinding())
            {
                return;
            }

            bindingRetryCoroutine = StartCoroutine(RetryBindProgressionRoutine());
        }

        private void StopBindingRetry()
        {
            if (bindingRetryCoroutine == null)
            {
                return;
            }

            StopCoroutine(bindingRetryCoroutine);
            bindingRetryCoroutine = null;
        }

        private void SetHudVisible(bool visible)
        {
            if (balancePanelRect != null && balancePanelRect.gameObject.activeSelf != visible)
            {
                balancePanelRect.gameObject.SetActive(visible);
            }
        }

        private static PlayerProgressionManager ResolveProgressionManager()
        {
            return PlayerProgressionManager.Instance ?? FindFirstObjectByType<PlayerProgressionManager>();
        }

        private bool ShouldAttemptProgressionBinding()
        {
            return showBalance && !IsMainMenuScene();
        }

        private static Sprite GetPanelSprite()
        {
            if (panelSprite != null)
            {
                return panelSprite;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, true);
            texture.name = "BalanceHudPanelSprite";
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply(false, true);

            panelSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            panelSprite.name = "BalanceHudPanelSprite";
            return panelSprite;
        }
    }
}
