using UnityEngine;
using TMPro;

namespace DeliveryDriver.UI
{
    /// <summary>
    /// Sahnedeki "SignText" objesini bulur ve ana menüde girilen şirket ismini otomatik olarak atar.
    /// </summary>
    public class AutoSignTextSetter : MonoBehaviour
    {
        [SerializeField] private string targetGameObjectName = "SignText";
        [SerializeField] private string prefsKey = "delivery_driver_company_name";
        [SerializeField] private bool spawnFleetPointNearSign = true;

        private void Start()
        {
            ApplySignText();
            if (spawnFleetPointNearSign)
            {
                StartCoroutine(EnsureFleetPointRoutine());
            }
        }

        public void ApplySignText()
        {
            GameObject signObj = FindSignObject();
            if (signObj == null) return;

            TMP_Text tmpText = signObj.GetComponent<TMP_Text>() ?? signObj.GetComponentInChildren<TMP_Text>();

            if (tmpText != null)
            {
                string companyName = PlayerPrefs.GetString(prefsKey, "Delivery Co.");
                tmpText.text = companyName;
                Debug.Log($"[AutoSignTextSetter] Sign text updated to: {companyName}");
            }
        }

        private GameObject FindSignObject()
        {
            GameObject signObj = GameObject.Find(targetGameObjectName);
            if (signObj != null) return signObj;

            signObj = GameObject.FindWithTag("Sign");
            if (signObj != null) return signObj;

            TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (var t in allTexts)
            {
                if (t.gameObject.name.Contains("SignText")) return t.gameObject;
            }
            return null;
        }

        private System.Collections.IEnumerator EnsureFleetPointRoutine()
        {
            // Veritabanı ve diğer sistemlerin oturması için bekle
            yield return new WaitForSeconds(1.0f);

            if (Object.FindAnyObjectByType<DeliveryDriver.Company.FleetInteractionPoint>() != null) yield break;

            GameObject signObj = FindSignObject();
            Vector3 spawnPos = Vector3.zero;
            bool posFound = false;

            if (signObj != null)
            {
                // Tabelanın 2.5 metre önünde yer belirle
                spawnPos = signObj.transform.position + signObj.transform.forward * -2.5f;
                
                // Zemin tespiti
                if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
                {
                    spawnPos = hit.point + Vector3.up * 0.05f;
                }
                else
                {
                    spawnPos.y = signObj.transform.position.y - 1.5f; // Tahmini zemin
                }
                posFound = true;
            }

            if (!posFound)
            {
                // Oyuncu fallback
                GameObject player = GameObject.FindWithTag("Player") ?? Object.FindAnyObjectByType<CarController>()?.gameObject;
                if (player != null)
                {
                    spawnPos = player.transform.position + player.transform.forward * 4f;
                    posFound = true;
                }
            }

            if (posFound)
            {
                GameObject fleetPoint = new GameObject("FleetInteractionPoint");
                fleetPoint.transform.position = spawnPos;
                fleetPoint.AddComponent<DeliveryDriver.Company.FleetInteractionPoint>();
                Debug.Log($"[AutoSignTextSetter] Successfully spawned FleetInteractionPoint at {spawnPos}");
            }
        }
    }
}
