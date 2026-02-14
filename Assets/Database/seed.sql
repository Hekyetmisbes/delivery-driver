PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Step 1 (MVP) seed data

INSERT OR IGNORE INTO players (
  player_id, display_name, created_at, last_login_at, level, xp, xp_to_next_level, money_balance, reputation_score
) VALUES
('11111111-1111-1111-1111-111111111111', 'PlayerOne', '2026-02-14 10:00:00', '2026-02-14 10:00:00', 3, 120, 200, 1500, 5);

INSERT OR IGNORE INTO neighborhoods (neighborhood_id, name, risk_level, traffic_density_factor, is_active) VALUES
(1, 'Downtown', 3, 1.30, 1),
(2, 'Old Town', 2, 1.10, 1),
(3, 'Industrial', 4, 1.45, 1),
(4, 'Suburbs', 1, 0.90, 1);

INSERT OR IGNORE INTO quest_locations (
  location_id, location_name, neighborhood_id, world_x, world_y, world_z,
  road_segment_index, waypoint_index, trigger_radius, location_type, is_enabled
) VALUES
(1, 'Downtown Warehouse', 1, 120.0, 0.0, 340.0, 10, 2, 12.0, 'pickup', 1),
(2, 'Old Town Market', 2, 450.0, 0.0, 210.0, 14, 5, 10.0, 'delivery', 1),
(3, 'Industrial Depot', 3, 800.0, 0.0, 120.0, 21, 1, 14.0, 'both', 1),
(4, 'Suburbs Gas Station', 4, 210.0, 0.0, 760.0, 31, 3, 10.0, 'delivery', 1);

INSERT OR IGNORE INTO cargo_types (
  cargo_type_id, cargo_name, weight_kg, is_fragile, base_health, description, icon_key
) VALUES
(1, 'Standard Box', 80.0, 0, 100, 'Genel dagitim paketi', 'cargo_standard'),
(2, 'Glassware', 40.0, 1, 100, 'Kirilmaya hassas cam urunler', 'cargo_glass'),
(3, 'Medical Supplies', 25.0, 1, 100, 'Acil tibbi malzemeler', 'cargo_medical'),
(4, 'Machine Parts', 140.0, 0, 100, 'Agir sanayi parcasi', 'cargo_parts');

INSERT OR IGNORE INTO quest_templates (
  template_id, quest_name, quest_description, quest_type, difficulty,
  required_level, is_repeatable, delivery_stop_count, base_time_limit_sec,
  base_reward, bonus_reward, bonus_time_threshold, xp_reward,
  default_cargo_type_id, is_active, weight
) VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Package Delivery', 'Pick up from Downtown and deliver to Old Town.', 'StandardDelivery', 'Easy', 1, 1, 1, 300, 120, 50, 0.50, 40, 1, 1, 100),
('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Fragile Rush', 'Deliver fragile cargo without heavy collisions.', 'FragileDelivery', 'Medium', 2, 1, 1, 360, 180, 90, 0.45, 70, 2, 1, 80),
('cccccccc-cccc-cccc-cccc-cccccccccccc', 'City Multi Drop', 'Complete two-stop delivery route.', 'MultiStopDelivery', 'Hard', 3, 1, 2, 540, 300, 150, 0.40, 120, 4, 1, 60),
('dddddddd-dddd-dddd-dddd-dddddddddddd', 'Medical Express', 'Fast urgent delivery to Suburbs.', 'ExpressDelivery', 'Medium', 2, 1, 1, 240, 220, 120, 0.55, 85, 3, 1, 70);

INSERT OR IGNORE INTO quest_instances (
  quest_instance_id, player_id, template_id, quest_status,
  assigned_at, started_at, completed_at, failed_at,
  time_limit_sec, time_remaining_sec, current_delivery_index, has_picked_up_cargo,
  pickup_location_id, pickup_neighborhood_id, selected_cargo_type_id,
  cargo_health, collision_count, npc_collision_count, total_distance_m,
  earned_bonus, performance_rating, base_reward_final, bonus_reward_final,
  penalty_amount, total_reward_final, failure_reason,
  is_daily_challenge, daily_challenge_date, streak_multiplier_applied
) VALUES
(
  'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
  '11111111-1111-1111-1111-111111111111',
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  'Active',
  '2026-02-14 10:05:00',
  '2026-02-14 10:06:00',
  NULL,
  NULL,
  300,
  245,
  0,
  1,
  1,
  1,
  1,
  100,
  0,
  0,
  820.0,
  0,
  NULL,
  120,
  0,
  0,
  120,
  NULL,
  0,
  NULL,
  1.10
);

INSERT OR IGNORE INTO quest_instance_stops (
  stop_id, quest_instance_id, stop_order, location_id, neighborhood_id, status, eta_sec, reached_at, completed_at
) VALUES
(1, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 1, 2, 2, 'Pending', 180, NULL, NULL);

COMMIT;