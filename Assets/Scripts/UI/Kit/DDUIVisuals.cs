using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UIKit
{
    /// <summary>Shared, static chrome for the Delivery Driver runtime menus.</summary>
    public static class DDUIVisuals
    {
        private const string ResourceRoot = "UI/DeliveryDriver/";
        private const string AssetRoot = "Assets/Resources/UI/DeliveryDriver/";

        public static Sprite LoadComponent(string name)
        {
            return Load("Components/", name);
        }

        public static Sprite LoadIcon(string name)
        {
            return Load("Icons/", name);
        }

        public static Sprite LoadCityBackdrop()
        {
            return Load("Backgrounds/", "city_backdrop");
        }

        public static void AddChrome(Transform parent, string section, string kicker)
        {
            Image topBar = CreateImage("DDUI_TopBar", parent, DDUITheme.Background);
            RectTransform topRect = topBar.rectTransform;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 102f);
            topRect.anchoredPosition = Vector2.zero;
            topBar.raycastTarget = false;

            Image iconTile = CreateImage("BrandIconTile", topRect, DDUITheme.Primary);
            SetTopLeft(iconTile.rectTransform, new Vector2(56f, 25f), new Vector2(46f, 46f));
            iconTile.raycastTarget = false;
            Sprite truck = LoadIcon("truck");
            if (truck != null)
            {
                Image icon = CreateImage("BrandTruck", iconTile.transform, DDUITheme.Background);
                icon.sprite = truck;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(8f, 8f);
                iconRect.offsetMax = new Vector2(-8f, -8f);
            }

            AddText("BrandDelivery", topRect, "DELIVERY", 27f, DDUITheme.TextPrimary,
                TextAlignmentOptions.Left, new Vector2(115f, 27f), new Vector2(160f, 38f));
            AddText("BrandDriver", topRect, "DRIVER", 27f, DDUITheme.Primary,
                TextAlignmentOptions.Left, new Vector2(270f, 27f), new Vector2(130f, 38f));
            AddText("BrandKicker", topRect, kicker, 12f, DDUITheme.TextSecondary,
                TextAlignmentOptions.Left, new Vector2(58f, 75f), new Vector2(390f, 20f));

            TextMeshProUGUI sectionLabel = AddText("Section", topRect, section, 17f,
                DDUITheme.TextPrimary, TextAlignmentOptions.Right,
                new Vector2(55f, 29f), new Vector2(310f, 35f));
            RectTransform sectionRect = sectionLabel.rectTransform;
            sectionRect.anchorMin = sectionRect.anchorMax = Vector2.one;
            sectionRect.pivot = Vector2.one;
            sectionRect.anchoredPosition = new Vector2(-55f, -29f);

            Image bottomBar = CreateImage("DDUI_BottomBar", parent, DDUITheme.Background);
            RectTransform bottomRect = bottomBar.rectTransform;
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(0f, 46f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomBar.raycastTarget = false;
            AddText("Footer", bottomRect, "DELIVERY DRIVER  /  LOGISTICS", 12f,
                DDUITheme.TextSecondary, TextAlignmentOptions.Left,
                new Vector2(65f, 10f), new Vector2(350f, 25f));
        }

        public static TextMeshProUGUI AddText(string name, Transform parent, string value, float fontSize,
            Color color, TextAlignmentOptions alignment, Vector2 topLeft, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            SetTopLeft(text.rectTransform, topLeft, size);
            return text;
        }

        private static Sprite Load(string folder, string name)
        {
            return RuntimeUiSkinLoader.LoadSprite(ResourceRoot + folder + name,
                AssetRoot + folder + name + ".png");
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        }
    }
}
