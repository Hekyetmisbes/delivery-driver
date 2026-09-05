using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akıllı Telefon "QuickPay Banka" Uygulaması.
    /// Oyuncunun mevcut parasını, toplam kariyer kazancını, seviyesini ve XP durumunu
    /// modern bir mobil bankacılık kartı tasarımıyla gösterir.
    /// </summary>
    public class SmartphoneBankApp : MonoBehaviour
    {
        private GameObject appRoot;
        private Sprite fallbackSprite;

        private TextMeshProUGUI balanceAmountText;
        private TextMeshProUGUI totalEarnedText;
        private TextMeshProUGUI completedJobsText;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI xpProgressText;
        private Image xpProgressBar;

        public void Initialize(Transform parent, Sprite fallback)
        {
            if (appRoot != null) return;
            fallbackSprite = fallback;

            appRoot = new GameObject("BankAppScreen", typeof(RectTransform));
            appRoot.transform.SetParent(parent, false);

            RectTransform appRect = appRoot.GetComponent<RectTransform>();
            appRect.anchorMin = Vector2.zero;
            appRect.anchorMax = Vector2.one;
            appRect.offsetMin = Vector2.zero;
            appRect.offsetMax = Vector2.zero;

            BuildBankContent(appRoot.transform);
            appRoot.SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (appRoot != null)
            {
                appRoot.SetActive(active);
                if (active)
                {
                    RefreshData();
                }
            }
        }

        public void RefreshData()
        {
            PlayerProgressionManager prog = PlayerProgressionManager.Instance;
            if (prog == null)
            {
                prog = FindFirstObjectByType<PlayerProgressionManager>();
            }

            int money = prog != null ? prog.CurrentMoney : 0;
            int totalEarned = prog != null ? prog.TotalMoneyEarned : 0;
            int completedJobs = prog != null ? prog.TotalQuestsCompleted : 0;
            int level = prog != null ? prog.CurrentLevel : 1;
            int currentXp = prog != null ? prog.CurrentXP : 0;
            int nextXp = prog != null ? Mathf.Max(1, prog.XPToNextLevel) : 100;

            if (balanceAmountText != null) balanceAmountText.text = $"{money:N0} TL";
            if (totalEarnedText != null) totalEarnedText.text = $"{totalEarned:N0} TL";
            if (completedJobsText != null) completedJobsText.text = $"{completedJobs} Teslimat";
            if (levelText != null) levelText.text = $"Seviye {level}";

            if (xpProgressText != null) xpProgressText.text = $"{currentXp} / {nextXp} XP";
            if (xpProgressBar != null)
            {
                float ratio = Mathf.Clamp01((float)currentXp / nextXp);
                xpProgressBar.fillAmount = ratio;
            }
        }

        private void BuildBankContent(Transform parent)
        {
            // App Header (QuickPay Banka)
            GameObject headerObj = new GameObject("AppHeader", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(parent, false);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -48f);
            hRect.sizeDelta = new Vector2(0f, 44f);

            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.08f, 0.14f, 0.22f, 0.98f);
            hImg.sprite = fallbackSprite;

            GameObject hIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            hIconObj.transform.SetParent(headerObj.transform, false);
            RectTransform hiRect = hIconObj.GetComponent<RectTransform>();
            hiRect.anchorMin = new Vector2(0f, 0.5f);
            hiRect.anchorMax = new Vector2(0f, 0.5f);
            hiRect.pivot = new Vector2(0f, 0.5f);
            hiRect.anchoredPosition = new Vector2(14f, 0f);
            hiRect.sizeDelta = new Vector2(24f, 24f);
            Image hiImg = hIconObj.GetComponent<Image>();
            hiImg.sprite = SmartphoneIconRegistry.GetBankAppIcon();
            hiImg.preserveAspect = true;

            GameObject hTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            hTitleObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = hTitleObj.GetComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = new Vector2(46f, 0f);
            htRect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI htText = hTitleObj.GetComponent<TextMeshProUGUI>();
            htText.text = "QuickPay Mobil Banka";
            htText.fontSize = 15f;
            htText.fontStyle = FontStyles.Bold;
            htText.alignment = TextAlignmentOptions.Left;
            htText.color = new Color(0.3f, 0.95f, 0.45f, 1f);

            // Virtual Debit Card Box
            GameObject cardObj = new GameObject("CreditCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(parent, false);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -104f);
            cardRect.sizeDelta = new Vector2(-28f, 150f);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.12f, 0.22f, 0.38f, 0.96f); // Rich sapphire debit card
            cardBg.sprite = fallbackSprite;

            // Chip / PayPass icon
            GameObject chipObj = new GameObject("CardChip", typeof(RectTransform), typeof(TextMeshProUGUI));
            chipObj.transform.SetParent(cardObj.transform, false);
            RectTransform chRect = chipObj.GetComponent<RectTransform>();
            chRect.anchorMin = new Vector2(0f, 1f);
            chRect.anchorMax = new Vector2(0f, 1f);
            chRect.pivot = new Vector2(0f, 1f);
            chRect.anchoredPosition = new Vector2(16f, -14f);
            chRect.sizeDelta = new Vector2(120f, 22f);
            TextMeshProUGUI chText = chipObj.GetComponent<TextMeshProUGUI>();
            chText.text = "DEBIT CARD  )))";
            chText.fontSize = 11f;
            chText.fontStyle = FontStyles.Bold;
            chText.color = new Color(0.8f, 0.9f, 1f, 0.8f);

            // Balance Label
            GameObject bLblObj = new GameObject("BalanceLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            bLblObj.transform.SetParent(cardObj.transform, false);
            RectTransform blRect = bLblObj.GetComponent<RectTransform>();
            blRect.anchorMin = new Vector2(0f, 0.5f);
            blRect.anchorMax = new Vector2(1f, 0.5f);
            blRect.pivot = new Vector2(0f, 0.5f);
            blRect.anchoredPosition = new Vector2(16f, 14f);
            blRect.sizeDelta = new Vector2(-32f, 18f);
            TextMeshProUGUI blText = bLblObj.GetComponent<TextMeshProUGUI>();
            blText.text = "Mevcut Bakiye";
            blText.fontSize = 11f;
            blText.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);

            // Balance Amount Big
            GameObject bAmtObj = new GameObject("BalanceAmount", typeof(RectTransform), typeof(TextMeshProUGUI));
            bAmtObj.transform.SetParent(cardObj.transform, false);
            RectTransform baRect = bAmtObj.GetComponent<RectTransform>();
            baRect.anchorMin = new Vector2(0f, 0.5f);
            baRect.anchorMax = new Vector2(1f, 0.5f);
            baRect.pivot = new Vector2(0f, 0.5f);
            baRect.anchoredPosition = new Vector2(16f, -14f);
            baRect.sizeDelta = new Vector2(-32f, 36f);
            balanceAmountText = bAmtObj.GetComponent<TextMeshProUGUI>();
            balanceAmountText.text = "0 TL";
            balanceAmountText.fontSize = 28f;
            balanceAmountText.fontStyle = FontStyles.Bold;
            balanceAmountText.color = new Color(0.35f, 1f, 0.55f, 1f);

            // Card Holder / Number
            GameObject numObj = new GameObject("CardNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(cardObj.transform, false);
            RectTransform nRect = numObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0f, 0f);
            nRect.anchorMax = new Vector2(1f, 0f);
            nRect.pivot = new Vector2(0f, 0f);
            nRect.anchoredPosition = new Vector2(16f, 12f);
            nRect.sizeDelta = new Vector2(-32f, 18f);
            TextMeshProUGUI nText = numObj.GetComponent<TextMeshProUGUI>();
            nText.text = "**** 4821   |   KURYE HESABI";
            nText.fontSize = 11f;
            nText.color = new Color(0.85f, 0.92f, 1f, 0.7f);

            // Statistics Panel
            GameObject statsObj = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image));
            statsObj.transform.SetParent(parent, false);
            RectTransform spRect = statsObj.GetComponent<RectTransform>();
            spRect.anchorMin = new Vector2(0f, 1f);
            spRect.anchorMax = new Vector2(1f, 1f);
            spRect.pivot = new Vector2(0.5f, 1f);
            spRect.anchoredPosition = new Vector2(0f, -266f);
            spRect.sizeDelta = new Vector2(-28f, 210f);

            Image spBg = statsObj.GetComponent<Image>();
            spBg.color = new Color(0.1f, 0.14f, 0.2f, 0.95f);
            spBg.sprite = fallbackSprite;

            // Stat 1: Total Money Earned
            totalEarnedText = CreateStatRow(statsObj.transform, "Kariyer Toplam Kazancı:", "0 TL", -16f);

            // Stat 2: Completed Deliveries
            completedJobsText = CreateStatRow(statsObj.transform, "Tamamlanan İşler:", "0", -56f);

            // Stat 3: Level & XP
            levelText = CreateStatRow(statsObj.transform, "Kurye Seviyesi:", "Seviye 1", -96f);

            // XP Bar Label
            GameObject xpLblObj = new GameObject("XPLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            xpLblObj.transform.SetParent(statsObj.transform, false);
            RectTransform xplRect = xpLblObj.GetComponent<RectTransform>();
            xplRect.anchorMin = new Vector2(0f, 1f);
            xplRect.anchorMax = new Vector2(1f, 1f);
            xplRect.anchoredPosition = new Vector2(14f, -140f);
            xplRect.sizeDelta = new Vector2(-28f, 18f);
            TextMeshProUGUI xplText = xpLblObj.GetComponent<TextMeshProUGUI>();
            xplText.text = "Sonraki Seviyeye İlerleme:";
            xplText.fontSize = 11f;
            xplText.color = new Color(0.7f, 0.82f, 0.95f, 0.9f);

            xpProgressText = CreateStatRow(statsObj.transform, "", "0 / 100 XP", -140f);

            // XP Progress Bar BG
            GameObject barBgObj = new GameObject("XPBarBg", typeof(RectTransform), typeof(Image));
            barBgObj.transform.SetParent(statsObj.transform, false);
            RectTransform bbRect = barBgObj.GetComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0f, 0f);
            bbRect.anchorMax = new Vector2(1f, 0f);
            bbRect.pivot = new Vector2(0.5f, 0f);
            bbRect.anchoredPosition = new Vector2(0f, 18f);
            bbRect.sizeDelta = new Vector2(-28f, 12f);

            Image bbImg = barBgObj.GetComponent<Image>();
            bbImg.color = new Color(0.05f, 0.08f, 0.12f, 1f);
            bbImg.sprite = fallbackSprite;

            // XP Progress Fill
            GameObject barFillObj = new GameObject("XPBarFill", typeof(RectTransform), typeof(Image));
            barFillObj.transform.SetParent(barBgObj.transform, false);
            RectTransform bfRect = barFillObj.GetComponent<RectTransform>();
            bfRect.anchorMin = Vector2.zero;
            bfRect.anchorMax = Vector2.one;
            bfRect.offsetMin = Vector2.zero;
            bfRect.offsetMax = Vector2.zero;

            xpProgressBar = barFillObj.GetComponent<Image>();
            xpProgressBar.color = new Color(0.3f, 0.95f, 0.45f, 1f);
            xpProgressBar.sprite = fallbackSprite;
            xpProgressBar.type = Image.Type.Filled;
            xpProgressBar.fillMethod = Image.FillMethod.Horizontal;
            xpProgressBar.fillAmount = 0.35f;
        }

        private TextMeshProUGUI CreateStatRow(Transform parent, string label, string defaultValue, float yOffset)
        {
            if (!string.IsNullOrEmpty(label))
            {
                GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(parent, false);
                RectTransform lRect = lblObj.GetComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0f, 1f);
                lRect.anchorMax = new Vector2(0.6f, 1f);
                lRect.pivot = new Vector2(0f, 1f);
                lRect.anchoredPosition = new Vector2(14f, yOffset);
                lRect.sizeDelta = new Vector2(0f, 22f);

                TextMeshProUGUI lText = lblObj.GetComponent<TextMeshProUGUI>();
                lText.text = label;
                lText.fontSize = 12f;
                lText.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
            }

            GameObject valObj = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            valObj.transform.SetParent(parent, false);
            RectTransform vRect = valObj.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.5f, 1f);
            vRect.anchorMax = new Vector2(1f, 1f);
            vRect.pivot = new Vector2(1f, 1f);
            vRect.anchoredPosition = new Vector2(-14f, yOffset);
            vRect.sizeDelta = new Vector2(0f, 22f);

            TextMeshProUGUI vText = valObj.GetComponent<TextMeshProUGUI>();
            vText.text = defaultValue;
            vText.fontSize = 12f;
            vText.fontStyle = FontStyles.Bold;
            vText.alignment = TextAlignmentOptions.Right;
            vText.color = Color.white;

            return vText;
        }
    }
}