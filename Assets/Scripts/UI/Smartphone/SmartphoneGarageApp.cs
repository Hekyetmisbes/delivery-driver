using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akıllı Telefon "Garaj & Filo" Uygulaması.
    /// Aktif teslimat aracını, kat edilen kilometreyi ve araç performans istatistiklerini gösterir.
    /// </summary>
    public class SmartphoneGarageApp : MonoBehaviour
    {
        private GameObject appRoot;
        private Sprite fallbackSprite;

        private TextMeshProUGUI vehicleNameText;
        private TextMeshProUGUI vehicleStatusText;
        private TextMeshProUGUI totalKmText;
        private TextMeshProUGUI speedBonusesText;
        private TextMeshProUGUI sRankCountText;

        public void Initialize(Transform parent, Sprite fallback)
        {
            if (appRoot != null) return;
            fallbackSprite = fallback;

            appRoot = new GameObject("GarageAppScreen", typeof(RectTransform));
            appRoot.transform.SetParent(parent, false);

            RectTransform appRect = appRoot.GetComponent<RectTransform>();
            appRect.anchorMin = Vector2.zero;
            appRect.anchorMax = Vector2.one;
            appRect.offsetMin = Vector2.zero;
            appRect.offsetMax = Vector2.zero;

            BuildGarageContent(appRoot.transform);
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

            float distanceKm = prog != null ? prog.TotalDistanceTraveled / 1000f : 0f;
            int speedBonuses = prog != null ? prog.SpeedBonusesEarned : 0;
            int sRanks = prog != null ? prog.SRanksAchieved : 0;

            if (vehicleNameText != null) vehicleNameText.text = "Standart Kurye Vanı (V1)";
            if (vehicleStatusText != null) vehicleStatusText.text = "[+] Aktif & Hazır Durumda";
            if (totalKmText != null) totalKmText.text = $"{distanceKm:F1} km";
            if (speedBonusesText != null) speedBonusesText.text = $"{speedBonuses} Kez";
            if (sRankCountText != null) sRankCountText.text = $"{sRanks} Teslimat";
        }

        private void BuildGarageContent(Transform parent)
        {
            // App Header
            GameObject headerObj = new GameObject("AppHeader", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(parent, false);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -48f);
            hRect.sizeDelta = new Vector2(0f, 44f);

            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.18f, 0.14f, 0.08f, 0.98f);
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
            hiImg.sprite = SmartphoneIconRegistry.GetGarageAppIcon();
            hiImg.preserveAspect = true;

            GameObject hTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            hTitleObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = hTitleObj.GetComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = new Vector2(46f, 0f);
            htRect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI htText = hTitleObj.GetComponent<TextMeshProUGUI>();
            htText.text = "Garajım & Araç Durumu";
            htText.fontSize = 15f;
            htText.fontStyle = FontStyles.Bold;
            htText.alignment = TextAlignmentOptions.Left;
            htText.color = new Color(1f, 0.75f, 0.2f, 1f);

            // Vehicle Card
            GameObject vCardObj = new GameObject("VehicleCard", typeof(RectTransform), typeof(Image));
            vCardObj.transform.SetParent(parent, false);
            RectTransform vcRect = vCardObj.GetComponent<RectTransform>();
            vcRect.anchorMin = new Vector2(0f, 1f);
            vcRect.anchorMax = new Vector2(1f, 1f);
            vcRect.pivot = new Vector2(0.5f, 1f);
            vcRect.anchoredPosition = new Vector2(0f, -104f);
            vcRect.sizeDelta = new Vector2(-28f, 140f);

            Image vcBg = vCardObj.GetComponent<Image>();
            vcBg.color = new Color(0.13f, 0.17f, 0.24f, 0.95f);
            vcBg.sprite = fallbackSprite;

            GameObject vIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            vIconObj.transform.SetParent(vCardObj.transform, false);
            RectTransform viRect = vIconObj.GetComponent<RectTransform>();
            viRect.anchorMin = new Vector2(0.5f, 1f);
            viRect.anchorMax = new Vector2(0.5f, 1f);
            viRect.pivot = new Vector2(0.5f, 1f);
            viRect.anchoredPosition = new Vector2(0f, -10f);
            viRect.sizeDelta = new Vector2(50f, 50f);
            Image viImg = vIconObj.GetComponent<Image>();
            viImg.sprite = SmartphoneIconRegistry.GetGarageAppIcon();
            viImg.preserveAspect = true;

            GameObject vNameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            vNameObj.transform.SetParent(vCardObj.transform, false);
            RectTransform vnRect = vNameObj.GetComponent<RectTransform>();
            vnRect.anchorMin = new Vector2(0f, 0f);
            vnRect.anchorMax = new Vector2(1f, 0f);
            vnRect.pivot = new Vector2(0.5f, 0f);
            vnRect.anchoredPosition = new Vector2(0f, 38f);
            vnRect.sizeDelta = new Vector2(-20f, 26f);
            vehicleNameText = vNameObj.GetComponent<TextMeshProUGUI>();
            vehicleNameText.text = "Standart Kurye Vanı";
            vehicleNameText.fontSize = 16f;
            vehicleNameText.fontStyle = FontStyles.Bold;
            vehicleNameText.alignment = TextAlignmentOptions.Center;
            vehicleNameText.color = Color.white;

            GameObject vStatObj = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            vStatObj.transform.SetParent(vCardObj.transform, false);
            RectTransform vsRect = vStatObj.GetComponent<RectTransform>();
            vsRect.anchorMin = new Vector2(0f, 0f);
            vsRect.anchorMax = new Vector2(1f, 0f);
            vsRect.pivot = new Vector2(0.5f, 0f);
            vsRect.anchoredPosition = new Vector2(0f, 14f);
            vsRect.sizeDelta = new Vector2(-20f, 20f);
            vehicleStatusText = vStatObj.GetComponent<TextMeshProUGUI>();
            vehicleStatusText.text = "[+] Aktif & Hazır Durumda";
            vehicleStatusText.fontSize = 12f;
            vehicleStatusText.alignment = TextAlignmentOptions.Center;
            vehicleStatusText.color = new Color(0.2f, 1f, 0.45f, 0.95f);

            // Driving Stats Box
            GameObject dStatsObj = new GameObject("DrivingStats", typeof(RectTransform), typeof(Image));
            dStatsObj.transform.SetParent(parent, false);
            RectTransform dsRect = dStatsObj.GetComponent<RectTransform>();
            dsRect.anchorMin = new Vector2(0f, 1f);
            dsRect.anchorMax = new Vector2(1f, 1f);
            dsRect.pivot = new Vector2(0.5f, 1f);
            dsRect.anchoredPosition = new Vector2(0f, -256f);
            dsRect.sizeDelta = new Vector2(-28f, 170f);

            Image dsBg = dStatsObj.GetComponent<Image>();
            dsBg.color = new Color(0.1f, 0.14f, 0.2f, 0.95f);
            dsBg.sprite = fallbackSprite;

            totalKmText = CreateRow(dStatsObj.transform, "Toplam Sürüş:", "0.0 km", -16f);
            speedBonusesText = CreateRow(dStatsObj.transform, "Hız Bonusları:", "0", -56f);
            sRankCountText = CreateRow(dStatsObj.transform, "S-Rank Başarıları:", "0", -96f);

            // Tuning Tip
            GameObject tipObj = new GameObject("Tip", typeof(RectTransform), typeof(TextMeshProUGUI));
            tipObj.transform.SetParent(dStatsObj.transform, false);
            RectTransform tRect = tipObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0f);
            tRect.anchorMax = new Vector2(1f, 0f);
            tRect.anchoredPosition = new Vector2(0f, 14f);
            tRect.sizeDelta = new Vector2(-28f, 22f);
            TextMeshProUGUI tTmp = tipObj.GetComponent<TextMeshProUGUI>();
            tTmp.text = "[!] Para biriktirerek yeni araçlar satın alabilirsiniz!";
            tTmp.fontSize = 11f;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.color = new Color(0.7f, 0.85f, 0.95f, 0.75f);
        }

        private TextMeshProUGUI CreateRow(Transform parent, string label, string defaultValue, float yOffset)
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