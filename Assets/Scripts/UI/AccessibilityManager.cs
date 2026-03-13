using UnityEngine;
using TMPro;
using DeliveryDriver.Quest;

namespace DeliveryDriver.UI
{
    public enum SemanticColor
    {
        Positive,
        Negative,
        Warning,
        Info,
        Speed,
        TimerSafe,
        TimerWarning,
        TimerDanger
    }

    public class AccessibilityManager : MonoBehaviour
    {
        private static AccessibilityManager instance;
        public static AccessibilityManager Instance => instance;

        // Normal vision palettes
        private static readonly Color[] NormalPalette =
        {
            new Color(0.3f, 0.95f, 0.45f, 1f),   // Positive
            new Color(1f, 0.35f, 0.3f, 1f),       // Negative
            new Color(1f, 0.85f, 0.2f, 1f),        // Warning
            new Color(0.3f, 0.7f, 1f, 1f),         // Info
            new Color(0f, 0.78f, 1f, 1f),           // Speed
            new Color(0.2f, 0.9f, 0.3f, 1f),       // TimerSafe
            new Color(1f, 0.85f, 0.2f, 1f),        // TimerWarning
            new Color(1f, 0.25f, 0.2f, 1f),        // TimerDanger
        };

        // Protanopia-friendly
        private static readonly Color[] ProtanopiaPalette =
        {
            new Color(0.4f, 0.7f, 1f, 1f),
            new Color(1f, 0.6f, 0f, 1f),
            new Color(1f, 1f, 0.4f, 1f),
            new Color(0.6f, 0.8f, 1f, 1f),
            new Color(0.5f, 0.75f, 1f, 1f),
            new Color(0.4f, 0.7f, 1f, 1f),
            new Color(1f, 1f, 0.4f, 1f),
            new Color(1f, 0.6f, 0f, 1f),
        };

        // Deuteranopia-friendly
        private static readonly Color[] DeuteranopiaPalette =
        {
            new Color(0.3f, 0.6f, 1f, 1f),
            new Color(1f, 0.55f, 0f, 1f),
            new Color(1f, 1f, 0.3f, 1f),
            new Color(0.5f, 0.75f, 1f, 1f),
            new Color(0.4f, 0.65f, 1f, 1f),
            new Color(0.3f, 0.6f, 1f, 1f),
            new Color(1f, 1f, 0.3f, 1f),
            new Color(1f, 0.55f, 0f, 1f),
        };

        // Tritanopia-friendly
        private static readonly Color[] TritanopiaPalette =
        {
            new Color(0.2f, 0.9f, 0.5f, 1f),
            new Color(1f, 0.3f, 0.4f, 1f),
            new Color(1f, 0.7f, 0.3f, 1f),
            new Color(0.3f, 0.85f, 0.85f, 1f),
            new Color(0.2f, 0.85f, 0.7f, 1f),
            new Color(0.2f, 0.9f, 0.5f, 1f),
            new Color(1f, 0.7f, 0.3f, 1f),
            new Color(1f, 0.3f, 0.4f, 1f),
        };

        private static readonly Color[][] Palettes = { NormalPalette, ProtanopiaPalette, DeuteranopiaPalette, TritanopiaPalette };

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            GameSettings.OnAccessibilityChanged += OnAccessibilityChanged;
        }

        private void OnDestroy()
        {
            GameSettings.OnAccessibilityChanged -= OnAccessibilityChanged;
            if (instance == this) instance = null;
        }

        public static Color GetSemanticColor(SemanticColor type)
        {
            int mode = 0;
            if (GameSettings.Instance != null)
            {
                mode = Mathf.Clamp(GameSettings.Instance.ColorBlindMode, 0, Palettes.Length - 1);
            }

            int index = (int)type;
            Color[] palette = Palettes[mode];
            if (index >= 0 && index < palette.Length)
            {
                return palette[index];
            }

            return Color.white;
        }

        public static float GetTextScale()
        {
            if (GameSettings.Instance != null)
            {
                return GameSettings.Instance.TextScaleMultiplier;
            }
            return 1f;
        }

        public static bool IsHighContrast()
        {
            return GameSettings.Instance != null && GameSettings.Instance.HighContrastMode;
        }

        public static void ApplyTextScale(TextMeshProUGUI text, float baseSize)
        {
            if (text == null) return;
            text.fontSize = baseSize * GetTextScale();
        }

        public static void ApplyHighContrastPanel(UnityEngine.UI.Image panelImage)
        {
            if (panelImage == null || !IsHighContrast()) return;
            Color c = panelImage.color;
            c.a = 0.98f;
            panelImage.color = c;
        }

        public static void ApplyHighContrastText(TextMeshProUGUI text)
        {
            if (text == null || !IsHighContrast()) return;
            text.outlineWidth = Mathf.Max(text.outlineWidth, 0.3f);
            text.color = Color.white;
        }

        private void OnAccessibilityChanged()
        {
            // Future: iterate active UI and refresh colors/text
        }
    }
}
