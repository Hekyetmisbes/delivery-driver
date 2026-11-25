using System;
using System.IO;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Manages saving and loading game state to persistent storage
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Save Settings")]
        [SerializeField] private string saveFileName = "savegame.json";
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes

        private string saveFilePath;
        private GameSaveData currentSaveData;
        private float timeSinceLastAutoSave = 0f;

        public string SaveFilePath => saveFilePath;
        public GameSaveData CurrentSaveData => currentSaveData;
        public bool HasSaveFile => SaveFileExists();

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Set save file path
            saveFilePath = Path.Combine(Application.persistentDataPath, saveFileName);

            Debug.Log($"[SaveManager] Initialized. Save file path: {saveFilePath}");
        }

        private void Update()
        {
            // Auto-save system
            if (enableAutoSave)
            {
                timeSinceLastAutoSave += Time.deltaTime;

                if (timeSinceLastAutoSave >= autoSaveInterval)
                {
                    SaveGame();
                    timeSinceLastAutoSave = 0f;
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (Instance == this)
                SaveGame();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Saves the current game state to disk
        /// </summary>
        public void SaveGame()
        {
            TrySaveGame();
        }

        public bool TrySaveGame()
        {
            var progression = PlayerProgressionManager.Instance;
            var quests = QuestManager.Instance;
            if (Instance != this || progression == null || quests == null ||
                !progression.IsSaveReady || !quests.IsSaveReady)
            {
                Debug.LogWarning("[SaveManager] Save skipped: gameplay state is not ready.");
                return false;
            }

            try
            {
                var saveData = new GameSaveData
                {
                    PlayerData = PlayerProgressionData.FromManager(progression),
                    QuestData = quests.GetSaveData(),
                    SaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    SaveVersion = GameSaveData.CurrentVersion
                };

                GameSaveFileStore.Write(saveFilePath, saveData);
                currentSaveData = saveData;
                Debug.Log($"[SaveManager] Game saved successfully to {saveFilePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads game state from disk
        /// </summary>
        /// <returns>Loaded save data, or null if no save exists</returns>
        public GameSaveData LoadGame()
        {
            currentSaveData = null;
            if (GameSaveFileStore.TryRead(saveFilePath, out var data))
            {
                currentSaveData = data;
                return data;
            }

            if (GameSaveFileStore.TryRead(saveFilePath + ".bak", out data))
            {
                currentSaveData = data;
                Debug.LogWarning("[SaveManager] Recovered game state from backup.");
                return data;
            }

            if (SaveFileExists())
                Debug.LogError("[SaveManager] No valid save or backup could be loaded.");
            return null;
        }

        /// <summary>
        /// Deletes the save file (for new game)
        /// </summary>
        public void DeleteSaveFile()
        {
            try
            {
                // Remove recovery files first so an intentional reset cannot restore them.
                File.Delete(saveFilePath + ".tmp");
                File.Delete(saveFilePath + ".bak");
                File.Delete(saveFilePath);
                currentSaveData = null;
                Debug.Log("[SaveManager] Save files deleted.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to delete save file: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the last save date as a formatted string
        /// </summary>
        /// <returns>Formatted save date string, or "No save" if no save exists</returns>
        public string GetLastSaveDate()
        {
            if (currentSaveData != null)
                return currentSaveData.SaveDate;

            if (GameSaveFileStore.TryRead(saveFilePath, out var data) ||
                GameSaveFileStore.TryRead(saveFilePath + ".bak", out data))
                return data.SaveDate;

            return SaveFileExists() ? "Error reading save" : "No save";
        }

        /// <summary>
        /// Checks if a save file exists
        /// </summary>
        /// <returns>True if save file exists</returns>
        public bool SaveFileExists()
        {
            return !string.IsNullOrEmpty(saveFilePath) &&
                   (File.Exists(saveFilePath) || File.Exists(saveFilePath + ".bak"));
        }

        /// <summary>
        /// Gets the save file size in bytes
        /// </summary>
        /// <returns>File size in bytes, or 0 if no save exists</returns>
        public long GetSaveFileSize()
        {
            if (File.Exists(saveFilePath))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(saveFilePath);
                    return fileInfo.Length;
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// Manually triggers an auto-save
        /// </summary>
        public void TriggerAutoSave()
        {
            if (enableAutoSave)
            {
                SaveGame();
                timeSinceLastAutoSave = 0f;
            }
        }
    }
}
