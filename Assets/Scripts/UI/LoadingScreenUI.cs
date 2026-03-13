using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            bg.color = new Color(0.03f, 0.05f, 0.08f, 1f);
            bg.raycastTarget = true;

            // Title
            GameObject titleObj = new GameObject("LoadingTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(root.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.55f);
            titleRect.anchorMax = new Vector2(0.5f, 0.55f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(600f, 80f);

            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = "DELIVERY DRIVER";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 48f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) titleText.font = TMP_Settings.defaultFontAsset;

            // Progress bar background
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.5f, 0.42f);
            barBgRect.anchorMax = new Vector2(0.5f, 0.42f);
            barBgRect.pivot = new Vector2(0.5f, 0.5f);
            barBgRect.sizeDelta = new Vector2(500f, 24f);
            barBg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.22f, 1f);

            // Progress fill
            GameObject fillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            progressFill = fillObj.GetComponent<Image>();
            progressFill.color = new Color(0.2f, 0.65f, 0.95f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            // Loading text
            GameObject loadingObj = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            loadingObj.transform.SetParent(root.transform, false);
            RectTransform loadingRect = loadingObj.GetComponent<RectTransform>();
            loadingRect.anchorMin = new Vector2(0.5f, 0.35f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.35f);
            loadingRect.pivot = new Vector2(0.5f, 0.5f);
            loadingRect.sizeDelta = new Vector2(400f, 40f);

            TextMeshProUGUI loadingText = loadingObj.GetComponent<TextMeshProUGUI>();
            loadingText.text = LocalizationTable.Get("loading");
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.fontSize = 22f;
            loadingText.color = new Color(0.7f, 0.75f, 0.8f, 1f);
            if (TMP_Settings.defaultFontAsset != null) loadingText.font = TMP_Settings.defaultFontAsset;

            // Tip text
            GameObject tipObj = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tipObj.transform.SetParent(root.transform, false);
            RectTransform tipRect = tipObj.GetComponent<RectTransform>();
            tipRect.anchorMin = new Vector2(0.5f, 0.12f);
            tipRect.anchorMax = new Vector2(0.5f, 0.12f);
            tipRect.pivot = new Vector2(0.5f, 0.5f);
            tipRect.sizeDelta = new Vector2(700f, 60f);

            tipText = tipObj.GetComponent<TextMeshProUGUI>();
            tipText.alignment = TextAlignmentOptions.Center;
            tipText.fontSize = 20f;
            tipText.fontStyle = FontStyles.Italic;
            tipText.color = new Color(0.6f, 0.65f, 0.7f, 1f);
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
