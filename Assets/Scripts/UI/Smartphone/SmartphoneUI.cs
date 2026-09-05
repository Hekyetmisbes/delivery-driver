using System;
using System.Collections;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using DeliveryDriver.Company;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.UI.Smartphone
{
    public enum SmartphoneAppType
    {
        Home,
        Delivery,
        Bank,
        Garage,
        Messages
    }

    /// <summary>
    /// Modern Akıllı Kurye Telefonu (Smartphone UI).
    /// Eski rahatsız edici pop-up'ların yerine geçen, ekranın sağından kayarak açılan,
    /// içerisinde Kurye Express, QuickPay Banka, Garajım ve Müşteri Mesajları uygulamaları
    /// barındıran merkezi kurye akıllı telefonu.
    /// </summary>
    public class SmartphoneUI : MonoBehaviour
    {
        public static SmartphoneUI Instance { get; private set; }

        private const string GameSceneName = "Game";
        private const string AltGameSceneName = "GameScene";

        [Header("Phone Dimensions")]
        [SerializeField] private Vector2 phoneSize = new Vector2(350f, 630f);
        [SerializeField] private Vector2 openPosition = new Vector2(-28f, 24f);
        [SerializeField] private Vector2 closedPosition = new Vector2(-28f, -700f);

        // State
        private bool isOpen = false;
        private SmartphoneAppType currentApp = SmartphoneAppType.Home;
        private int unreadNotifications = 0;
        private float clockTimer = 0f;

        // UI Roots
        private Canvas targetCanvas;
        private RectTransform phoneRootRect;
        private CanvasGroup phoneCanvasGroup;
        private GameObject contentAreaObj;
        private Sprite fallbackSprite;

        // Status Bar
        private TextMeshProUGUI timeText;
        private GameObject backButtonObj;

        // Home Screen
        private GameObject homeScreenObj;
        private TextMeshProUGUI homeBankSubtitle;
        private TextMeshProUGUI homeGarageSubtitle;
        private TextMeshProUGUI homeMessagesSubtitle;
        private GameObject deliveryBadgeObj;
        private TextMeshProUGUI deliveryBadgeText;

        // Sub-Apps
        private SmartphoneDeliveryApp deliveryApp;
        private SmartphoneBankApp bankApp;
        private SmartphoneGarageApp garageApp;
        private SmartphoneMessagesApp messagesApp;

        // HUD Elements
        [SerializeField] private bool showHudButton = false;
        private RectTransform hudButtonRect;
        private GameObject hudBadgeObj;
        private TextMeshProUGUI hudBadgeText;
        private SmartphoneNotificationToast notificationToast;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            fallbackSprite = DeliveryUiSpriteHelper.GetFallbackSprite();
        }

        private void Start()
        {
            EnsureUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameSceneActive())
            {
                EnsureUI();
                ClosePhoneImmediate();
            }
            else
            {
                if (phoneRootRect != null) phoneRootRect.gameObject.SetActive(false);
                if (hudButtonRect != null) hudButtonRect.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsGameSceneActive()) return;

            // Handle Input (Tab or T to toggle phone)
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.tKey.wasPressedThisFrame)
                {
                    togglePressed = true;
                }
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!togglePressed && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.T)))
            {
                togglePressed = true;
            }
