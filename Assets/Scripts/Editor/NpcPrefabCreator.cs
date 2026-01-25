using UnityEngine;
using UnityEditor;
using System.IO;
using TrafficSystem;

/// <summary>
/// Editor tool to automatically create NPC vehicle prefabs from existing car prefabs
/// </summary>
public class NpcPrefabCreator : EditorWindow
{
    private string[] sourcePrefabPaths = new string[]
    {
        "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/Minivan.prefab",
        "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/LorryCargo.prefab",
        "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/Police.prefab",
        "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/Muscle.prefab",
        "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/Ambulance.prefab"
    };

    private string outputFolder = "Assets/Prefabs/NPCs";

    [MenuItem("Tools/Traffic System/Create NPC Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<NpcPrefabCreator>("NPC Prefab Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("NPC Prefab Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Bu araç mevcut araç prefablarından otomatik olarak NPC versiyonları oluşturur.\n\n" +
            "Eklenen component'ler:\n" +
            "- NpcCarAgent (sürüş kontrolü)\n" +
            "- NpcRecovery (kurtarma sistemi)\n" +
            "- WheelCollider'lar (4 adet)\n" +
            "- Rigidbody ayarları",
            MessageType.Info
        );

        GUILayout.Space(10);

        outputFolder = EditorGUILayout.TextField("Output Klasörü:", outputFolder);

        GUILayout.Space(10);

        if (GUILayout.Button("NPC Prefablarını Oluştur", GUILayout.Height(40)))
        {
            CreateNpcPrefabs();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Test: Scene'e Minivan Ekle"))
        {
            CreateTestVehicleInScene();
        }
    }

    private void CreateNpcPrefabs()
    {
        // Output klasörünü oluştur
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        int successCount = 0;
        int failCount = 0;

        foreach (string prefabPath in sourcePrefabPaths)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"Prefab bulunamadı: {prefabPath}");
                failCount++;
                continue;
            }

            try
            {
                // Load source prefab
                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (sourcePrefab == null)
                {
                    Debug.LogWarning($"Prefab yüklenemedi: {prefabPath}");
                    failCount++;
                    continue;
                }

                // Instantiate in scene
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
                instance.name = sourcePrefab.name + "_NPC";

                // Setup NPC components
                SetupNpcComponents(instance);

                // Save as new prefab
                string fileName = Path.GetFileNameWithoutExtension(prefabPath);
                string newPrefabPath = Path.Combine(outputFolder, fileName + "_NPC.prefab");

                PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
                DestroyImmediate(instance);

                Debug.Log($"✓ NPC prefab oluşturuldu: {newPrefabPath}");
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"✗ Hata: {prefabPath} - {e.Message}");
                failCount++;
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "NPC Prefab Oluşturma Tamamlandı",
            $"Başarılı: {successCount}\nHatalı: {failCount}\n\nPrefablar şurada: {outputFolder}",
            "Tamam"
        );
    }

    private void SetupNpcComponents(GameObject vehicle)
    {
        // Rigidbody ekle/ayarla
        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = vehicle.AddComponent<Rigidbody>();
        }
        rb.mass = 1500f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // WheelCollider'ları ekle veya bul
        SetupWheelColliders(vehicle);

        // NpcCarAgent ekle
        NpcCarAgent carAgent = vehicle.GetComponent<NpcCarAgent>();
        if (carAgent == null)
        {
            carAgent = vehicle.AddComponent<NpcCarAgent>();
        }

        // WheelCollider'ları otomatik ata
        WheelCollider[] wheelColliders = vehicle.GetComponentsInChildren<WheelCollider>();
        if (wheelColliders.Length >= 4)
        {
            // Genellikle sıralama: FL, FR, RL, RR
            AssignWheelColliders(carAgent, wheelColliders);
        }

        // NpcRecovery ekle
        NpcRecovery recovery = vehicle.GetComponent<NpcRecovery>();
        if (recovery == null)
        {
            recovery = vehicle.AddComponent<NpcRecovery>();
        }

        Debug.Log($"  - Component'ler eklendi: {vehicle.name}");
    }

    private void SetupWheelColliders(GameObject vehicle)
    {
        // Mevcut wheel collider'ları kontrol et
        WheelCollider[] existingColliders = vehicle.GetComponentsInChildren<WheelCollider>();
        if (existingColliders.Length >= 4)
        {
            Debug.Log($"  - Mevcut {existingColliders.Length} WheelCollider bulundu");
            return;
        }

        // Wheel mesh'leri bul
        Transform[] wheels = FindWheelMeshes(vehicle.transform);
        if (wheels.Length < 4)
        {
            Debug.LogWarning($"  - Yeterli wheel mesh bulunamadı (bulundu: {wheels.Length})");
            CreateDefaultWheelColliders(vehicle);
            return;
        }

        // Her wheel mesh için WheelCollider ekle
        foreach (Transform wheelMesh in wheels)
        {
            if (wheelMesh.GetComponent<WheelCollider>() == null)
            {
                WheelCollider wc = wheelMesh.gameObject.AddComponent<WheelCollider>();
                ConfigureWheelCollider(wc);
            }
        }

        Debug.Log($"  - {wheels.Length} WheelCollider oluşturuldu");
    }

    private Transform[] FindWheelMeshes(Transform root)
    {
        System.Collections.Generic.List<Transform> wheels = new System.Collections.Generic.List<Transform>();

        // "Wheel" kelimesini içeren child'ları bul
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("wheel") && child.GetComponent<MeshRenderer>() != null)
            {
                wheels.Add(child);
            }
        }

        return wheels.ToArray();
    }

    private void CreateDefaultWheelColliders(GameObject vehicle)
    {
        // Varsayılan pozisyonlarda wheel collider'lar oluştur
        GameObject wheelsParent = new GameObject("WheelColliders");
        wheelsParent.transform.SetParent(vehicle.transform);
        wheelsParent.transform.localPosition = Vector3.zero;

        Vector3[] positions = new Vector3[]
        {
            new Vector3(-0.8f, 0.3f, 1.5f),  // Front Left
            new Vector3(0.8f, 0.3f, 1.5f),   // Front Right
            new Vector3(-0.8f, 0.3f, -1.5f), // Rear Left
            new Vector3(0.8f, 0.3f, -1.5f)   // Rear Right
        };

        string[] names = new string[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject wheelGO = new GameObject(names[i]);
            wheelGO.transform.SetParent(wheelsParent.transform);
            wheelGO.transform.localPosition = positions[i];

            WheelCollider wc = wheelGO.AddComponent<WheelCollider>();
            ConfigureWheelCollider(wc);
        }

        Debug.Log("  - Varsayılan WheelCollider'lar oluşturuldu");
    }

    private void ConfigureWheelCollider(WheelCollider wc)
    {
        wc.mass = 20f;
        wc.radius = 0.35f;
        wc.wheelDampingRate = 0.25f;
        wc.suspensionDistance = 0.3f;
        wc.forceAppPointDistance = 0f;

        JointSpring spring = wc.suspensionSpring;
        spring.spring = 35000f;
        spring.damper = 4500f;
        spring.targetPosition = 0.5f;
        wc.suspensionSpring = spring;

        WheelFrictionCurve forwardFriction = wc.forwardFriction;
        forwardFriction.extremumSlip = 0.4f;
        forwardFriction.extremumValue = 1f;
        forwardFriction.asymptoteSlip = 0.8f;
        forwardFriction.asymptoteValue = 0.5f;
        forwardFriction.stiffness = 1f;
        wc.forwardFriction = forwardFriction;

        WheelFrictionCurve sidewaysFriction = wc.sidewaysFriction;
        sidewaysFriction.extremumSlip = 0.2f;
        sidewaysFriction.extremumValue = 1f;
        sidewaysFriction.asymptoteSlip = 0.5f;
        sidewaysFriction.asymptoteValue = 0.75f;
        sidewaysFriction.stiffness = 1f;
        wc.sidewaysFriction = sidewaysFriction;
    }

    private void AssignWheelColliders(NpcCarAgent carAgent, WheelCollider[] colliders)
    {
        // İsimlere göre ata
        foreach (var wc in colliders)
        {
            string name = wc.name.ToLower();
            if (name.Contains("fl") || (name.Contains("front") && name.Contains("left")))
            {
                SetField(carAgent, "frontLeftCollider", wc);
            }
            else if (name.Contains("fr") || (name.Contains("front") && name.Contains("right")))
            {
                SetField(carAgent, "frontRightCollider", wc);
            }
            else if (name.Contains("rl") || (name.Contains("rear") && name.Contains("left")))
            {
                SetField(carAgent, "rearLeftCollider", wc);
            }
            else if (name.Contains("rr") || (name.Contains("rear") && name.Contains("right")))
            {
                SetField(carAgent, "rearRightCollider", wc);
            }
        }

        // Eğer atanmadıysa, pozisyona göre ata
        if (GetField<WheelCollider>(carAgent, "frontLeftCollider") == null && colliders.Length >= 4)
        {
            SetField(carAgent, "frontLeftCollider", colliders[0]);
            SetField(carAgent, "frontRightCollider", colliders[1]);
            SetField(carAgent, "rearLeftCollider", colliders[2]);
            SetField(carAgent, "rearRightCollider", colliders[3]);
        }
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }

    private T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (T)field.GetValue(obj);
        }
        return default(T);
    }

    private void CreateTestVehicleInScene()
    {
        string testPrefabPath = "Assets/Nebula - Free low poly car pack/Prefabs/Simple Vehicles/Minivan.prefab";
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(testPrefabPath);

        if (sourcePrefab == null)
        {
            EditorUtility.DisplayDialog("Hata", "Minivan prefab bulunamadı!", "Tamam");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        instance.name = "TestNPC_Minivan";
        instance.transform.position = Vector3.up * 2f;

        SetupNpcComponents(instance);

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);

        Debug.Log("✓ Test NPC Minivan scene'e eklendi!");
    }
}
