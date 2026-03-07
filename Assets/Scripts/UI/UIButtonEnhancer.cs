using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public class UIButtonEnhancer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 originalScale = Vector3.one;
        private Color originalColor = Color.white;
        private Image targetImage;
        private bool initialized;

        private const float HoverScale = 1.03f;
        private const float PressScale = 0.97f;
        private const float ColorShiftAmount = 0.08f;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            originalScale = transform.localScale;
            targetImage = GetComponent<Image>();
            if (targetImage != null)
            {
                originalColor = targetImage.color;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Initialize();
            transform.localScale = originalScale * HoverScale;

            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(originalColor, Color.white, ColorShiftAmount);
            }

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayHover();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Initialize();
            transform.localScale = originalScale;

            if (targetImage != null)
            {
                targetImage.color = originalColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Initialize();
            transform.localScale = originalScale * PressScale;

            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(originalColor, Color.black, ColorShiftAmount * 2f);
            }

            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlayClick();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Initialize();
            transform.localScale = originalScale * HoverScale;

            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(originalColor, Color.white, ColorShiftAmount);
            }
        }

        public static void EnhanceButton(Button btn)
        {
            if (btn == null) return;

            if (btn.GetComponent<UIButtonEnhancer>() == null)
            {
                btn.gameObject.AddComponent<UIButtonEnhancer>();
            }
        }
    }
}
