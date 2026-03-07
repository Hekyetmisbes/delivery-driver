using System;
using System.Collections;
using UnityEngine;

namespace DeliveryDriver.UI
{
    public static class UIAnimationHelper
    {
        public static Coroutine FadeIn(MonoBehaviour host, CanvasGroup group, float duration)
        {
            if (host == null || group == null) return null;
            return host.StartCoroutine(FadeCoroutine(group, 0f, 1f, duration, null));
        }

        public static Coroutine FadeOut(MonoBehaviour host, CanvasGroup group, float duration, Action onComplete = null)
        {
            if (host == null || group == null) return null;
            return host.StartCoroutine(FadeCoroutine(group, group.alpha, 0f, duration, onComplete));
        }

        public static Coroutine ScaleIn(MonoBehaviour host, RectTransform rect, float duration)
        {
            if (host == null || rect == null) return null;
            return host.StartCoroutine(ScaleCoroutine(rect, 0.85f, 1f, duration, EaseOutBack));
        }

        public static Coroutine SlideIn(MonoBehaviour host, RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            if (host == null || rect == null) return null;
            return host.StartCoroutine(SlideCoroutine(rect, from, to, duration, EaseOutCubic));
        }

        public static Coroutine SlideOut(MonoBehaviour host, RectTransform rect, Vector2 from, Vector2 to, float duration, Action onComplete = null)
        {
            if (host == null || rect == null) return null;
            return host.StartCoroutine(SlideCoroutine(rect, from, to, duration, EaseOutCubic, onComplete));
        }

        public static Coroutine PulseScale(MonoBehaviour host, RectTransform rect, float scaleAmount, float duration)
        {
            if (host == null || rect == null) return null;
            return host.StartCoroutine(PulseScaleCoroutine(rect, scaleAmount, duration));
        }

        private static IEnumerator FadeCoroutine(CanvasGroup group, float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            group.alpha = from;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t));
                yield return null;
            }

            group.alpha = to;
            group.interactable = to > 0.5f;
            group.blocksRaycasts = to > 0.5f;
            onComplete?.Invoke();
        }

        private static IEnumerator ScaleCoroutine(RectTransform rect, float from, float to, float duration, Func<float, float> easing)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float scale = Mathf.LerpUnclamped(from, to, easing(t));
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            rect.localScale = new Vector3(to, to, 1f);
        }

        private static IEnumerator SlideCoroutine(RectTransform rect, Vector2 from, Vector2 to, float duration, Func<float, float> easing, Action onComplete = null)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            rect.anchoredPosition = from;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, easing(t));
                yield return null;
            }

            rect.anchoredPosition = to;
            onComplete?.Invoke();
        }

        private static IEnumerator PulseScaleCoroutine(RectTransform rect, float scaleAmount, float duration)
        {
            float halfDuration = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float scale = Mathf.Lerp(1f, scaleAmount, EaseOutCubic(t));
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float scale = Mathf.Lerp(scaleAmount, 1f, EaseOutCubic(t));
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        // Easing functions
        public static float EaseOutCubic(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }

        public static float Linear(float t)
        {
            return t;
        }
    }
}