#endif
#else
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.T))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed)
            {
                TogglePhone();
            }

            // Update clock every second
            clockTimer += Time.unscaledDeltaTime;
            if (clockTimer >= 1f)
            {
                clockTimer = 0f;
                UpdateClock();
            }
        }

        public void EnsureUI()
        {
            if (phoneRootRect != null) return;
            if (!IsGameSceneActive()) return;

            targetCanvas = ResolveCanvas();
            if (targetCanvas == null) return;

            if (showHudButton)
            {
                BuildHUDButton(targetCanvas.transform);
            }
            BuildNotificationToast(targetCanvas.transform);
            BuildPhoneContainer(targetCanvas.transform);
            BuildHomeScreen(contentAreaObj.transform);
            BuildSubApps(contentAreaObj.transform);

            OpenApp(SmartphoneAppType.Home);
            ClosePhoneImmediate();
            RefreshBadges();
        }

        public void ReceiveMissionOffer(string title, string body, string reward, Action onAccept, Action onReject)
        {
            EnsureUI();

            if (deliveryApp != null)
            {
                deliveryApp.SetOffer(title, body, reward, onAccept, onReject);
            }

            unreadNotifications = 1;
            RefreshBadges();

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlaySwitch();
            }

            if (notificationToast != null)
            {
                string displayTitle = string.IsNullOrWhiteSpace(title) ? "Yeni Kargo Teklifi" : title;
                string displaySub = $"{reward} • Açmak için [TAB] tuşuna bas";
                notificationToast.Show(displayTitle, displaySub, () =>
                {
                    OpenPhone();
                    OpenApp(SmartphoneAppType.Delivery);
                }, 6f);
            }
        }

        public void TriggerNotification(string title, string body, Action onClick = null)
        {
            EnsureUI();

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlaySwitch();
            }

            if (notificationToast != null)
            {
                Action action = onClick ?? (() =>
                {
                    OpenPhone();
                    OpenApp(SmartphoneAppType.Messages);
                });
                notificationToast.Show(title, body, action, 5f);
            }
        }

        public void ClearOffer()
        {
            if (deliveryApp != null)
            {
                deliveryApp.ClearOffer();
            }

            unreadNotifications = 0;
            RefreshBadges();

            if (notificationToast != null)
            {
                notificationToast.Hide();
            }
        }

        public void TogglePhone()
        {
            if (isOpen)
            {
                ClosePhone();
            }
            else
            {
                OpenPhone();
            }
        }

        public void OpenPhone()
        {
            EnsureUI();
            if (phoneRootRect == null) return;

            isOpen = true;
            phoneRootRect.gameObject.SetActive(true);
            phoneCanvasGroup.blocksRaycasts = true;
            phoneCanvasGroup.interactable = true;

            StopAllCoroutines();
            UIAnimationHelper.SlideIn(this, phoneRootRect, phoneRootRect.anchoredPosition, openPosition, 0.28f);
            UIAnimationHelper.FadeIn(this, phoneCanvasGroup, 0.24f);

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlaySwitch();
            }

            // If we have an offer waiting and user opened phone, navigate directly to delivery app
            if (deliveryApp != null && deliveryApp.HasPendingOffer && currentApp == SmartphoneAppType.Home)
            {
                OpenApp(SmartphoneAppType.Delivery);
            }
            else
            {
                RefreshCurrentApp();
            }

            UpdateHomeSubtitles();
        }

        public void ClosePhone()
        {
            if (phoneRootRect == null) return;

            isOpen = false;
            phoneCanvasGroup.blocksRaycasts = false;
            phoneCanvasGroup.interactable = false;

            StopAllCoroutines();
            UIAnimationHelper.SlideOut(this, phoneRootRect, phoneRootRect.anchoredPosition, closedPosition, 0.22f);
            UIAnimationHelper.FadeOut(this, phoneCanvasGroup, 0.22f, () =>
            {
                if (phoneRootRect != null) phoneRootRect.gameObject.SetActive(false);
            });

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayClick();
            }
        }

        private void ClosePhoneImmediate()
        {
            if (phoneRootRect == null) return;

            isOpen = false;
            phoneRootRect.anchoredPosition = closedPosition;
            if (phoneCanvasGroup != null)
            {
                phoneCanvasGroup.alpha = 0f;
                phoneCanvasGroup.blocksRaycasts = false;
                phoneCanvasGroup.interactable = false;
            }
            phoneRootRect.gameObject.SetActive(false);
        }

        public void OpenApp(SmartphoneAppType app)
        {
            currentApp = app;

            if (homeScreenObj != null) homeScreenObj.SetActive(app == SmartphoneAppType.Home);
            if (deliveryApp != null) deliveryApp.SetActive(app == SmartphoneAppType.Delivery);
            if (bankApp != null) bankApp.SetActive(app == SmartphoneAppType.Bank);
            if (garageApp != null) garageApp.SetActive(app == SmartphoneAppType.Garage);
            if (messagesApp != null) messagesApp.SetActive(app == SmartphoneAppType.Messages);

            if (backButtonObj != null)
            {
                backButtonObj.SetActive(app != SmartphoneAppType.Home);
            }

            if (timeText != null)
            {
                timeText.gameObject.SetActive(app == SmartphoneAppType.Home);
            }

            if (app == SmartphoneAppType.Home)
            {
                UpdateHomeSubtitles();
            }
        }

        private void RefreshCurrentApp()
        {
            if (currentApp == SmartphoneAppType.Delivery && deliveryApp != null) deliveryApp.RefreshView();
            else if (currentApp == SmartphoneAppType.Bank && bankApp != null) bankApp.RefreshData();
            else if (currentApp == SmartphoneAppType.Garage && garageApp != null) garageApp.RefreshData();
            else if (currentApp == SmartphoneAppType.Messages && messagesApp != null) messagesApp.RefreshView();
            else if (currentApp == SmartphoneAppType.Home) UpdateHomeSubtitles();
        }

        private void RefreshBadges()
        {
            bool hasUnread = unreadNotifications > 0;

            if (hudBadgeObj != null)
            {
                hudBadgeObj.SetActive(hasUnread);
                if (hudBadgeText != null) hudBadgeText.text = unreadNotifications.ToString();
            }

            if (deliveryBadgeObj != null)
            {
                deliveryBadgeObj.SetActive(hasUnread);
                if (deliveryBadgeText != null) deliveryBadgeText.text = unreadNotifications.ToString();
            }
        }

        private void UpdateClock()
        {
            if (timeText != null)
            {
                timeText.text = DateTime.Now.ToString("HH:mm");
            }
        }

        private void UpdateHomeSubtitles()
        {
            if (homeBankSubtitle != null)
            {
                int money = PlayerProgressionManager.Instance != null ? PlayerProgressionManager.Instance.CurrentMoney : 0;
                homeBankSubtitle.text = $"{money:N0} TL";
            }

            if (homeGarageSubtitle != null)
            {
                string vName = "Aktif Kurye Vanı";
                if (PlayerVehicleManager.Instance != null && PlayerVehicleManager.Instance.ActiveVehicleController != null)
                {
                    vName = PlayerVehicleManager.Instance.ActiveVehicleController.gameObject.name.Replace("(Clone)", "").Trim();
                }
                homeGarageSubtitle.text = vName;
            }

            if (homeMessagesSubtitle != null)
            {
                float rating = CourierRatingManager.Instance != null ? CourierRatingManager.Instance.CurrentRating : 5.0f;
                homeMessagesSubtitle.text = $"{rating:F1} / 5.0 Puan";
            }
        }

        private Canvas ResolveCanvas()
        {
            if (GlobalUiCoordinator.PrimaryCanvas != null)
            {
                return GlobalUiCoordinator.PrimaryCanvas;
            }

            GameObject hud = GameObject.Find("GameplayHUDCanvas");
            if (hud != null)
            {
                Canvas c = hud.GetComponent<Canvas>();
                if (c != null) return c;
            }

            Canvas anyCanvas = FindFirstObjectByType<Canvas>();
            return anyCanvas;
        }

        // =========================================================================
        // UI BUILDERS
        // =========================================================================

        private void BuildHUDButton(Transform parent)
        {
            if (hudButtonRect != null) return;

            GameObject btnObj = new GameObject("HUD_PhoneButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            hudButtonRect = btnObj.GetComponent<RectTransform>();
            hudButtonRect.anchorMin = new Vector2(1f, 0f);
            hudButtonRect.anchorMax = new Vector2(1f, 0f);
            hudButtonRect.pivot = new Vector2(1f, 0f);
            hudButtonRect.anchoredPosition = new Vector2(-28f, 28f);
            hudButtonRect.sizeDelta = new Vector2(62f, 62f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            btnImg.sprite = fallbackSprite;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(TogglePhone);
            UIButtonEnhancer.EnhanceButton(btn);

            // Icon & Hotkey
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iRect = iconObj.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.5f, 0.62f);
            iRect.anchorMax = new Vector2(0.5f, 0.62f);
            iRect.pivot = new Vector2(0.5f, 0.5f);
            iRect.sizeDelta = new Vector2(28f, 28f);
            Image iImg = iconObj.GetComponent<Image>();
            iImg.sprite = SmartphoneIconRegistry.GetPhoneIcon();
            iImg.color = Color.white;
            iImg.raycastTarget = false;

            GameObject keyObj = new GameObject("KeyHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            keyObj.transform.SetParent(btnObj.transform, false);
            RectTransform kRect = keyObj.GetComponent<RectTransform>();
            kRect.anchorMin = Vector2.zero;
            kRect.anchorMax = new Vector2(1f, 0.35f);
            kRect.offsetMin = Vector2.zero;
            kRect.offsetMax = Vector2.zero;
            TextMeshProUGUI keyTmp = keyObj.GetComponent<TextMeshProUGUI>();
            keyTmp.text = "[TAB]";
            keyTmp.fontSize = 11f;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = new Color(0f, 0.85f, 1f, 0.9f);
            keyTmp.alignment = TextAlignmentOptions.Center;
            keyTmp.raycastTarget = false;

            // Notification Badge (Red dot)
            hudBadgeObj = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            hudBadgeObj.transform.SetParent(btnObj.transform, false);
            RectTransform bRect = hudBadgeObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(1f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 0.5f);
            bRect.anchoredPosition = new Vector2(-4f, -4f);
            bRect.sizeDelta = new Vector2(22f, 22f);

            Image bImg = hudBadgeObj.GetComponent<Image>();
            bImg.color = new Color(1f, 0.2f, 0.2f, 1f);
            bImg.sprite = fallbackSprite;

            GameObject btObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btObj.transform.SetParent(hudBadgeObj.transform, false);
            RectTransform btRect = btObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            hudBadgeText = btObj.GetComponent<TextMeshProUGUI>();
            hudBadgeText.text = "1";
            hudBadgeText.fontSize = 12f;
            hudBadgeText.fontStyle = FontStyles.Bold;
            hudBadgeText.alignment = TextAlignmentOptions.Center;
            hudBadgeText.color = Color.white;

            hudBadgeObj.SetActive(false);
        }

        private void BuildNotificationToast(Transform parent)
        {
            if (notificationToast != null) return;
            notificationToast = gameObject.AddComponent<SmartphoneNotificationToast>();
            notificationToast.Initialize(parent, fallbackSprite);
        }

        private void BuildPhoneContainer(Transform parent)
        {
            if (phoneRootRect != null) return;

            // Phone Sliding Root
            GameObject rootObj = new GameObject("SmartphoneUI", typeof(RectTransform), typeof(CanvasGroup));
            rootObj.transform.SetParent(parent, false);

            phoneRootRect = rootObj.GetComponent<RectTransform>();
            phoneRootRect.anchorMin = new Vector2(1f, 0f);
            phoneRootRect.anchorMax = new Vector2(1f, 0f);
            phoneRootRect.pivot = new Vector2(1f, 0f);
            phoneRootRect.anchoredPosition = closedPosition;
            phoneRootRect.sizeDelta = phoneSize;

            phoneCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            phoneCanvasGroup.alpha = 0f;
            phoneCanvasGroup.blocksRaycasts = false;
            phoneCanvasGroup.interactable = false;

            // Phone Outer Bezel/Chassis (Titanium)
            GameObject chassisObj = new GameObject("Chassis", typeof(RectTransform), typeof(Image));
            chassisObj.transform.SetParent(rootObj.transform, false);
            RectTransform chRect = chassisObj.GetComponent<RectTransform>();
            chRect.anchorMin = Vector2.zero;
            chRect.anchorMax = Vector2.one;
            chRect.offsetMin = Vector2.zero;
            chRect.offsetMax = Vector2.zero;

            Image chImg = chassisObj.GetComponent<Image>();
            chImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            chImg.sprite = fallbackSprite;

            // Screen Inner Body
            GameObject screenObj = new GameObject("Screen", typeof(RectTransform), typeof(Image), typeof(Mask));
            screenObj.transform.SetParent(chassisObj.transform, false);
            RectTransform scrRect = screenObj.GetComponent<RectTransform>();
            scrRect.anchorMin = Vector2.zero;
            scrRect.anchorMax = Vector2.one;
            scrRect.offsetMin = new Vector2(8f, 8f);
            scrRect.offsetMax = new Vector2(-8f, -8f);

            Image scrImg = screenObj.GetComponent<Image>();
            scrImg.color = new Color(0.04f, 0.06f, 0.09f, 1f);
            scrImg.sprite = fallbackSprite;

            Mask mask = screenObj.GetComponent<Mask>();
            mask.showMaskGraphic = true;

            // Status Bar (Top)
            BuildStatusBar(screenObj.transform);

            // App Content Area (Middle)
            contentAreaObj = new GameObject("ContentArea", typeof(RectTransform));
            contentAreaObj.transform.SetParent(screenObj.transform, false);
            RectTransform cRect = contentAreaObj.GetComponent<RectTransform>();
            cRect.anchorMin = Vector2.zero;
            cRect.anchorMax = Vector2.one;
            cRect.offsetMin = new Vector2(0f, 32f); // Above bottom bar
            cRect.offsetMax = new Vector2(0f, -38f); // Below status bar

            // Bottom Navigation Pill Bar
            BuildBottomBar(screenObj.transform);
        }

        private void BuildStatusBar(Transform parent)
        {
            GameObject barObj = new GameObject("StatusBar", typeof(RectTransform), typeof(Image));
            barObj.transform.SetParent(parent, false);
            RectTransform bRect = barObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = Vector2.zero;
            bRect.sizeDelta = new Vector2(0f, 38f);

            Image bImg = barObj.GetComponent<Image>();
            bImg.color = new Color(0.03f, 0.04f, 0.07f, 0.95f);
            bImg.sprite = fallbackSprite;

            // Dynamic Island / Notch
            GameObject notchObj = new GameObject("Notch", typeof(RectTransform), typeof(Image));
            notchObj.transform.SetParent(barObj.transform, false);
            RectTransform nRect = notchObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.5f, 0.5f);
            nRect.anchorMax = new Vector2(0.5f, 0.5f);
            nRect.pivot = new Vector2(0.5f, 0.5f);
            nRect.anchoredPosition = new Vector2(0f, 2f);
            nRect.sizeDelta = new Vector2(85f, 18f);
            Image nImg = notchObj.GetComponent<Image>();
            nImg.color = new Color(0.01f, 0.02f, 0.03f, 1f);
            nImg.sprite = fallbackSprite;

            // Clock Time
            GameObject timeObj = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeObj.transform.SetParent(barObj.transform, false);
            RectTransform tRect = timeObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0f);
            tRect.anchorMax = new Vector2(0.3f, 1f);
            tRect.offsetMin = new Vector2(12f, 0f);
            tRect.offsetMax = Vector2.zero;
            timeText = timeObj.GetComponent<TextMeshProUGUI>();
            timeText.text = DateTime.Now.ToString("HH:mm");
            timeText.fontSize = 12f;
            timeText.fontStyle = FontStyles.Bold;
            timeText.alignment = TextAlignmentOptions.Left;
            timeText.color = new Color(0.9f, 0.95f, 1f, 1f);

            // Icons (5G, Battery)
            GameObject iconsObj = new GameObject("Icons", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconsObj.transform.SetParent(barObj.transform, false);
            RectTransform icRect = iconsObj.GetComponent<RectTransform>();
            icRect.anchorMin = new Vector2(0.7f, 0f);
            icRect.anchorMax = new Vector2(1f, 1f);
            icRect.offsetMin = Vector2.zero;
            icRect.offsetMax = new Vector2(-10f, 0f);
            TextMeshProUGUI icTmp = iconsObj.GetComponent<TextMeshProUGUI>();
            icTmp.text = "5G [||||]";
            icTmp.fontSize = 11f;
            icTmp.alignment = TextAlignmentOptions.Right;
            icTmp.color = new Color(0.85f, 0.9f, 0.95f, 0.9f);

            // Universal Back Button (Visible when inside an app)
            backButtonObj = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backButtonObj.transform.SetParent(barObj.transform, false);
            RectTransform bbRect = backButtonObj.GetComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0f, 0f);
            bbRect.anchorMax = new Vector2(0f, 1f);
            bbRect.pivot = new Vector2(0f, 0.5f);
            bbRect.anchoredPosition = new Vector2(6f, 0f);
            bbRect.sizeDelta = new Vector2(75f, 26f);

            Image bbImg = backButtonObj.GetComponent<Image>();
            bbImg.color = new Color(0.12f, 0.18f, 0.28f, 0.9f);
            bbImg.sprite = fallbackSprite;

            Button bbBtn = backButtonObj.GetComponent<Button>();
            bbBtn.onClick.AddListener(() => OpenApp(SmartphoneAppType.Home));

            GameObject bbtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            bbtObj.transform.SetParent(backButtonObj.transform, false);
            RectTransform bbtRect = bbtObj.GetComponent<RectTransform>();
            bbtRect.anchorMin = Vector2.zero;
            bbtRect.anchorMax = Vector2.one;
            bbtRect.offsetMin = Vector2.zero;
            bbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI bbtTmp = bbtObj.GetComponent<TextMeshProUGUI>();
            bbtTmp.text = "< Menü";
            bbtTmp.fontSize = 11f;
            bbtTmp.fontStyle = FontStyles.Bold;
            bbtTmp.alignment = TextAlignmentOptions.Center;
            bbtTmp.color = new Color(0f, 0.85f, 1f, 1f);

            backButtonObj.SetActive(false);
        }

        private void BuildBottomBar(Transform parent)
        {
            GameObject barObj = new GameObject("BottomBar", typeof(RectTransform), typeof(Image), typeof(Button));
            barObj.transform.SetParent(parent, false);
            RectTransform bRect = barObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 0f);
            bRect.anchorMax = new Vector2(1f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.anchoredPosition = Vector2.zero;
            bRect.sizeDelta = new Vector2(0f, 32f);

            Image bImg = barObj.GetComponent<Image>();
            bImg.color = new Color(0.02f, 0.03f, 0.05f, 0.98f);
            bImg.sprite = fallbackSprite;

            Button btn = barObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (currentApp != SmartphoneAppType.Home)
                {
                    OpenApp(SmartphoneAppType.Home);
                }
                else
                {
                    ClosePhone();
                }
            });

            // Home Pill Bar
            GameObject pillObj = new GameObject("HomePill", typeof(RectTransform), typeof(Image));
            pillObj.transform.SetParent(barObj.transform, false);
            RectTransform pRect = pillObj.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f);
            pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero;
            pRect.sizeDelta = new Vector2(120f, 4.5f);

            Image pImg = pillObj.GetComponent<Image>();
            pImg.color = new Color(1f, 1f, 1f, 0.65f);
            pImg.sprite = fallbackSprite;
            pImg.raycastTarget = false;
        }

        private void BuildHomeScreen(Transform parent)
        {
            homeScreenObj = new GameObject("HomeScreen", typeof(RectTransform));
            homeScreenObj.transform.SetParent(parent, false);
            RectTransform hsRect = homeScreenObj.GetComponent<RectTransform>();
            hsRect.anchorMin = Vector2.zero;
            hsRect.anchorMax = Vector2.one;
            hsRect.offsetMin = Vector2.zero;
            hsRect.offsetMax = Vector2.zero;

            // Header Banner (Greeting & Shift)
            GameObject headerObj = new GameObject("HeaderBanner", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(homeScreenObj.transform, false);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -10f);
            hRect.sizeDelta = new Vector2(-24f, 85f);

            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            hImg.sprite = fallbackSprite;

            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(headerObj.transform, false);
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0.5f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.offsetMin = new Vector2(14f, 0f);
            tRect.offsetMax = new Vector2(-14f, -6f);
            TextMeshProUGUI tTmp = titleObj.GetComponent<TextMeshProUGUI>();
            tTmp.text = "Kurye Portalı";
            tTmp.fontSize = 17f;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.color = new Color(0.95f, 0.98f, 1f, 1f);

            // Shift Indicator Dot
            GameObject dotObj = new GameObject("ShiftDot", typeof(RectTransform), typeof(Image));
            dotObj.transform.SetParent(headerObj.transform, false);
            RectTransform dr = dotObj.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0f, 0f);
            dr.anchorMax = new Vector2(0f, 0.5f);
            dr.pivot = new Vector2(0f, 0.5f);
            dr.anchoredPosition = new Vector2(14f, 0f);
            dr.sizeDelta = new Vector2(12f, 12f);
            Image dImg = dotObj.GetComponent<Image>();
            dImg.sprite = SmartphoneIconRegistry.GetDotGreenIcon();
            dImg.color = Color.white;
            dImg.raycastTarget = false;

            GameObject shiftObj = new GameObject("ShiftStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            shiftObj.transform.SetParent(headerObj.transform, false);
            RectTransform sRect = shiftObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.offsetMin = new Vector2(32f, 6f);
            sRect.offsetMax = new Vector2(-14f, 0f);
            TextMeshProUGUI sTmp = shiftObj.GetComponent<TextMeshProUGUI>();
            sTmp.text = "Vardiya: Aktif | Şehir Teslimatları";
            sTmp.fontSize = 11.5f;
            sTmp.color = new Color(0.2f, 0.95f, 0.45f, 0.95f);

            // Apps Grid Container
            GameObject gridObj = new GameObject("AppGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObj.transform.SetParent(homeScreenObj.transform, false);
            RectTransform gRect = gridObj.GetComponent<RectTransform>();
            gRect.anchorMin = new Vector2(0f, 0f);
            gRect.anchorMax = new Vector2(1f, 1f);
            gRect.offsetMin = new Vector2(12f, 20f);
            gRect.offsetMax = new Vector2(-12f, -110f);

            GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(148f, 180f);
            grid.spacing = new Vector2(12f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // App 1: Kurye Express
            CreateAppCard(gridObj.transform, "Kurye Express", "Teslimat & İlanlar", SmartphoneIconRegistry.GetDeliveryAppIcon(), new Color(0.95f, 0.45f, 0.1f, 1f), () => OpenApp(SmartphoneAppType.Delivery), out deliveryBadgeObj, out deliveryBadgeText);

            // App 2: QuickPay Banka
            CreateAppCard(gridObj.transform, "QuickPay Banka", "0 TL", SmartphoneIconRegistry.GetBankAppIcon(), new Color(0.1f, 0.85f, 0.45f, 1f), () => OpenApp(SmartphoneAppType.Bank), out _, out homeBankSubtitle);

            // App 3: Garajım & Filo
            CreateAppCard(gridObj.transform, "Garajım", "Aktif Kurye Vanı", SmartphoneIconRegistry.GetGarageAppIcon(), new Color(0.15f, 0.65f, 1f, 1f), () => OpenApp(SmartphoneAppType.Garage), out _, out homeGarageSubtitle);

            // App 4: Müşteri Mesajları
            CreateAppCard(gridObj.transform, "Müşteri Yorumları", "5.0 / 5.0 Puan", SmartphoneIconRegistry.GetMessagesAppIcon(), new Color(0.7f, 0.35f, 1f, 1f), () => OpenApp(SmartphoneAppType.Messages), out _, out homeMessagesSubtitle);
        }

        private void CreateAppCard(Transform parent, string title, string subtitle, Sprite iconSprite, Color accentColor, Action onClick, out GameObject badgeObj, out TextMeshProUGUI subtitleText)
        {
            GameObject cardObj = new GameObject($"AppCard_{title}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(parent, false);

            Image cImg = cardObj.GetComponent<Image>();
            cImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            cImg.sprite = fallbackSprite;

            Button btn = cardObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            UIButtonEnhancer.EnhanceButton(btn);

            // Icon Background
            GameObject iconBgObj = new GameObject("IconBg", typeof(RectTransform), typeof(Image));
            iconBgObj.transform.SetParent(cardObj.transform, false);
            RectTransform ibRect = iconBgObj.GetComponent<RectTransform>();
            ibRect.anchorMin = new Vector2(0.5f, 0.72f);
            ibRect.anchorMax = new Vector2(0.5f, 0.72f);
            ibRect.pivot = new Vector2(0.5f, 0.5f);
            ibRect.sizeDelta = new Vector2(62f, 62f);

            Image ibImg = iconBgObj.GetComponent<Image>();
            ibImg.color = accentColor * 0.25f;
            ibImg.sprite = fallbackSprite;
            ibImg.raycastTarget = false;

            // Icon Image
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconBgObj.transform, false);
            RectTransform iRect = iconObj.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.5f, 0.5f);
            iRect.anchorMax = new Vector2(0.5f, 0.5f);
            iRect.pivot = new Vector2(0.5f, 0.5f);
            iRect.sizeDelta = new Vector2(40f, 40f);
            Image iImg = iconObj.GetComponent<Image>();
            iImg.sprite = iconSprite;
            iImg.color = Color.white;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;

            // Title
            GameObject tObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            tObj.transform.SetParent(cardObj.transform, false);
            RectTransform tRect = tObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0.22f);
            tRect.anchorMax = new Vector2(1f, 0.42f);
            tRect.offsetMin = new Vector2(8f, 0f);
            tRect.offsetMax = new Vector2(-8f, 0f);
            TextMeshProUGUI tTmp = tObj.GetComponent<TextMeshProUGUI>();
            tTmp.text = title;
            tTmp.fontSize = 13.5f;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.color = Color.white;
            tTmp.raycastTarget = false;

            // Subtitle
            GameObject sObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            sObj.transform.SetParent(cardObj.transform, false);
            RectTransform sRect = sObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0.05f);
            sRect.anchorMax = new Vector2(1f, 0.22f);
            sRect.offsetMin = new Vector2(6f, 0f);
            sRect.offsetMax = new Vector2(-6f, 0f);
            subtitleText = sObj.GetComponent<TextMeshProUGUI>();
            subtitleText.text = subtitle;
            subtitleText.fontSize = 11f;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.7f, 0.85f, 0.95f, 0.85f);
            subtitleText.raycastTarget = false;

            // Badge (Optional)
            badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(cardObj.transform, false);
            RectTransform bRect = badgeObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(1f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 0.5f);
            bRect.anchoredPosition = new Vector2(-6f, -6f);
            bRect.sizeDelta = new Vector2(20f, 20f);

            Image bImg = badgeObj.GetComponent<Image>();
            bImg.color = new Color(1f, 0.25f, 0.25f, 1f);
            bImg.sprite = fallbackSprite;

            GameObject btObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btObj.transform.SetParent(badgeObj.transform, false);
            RectTransform btRect = btObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            TextMeshProUGUI btTmp = btObj.GetComponent<TextMeshProUGUI>();
            btTmp.text = "1";
            btTmp.fontSize = 11f;
            btTmp.fontStyle = FontStyles.Bold;
            btTmp.alignment = TextAlignmentOptions.Center;
            btTmp.color = Color.white;

            badgeObj.SetActive(false);
        }

        private void BuildSubApps(Transform parent)
        {
            // 1. Delivery App
            deliveryApp = gameObject.AddComponent<SmartphoneDeliveryApp>();
            deliveryApp.Initialize(parent, fallbackSprite);

            // 2. Bank App
            bankApp = gameObject.AddComponent<SmartphoneBankApp>();
            bankApp.Initialize(parent, fallbackSprite);

            // 3. Garage App
            garageApp = gameObject.AddComponent<SmartphoneGarageApp>();
            garageApp.Initialize(parent, fallbackSprite);

            // 4. Messages App
            messagesApp = gameObject.AddComponent<SmartphoneMessagesApp>();
            messagesApp.Initialize(parent, fallbackSprite);
        }

        private static bool IsGameSceneActive()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return sceneName.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase) ||
                   sceneName.Equals(AltGameSceneName, StringComparison.OrdinalIgnoreCase);
        }
    }
}