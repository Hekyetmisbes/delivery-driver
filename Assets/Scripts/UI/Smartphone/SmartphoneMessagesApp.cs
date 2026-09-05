using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akıllı Telefon "Mesajlar & Yorumlar" Uygulaması.
    /// Müşterilerden gelen teslimat puanlamalarını, 5 yıldızlı yorumları ve merkez bildirimlerini gösterir.
    /// </summary>
    public class SmartphoneMessagesApp : MonoBehaviour
    {
        private GameObject appRoot;
        private Sprite fallbackSprite;

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

            GameObject hTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            hTitleObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = hTitleObj.GetComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = new Vector2(16f, 0f);
            htRect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI htText = hTitleObj.GetComponent<TextMeshProUGUI>();
            htText.text = "💬 Müşteri Yorumları & SMS";
            htText.fontSize = 15f;
            htText.fontStyle = FontStyles.Bold;
            htText.alignment = TextAlignmentOptions.Left;
            htText.color = new Color(0.4f, 0.85f, 1f, 1f);

            // Reviews List Container
            CreateMessageBubble(parent, "Kızılay Medikal", "⭐⭐⭐⭐⭐", "Acil kargomuz tam zamanında ulaştı, çok profesyonel kurye.", "12 dk önce", -104f);
            CreateMessageBubble(parent, "Tunalı Pastanesi", "⭐⭐⭐⭐⭐", "Hassas pasta siparişimiz tek bir çizik bile olmadan geldi. Elinize sağlık!", "38 dk önce", -184f);
            CreateMessageBubble(parent, "Batıkent Elektronik", "⭐⭐⭐⭐⭐", "Çok hızlı teslimat yapıldı, kurye arkadaş çok nazikti.", "Bugün 14:15", -264f);
            CreateMessageBubble(parent, "Lojistik Merkezi", "🔔 BİLDİRİM", "Tebrikler! Güvenli sürüş ve zamanında teslimat priminiz cüzdanınıza yansıtıldı.", "Bugün 12:00", -344f);
        }

        private void CreateMessageBubble(Transform parent, string sender, string ratingOrTag, string message, string time, float yPos)
        {
            GameObject bubbleObj = new GameObject("MessageBubble", typeof(RectTransform), typeof(Image));
            bubbleObj.transform.SetParent(parent, false);
            RectTransform bRect = bubbleObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = new Vector2(0f, yPos);
            bRect.sizeDelta = new Vector2(-28f, 72f);

            Image bBg = bubbleObj.GetComponent<Image>();
            bBg.color = new Color(0.1f, 0.14f, 0.2f, 0.95f);
            bBg.sprite = fallbackSprite;

            // Sender & Rating
            GameObject sObj = new GameObject("Sender", typeof(RectTransform), typeof(TextMeshProUGUI));
            sObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform sr = sObj.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.anchoredPosition = new Vector2(12f, -8f);
            sr.sizeDelta = new Vector2(-24f, 18f);
            TextMeshProUGUI st = sObj.GetComponent<TextMeshProUGUI>();
            st.text = $"<b>{sender}</b> <color=#FFD700>{ratingOrTag}</color>";
            st.fontSize = 12f;
            st.color = Color.white;

            // Message Body
            GameObject mObj = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            mObj.transform.SetParent(bubbleObj.transform, false);
            RectTransform mr = mObj.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0f, 0f);
            mr.anchorMax = new Vector2(1f, 1f);
            mr.offsetMin = new Vector2(12f, 8f);
            mr.offsetMax = new Vector2(-12f, -24f);
            TextMeshProUGUI mt = mObj.GetComponent<TextMeshProUGUI>();
            mt.text = message;
            mt.fontSize = 11f;
            mt.color = new Color(0.8f, 0.88f, 0.95f, 0.9f);
            mt.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}