using UnityEngine;

namespace DeliveryDriver.UI.Smartphone
{
    /// <summary>
    /// Akilli telefon arayuzu ve uygulamalari icin 2D ikon saglayicisi.
    /// Sprite'lari Resources/UI/Smartphone/Icons klasorunden yukler, bulamazsa dinamik Texture2D fallback uretir.
    /// </summary>
    public static class SmartphoneIconRegistry
    {
        private static Sprite cachedPhone;
        private static Sprite cachedDelivery;
        private static Sprite cachedBank;
        private static Sprite cachedGarage;
        private static Sprite cachedMessages;
        private static Sprite cachedBell;
        private static Sprite cachedStar;
        private static Sprite cachedDotGreen;
        private static Sprite cachedLocation;

        public static Sprite GetPhoneIcon() => GetOrLoad(ref cachedPhone, "icon_phone", new Color(0.9f, 0.95f, 1f));
        public static Sprite GetDeliveryAppIcon() => GetOrLoad(ref cachedDelivery, "app_delivery", new Color(1f, 0.55f, 0.1f));
        public static Sprite GetBankAppIcon() => GetOrLoad(ref cachedBank, "app_bank", new Color(0.1f, 0.85f, 0.45f));
        public static Sprite GetGarageAppIcon() => GetOrLoad(ref cachedGarage, "app_garage", new Color(0f, 0.8f, 1f));
        public static Sprite GetMessagesAppIcon() => GetOrLoad(ref cachedMessages, "app_messages", new Color(0.7f, 0.35f, 1f));
        public static Sprite GetBellIcon() => GetOrLoad(ref cachedBell, "icon_bell", new Color(0f, 0.9f, 1f));
        public static Sprite GetStarIcon() => GetOrLoad(ref cachedStar, "icon_star", new Color(1f, 0.85f, 0.1f));
        public static Sprite GetDotGreenIcon() => GetOrLoad(ref cachedDotGreen, "icon_dot_green", new Color(0.1f, 0.95f, 0.45f));
        public static Sprite GetLocationIcon() => GetOrLoad(ref cachedLocation, "icon_location", new Color(1f, 0.35f, 0.35f));

        private static Sprite GetOrLoad(ref Sprite cache, string iconName, Color fallbackColor)
        {
            if (cache != null) return cache;

            cache = Resources.Load<Sprite>("UI/Smartphone/Icons/" + iconName);
            if (cache != null) return cache;

#if UNITY_EDITOR
            cache = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Smartphone/Icons/" + iconName + ".png");
            if (cache != null) return cache;
#endif

            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = fallbackColor;
            tex.SetPixels(colors);
            tex.Apply();
            cache = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            return cache;
        }
    }
}
