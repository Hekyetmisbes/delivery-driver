using System;
using System.Collections;
using System.Collections.Generic;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DeliveryDriver.Company
{
    [DefaultExecutionOrder(-500)]
    public class CompanyPageUI : MonoBehaviour
    {
        private const float DatabaseReadyTimeoutSeconds = 10f;
        private const string OverlayName = "CompanyPageOverlay";
        private const string PanelName = "CompanyPagePanel";
        private const string GameSceneName = "Game";

        [SerializeField] private Vector2 panelSize = new Vector2(860f, 620f);

        private GameObject overlayObject;
        private GameObject panelObject;
        private TextMeshProUGUI companyNameValueText;
        private TextMeshProUGUI balanceValueText;
        private TextMeshProUGUI managerValueText;
        private TextMeshProUGUI statusText;
        private RuntimeOptionSelector vehicleTypeDropdown;
        private Button continueButton;
        private bool suppressDropdownCallbacks;
        private bool profileLoaded;
        private VehicleType selectedVehicleType = VehicleType.Van;
        private readonly List<VehicleType> availableVehicleTypes = new List<VehicleType>();
        private float previousTimeScale = 1f;
        private bool gameplayPausedByPanel;
        private bool questPausedByPanel;

        private void Awake()
        {
            if (!IsGameSceneActive())
            {
                return;
            }

            PauseGameplayForCompanyPage();
        }

        private void Start()
        {
            if (!IsGameSceneActive())
            {
                Destroy(gameObject);
                return;
            }

            StartCoroutine(InitializeRoutine());
        }

        private void Update()
        {
            if (!profileLoaded)
            {
                return;
            }

            if (WasContinueShortcutPressed())
            {
                OnContinueClicked();
            }
        }

        private void OnDestroy()
        {
            if (vehicleTypeDropdown != null)
            {
                vehicleTypeDropdown.onValueChanged.RemoveListener(OnVehicleTypeChanged);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            ResumeGameplayFromCompanyPage();
        }

        private IEnumerator InitializeRoutine()
        {
            EnsureUi();
            SetLoadingState(LocalizationTable.Get("company_loading"));

            float timeoutAt = Time.realtimeSinceStartup + DatabaseReadyTimeoutSeconds;
            while ((QuestDatabaseService.Instance == null || !QuestDatabaseService.Instance.IsReady) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (!TryLoadCompanyProfile(out CompanyProfileData profile))
            {
                Debug.LogError("[CompanyPageUI] Company profile could not be resolved from the database.");
                ShowFatalError(LocalizationTable.Get("company_load_error"));
                yield break;
            }

            VehicleType resolvedVehicleType = RefreshVehicleDropdownOptions(profile.SelectedVehicleType);
            bool preferredVehicleAvailable = resolvedVehicleType == profile.SelectedVehicleType;
            profile.SelectedVehicleType = resolvedVehicleType;

            if (!preferredVehicleAvailable)
            {
                TryPersistVehicleType(resolvedVehicleType);
            }

            ApplyProfile(profile);

            if (!ApplyVehicleSelection(profile.SelectedVehicleType, true))
            {
                ShowFatalError(LocalizationTable.Get("company_apply_vehicle_error"));
                yield break;
            }

            if (!preferredVehicleAvailable)
            {
                SetWarningState(LocalizationTable.Format("company_unavailable_saved_vehicle", VehicleTypeExtensions.ToDisplayLabel(resolvedVehicleType)));
                yield break;
            }

            SetReadyState(LocalizationTable.Get("company_ready"));
        }

        private void EnsureUi()
        {
            if (overlayObject != null && panelObject != null)
            {
                return;
            }

            EnsureEventSystem();

            Canvas parentCanvas = ResolveParentCanvas();
            if (parentCanvas == null)
            {
                Debug.LogError("[CompanyPageUI] No canvas could be resolved for the company page.");
                return;
            }

            overlayObject = new GameObject(OverlayName, typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            overlayObject.transform.SetParent(parentCanvas.transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.color = UIThemeConstants.OverlayBackground;

            Canvas overlayCanvas = overlayObject.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 9985;

            panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(overlayObject.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = UIThemeConstants.PanelBackground;
            panelImage.sprite = DeliveryUiSpriteHelper.GetFallbackSprite();
            panelImage.type = Image.Type.Sliced;

            VerticalLayoutGroup panelLayout = panelObject.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(32, 32, 28, 28);
            panelLayout.spacing = 16f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            CreateHeader(panelObject.transform);
            Transform infoSection = CreateSection(panelObject.transform, "SirketBilgileri");
            companyNameValueText = CreateInfoRow(infoSection, LocalizationTable.Get("company_label_name"));
            balanceValueText = CreateInfoRow(infoSection, LocalizationTable.Get("company_label_balance"));
            managerValueText = CreateInfoRow(infoSection, LocalizationTable.Get("company_label_manager"));
            vehicleTypeDropdown = CreateVehicleDropdownRow(infoSection, LocalizationTable.Get("company_label_vehicle_type"));
            vehicleTypeDropdown.onValueChanged.AddListener(OnVehicleTypeChanged);

            statusText = CreateStatusText(panelObject.transform);
            Transform footerRow = CreateFooterRow(panelObject.transform);
            continueButton = CreateButton(footerRow, LocalizationTable.Get("continue"), UIThemeConstants.ButtonGreen);
            continueButton.onClick.AddListener(OnContinueClicked);

            UIAnimationHelper.ScaleIn(this, panelRect, UIThemeConstants.PanelScaleDuration);
        }

        private void ApplyProfile(CompanyProfileData profile)
        {
            profileLoaded = true;
            selectedVehicleType = profile.SelectedVehicleType;

            companyNameValueText.text = profile.CompanyName;
            balanceValueText.text = $"${profile.Balance:N0}";
            managerValueText.text = profile.PlayerDisplayName;

            suppressDropdownCallbacks = true;
            vehicleTypeDropdown.SetValueWithoutNotify(GetDropdownIndexForVehicleType(profile.SelectedVehicleType));
            vehicleTypeDropdown.RefreshShownValue();
            suppressDropdownCallbacks = false;
        }

        private void SetLoadingState(string message)
        {
            profileLoaded = false;
            if (vehicleTypeDropdown != null)
            {
                vehicleTypeDropdown.interactable = false;
            }

            if (continueButton != null)
            {
                continueButton.interactable = false;
            }

            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = UIThemeConstants.TextSecondary;
            }
        }

        private void SetReadyState(string message)
        {
            if (vehicleTypeDropdown != null)
            {
                vehicleTypeDropdown.interactable = availableVehicleTypes.Count > 1;
            }

            if (continueButton != null)
            {
                continueButton.interactable = true;
            }

            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = UIThemeConstants.Positive;
            }
        }

        private void SetWarningState(string message)
        {
            if (vehicleTypeDropdown != null)
            {
                vehicleTypeDropdown.interactable = availableVehicleTypes.Count > 1;
            }

            if (continueButton != null)
            {
                continueButton.interactable = true;
            }

            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = UIThemeConstants.Warning;
            }
        }

        private void ShowFatalError(string message)
        {
            profileLoaded = false;
            if (vehicleTypeDropdown != null)
            {
                vehicleTypeDropdown.interactable = false;
            }

            if (continueButton != null)
            {
                continueButton.interactable = false;
            }

            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = UIThemeConstants.Negative;
            }
        }

        private void OnVehicleTypeChanged(int index)
        {
            if (suppressDropdownCallbacks || !profileLoaded)
            {
                return;
            }

            VehicleType requestedVehicleType = GetVehicleTypeForDropdownIndex(index);
            if (!TryPersistVehicleType(requestedVehicleType))
            {
                RevertVehicleSelection();
                SetWarningState(LocalizationTable.Get("company_save_vehicle_failed"));
                return;
            }

            if (!ApplyVehicleSelection(requestedVehicleType))
            {
                TryPersistVehicleType(selectedVehicleType);
                RevertVehicleSelection();
                SetWarningState(LocalizationTable.Get("company_vehicle_not_available"));
                return;
            }

            selectedVehicleType = requestedVehicleType;
            SetReadyState(LocalizationTable.Format("company_vehicle_saved", VehicleTypeExtensions.ToDisplayLabel(requestedVehicleType)));
        }

        private void RevertVehicleSelection()
        {
            if (vehicleTypeDropdown == null)
            {
                return;
            }

            suppressDropdownCallbacks = true;
            vehicleTypeDropdown.SetValueWithoutNotify(GetDropdownIndexForVehicleType(selectedVehicleType));
            vehicleTypeDropdown.RefreshShownValue();
            suppressDropdownCallbacks = false;
        }

        private void OnContinueClicked()
        {
            if (!profileLoaded)
            {
                return;
            }

            if (overlayObject != null)
            {
                Destroy(overlayObject);
            }

            ResumeGameplayFromCompanyPage();
            Destroy(gameObject);
        }

        private bool ApplyVehicleSelection(VehicleType vehicleType, bool allowDuringActiveQuest = false)
        {
            PlayerVehicleManager vehicleManager = ResolveVehicleManager();
            if (vehicleManager == null)
            {
                Debug.LogError("[CompanyPageUI] PlayerVehicleManager was not found while applying vehicle selection.");
                return false;
            }

            bool applied = allowDuringActiveQuest
                ? vehicleManager.EnsureVehicleType(vehicleType)
                : vehicleManager.ApplyVehicleType(vehicleType);

            if (!applied)
            {
                Debug.LogError($"[CompanyPageUI] Vehicle type '{vehicleType}' could not be applied.");
                return false;
            }

            return true;
        }

        private VehicleType RefreshVehicleDropdownOptions(VehicleType preferredVehicleType)
        {
            availableVehicleTypes.Clear();

            PlayerVehicleManager vehicleManager = ResolveVehicleManager();
            if (vehicleManager != null)
            {
                if (vehicleManager.IsVehicleTypeAvailable(VehicleType.Van))
                {
                    availableVehicleTypes.Add(VehicleType.Van);
                }

                if (vehicleManager.IsVehicleTypeAvailable(VehicleType.Truck))
                {
                    availableVehicleTypes.Add(VehicleType.Truck);
                }
            }

            if (availableVehicleTypes.Count == 0)
            {
                availableVehicleTypes.Add(VehicleType.Van);
            }

            List<string> labels = new List<string>(availableVehicleTypes.Count);
            for (int i = 0; i < availableVehicleTypes.Count; i++)
            {
                labels.Add(VehicleTypeExtensions.ToDisplayLabel(availableVehicleTypes[i]));
            }

            suppressDropdownCallbacks = true;
            vehicleTypeDropdown.ClearOptions();
            vehicleTypeDropdown.AddOptions(labels);
            vehicleTypeDropdown.interactable = availableVehicleTypes.Count > 1;

            VehicleType resolvedVehicleType = availableVehicleTypes.Contains(preferredVehicleType)
                ? preferredVehicleType
                : availableVehicleTypes[0];

            vehicleTypeDropdown.SetValueWithoutNotify(GetDropdownIndexForVehicleType(resolvedVehicleType));
            vehicleTypeDropdown.RefreshShownValue();
            suppressDropdownCallbacks = false;

            return resolvedVehicleType;
        }

        private int GetDropdownIndexForVehicleType(VehicleType vehicleType)
        {
            int index = availableVehicleTypes.IndexOf(vehicleType);
            return index >= 0 ? index : 0;
        }

        private VehicleType GetVehicleTypeForDropdownIndex(int index)
        {
            if (index >= 0 && index < availableVehicleTypes.Count)
            {
                return availableVehicleTypes[index];
            }

            return selectedVehicleType;
        }

        private static PlayerVehicleManager ResolveVehicleManager()
        {
            return PlayerVehicleManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<PlayerVehicleManager>();
        }

        private static bool TryLoadCompanyProfile(out CompanyProfileData profile)
        {
            profile = null;
            QuestDatabaseService database = QuestDatabaseService.Instance;
            if (database == null || !database.IsReady)
            {
                Debug.LogError("[CompanyPageUI] Database is not ready. Company page cannot continue without SQLite.");
                return false;
            }

            if (!database.EnsureDefaultCompanyProfile())
            {
                Debug.LogError("[CompanyPageUI] Failed to ensure the default company profile in the database.");
                return false;
            }

            profile = database.GetCompanyProfile(QuestDatabaseService.DefaultPlayerId);
            return profile != null;
        }

        private static bool TryPersistVehicleType(VehicleType requestedVehicleType)
        {
            QuestDatabaseService database = QuestDatabaseService.Instance;
            return database != null &&
                   database.IsReady &&
                   database.SaveSelectedVehicleType(QuestDatabaseService.DefaultPlayerId, requestedVehicleType);
        }

        private void PauseGameplayForCompanyPage()
        {
            if (gameplayPausedByPanel)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            gameplayPausedByPanel = true;

            QuestData activeQuest = QuestManager.Instance != null ? QuestManager.Instance.CurrentQuest : null;
            if (activeQuest != null && !activeQuest.IsPaused)
            {
                activeQuest.PauseQuest();
                questPausedByPanel = true;
            }
        }

        private void ResumeGameplayFromCompanyPage()
        {
            if (!gameplayPausedByPanel)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            gameplayPausedByPanel = false;

            QuestData activeQuest = QuestManager.Instance != null ? QuestManager.Instance.CurrentQuest : null;
            if (questPausedByPanel && activeQuest != null)
            {
                activeQuest.ResumeQuest();
            }

            questPausedByPanel = false;
        }

        private static bool WasContinueShortcutPressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool continuePressed = Keyboard.current != null &&
                                   (Keyboard.current.enterKey.wasPressedThisFrame ||
                                    Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                    Keyboard.current.spaceKey.wasPressedThisFrame);
#if ENABLE_LEGACY_INPUT_MANAGER
            continuePressed = continuePressed || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
            return continuePressed;
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool IsGameSceneActive()
        {
            return SceneManager.GetActiveScene().name.Equals(GameSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            }

            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                UnityEngine.Object.Destroy(standalone);
            }
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif

            if (!eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(true);
            }
        }

        private static Canvas ResolveParentCanvas()
        {
            if (GlobalUiCoordinator.PrimaryCanvas != null)
            {
                return GlobalUiCoordinator.PrimaryCanvas;
            }

            Canvas existingCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                return existingCanvas;
            }

            GameObject canvasObject = new GameObject("CompanyPageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private void CreateHeader(Transform parent)
        {
            CreateText(parent, LocalizationTable.Get("company_page_title"), 42f, FontStyles.Bold, TextAlignmentOptions.Center, UIThemeConstants.TextHeader, 64f);
            CreateText(parent, LocalizationTable.Get("company_page_subtitle"), 20f, FontStyles.Normal, TextAlignmentOptions.Center, UIThemeConstants.TextSecondary, 54f);
        }

        private Transform CreateSection(Transform parent, string name)
        {
            GameObject sectionObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            sectionObject.transform.SetParent(parent, false);

            Image sectionImage = sectionObject.GetComponent<Image>();
            sectionImage.color = UIThemeConstants.SectionBackground;
            sectionImage.sprite = DeliveryUiSpriteHelper.GetFallbackSprite();
            sectionImage.type = Image.Type.Sliced;

            VerticalLayoutGroup sectionLayout = sectionObject.GetComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(22, 22, 20, 20);
            sectionLayout.spacing = 12f;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            LayoutElement layoutElement = sectionObject.GetComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 280f;

            return sectionObject.transform;
        }

        private TextMeshProUGUI CreateInfoRow(Transform parent, string label)
        {
            GameObject rowObject = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 18f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 44f;

            CreateLabeledText(rowObject.transform, label, 230f);
            return CreateValueText(rowObject.transform, "-");
        }

        private RuntimeOptionSelector CreateVehicleDropdownRow(Transform parent, string label)
        {
            GameObject rowObject = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 18f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.minHeight = 52f;

            CreateLabeledText(rowObject.transform, label, 230f);
            RuntimeOptionSelector selector = CreateDropdown(rowObject.transform, new List<string> { LocalizationTable.Get("vehicle_van"), LocalizationTable.Get("vehicle_truck") });
            selector.interactable = false;
            return selector;
        }

        private TextMeshProUGUI CreateLabeledText(Transform parent, string label, float width)
        {
            GameObject labelObject = new GameObject($"{label}Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = labelObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0f;

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.color = UIThemeConstants.TextSubheader;
            text.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private TextMeshProUGUI CreateValueText(Transform parent, string value)
        {
            GameObject valueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            valueObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = valueObject.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;

            TextMeshProUGUI text = valueObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 22f;
            text.color = UIThemeConstants.TextPrimary;
            text.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private TextMeshProUGUI CreateStatusText(Transform parent)
        {
            GameObject statusObject = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            statusObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = statusObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 52f;

            TextMeshProUGUI text = statusObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = UIThemeConstants.TextSecondary;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static Transform CreateFooterRow(Transform parent)
        {
            GameObject rowObject = new GameObject("FooterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 68f;

            return rowObject.transform;
        }

        private Button CreateButton(Transform parent, string label, Color color)
        {
            GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minWidth = 240f;
            layoutElement.preferredWidth = 240f;
            layoutElement.minHeight = 60f;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = color;
            buttonImage.sprite = DeliveryUiSpriteHelper.GetFallbackSprite();
            buttonImage.type = Image.Type.Sliced;

            Button button = buttonObject.GetComponent<Button>();

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            UIButtonEnhancer.EnhanceButton(button);
            return button;
        }

        private RuntimeOptionSelector CreateDropdown(Transform parent, List<string> options)
        {
            GameObject dropdownObject = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dropdownObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = dropdownObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 300f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 44f;

            RuntimeOptionSelector selector = dropdownObject.AddComponent<RuntimeOptionSelector>();
            selector.Initialize(options);
            selector.interactable = options != null && options.Count > 1;
            return selector;
        }

        private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color, float preferredHeight)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }
    }
}
