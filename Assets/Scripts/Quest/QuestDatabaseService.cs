using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        public int GetPlayerBalance(string playerId, int fallback = 0)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(playerId))
            {
                return fallback;
            }

            object result = ExecuteScalar(
                "SELECT money_balance FROM players WHERE player_id=@id LIMIT 1;",
                new Dictionary<string, object> { ["@id"] = playerId });
            return CvI(result, fallback);
        }

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
            if (quest == null || string.IsNullOrWhiteSpace(quest.QuestID) || !EnsurePlayer(playerId)) return false;
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
              completed_at=excluded.completed_at,
              failed_at=excluded.failed_at,
              time_limit_sec=excluded.time_limit_sec,
              time_remaining_sec=excluded.time_remaining_sec,
              current_delivery_index=excluded.current_delivery_index,
              has_picked_up_cargo=excluded.has_picked_up_cargo,
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
            return SaveStops(quest.QuestID, quest.DeliveryLocations);
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
            VALUES (@pid,@qid,@type,@amount,@bal,datetime('now'),@desc);";
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
            if (!IsReady || string.IsNullOrWhiteSpace(sql)) return false;
            lock (gate)
            {
                object conn = null; object cmd = null;
                try
                {
                    conn = Open(); cmd = Build(conn, sql, parameters); Invoke(cmd, "ExecuteNonQuery"); return true;
                }
                catch (Exception e) { Debug.LogError($"[QuestDatabaseService] NonQuery failed: {e.Message}"); return false; }
                finally { Dispose(cmd); Close(conn); }
            }
        }

        public object ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(sql)) return null;
            lock (gate)
            {
                object conn = null; object cmd = null;
                try { conn = Open(); cmd = Build(conn, sql, parameters); return Invoke(cmd, "ExecuteScalar"); }
                catch (Exception e) { Debug.LogError($"[QuestDatabaseService] Scalar failed: {e.Message}"); return null; }
                finally { Dispose(cmd); Close(conn); }
            }
        }

        public List<Dictionary<string, object>> ExecuteQuery(string sql, Dictionary<string, object> parameters = null)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            if (!IsReady || string.IsNullOrWhiteSpace(sql)) return rows;
            lock (gate)
            {
                object conn = null; object cmd = null; object reader = null;
                try
                {
                    conn = Open(); cmd = Build(conn, sql, parameters); reader = Invoke(cmd, "ExecuteReader");
                    int n = CvI(GetProp(reader, "FieldCount"), 0);
                    while (Convert.ToBoolean(Invoke(reader, "Read")))
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < n; i++)
                        {
                            string key = Convert.ToString(Invoke(reader, "GetName", i));
                            object value = Invoke(reader, "GetValue", i);
                            row[key] = value == DBNull.Value ? null : value;
                        }
                        rows.Add(row);
                    }
                }
                catch (Exception e) { Debug.LogError($"[QuestDatabaseService] Query failed: {e.Message}"); }
                finally { Dispose(reader); Dispose(cmd); Close(conn); }
            }
            return rows;
        }

        private bool SaveStops(string questId, List<QuestLocation> stops)
        {
            if (!ExecuteNonQuery("DELETE FROM quest_instance_stops WHERE quest_instance_id=@qid;", new Dictionary<string, object> { ["@qid"] = questId })) return false;
            if (stops == null) return true;
            const string sql = @"INSERT INTO quest_instance_stops
            (quest_instance_id,stop_order,location_id,neighborhood_id,status,eta_sec,reached_at,completed_at)
            VALUES (@qid,@ord,@loc,@nid,'Pending',NULL,NULL,NULL);";
            for (int i = 0; i < stops.Count; i++)
            {
                int? loc = EnsureLocation(stops[i], "delivery");
                if (!loc.HasValue) continue;
                int? nid = GetNeighborhoodForLocation(loc);
                if (!ExecuteNonQuery(sql, new Dictionary<string, object> { ["@qid"] = questId, ["@ord"] = i + 1, ["@loc"] = loc.Value, ["@nid"] = Db(nid) })) return false;
            }
            return true;
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

        private object Open() { object c = Activator.CreateInstance(connType, $"Data Source={dbPath}"); Invoke(c, "Open"); return c; }
        private object Build(object conn, string sql, Dictionary<string, object> p)
        {
            object cmd = Invoke(conn, "CreateCommand"); SetProp(cmd, "CommandText", sql);
            if (p == null) return cmd;
            object col = GetProp(cmd, "Parameters");
            foreach (KeyValuePair<string, object> kv in p)
            {
                object prm = Invoke(cmd, "CreateParameter");
                SetProp(prm, "ParameterName", kv.Key);
                SetProp(prm, "Value", kv.Value ?? DBNull.Value);

                if (col is System.Collections.IList list)
                {
                    list.Add(prm);
                }
                else
                {
                    Invoke(col, "Add", prm);
                }
            }
            return cmd;
        }
        private static void Close(object c) { if (c != null) try { Invoke(c, "Close"); } catch { } }
        private static void Dispose(object o) { if (o != null) try { Invoke(o, "Dispose"); } catch { } }
        private static object Invoke(object o, string m, params object[] a)
        {
            Type t = o.GetType();
            object[] args = a ?? Array.Empty<object>();
            MethodInfo best = null;
            int bestScore = -1;

            foreach (MethodInfo candidate in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!string.Equals(candidate.Name, m, StringComparison.Ordinal)) continue;
                ParameterInfo[] ps = candidate.GetParameters();
                if (ps.Length != args.Length) continue;

                int score = 0;
                bool compatible = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt.IsByRef) pt = pt.GetElementType();

                    object arg = args[i];
                    if (arg == null)
                    {
                        if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null)
                        {
                            compatible = false;
                            break;
                        }
                        score += 1;
                        continue;
                    }

                    Type at = arg.GetType();
                    if (pt == at) score += 4;
                    else if (pt.IsAssignableFrom(at)) score += 3;
                    else if (pt == typeof(object)) score += 2;
                    else
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible) continue;
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                throw new MissingMethodException(t.FullName, m);
            }

            return best.Invoke(o, args);
        }
        private static object GetProp(object o, string p)
        {
            PropertyInfo prop = FindProperty(o.GetType(), p);
            if (prop == null)
            {
                throw new MissingMemberException(o.GetType().FullName, p);
            }

            return prop.GetValue(o);
        }

        private static void SetProp(object o, string p, object v)
        {
            PropertyInfo prop = FindProperty(o.GetType(), p);
            if (prop == null || !prop.CanWrite)
            {
                throw new MissingMemberException(o.GetType().FullName, p);
            }

            prop.SetValue(o, v);
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
            object conn = null;
            try
            {
                conn = Activator.CreateInstance(connType, "Data Source=:memory:");
                Invoke(conn, "Open");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestDatabaseService] Provider validation failed: {FormatExceptionChain(ex)} | {SqliteProviderResolver.GetNativeDependencyDiagnostics()}");
                return false;
            }
            finally
            {
                Close(conn);
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

        private static PropertyInfo FindProperty(Type type, string name)
        {
            Type t = type;
            while (t != null)
            {
                PropertyInfo prop = t.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null)
                {
                    return prop;
                }

                t = t.BaseType;
            }

            return null;
        }
    }
}
