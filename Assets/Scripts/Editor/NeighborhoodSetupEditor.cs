using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using DeliveryDriver.City;

/// <summary>
/// Editor utility to automatically setup neighborhood UI in the scene
/// </summary>
public class NeighborhoodSetupEditor : EditorWindow
{
    [MenuItem("Tools/SimplePoly/Setup Neighborhood UI")]
    public static void ShowWindow()
    {
        GetWindow<NeighborhoodSetupEditor>("Neighborhood Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Neighborhood System Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Bu tool scene'e otomatik olarak şunları ekler:\n" +
            "• NeighborhoodManager (Singleton)\n" +
            "• NeighborhoodUI Canvas\n" +
            "• Mahalle ismi paneli ve text\n" +
            "• Gerekli tüm bağlantılar",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Setup Neighborhood System", GUILayout.Height(40)))
        {
            SetupNeighborhoodSystem();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Find Existing Setup", GUILayout.Height(30)))
        {
            FindExistingSetup();
        }
    }

    private void SetupNeighborhoodSystem()
    {
        // Check if already exists
        NeighborhoodManager existingManager = FindFirstObjectByType<NeighborhoodManager>();
        if (existingManager != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Already Exists",
                "NeighborhoodManager zaten scene'de var. Yeniden oluşturulsun mu?",
                "Evet, Yeniden Oluştur",
                "İptal");

            if (!replace)
            {
                return;
            }

            DestroyImmediate(existingManager.gameObject);
        }

        // Create NeighborhoodManager
        GameObject managerObj = new GameObject("NeighborhoodManager");
        NeighborhoodManager manager = managerObj.AddComponent<NeighborhoodManager>();
        Undo.RegisterCreatedObjectUndo(managerObj, "Create NeighborhoodManager");

        // Find or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("NeighborhoodCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            Debug.Log("[NeighborhoodSetup] Canvas oluşturuldu.");
        }

        // Create UI Panel
        GameObject panelObj = new GameObject("NeighborhoodPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0, -50);
        panelRect.sizeDelta = new Vector2(500, 100);

        // Add CanvasGroup for fading
        CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;

        Undo.RegisterCreatedObjectUndo(panelObj, "Create Neighborhood Panel");

        // Create Text
        GameObject textObj = new GameObject("NeighborhoodText");
        textObj.transform.SetParent(panelObj.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Mahalle İsmi";
        text.fontSize = 36;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        Undo.RegisterCreatedObjectUndo(textObj, "Create Neighborhood Text");

        // Create NeighborhoodUI component
        NeighborhoodUI ui = managerObj.AddComponent<NeighborhoodUI>();

        // Use reflection to set private fields
        var uiType = typeof(NeighborhoodUI);
        var panelField = uiType.GetField("neighborhoodPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var textField = uiType.GetField("neighborhoodNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (panelField != null) panelField.SetValue(ui, panelObj);
        if (textField != null) textField.SetValue(ui, text);

        // Mark objects as dirty
        EditorUtility.SetDirty(managerObj);
        EditorUtility.SetDirty(canvas.gameObject);

        // Hide panel initially
        panelObj.SetActive(false);

        Debug.Log("[NeighborhoodSetup] Neighborhood sistemi başarıyla kuruldu!");
        EditorUtility.DisplayDialog(
            "Başarılı!",
            "Neighborhood sistemi scene'e eklendi.\n\n" +
            "• NeighborhoodManager oluşturuldu\n" +
            "• UI Canvas ve Panel hazır\n" +
            "• Tüm bağlantılar yapıldı\n\n" +
            "Şimdi Editor Tool'dan 'Generate City' yapabilirsiniz!",
            "Tamam");

        Selection.activeGameObject = managerObj;
    }

    private void FindExistingSetup()
    {
        NeighborhoodManager manager = FindFirstObjectByType<NeighborhoodManager>();
        NeighborhoodUI ui = FindFirstObjectByType<NeighborhoodUI>();

        string message = "Scene'deki Durum:\n\n";

        if (manager != null)
        {
            message += "✓ NeighborhoodManager bulundu\n";
        }
        else
        {
            message += "✗ NeighborhoodManager bulunamadı\n";
        }

        if (ui != null)
        {
            message += "✓ NeighborhoodUI bulundu\n";
        }
        else
        {
            message += "✗ NeighborhoodUI bulunamadı\n";
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            message += "✓ Canvas bulundu\n";
        }
        else
        {
            message += "✗ Canvas bulunamadı\n";
        }

        Transform neighborhoodParent = GameObject.Find("Neighborhoods")?.transform;
        if (neighborhoodParent != null)
        {
            int zoneCount = 0;
            foreach (Transform child in neighborhoodParent)
            {
                if (child.GetComponent<NeighborhoodZone>() != null)
                {
                    zoneCount++;
                }
            }
            message += $"✓ {zoneCount} adet mahalle zone'u bulundu\n";
        }
        else
        {
            message += "✗ Neighborhoods parent bulunamadı\n";
        }

        EditorUtility.DisplayDialog("Scene Durumu", message, "Tamam");

        if (manager != null)
        {
            Selection.activeGameObject = manager.gameObject;
        }
    }
}
