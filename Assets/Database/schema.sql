PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Step 1 (MVP): Core quest database schema

CREATE TABLE IF NOT EXISTS players (
  player_id TEXT PRIMARY KEY,
  display_name TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  last_login_at TEXT,
  level INTEGER NOT NULL DEFAULT 1 CHECK (level >= 1),
  xp INTEGER NOT NULL DEFAULT 0 CHECK (xp >= 0),
  xp_to_next_level INTEGER NOT NULL DEFAULT 100 CHECK (xp_to_next_level > 0),
  money_balance INTEGER NOT NULL DEFAULT 0,
  reputation_score INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS neighborhoods (
  neighborhood_id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  risk_level INTEGER NOT NULL DEFAULT 1 CHECK (risk_level BETWEEN 1 AND 5),
  traffic_density_factor REAL NOT NULL DEFAULT 1.00 CHECK (traffic_density_factor > 0),
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1))
);

CREATE TABLE IF NOT EXISTS quest_locations (
  location_id INTEGER PRIMARY KEY AUTOINCREMENT,
  location_name TEXT NOT NULL,
  neighborhood_id INTEGER NOT NULL REFERENCES neighborhoods(neighborhood_id),
  world_x REAL NOT NULL,
  world_y REAL NOT NULL,
  world_z REAL NOT NULL,
  road_segment_index INTEGER NOT NULL DEFAULT -1,
  waypoint_index INTEGER NOT NULL DEFAULT -1,
  trigger_radius REAL NOT NULL DEFAULT 10.0 CHECK (trigger_radius > 0),
  location_type TEXT NOT NULL CHECK (location_type IN ('pickup', 'delivery', 'both')),
  is_enabled INTEGER NOT NULL DEFAULT 1 CHECK (is_enabled IN (0, 1))
);

CREATE TABLE IF NOT EXISTS cargo_types (
  cargo_type_id INTEGER PRIMARY KEY AUTOINCREMENT,
  cargo_name TEXT NOT NULL UNIQUE,
  weight_kg REAL NOT NULL CHECK (weight_kg >= 0 AND weight_kg <= 500),
  is_fragile INTEGER NOT NULL DEFAULT 0 CHECK (is_fragile IN (0, 1)),
  base_health REAL NOT NULL DEFAULT 100 CHECK (base_health BETWEEN 0 AND 100),
  description TEXT NOT NULL DEFAULT '',
  icon_key TEXT
);

CREATE TABLE IF NOT EXISTS quest_templates (
  template_id TEXT PRIMARY KEY,
  quest_name TEXT NOT NULL,
  quest_description TEXT NOT NULL DEFAULT '',
  quest_type TEXT NOT NULL CHECK (quest_type IN ('StandardDelivery', 'ExpressDelivery', 'FragileDelivery', 'MultiStopDelivery', 'TimeTrial')),
  difficulty TEXT NOT NULL CHECK (difficulty IN ('Easy', 'Medium', 'Hard', 'Expert')),
  required_level INTEGER NOT NULL DEFAULT 1 CHECK (required_level >= 1),
  is_repeatable INTEGER NOT NULL DEFAULT 1 CHECK (is_repeatable IN (0, 1)),
  delivery_stop_count INTEGER NOT NULL DEFAULT 1 CHECK (delivery_stop_count BETWEEN 1 AND 5),
  base_time_limit_sec INTEGER NOT NULL CHECK (base_time_limit_sec > 0),
  base_reward INTEGER NOT NULL DEFAULT 0,
  bonus_reward INTEGER NOT NULL DEFAULT 0,
  bonus_time_threshold REAL NOT NULL DEFAULT 0.5 CHECK (bonus_time_threshold BETWEEN 0 AND 1),
  xp_reward INTEGER NOT NULL DEFAULT 0,
  default_cargo_type_id INTEGER REFERENCES cargo_types(cargo_type_id),
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
  weight INTEGER NOT NULL DEFAULT 100 CHECK (weight > 0)
);

