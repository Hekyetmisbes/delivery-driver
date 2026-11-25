using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UIKit
{
    /// <summary>Design tokens; safe to use alongside existing UIThemeConstants.</summary>
    public static class DDUITheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1600, 900);
        public static readonly Color Background = Hex("#09121B");
        public static readonly Color Surface = Hex("#142530");
        public static readonly Color RaisedSurface = Hex("#1C303B");
        public static readonly Color Border = Hex("#37505B");
        public static readonly Color Primary = Hex("#FF921E");
        public static readonly Color Navigation = Hex("#52D7DF");
        public static readonly Color Success = Hex("#44E4AB");
        public static readonly Color Warning = Hex("#FFCD65");
        public static readonly Color Danger = Hex("#FF6172");
        public static readonly Color TextPrimary = Hex("#F6F9FA");
        public static readonly Color TextSecondary = Hex("#9DB0BA");

        public static Color Hex(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var color)) return Color.white;
            return color;
        }

        public static void SetupCanvas(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
