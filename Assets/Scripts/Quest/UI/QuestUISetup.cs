using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeliveryDriver.UI;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Unity Editor helper script to automatically setup Quest UI in the scene
    /// Attach this to an empty GameObject and press Play to create the UI
    /// </summary>
    public class QuestUISetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool destroyAfterSetup = true;

        private void Awake()
        {
            if (setupOnAwake)
            {
                SetupQuestUI();

                if (destroyAfterSetup)
                {
                    Destroy(gameObject);
                }
            }
        }

        [ContextMenu("Setup Quest UI")]
        public void SetupQuestUI()
        {
            Debug.Log("[QuestUISetup] Starting Quest UI setup...");

            // Check if already exists
            if (FindAnyObjectByType<QuestUIManager>() != null)
            {
                Debug.LogWarning("[QuestUISetup] QuestUIManager already exists in scene!");
                return;
            }

            Canvas mainCanvas = GetOrCreateQuestCanvas();

            // Create QuestUIManager GameObject
            GameObject uiManagerObj = new GameObject("QuestUIManager");
            uiManagerObj.transform.SetParent(mainCanvas.transform, false);
            QuestUIManager uiManager = uiManagerObj.AddComponent<QuestUIManager>();

            // Create Quest List UI
            QuestListUI questListUI = CreateQuestListUI(mainCanvas.transform);

            // Create Active Quest UI
            ActiveQuestUI activeQuestUI = CreateActiveQuestUI(mainCanvas.transform);

            // Create Quest Complete UI
            QuestCompleteUI questCompleteUI = CreateQuestCompleteUI(mainCanvas.transform);

            // Wire up references using reflection (since fields are private)
            var questListField = typeof(QuestUIManager).GetField("questListUI",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeQuestField = typeof(QuestUIManager).GetField("activeQuestUI",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var questCompleteField = typeof(QuestUIManager).GetField("questCompleteUI",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            questListField?.SetValue(uiManager, questListUI);
            activeQuestField?.SetValue(uiManager, activeQuestUI);
            questCompleteField?.SetValue(uiManager, questCompleteUI);

            Debug.Log("[QuestUISetup] Quest UI setup complete!");
            Debug.Log("- Quest List UI: Created");
            Debug.Log("- Active Quest UI: Created");
            Debug.Log("- Quest Complete UI: Created");
        }

        private Canvas CreateMainCanvas()
        {
            GameObject canvasObj = new GameObject("Quest UI Canvas");
            RectTransform canvasRect = canvasObj.AddComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Debug.Log("[QuestUISetup] Created main canvas");
            return canvas;
        }

        private Canvas GetOrCreateQuestCanvas()
        {
            GameObject existing = GameObject.Find("Quest UI Canvas");
            if (existing != null)
            {
                Canvas canvas = existing.GetComponent<Canvas>();
                if (canvas != null)
                {
                    EnsureCanvasScaler(existing, canvas);
                    return canvas;
                }
            }

            return CreateMainCanvas();
        }

        private static void EnsureCanvasScaler(GameObject canvasObject, Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 160);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
            }
        }

        private QuestListUI CreateQuestListUI(Transform parent)
        {
            // Create Quest List Panel
            GameObject questListObj = new GameObject("QuestListUI");
            RectTransform rectTransform = questListObj.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            // Position on left side
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.pivot = new Vector2(0, 0.5f);
            rectTransform.anchoredPosition = new Vector2(20, 0);
            rectTransform.sizeDelta = new Vector2(400, 600);

            // Add background
            Image bgImage = questListObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Add QuestListUI component
            QuestListUI questListUI = questListObj.AddComponent<QuestListUI>();

            // Create title
            GameObject titleObj = new GameObject("Title");
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.SetParent(rectTransform, false);
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -10);
            titleRect.sizeDelta = new Vector2(-20, 40);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = LocalizationTable.Get("quest_list_title");
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Create quest entries container (ScrollView)
            GameObject scrollViewObj = new GameObject("ScrollView");
            RectTransform scrollRect = scrollViewObj.AddComponent<RectTransform>();
            scrollRect.SetParent(rectTransform, false);
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -60);

            ScrollRect scrollView = scrollViewObj.AddComponent<ScrollRect>();
            scrollViewObj.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.5f);

            // Create content container
            GameObject contentObj = new GameObject("Content");
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.SetParent(scrollRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layoutGroup = contentObj.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 10;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;

            ContentSizeFitter sizeFitter = contentObj.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.viewport = scrollRect;
            scrollView.horizontal = false;
            scrollView.vertical = true;

            // Wire up to QuestListUI using reflection
            var containerField = typeof(QuestListUI).GetField("questEntriesContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            containerField?.SetValue(questListUI, contentRect);

            // Start hidden
            questListObj.SetActive(false);

            Debug.Log("[QuestUISetup] Created Quest List UI");
            return questListUI;
        }

        private ActiveQuestUI CreateActiveQuestUI(Transform parent)
        {
            // Create Active Quest Panel (top-right corner)
            GameObject activeQuestObj = new GameObject("ActiveQuestUI");
            RectTransform rectTransform = activeQuestObj.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-20, -20);
            rectTransform.sizeDelta = new Vector2(520, 250);

            // Add background
            Image bgImage = activeQuestObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            ActiveQuestUI activeQuestUI = activeQuestObj.AddComponent<ActiveQuestUI>();

            // Create objective text
            GameObject objectiveObj = new GameObject("Objective");
            RectTransform objectiveRect = objectiveObj.AddComponent<RectTransform>();
            objectiveRect.SetParent(rectTransform, false);
            objectiveRect.anchorMin = new Vector2(0, 1);
            objectiveRect.anchorMax = new Vector2(1, 1);
            objectiveRect.pivot = new Vector2(0.5f, 1);
            objectiveRect.anchoredPosition = new Vector2(0, -10);
            objectiveRect.sizeDelta = new Vector2(-24, 90);

            TextMeshProUGUI objectiveText = objectiveObj.AddComponent<TextMeshProUGUI>();
            objectiveText.text = LocalizationTable.Get("quest_active_delivery_title");
            objectiveText.fontSize = 26;
            objectiveText.fontStyle = FontStyles.Bold;
            objectiveText.alignment = TextAlignmentOptions.TopLeft;
            objectiveText.color = new Color(1f, 0.9f, 0.3f);

            // Create distance text
            GameObject distObj = new GameObject("Distance");
            RectTransform distRect = distObj.AddComponent<RectTransform>();
            distRect.SetParent(rectTransform, false);
            distRect.anchorMin = new Vector2(0, 1);
            distRect.anchorMax = new Vector2(1, 1);
            distRect.pivot = new Vector2(0.5f, 1);
            distRect.anchoredPosition = new Vector2(0, -104);
            distRect.sizeDelta = new Vector2(-24, 34);

            TextMeshProUGUI distText = distObj.AddComponent<TextMeshProUGUI>();
            distText.text = "--";
            distText.fontSize = 20;
            distText.alignment = TextAlignmentOptions.TopLeft;
            distText.color = Color.white;

            // Create timer text
            GameObject timerObj = new GameObject("Timer");
            RectTransform timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.SetParent(rectTransform, false);
            timerRect.anchorMin = new Vector2(0, 1);
            timerRect.anchorMax = new Vector2(1, 1);
            timerRect.pivot = new Vector2(0.5f, 1);
            timerRect.anchoredPosition = new Vector2(0, -144);
            timerRect.sizeDelta = new Vector2(-24, 32);

            TextMeshProUGUI timerText = timerObj.AddComponent<TextMeshProUGUI>();
            timerText.text = "00:00";
            timerText.fontSize = 20;
            timerText.alignment = TextAlignmentOptions.TopLeft;
            timerText.color = Color.white;

            // Wire references using reflection
            var objectiveField = typeof(ActiveQuestUI).GetField("objectiveText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var timerField = typeof(ActiveQuestUI).GetField("timerText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var distField = typeof(ActiveQuestUI).GetField("distanceText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            objectiveField?.SetValue(activeQuestUI, objectiveText);
            timerField?.SetValue(activeQuestUI, timerText);
            distField?.SetValue(activeQuestUI, distText);

            activeQuestObj.SetActive(false); // Start hidden

            Debug.Log("[QuestUISetup] Created Active Quest UI");
            return activeQuestUI;
        }

        private QuestCompleteUI CreateQuestCompleteUI(Transform parent)
        {
            // Create full-screen overlay
            GameObject completeObj = new GameObject("QuestCompleteUI");
            RectTransform rectTransform = completeObj.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            // Dark overlay background
            Image bgImage = completeObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);

            // NOTE: QuestCompleteUI component is added AFTER all children are created
            // so that Awake -> EnsureReadableLayout() can find "ResultPanel" and child texts.

            // Create result panel (center)
            GameObject panelObj = new GameObject("ResultPanel");
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.SetParent(rectTransform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 400);

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            // Result text (GÖREV TAMAMLANDI!)
            GameObject resultObj = new GameObject("ResultText");
            RectTransform resultRect = resultObj.AddComponent<RectTransform>();
            resultRect.SetParent(panelRect, false);
            resultRect.anchorMin = new Vector2(0, 1);
            resultRect.anchorMax = new Vector2(1, 1);
            resultRect.pivot = new Vector2(0.5f, 1);
            resultRect.anchoredPosition = new Vector2(0, -30);
            resultRect.sizeDelta = new Vector2(-40, 60);

            TextMeshProUGUI resultText = resultObj.AddComponent<TextMeshProUGUI>();
            resultText.text = LocalizationTable.Get("delivery_complete");
            resultText.fontSize = 36;
            resultText.fontStyle = FontStyles.Bold;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = new Color(0.2f, 1f, 0.2f); // Green

            // Quest name
            GameObject nameObj = new GameObject("QuestName");
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.SetParent(panelRect, false);
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(0, 50);
            nameRect.sizeDelta = new Vector2(-40, 40);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = LocalizationTable.Get("quest_complete_default_name");
            nameText.fontSize = 24;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;

            // Stats text
            GameObject statsObj = new GameObject("Stats");
            RectTransform statsRect = statsObj.AddComponent<RectTransform>();
            statsRect.SetParent(panelRect, false);
            statsRect.anchorMin = new Vector2(0, 0.5f);
            statsRect.anchorMax = new Vector2(1, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            statsRect.anchoredPosition = new Vector2(0, -20);
            statsRect.sizeDelta = new Vector2(-40, 80);

            TextMeshProUGUI statsText = statsObj.AddComponent<TextMeshProUGUI>();
            statsText.text = "";
            statsText.fontSize = 18;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = new Color(0.8f, 0.8f, 0.8f);

            // Reward text
            GameObject rewardObj = new GameObject("Reward");
            RectTransform rewardRect = rewardObj.AddComponent<RectTransform>();
            rewardRect.SetParent(panelRect, false);
            rewardRect.anchorMin = new Vector2(0, 0);
            rewardRect.anchorMax = new Vector2(1, 0);
            rewardRect.pivot = new Vector2(0.5f, 0);
            rewardRect.anchoredPosition = new Vector2(0, 80);
            rewardRect.sizeDelta = new Vector2(-40, 40);

            TextMeshProUGUI rewardText = rewardObj.AddComponent<TextMeshProUGUI>();
            rewardText.text = "+ $100";
            rewardText.fontSize = 28;
            rewardText.fontStyle = FontStyles.Bold;
            rewardText.alignment = TextAlignmentOptions.Center;
            rewardText.color = new Color(1f, 0.9f, 0.3f); // Gold

            // Continue button
            GameObject buttonObj = new GameObject("ContinueButton");
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.SetParent(panelRect, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0);
            buttonRect.anchorMax = new Vector2(0.5f, 0);
            buttonRect.pivot = new Vector2(0.5f, 0);
            buttonRect.anchoredPosition = new Vector2(0, 20);
            buttonRect.sizeDelta = new Vector2(200, 50);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f);

            Button continueButton = buttonObj.AddComponent<Button>();

            // Add button navigation
            var navigation = continueButton.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            continueButton.navigation = navigation;

            GameObject buttonTextObj = new GameObject("Text");
            RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
            buttonTextRect.SetParent(buttonRect, false);
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = LocalizationTable.Get("quest_complete_button");
            buttonText.fontSize = 20;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;

            // Add QuestCompleteUI component AFTER all children exist
            // so Awake -> EnsureReadableLayout() can find ResultPanel and text components
            QuestCompleteUI questCompleteUI = completeObj.AddComponent<QuestCompleteUI>();

            // Wire references using reflection
            var rootPanelField = typeof(QuestCompleteUI).GetField("rootPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resultTextField = typeof(QuestCompleteUI).GetField("resultText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nameTextField = typeof(QuestCompleteUI).GetField("questNameText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var statsTextField = typeof(QuestCompleteUI).GetField("statsText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rewardTextField = typeof(QuestCompleteUI).GetField("rewardText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var continueButtonField = typeof(QuestCompleteUI).GetField("continueButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            rootPanelField?.SetValue(questCompleteUI, completeObj);
            resultTextField?.SetValue(questCompleteUI, resultText);
            nameTextField?.SetValue(questCompleteUI, nameText);
            statsTextField?.SetValue(questCompleteUI, statsText);
            rewardTextField?.SetValue(questCompleteUI, rewardText);
            continueButtonField?.SetValue(questCompleteUI, continueButton);

            // Re-run EnsureReadableLayout now that reflection fields are set
            // (Awake ran before reflection, so text fields were null at that point)
            var ensureLayoutMethod = typeof(QuestCompleteUI).GetMethod("EnsureReadableLayout",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ensureLayoutMethod?.Invoke(questCompleteUI, null);

            // Setup button click handler - CRITICAL: Must be done AFTER field is set
            continueButton.onClick.RemoveAllListeners(); // Clear any existing listeners
            continueButton.onClick.AddListener(() =>
            {
                Debug.Log("[QuestCompleteUI] Continue button clicked");
                questCompleteUI.Hide();

                // Refresh quest list if QuestUIManager exists
                if (QuestUIManager.Instance != null)
                {
                    var refreshMethod = typeof(QuestUIManager).GetMethod("RefreshQuestList",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    refreshMethod?.Invoke(QuestUIManager.Instance, null);
                }
            });

            completeObj.SetActive(false); // Start hidden

            Debug.Log("[QuestUISetup] Created Quest Complete UI");
            return questCompleteUI;
        }
    }
}
