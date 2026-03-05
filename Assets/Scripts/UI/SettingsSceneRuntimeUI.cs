using DeliveryDriver.Quest.UI;
using UnityEngine;

public class SettingsSceneRuntimeUI : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        PauseMenuUI pauseMenu = FindFirstObjectByType<PauseMenuUI>();
        if (pauseMenu == null)
        {
            pauseMenu = gameObject.AddComponent<PauseMenuUI>();
        }

        pauseMenu.ConfigureForSettingsScene(mainMenuSceneName);
        pauseMenu.SetPaused(true);
    }
}
