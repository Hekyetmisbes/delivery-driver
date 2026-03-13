using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuSceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            EnsureComponent<MainMenuRuntimeUI>("MainMenuRuntimeUI");
            return;
        }

        if (scene.name == "SettingsScene")
        {
            EnsureComponent<SettingsSceneRuntimeUI>("SettingsSceneRuntimeUI");
            return;
        }

        if (scene.name == "CreditsScene")
        {
            EnsureComponent<CreditsSceneRuntimeUI>("CreditsSceneRuntimeUI");
        }
    }

    private static void EnsureComponent<T>(string objectName) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(objectName);
        runtimeObject.AddComponent<T>();
    }
}
