using UnityEngine;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Company
{
    /// <summary>
    /// Filo yönetim arayüzünü açan dünya etkileşim noktası (Hologram).
    /// </summary>
    public class FleetInteractionPoint : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float interactionRadius = 5f;
        [SerializeField] private string interactionMessage = "E - Filoyu Yönet";

        [Header("Visuals")]
        [SerializeField] private GameObject hologramPrefab;
        [SerializeField] private Vector3 hologramOffset = new Vector3(0, 0.05f, 0);

        private bool isPlayerInside;
        private GameObject spawnedHologram;

        private void Start()
        {
            EnsureHologram();
        }

        private void EnsureHologram()
        {
            if (spawnedHologram != null) return;

            if (hologramPrefab != null)
            {
                spawnedHologram = Instantiate(hologramPrefab, transform.position + hologramOffset, transform.rotation, transform);
            }
            else
            {
                // Görsel İyileştirme: Daha kalın ve belirgin bir 'Enerji Platformu'
                spawnedHologram = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spawnedHologram.name = "FleetHologramProxy";
                spawnedHologram.transform.SetParent(transform);
                spawnedHologram.transform.localPosition = hologramOffset + Vector3.up * 0.1f;
                spawnedHologram.transform.localScale = new Vector3(interactionRadius * 0.9f, 0.1f, interactionRadius * 0.9f);
                
                var renderer = spawnedHologram.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // URP compatible approach
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    
                    renderer.material = new Material(shader);
                    
                    // Transparent setup for URP
                    renderer.material.SetFloat("_Surface", 1); // 1 is Transparent
                    renderer.material.SetFloat("_Blend", 0); // 0 is Alpha
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.renderQueue = 3000;
                    
                    Color hologramColor = new Color(0, 0.8f, 1f, 0.4f);
                    renderer.material.color = hologramColor;
                    renderer.material.SetColor("_BaseColor", hologramColor);
                    
                    // Emission for that glowy look
                    renderer.material.EnableKeyword("_EMISSION");
                    Color emissionColor = new Color(0, 0.4f, 0.6f) * 2f;
                    renderer.material.SetColor("_EmissionColor", emissionColor);
                }
                
                var collider = spawnedHologram.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
        }

        private void Update()
        {
            // Menü açıkken veya oyun durmuşken (menü hariç) etkileşime girme
            if (Time.timeScale <= 0 && (FleetManagementUI.Instance == null || !FleetManagementUI.Instance.IsOpen)) return; 

            Transform player = GetPlayerTransform();
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.position);
            bool isInside = distance <= interactionRadius;

            if (isInside)
            {
                if (WasInteractionKeyPressed())
                {
                    OpenFleetManagement();
                }
            }
            
            isPlayerInside = isInside;
        }

        private bool WasInteractionKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.eKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.E);
        }

        private void OnGUI()
        {
            if (isPlayerInside && Time.timeScale > 0)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 32; // Increased size
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.cyan;
                
                string hint = GetInteractionHint();
                Rect rect = new Rect(Screen.width / 2 - 200, Screen.height - 150, 400, 60);
                GUI.Label(rect, hint, style);
            }
        }

        private string GetInteractionHint()
        {
            string localizedHint = LocalizationTable.Get("fleet_interaction_hint");
            if (!string.IsNullOrWhiteSpace(localizedHint) && localizedHint != "fleet_interaction_hint")
            {
                return localizedHint;
            }

            return interactionMessage;
        }

        private float lastOpenTime;
        private const float OpenCooldown = 0.5f;

        private void OpenFleetManagement()
        {
            if (Time.unscaledTime - lastOpenTime < OpenCooldown) return;
            
            FleetManagementUI ui = FleetManagementUI.Instance;
            if (ui == null)
            {
                GameObject uiObj = new GameObject("FleetManagementUI");
                ui = uiObj.AddComponent<FleetManagementUI>();
            }
            
            if (ui.IsOpen) return;
            
            lastOpenTime = Time.unscaledTime;
            ui.Open();
        }

        private Transform GetPlayerTransform()
        {
            if (QuestManager.Instance != null && QuestManager.Instance.PlayerTransform != null)
                return QuestManager.Instance.PlayerTransform;
            
            return GameObject.FindWithTag("Player")?.transform;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
