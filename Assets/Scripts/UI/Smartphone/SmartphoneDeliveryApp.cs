using System;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akıllı Telefon "Kurye Express" Uygulaması.
    /// Rahatsız edici pop-up'ların yerini alan, gelen kargo tekliflerini şık kartlar halinde listeleyen
    /// ve aktif teslimat durumunu gösteren ana kurye uygulaması.
    /// </summary>
    public class SmartphoneDeliveryApp : MonoBehaviour
    {
        private GameObject appRoot;
        private Sprite fallbackSprite;

        // Pending Offer UI elements
        private GameObject offerCardObj;
        private TextMeshProUGUI offerTitleText;
        private TextMeshProUGUI offerRouteText;
        private TextMeshProUGUI offerRewardText;
        private TextMeshProUGUI offerDescText;
        private GameObject cargoTypeBadgeObj;
        private TextMeshProUGUI cargoTypeBadgeText;
        private Image cargoTypeBadgeBg;
        private Button acceptButton;
        private Button rejectButton;

        // Active Delivery UI elements
        private GameObject activeDeliveryCardObj;
        private TextMeshProUGUI activeTargetText;
        private TextMeshProUGUI activeStatusText;
        private TextMeshProUGUI activeRewardText;

        // Empty / Scanning state UI elements
        private GameObject emptyStateObj;

        // Current state
        private Action currentAcceptAction;
        private Action currentRejectAction;
        private bool hasPendingOffer;

        public bool HasPendingOffer => hasPendingOffer;

        public void Initialize(Transform parent, Sprite fallback)
        {
            if (appRoot != null) return;
            fallbackSprite = fallback;

            appRoot = new GameObject("DeliveryAppScreen", typeof(RectTransform));
            appRoot.transform.SetParent(parent, false);

            RectTransform appRect = appRoot.GetComponent<RectTransform>();
            appRect.anchorMin = Vector2.zero;
            appRect.anchorMax = Vector2.one;
            appRect.offsetMin = Vector2.zero;
            appRect.offsetMax = Vector2.zero;

            BuildAppContent(appRoot.transform);
            appRoot.SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (appRoot != null)
            {
                appRoot.SetActive(active);
                if (active)
                {
                    RefreshView();
                }
            }
        }

        public void SetOffer(string title, string body, string reward, Action onAccept, Action onReject)
        {
            hasPendingOffer = true;
            currentAcceptAction = onAccept;
            currentRejectAction = onReject;

            if (offerTitleText != null) offerTitleText.text = string.IsNullOrWhiteSpace(title) ? "Kargo Teslimatı" : title;
            if (offerDescText != null) offerDescText.text = body;
            if (offerRewardText != null) offerRewardText.text = string.IsNullOrWhiteSpace(reward) ? "₺150" : reward;

            // Route & Cargo Type extraction / formatting
            if (offerRouteText != null)
            {
                offerRouteText.text = "📍 Şehir İçi Rota ➔ Teslimat Noktası";
            }

            if (cargoTypeBadgeText != null && cargoTypeBadgeBg != null)
            {
                string upper = (title + " " + body).ToUpperInvariant();
                if (upper.Contains("KIRILABİLİR") || upper.Contains("FRAGILE") || upper.Contains("HASSAS"))
                {
                    cargoTypeBadgeText.text = "KIRILABİLİR";
                    cargoTypeBadgeBg.color = new Color(0.95f, 0.45f, 0.15f, 1f);
                }
                else if (upper.Contains("ACİL") || upper.Contains("TIMED") || upper.Contains("ZAMANLI"))
                {
                    cargoTypeBadgeText.text = "ACİL KARGO";
                    cargoTypeBadgeBg.color = new Color(0.95f, 0.2f, 0.2f, 1f);
                }
                else if (upper.Contains("ÇOKLU") || upper.Contains("MULTI"))
                {
                    cargoTypeBadgeText.text = "ÇOKLU TESLİMAT";
                    cargoTypeBadgeBg.color = new Color(0.6f, 0.25f, 0.95f, 1f);
                }
                else
                {
                    cargoTypeBadgeText.text = "STANDART";
                    cargoTypeBadgeBg.color = new Color(0.15f, 0.6f, 0.95f, 1f);
                }
            }

            RefreshView();
        }

        public void ClearOffer()
        {
            hasPendingOffer = false;
            currentAcceptAction = null;
            currentRejectAction = null;
            RefreshView();
        }

        public void RefreshView()
        {
            DeliveryManager deliveryMgr = FindFirstObjectByType<DeliveryManager>();
            bool isDelivering = deliveryMgr != null && (deliveryMgr.IsDeliveryActive || deliveryMgr.HasBox);

            if (offerCardObj != null) offerCardObj.SetActive(hasPendingOffer && !isDelivering);
            if (activeDeliveryCardObj != null)
            {
                activeDeliveryCardObj.SetActive(isDelivering);
                if (isDelivering)
                {
                    UpdateActiveDeliveryInfo(deliveryMgr);
                }
            }
            if (emptyStateObj != null) emptyStateObj.SetActive(!hasPendingOffer && !isDelivering);
        }

        private void UpdateActiveDeliveryInfo(DeliveryManager deliveryMgr)
        {
            if (deliveryMgr == null) return;

            if (activeTargetText != null)
            {
                string targetArea = !string.IsNullOrEmpty(deliveryMgr.CurrentDeliveryNeighborhoodName)
                    ? deliveryMgr.CurrentDeliveryNeighborhoodName
                    : "Teslimat Bölgesi";
                activeTargetText.text = $"Hedef: {targetArea}";
            }

            if (activeStatusText != null)
            {
                if (deliveryMgr.IsCarryingBox)
                {
                    activeStatusText.text = "📦 Kargo Aracınızda - Adrese Ulaştırın";
                    activeStatusText.color = new Color(0.2f, 0.95f, 0.45f, 1f);
                }
                else
                {
                    activeStatusText.text = "📍 Yükleme Noktasına Gidin (Kutuyu Alın)";
                    activeStatusText.color = new Color(1f, 0.85f, 0.2f, 1f);
                }
            }

            if (activeRewardText != null)
            {
                activeRewardText.text = "💰 Teslimat Tamamlanınca Bakiye Yüklenecek";
            }
        }

        private void BuildAppContent(Transform parent)
        {
            // App Header Bar (Kurye Express)
            GameObject headerObj = new GameObject("AppHeader", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(parent, false);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -48f); // Below phone status bar
            hRect.sizeDelta = new Vector2(0f, 44f);

            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.09f, 0.13f, 0.2f, 0.98f);
            hImg.sprite = fallbackSprite;

            GameObject hTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            hTitleObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = hTitleObj.GetComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = new Vector2(16f, 0f);
            htRect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI htText = hTitleObj.GetComponent<TextMeshProUGUI>();
            htText.text = "📦 Kurye Express - İlanlar";
            htText.fontSize = 15f;
            htText.fontStyle = FontStyles.Bold;
            htText.alignment = TextAlignmentOptions.Left;
            htText.color = new Color(0.3f, 0.95f, 0.45f, 1f);

            // 1. Pending Offer Card
            offerCardObj = new GameObject("OfferCard", typeof(RectTransform), typeof(Image));
            offerCardObj.transform.SetParent(parent, false);
            RectTransform cardRect = offerCardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 10f);
            cardRect.sizeDelta = new Vector2(-28f, 330f);

            Image cardBg = offerCardObj.GetComponent<Image>();
            cardBg.color = new Color(0.11f, 0.16f, 0.24f, 0.95f);
            cardBg.sprite = fallbackSprite;

            // Badge for Cargo Type
            cargoTypeBadgeObj = new GameObject("CargoBadge", typeof(RectTransform), typeof(Image));
            cargoTypeBadgeObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform badgeRect = cargoTypeBadgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(14f, -12f);
            badgeRect.sizeDelta = new Vector2(110f, 22f);

            cargoTypeBadgeBg = cargoTypeBadgeObj.GetComponent<Image>();
            cargoTypeBadgeBg.color = new Color(0.15f, 0.6f, 0.95f, 1f);
            cargoTypeBadgeBg.sprite = fallbackSprite;

            GameObject badgeTextObj = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextObj.transform.SetParent(cargoTypeBadgeObj.transform, false);
            RectTransform btRect = badgeTextObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            cargoTypeBadgeText = badgeTextObj.GetComponent<TextMeshProUGUI>();
            cargoTypeBadgeText.text = "STANDART";
            cargoTypeBadgeText.fontSize = 11f;
            cargoTypeBadgeText.fontStyle = FontStyles.Bold;
            cargoTypeBadgeText.alignment = TextAlignmentOptions.Center;
            cargoTypeBadgeText.color = Color.white;

            // Offer Title (Customer / Merchant)
            GameObject titleObj = new GameObject("OfferTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform otRect = titleObj.GetComponent<RectTransform>();
            otRect.anchorMin = new Vector2(0f, 1f);
            otRect.anchorMax = new Vector2(1f, 1f);
            otRect.pivot = new Vector2(0f, 1f);
            otRect.anchoredPosition = new Vector2(14f, -40f);
            otRect.sizeDelta = new Vector2(-28f, 26f);

            offerTitleText = titleObj.GetComponent<TextMeshProUGUI>();
            offerTitleText.text = "Kızılay Medikal Deposu";
            offerTitleText.fontSize = 16f;
            offerTitleText.fontStyle = FontStyles.Bold;
            offerTitleText.color = Color.white;

            // Offer Route Info
            GameObject routeObj = new GameObject("OfferRoute", typeof(RectTransform), typeof(TextMeshProUGUI));
            routeObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform rtRect = routeObj.GetComponent<RectTransform>();
            rtRect.anchorMin = new Vector2(0f, 1f);
            rtRect.anchorMax = new Vector2(1f, 1f);
            rtRect.pivot = new Vector2(0f, 1f);
            rtRect.anchoredPosition = new Vector2(14f, -68f);
            rtRect.sizeDelta = new Vector2(-28f, 22f);

            offerRouteText = routeObj.GetComponent<TextMeshProUGUI>();
            offerRouteText.text = "📍 Şehir İçi Rota";
            offerRouteText.fontSize = 13f;
            offerRouteText.color = new Color(0.35f, 0.85f, 1f, 1f);

            // Offer Description Body
            GameObject descObj = new GameObject("OfferDesc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform dtRect = descObj.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0f, 1f);
            dtRect.anchorMax = new Vector2(1f, 1f);
            dtRect.pivot = new Vector2(0f, 1f);
            dtRect.anchoredPosition = new Vector2(14f, -94f);
            dtRect.sizeDelta = new Vector2(-28f, 90f);

            offerDescText = descObj.GetComponent<TextMeshProUGUI>();
            offerDescText.text = "Yeni bir teslimat görevi geldi. Belirtilen noktadan kargoyu alıp adrese ulaştırın.";
            offerDescText.fontSize = 12f;
            offerDescText.color = new Color(0.85f, 0.9f, 0.95f, 0.95f);
            offerDescText.textWrappingMode = TextWrappingModes.Normal;

            // Offer Reward Box
            GameObject rewardObj = new GameObject("RewardBox", typeof(RectTransform), typeof(Image));
            rewardObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform rwRect = rewardObj.GetComponent<RectTransform>();
            rwRect.anchorMin = new Vector2(0f, 0f);
            rwRect.anchorMax = new Vector2(1f, 0f);
            rwRect.pivot = new Vector2(0.5f, 0f);
            rwRect.anchoredPosition = new Vector2(0f, 68f);
            rwRect.sizeDelta = new Vector2(-28f, 44f);

            Image rwBg = rewardObj.GetComponent<Image>();
            rwBg.color = new Color(0.06f, 0.1f, 0.16f, 0.9f);
            rwBg.sprite = fallbackSprite;

            GameObject rwTextObj = new GameObject("RewardText", typeof(RectTransform), typeof(TextMeshProUGUI));
            rwTextObj.transform.SetParent(rewardObj.transform, false);
            RectTransform rwtRect = rwTextObj.GetComponent<RectTransform>();
            rwtRect.anchorMin = Vector2.zero;
            rwtRect.anchorMax = Vector2.one;
            rwtRect.offsetMin = new Vector2(12f, 0f);
            rwtRect.offsetMax = new Vector2(-12f, 0f);

            offerRewardText = rwTextObj.GetComponent<TextMeshProUGUI>();
            offerRewardText.text = "Ödül: ₺250";
            offerRewardText.fontSize = 17f;
            offerRewardText.fontStyle = FontStyles.Bold;
            offerRewardText.color = new Color(0.3f, 0.95f, 0.45f, 1f);
            offerRewardText.alignment = TextAlignmentOptions.Center;

            // Accept Button
            GameObject acceptBtnObj = new GameObject("AcceptBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            acceptBtnObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform abRect = acceptBtnObj.GetComponent<RectTransform>();
            abRect.anchorMin = new Vector2(0f, 0f);
            abRect.anchorMax = new Vector2(0.5f, 0f);
            abRect.pivot = new Vector2(0.5f, 0f);
            abRect.anchoredPosition = new Vector2(14f, 14f);
            abRect.sizeDelta = new Vector2(-20f, 42f);

            Image abImg = acceptBtnObj.GetComponent<Image>();
            abImg.color = new Color(0.15f, 0.7f, 0.35f, 1f);
            abImg.sprite = fallbackSprite;

            acceptButton = acceptBtnObj.GetComponent<Button>();
            acceptButton.onClick.AddListener(HandleAccept);

            GameObject abTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            abTextObj.transform.SetParent(acceptBtnObj.transform, false);
            RectTransform abtRect = abTextObj.GetComponent<RectTransform>();
            abtRect.anchorMin = Vector2.zero;
            abtRect.anchorMax = Vector2.one;
            abtRect.offsetMin = Vector2.zero;
            abtRect.offsetMax = Vector2.zero;

            TextMeshProUGUI abTmp = abTextObj.GetComponent<TextMeshProUGUI>();
            abTmp.text = "✓ KABUL ET";
            abTmp.fontSize = 13f;
            abTmp.fontStyle = FontStyles.Bold;
            abTmp.alignment = TextAlignmentOptions.Center;
            abTmp.color = Color.white;

            // Reject Button
            GameObject rejectBtnObj = new GameObject("RejectBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            rejectBtnObj.transform.SetParent(offerCardObj.transform, false);
            RectTransform rbRect = rejectBtnObj.GetComponent<RectTransform>();
            rbRect.anchorMin = new Vector2(0.5f, 0f);
            rbRect.anchorMax = new Vector2(1f, 0f);
            rbRect.pivot = new Vector2(0.5f, 0f);
            rbRect.anchoredPosition = new Vector2(-14f, 14f);
            rbRect.sizeDelta = new Vector2(-20f, 42f);

            Image rbImg = rejectBtnObj.GetComponent<Image>();
            rbImg.color = new Color(0.65f, 0.2f, 0.2f, 1f);
            rbImg.sprite = fallbackSprite;

            rejectButton = rejectBtnObj.GetComponent<Button>();
            rejectButton.onClick.AddListener(HandleReject);

            GameObject rbTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            rbTextObj.transform.SetParent(rejectBtnObj.transform, false);
            RectTransform rbtRect = rbTextObj.GetComponent<RectTransform>();
            rbtRect.anchorMin = Vector2.zero;
            rbtRect.anchorMax = Vector2.one;
            rbtRect.offsetMin = Vector2.zero;
            rbtRect.offsetMax = Vector2.zero;

            TextMeshProUGUI rbTmp = rbTextObj.GetComponent<TextMeshProUGUI>();
            rbTmp.text = "✕ REDDET";
            rbTmp.fontSize = 13f;
            rbTmp.fontStyle = FontStyles.Bold;
            rbTmp.alignment = TextAlignmentOptions.Center;
            rbTmp.color = Color.white;

            // 2. Active Delivery View
            activeDeliveryCardObj = new GameObject("ActiveDeliveryCard", typeof(RectTransform), typeof(Image));
            activeDeliveryCardObj.transform.SetParent(parent, false);
            RectTransform adcRect = activeDeliveryCardObj.GetComponent<RectTransform>();
            adcRect.anchorMin = new Vector2(0f, 0.5f);
            adcRect.anchorMax = new Vector2(1f, 0.5f);
            adcRect.pivot = new Vector2(0.5f, 0.5f);
            adcRect.anchoredPosition = new Vector2(0f, 10f);
            adcRect.sizeDelta = new Vector2(-28f, 260f);

            Image adcBg = activeDeliveryCardObj.GetComponent<Image>();
            adcBg.color = new Color(0.1f, 0.15f, 0.22f, 0.95f);
            adcBg.sprite = fallbackSprite;

            GameObject adTitle = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            adTitle.transform.SetParent(activeDeliveryCardObj.transform, false);
            RectTransform adtRect = adTitle.GetComponent<RectTransform>();
            adtRect.anchorMin = new Vector2(0f, 1f);
            adtRect.anchorMax = new Vector2(1f, 1f);
            adtRect.anchoredPosition = new Vector2(14f, -18f);
            adtRect.sizeDelta = new Vector2(-28f, 26f);
            TextMeshProUGUI adtTmp = adTitle.GetComponent<TextMeshProUGUI>();
            adtTmp.text = "🚚 AKTİF TESLİMAT SÜRÜYOR";
            adtTmp.fontSize = 15f;
            adtTmp.fontStyle = FontStyles.Bold;
            adtTmp.color = new Color(0.3f, 0.95f, 0.45f, 1f);

            GameObject adTarget = new GameObject("Target", typeof(RectTransform), typeof(TextMeshProUGUI));
            adTarget.transform.SetParent(activeDeliveryCardObj.transform, false);
            RectTransform adtrRect = adTarget.GetComponent<RectTransform>();
            adtrRect.anchorMin = new Vector2(0f, 1f);
            adtrRect.anchorMax = new Vector2(1f, 1f);
            adtrRect.anchoredPosition = new Vector2(14f, -50f);
            adtrRect.sizeDelta = new Vector2(-28f, 26f);
            activeTargetText = adTarget.GetComponent<TextMeshProUGUI>();
            activeTargetText.fontSize = 14f;
            activeTargetText.fontStyle = FontStyles.Bold;
            activeTargetText.color = Color.white;

            GameObject adStatus = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            adStatus.transform.SetParent(activeDeliveryCardObj.transform, false);
            RectTransform adsRect = adStatus.GetComponent<RectTransform>();
            adsRect.anchorMin = new Vector2(0f, 1f);
            adsRect.anchorMax = new Vector2(1f, 1f);
            adsRect.anchoredPosition = new Vector2(14f, -84f);
            adsRect.sizeDelta = new Vector2(-28f, 40f);
            activeStatusText = adStatus.GetComponent<TextMeshProUGUI>();
            activeStatusText.fontSize = 13f;
            activeStatusText.textWrappingMode = TextWrappingModes.Normal;

            GameObject adRw = new GameObject("Reward", typeof(RectTransform), typeof(TextMeshProUGUI));
            adRw.transform.SetParent(activeDeliveryCardObj.transform, false);
            RectTransform adrwRect = adRw.GetComponent<RectTransform>();
            adrwRect.anchorMin = new Vector2(0f, 0f);
            adrwRect.anchorMax = new Vector2(1f, 0f);
            adrwRect.anchoredPosition = new Vector2(14f, 20f);
            adrwRect.sizeDelta = new Vector2(-28f, 30f);
            activeRewardText = adRw.GetComponent<TextMeshProUGUI>();
            activeRewardText.fontSize = 13f;
            activeRewardText.color = new Color(0.85f, 0.95f, 0.7f, 0.9f);

            activeDeliveryCardObj.SetActive(false);

            // 3. Empty State View
            emptyStateObj = new GameObject("EmptyState", typeof(RectTransform));
            emptyStateObj.transform.SetParent(parent, false);
            RectTransform esRect = emptyStateObj.GetComponent<RectTransform>();
            esRect.anchorMin = new Vector2(0f, 0.5f);
            esRect.anchorMax = new Vector2(1f, 0.5f);
            esRect.pivot = new Vector2(0.5f, 0.5f);
            esRect.anchoredPosition = new Vector2(0f, 20f);
            esRect.sizeDelta = new Vector2(-40f, 200f);

            GameObject esIcon = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
            esIcon.transform.SetParent(emptyStateObj.transform, false);
            RectTransform esiRect = esIcon.GetComponent<RectTransform>();
            esiRect.anchorMin = new Vector2(0.5f, 1f);
            esiRect.anchorMax = new Vector2(0.5f, 1f);
            esiRect.pivot = new Vector2(0.5f, 1f);
            esiRect.anchoredPosition = new Vector2(0f, 0f);
            esiRect.sizeDelta = new Vector2(60f, 60f);
            TextMeshProUGUI esiTmp = esIcon.GetComponent<TextMeshProUGUI>();
            esiTmp.text = "📡";
            esiTmp.fontSize = 38f;
            esiTmp.alignment = TextAlignmentOptions.Center;

            GameObject esText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            esText.transform.SetParent(emptyStateObj.transform, false);
            RectTransform estRect = esText.GetComponent<RectTransform>();
            estRect.anchorMin = new Vector2(0f, 0f);
            estRect.anchorMax = new Vector2(1f, 1f);
            estRect.offsetMin = new Vector2(0f, 0f);
            estRect.offsetMax = new Vector2(0f, -70f);
            TextMeshProUGUI estTmp = esText.GetComponent<TextMeshProUGUI>();
            estTmp.text = "Yeni kargo ilanları taranıyor...\n\nŞehirde gezerken yeni teslimat teklifleri telefonunuza bildirim olarak gelecektir.";
            estTmp.fontSize = 13f;
            estTmp.alignment = TextAlignmentOptions.Center;
            estTmp.color = new Color(0.7f, 0.8f, 0.9f, 0.8f);
            estTmp.textWrappingMode = TextWrappingModes.Normal;
        }

        private void HandleAccept()
        {
            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayClick();
            }

            Action action = currentAcceptAction;
            ClearOffer();
            action?.Invoke();

            // Auto-close phone so player can start driving immediately
            if (SmartphoneUI.Instance != null)
            {
                SmartphoneUI.Instance.ClosePhone();
            }
        }

        private void HandleReject()
        {
            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayError();
            }

            Action action = currentRejectAction;
            ClearOffer();
            action?.Invoke();
        }
    }
}