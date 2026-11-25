using UnityEngine;
using TMPro;
using System.Collections;
using System.Text;
using DeliveryDriver.UI;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Handles displaying neighborhood name on screen when player enters a neighborhood.
    /// </summary>
    public class NeighborhoodUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject neighborhoodPanel;
        [SerializeField] private TextMeshProUGUI neighborhoodNameText;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private Coroutine currentDisplayCoroutine;
        private CanvasGroup canvasGroup;
        private string currentNeighborhoodName;
        public bool HasValidReferences => neighborhoodPanel != null && neighborhoodNameText != null;

        private void Awake()
        {
            EnsureCanvasGroup();
        }

        private void OnEnable()
        {
            LocalizationTable.OnLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationTable.OnLocaleChanged -= HandleLocaleChanged;
        }

        public void ShowNeighborhoodName(string neighborhoodName)
        {
            if (neighborhoodPanel == null || neighborhoodNameText == null)
            {
                Debug.LogWarning("[NeighborhoodUI] UI references not set.");
                return;
            }

            if (string.IsNullOrWhiteSpace(neighborhoodName))
            {
                return;
            }

            if (currentDisplayCoroutine != null)
            {
                StopCoroutine(currentDisplayCoroutine);
            }

            currentNeighborhoodName = neighborhoodName;
            currentDisplayCoroutine = StartCoroutine(DisplayNeighborhoodNameRoutine(neighborhoodName));
        }

        public void ConfigureReferences(GameObject panel, TextMeshProUGUI text)
        {
            neighborhoodPanel = panel;
            neighborhoodNameText = text;
            EnsureCanvasGroup();
        }

        private IEnumerator DisplayNeighborhoodNameRoutine(string name)
        {
            neighborhoodNameText.text = GetLocalizedNeighborhoodName(name);
            neighborhoodPanel.SetActive(true);

            // Fade in
            yield return FadeCanvasGroup(canvasGroup, 0f, 1f, fadeInDuration);

            // Display
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            yield return FadeCanvasGroup(canvasGroup, 1f, 0f, fadeOutDuration);

            neighborhoodPanel.SetActive(false);
            currentNeighborhoodName = null;
            currentDisplayCoroutine = null;
        }

        private void HandleLocaleChanged()
        {
            if (neighborhoodNameText == null || string.IsNullOrWhiteSpace(currentNeighborhoodName))
            {
                return;
            }

            neighborhoodNameText.text = GetLocalizedNeighborhoodName(currentNeighborhoodName);
        }

        private static string GetLocalizedNeighborhoodName(string rawName)
        {
            string key = BuildNeighborhoodLocalizationKey(rawName);
            string localizedName = LocalizationTable.Get(key);
            return localizedName != key ? localizedName : rawName;
        }

        private static string BuildNeighborhoodLocalizationKey(string rawName)
        {
            StringBuilder builder = new StringBuilder("neighborhood_");
            for (int i = 0; i < rawName.Length; i++)
            {
                char c = rawName[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(NormalizeTurkishCharacter(c)));
                }
                else if (builder[builder.Length - 1] != '_')
                {
                    builder.Append('_');
                }
            }

            if (builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.ToString();
        }

        private static char NormalizeTurkishCharacter(char value)
        {
            return value switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'I' or 'İ' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => value
            };
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float startAlpha, float endAlpha, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                yield return null;
            }

            group.alpha = endAlpha;
        }

        private void EnsureCanvasGroup()
        {
            if (neighborhoodPanel == null)
            {
                canvasGroup = null;
                return;
            }

            canvasGroup = neighborhoodPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = neighborhoodPanel.AddComponent<CanvasGroup>();
            }

            neighborhoodPanel.SetActive(false);
        }

        public void HideNeighborhoodName()
        {
            if (currentDisplayCoroutine != null)
            {
                StopCoroutine(currentDisplayCoroutine);
                currentDisplayCoroutine = null;
            }

            if (neighborhoodPanel != null)
            {
                neighborhoodPanel.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            displayDuration = Mathf.Max(0.1f, displayDuration);
            fadeInDuration = Mathf.Max(0.1f, fadeInDuration);
            fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);
        }
#endif
    }
}