CREATE TABLE IF NOT EXISTS quest_instances (
  quest_instance_id TEXT PRIMARY KEY,
  player_id TEXT NOT NULL REFERENCES players(player_id),
  template_id TEXT NOT NULL REFERENCES quest_templates(template_id),
  quest_status TEXT NOT NULL CHECK (quest_status IN ('NotStarted', 'Active', 'Completed', 'Failed', 'Expired')),
  assigned_at TEXT NOT NULL DEFAULT (datetime('now')),
  started_at TEXT,
  completed_at TEXT,
  failed_at TEXT,
  time_limit_sec INTEGER NOT NULL CHECK (time_limit_sec > 0),
  time_remaining_sec INTEGER NOT NULL CHECK (time_remaining_sec >= 0),
  current_delivery_index INTEGER NOT NULL DEFAULT 0 CHECK (current_delivery_index >= 0),
  has_picked_up_cargo INTEGER NOT NULL DEFAULT 0 CHECK (has_picked_up_cargo IN (0, 1)),
  pickup_location_id INTEGER REFERENCES quest_locations(location_id),
  pickup_neighborhood_id INTEGER REFERENCES neighborhoods(neighborhood_id),
  selected_cargo_type_id INTEGER REFERENCES cargo_types(cargo_type_id),
  cargo_health REAL CHECK (cargo_health BETWEEN 0 AND 100),
  collision_count INTEGER NOT NULL DEFAULT 0 CHECK (collision_count >= 0),
  npc_collision_count INTEGER NOT NULL DEFAULT 0 CHECK (npc_collision_count >= 0),
  total_distance_m REAL NOT NULL DEFAULT 0 CHECK (total_distance_m >= 0),
  earned_bonus INTEGER NOT NULL DEFAULT 0 CHECK (earned_bonus IN (0, 1)),
  performance_rating TEXT CHECK (performance_rating IN ('F', 'D', 'C', 'B', 'A', 'S')),
  base_reward_final INTEGER NOT NULL DEFAULT 0,
  bonus_reward_final INTEGER NOT NULL DEFAULT 0,
  penalty_amount INTEGER NOT NULL DEFAULT 0,
  total_reward_final INTEGER NOT NULL DEFAULT 0,
  failure_reason TEXT,
  is_daily_challenge INTEGER NOT NULL DEFAULT 0 CHECK (is_daily_challenge IN (0, 1)),
  daily_challenge_date TEXT,
  streak_multiplier_applied REAL NOT NULL DEFAULT 1.00 CHECK (streak_multiplier_applied > 0),
  CHECK (time_remaining_sec <= time_limit_sec),
  CHECK ((quest_status <> 'Completed') OR completed_at IS NOT NULL),
  CHECK ((quest_status <> 'Failed') OR (failure_reason IS NOT NULL AND length(trim(failure_reason)) > 0))
);

CREATE TABLE IF NOT EXISTS quest_instance_stops (
  stop_id INTEGER PRIMARY KEY AUTOINCREMENT,
  quest_instance_id TEXT NOT NULL REFERENCES quest_instances(quest_instance_id) ON DELETE CASCADE,
  stop_order INTEGER NOT NULL CHECK (stop_order >= 1),
  location_id INTEGER NOT NULL REFERENCES quest_locations(location_id),
  neighborhood_id INTEGER REFERENCES neighborhoods(neighborhood_id),
  status TEXT NOT NULL CHECK (status IN ('Pending', 'Reached', 'Completed', 'Skipped')),
  eta_sec INTEGER CHECK (eta_sec IS NULL OR eta_sec >= 0),
  reached_at TEXT,
  completed_at TEXT,
  UNIQUE (quest_instance_id, stop_order)
);

