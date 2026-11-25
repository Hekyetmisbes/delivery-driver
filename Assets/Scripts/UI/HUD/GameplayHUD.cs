using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.UI
{
    public class GameplayHUD : MonoBehaviour
    {
        private static GameplayHUD instance;
        public static GameplayHUD Instance => instance;

        private readonly Dictionary<string, RectTransform> hudZones = new Dictionary<string, RectTransform>();

        public const string ZoneTopLeft = "top-left";
        public const string ZoneTopRight = "top-right";
        public const string ZoneTopCenter = "top-center";
        public const string ZoneBottomLeft = "bottom-left";
        public const string ZoneBottomRight = "bottom-right";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnEnable()
        {
            Quest.GameSettings.OnAccessibilityChanged += OnAccessibilityChanged;
        }

        private void OnDisable()
        {
            Quest.GameSettings.OnAccessibilityChanged -= OnAccessibilityChanged;
        }

        public void RegisterHudElement(string zone, RectTransform element)
        {
            if (string.IsNullOrEmpty(zone) || element == null) return;
            hudZones[zone] = element;
        }

        public RectTransform GetZone(string zone)
        {
            return hudZones.TryGetValue(zone, out RectTransform rect) ? rect : null;
        }

        private void OnAccessibilityChanged()
        {
            if (Quest.GameSettings.Instance == null) return;
            float scale = Quest.GameSettings.Instance.TextScaleMultiplier;
            Vector3 scaleVec = new Vector3(scale, scale, 1f);

            foreach (var kvp in hudZones)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.localScale = scaleVec;
                }
            }
        }
    }
}
