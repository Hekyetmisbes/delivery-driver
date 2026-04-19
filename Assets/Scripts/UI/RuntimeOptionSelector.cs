using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public sealed class RuntimeOptionSelector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class OptionChangedEvent : UnityEvent<int> { }

        private readonly List<string> options = new List<string>();

        private Image backgroundImage;
        private Button previousButton;
        private Button nextButton;
        private TextMeshProUGUI valueText;
        private bool built;
        private bool isInteractable = true;
        private int currentValue;

        public OptionChangedEvent onValueChanged = new OptionChangedEvent();

        public int value
        {
            get => currentValue;
            set => SetValue(value, true);
        }

        public bool interactable
        {
            get => isInteractable;
            set
            {
                isInteractable = value;
                RefreshInteractableState();
            }
        }

        public void Initialize(IList<string> initialOptions, Sprite backgroundSprite = null)
        {
            if (!built)
            {
                BuildVisualTree(backgroundSprite);
            }

            ClearOptions();
            AddOptions(initialOptions);
            SetValueWithoutNotify(0);
            RefreshShownValue();
        }

        public void ClearOptions()
        {
            options.Clear();
            currentValue = 0;
            RefreshShownValue();
        }

        public void AddOptions(IList<string> newOptions)
        {
            if (newOptions == null)
            {
                RefreshShownValue();
                return;
            }

            for (int i = 0; i < newOptions.Count; i++)
            {
                options.Add(newOptions[i] ?? string.Empty);
            }

            currentValue = Mathf.Clamp(currentValue, 0, Mathf.Max(0, options.Count - 1));
            RefreshShownValue();
        }

        public void SetValueWithoutNotify(int index)
        {
            SetValue(index, false);
        }

        public void RefreshShownValue()
        {
            if (valueText != null)
            {
                valueText.text = options.Count > 0 && currentValue >= 0 && currentValue < options.Count
                    ? options[currentValue]
                    : "-";
            }

            RefreshInteractableState();
        }

        private void SetValue(int index, bool notify)
        {
            int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            if (currentValue == clamped && built)
            {
                RefreshShownValue();
                return;
            }

            currentValue = clamped;
            RefreshShownValue();

            if (notify)
            {
                onValueChanged?.Invoke(currentValue);
            }
        }

        private void BuildVisualTree(Sprite backgroundSprite)
        {
            built = true;

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }

            backgroundImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            backgroundImage.sprite = backgroundSprite != null ? backgroundSprite : DeliveryUiSpriteHelper.GetFallbackSprite();
            backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            previousButton = CreateArrowButton("PreviousButton", "<");
            valueText = CreateValueLabel();
            nextButton = CreateArrowButton("NextButton", ">");

            previousButton.onClick.AddListener(SelectPrevious);
            nextButton.onClick.AddListener(SelectNext);
        }

        private Button CreateArrowButton(string name, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(transform, false);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minWidth = 42f;
            layoutElement.preferredWidth = 42f;
            layoutElement.minHeight = 32f;
            layoutElement.preferredHeight = 32f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.22f, 0.96f);
            image.sprite = DeliveryUiSpriteHelper.GetFallbackSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.16f, 0.22f, 0.96f);
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.36f, 1f);
            colors.pressedColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.10f, 0.12f, 0.16f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = UIThemeConstants.TextPrimary;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            UIButtonEnhancer.EnhanceButton(button);
            return button;
        }

        private TextMeshProUGUI CreateValueLabel()
        {
            GameObject valueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            valueObject.transform.SetParent(transform, false);

            LayoutElement layoutElement = valueObject.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minWidth = 140f;
            layoutElement.preferredHeight = 32f;

            TextMeshProUGUI text = valueObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = UIThemeConstants.TextPrimary;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private void SelectPrevious()
        {
            if (!interactable || options.Count <= 1)
            {
                return;
            }

            int nextIndex = currentValue - 1;
            if (nextIndex < 0)
            {
                nextIndex = options.Count - 1;
            }

            value = nextIndex;
        }

        private void SelectNext()
        {
            if (!interactable || options.Count <= 1)
            {
                return;
            }

            int nextIndex = (currentValue + 1) % options.Count;
            value = nextIndex;
        }

        private void RefreshInteractableState()
        {
            bool buttonsEnabled = isInteractable && options.Count > 1;

            if (previousButton != null)
            {
                previousButton.interactable = buttonsEnabled;
            }

            if (nextButton != null)
            {
                nextButton.interactable = buttonsEnabled;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isInteractable
                    ? new Color(0.18f, 0.22f, 0.28f, 1f)
                    : new Color(0.16f, 0.18f, 0.22f, 0.72f);
            }

            if (valueText != null)
            {
                valueText.color = isInteractable ? UIThemeConstants.TextPrimary : UIThemeConstants.TextSecondary;
            }
        }
    }
}
