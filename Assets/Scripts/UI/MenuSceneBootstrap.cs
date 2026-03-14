using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuSceneBootstrap
{
    private const string MainMenuSceneName = "MainMenu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MainMenuSceneName)
        {
            EnsureComponent<MainMenuRuntimeUI>("MainMenuRuntimeUI");
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
