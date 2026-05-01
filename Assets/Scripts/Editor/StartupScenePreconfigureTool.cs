#if UNITY_EDITOR
using DeliveryDriver.City;
using DeliveryDriver.Company;
using DeliveryDriver.Navigation;
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
        changed |= ConfigureComponents(Object.FindObjectsByType<ProgressionSceneInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None), ref configuredComponents);
        changed |= ConfigureComponents(Object.FindObjectsByType<GameSceneCompanyPageInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None), ref configuredComponents);
        changed |= PreconfigureRuntimeCreatedSceneSystems(activeScene, ref configuredComponents);

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

    private static bool ConfigureComponents(ProgressionSceneInstaller[] installers, ref int configuredComponents)
    {
        bool changed = false;
        for (int i = 0; i < installers.Length; i++)
        {
            ProgressionSceneInstaller installer = installers[i];
            if (installer == null)
            {
                continue;
            }

            Undo.RecordObject(installer, "Auto Assign Progression Startup References");
            if (!installer.AutoAssignStartupReferences())
            {
                continue;
            }

            EditorUtility.SetDirty(installer);
            configuredComponents++;
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureComponents(GameSceneCompanyPageInstaller[] installers, ref int configuredComponents)
    {
        bool changed = false;
        for (int i = 0; i < installers.Length; i++)
        {
            GameSceneCompanyPageInstaller installer = installers[i];
            if (installer == null)
            {
                continue;
            }

            Undo.RecordObject(installer, "Auto Assign Company Startup References");
            if (!installer.AutoAssignStartupReferences())
            {
                continue;
            }

            EditorUtility.SetDirty(installer);
            configuredComponents++;
            changed = true;
        }

        return changed;
    }

    private static bool PreconfigureRuntimeCreatedSceneSystems(Scene activeScene, ref int configuredComponents)
    {
        bool changed = false;

        if (activeScene.name == "MainMenu")
        {
            EnsureSceneComponent<MainMenuRuntimeUI>(activeScene, "MainMenuRuntimeUI", ref configuredComponents, out bool createdMainMenuUi);
            EnsureSceneComponent<GlobalUiCoordinator>(activeScene, "GlobalUIRoot", ref configuredComponents, out bool createdGlobalUi);
            changed |= createdMainMenuUi || createdGlobalUi;
            return changed;
        }

        if (activeScene.name != "Game")
        {
            return changed;
        }

        EnsureSceneComponent<GlobalUiCoordinator>(activeScene, "GlobalUIRoot", ref configuredComponents, out bool createdGameGlobalUi);
        EnsureSceneComponent<NavigationService>(activeScene, "NavigationService", ref configuredComponents, out bool createdNavigationService);
        changed |= createdGameGlobalUi || createdNavigationService;

        EnsureSceneComponent<PlayerProgressionManager>(activeScene, "PlayerProgressionManager", ref configuredComponents, out bool createdPlayerProgression);
        EnsureSceneComponent<DriverProgressionSystem>(activeScene, "DriverProgressionSystem", ref configuredComponents, out bool createdDriverProgression);
        EnsureSceneComponent<ProgressionSkillTreeUI>(activeScene, "ProgressionSkillTreeUI", ref configuredComponents, out bool createdSkillTree);
        ProgressionSceneInstaller progressionInstaller = EnsureSceneComponent<ProgressionSceneInstaller>(activeScene, "ProgressionSceneInstaller", ref configuredComponents, out bool createdProgressionInstaller);
        changed |= createdPlayerProgression || createdDriverProgression || createdSkillTree || createdProgressionInstaller;
        if (progressionInstaller != null)
        {
            Undo.RecordObject(progressionInstaller, "Auto Assign Progression Startup References");
            if (progressionInstaller.AutoAssignStartupReferences())
            {
                EditorUtility.SetDirty(progressionInstaller);
                changed = true;
                configuredComponents++;
            }
        }

        EnsureSceneComponent<PlayerVehicleManager>(activeScene, "PlayerVehicleManager", ref configuredComponents, out bool createdVehicleManager);
        EnsureSceneComponent<CompanyPageUI>(activeScene, "CompanyPageUI", ref configuredComponents, out bool createdCompanyPage);
        GameSceneCompanyPageInstaller companyInstaller = EnsureSceneComponent<GameSceneCompanyPageInstaller>(activeScene, "GameSceneCompanyPageInstaller", ref configuredComponents, out bool createdCompanyInstaller);
        changed |= createdVehicleManager || createdCompanyPage || createdCompanyInstaller;
        if (companyInstaller != null)
        {
            Undo.RecordObject(companyInstaller, "Auto Assign Company Startup References");
            if (companyInstaller.AutoAssignStartupReferences())
            {
                EditorUtility.SetDirty(companyInstaller);
                changed = true;
                configuredComponents++;
            }
        }

        BuildingCollisionBootstrap buildingBootstrap = EnsureSceneComponent<BuildingCollisionBootstrap>(activeScene, "BuildingCollisionBootstrap", ref configuredComponents, out bool createdBuildingBootstrap);
        changed |= createdBuildingBootstrap;
        if (buildingBootstrap != null)
        {
            Undo.RecordObject(buildingBootstrap, "Bake Building Colliders To Scene");
            int collidersAdded = buildingBootstrap.PreconfigureSceneForEditor(activeScene, true);
            EditorUtility.SetDirty(buildingBootstrap);
            configuredComponents++;
            changed = true;
            Debug.Log($"[StartupScenePreconfigureTool] Baked {collidersAdded} building collider(s) into scene '{activeScene.name}'. Runtime building collider scan will be skipped for this scene instance.");
        }

        return changed;
    }

    private static T EnsureSceneComponent<T>(Scene scene, string objectName, ref int configuredComponents, out bool created) where T : Component
    {
        T existing = FindComponentInScene<T>(scene);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        GameObject host = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(host, $"Create {objectName}");
        SceneManager.MoveGameObjectToScene(host, scene);

        T component = host.AddComponent<T>();
        EditorUtility.SetDirty(component);
        configuredComponents++;
        created = true;
        return component;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
#endif
