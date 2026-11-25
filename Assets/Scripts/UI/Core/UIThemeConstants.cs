using UnityEngine;

namespace DeliveryDriver.UI
{
    public static class UIThemeConstants
    {
        // Panel backgrounds
        public static readonly Color PanelBackground = new Color(0.05f, 0.08f, 0.12f, 0.94f);
        public static readonly Color SectionBackground = new Color(0.09f, 0.13f, 0.19f, 0.92f);
        public static readonly Color OverlayBackground = new Color(0f, 0f, 0f, 0.55f);

        // Semantic colors
        public static readonly Color Positive = new Color(0.3f, 0.95f, 0.45f, 1f);
        public static readonly Color Negative = new Color(1f, 0.35f, 0.3f, 1f);
        public static readonly Color Warning = new Color(1f, 0.85f, 0.2f, 1f);
        public static readonly Color Info = new Color(0.3f, 0.7f, 1f, 1f);

        // Button colors
        public static readonly Color ButtonGreen = new Color(0.16f, 0.62f, 0.3f, 1f);
        public static readonly Color ButtonBlue = new Color(0.15f, 0.45f, 0.75f, 0.95f);
        public static readonly Color ButtonRed = new Color(0.67f, 0.22f, 0.2f, 1f);
        public static readonly Color ButtonAmber = new Color(0.64f, 0.45f, 0.1f, 0.95f);
        public static readonly Color ButtonNeutral = new Color(0.35f, 0.38f, 0.42f, 1f);

        // Text colors
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextSecondary = new Color(0.85f, 0.9f, 0.95f, 1f);
        public static readonly Color TextHeader = new Color(0.88f, 0.94f, 1f, 1f);
        public static readonly Color TextSubheader = new Color(0.78f, 0.87f, 0.98f, 0.95f);

        // HUD colors
        public static readonly Color HudPanelBackground = new Color(0.05f, 0.1f, 0.18f, 0.82f);
        public static readonly Color MoneyText = new Color(0.3f, 0.95f, 0.45f, 1f);
        public static readonly Color RewardText = new Color(0.96f, 0.86f, 0.25f, 1f);

        // Timer colors
        public static readonly Color TimerSafe = new Color(0.2f, 0.9f, 0.3f, 1f);
        public static readonly Color TimerWarning = new Color(1f, 0.85f, 0.2f, 1f);
        public static readonly Color TimerDanger = new Color(1f, 0.25f, 0.2f, 1f);

        // Font sizes
        public const float TitleFontSize = 52f;
        public const float HeadingFontSize = 34f;
        public const float SubheadingFontSize = 24f;
        public const float BodyFontSize = 22f;
        public const float SmallFontSize = 18f;

        // Layout
        public const float StandardPadding = 24f;
        public const float StandardSpacing = 12f;
        public const float ButtonHeight = 64f;
        public const float SectionMinHeight = 120f;

        // Animation durations
        public const float PanelFadeDuration = 0.25f;
        public const float PanelScaleDuration = 0.3f;
        public const float SlideInDuration = 0.35f;
        public const float PulseDuration = 0.4f;
    }
}
