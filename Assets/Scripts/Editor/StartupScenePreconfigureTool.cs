#if UNITY_EDITOR
using DeliveryDriver.Quest;
using DeliveryDriver.Quest.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StartupScenePreconfigureTool
{
    [MenuItem("Tools/Delivery Driver/Optimize Startup/Preconfigure Active Scene")]
    public static void PreconfigureActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("[StartupScenePreconfigureTool] Active scene is not valid.");
            return;
        }

        bool changed = false;
        int configuredComponents = 0;

        QuestUISetup[] uiSetups = Object.FindObjectsByType<QuestUISetup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < uiSetups.Length; i++)
        {
            QuestUISetup setup = uiSetups[i];
            if (setup == null)
            {
                continue;
            }

            Undo.RecordObject(setup, "Bake Quest UI To Scene");
            setup.BakeQuestUiToScene();
            EditorUtility.SetDirty(setup);
            changed = true;
            configuredComponents++;
        }

        changed |= ConfigureComponents(Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include, FindObjectsSortMode.None), ref configuredComponents);
        changed |= ConfigureComponents(Object.FindObjectsByType<QuestUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None), ref configuredComponents);
        changed |= ConfigureComponents(Object.FindObjectsByType<DeliveryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None), ref configuredComponents);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"[StartupScenePreconfigureTool] Preconfigured startup references for {configuredComponents} component(s) in scene '{activeScene.name}'. Save the scene to persist the startup optimization.");
        }
        else
        {
            Debug.Log($"[StartupScenePreconfigureTool] No startup reference changes were needed in scene '{activeScene.name}'.");
        }
    }

    private static bool ConfigureComponents(QuestManager[] managers, ref int configuredComponents)
    {
        bool changed = false;
        for (int i = 0; i < managers.Length; i++)
        {
            QuestManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            Undo.RecordObject(manager, "Auto Assign QuestManager Startup References");
            if (!manager.AutoAssignStartupReferences())
            {
                continue;
            }

            EditorUtility.SetDirty(manager);
            configuredComponents++;
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureComponents(QuestUIManager[] managers, ref int configuredComponents)
    {
        bool changed = false;
        for (int i = 0; i < managers.Length; i++)
        {
            QuestUIManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            Undo.RecordObject(manager, "Auto Assign QuestUIManager Startup References");
            if (!manager.AutoAssignStartupReferences())
            {
                continue;
            }

            EditorUtility.SetDirty(manager);
            configuredComponents++;
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureComponents(DeliveryManager[] managers, ref int configuredComponents)
    {
        bool changed = false;
        for (int i = 0; i < managers.Length; i++)
        {
            DeliveryManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            Undo.RecordObject(manager, "Auto Assign DeliveryManager Startup References");
            if (!manager.AutoAssignStartupReferences())
            {
                continue;
            }

            EditorUtility.SetDirty(manager);
            configuredComponents++;
            changed = true;
        }

        return changed;
    }
}
#endif
