using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public enum NotificationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    public struct NotificationData
    {
        public string Title;
        public string Message;
        public float Duration;
        public NotificationPriority Priority;
    }

    public class NotificationQueue : MonoBehaviour
    {
        private static NotificationQueue instance;
        private const string NotificationRootName = "NotificationRoot";
        private const string NotificationContainerName = "Container";

        private const int MaxVisible = 3;
        private const float DefaultDuration = 3f;
        private const float FadeOutDuration = 0.35f;
        private const float EntryHeight = 60f;
        private const float EntrySpacing = 8f;

        private Canvas notificationCanvas;
        private RectTransform notificationRootRect;
        private RectTransform containerRect;
        private readonly Queue<NotificationData> pendingQueue = new Queue<NotificationData>();
        private readonly List<NotificationEntry> activeEntries = new List<NotificationEntry>();

        private class NotificationEntry
        {
            public GameObject Root;
            public CanvasGroup CanvasGroup;
            public float ExpireTime;
            public bool FadingOut;
        }

        public static void Enqueue(NotificationData data)
        {
            EnsureInstance();
            if (data.Duration <= 0f) data.Duration = DefaultDuration;
            instance.pendingQueue.Enqueue(data);
        }

        public static void Enqueue(string title, string message, float duration = DefaultDuration, NotificationPriority priority = NotificationPriority.Normal)
        {
            Enqueue(new NotificationData
            {
                Title = title,
                Message = message,
                Duration = duration,
                Priority = priority
            });
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            GameObject go = new GameObject("NotificationQueue", typeof(NotificationQueue));
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateCanvas();
        }

        private void Update()
        {
            // Process expiring entries
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                NotificationEntry entry = activeEntries[i];
                if (entry.FadingOut) continue;

                if (Time.unscaledTime >= entry.ExpireTime)
                {
                    entry.FadingOut = true;
                    StartCoroutine(FadeOutAndRemove(entry, i));
                }
            }

            // Show pending if space available
            while (pendingQueue.Count > 0 && activeEntries.Count < MaxVisible)
            {
                NotificationData data = pendingQueue.Dequeue();
                ShowNotification(data);
            }
        }

        private void ShowNotification(NotificationData data)
        {
            if (containerRect == null) CreateCanvas();

            GameObject root = new GameObject("Notification", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(containerRect, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            float yOffset = -(activeEntries.Count * (EntryHeight + EntrySpacing));
            rect.anchoredPosition = new Vector2(0f, yOffset);
            rect.sizeDelta = new Vector2(450f, EntryHeight);

            Image bg = root.GetComponent<Image>();
            bg.color = UIThemeConstants.PanelBackground;
            bg.raycastTarget = false;

            CanvasGroup cg = root.GetComponent<CanvasGroup>();
            cg.alpha = 0f;

            // Title
            if (!string.IsNullOrEmpty(data.Title))
            {
                GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(root.transform, false);
                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 0.5f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(12f, 0f);
                titleRect.offsetMax = new Vector2(-12f, -4f);

                TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
                titleText.text = data.Title;
                titleText.fontSize = 18f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.color = Color.white;
                titleText.alignment = TextAlignmentOptions.Left;
                if (TMP_Settings.defaultFontAsset != null) titleText.font = TMP_Settings.defaultFontAsset;
            }

            // Message
            if (!string.IsNullOrEmpty(data.Message))
            {
                GameObject msgObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
                msgObj.transform.SetParent(root.transform, false);
                RectTransform msgRect = msgObj.GetComponent<RectTransform>();
                msgRect.anchorMin = Vector2.zero;
                msgRect.anchorMax = new Vector2(1f, 0.5f);
                msgRect.offsetMin = new Vector2(12f, 4f);
                msgRect.offsetMax = new Vector2(-12f, 0f);

                TextMeshProUGUI msgText = msgObj.GetComponent<TextMeshProUGUI>();
                msgText.text = data.Message;
                msgText.fontSize = 15f;
                msgText.color = UIThemeConstants.TextSecondary;
                msgText.alignment = TextAlignmentOptions.Left;
                if (TMP_Settings.defaultFontAsset != null) msgText.font = TMP_Settings.defaultFontAsset;
            }

            NotificationEntry entry = new NotificationEntry
            {
                Root = root,
                CanvasGroup = cg,
                ExpireTime = Time.unscaledTime + data.Duration,
                FadingOut = false
            };

            activeEntries.Add(entry);
            UIAnimationHelper.FadeIn(this, cg, 0.2f);
        }

        private IEnumerator FadeOutAndRemove(NotificationEntry entry, int index)
        {
            float elapsed = 0f;
            while (elapsed < FadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (entry.CanvasGroup != null)
                {
                    entry.CanvasGroup.alpha = 1f - (elapsed / FadeOutDuration);
                }
                yield return null;
            }

            activeEntries.Remove(entry);
            if (entry.Root != null) Destroy(entry.Root);
            RepositionEntries();
        }

        private void RepositionEntries()
        {
            for (int i = 0; i < activeEntries.Count; i++)
            {
                RectTransform rect = activeEntries[i].Root.GetComponent<RectTransform>();
                if (rect != null)
                {
                    float yOffset = -(i * (EntryHeight + EntrySpacing));
                    rect.anchoredPosition = new Vector2(0f, yOffset);
                }
            }
        }

        private void CreateCanvas()
        {
            if (containerRect != null)
            {
                return;
            }

            Transform uiParent = GlobalUiCoordinator.CanvasGroupRoot ?? GlobalUiCoordinator.PrimaryCanvas?.transform;
            if (uiParent != null)
            {
                Transform existingRoot = uiParent.Find(NotificationRootName);
                if (existingRoot == null)
                {
                    GameObject rootObject = new GameObject(NotificationRootName, typeof(RectTransform));
                    rootObject.transform.SetParent(uiParent, false);
                    notificationRootRect = rootObject.GetComponent<RectTransform>();
                    notificationRootRect.anchorMin = Vector2.zero;
                    notificationRootRect.anchorMax = Vector2.one;
                    notificationRootRect.offsetMin = Vector2.zero;
                    notificationRootRect.offsetMax = Vector2.zero;
                }
                else
                {
                    notificationRootRect = existingRoot as RectTransform;
                }
            }
            else
            {
                GameObject canvasObj = new GameObject("NotificationCanvas", typeof(Canvas), typeof(CanvasScaler));
                canvasObj.transform.SetParent(transform, false);

                notificationCanvas = canvasObj.GetComponent<Canvas>();
                notificationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                notificationCanvas.sortingOrder = 800;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                notificationRootRect = canvasObj.GetComponent<RectTransform>();
            }

            Transform existingContainer = notificationRootRect != null
                ? notificationRootRect.Find(NotificationContainerName)
                : null;
            if (existingContainer != null)
            {
                containerRect = existingContainer as RectTransform;
            }
            else
            {
                GameObject container = new GameObject(NotificationContainerName, typeof(RectTransform));
                container.transform.SetParent(notificationRootRect, false);
                containerRect = container.GetComponent<RectTransform>();
            }

            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = new Vector2(0f, -20f);
            containerRect.sizeDelta = new Vector2(460f, 300f);
            containerRect.SetAsLastSibling();
        }
    }
}
