using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    /// <summary>
    /// Shared UI creation methods extracted from PauseMenuUI and MainMenuRuntimeUI.
    /// </summary>
    public static class UIBuilderHelper
    {
        public static GameObject CreatePanel(Transform parent, string name, Vector2 size, Sprite bgSprite = null)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Image image = panel.GetComponent<Image>();
            image.color = UIThemeConstants.PanelBackground;
            if (bgSprite != null)
            {
                image.sprite = bgSprite;
                image.type = Image.Type.Sliced;
            }

            panel.SetActive(false);
            return panel;
        }

        public static Button CreateMenuButton(Transform parent, string label, Color color, Sprite bgSprite = null)
        {
            GameObject buttonObj = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObj.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObj.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = UIThemeConstants.ButtonHeight;

            Image buttonImage = buttonObj.GetComponent<Image>();
            buttonImage.color = color;
            if (bgSprite != null)
            {
                buttonImage.sprite = bgSprite;
                buttonImage.type = Image.Type.Sliced;
            }

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(buttonObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = UIThemeConstants.HeadingFontSize;
            text.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;

            return buttonObj.GetComponent<Button>();
        }

        public static Slider CreateLabeledSlider(Transform parent, string label, Sprite bg = null, Sprite fill = null, Sprite handle = null)
        {
            GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            row.GetComponent<LayoutElement>().preferredHeight = 74f;
            VerticalLayoutGroup v = row.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            CreateLabel(row.transform, label, UIThemeConstants.BodyFontSize);

            GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
            sliderObj.transform.SetParent(row.transform, false);
            sliderObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
            if (bg != null)
            {
                Image sliderImage = sliderObj.GetComponent<Image>();
                sliderImage.sprite = bg;
                sliderImage.type = Image.Type.Sliced;
            }

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Fill area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(10f, 6f);
            fillAreaRect.offsetMax = new Vector2(-10f, -6f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            fillGo.GetComponent<Image>().color = new Color(0.2f, 0.62f, 0.95f, 1f);
            if (fill != null)
            {
                Image fi = fillGo.GetComponent<Image>();
                fi.sprite = fill;
                fi.type = Image.Type.Sliced;
            }
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            handleGo.GetComponent<Image>().color = Color.white;
            if (handle != null)
            {
                Image hi = handleGo.GetComponent<Image>();
                hi.sprite = handle;
                hi.type = Image.Type.Simple;
                hi.preserveAspect = true;
            }
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 28f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        public static TMP_Dropdown CreateLabeledDropdown(Transform parent, string label, List<string> options, Sprite bg = null)
        {
            GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            row.GetComponent<LayoutElement>().preferredHeight = 64f;
            VerticalLayoutGroup v = row.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            CreateLabel(row.transform, label, UIThemeConstants.BodyFontSize);

            GameObject ddObj = new GameObject("Dropdown", typeof(RectTransform), typeof(TMP_Dropdown), typeof(Image));
            ddObj.transform.SetParent(row.transform, false);
            ddObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);
            if (bg != null)
            {
                Image di = ddObj.GetComponent<Image>();
                di.sprite = bg;
                di.type = Image.Type.Sliced;
            }

            // Caption
            GameObject captionObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            captionObj.transform.SetParent(ddObj.transform, false);
            RectTransform capRect = captionObj.GetComponent<RectTransform>();
            capRect.anchorMin = Vector2.zero;
            capRect.anchorMax = Vector2.one;
            capRect.offsetMin = new Vector2(8f, 0f);
            capRect.offsetMax = new Vector2(-30f, 0f);
            TextMeshProUGUI captionText = captionObj.GetComponent<TextMeshProUGUI>();
            captionText.fontSize = UIThemeConstants.SmallFontSize;
            captionText.color = Color.white;
            captionText.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null) captionText.font = TMP_Settings.defaultFontAsset;

            // Template
            GameObject templateObj = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateObj.transform.SetParent(ddObj.transform, false);
            templateObj.SetActive(false);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportObj.transform.SetParent(templateObj.transform, false);
            viewportObj.GetComponent<Image>().color = Color.white;
            viewportObj.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);

            GameObject itemObj = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObj.transform.SetParent(contentObj.transform, false);

            new GameObject("Item Background", typeof(RectTransform), typeof(Image)).transform.SetParent(itemObj.transform, false);

            GameObject itemLabelObj = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            TextMeshProUGUI itemText = itemLabelObj.GetComponent<TextMeshProUGUI>();
            itemText.fontSize = UIThemeConstants.SmallFontSize;
            itemText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) itemText.font = TMP_Settings.defaultFontAsset;

            TMP_Dropdown dropdown = ddObj.GetComponent<TMP_Dropdown>();
            dropdown.template = templateObj.GetComponent<RectTransform>();
            dropdown.captionText = captionText;
            dropdown.itemText = itemText;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            return dropdown;
        }

        public static Toggle CreateLabeledToggle(Transform parent, string label, Sprite bg = null)
        {
            GameObject row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            row.GetComponent<LayoutElement>().preferredHeight = 38f;
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObj.transform.SetParent(row.transform, false);
            labelObj.GetComponent<LayoutElement>().preferredWidth = 230f;
            TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = UIThemeConstants.BodyFontSize;
            labelText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) labelText.font = TMP_Settings.defaultFontAsset;

            GameObject toggleObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image));
            toggleObj.transform.SetParent(row.transform, false);
            Image tbg = toggleObj.GetComponent<Image>();
            tbg.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            if (bg != null)
            {
                tbg.sprite = bg;
                tbg.type = Image.Type.Sliced;
            }

            GameObject checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObj.transform.SetParent(toggleObj.transform, false);
            Image check = checkObj.GetComponent<Image>();
            check.color = new Color(0.2f, 0.62f, 0.95f, 1f);
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            Toggle toggle = toggleObj.GetComponent<Toggle>();
            toggle.targetGraphic = tbg;
            toggle.graphic = check;

            return toggle;
        }

        public static void CreateTitle(Transform parent, string title)
        {
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleObj.transform.SetParent(parent, false);
            titleObj.GetComponent<LayoutElement>().preferredHeight = 72f;

            TextMeshProUGUI text = titleObj.GetComponent<TextMeshProUGUI>();
            text.text = title;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = UIThemeConstants.TitleFontSize;
            text.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        }

        public static Transform CreateSectionContainer(Transform parent, string name, Sprite bgSprite = null)
        {
            GameObject sectionObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            sectionObj.transform.SetParent(parent, false);

            Image sectionImage = sectionObj.GetComponent<Image>();
            sectionImage.color = UIThemeConstants.SectionBackground;
            if (bgSprite != null)
            {
                sectionImage.sprite = bgSprite;
                sectionImage.type = Image.Type.Sliced;
            }

            VerticalLayoutGroup layout = sectionObj.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            sectionObj.GetComponent<LayoutElement>().minHeight = UIThemeConstants.SectionMinHeight;

            return sectionObj.transform;
        }

        private static void CreateLabel(Transform parent, string text, float fontSize)
        {
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(parent, false);
            TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
            labelText.text = text;
            labelText.fontSize = fontSize;
            labelText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) labelText.font = TMP_Settings.defaultFontAsset;
        }
    }
}
