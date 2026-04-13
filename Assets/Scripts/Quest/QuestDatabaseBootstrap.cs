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
            Type connectionType = SqliteProviderResolver.ResolveConnectionType();
            if (connectionType == null)
            {
                Debug.LogError("[QuestDatabaseBootstrap] Mono.Data.Sqlite is not available.");
                return false;
            }

            string connectionString = $"Data Source={dbPath}";
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

                if (!HasRequiredTables(connection))
                {
                    Debug.LogError("[QuestDatabaseBootstrap] Database schema verification failed. Required tables are missing after setup.");
                    return false;
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

        private static bool HasRequiredTables(object connection)
        {
            return HasTable(connection, "players") && HasTable(connection, "company_profiles");
        }

        private static bool HasTable(object connection, string tableName)
        {
            object command = null;
            try
            {
                command = Invoke(connection, "CreateCommand");
                SetProperty(command, "CommandText", "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@name;");

                object parameter = Invoke(command, "CreateParameter");
                SetProperty(parameter, "ParameterName", "@name");
                SetProperty(parameter, "Value", tableName);

                object parameters = GetProperty(command, "Parameters");
                if (parameters is System.Collections.IList list)
                {
                    list.Add(parameter);
                }
                else
                {
                    Invoke(parameters, "Add", parameter);
                }

                object result = Invoke(command, "ExecuteScalar");
                return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (command != null)
                {
                    try
                    {
                        Invoke(command, "Dispose");
                    }
                    catch
                    {
                        // Ignore dispose failures.
                    }
                }
            }
        }

        private static void ExecuteSql(object connection, string sql)
        {
            object command = null;
            try
            {
                command = Invoke(connection, "CreateCommand");
                SetProperty(command, "CommandText", sql);
                Invoke(command, "ExecuteNonQuery");
            }
            finally
            {
                if (command != null)
                {
                    try
                    {
                        Invoke(command, "Dispose");
                    }
                    catch
                    {
                        // Ignore dispose failures.
                    }
                }
            }
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            Type type = target.GetType();
            object[] resolvedArgs = args ?? Array.Empty<object>();
            MethodInfo best = null;
            int bestScore = -1;

            foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != resolvedArgs.Length)
                {
                    continue;
                }

                int score = 0;
                bool compatible = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType.IsByRef)
                    {
                        parameterType = parameterType.GetElementType();
                    }

                    object arg = resolvedArgs[i];
                    if (arg == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            compatible = false;
                            break;
                        }

                        score += 1;
                        continue;
                    }

                    Type argumentType = arg.GetType();
                    if (parameterType == argumentType)
                    {
                        score += 4;
                    }
                    else if (parameterType.IsAssignableFrom(argumentType))
                    {
                        score += 3;
                    }
                    else if (parameterType == typeof(object))
                    {
                        score += 2;
                    }
                    else
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible || score <= bestScore)
                {
                    continue;
                }

                best = candidate;
                bestScore = score;
            }

            if (best == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return best.Invoke(target, resolvedArgs);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            if (property == null || !property.CanWrite)
            {
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            }

            property.SetValue(target, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            if (property == null || !property.CanRead)
            {
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            }

            return property.GetValue(target);
        }

        private static PropertyInfo FindProperty(Type type, string propertyName)
        {
            Type current = type;
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }

                current = current.BaseType;
            }

            return null;
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
