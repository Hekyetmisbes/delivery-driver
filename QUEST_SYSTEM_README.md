# Quest System Architecture

This document summarizes how the Delivery Driver quest system is structured, how data flows between systems, and where to configure or extend functionality.

## Overview
The quest system is event-driven and built around a central `QuestManager` singleton. It coordinates quest generation, quest lifecycle state, UI updates, and reward distribution.

## Core Components
- `QuestManager`: Generates quests, manages lifecycle (accept, pickup, deliver, fail), spawns zones/markers, and dispatches quest events.
- `QuestData`: Serializable data model for a quest instance (locations, difficulty, rewards, timers, and progress).
- `QuestDatabase`: ScriptableObject library of quest templates for curated quest pools.
- `QuestZone`: Trigger colliders for pickup and delivery checkpoints.
- `QuestMarkerPool`: Pooled visual markers for quest locations.
- `PlayerProgressionManager`: Handles currency, XP, achievements, and analytics statistics.
- `SaveManager`: Persists `GameSaveData` containing quest + player state.

## UI Flow
- `QuestUIManager` listens to quest events and updates `QuestListUI`, `ActiveQuestUI`, and `QuestCompleteUI`.
- `PauseMenuUI` can optionally show `QuestStatisticsUI` for session analytics.
- `QuestCompleteUI` presents completion or failure results and basic quest stats.

## Data Flow (High-Level)
1. `QuestManager.GenerateAvailableQuests()` creates quests (from templates or procedural generation).
2. Player accepts a quest via UI → `QuestManager.AcceptQuest()` → quest enters `Active`.
3. Pickup triggers `QuestZone` → cargo loaded → delivery zones spawned.
4. Delivery triggers completion → rewards + analytics recorded by `PlayerProgressionManager`.
5. `SaveManager` persists `PlayerProgressionData` and `QuestSaveData`.

## Configuration
- `QuestSystemSettings` (ScriptableObject) exposes tuning knobs for time multipliers, reward scaling, and distance ranges.
  - Assign this asset to `QuestManager` in the scene for consistent tuning.

## Analytics & Statistics
`PlayerProgressionManager` tracks:
- Quest attempts, completions, failures, success rate
- Money earned, distance traveled, and delivery times
- S-rank counts and favorite cargo
- Daily stats and level progression snapshots

Use `QuestStatisticsUI` to display these values in a menu.

## Debug & Validation
- `DebugQuestMenu` (F1) provides instant quest controls in dev builds.
- `QuestBalanceTester` offers quick balance reports and edge-case checks.
- `QuestSystemValidator` can be invoked from the context menu to check key references.

## Extending the System
- Add new quest types by extending `QuestEnums` and implementing generation methods in `QuestManager`.
- Add new quest templates in `QuestDatabase` for designer-driven content.
- Extend analytics by adding fields to `PlayerProgressionManager` and persisting in `SaveData`.
