using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeliveryDriver.UIKit;

namespace DeliveryDriver.UI
{
    public class LoadingScreenUI : MonoBehaviour
    {
        private TextMeshProUGUI titleText;
        private Image progressFill;
        private TextMeshProUGUI tipText;
        private GameObject root;
        private bool built;

        private static readonly string[] TipKeys =
        {
            "tip_rain", "tip_drift", "tip_brake", "tip_fragile", "tip_shortcut"
        };

        public void Show()
        {
            EnsureBuilt();
            if (root != null) root.SetActive(true);
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (tipText != null) tipText.text = GetRandomTip();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void UpdateProgress(float progress)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(progress);
            }
        }

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            root = gameObject;

            // Background
            Image bg = root.GetComponent<Image>();
            if (bg == null) bg = root.AddComponent<Image>();
            bg.sprite = DDUIVisuals.LoadCityBackdrop();
            bg.color = bg.sprite != null ? Color.white : DDUITheme.Background;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.raycastTarget = true;

            bool turkish = LocalizationTable.CurrentLocale == LocalizationTable.TurkishLocale;
            DDUIVisuals.AddChrome(root.transform, turkish ? "YÜKLENİYOR" : "LOADING",
                turkish ? "ŞEHRE HAZIRLANILIYOR" : "PREPARING THE CITY");

            // Title
            GameObject titleObj = new GameObject("LoadingTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(root.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 0.32f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(100f, 0f);
            titleRect.sizeDelta = new Vector2(940f, 80f);

            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = turkish ? "ŞEHRE HAZIRLANILIYOR" : "PREPARING THE CITY";
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontSize = 40f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) titleText.font = TMP_Settings.defaultFontAsset;

            // Progress bar background
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0.21f);
            barBgRect.anchorMax = new Vector2(1f, 0.21f);
            barBgRect.pivot = new Vector2(0.5f, 0.5f);
            barBgRect.anchoredPosition = Vector2.zero;
            barBgRect.sizeDelta = new Vector2(-200f, 18f);
            barBg.GetComponent<Image>().color = DDUITheme.Border;

            // Progress fill
            GameObject fillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            progressFill = fillObj.GetComponent<Image>();
            progressFill.sprite = DDUIVisuals.LoadComponent("progress_fill_orange");
            progressFill.color = progressFill.sprite != null ? Color.white : DDUITheme.Primary;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            // Loading text
            GameObject loadingObj = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            loadingObj.transform.SetParent(root.transform, false);
            RectTransform loadingRect = loadingObj.GetComponent<RectTransform>();
            loadingRect.anchorMin = loadingRect.anchorMax = new Vector2(0f, 0.27f);
            loadingRect.pivot = new Vector2(0f, 0.5f);
            loadingRect.anchoredPosition = new Vector2(100f, 0f);
            loadingRect.sizeDelta = new Vector2(820f, 40f);

            TextMeshProUGUI loadingText = loadingObj.GetComponent<TextMeshProUGUI>();
            loadingText.text = LocalizationTable.Get("loading");
            loadingText.alignment = TextAlignmentOptions.Left;
            loadingText.fontSize = 22f;
            loadingText.color = DDUITheme.TextSecondary;
            if (TMP_Settings.defaultFontAsset != null) loadingText.font = TMP_Settings.defaultFontAsset;

            // Tip text
            GameObject tipObj = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tipObj.transform.SetParent(root.transform, false);
            RectTransform tipRect = tipObj.GetComponent<RectTransform>();
            tipRect.anchorMin = tipRect.anchorMax = new Vector2(0f, 0.14f);
            tipRect.pivot = new Vector2(0f, 0.5f);
            tipRect.anchoredPosition = new Vector2(100f, 0f);
            tipRect.sizeDelta = new Vector2(1000f, 60f);

            tipText = tipObj.GetComponent<TextMeshProUGUI>();
            tipText.alignment = TextAlignmentOptions.Left;
            tipText.fontSize = 20f;
            tipText.fontStyle = FontStyles.Italic;
            tipText.color = DDUITheme.TextSecondary;
            tipText.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null) tipText.font = TMP_Settings.defaultFontAsset;
        }

        private static string GetRandomTip()
        {
            string key = TipKeys[Random.Range(0, TipKeys.Length)];
            return LocalizationTable.Get(key);
        }
    }
}
