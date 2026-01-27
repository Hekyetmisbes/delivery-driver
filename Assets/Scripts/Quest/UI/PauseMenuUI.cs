using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Quest.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private SettingsMenuUI settingsMenu;
        [SerializeField] private bool pauseTimeScale = true;

        private bool isPaused;

        private void Start()
        {
            SetPaused(false);
        }

        private void Update()
        {
            HandlePauseInput();
        }

        private void HandlePauseInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
#endif
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;

            if (pausePanel != null)
            {
                pausePanel.SetActive(paused);
            }

            if (settingsMenu != null)
            {
                settingsMenu.SetOpen(paused);
            }

            if (pauseTimeScale)
            {
                Time.timeScale = paused ? 0f : 1f;
            }

            if (QuestManager.Instance != null && QuestManager.Instance.CurrentQuest != null)
            {
                if (paused)
                {
                    QuestManager.Instance.CurrentQuest.PauseQuest();
                }
                else
                {
                    QuestManager.Instance.CurrentQuest.ResumeQuest();
                }
            }
        }
    }
}
