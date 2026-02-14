using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>
    /// Runtime UI for level/progression HUD and lightweight skill tree panel.
    /// </summary>
    public class ProgressionSkillTreeUI : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleSkillTreeKey = KeyCode.K;

        private GameObject rootCanvasObject;
        private GameObject skillTreeWindow;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI xpText;
        private TextMeshProUGUI moneyText;
        private Image xpFillImage;
        private TextMeshProUGUI skillPointsText;
        private TextMeshProUGUI rewardsText;
        private readonly Dictionary<DriverSkillType, TextMeshProUGUI> skillRankLabels = new Dictionary<DriverSkillType, TextMeshProUGUI>();
        private readonly Dictionary<DriverSkillType, Button> skillButtons = new Dictionary<DriverSkillType, Button>();

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            BindEvents();
            Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleSkillTreeKey) && skillTreeWindow != null)
            {
                skillTreeWindow.SetActive(!skillTreeWindow.activeSelf);
            }
        }

        private void OnDestroy()
        {
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnMoneyChanged.RemoveListener(OnProgressionChanged);
                PlayerProgressionManager.Instance.OnLevelUp.RemoveListener(OnProgressionChanged);
                PlayerProgressionManager.Instance.OnXPGained.RemoveListener(OnXPGained);
            }

            if (DriverProgressionSystem.Instance != null)
            {
                DriverProgressionSystem.Instance.OnSkillPointsChanged -= OnSkillPointsChanged;
                DriverProgressionSystem.Instance.OnSkillRankChanged -= OnSkillRankChanged;
                DriverProgressionSystem.Instance.OnLevelRewardUnlocked -= OnLevelRewardUnlocked;
            }
        }

        private void BindEvents()
        {
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.OnMoneyChanged.AddListener(OnProgressionChanged);
                PlayerProgressionManager.Instance.OnLevelUp.AddListener(OnProgressionChanged);
                PlayerProgressionManager.Instance.OnXPGained.AddListener(OnXPGained);
            }

            if (DriverProgressionSystem.Instance != null)
            {
                DriverProgressionSystem.Instance.OnSkillPointsChanged += OnSkillPointsChanged;
                DriverProgressionSystem.Instance.OnSkillRankChanged += OnSkillRankChanged;
                DriverProgressionSystem.Instance.OnLevelRewardUnlocked += OnLevelRewardUnlocked;
            }
        }

        private void OnProgressionChanged(int _)
        {
            Refresh();
        }

        private void OnXPGained(int _)
        {
            Refresh();
        }

        private void OnSkillPointsChanged(int _)
        {
            Refresh();
        }

        private void OnSkillRankChanged(DriverSkillType _, int __)
        {
            Refresh();
        }

        private void OnLevelRewardUnlocked(LevelRewardDefinition _)
        {
            Refresh();
        }

        private void BuildUI()
        {
            rootCanvasObject = new GameObject("ProgressionSkillTreeCanvas");
            Canvas canvas = rootCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            rootCanvasObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = rootCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildProgressHud(rootCanvasObject.transform);
            BuildSkillTreeWindow(rootCanvasObject.transform);
        }

        private void BuildProgressHud(Transform parent)
        {
            GameObject panel = CreatePanel("DriverProgressHUD", parent, new Vector2(20f, -20f), new Vector2(370f, 145f), TextAnchor.UpperLeft);
            levelText = CreateText("LevelText", panel.transform, "Seviye 1", 24, FontStyles.Bold);
            levelText.rectTransform.anchoredPosition = new Vector2(14f, -10f);

            moneyText = CreateText("MoneyText", panel.transform, "$0", 20, FontStyles.Normal);
            moneyText.rectTransform.anchoredPosition = new Vector2(14f, -42f);

            xpText = CreateText("XPText", panel.transform, "0 / 100 XP", 18, FontStyles.Normal);
            xpText.rectTransform.anchoredPosition = new Vector2(14f, -70f);

            GameObject xpBackground = new GameObject("XPBarBG");
            xpBackground.transform.SetParent(panel.transform, false);
            RectTransform bgRect = xpBackground.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(14f, -98f);
            bgRect.sizeDelta = new Vector2(340f, 20f);
            Image bgImage = xpBackground.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.5f);

            GameObject xpFill = new GameObject("XPBarFill");
            xpFill.transform.SetParent(xpBackground.transform, false);
            RectTransform fillRect = xpFill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            xpFillImage = xpFill.AddComponent<Image>();
            xpFillImage.type = Image.Type.Filled;
            xpFillImage.fillMethod = Image.FillMethod.Horizontal;
            xpFillImage.fillOrigin = 0;
            xpFillImage.fillAmount = 0f;
            xpFillImage.color = new Color(0.21f, 0.73f, 0.39f, 0.95f);

            CreateText("HintText", panel.transform, $"Skill Tree: [{toggleSkillTreeKey}]", 16, FontStyles.Italic).rectTransform.anchoredPosition = new Vector2(14f, -124f);
        }

        private void BuildSkillTreeWindow(Transform parent)
        {
            skillTreeWindow = CreatePanel("SkillTreeWindow", parent, new Vector2(-20f, -20f), new Vector2(460f, 520f), TextAnchor.UpperRight);
            skillTreeWindow.SetActive(true);

            TextMeshProUGUI title = CreateText("Title", skillTreeWindow.transform, "Yetenek Agaci", 28, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            title.rectTransform.sizeDelta = new Vector2(0f, 36f);

            skillPointsText = CreateText("SkillPoints", skillTreeWindow.transform, "Yet.Puan: 0", 19, FontStyles.Bold);
            skillPointsText.rectTransform.anchoredPosition = new Vector2(16f, -52f);

            float y = -96f;
            foreach (DriverSkillType skill in new[] { DriverSkillType.FuelEfficiency, DriverSkillType.CargoDurability, DriverSkillType.RouteAssist })
            {
                GameObject row = CreatePanel($"Skill_{skill}", skillTreeWindow.transform, new Vector2(12f, y), new Vector2(436f, 92f), TextAnchor.UpperLeft, new Color(0f, 0f, 0f, 0.32f));

                TextMeshProUGUI nameLabel = CreateText("Name", row.transform, skill.ToString(), 19, FontStyles.Bold);
                nameLabel.rectTransform.anchoredPosition = new Vector2(10f, -8f);

                TextMeshProUGUI rankLabel = CreateText("Rank", row.transform, "Rank 0/3", 16, FontStyles.Normal);
                rankLabel.rectTransform.anchoredPosition = new Vector2(10f, -35f);
                skillRankLabels[skill] = rankLabel;

                Button button = CreateButton("UpgradeButton", row.transform, "+", new Vector2(340f, -23f), new Vector2(78f, 42f));
                DriverSkillType captured = skill;
                button.onClick.AddListener(() => TryUpgradeSkill(captured));
                skillButtons[skill] = button;

                y -= 102f;
            }

            TextMeshProUGUI rewardsTitle = CreateText("RewardsTitle", skillTreeWindow.transform, "Seviye Odulleri", 20, FontStyles.Bold);
            rewardsTitle.rectTransform.anchoredPosition = new Vector2(16f, y - 2f);

            rewardsText = CreateText("RewardsText", skillTreeWindow.transform, "-", 16, FontStyles.Normal);
            rewardsText.enableWordWrapping = true;
            rewardsText.alignment = TextAlignmentOptions.TopLeft;
            rewardsText.rectTransform.anchorMin = new Vector2(0f, 0f);
            rewardsText.rectTransform.anchorMax = new Vector2(1f, 0f);
            rewardsText.rectTransform.pivot = new Vector2(0.5f, 0f);
            rewardsText.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            rewardsText.rectTransform.sizeDelta = new Vector2(-24f, 160f);
        }

        private void TryUpgradeSkill(DriverSkillType skillType)
        {
            if (DriverProgressionSystem.Instance == null)
            {
                return;
            }

            if (DriverProgressionSystem.Instance.TryUnlockSkill(skillType))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            PlayerProgressionManager progression = PlayerProgressionManager.Instance;
            DriverProgressionSystem driverProgression = DriverProgressionSystem.Instance;

            if (progression != null)
            {
                if (levelText != null)
                {
                    levelText.text = $"Seviye {progression.CurrentLevel}";
                }

                if (moneyText != null)
                {
                    moneyText.text = $"Bakiye: ${progression.CurrentMoney:N0}";
                }

                if (xpText != null)
                {
                    xpText.text = $"{progression.CurrentXP} / {progression.XPToNextLevel} XP";
                }

                if (xpFillImage != null)
                {
                    xpFillImage.fillAmount = progression.GetLevelProgressPercentage();
                }
            }

            if (driverProgression == null)
            {
                return;
            }

            if (skillPointsText != null)
            {
                skillPointsText.text = $"Yet. Puan: {driverProgression.UnspentSkillPoints}";
            }

            foreach (SkillNodeState node in driverProgression.Skills)
            {
                if (node == null)
                {
                    continue;
                }

                if (skillRankLabels.TryGetValue(node.SkillType, out TextMeshProUGUI rankLabel) && rankLabel != null)
                {
                    rankLabel.text = $"Rank {node.Rank}/{node.MaxRank}";
                }

                if (skillButtons.TryGetValue(node.SkillType, out Button button) && button != null)
                {
                    button.interactable = driverProgression.UnspentSkillPoints > 0 && node.Rank < node.MaxRank;
                }
            }

            if (rewardsText != null)
            {
                IEnumerable<string> unlocked = driverProgression.LevelRewards
                    .Where(reward => reward != null && driverProgression.IsRewardUnlocked(reward.RewardId))
                    .OrderBy(reward => reward.RequiredLevel)
                    .Select(reward => $"Lv.{reward.RequiredLevel} - {reward.Title}: {reward.Description}");

                rewardsText.text = unlocked.Any()
                    ? string.Join("\n", unlocked)
                    : "Henuz acilan odul yok.";
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor anchor,
            Color? color = null)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rectTransform = panel.AddComponent<RectTransform>();

            switch (anchor)
            {
                case TextAnchor.UpperRight:
                    rectTransform.anchorMin = new Vector2(1f, 1f);
                    rectTransform.anchorMax = new Vector2(1f, 1f);
                    rectTransform.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.UpperLeft:
                default:
                    rectTransform.anchorMin = new Vector2(0f, 1f);
                    rectTransform.anchorMax = new Vector2(0f, 1f);
                    rectTransform.pivot = new Vector2(0f, 1f);
                    break;
            }

            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panel.AddComponent<Image>();
            image.color = color ?? new Color(0.06f, 0.08f, 0.12f, 0.78f);
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            RectTransform rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(0f, 24f);

            TextMeshProUGUI textUi = textObject.AddComponent<TextMeshProUGUI>();
            textUi.text = text;
            textUi.fontSize = fontSize;
            textUi.fontStyle = style;
            textUi.color = new Color(0.95f, 0.96f, 0.97f, 1f);
            textUi.alignment = TextAlignmentOptions.TopLeft;
            return textUi;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.49f, 0.25f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.49f, 0.25f, 0.95f);
            colors.highlightedColor = new Color(0.19f, 0.62f, 0.31f, 1f);
            colors.pressedColor = new Color(0.09f, 0.35f, 0.19f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            button.colors = colors;

            TextMeshProUGUI buttonLabel = CreateText("Label", buttonObject.transform, label, 24, FontStyles.Bold);
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            buttonLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            buttonLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            buttonLabel.rectTransform.anchoredPosition = Vector2.zero;
            buttonLabel.rectTransform.sizeDelta = Vector2.zero;

            return button;
        }
    }
}
