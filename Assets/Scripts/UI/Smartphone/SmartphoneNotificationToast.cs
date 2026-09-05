using System;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Ekranın sağ alt köşesinde (telefon butonunun üstünde) yeni bir görev veya bildirim
    /// geldiğinde beliren, tıklanabilir modern bildirim balonu (Toast Banner).
    /// </summary>
    public class SmartphoneNotificationToast : MonoBehaviour
    {
        private RectTransform rootRect;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private Image iconImage;
        private Button clickButton;
        private Action onClickAction;
        private Coroutine activeRoutine;

        public void Initialize(Transform parent, Sprite fallbackSprite)
        {
            if (rootRect != null) return;

            // Toast Root
            GameObject toastObj = new GameObject("SmartphoneToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            toastObj.transform.SetParent(parent, false);

            rootRect = toastObj.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-28f, 105f); // Directly above HUD Phone Button
            rootRect.sizeDelta = new Vector2(320f, 68f);

            canvasGroup = toastObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image bg = toastObj.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            bg.sprite = fallbackSprite;

            clickButton = toastObj.GetComponent<Button>();
            clickButton.onClick.AddListener(HandleToastClicked);

            // Left Accent Border (Cyan/Blue)
            GameObject accentObj = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accentObj.transform.SetParent(toastObj.transform, false);
            RectTransform accRect = accentObj.GetComponent<RectTransform>();
            accRect.anchorMin = new Vector2(0f, 0f);
            accRect.anchorMax = new Vector2(0f, 1f);
            accRect.pivot = new Vector2(0f, 0.5f);
            accRect.sizeDelta = new Vector2(5f, 0f);
            Image accImg = accentObj.GetComponent<Image>();
            accImg.color = new Color(0.18f, 0.85f, 1f, 1f);
            accImg.sprite = fallbackSprite;
            accImg.raycastTarget = false;

            // Icon Badge
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(toastObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(24f, 0f);
            iconRect.sizeDelta = new Vector2(26f, 26f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = SmartphoneIconRegistry.GetBellIcon();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Title Text
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(toastObj.transform, false);
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0.5f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.offsetMin = new Vector2(46f, 0f);
            tRect.offsetMax = new Vector2(-10f, -4f);

            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.text = LocalizationTable.Get("phone_toast_offer_title");
            titleText.fontSize = 13f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.95f, 0.98f, 1f, 1f);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.raycastTarget = false;

            // Subtitle / Hint Text
            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(toastObj.transform, false);
            RectTransform sRect = subObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.offsetMin = new Vector2(46f, 4f);
            sRect.offsetMax = new Vector2(-10f, 0f);

            subtitleText = subObj.GetComponent<TextMeshProUGUI>();
            subtitleText.text = LocalizationTable.Get("phone_toast_hint");
            subtitleText.fontSize = 11f;
            subtitleText.color = new Color(0.7f, 0.85f, 0.95f, 0.9f);
            subtitleText.alignment = TextAlignmentOptions.Left;
            subtitleText.raycastTarget = false;

            toastObj.SetActive(false);
        }

        public void Show(string title, string subtitle, Action onClick, float duration = 5f)
        {
            if (rootRect == null) return;

            onClickAction = onClick;
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(ToastRoutine(duration));
        }

        public void Hide()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (canvasGroup != null && rootRect != null && rootRect.gameObject.activeSelf)
            {
                UIAnimationHelper.FadeOut(this, canvasGroup, 0.2f, () =>
                {
                    if (rootRect != null) rootRect.gameObject.SetActive(false);
                });
            }
        }

        private System.Collections.IEnumerator ToastRoutine(float duration)
        {
            rootRect.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Vector2 targetPos = new Vector2(-28f, 105f);
            Vector2 startPos = new Vector2(100f, 105f);

            UIAnimationHelper.SlideIn(this, rootRect, startPos, targetPos, 0.25f);
            UIAnimationHelper.FadeIn(this, canvasGroup, 0.25f);

            yield return new WaitForSecondsRealtime(duration);

            UIAnimationHelper.SlideOut(this, rootRect, targetPos, startPos, 0.25f);
            yield return UIAnimationHelper.FadeOut(this, canvasGroup, 0.25f);

            rootRect.gameObject.SetActive(false);
            activeRoutine = null;
        }

        private void HandleToastClicked()
        {
            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayClick();
            }

            Hide();
            onClickAction?.Invoke();
        }
    }
}