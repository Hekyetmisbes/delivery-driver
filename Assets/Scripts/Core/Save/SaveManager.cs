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
        public bool HasSaveFile => File.Exists(saveFilePath);

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
            // Save on quit
            SaveGame();
        }

        /// <summary>
        /// Saves the current game state to disk
        /// </summary>
        public void SaveGame()
        {
            try
            {
                // Create save data
                GameSaveData saveData = new GameSaveData();

                // Gather player progression data
                if (PlayerProgressionManager.Instance != null)
                {
                    saveData.PlayerData = PlayerProgressionData.FromManager(PlayerProgressionManager.Instance);
                }
                else
                {
                    Debug.Log("[SaveManager] PlayerProgressionManager not found. Saving with default player data.");
                    saveData.PlayerData = new PlayerProgressionData();
                }

                // Gather quest data
                if (QuestManager.Instance != null)
                {
                    saveData.QuestData = QuestManager.Instance.GetSaveData();
                }
                else
                {
                    Debug.Log("[SaveManager] QuestManager not found. Saving with default quest data.");
                    saveData.QuestData = new QuestSaveData();
                }

                // Update timestamp
                saveData.SaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                saveData.SaveVersion = 3;

                // Serialize to JSON
                string json = JsonUtility.ToJson(saveData, true);

                // Write to file
                File.WriteAllText(saveFilePath, json);

                // Store current save data
                currentSaveData = saveData;

                Debug.Log($"[SaveManager] Game saved successfully to {saveFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save game: {e.Message}");
            }
        }

        /// <summary>
        /// Loads game state from disk
        /// </summary>
        /// <returns>Loaded save data, or null if no save exists</returns>
        public GameSaveData LoadGame()
        {
            try
            {
                // Check if save file exists
                if (!File.Exists(saveFilePath))
                {
                    Debug.Log("[SaveManager] No save file found. Starting new game.");
                    return null;
                }

                // Read file
                string json = File.ReadAllText(saveFilePath);

                // Deserialize JSON
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogError("[SaveManager] Failed to deserialize save data.");
                    return null;
                }

                // Store current save data
                currentSaveData = saveData;

                Debug.Log($"[SaveManager] Game loaded successfully from {saveFilePath}");
                Debug.Log($"[SaveManager] Save date: {saveData.SaveDate}, Version: {saveData.SaveVersion}");

                return saveData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to load game: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the save file (for new game)
        /// </summary>
        public void DeleteSaveFile()
        {
            try
            {
                if (File.Exists(saveFilePath))
                {
                    File.Delete(saveFilePath);
                    currentSaveData = null;
                    Debug.Log("[SaveManager] Save file deleted.");
                }
                else
                {
                    Debug.Log("[SaveManager] No save file to delete.");
                }
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
            {
                return currentSaveData.SaveDate;
            }

            if (File.Exists(saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(saveFilePath);
                    GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
                    return saveData?.SaveDate ?? "Unknown";
                }
                catch
                {
                    return "Error reading save";
                }
            }

            return "No save";
        }

        /// <summary>
        /// Checks if a save file exists
        /// </summary>
        /// <returns>True if save file exists</returns>
        public bool SaveFileExists()
        {
            return File.Exists(saveFilePath);
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
