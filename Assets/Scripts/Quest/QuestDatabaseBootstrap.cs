using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Initializes the quest SQLite database at startup by applying schema and optional seed scripts.
    /// </summary>
    public class QuestDatabaseBootstrap : MonoBehaviour
    {
        private const string SqliteConnectionTypeName = "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite";
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
            string schemaPath = Path.Combine(Application.streamingAssetsPath, schemaRelativePath);
            string seedPath = Path.Combine(Application.streamingAssetsPath, seedRelativePath);

            string schemaSql = null;
            string seedSql = null;

            yield return LoadTextFromPath(schemaPath, sql => schemaSql = sql);
            yield return LoadTextFromPath(seedPath, sql => seedSql = sql);

            if (string.IsNullOrWhiteSpace(schemaSql))
            {
                Debug.LogError("[QuestDatabaseBootstrap] Schema SQL could not be loaded. Initialization aborted.");
                Destroy(gameObject);
                yield break;
            }

            string dbPath = Path.Combine(Application.persistentDataPath, databaseFileName);
            bool isFirstRun = !File.Exists(dbPath);

            if (!ExecuteDatabaseSetup(dbPath, schemaSql, seedSql, isFirstRun))
            {
                Debug.LogError("[QuestDatabaseBootstrap] Database initialization failed.");
            }

            Destroy(gameObject);
        }

        private bool ExecuteDatabaseSetup(string dbPath, string schemaSql, string seedSql, bool isFirstRun)
        {
            Type connectionType = Type.GetType(SqliteConnectionTypeName);
            if (connectionType == null)
            {
                Debug.LogError("[QuestDatabaseBootstrap] Mono.Data.Sqlite is not available.");
                return false;
            }

            string connectionString = $"URI=file:{dbPath}";
            object connection = null;

            try
            {
                connection = Activator.CreateInstance(connectionType, connectionString);
                Invoke(connection, "Open");

                ExecuteSql(connection, "PRAGMA foreign_keys = ON;");
                ExecuteSql(connection, schemaSql);

                bool shouldSeed =
                    forceSeedEveryLaunch ||
                    (applySeedOnFirstRun && (isFirstRun || PlayerPrefs.GetInt(SeedStateKey, 0) == 0));

                if (shouldSeed && !string.IsNullOrWhiteSpace(seedSql))
                {
                    ExecuteSql(connection, seedSql);
                    PlayerPrefs.SetInt(SeedStateKey, 1);
                    PlayerPrefs.Save();
                }

                Debug.Log($"[QuestDatabaseBootstrap] Database ready at: {dbPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestDatabaseBootstrap] Initialization exception: {FormatExceptionChain(ex)}");
                return false;
            }
            finally
            {
                if (connection != null)
                {
                    try
                    {
                        Invoke(connection, "Close");
                    }
                    catch
                    {
                        // Ignore close failures.
                    }
                }
            }
        }

        private static void ExecuteSql(object connection, string sql)
        {
            object command = Invoke(connection, "CreateCommand");
            SetProperty(command, "CommandText", sql);
            Invoke(command, "ExecuteNonQuery");
            Invoke(command, "Dispose");
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            return method.Invoke(target, args);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            }

            property.SetValue(target, value);
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

        private static Exception UnwrapInvocationException(Exception ex)
        {
            Exception current = ex;
            while (current is TargetInvocationException tie && tie.InnerException != null)
            {
                current = tie.InnerException;
            }

            return current;
        }

        private static string FormatExceptionChain(Exception ex)
        {
            Exception current = UnwrapInvocationException(ex);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int depth = 0;
            while (current != null)
            {
                if (depth > 0) sb.Append("\n-- Inner -> ");
                sb.Append(current.GetType().Name);
                sb.Append(": ");
                sb.Append(current.Message);
                current = current.InnerException;
                depth++;
            }

            return sb.ToString();
        }
    }
}
