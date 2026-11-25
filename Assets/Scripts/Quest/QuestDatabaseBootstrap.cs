using System;
using System.Collections;
using System.IO;
using Mono.Data.Sqlite;
using UnityEngine;
using UnityEngine.Networking;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Initializes the quest SQLite database at startup by applying schema and optional seed scripts.
    /// </summary>
    public class QuestDatabaseBootstrap : MonoBehaviour
    {
        private const string SeedStateKey = "quest_db_seed_applied_v1";

        [Header("Database")]
        [SerializeField] private string databaseFileName = "quest.db";

        [Header("Scripts")]
        [SerializeField] private string schemaRelativePath = "Database/schema.sql";
        [SerializeField] private string seedRelativePath = "Database/seed.sql";

        [Header("Seed Behavior")]
        [SerializeField] private bool applySeedOnFirstRun = true;
        [SerializeField] private bool forceSeedEveryLaunch = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBootstrap()
        {
            GameObject bootstrapObject = new GameObject("QuestDatabaseBootstrap");
            DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<QuestDatabaseBootstrap>();
        }

        private void Awake()
        {
            StartCoroutine(InitializeDatabaseRoutine());
        }

        private IEnumerator InitializeDatabaseRoutine()
        {
            QuestDatabaseService.ResetDatabaseInitialization();

            string schemaPath = Path.Combine(Application.streamingAssetsPath, schemaRelativePath);
            string seedPath = Path.Combine(Application.streamingAssetsPath, seedRelativePath);
            string dbPath = Path.Combine(Application.persistentDataPath, databaseFileName);

            Debug.Log($"[QuestDatabaseBootstrap] Starting initialization. Schema='{schemaPath}', Seed='{seedPath}', DB='{dbPath}'");

            string schemaSql = null;
            string seedSql = null;

            yield return LoadTextFromPath(schemaPath, sql => schemaSql = sql);
            yield return LoadTextFromPath(seedPath, sql => seedSql = sql);

            if (string.IsNullOrWhiteSpace(schemaSql))
            {
                Debug.LogError("[QuestDatabaseBootstrap] Schema SQL could not be loaded. Initialization aborted.");
                QuestDatabaseService.ReportDatabaseInitialization(false);
                Destroy(gameObject);
                yield break;
            }

            bool isFirstRun = !File.Exists(dbPath);

            bool setupSucceeded = ExecuteDatabaseSetup(dbPath, schemaSql, seedSql, isFirstRun);
            QuestDatabaseService.ReportDatabaseInitialization(setupSucceeded);

            if (!setupSucceeded)
            {
                Debug.LogError("[QuestDatabaseBootstrap] Database initialization failed.");
            }

            Destroy(gameObject);
        }

        private bool ExecuteDatabaseSetup(string dbPath, string schemaSql, string seedSql, bool isFirstRun)
        {
            try
            {
                // Preserve the existing native provider probe before opening SQLite.
                SqliteProviderResolver.ResolveConnectionType();
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    ExecuteSql(connection, "PRAGMA foreign_keys = ON;");
                    ExecuteSql(connection, schemaSql);

                    bool shouldSeed = forceSeedEveryLaunch ||
                        (applySeedOnFirstRun && (isFirstRun || PlayerPrefs.GetInt(SeedStateKey, 0) == 0));
                    if (shouldSeed && !string.IsNullOrWhiteSpace(seedSql))
                    {
                        ExecuteSql(connection, seedSql);
                        PlayerPrefs.SetInt(SeedStateKey, 1);
                        PlayerPrefs.Save();
                    }

                    if (!HasTable(connection, "players") || !HasTable(connection, "company_profiles"))
                    {
                        Debug.LogError("[QuestDatabaseBootstrap] Database schema verification failed. Required tables are missing after setup.");
                        return false;
                    }
                }

                Debug.Log($"[QuestDatabaseBootstrap] Database ready at: {dbPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestDatabaseBootstrap] Initialization exception: {ex} | {SqliteProviderResolver.GetNativeDependencyDiagnostics()}");
                return false;
            }
        }

        private static bool HasTable(SqliteConnection connection, string tableName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@name;";
                command.Parameters.AddWithValue("@name", tableName);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void ExecuteSql(SqliteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static IEnumerator LoadTextFromPath(string path, Action<string> onLoaded)
        {
            onLoaded?.Invoke(null);

            if (path.Contains("://"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        onLoaded?.Invoke(request.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogWarning($"[QuestDatabaseBootstrap] Failed to load SQL from '{path}': {request.error}");
                    }
                }
                yield break;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[QuestDatabaseBootstrap] SQL file not found: {path}");
                yield break;
            }

            onLoaded?.Invoke(File.ReadAllText(path));
            yield return null;
        }

    }
}
