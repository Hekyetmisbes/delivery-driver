using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DeliveryDriver.UI;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages the Save/Load UI panel with save, load, and new game functionality
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI lastSaveText;

        [Header("Settings")]
        [SerializeField] private float statusMessageDuration = 2f;
        [SerializeField] private bool autoUpdateLastSaveTime = true;
        [SerializeField] private float updateInterval = 1f;

        private float timeSinceLastUpdate = 0f;

        private void Start()
        {
            // Add button listeners
            if (saveButton != null)
            {
                saveButton.onClick.AddListener(OnSaveButtonClicked);
            }

            if (loadButton != null)
            {
                loadButton.onClick.AddListener(OnLoadButtonClicked);
            }

            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(OnNewGameButtonClicked);
            }

            // Update last save time display
            UpdateLastSaveDisplay();

            // Update button interactability
            UpdateButtonStates();
        }

        private void Update()
        {
            if (autoUpdateLastSaveTime)
            {
                timeSinceLastUpdate += Time.deltaTime;

                if (timeSinceLastUpdate >= updateInterval)
                {
                    UpdateLastSaveDisplay();
                    timeSinceLastUpdate = 0f;
                }
            }
        }

        /// <summary>
        /// Called when the Save Game button is clicked
        /// </summary>
        private void OnSaveButtonClicked()
        {
            if (SaveManager.Instance == null)
            {
                ShowStatus(LocalizationTable.Get("save_manager_missing"), true);
                return;
            }

            SaveManager.Instance.SaveGame();
            ShowStatus(LocalizationTable.Get("save_game_saved"));
            UpdateLastSaveDisplay();
            UpdateButtonStates();

            Debug.Log("[SaveLoadUI] Game saved by user.");
        }

        /// <summary>
        /// Called when the Load Game button is clicked
        /// </summary>
        private void OnLoadButtonClicked()
        {
            if (SaveManager.Instance == null)
            {
                ShowStatus(LocalizationTable.Get("save_manager_missing"), true);
                return;
            }

            if (!SaveManager.Instance.SaveFileExists())
            {
                ShowStatus(LocalizationTable.Get("save_no_file"), true);
                return;
            }

            // Load the game data
            GameSaveData saveData = SaveManager.Instance.LoadGame();

            if (saveData == null)
            {
                ShowStatus(LocalizationTable.Get("save_load_failed"), true);
                return;
            }

            // Reload the current scene to restart with loaded data
            ShowStatus(LocalizationTable.Get("save_loading_game"));
            ReloadScene();
        }

        /// <summary>
        /// Called when the New Game button is clicked
        /// </summary>
        private void OnNewGameButtonClicked()
        {
            if (SaveManager.Instance == null)
            {
                ShowStatus(LocalizationTable.Get("save_manager_missing"), true);
                return;
            }

            // Confirm deletion (you may want to add a confirmation dialog here)
            SaveManager.Instance.DeleteSaveFile();

            // Reset progression managers
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.ResetProgression();
            }

            if (QuestManager.Instance != null)
            {
                // Clear all quests and generate new ones
                QuestManager.Instance.ClearAllZones();
                // You may want to add a ClearAllQuests method to QuestManager
            }

            ShowStatus(LocalizationTable.Get("save_new_game_started"));
            UpdateLastSaveDisplay();
            UpdateButtonStates();

            Debug.Log("[SaveLoadUI] New game started. Save file deleted.");

            // Optionally reload the scene
            ReloadScene();
        }

        /// <summary>
        /// Displays a status message for a short duration
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="isError">Whether this is an error message</param>
        private void ShowStatus(string message, bool isError = false)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message;
            statusText.color = isError ? Color.red : Color.green;

            // Clear the message after a delay
            CancelInvoke(nameof(ClearStatusText));
            Invoke(nameof(ClearStatusText), statusMessageDuration);
        }

        /// <summary>
        /// Clears the status text
        /// </summary>
        private void ClearStatusText()
        {
            if (statusText != null)
            {
                statusText.text = "";
            }
        }

        /// <summary>
        /// Updates the last save time display
        /// </summary>
        private void UpdateLastSaveDisplay()
        {
            if (lastSaveText == null)
            {
                return;
            }

            if (SaveManager.Instance == null)
            {
                lastSaveText.text = LocalizationTable.Get("save_last_saved_none");
                return;
            }

            string lastSaveDate = SaveManager.Instance.GetLastSaveDate();
            lastSaveText.text = string.IsNullOrWhiteSpace(lastSaveDate)
                ? LocalizationTable.Get("save_last_saved_none")
                : LocalizationTable.Format("save_last_saved_format", lastSaveDate);
        }

        /// <summary>
        /// Updates the interactability of buttons based on save file existence
        /// </summary>
        private void UpdateButtonStates()
        {
            bool saveExists = SaveManager.Instance != null && SaveManager.Instance.SaveFileExists();

            // Load button should only be enabled if a save exists
            if (loadButton != null)
            {
                loadButton.interactable = saveExists;
            }

            // Save button is always enabled
            if (saveButton != null)
            {
                saveButton.interactable = true;
            }

            // New Game button is always enabled
            if (newGameButton != null)
            {
                newGameButton.interactable = true;
            }
        }

        /// <summary>
        /// Reloads the current scene to apply loaded data
        /// </summary>
        private void ReloadScene()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentSceneIndex);
        }

        /// <summary>
        /// Gets the save file size as a formatted string
        /// </summary>
        /// <returns>Formatted file size string</returns>
        public string GetSaveFileSizeFormatted()
        {
            if (SaveManager.Instance == null)
            {
                return "0 KB";
            }

            long sizeBytes = SaveManager.Instance.GetSaveFileSize();
            float sizeKB = sizeBytes / 1024f;

            return $"{sizeKB:F1} KB";
        }

        private void OnDestroy()
        {
            // Remove button listeners
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(OnSaveButtonClicked);
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadButtonClicked);
            }

            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(OnNewGameButtonClicked);
            }
        }
    }
}
