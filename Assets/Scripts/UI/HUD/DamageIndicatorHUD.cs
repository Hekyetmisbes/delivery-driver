using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeliveryDriver.UI
{
    public class DamageIndicatorHUD : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float maxDamage = 100f;

        private float currentDamage;
        private Image damageIcon;
        private TextMeshProUGUI damageText;
        private RectTransform rootRect;
        private Rigidbody playerRigidbody;
        private float lastCollisionTime;
        private float pulseTimer;
        private bool isPulsing;

        private static readonly Color HealthyColor = new Color(0.3f, 0.95f, 0.4f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f, 1f);

        public void Initialize(Rigidbody rb)
        {
            playerRigidbody = rb;
            if (rb != null)
            {
                DamageCollisionReporter reporter = rb.gameObject.GetComponent<DamageCollisionReporter>();
                if (reporter == null)
                {
                    reporter = rb.gameObject.AddComponent<DamageCollisionReporter>();
                }
                reporter.indicator = this;
            }
            EnsureUI();
        }

        public void OnVehicleCollision(float impactForce)
        {
            float damage = Mathf.Clamp(impactForce * 0.5f, 1f, 25f);
            currentDamage = Mathf.Min(currentDamage + damage, maxDamage);
            isPulsing = true;
            pulseTimer = 0.4f;
            UpdateDisplay();
        }

        private void Update()
        {
            if (!isPulsing || rootRect == null) return;

            pulseTimer -= Time.deltaTime;
            if (pulseTimer > 0f)
            {
                float scale = 1f + 0.15f * Mathf.Sin(pulseTimer * 20f);
                rootRect.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                rootRect.localScale = Vector3.one;
                isPulsing = false;
            }
        }

        private void EnsureUI()
        {
            if (rootRect != null) return;

            Canvas canvas = GetOrCreateHudCanvas();
            if (canvas == null) return;

            GameObject root = new GameObject("DamageIndicator", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-32f, 260f);
            rootRect.sizeDelta = new Vector2(48f, 48f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.18f, 0.75f);
            bg.raycastTarget = false;

            damageIcon = bg;

            GameObject textObj = new GameObject("DamagePercent", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(root.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            damageText = textObj.GetComponent<TextMeshProUGUI>();
            damageText.alignment = TextAlignmentOptions.Center;
            damageText.fontSize = 16f;
            damageText.fontStyle = FontStyles.Bold;
            damageText.color = HealthyColor;
            damageText.text = "100%";
            if (TMP_Settings.defaultFontAsset != null)
            {
                damageText.font = TMP_Settings.defaultFontAsset;
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (damageText == null) return;

            float healthPercent = Mathf.Clamp01(1f - currentDamage / maxDamage);
            damageText.text = $"{Mathf.RoundToInt(healthPercent * 100)}%";

            Color color;
            if (healthPercent > 0.6f)
                color = HealthyColor;
            else if (healthPercent > 0.3f)
                color = Color.Lerp(WarningColor, HealthyColor, (healthPercent - 0.3f) / 0.3f);
            else
                color = Color.Lerp(DangerColor, WarningColor, healthPercent / 0.3f);

            damageText.color = color;
        }

        private static Canvas GetOrCreateHudCanvas()
        {
            GameObject existing = GameObject.Find("GameplayHUDCanvas");
            if (existing != null)
            {
                Canvas c = existing.GetComponent<Canvas>();
                if (c != null) return c;
            }
            return null;
        }
    }

    public class DamageCollisionReporter : MonoBehaviour
    {
        [HideInInspector] public DamageIndicatorHUD indicator;

        private void OnCollisionEnter(Collision collision)
        {
            if (indicator == null) return;
            float force = collision.impulse.magnitude / Mathf.Max(1f, Time.fixedDeltaTime);
            float normalized = force / 10000f;
            if (normalized > 0.05f)
            {
                indicator.OnVehicleCollision(normalized * 50f);
            }
        }
    }
}
