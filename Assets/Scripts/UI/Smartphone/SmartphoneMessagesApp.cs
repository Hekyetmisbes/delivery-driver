using System.Collections.Generic;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akıllı Telefon "Mesajlar & Yorumlar" Uygulaması.
    /// Kuryenin 5 üzerinden güncel kargo puanını (Rating), kurye unvanını ve
    /// teslimat performansına göre positive/negative JSON'dan üretilen gerçekçi müşteri yorumlarını gösterir.
    /// </summary>
    public class SmartphoneMessagesApp : MonoBehaviour
    {
        private GameObject appRoot;
        private Sprite fallbackSprite;

        private TextMeshProUGUI ratingScoreText;
        private TextMeshProUGUI ratingTierText;
        private TextMeshProUGUI rewardBonusText;
        private Transform listContainer;
        private readonly List<GameObject> activeBubbleObjects = new List<GameObject>();

        public void Initialize(Transform parent, Sprite fallback)
        {
            if (appRoot != null) return;
            fallbackSprite = fallback;

            appRoot = new GameObject("MessagesAppScreen", typeof(RectTransform));
            appRoot.transform.SetParent(parent, false);

            RectTransform appRect = appRoot.GetComponent<RectTransform>();
            appRect.anchorMin = Vector2.zero;
            appRect.anchorMax = Vector2.one;
            appRect.offsetMin = Vector2.zero;
            appRect.offsetMax = Vector2.zero;

            BuildMessagesContent(appRoot.transform);
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

        public void RefreshView()
        {
            float rating = CourierRatingManager.Instance != null ? CourierRatingManager.Instance.CurrentRating : 5.0f;
            string tier = CourierRatingManager.Instance != null ? CourierRatingManager.Instance.GetRatingTierTitle() : "VIP / Mükemmel Kurye";
            float multiplier = CourierRatingManager.Instance != null ? CourierRatingManager.Instance.GetRatingRewardMultiplier() : 1.35f;

            if (ratingScoreText != null) ratingScoreText.text = $"★ {rating:F1} / 5.0";
            if (ratingTierText != null) ratingTierText.text = tier;
            if (rewardBonusText != null)
            {
                int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
                string bonusStr = percent >= 0 ? $"+%{percent} Kargo Primi" : $"{percent}% Kesinti";
                rewardBonusText.text = bonusStr;
                rewardBonusText.color = percent >= 0 ? new Color(0.3f, 0.95f, 0.45f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
            }

            RebuildReviewList();
        }

        private void RebuildReviewList()
        {
            if (listContainer == null) return;

            for (int i = 0; i < activeBubbleObjects.Count; i++)
            {
                if (activeBubbleObjects[i] != null) Destroy(activeBubbleObjects[i]);
            }
            activeBubbleObjects.Clear();

            IReadOnlyList<CourierRatingManager.ActiveReviewItem> reviews = CourierRatingManager.Instance != null
                ? CourierRatingManager.Instance.RecentReviews
                : null;

            if (reviews == null || reviews.Count == 0) return;

            float yOffset = 0f;
            int count = Mathf.Min(reviews.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var r = reviews[i];
                string ratingTag = $"[★ {r.rating:F1}]";
                Color tagColor = r.isPositive ? new Color(1f, 0.84f, 0f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);

                GameObject bubble = CreateMessageBubble(listContainer, r.customerName, ratingTag, tagColor, r.comment, r.timeString, yOffset);
                activeBubbleObjects.Add(bubble);
                yOffset -= 76f;
            }
        }

        private void BuildMessagesContent(Transform parent)
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
            hImg.color = new Color(0.12f, 0.18f, 0.26f, 0.98f);
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
            hiImg.sprite = SmartphoneIconRegistry.GetMessagesAppIcon();
            hiImg.preserveAspect = true;

            GameObject hTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            hTitleObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = hTitleObj.GetComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = new Vector2(46f, 0f);
            htRect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI htText = hTitleObj.GetComponent<TextMeshProUGUI>();
            htText.text = "Müşteri Yorumları & Puan";
            htText.fontSize = 15f;
            htText.fontStyle = FontStyles.Bold;
            htText.alignment = TextAlignmentOptions.Left;
            htText.color = new Color(0.4f, 0.85f, 1f, 1f);

            // Rating Score Card (Overview)
            GameObject scoreCardObj = new GameObject("RatingScoreCard", typeof(RectTransform), typeof(Image));
            scoreCardObj.transform.SetParent(parent, false);
            RectTransform scRect = scoreCardObj.GetComponent<RectTransform>();
            scRect.anchorMin = new Vector2(0f, 1f);
            scRect.anchorMax = new Vector2(1f, 1f);
            scRect.pivot = new Vector2(0.5f, 1f);
            scRect.anchoredPosition = new Vector2(0f, -98f);
            scRect.sizeDelta = new Vector2(-28f, 68f);

            Image scImg = scoreCardObj.GetComponent<Image>();
            scImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
            scImg.sprite = fallbackSprite;

            // Score Big
            GameObject sbObj = new GameObject("RatingScore", typeof(RectTransform), typeof(TextMeshProUGUI));
            sbObj.transform.SetParent(scoreCardObj.transform, false);
            RectTransform sbRect = sbObj.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0f, 0.5f);
            sbRect.anchorMax = new Vector2(0.55f, 1f);
            sbRect.offsetMin = new Vector2(12f, 0f);
            sbRect.offsetMax = new Vector2(0f, -6f);
            ratingScoreText = sbObj.GetComponent<TextMeshProUGUI>();
            ratingScoreText.text = "★ 5.0 / 5.0";
            ratingScoreText.fontSize = 18f;
            ratingScoreText.fontStyle = FontStyles.Bold;
            ratingScoreText.color = new Color(1f, 0.85f, 0.15f, 1f);

            // Tier Title
            GameObject stObj = new GameObject("RatingTier", typeof(RectTransform), typeof(TextMeshProUGUI));
            stObj.transform.SetParent(scoreCardObj.transform, false);
            RectTransform stRect = stObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 0f);
            stRect.anchorMax = new Vector2(0.6f, 0.5f);
            stRect.offsetMin = new Vector2(12f, 6f);
            stRect.offsetMax = Vector2.zero;
            ratingTierText = stObj.GetComponent<TextMeshProUGUI>();
            ratingTierText.text = "VIP / Mükemmel Kurye";
            ratingTierText.fontSize = 11.5f;
            ratingTierText.color = new Color(0.85f, 0.92f, 1f, 0.85f);

            // Reward Bonus Badge (Right side of card)
            GameObject rbObj = new GameObject("RewardBonus", typeof(RectTransform), typeof(TextMeshProUGUI));
            rbObj.transform.SetParent(scoreCardObj.transform, false);
            RectTransform rbRect = rbObj.GetComponent<RectTransform>();
            rbRect.anchorMin = new Vector2(0.55f, 0f);
            rbRect.anchorMax = new Vector2(1f, 1f);
            rbRect.offsetMin = new Vector2(0f, 6f);
            rbRect.offsetMax = new Vector2(-12f, -6f);
            rewardBonusText = rbObj.GetComponent<TextMeshProUGUI>();
            rewardBonusText.text = "+%35 Kargo Primi";
            rewardBonusText.fontSize = 12f;
            rewardBonusText.fontStyle = FontStyles.Bold;
            rewardBonusText.alignment = TextAlignmentOptions.Right;
            rewardBonusText.color = new Color(0.3f, 0.95f, 0.45f, 1f);

            // Review List Container
            GameObject containerObj = new GameObject("ReviewListContainer", typeof(RectTransform));
            containerObj.transform.SetParent(parent, false);
            RectTransform cRect = containerObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 1f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.pivot = new Vector2(0.5f, 1f);
            cRect.anchoredPosition = new Vector2(0f, -174f);
            cRect.sizeDelta = new Vector2(0f, 400f);
            listContainer = containerObj.transform;

            RefreshView();
        }

        private GameObject CreateMessageBubble(Transform parent, string sender, string ratingOrTag, Color tagColor, string message, string time, float yPos)
        {
            GameObject bubbleObj = new GameObject($"Bubble_{sender}", typeof(RectTransform), typeof(Image));
            bubbleObj.transform.SetParent(parent, false);
            RectTransform bRect = bubbleObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = new Vector2(0f, yPos);
            bRect.sizeDelta = new Vector2(-28f, 70f);

            Image bBg = bubbleObj.GetComponent<Image>();
            bBg.color = new Color(0.1f, 0.14f, 0.2f, 0.95f);
            bBg.sprite = fallbackSprite;

            // Sender & Rating
            GameObject sObj = new GameObject("Sender", typeof(RectTransform), typeof(TextMeshProUGUI));
            sObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform sr = sObj.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(0.7f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(12f, -6f);
            sr.sizeDelta = new Vector2(0f, 18f);
            TextMeshProUGUI st = sObj.GetComponent<TextMeshProUGUI>();
            string hexColor = ColorUtility.ToHtmlStringRGB(tagColor);
            st.text = $"<b>{sender}</b> <color=#{hexColor}>{ratingOrTag}</color>";
            st.fontSize = 11.5f;
            st.color = Color.white;

            // Timestamp (top-right)
            GameObject tmObj = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
            tmObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform tmr = tmObj.GetComponent<RectTransform>();
            tmr.anchorMin = new Vector2(0.7f, 1f);
            tmr.anchorMax = new Vector2(1f, 1f);
            tmr.pivot = new Vector2(1f, 1f);
            tmr.anchoredPosition = new Vector2(-12f, -6f);
            tmr.sizeDelta = new Vector2(0f, 18f);
            TextMeshProUGUI tmt = tmObj.GetComponent<TextMeshProUGUI>();
            tmt.text = time;
            tmt.fontSize = 10f;
            tmt.alignment = TextAlignmentOptions.Right;
            tmt.color = new Color(0.7f, 0.8f, 0.9f, 0.7f);

            // Message Body
            GameObject mObj = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            mObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform mr = mObj.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0f, 0f);
            mr.anchorMax = new Vector2(1f, 1f);
            mr.offsetMin = new Vector2(12f, 6f);
            mr.offsetMax = new Vector2(-12f, -22f);
            TextMeshProUGUI mt = mObj.GetComponent<TextMeshProUGUI>();
            mt.text = message;
            mt.fontSize = 10.5f;
            mt.color = new Color(0.8f, 0.88f, 0.95f, 0.9f);
            mt.textWrappingMode = TextWrappingModes.Normal;

            return bubbleObj;
        }
    }
}