-- Step 2 (Live Ops): Analytics and economy tables
CREATE TABLE IF NOT EXISTS quest_events (
  event_id INTEGER PRIMARY KEY AUTOINCREMENT,
  quest_instance_id TEXT NOT NULL REFERENCES quest_instances(quest_instance_id) ON DELETE CASCADE,
  player_id TEXT NOT NULL REFERENCES players(player_id),
  event_type TEXT NOT NULL,
  event_time TEXT NOT NULL DEFAULT (datetime('now')),
  event_value_num REAL,
  event_value_text TEXT,
  position_x REAL,
  position_y REAL,
  position_z REAL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS wallet_transactions (
  tx_id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id TEXT NOT NULL REFERENCES players(player_id),
  quest_instance_id TEXT REFERENCES quest_instances(quest_instance_id) ON DELETE SET NULL,
  tx_type TEXT NOT NULL CHECK (tx_type IN ('QUEST_REWARD', 'BONUS', 'PENALTY', 'PURCHASE', 'REFUND')),
  amount INTEGER NOT NULL,
  balance_after INTEGER NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  description TEXT
);

CREATE TABLE IF NOT EXISTS player_daily_stats (
  player_id TEXT NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  stat_date TEXT NOT NULL,
  quests_completed INTEGER NOT NULL DEFAULT 0 CHECK (quests_completed >= 0),
  quests_failed INTEGER NOT NULL DEFAULT 0 CHECK (quests_failed >= 0),
  money_earned INTEGER NOT NULL DEFAULT 0,
  distance_traveled_m REAL NOT NULL DEFAULT 0 CHECK (distance_traveled_m >= 0),
  avg_delivery_time_sec REAL NOT NULL DEFAULT 0 CHECK (avg_delivery_time_sec >= 0),
  fastest_delivery_sec REAL NOT NULL DEFAULT 0 CHECK (fastest_delivery_sec >= 0),
  fragile_undamaged_count INTEGER NOT NULL DEFAULT 0 CHECK (fragile_undamaged_count >= 0),
  PRIMARY KEY (player_id, stat_date)
);

-- Step 3 (Advanced): Dynamic pricing, seasonal events, leaderboard, anti-cheat
CREATE TABLE IF NOT EXISTS pricing_rules (
  rule_id INTEGER PRIMARY KEY AUTOINCREMENT,
  neighborhood_id INTEGER REFERENCES neighborhoods(neighborhood_id),
  hour_start INTEGER NOT NULL CHECK (hour_start BETWEEN 0 AND 23),
  hour_end INTEGER NOT NULL CHECK (hour_end BETWEEN 0 AND 23),
  difficulty TEXT CHECK (difficulty IN ('Easy', 'Medium', 'Hard', 'Expert')),
  min_distance_m REAL NOT NULL DEFAULT 0 CHECK (min_distance_m >= 0),
  max_distance_m REAL,
  reward_multiplier REAL NOT NULL DEFAULT 1.0 CHECK (reward_multiplier > 0),
  time_limit_multiplier REAL NOT NULL DEFAULT 1.0 CHECK (time_limit_multiplier > 0),
  traffic_multiplier REAL NOT NULL DEFAULT 1.0 CHECK (traffic_multiplier > 0),
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
  valid_from TEXT,
  valid_to TEXT,
  CHECK (max_distance_m IS NULL OR max_distance_m >= min_distance_m)
);

CREATE TABLE IF NOT EXISTS seasonal_events (
  event_id INTEGER PRIMARY KEY AUTOINCREMENT,
  event_key TEXT NOT NULL UNIQUE,
  event_name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  starts_at TEXT NOT NULL,
  ends_at TEXT NOT NULL,
  reward_multiplier REAL NOT NULL DEFAULT 1.0 CHECK (reward_multiplier > 0),
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
  CHECK (ends_at > starts_at)
);

CREATE TABLE IF NOT EXISTS seasonal_event_templates (
  event_id INTEGER NOT NULL REFERENCES seasonal_events(event_id) ON DELETE CASCADE,
  template_id TEXT NOT NULL REFERENCES quest_templates(template_id) ON DELETE CASCADE,
  spawn_weight INTEGER NOT NULL DEFAULT 100 CHECK (spawn_weight > 0),
  PRIMARY KEY (event_id, template_id)
);

CREATE TABLE IF NOT EXISTS leaderboard_snapshots (
  snapshot_id INTEGER PRIMARY KEY AUTOINCREMENT,
  board_key TEXT NOT NULL,
  board_period TEXT NOT NULL CHECK (board_period IN ('daily', 'weekly', 'seasonal', 'alltime')),
  period_start TEXT NOT NULL,
  period_end TEXT NOT NULL,
  player_id TEXT NOT NULL REFERENCES players(player_id),
  score_value REAL NOT NULL DEFAULT 0,
  rank_position INTEGER NOT NULL CHECK (rank_position >= 1),
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS anti_cheat_flags (
  flag_id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id TEXT NOT NULL REFERENCES players(player_id),
  quest_instance_id TEXT REFERENCES quest_instances(quest_instance_id) ON DELETE SET NULL,
  source_event_id INTEGER REFERENCES quest_events(event_id) ON DELETE SET NULL,
  flag_type TEXT NOT NULL CHECK (flag_type IN ('SpeedHack', 'Teleport', 'ImpossibleCompletionTime', 'EconomyAnomaly', 'CollisionTamper')),
  severity INTEGER NOT NULL DEFAULT 1 CHECK (severity BETWEEN 1 AND 5),
  confidence REAL NOT NULL DEFAULT 0.5 CHECK (confidence BETWEEN 0 AND 1),
  detected_at TEXT NOT NULL DEFAULT (datetime('now')),
  status TEXT NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'Reviewed', 'Ignored', 'Actioned')),
  notes TEXT
);

CREATE INDEX IF NOT EXISTS idx_quest_instances_player_status
  ON quest_instances(player_id, quest_status);

CREATE INDEX IF NOT EXISTS idx_quest_instances_assigned_at
  ON quest_instances(assigned_at DESC);

CREATE INDEX IF NOT EXISTS idx_quest_instances_template
  ON quest_instances(template_id);

CREATE INDEX IF NOT EXISTS idx_quest_stops_instance_order
  ON quest_instance_stops(quest_instance_id, stop_order);

CREATE INDEX IF NOT EXISTS idx_quest_events_instance_time
  ON quest_events(quest_instance_id, event_time);

CREATE INDEX IF NOT EXISTS idx_wallet_tx_player_created
  ON wallet_transactions(player_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_pricing_rules_active_scope
  ON pricing_rules(is_active, neighborhood_id, difficulty, hour_start, hour_end);

CREATE INDEX IF NOT EXISTS idx_seasonal_events_active_window
  ON seasonal_events(is_active, starts_at, ends_at);

CREATE INDEX IF NOT EXISTS idx_leaderboard_lookup
  ON leaderboard_snapshots(board_key, board_period, period_start, rank_position);

CREATE INDEX IF NOT EXISTS idx_anticheat_player_status
  ON anti_cheat_flags(player_id, status, detected_at DESC);

CREATE INDEX IF NOT EXISTS idx_quest_locations_neighborhood_enabled
  ON quest_locations(neighborhood_id, is_enabled);

CREATE INDEX IF NOT EXISTS idx_quest_templates_diff_level_active
  ON quest_templates(difficulty, required_level, is_active);

COMMIT;
