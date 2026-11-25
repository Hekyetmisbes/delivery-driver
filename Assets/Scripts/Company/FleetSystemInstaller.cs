using UnityEngine;
using UnityEngine.SceneManagement;
using DeliveryDriver.Quest;
using System.Collections;

namespace DeliveryDriver.Company
{
    /// <summary>
    /// Filo yönetim sistemini (Hologram ve UI) sahneye yerleştiren başlatıcı.
    /// </summary>
    public class FleetSystemInstaller : MonoBehaviour
    {
        private const string GameSceneName = "Game";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!SceneManager.GetActiveScene().name.Equals(GameSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;

            GameObject installerObj = new GameObject("FleetSystemInstaller_Runtime");
            installerObj.AddComponent<FleetSystemInstaller>();
        }

        private void Start()
        {
            StartCoroutine(SetupFleetSystemsRoutine());
        }

        private IEnumerator SetupFleetSystemsRoutine()
        {
            // 1. FleetManagementUI'ı oluştur (Eğer yoksa)
            if (Object.FindAnyObjectByType<FleetManagementUI>() == null)
            {
                GameObject uiObj = new GameObject("FleetManagementUI");
                uiObj.AddComponent<FleetManagementUI>();
                Object.DontDestroyOnLoad(uiObj);
                Debug.Log("[FleetSystemInstaller] FleetManagementUI created.");
            }

            Vector3 spawnPos = Vector3.zero;
            bool posFound = false;
            float searchStartTime = Time.time;
            float maxSearchDuration = 5.0f; // 5 saniye boyunca aramaya devam et

            while (!posFound && (Time.time - searchStartTime) < maxSearchDuration)
            {
                // Önce oyuncuyu dene
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                {
                    // Alternatif: CarController olan herhangi bir objeyi ara
                    var car = Object.FindAnyObjectByType<CarController>();
                    if (car != null) player = car.gameObject;
                }

                if (player != null)
                {
                    // Oyuncunun 5 metre önünde oluştur
                    spawnPos = player.transform.position + player.transform.forward * 5f;
                    spawnPos.y = player.transform.position.y; // Oyuncuyla aynı yükseklik
                    posFound = true;
                    Debug.Log($"[FleetSystemInstaller] Found player! Positioning hologram at {spawnPos}");
                }

                // Eğer oyuncu hala yoksa tabelayı (SignText) daha kapsamlı ara
                if (!posFound)
                {
                    GameObject sign = GameObject.Find("SignText");
                    if (sign == null)
                    {
                        // Parçalı isimle ara
                        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                        foreach (var obj in allObjects)
                        {
                            if (obj.name.Contains("SignText"))
                            {
                                sign = obj;
                                break;
                            }
                        }
                    }

                    if (sign != null)
                    {
                        // Tabelanın önünde bir yer belirle
                        spawnPos = sign.transform.position + Vector3.down * 2f; // Önce biraz aşağı in
                        
                        // Zemin tespiti (Raycast)
                        RaycastHit hit;
                        if (Physics.Raycast(sign.transform.position + Vector3.up * 5f, Vector3.down, out hit, 20f))
                        {
                            spawnPos = hit.point + Vector3.up * 0.05f; // Zeminin hemen üstü
                        }
                        else
                        {
                            spawnPos.y = 0; // Fallback
                        }
                        
                        posFound = true;
                        Debug.Log($"[FleetSystemInstaller] Found sign! Positioning hologram at {spawnPos} (Ground height: {spawnPos.y})");
                    }
                }

                if (!posFound) yield return new WaitForSeconds(0.5f);
            }

            // Eğer hala pozisyon bulunamadıysa oyuncunun olduğu yeri kullan (Son çare)
            if (!posFound)
            {
                var car = Object.FindAnyObjectByType<CarController>();
                if (car != null) 
                {
                    spawnPos = car.transform.position + car.transform.forward * 3f;
                    posFound = true;
                }
            }

            // 2. FleetInteractionPoint'i (Hologram) oluştur
            if (Object.FindAnyObjectByType<FleetInteractionPoint>() == null)
            {
                if (!posFound)
                {
                    Debug.LogWarning("[FleetSystemInstaller] Could not find player or sign after 5s. Using default position (0,0,0).");
                    spawnPos = Vector3.zero;
                }

                GameObject fleetPoint = new GameObject("FleetInteractionPoint");
                fleetPoint.transform.position = spawnPos;
                fleetPoint.AddComponent<FleetInteractionPoint>();
                Debug.Log($"[FleetSystemInstaller] FINAL: Fleet Interaction Point created at {spawnPos}");
            }

            Destroy(gameObject);
        }
    }
}
