using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UIKit
{
    /// <summary>Small, optional uGUI construction helpers. No gameplay wiring is included.</summary>
    public static class DDUIFactory
    {
        public static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static Image MakePanel(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position,
            Color? tint = null)
        {
            RectTransform rt = MakeRect(name, parent, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced : Image.Type.Simple;
            image.color = tint ?? Color.white;
            return image;
        }

        public static TextMeshProUGUI MakeText(string name, Transform parent, string value,
            Vector2 size, Vector2 position, float fontSize = 18f, Color? color = null)
        {
            RectTransform rt = MakeRect(name, parent, size, position);
            TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.color = color ?? DDUITheme.TextPrimary;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        public static Button MakeButton(string name, Transform parent, Sprite sprite, string label,
            Vector2 size, Vector2 position, Action onClick)
        {
            Image image = MakePanel(name, parent, sprite, size, position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            MakeText("Label", image.transform, label, size, Vector2.zero, 20f);
            UnityEngine.UI.Navigation nav = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Automatic
            };
            button.navigation = nav;
            return button;
        }
    }
}
