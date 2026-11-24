using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using DeliveryDriver.Company;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class QuestDatabaseService : MonoBehaviour
    {
        public const string DefaultPlayerId = "local-player";
        public const string DefaultPlayerDisplayName = "Local Player";
        public const string DefaultCompanyId = "company-local-player";
        public const string DefaultCompanyName = "Player Company";
        private static QuestDatabaseService instance;
        private static bool databaseInitializationCompleted;
        private static bool databaseInitializationSucceeded;
        private readonly object gate = new object();
        private SqliteConnection questWriteConnection;
        private SqliteTransaction questWriteTransaction;

        [SerializeField] private string databaseFileName = "quest.db";
        private Type connType;
        private string dbPath;
        private bool providerHealthy;
        private VehicleType currentSelectedVehicleType = VehicleType.Van;

        public static QuestDatabaseService Instance => instance;
        public bool IsReady => connType != null && providerHealthy && databaseInitializationCompleted && databaseInitializationSucceeded && File.Exists(dbPath);
        public string DatabasePath => dbPath;
        public VehicleType CurrentSelectedVehicleType => currentSelectedVehicleType;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            databaseInitializationCompleted = false;
            databaseInitializationSucceeded = false;
            GameObject go = new GameObject("QuestDatabaseService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<QuestDatabaseService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            dbPath = Path.Combine(Application.persistentDataPath, databaseFileName);
            Debug.Log($"[QuestDatabaseService] Initializing. Persistent DB path: {dbPath}");
            connType = SqliteProviderResolver.ResolveConnectionType();
            if (connType == null)
            {
                Debug.LogError("[QuestDatabaseService] Mono.Data.Sqlite not available. The managed SQLite provider may be missing from the build or stripped by the linker.");
                providerHealthy = false;
                return;
            }

            providerHealthy = ValidateProvider();
            if (!providerHealthy)
            {
                Debug.LogError($"[QuestDatabaseService] SQLite provider initialization failed. Database service disabled. {SqliteProviderResolver.GetNativeDependencyDiagnostics()}");
                return;
            }

            string nativeVersion = SqliteProviderResolver.TryGetNativeVersion();
            Debug.Log(string.IsNullOrWhiteSpace(nativeVersion)
                ? "[QuestDatabaseService] SQLite provider validated successfully."
                : $"[QuestDatabaseService] SQLite provider validated successfully. Native sqlite3 version: {nativeVersion}");
        }

        public static void ReportDatabaseInitialization(bool success)
        {
            databaseInitializationCompleted = true;
            databaseInitializationSucceeded = success;

            Debug.Log(success
                ? "[QuestDatabaseService] Database bootstrap completed successfully."
                : "[QuestDatabaseService] Database bootstrap failed. Service will remain unavailable.");
        }

        public static void ResetDatabaseInitialization()
        {
            databaseInitializationCompleted = false;
            databaseInitializationSucceeded = false;
        }

        public bool EnsurePlayer(string playerId, string displayName = "Player")
        {
            if (string.IsNullOrWhiteSpace(playerId)) return false;
            const string sql = @"INSERT OR IGNORE INTO players
            (player_id,display_name,created_at,last_login_at,level,xp,xp_to_next_level,money_balance,reputation_score)
            VALUES (@id,@name,datetime('now'),datetime('now'),1,0,100,0,0);";
            return ExecuteNonQuery(sql, new Dictionary<string, object> { ["@id"] = playerId, ["@name"] = displayName });
        }

        public bool EnsureDefaultPlayer()
        {
            return EnsurePlayer(DefaultPlayerId, DefaultPlayerDisplayName);
        }

        public bool PlayerExists(string playerId)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            object result = ExecuteScalar(
                "SELECT COUNT(1) FROM players WHERE player_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = playerId });
            return CvI(result, 0) > 0;
        }

        public bool TryGetPlayerBalance(string playerId, out int balance)
        {
            balance = 0;
            if (!IsReady || string.IsNullOrWhiteSpace(playerId))
                return false;

            object result = ExecuteScalar(
                "SELECT money_balance FROM players WHERE player_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = playerId });
            // Zero is a valid balance; a missing row or failed query is not.
            if (result == null || result == DBNull.Value)
                return false;

            balance = CvI(result, -1);
            return balance >= 0;
        }

        public int GetPlayerBalance(string playerId, int fallback = 0) =>
            TryGetPlayerBalance(playerId, out int balance) ? balance : fallback;

        public int GetDefaultPlayerBalance(int fallback = 0)
        {
            return GetPlayerBalance(DefaultPlayerId, fallback);
        }

        public bool SetPlayerBalance(string playerId, int balance)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            if (!EnsurePlayer(playerId))
            {
                return false;
            }

            const string sql = @"UPDATE players
            SET money_balance=@balance,
                last_login_at=datetime('now')
            WHERE player_id=@id;";
            return ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@id"] = playerId,
                ["@balance"] = Mathf.Max(0, balance)
            });
        }

        public bool SetDefaultPlayerBalance(int balance)
        {
            return SetPlayerBalance(DefaultPlayerId, balance);
        }

        public bool TryGetDefaultCompanyName(out string companyName)
        {
            companyName = string.Empty;
            if (!IsReady)
            {
                return false;
            }

            object result = ExecuteScalar(
                "SELECT company_name FROM company_profiles WHERE player_id=@playerId LIMIT 1;",
                new Dictionary<string, object> { ["@playerId"] = DefaultPlayerId });

            companyName = Convert.ToString(result) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(companyName);
        }

        public bool ConfigureDefaultStartupProfile(string companyName)
        {
            if (!IsReady)
            {
                Debug.LogError("[QuestDatabaseService] Cannot configure startup profile because the database is not ready.");
                return false;
            }

            string resolvedCompanyName = NormalizeCompanyName(companyName);
            if (string.IsNullOrWhiteSpace(resolvedCompanyName))
            {
                Debug.LogError("[QuestDatabaseService] Cannot configure startup profile without a company name.");
                return false;
            }

            if (!EnsurePlayer(DefaultPlayerId, DefaultPlayerDisplayName))
            {
                Debug.LogError("[QuestDatabaseService] Failed to ensure the default player before configuring startup profile.");
                return false;
            }

            if (!SetDefaultPlayerBalance(0))
            {
                Debug.LogError("[QuestDatabaseService] Failed to reset default player balance for startup profile.");
                return false;
            }

            if (!EnsureDefaultCompanyProfile(DefaultPlayerId, resolvedCompanyName))
            {
                Debug.LogError("[QuestDatabaseService] Failed to ensure company profile before applying startup company name.");
                return false;
            }

            const string sql = @"UPDATE company_profiles
            SET company_name=@companyName,
                updated_at=datetime('now')
            WHERE player_id=@playerId;";

            bool saved = ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@playerId"] = DefaultPlayerId,
                ["@companyName"] = resolvedCompanyName
            });

            if (!saved)
            {
                Debug.LogError("[QuestDatabaseService] Failed to save startup company name.");
                return false;
            }

            Debug.Log($"[QuestDatabaseService] Startup profile configured. Company='{resolvedCompanyName}', Balance=0.");
            return true;
        }

        public bool EnsureDefaultCompanyProfile()
        {
            return EnsureDefaultCompanyProfile(DefaultPlayerId, DefaultCompanyName);
        }

        public bool EnsureDefaultCompanyProfile(string playerId, string companyName)
        {
            if (!IsReady)
            {
                Debug.LogError("[QuestDatabaseService] Cannot ensure company profile because the database is not ready.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError("[QuestDatabaseService] Cannot ensure company profile without a valid player id.");
                return false;
            }

            string resolvedCompanyName = string.IsNullOrWhiteSpace(companyName) ? DefaultCompanyName : companyName.Trim();
            string displayName = string.Equals(playerId, DefaultPlayerId, StringComparison.Ordinal)
                ? DefaultPlayerDisplayName
                : "Player";
            if (!EnsurePlayer(playerId, displayName))
            {
                Debug.LogError($"[QuestDatabaseService] Failed to ensure player '{playerId}' before creating company profile.");
                return false;
            }

            const string sql = @"INSERT OR IGNORE INTO company_profiles
            (company_id,player_id,company_name,selected_vehicle_type,created_at,updated_at)
            VALUES (@companyId,@playerId,@companyName,@vehicleType,datetime('now'),datetime('now'));";

            bool inserted = ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@companyId"] = GetCompanyIdForPlayer(playerId),
                ["@playerId"] = playerId,
                ["@companyName"] = resolvedCompanyName,
                ["@vehicleType"] = VehicleTypeExtensions.ToDatabaseValue(VehicleType.Van)
            });

            if (!inserted)
            {
                Debug.LogError($"[QuestDatabaseService] Failed to ensure company profile for player '{playerId}'.");
                return false;
            }

            Debug.Log($"[QuestDatabaseService] Company profile ensured for player '{playerId}'.");
            return true;
        }

        public CompanyProfileData GetCompanyProfile(string playerId)
        {
            if (!IsReady)
            {
                Debug.LogError("[QuestDatabaseService] Cannot load company profile because the database is not ready.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError("[QuestDatabaseService] Cannot load company profile without a valid player id.");
                return null;
            }

            if (!EnsureDefaultCompanyProfile(playerId, DefaultCompanyName))
            {
                Debug.LogError($"[QuestDatabaseService] Company profile could not be ensured for player '{playerId}'.");
                return null;
            }

            const string sql = @"SELECT cp.company_id,cp.player_id,cp.company_name,cp.selected_vehicle_type,
            p.display_name,p.money_balance
            FROM company_profiles cp
            INNER JOIN players p ON p.player_id=cp.player_id
            WHERE cp.player_id=@playerId
            LIMIT 1;";

            List<Dictionary<string, object>> rows = ExecuteQuery(sql, new Dictionary<string, object>
            {
                ["@playerId"] = playerId
            });

            if (rows.Count == 0)
            {
                Debug.LogError($"[QuestDatabaseService] Company profile record was not found for player '{playerId}'.");
                return null;
            }

            Dictionary<string, object> row = rows[0];
            string vehicleValue = S(row, "selected_vehicle_type");
            if (!VehicleTypeExtensions.TryParseDatabaseValue(vehicleValue, out VehicleType vehicleType))
            {
                Debug.LogError($"[QuestDatabaseService] Invalid vehicle type '{vehicleValue}' for player '{playerId}'.");
                return null;
            }

            currentSelectedVehicleType = vehicleType;

            CompanyProfileData profile = new CompanyProfileData
            {
                CompanyId = S(row, "company_id"),
                PlayerId = S(row, "player_id"),
                CompanyName = S(row, "company_name"),
                PlayerDisplayName = S(row, "display_name"),
                Balance = I(row, "money_balance", 0),
                SelectedVehicleType = vehicleType
            };

            Debug.Log($"[QuestDatabaseService] Company profile found for player '{playerId}' with vehicle '{vehicleValue}'.");
            return profile;
        }

        public bool SaveSelectedVehicleType(string playerId, VehicleType vehicleType)
        {
            if (!IsReady)
            {
                Debug.LogError("[QuestDatabaseService] Cannot save vehicle type because the database is not ready.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError("[QuestDatabaseService] Cannot save vehicle type without a valid player id.");
                return false;
            }

            if (!EnsureDefaultCompanyProfile(playerId, DefaultCompanyName))
            {
                Debug.LogError($"[QuestDatabaseService] Company profile could not be ensured before saving vehicle type for '{playerId}'.");
                return false;
            }

            const string sql = @"UPDATE company_profiles
            SET selected_vehicle_type=@vehicleType,
                updated_at=datetime('now')
            WHERE player_id=@playerId;";

            bool saved = ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@playerId"] = playerId,
                ["@vehicleType"] = VehicleTypeExtensions.ToDatabaseValue(vehicleType)
            });

            if (!saved)
            {
                Debug.LogError($"[QuestDatabaseService] Failed to save vehicle type '{vehicleType}' for player '{playerId}'.");
                return false;
            }

            currentSelectedVehicleType = vehicleType;
            Debug.Log($"[QuestDatabaseService] Selected vehicle type saved for player '{playerId}': {VehicleTypeExtensions.ToDatabaseValue(vehicleType)}");
            return true;
        }

        public bool SaveQuestInstance(string playerId, QuestData quest)
        {
            if (!IsReady || quest == null || string.IsNullOrWhiteSpace(quest.QuestID))
                return false;

            lock (gate)
            {
                try
                {
                    using (var connection = Open())
                    using (var transaction = connection.BeginTransaction())
                    {
                        questWriteConnection = connection;
                        questWriteTransaction = transaction;
                        if (!SaveQuestInstanceCore(playerId, quest))
                            return false; // Disposing an uncommitted transaction rolls it back.

                        transaction.Commit();
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[QuestDatabaseService] Quest snapshot failed: {e.Message}");
                    return false;
                }
                finally
                {
                    questWriteTransaction = null;
                    questWriteConnection = null;
                }
            }
        }

        private bool SaveQuestInstanceCore(string playerId, QuestData quest)
        {
            if (!EnsurePlayer(playerId)) return false;
            int cargoId = EnsureCargo(quest.Cargo);
            string templateId = EnsureTemplate(quest, cargoId);
            int? pickupLoc = EnsureLocation(quest.PickupLocation, "pickup");
            int penalty = quest.CollisionCount * 10 + quest.NpcCollisionCount * 100;
            int earnedBonus = quest.WillEarnBonus() ? 1 : 0;
            int bonusFinal = earnedBonus == 1 ? quest.BonusReward : 0;
            int total = Mathf.Max(0, quest.BaseReward + bonusFinal - penalty);
            string status = quest.Status.ToString();
            string completedAt = status == "Completed" ? UtcNow() : null;
            string failedAt = status == "Failed" ? UtcNow() : null;
            string failureReason = status == "Failed" ? "Runtime quest failed" : null;

            const string upsert = @"
            INSERT INTO quest_instances (
              quest_instance_id,player_id,template_id,quest_status,assigned_at,started_at,completed_at,failed_at,
              time_limit_sec,time_remaining_sec,current_delivery_index,has_picked_up_cargo,
              pickup_location_id,pickup_neighborhood_id,selected_cargo_type_id,cargo_health,
              collision_count,npc_collision_count,total_distance_m,earned_bonus,performance_rating,
              base_reward_final,bonus_reward_final,penalty_amount,total_reward_final,failure_reason,
              is_daily_challenge,daily_challenge_date,streak_multiplier_applied
            ) VALUES (
              @qid,@pid,@tid,@status,datetime('now'),@started,@completed,@failed,
              @limit,@remaining,@idx,@picked,
              @pickup,@pickupN,@cargo,@health,
              @coll,@npc,@dist,@earned,@rating,
              @base,@bonus,@penalty,@total,@reason,
              0,NULL,1.0
            )
            ON CONFLICT(quest_instance_id) DO UPDATE SET
              quest_status=excluded.quest_status,
              completed_at=CASE WHEN excluded.quest_status='Completed'
                  THEN COALESCE(quest_instances.completed_at,excluded.completed_at) ELSE NULL END,
              failed_at=CASE WHEN excluded.quest_status='Failed'
                  THEN COALESCE(quest_instances.failed_at,excluded.failed_at) ELSE NULL END,
              time_limit_sec=excluded.time_limit_sec,
              time_remaining_sec=excluded.time_remaining_sec,
              current_delivery_index=excluded.current_delivery_index,
              has_picked_up_cargo=excluded.has_picked_up_cargo,
              pickup_location_id=excluded.pickup_location_id,
              pickup_neighborhood_id=excluded.pickup_neighborhood_id,
              selected_cargo_type_id=excluded.selected_cargo_type_id,
              cargo_health=excluded.cargo_health,
              collision_count=excluded.collision_count,
              npc_collision_count=excluded.npc_collision_count,
              total_distance_m=excluded.total_distance_m,
              earned_bonus=excluded.earned_bonus,
              performance_rating=excluded.performance_rating,
              base_reward_final=excluded.base_reward_final,
              bonus_reward_final=excluded.bonus_reward_final,
              penalty_amount=excluded.penalty_amount,
              total_reward_final=excluded.total_reward_final,
              failure_reason=excluded.failure_reason;";

            bool ok = ExecuteNonQuery(upsert, new Dictionary<string, object>
            {
                ["@qid"] = quest.QuestID,
                ["@pid"] = playerId,
                ["@tid"] = templateId,
                ["@status"] = status,
                ["@started"] = UtcNow(),
                ["@completed"] = Db(completedAt),
                ["@failed"] = Db(failedAt),
                ["@limit"] = Mathf.Max(1, Mathf.RoundToInt(quest.TimeLimit)),
                ["@remaining"] = Mathf.Clamp(Mathf.RoundToInt(quest.TimeRemaining), 0, Mathf.Max(1, Mathf.RoundToInt(quest.TimeLimit))),
                ["@idx"] = Mathf.Max(0, quest.CurrentDeliveryIndex),
                ["@picked"] = quest.HasPickedUpCargo ? 1 : 0,
                ["@pickup"] = Db(pickupLoc),
                ["@pickupN"] = Db(GetNeighborhoodForLocation(pickupLoc)),
                ["@cargo"] = cargoId,
                ["@health"] = quest.Cargo != null ? Mathf.Clamp(quest.Cargo.CargoHealth, 0f, 100f) : 100f,
                ["@coll"] = Mathf.Max(0, quest.CollisionCount),
                ["@npc"] = Mathf.Max(0, quest.NpcCollisionCount),
                ["@dist"] = Mathf.Max(0f, quest.TotalDistanceTraveled),
                ["@earned"] = earnedBonus,
                ["@rating"] = quest.GetRatingDisplay(),
                ["@base"] = quest.BaseReward,
                ["@bonus"] = bonusFinal,
                ["@penalty"] = penalty,
                ["@total"] = total,
                ["@reason"] = Db(failureReason)
            });
            if (!ok) return false;
            return SaveStops(quest);
        }

        public bool InsertQuestEvent(string questId, string playerId, string type, string valueText = null, Vector3? pos = null, string metadataJson = null)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(type)) return false;
            Vector3 p = pos ?? Vector3.zero;
            bool has = pos.HasValue;
            const string sql = @"INSERT INTO quest_events
            (quest_instance_id,player_id,event_type,event_time,event_value_text,position_x,position_y,position_z,metadata_json)
            VALUES (@qid,@pid,@type,datetime('now'),@text,@x,@y,@z,@meta);";
            return ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@qid"] = questId, ["@pid"] = playerId, ["@type"] = type,
                ["@text"] = Db(valueText), ["@x"] = has ? p.x : (object)DBNull.Value,
                ["@y"] = has ? p.y : (object)DBNull.Value, ["@z"] = has ? p.z : (object)DBNull.Value,
                ["@meta"] = Db(metadataJson)
            });
        }

        public bool InsertWalletTransaction(string playerId, string questId, string txType, int amount, int balanceAfter, string description = null)
        {
            const string sql = @"INSERT INTO wallet_transactions
            (player_id,quest_instance_id,tx_type,amount,balance_after,created_at,description)
            SELECT @pid,@qid,@type,@amount,@bal,datetime('now'),@desc
            WHERE @qid IS NULL OR @type NOT IN ('QUEST_REWARD','PENALTY')
               OR NOT EXISTS (
                   SELECT 1 FROM wallet_transactions
                   WHERE player_id=@pid AND quest_instance_id=@qid AND tx_type=@type
               );";
            return ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@pid"] = playerId, ["@qid"] = Db(questId), ["@type"] = txType,
                ["@amount"] = amount, ["@bal"] = balanceAfter, ["@desc"] = Db(description)
            });
        }

        public List<QuestData> GetActiveQuests(string playerId)
        {
            const string sql = @"SELECT qi.quest_instance_id,qi.quest_status,qi.time_limit_sec,qi.time_remaining_sec,qi.current_delivery_index,
            qi.has_picked_up_cargo,qi.collision_count,qi.npc_collision_count,qi.total_distance_m,qi.selected_cargo_type_id,qi.pickup_location_id,
            qt.quest_name,qt.quest_description,qt.quest_type,qt.difficulty,qt.required_level,qt.is_repeatable,qt.base_reward,qt.bonus_reward,qt.bonus_time_threshold,qt.xp_reward
            FROM quest_instances qi JOIN quest_templates qt ON qt.template_id=qi.template_id
            WHERE qi.player_id=@pid AND qi.quest_status='Active' ORDER BY qi.assigned_at DESC;";

            List<QuestData> result = new List<QuestData>();
            foreach (Dictionary<string, object> r in ExecuteQuery(sql, new Dictionary<string, object> { ["@pid"] = playerId }))
            {
                QuestData q = new QuestData
                {
                    QuestID = S(r, "quest_instance_id"),
                    QuestName = S(r, "quest_name"),
                    QuestDescription = S(r, "quest_description"),
                    QuestType = P(S(r, "quest_type"), QuestType.StandardDelivery),
                    Difficulty = P(S(r, "difficulty"), QuestDifficulty.Easy),
                    Status = P(S(r, "quest_status"), QuestStatus.NotStarted),
                    TimeLimit = I(r, "time_limit_sec", 300),
                    TimeRemaining = I(r, "time_remaining_sec", 300),
                    CurrentDeliveryIndex = I(r, "current_delivery_index", 0),
                    HasPickedUpCargo = I(r, "has_picked_up_cargo", 0) == 1,
                    CollisionCount = I(r, "collision_count", 0),
                    NpcCollisionCount = I(r, "npc_collision_count", 0),
                    TotalDistanceTraveled = F(r, "total_distance_m", 0f),
                    RequiredLevel = I(r, "required_level", 1),
                    IsRepeatable = I(r, "is_repeatable", 1) == 1,
                    BaseReward = I(r, "base_reward", 0),
                    BonusReward = I(r, "bonus_reward", 0),
                    BonusTimeThreshold = Mathf.Clamp01(F(r, "bonus_time_threshold", 0.5f)),
                    XPReward = I(r, "xp_reward", 0)
                };
                q.Cargo = GetCargo(I(r, "selected_cargo_type_id", -1)) ?? new CargoData();
                int pickupId = I(r, "pickup_location_id", -1);
                q.PickupLocation = pickupId > 0 ? GetLocation(pickupId) : null;
                q.DeliveryLocations = GetStops(q.QuestID);
                result.Add(q);
            }
            return result;
        }

        public bool ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            return ExecuteCommand(sql, parameters, command =>
            {
                command.ExecuteNonQuery();
                return true;
            }, false);
        }

        public object ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            return ExecuteCommand<object>(sql, parameters, command => command.ExecuteScalar(), null);
        }

        public List<Dictionary<string, object>> ExecuteQuery(string sql, Dictionary<string, object> parameters = null)
        {
            return ExecuteCommand(sql, parameters, command =>
            {
                var rows = new List<Dictionary<string, object>>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                        rows.Add(row);
                    }
                }
                return rows;
            }, new List<Dictionary<string, object>>());
        }

        private T ExecuteCommand<T>(string sql, Dictionary<string, object> parameters,
            Func<SqliteCommand, T> execute, T fallback)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(sql))
                return fallback;

            lock (gate)
            {
                // All quest snapshot commands share one connection and transaction.
                // Exceptions must propagate so a partially written snapshot is rolled back.
                if (questWriteConnection != null)
                {
                    using (var command = Build(questWriteConnection, sql, parameters, questWriteTransaction))
                        return execute(command);
                }

                try
                {
                    using (var connection = Open())
                    using (var command = Build(connection, sql, parameters, null))
                        return execute(command);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[QuestDatabaseService] Command failed: {e.Message}");
                    return fallback;
                }
            }
        }

        private bool SaveStops(QuestData quest)
        {
            var stops = quest.DeliveryLocations;
            int count = stops?.Count ?? 0;
            const string sql = @"INSERT INTO quest_instance_stops
            (quest_instance_id,stop_order,location_id,neighborhood_id,status,eta_sec,reached_at,completed_at)
            VALUES (@qid,@ord,@loc,@nid,@status,NULL,NULL,
                    CASE WHEN @status='Completed' THEN datetime('now') ELSE NULL END)
            ON CONFLICT(quest_instance_id,stop_order) DO UPDATE SET
                location_id=excluded.location_id,
                neighborhood_id=excluded.neighborhood_id,
                status=excluded.status,
                completed_at=CASE WHEN excluded.status='Completed'
                    THEN COALESCE(quest_instance_stops.completed_at,excluded.completed_at)
                    ELSE NULL END;";

            for (int i = 0; i < count; i++)
            {
                int? location = EnsureLocation(stops[i], "delivery");
                if (!location.HasValue || location.Value <= 0)
                    return false;

                bool completed = quest.Status == QuestStatus.Completed ||
                                 (quest.HasPickedUpCargo && i < quest.CurrentDeliveryIndex);
                if (!ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    ["@qid"] = quest.QuestID, ["@ord"] = i + 1, ["@loc"] = location.Value,
                    ["@nid"] = Db(GetNeighborhoodForLocation(location)),
                    ["@status"] = completed ? "Completed" : "Pending"
                }))
                    return false;
            }

            // Remove only obsolete trailing stops, rather than recreating the entire route.
            return ExecuteNonQuery(
                "DELETE FROM quest_instance_stops WHERE quest_instance_id=@qid AND stop_order>@count;",
                new Dictionary<string, object> { ["@qid"] = quest.QuestID, ["@count"] = count });
        }

        private int EnsureCargo(CargoData c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.CargoName))
                c = new CargoData("Standard Cargo", 100f, false, "Auto");
            const string ins = @"INSERT OR IGNORE INTO cargo_types (cargo_name,weight_kg,is_fragile,base_health,description,icon_key)
            VALUES (@name,@w,@frag,@h,@d,NULL);";
            ExecuteNonQuery(ins, new Dictionary<string, object> { ["@name"] = c.CargoName, ["@w"] = Mathf.Clamp(c.Weight, 0f, 500f), ["@frag"] = c.IsFragile ? 1 : 0, ["@h"] = Mathf.Clamp(c.CargoHealth, 0f, 100f), ["@d"] = c.Description ?? "" });
            return CvI(ExecuteScalar("SELECT cargo_type_id FROM cargo_types WHERE cargo_name=@n LIMIT 1;", new Dictionary<string, object> { ["@n"] = c.CargoName }), 1);
        }

        private string EnsureTemplate(QuestData q, int cargoId)
        {
            string id = $"rt_{q.QuestType}_{q.Difficulty}_{(q.QuestName ?? "runtime").Trim().ToLowerInvariant().Replace(" ", "_")}";
            const string ins = @"INSERT OR IGNORE INTO quest_templates
            (template_id,quest_name,quest_description,quest_type,difficulty,required_level,is_repeatable,delivery_stop_count,base_time_limit_sec,base_reward,bonus_reward,bonus_time_threshold,xp_reward,default_cargo_type_id,is_active,weight)
            VALUES (@id,@n,@d,@t,@diff,@lvl,@rep,@stops,@time,@base,@bonus,@th,@xp,@cargo,1,100);";
            ExecuteNonQuery(ins, new Dictionary<string, object>
            {
                ["@id"] = id, ["@n"] = q.QuestName ?? "Runtime Quest", ["@d"] = q.QuestDescription ?? "",
                ["@t"] = q.QuestType.ToString(), ["@diff"] = q.Difficulty.ToString(), ["@lvl"] = Mathf.Max(1, q.RequiredLevel),
                ["@rep"] = q.IsRepeatable ? 1 : 0, ["@stops"] = Mathf.Clamp(q.DeliveryLocations != null ? q.DeliveryLocations.Count : 1, 1, 5),
                ["@time"] = Mathf.Max(1, Mathf.RoundToInt(q.TimeLimit)), ["@base"] = q.BaseReward, ["@bonus"] = q.BonusReward,
                ["@th"] = Mathf.Clamp01(q.BonusTimeThreshold), ["@xp"] = Mathf.Max(0, q.XPReward), ["@cargo"] = cargoId
            });
            return id;
        }

        private int? EnsureLocation(QuestLocation l, string type)
        {
            if (l == null || string.IsNullOrWhiteSpace(l.LocationName)) return null;
            string t = type == "pickup" ? "pickup" : "delivery";
            object existing = ExecuteScalar("SELECT location_id FROM quest_locations WHERE location_name=@n AND location_type IN (@t,'both') LIMIT 1;",
                new Dictionary<string, object> { ["@n"] = l.LocationName, ["@t"] = t });
            int id = CvI(existing, -1); if (id > 0) return id;
            int nid = EnsureNeighborhood("Unknown");
            const string ins = @"INSERT INTO quest_locations
            (location_name,neighborhood_id,world_x,world_y,world_z,road_segment_index,waypoint_index,trigger_radius,location_type,is_enabled)
            VALUES (@n,@nid,@x,@y,@z,@r,@w,@rad,@t,1);";
            if (!ExecuteNonQuery(ins, new Dictionary<string, object>
            {
                ["@n"] = l.LocationName, ["@nid"] = nid, ["@x"] = l.Position.x, ["@y"] = l.Position.y, ["@z"] = l.Position.z,
                ["@r"] = l.RoadSegmentIndex, ["@w"] = l.WaypointIndex, ["@rad"] = Mathf.Max(0.5f, l.TriggerRadius), ["@t"] = t
            })) return null;
            return CvI(ExecuteScalar("SELECT last_insert_rowid();"), -1);
        }

        private int EnsureNeighborhood(string name)
        {
            ExecuteNonQuery("INSERT OR IGNORE INTO neighborhoods (name,risk_level,traffic_density_factor,is_active) VALUES (@n,1,1.0,1);",
                new Dictionary<string, object> { ["@n"] = string.IsNullOrWhiteSpace(name) ? "Unknown" : name });
            return CvI(ExecuteScalar("SELECT neighborhood_id FROM neighborhoods WHERE name=@n LIMIT 1;",
                new Dictionary<string, object> { ["@n"] = string.IsNullOrWhiteSpace(name) ? "Unknown" : name }), 1);
        }

        private int? GetNeighborhoodForLocation(int? locationId)
        {
            if (!locationId.HasValue || locationId.Value <= 0) return null;
            int id = CvI(ExecuteScalar("SELECT neighborhood_id FROM quest_locations WHERE location_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = locationId.Value }), -1);
            return id > 0 ? id : (int?)null;
        }

        private CargoData GetCargo(int cargoTypeId)
        {
            if (cargoTypeId <= 0) return null;
            List<Dictionary<string, object>> rows = ExecuteQuery("SELECT cargo_name,weight_kg,is_fragile,base_health,description FROM cargo_types WHERE cargo_type_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = cargoTypeId });
            if (rows.Count == 0) return null;
            Dictionary<string, object> r = rows[0];
            return new CargoData(S(r, "cargo_name"), F(r, "weight_kg", 100f), I(r, "is_fragile", 0) == 1, S(r, "description")) { CargoHealth = Mathf.Clamp(F(r, "base_health", 100f), 0f, 100f) };
        }

        private QuestLocation GetLocation(int locationId)
        {
            List<Dictionary<string, object>> rows = ExecuteQuery("SELECT location_name,world_x,world_y,world_z,road_segment_index,waypoint_index,trigger_radius FROM quest_locations WHERE location_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = locationId });
            if (rows.Count == 0) return null;
            Dictionary<string, object> r = rows[0];
            return new QuestLocation(new Vector3(F(r, "world_x", 0), F(r, "world_y", 0), F(r, "world_z", 0)), S(r, "location_name"), Mathf.Max(0.5f, F(r, "trigger_radius", 10f)))
            { RoadSegmentIndex = I(r, "road_segment_index", -1), WaypointIndex = I(r, "waypoint_index", -1) };
        }

        private List<QuestLocation> GetStops(string questId)
        {
            List<QuestLocation> list = new List<QuestLocation>();
            const string sql = @"SELECT ql.location_name,ql.world_x,ql.world_y,ql.world_z,ql.road_segment_index,ql.waypoint_index,ql.trigger_radius
            FROM quest_instance_stops s JOIN quest_locations ql ON ql.location_id=s.location_id WHERE s.quest_instance_id=@id ORDER BY s.stop_order ASC;";
            foreach (Dictionary<string, object> r in ExecuteQuery(sql, new Dictionary<string, object> { ["@id"] = questId }))
            {
                list.Add(new QuestLocation(new Vector3(F(r, "world_x", 0), F(r, "world_y", 0), F(r, "world_z", 0)), S(r, "location_name"), Mathf.Max(0.5f, F(r, "trigger_radius", 10f)))
                { RoadSegmentIndex = I(r, "road_segment_index", -1), WaypointIndex = I(r, "waypoint_index", -1) });
            }
            return list;
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={dbPath}");
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static SqliteCommand Build(SqliteConnection connection, string sql,
            Dictionary<string, object> parameters, SqliteTransaction transaction)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = sql;
                command.Transaction = transaction;
                if (parameters != null)
                {
                    foreach (var parameter in parameters)
                        command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                }
                return command;
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }

        private static string GetCompanyIdForPlayer(string playerId)
        {
            return string.Equals(playerId, DefaultPlayerId, StringComparison.Ordinal)
                ? DefaultCompanyId
                : $"company-{playerId}";
        }
        private static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        private static object Db(object v) => v ?? DBNull.Value;
        private static string NormalizeCompanyName(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static int CvI(object v, int fb) { try { return v == null || v == DBNull.Value ? fb : Convert.ToInt32(v); } catch { return fb; } }
        private static string S(Dictionary<string, object> r, string k) => r.TryGetValue(k, out object v) && v != null ? Convert.ToString(v) ?? string.Empty : string.Empty;
        private static int I(Dictionary<string, object> r, string k, int fb) => r.TryGetValue(k, out object v) ? CvI(v, fb) : fb;
        private static float F(Dictionary<string, object> r, string k, float fb) { try { return r.TryGetValue(k, out object v) && v != null && v != DBNull.Value ? Convert.ToSingle(v) : fb; } catch { return fb; } }
        private static T P<T>(string v, T fb) where T : struct => Enum.TryParse(v, true, out T p) ? p : fb;

        private bool ValidateProvider()
        {
            try
            {
                using (var connection = new SqliteConnection("Data Source=:memory:"))
                    connection.Open();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestDatabaseService] Provider validation failed: {FormatExceptionChain(ex)} | {SqliteProviderResolver.GetNativeDependencyDiagnostics()}");
                return false;
            }
        }

        private static string FormatExceptionChain(Exception ex)
        {
            Exception current = ex;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (current != null)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(current.GetType().Name);
                sb.Append(": ");
                sb.Append(current.Message);
                current = current.InnerException;
            }

            return sb.ToString();
        }

    }
}
