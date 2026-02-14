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

CREATE INDEX IF NOT EXISTS idx_quest_instances_player_status
  ON quest_instances(player_id, quest_status);

CREATE INDEX IF NOT EXISTS idx_quest_instances_assigned_at
  ON quest_instances(assigned_at DESC);

CREATE INDEX IF NOT EXISTS idx_quest_instances_template
  ON quest_instances(template_id);

CREATE INDEX IF NOT EXISTS idx_quest_stops_instance_order
  ON quest_instance_stops(quest_instance_id, stop_order);

CREATE INDEX IF NOT EXISTS idx_quest_locations_neighborhood_enabled
  ON quest_locations(neighborhood_id, is_enabled);

CREATE INDEX IF NOT EXISTS idx_quest_templates_diff_level_active
  ON quest_templates(difficulty, required_level, is_active);

COMMIT;