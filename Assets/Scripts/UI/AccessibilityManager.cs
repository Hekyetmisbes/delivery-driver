using TMPro;
using DeliveryDriver.Quest;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private const float SemanticColorMinimumBrightness = 0.42f;
        private const float SemanticColorMinimumSaturation = 0.22f;
        private const float SemanticColorMatchThreshold = 0.22f;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject accessibilityObject = new GameObject("AccessibilityManager");
            accessibilityObject.AddComponent<AccessibilityManager>();
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
            GameSettings.OnAccessibilityChanged += OnAccessibilityChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshAll();
        }

        private void OnDestroy()
        {
            GameSettings.OnAccessibilityChanged -= OnAccessibilityChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
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

        public static Color RemapSemanticColor(Color color)
        {
            if (GameSettings.Instance == null)
            {
                return color;
            }

            int mode = Mathf.Clamp(GameSettings.Instance.ColorBlindMode, 0, Palettes.Length - 1);
            if (mode == 0)
            {
                return color;
            }

            int semanticIndex = FindSemanticColorIndex(color);
            if (semanticIndex < 0)
            {
                return color;
            }

            Color remapped = Palettes[mode][semanticIndex];
            remapped.a = color.a;
            return remapped;
        }

        public static void RefreshAll()
        {
            EnsureInstance();
            instance.ApplyToActiveUi();
        }

        public static void ApplyTo(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            EnsureInstance();
            instance.ApplyToRoot(root);
        }

        private void OnAccessibilityChanged()
        {
            ApplyToActiveUi();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(RefreshAfterSceneLoad());
        }

        private System.Collections.IEnumerator RefreshAfterSceneLoad()
        {
            yield return null;
            ApplyToActiveUi();
        }

        private void ApplyToActiveUi()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    ApplyToRoot(canvases[i].gameObject);
                }
            }
        }

        private void ApplyToRoot(GameObject root)
        {
            ApplyTextSettings(root.GetComponentsInChildren<TextMeshProUGUI>(true));
            ApplyGraphicSettings(root.GetComponentsInChildren<Graphic>(true));
            ApplySelectableSettings(root.GetComponentsInChildren<Selectable>(true));
        }

        private void ApplyTextSettings(TextMeshProUGUI[] texts)
        {
            float scale = GetTextScale();
            bool highContrast = IsHighContrast();

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null)
                {
                    continue;
                }

                AccessibilityTextState state = text.GetComponent<AccessibilityTextState>();
                if (state == null)
                {
                    state = text.gameObject.AddComponent<AccessibilityTextState>();
                    state.Capture(text);
                }

                text.fontSize = state.BaseFontSize * scale;
                text.outlineWidth = highContrast ? Mathf.Max(state.BaseOutlineWidth, 0.3f) : state.BaseOutlineWidth;
                text.color = highContrast ? Color.white : RemapSemanticColor(state.BaseColor);
            }
        }

        private void ApplyGraphicSettings(Graphic[] graphics)
        {
            bool highContrast = IsHighContrast();

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || graphic is TextMeshProUGUI)
                {
                    continue;
                }

                AccessibilityGraphicState state = graphic.GetComponent<AccessibilityGraphicState>();
                if (state == null)
                {
                    state = graphic.gameObject.AddComponent<AccessibilityGraphicState>();
                    state.Capture(graphic);
                }

                Color color = RemapSemanticColor(state.BaseColor);
                if (highContrast && state.BaseColor.a > 0.2f && IsPanelLikeColor(state.BaseColor))
                {
                    color.a = Mathf.Max(color.a, 0.98f);
                }

                graphic.color = color;
            }
        }

        private void ApplySelectableSettings(Selectable[] selectables)
        {
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null)
                {
                    continue;
                }

                AccessibilitySelectableState state = selectable.GetComponent<AccessibilitySelectableState>();
                if (state == null)
                {
                    state = selectable.gameObject.AddComponent<AccessibilitySelectableState>();
                    state.Capture(selectable);
                }

                selectable.colors = RemapColorBlock(state.BaseColors);
            }
        }

        private static ColorBlock RemapColorBlock(ColorBlock colors)
        {
            colors.normalColor = RemapSemanticColor(colors.normalColor);
            colors.highlightedColor = RemapSemanticColor(colors.highlightedColor);
            colors.pressedColor = RemapSemanticColor(colors.pressedColor);
            colors.selectedColor = RemapSemanticColor(colors.selectedColor);
            colors.disabledColor = RemapSemanticColor(colors.disabledColor);
            return colors;
        }

        private static int FindSemanticColorIndex(Color color)
        {
            if (color.a <= 0.05f)
            {
                return -1;
            }

            Color.RGBToHSV(color, out _, out float saturation, out float value);
            if (value < SemanticColorMinimumBrightness || saturation < SemanticColorMinimumSaturation)
            {
                return -1;
            }

            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < NormalPalette.Length; i++)
            {
                float distance = ColorDistanceRgb(color, NormalPalette[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestDistance <= SemanticColorMatchThreshold)
            {
                return bestIndex;
            }

            if (color.g > color.r * 1.12f && color.g > color.b * 1.05f)
            {
                return (int)SemanticColor.Positive;
            }

            if (color.r > color.g * 1.12f && color.r > color.b * 1.08f)
            {
                return color.g > 0.45f ? (int)SemanticColor.Warning : (int)SemanticColor.Negative;
            }

            if (color.b > color.r * 1.08f && color.b > color.g * 1.02f)
            {
                return (int)SemanticColor.Info;
            }

            return -1;
        }

        private static float ColorDistanceRgb(Color a, Color b)
        {
            float r = a.r - b.r;
            float g = a.g - b.g;
            float bValue = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bValue * bValue);
        }

        private static bool IsPanelLikeColor(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            return value < 0.35f || saturation < 0.18f;
        }
    }

    internal sealed class AccessibilityTextState : MonoBehaviour
    {
        public float BaseFontSize { get; private set; }
        public Color BaseColor { get; private set; }
        public float BaseOutlineWidth { get; private set; }

        public void Capture(TextMeshProUGUI text)
        {
            BaseFontSize = text.fontSize;
            BaseColor = text.color;
            BaseOutlineWidth = text.outlineWidth;
        }
    }

    internal sealed class AccessibilityGraphicState : MonoBehaviour
    {
        public Color BaseColor { get; private set; }

        public void Capture(Graphic graphic)
        {
            BaseColor = graphic.color;
        }
    }

    internal sealed class AccessibilitySelectableState : MonoBehaviour
    {
        public ColorBlock BaseColors { get; private set; }

        public void Capture(Selectable selectable)
        {
            BaseColors = selectable.colors;
        }
    }
}
