# Delivery Driver - Complete Agent Development Guide

## Table of Contents
1. [Project Overview](#project-overview)
2. [Project Goals & Vision](#project-goals--vision)
3. [Current Implementation Status](#current-implementation-status)
4. [System Architecture](#system-architecture)
5. [Core Systems Deep Dive](#core-systems-deep-dive)
6. [Code Organization](#code-organization)
7. [Development Guidelines](#development-guidelines)
8. [Technical Specifications](#technical-specifications)
9. [Integration Points](#integration-points)
10. [Future Roadmap](#future-roadmap)
11. [Common Tasks & Patterns](#common-tasks--patterns)
12. [Troubleshooting](#troubleshooting)

---

## Project Overview

**Delivery Driver** is a Unity-based 3D driving simulation game with a comprehensive quest system focused on cargo delivery missions. The game combines realistic vehicle physics, intelligent NPC traffic, procedural quest generation, and RPG-style progression mechanics.

### Game Loop
1. Player browses available delivery quests
2. Accepts a quest and navigates to pickup location
3. Picks up cargo (which affects vehicle handling)
4. Delivers cargo to destination within time limit
5. Earns rewards, XP, and unlocks new content
6. Repeats with increasing difficulty and variety

### Key Features
- **Realistic Driving Physics**: Wheel collider-based vehicle simulation with cargo weight effects
- **Dynamic Quest System**: Procedurally generated delivery missions with multiple types
- **NPC Traffic**: Autonomous AI vehicles with pathfinding and recovery systems
- **Progression System**: Level-based unlocking with achievements and daily challenges
- **Save/Load System**: Persistent game state across sessions
- **Audio & Visual Feedback**: Music transitions, sound effects, and particle systems

### Technology Stack
- **Engine**: Unity 2022.3+ (LTS)
- **Language**: C# 10
- **Input**: Unity Input System (New Input System)
- **Physics**: Unity PhysX (WheelColliders)
- **Road System**: EasyRoads3D integration (optional, can work with manual waypoints)
- **Serialization**: Unity JsonUtility + custom save system
- **Architecture**: Event-driven with Singleton managers

---

## Project Goals & Vision

### Primary Goals
1. **Create an Engaging Delivery Experience**
   - Make driving feel satisfying and challenging
   - Provide variety through different quest types and cargo
   - Balance difficulty progression to maintain engagement

2. **Build a Scalable Quest System**
   - Support procedural generation for infinite replayability
   - Allow designers to create custom quest templates
   - Enable modular quest mechanics (time trials, fragile cargo, multi-stop)

3. **Implement Robust Traffic Simulation**
   - NPCs should drive realistically along road networks
   - Automatic recovery from stuck/off-road situations
   - Performance optimization for multiple simultaneous vehicles

4. **Provide Player Progression & Retention**
   - Level-based unlocking of harder challenges
   - Achievement system for long-term goals
   - Daily challenges to encourage return visits

### Long-Term Vision
- **Open World Expansion**: Larger maps with multiple cities/regions
- **Vehicle Variety**: Different vehicle types with unique handling
- **Multiplayer**: Competitive delivery races or cooperative missions
- **Story Campaign**: Narrative-driven quest chains
- **Modding Support**: Community-created quests and vehicles
- **Mobile Port**: Optimized version for mobile platforms

---

## Current Implementation Status

### ✅ Fully Implemented (Production Ready)
- **Phase 1**: Core Data Structures (QuestEnums, QuestLocation, CargoData, QuestData, QuestDatabase)
- **Phase 2**: Quest Manager (Lifecycle, Update Loop, Location Generation, Save Hooks)
- **Phase 3**: UI System (Quest List, Active Quest Panel, Completion Popup, Markers)
- **Phase 4**: Quest Zones & Triggers (Zone Components, Pickup/Delivery Logic)
- **Phase 5**: Cargo System (Visuals, Weight Mechanics, Fragile Damage, Sound Effects, Cargo Library)
- **Phase 6**: Timer & Scoring (Quest Timer, Bonus Calculation, Distance Tracking, Collision Penalties, Performance Rating)
- **Phase 7**: Rewards & Progression (PlayerProgressionManager, Reward System, Progression UI, Quest Unlocking, Achievements, Daily Challenges)
- **Phase 8**: Save/Load System (Save Data Structures, SaveManager, Load System, Save/Load UI)
- **Phase 9**: Quest Generation (Procedural Locations, Difficulty-Based Generation, Multi-Stop Quests, Special Quest Types, Quest Pool Refresh)
- **Phase 10.1**: Audio & Music (Background Music, Sound Effects, Time Warning, Music Crossfading)
- **Phase 10.2**: Particle Effects (Quest Markers, Pickup/Delivery Effects, Damage Effects, Object Pooling)

### 🚧 Partially Implemented (Needs Work)
- **Phase 10**: Polish & Integration (Audio/Particles done, but minimap, tutorial, settings need completion)
- **Vehicle Physics**: Basic implementation exists but could use refinement
- **NPC Traffic**: Core functionality works but needs edge case handling

### ❌ Not Yet Implemented
- **Phase 10.3**: Minimap/Compass Integration
- **Phase 10.4**: Tutorial System
- **Phase 10.5**: Settings & Options Menu
- **Phase 10.6**: Performance Optimization Pass
- **Phase 10.7**: Debug Tools
- **Phase 10.8**: Final Testing & Balancing
- **Phase 10.9**: Analytics & Statistics
- **Phase 10.10**: Final Documentation

### Known Issues
1. ⚠️ RoadGraphBuilder sometimes fails to detect EasyRoads3D on complex scenes
2. ⚠️ NPC vehicles can rarely get stuck in infinite recovery loops
3. ⚠️ UI scaling issues on non-16:9 aspect ratios
4. ⚠️ Performance drops with >20 active NPCs on low-end hardware

---

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        GAME LOOP                             │
│  Unity MonoBehaviour Update/FixedUpdate/LateUpdate Cycle    │
└───────────────────┬─────────────────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        │                       │
┌───────▼──────┐      ┌────────▼────────┐
│  INPUT       │      │   MANAGERS      │
│  SYSTEM      │      │  (Singletons)   │
└───────┬──────┘      └────────┬────────┘
        │                      │
        │     ┌────────────────┼────────────────┐
        │     │                │                │
┌───────▼─────▼───┐  ┌────────▼───────┐  ┌────▼──────────┐
│  CarController  │  │ QuestManager   │  │ SaveManager   │
│  (Player)       │  │                │  │               │
└────────┬────────┘  └───────┬────────┘  └───────────────┘
         │                   │
    ┌────┴────┐         ┌────┴────────────────────┐
    │         │         │                         │
┌───▼───┐ ┌──▼──────┐ ┌▼──────────────┐  ┌──────▼────────┐
│Camera │ │ Cargo   │ │ PlayerProg-   │  │ QuestDatabase │
│Follow │ │ Visual  │ │ ressionMgr    │  │ (ScriptableObj)│
└───────┘ └─────────┘ └───────┬───────┘  └───────────────┘
                              │
                     ┌────────┴────────┐
                     │                 │
              ┌──────▼───────┐  ┌─────▼──────┐
              │ Achievement  │  │ UI Systems │
              │   System     │  │            │
              └──────────────┘  └────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    TRAFFIC SYSTEM                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │RoadGraphBuild│  │ NpcCarAgent  │  │ NpcRecovery  │      │
│  │      er      │─>│ (Waypoint AI)│──│   System     │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│         │                   │                                │
│  ┌──────▼──────┐    ┌──────▼──────┐                        │
│  │ RoadGraph   │    │ NpcSpawner  │                        │
│  │ (Waypoints) │    │             │                        │
│  └─────────────┘    └─────────────┘                        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                      QUEST SYSTEM                            │
│  ┌───────────┐  ┌───────────┐  ┌─────────────┐             │
│  │ QuestData │  │QuestZone  │  │ QuestMarker │             │
│  │           │─>│ (Trigger) │──│  (Visual)   │             │
│  └───────────┘  └───────────┘  └─────────────┘             │
│       │                                                      │
│  ┌────▼─────────┐  ┌─────────────┐                         │
│  │ CargoLibrary │  │ QuestUIMan- │                         │
│  │(Scriptable)  │  │    ager     │                         │
│  └──────────────┘  └─────────────┘                         │
└─────────────────────────────────────────────────────────────┘
```

### Design Patterns Used

1. **Singleton Pattern**
   - `QuestManager.Instance`
   - `PlayerProgressionManager.Instance`
   - `SaveManager.Instance`
   - **Why**: Single source of truth, globally accessible, persistent across scenes

2. **Observer Pattern (Unity Events)**
   - `OnQuestStarted`, `OnQuestCompleted`, `OnQuestFailed`, `OnQuestUpdated`
   - `OnMoneyChanged`, `OnLevelUp`, `OnXPGained`
   - **Why**: Decouples systems, allows multiple listeners, easy UI integration

3. **Object Pool Pattern**
   - Particle effects pooling in QuestManager
   - **Why**: Performance optimization, reduces GC pressure, reuses GameObjects

4. **ScriptableObject Architecture**
   - `QuestDatabase`, `CargoLibrary`
   - **Why**: Data-driven design, easy designer iteration, memory efficient

5. **Component-Based Architecture**
   - Separate components for NpcCarAgent, NpcRecovery, QuestZone
   - **Why**: Modularity, reusability, easier testing

6. **State Machine (Implicit)**
   - Quest status: NotStarted → Active → Completed/Failed
   - **Why**: Clear state transitions, easier debugging

7. **Strategy Pattern**
   - Different quest types with varied generation logic
   - **Why**: Extensible quest types, encapsulated behavior

---

## Core Systems Deep Dive

### 1. Quest Management System

**Location**: `Assets/Scripts/Quest/QuestManager.cs`

**Responsibilities**:
- Generate and manage available quests
- Handle quest lifecycle (accept, complete, fail, abandon)
- Track active quest progress (timer, distance, objectives)
- Spawn and manage quest zones/markers
- Integrate with save/load system
- Coordinate audio and particle effects

**Key Methods**:
```csharp
void GenerateAvailableQuests(int count)
QuestData GenerateQuestByDifficulty(QuestDifficulty difficulty)
bool AcceptQuest(string questID)
void CompleteQuest(QuestData quest)
void FailQuest(QuestData quest, string reason)
QuestLocation GenerateRandomLocation(string prefix)
void RefreshAvailableQuests()
QuestData GenerateMultiStopQuest(int stopCount, QuestDifficulty difficulty)
QuestData GenerateExpressDelivery()
QuestData GenerateFragileDelivery()
QuestData GenerateTimeTrial()
```

**Quest Generation Flow**:
```
GenerateAvailableQuests()
    │
    ├─> Pick player level
    │
    ├─> Determine difficulty distribution
    │
    ├─> For each quest:
    │       │
    │       ├─> Roll quest type (60% standard, 20% express, 15% fragile, 5% multi-stop)
    │       │
    │       ├─> Call appropriate generation method
    │       │
    │       └─> Generate locations with distance validation
    │
    └─> Add to availableQuests list
```

**Quest Update Loop** (in `Update()`):
1. Check time expiration → Fail if expired
2. Play time warning if < 30 seconds
3. Update distance tracking
4. Check pickup proximity (if not picked up)
5. Check delivery proximity (if picked up)
6. Check cargo destruction (if fragile)
7. Invoke OnQuestUpdated event

**Location Generation Algorithm**:
```
1. Get random waypoint from RoadGraph
2. Check if too close to recently used locations (cooldown)
3. Raycast down to validate ground exists
4. Generate location name based on segment ID
5. Add to usedLocations list (max 20)
6. Return QuestLocation
```

### 2. Traffic & Road Network System

**Location**: `Assets/Scripts/RoadGraphBuilder.cs`, `RoadGraphTypes.cs`

**Purpose**: Converts EasyRoads3D road meshes into a waypoint graph for AI navigation.

**Data Structures**:
```csharp
class Waypoint {
    Vector3 position;
    Vector3 forward; // Tangent direction
    int roadSegmentIndex;
}

class RoadSegment {
    int id;
    string name;
    List<Waypoint> waypoints;
    List<RoadConnection> connections;
}

class RoadConnection {
    RoadSegment fromSegment;
    RoadSegment toSegment;
    int fromWaypointIndex;
    int toWaypointIndex;
}

class RoadGraph {
    List<RoadSegment> roadSegments;
}
```

**Road Extraction Process**:
1. Auto-detect EasyRoads3D network in scene (via reflection)
2. For each road object:
   - Extract spline/mesh data
   - Sample points along road at regular intervals (default: 5m)
   - Create waypoints with position and forward direction
3. Connect road segments at intersections
4. Build final RoadGraph

**Waypoint Sampling**:
- Uses spline interpolation or mesh centerline detection
- Configurable sample step (default: 5m)
- Handles curved roads by following tangent vectors
- Normalizes forward vectors for consistent direction

### 3. NPC Vehicle AI System

**Location**: `Assets/Scripts/NpcCarAgent.cs`, `Assets/Scripts/NpcRecovery.cs`

**NpcCarAgent** - Main AI controller:
- Follows waypoints from RoadGraph
- Applies steering and throttle using WheelColliders
- Handles lane selection and speed limiting
- Detects intersections and road ends
- Uses Rigidbody forces for movement

**Key Features**:
- Target speed based on road segment
- Waypoint transition with look-ahead distance
- Steering damping for smooth turning
- Random initialization on road network
- Debug visualization (gizmos)

**NpcRecovery** - Autonomous recovery system:
- Detects off-road conditions (distance from road)
- Detects stuck conditions (low speed for extended time)
- Detects flipped vehicles (up vector check)
- Detects falling vehicles (high downward velocity)
- Scene boundary enforcement

**Recovery Process**:
1. Check if recovery conditions met
2. Find nearest road waypoint
3. Snap vehicle position to road
4. Reset velocity and rotation
5. Resume normal driving

**Recovery Cooldown**: 5 seconds between recovery attempts to prevent loops.

### 4. Player Progression System

**Location**: `Assets/Scripts/Quest/PlayerProgressionManager.cs`

**Tracked Metrics**:
- Current money (currency)
- Current level (1-50)
- Current XP and XP to next level
- Total quests completed
- Total distance traveled
- Total time played
- Unlocked achievements

**Level Progression**:
- XP Formula: `100 * level²` (exponential growth)
- XP overflow carries to next level
- Level up triggers UI effects and audio

**Reward System**:
```csharp
void AwardMoney(int amount) // Add currency
void AwardXP(int amount)    // Add experience
void LevelUp()              // Level progression
```

**Achievement System**:
- Predefined achievement list
- Tracks unlock status
- Awards bonus money on unlock
- Notifications on completion

**Daily Challenge System**:
- Resets at midnight (DateTime.Now.Date)
- 2x reward multiplier
- Higher difficulty than normal quests
- Tight time limit (0.8x normal)

### 5. Save/Load System

**Location**: `Assets/Scripts/Quest/SaveSystem/SaveManager.cs`

**Save Data Structure**:
```csharp
class GameSaveData {
    PlayerProgressionData playerData;
    QuestSaveData questData;
    string saveDate;
    int saveVersion; // For backwards compatibility
}
```

**Save Triggers**:
- Quest completion
- Application quit
- Manual save button
- Auto-save every 5 minutes (optional)

**Serialization**:
- Uses Unity JsonUtility for serialization
- Stores in `Application.persistentDataPath`
- File: `savegame.json`

**Save Location**:
- Windows: `C:/Users/[User]/AppData/LocalLow/[CompanyName]/[GameName]/`
- Uses persistent path for cross-platform compatibility

### 6. UI System

**Location**: `Assets/Scripts/Quest/UI/`

**UI Panels**:
1. **QuestListUI** - Available quests browser
   - Scrollable list with quest entries
   - Show/hide with Q key
   - Displays difficulty, type, reward, time limit

2. **ActiveQuestUI** - Current quest HUD
   - Top-right corner
   - Shows objective, timer, distance, cargo health
   - Color-coded timer (green → yellow → red)

3. **QuestCompleteUI** - Completion/failure popup
   - Center screen overlay
   - Shows statistics, rating, rewards
   - Continue button to dismiss

4. **ProgressionUI** - Money/Level/XP display
   - Top-right persistent display
   - Animated on changes
   - XP bar with smooth fill

5. **SaveLoadUI** - Save/Load controls
   - Manual save/load buttons
   - Last save time display
   - Save confirmation feedback

**Quest Markers**:
- 3D world-space markers at quest zones
- Pickup: Blue/Cyan color
- Delivery: Green/Yellow color
- Rotating and bobbing animation
- Particle effects for visibility

### 7. Audio System

**Location**: Integrated in `QuestManager.cs` (Task 10.1)

**Audio Sources**:
- `musicSource` - Background music with looping
- `questSfxSource` - One-shot sound effects

**Sound Effects**:
- Quest accepted (confirmation beep)
- Cargo pickup (loading sound)
- Cargo delivery (success chime)
- Cargo damage (crash/impact)
- Cargo destroyed (alarm)
- Quest failed (failure buzzer)
- Time warning (alarm at 30s)
- Level up (victory jingle)

**Background Music**:
- **Exploration Music**: Calm, ambient track during free roam
- **Delivery Music**: Intense, urgent track during active quest
- **Crossfade**: 2-second smooth transition between tracks

**Volume Control**:
```csharp
void SetMusicVolume(float volume) // 0-1 range
void SetSFXVolume(float volume)   // 0-1 range
```

### 8. Particle System

**Location**: Integrated in `QuestManager.cs` (Task 10.2)

**Particle Effects**:
- **Quest Marker Particles**: Rising glow at quest zones
- **Pickup Effect**: Burst when cargo loaded
- **Delivery Effect**: Celebration/confetti on completion
- **Damage Effect**: Sparks/smoke on collision
- **Level Up Effect**: Golden burst on level up

**Object Pooling**:
- Pre-instantiate particle systems on startup
- Reuse from pool instead of Instantiate/Destroy
- Automatic return to pool after particle lifetime
- Default pool size: 10 objects

**Performance**:
- Zero allocations during gameplay (pooling)
- Automatic cleanup after duration
- Configurable pool size for memory tuning

---

## Code Organization

### Directory Structure

```
Assets/
├── Scripts/
│   ├── CarController.cs              # Player vehicle physics
│   ├── CameraFollow.cs               # Third-person camera
│   ├── RoadGraphBuilder.cs           # Road network extraction
│   ├── RoadGraphTypes.cs             # Waypoint/Segment definitions
│   ├── NpcCarAgent.cs                # AI vehicle controller
│   ├── NpcRecovery.cs                # AI recovery system
│   ├── NpcSpawner.cs                 # Traffic spawning
│   │
│   ├── Quest/
│   │   ├── QuestManager.cs           # ⭐ Central quest controller
│   │   ├── QuestEnums.cs             # Quest type/status/difficulty enums
│   │   ├── QuestData.cs              # Quest data class
│   │   ├── QuestDatabase.cs          # ScriptableObject database
│   │   ├── QuestLocation.cs          # Location data class
│   │   ├── QuestZone.cs              # Trigger zone component
│   │   ├── QuestZoneType.cs          # Pickup/Delivery enum
│   │   ├── QuestMarker.cs            # 3D marker component
│   │   ├── QuestSaveData.cs          # Serializable quest data
│   │   │
│   │   ├── CargoData.cs              # Cargo properties
│   │   ├── CargoLibrary.cs           # ScriptableObject cargo database
│   │   ├── CargoVisual.cs            # Cargo 3D model controller
│   │   │
│   │   ├── PlayerProgressionManager.cs # XP/Level/Achievements
│   │   ├── Achievement.cs            # Achievement data class
│   │   │
│   │   ├── SaveSystem/
│   │   │   ├── SaveManager.cs        # Save/Load controller
│   │   │   └── SaveData.cs           # Root save data class
│   │   │
│   │   ├── UI/
│   │   │   ├── QuestListUI.cs        # Available quests panel
│   │   │   ├── QuestEntryUI.cs       # Quest entry prefab controller
│   │   │   ├── ActiveQuestUI.cs      # HUD quest display
│   │   │   ├── QuestCompleteUI.cs    # Completion popup
│   │   │   ├── ProgressionUI.cs      # Money/Level/XP display
│   │   │   ├── SaveLoadUI.cs         # Save/Load buttons
│   │   │   └── QuestUIManager.cs     # UI coordinator
│   │   │
│   │   └── Editor/
│   │       └── QuestZoneEditor.cs    # Custom inspector for zones
│   │
│   └── Editor/
│       ├── NpcPrefabCreator.cs       # Editor tool for NPC setup
│       ├── NpcPrefabFixer.cs         # Editor tool for NPC fixes
│       ├── RouteGizmoDrawer.cs       # Road visualization
│       └── RouteVisualizerEditor.cs  # Road debug tools
│
├── Prefabs/
│   ├── Quest/
│   │   ├── QuestMarkerPickup.prefab  # Blue pickup marker
│   │   ├── QuestMarkerDelivery.prefab# Green delivery marker
│   │   └── QuestZone.prefab          # Trigger zone template
│   │
│   ├── UI/
│   │   ├── QuestEntryPrefab.prefab   # Quest list entry
│   │   └── QuestUICanvas.prefab      # Complete UI hierarchy
│   │
│   ├── Effects/
│   │   ├── PickupParticles.prefab    # Cargo pickup effect
│   │   ├── DeliveryParticles.prefab  # Delivery success effect
│   │   ├── DamageParticles.prefab    # Cargo damage effect
│   │   └── LevelUpParticles.prefab   # Level up celebration
│   │
│   └── NPC/
│       ├── NpcCar_Sedan.prefab       # NPC vehicle prefab
│       └── ... (other vehicle types)
│
├── Resources/
│   ├── QuestDatabase.asset           # Main quest database
│   └── CargoLibrary.asset            # Cargo definitions
│
└── Scenes/
    └── SampleScene.unity             # Main game scene
```

### Naming Conventions

**Classes**:
- PascalCase: `QuestManager`, `CarController`
- Descriptive names: `PlayerProgressionManager` not `ProgMgr`

**Methods**:
- PascalCase: `GenerateQuest()`, `AcceptQuest()`
- Action verbs: `Calculate`, `Update`, `Generate`, `Spawn`

**Fields/Variables**:
- camelCase: `currentQuest`, `timeRemaining`
- Serialized fields: `[SerializeField] private GameObject prefab;`
- Constants: `UPPER_SNAKE_CASE` or `PascalCase`

**Events**:
- PascalCase with "On" prefix: `OnQuestStarted`, `OnLevelUp`

**Enums**:
- PascalCase enum name: `QuestType`, `QuestStatus`
- PascalCase values: `StandardDelivery`, `NotStarted`

**File Organization**:
- One class per file (except small nested classes)
- File name matches class name exactly
- Group related classes in folders

### Code Style Guidelines

**Regions**:
```csharp
#region Task 9.3: Multi-Stop Quest Generation
// Code here
#endregion
```

**XML Documentation**:
```csharp
/// <summary>
/// Brief description of what this method does
/// </summary>
/// <param name="parameter">Parameter description</param>
/// <returns>Return value description</returns>
public ReturnType MethodName(ParamType parameter)
```

**Unity Lifecycle Order**:
```csharp
// 1. Unity Messages
Awake()
Start()
Update()
FixedUpdate()
LateUpdate()
OnDestroy()

// 2. Public Methods
// 3. Private Methods
// 4. Coroutines
// 5. Event Handlers
```

**Null Checks**:
```csharp
// Preferred
if (obj == null) return;

// Also acceptable
if (obj != null) { }
```

---

## Development Guidelines

### Adding a New Quest Type

**Step 1**: Add enum value
```csharp
// In QuestEnums.cs
public enum QuestType
{
    // ... existing types
    NewQuestType // Add here
}
```

**Step 2**: Create generation method
```csharp
// In QuestManager.cs
public QuestData GenerateNewQuestType()
{
    // Pick cargo
    CargoData cargo = cargoLibrary.GetRandomCargo();

    // Generate locations
    QuestLocation pickup = GenerateRandomLocation("Pickup");
    QuestLocation delivery = GenerateRandomLocation("Delivery");

    // Create quest
    QuestData quest = new QuestData
    {
        QuestID = System.Guid.NewGuid().ToString(),
        QuestName = "NEW: Quest Name",
        QuestDescription = "Description here",
        QuestType = QuestType.NewQuestType,
        // ... set all properties
    };

    return quest;
}
```

**Step 3**: Add to weighted selection
```csharp
// In GenerateRandomQuestWithTypes()
float typeRoll = UnityEngine.Random.value;

if (typeRoll < 0.50f) return GenerateQuestByDifficulty(difficulty);
else if (typeRoll < 0.70f) return GenerateExpressDelivery();
// ... add your type here
else if (typeRoll < 0.90f) return GenerateNewQuestType(); // NEW
else return GenerateMultiStopQuest(2, difficulty);
```

**Step 4**: Update UI icons (optional)
- Create sprite for quest type icon
- Add to QuestEntryUI icon mapping

### Adding a New Cargo Type

**Step 1**: Create cargo in library
```csharp
// In Unity Inspector on CargoLibrary asset
// Or via code:
CargoData newCargo = new CargoData(
    "Cargo Name",
    weight: 150f,
    isFragile: false,
    description: "Description"
);
cargoLibrary.cargoTypes.Add(newCargo);
```

**Step 2**: (Optional) Add cargo model
- Import 3D model
- Set up materials
- Add to CargoVisual prefab variants

**Step 3**: (Optional) Create special behavior
```csharp
// In QuestManager or CargoData
if (cargo.CargoName == "Special Cargo")
{
    // Custom logic here
}
```

### Adding a New Achievement

**Step 1**: Define achievement
```csharp
// In PlayerProgressionManager.cs or separate file
Achievement newAchievement = new Achievement
{
    achievementID = "unique_id",
    name = "Achievement Name",
    description = "Complete X deliveries",
    icon = achievementSprite,
    rewardMoney = 500,
    isUnlocked = false
};
achievements.Add(newAchievement);
```

**Step 2**: Add check logic
```csharp
// In PlayerProgressionManager.CheckAchievements()
public void CheckAchievements()
{
    // Check "First Delivery"
    if (totalQuestsCompleted >= 1 && !IsAchievementUnlocked("first_delivery"))
    {
        UnlockAchievement("first_delivery");
    }

    // Add your achievement check here
    if (/* your condition */ && !IsAchievementUnlocked("unique_id"))
    {
        UnlockAchievement("unique_id");
    }
}
```

**Step 3**: Trigger check after quest completion
```csharp
// In QuestManager.CompleteQuest()
PlayerProgressionManager.Instance.CheckAchievements();
```

### Debugging Quest Generation

**Enable Debug Logging**:
```csharp
// In QuestManager.cs
Debug.Log($"[QuestManager] Generated quest: {quest.QuestName}");
Debug.Log($"  Distance: {distance:F0}m, Time: {quest.TimeLimit:F0}s");
Debug.Log($"  Pickup: {pickup.LocationName} at {pickup.Position}");
Debug.Log($"  Delivery: {delivery.LocationName} at {delivery.Position}");
```

**Visualize Waypoints**:
```csharp
// In RoadGraphBuilder
showWaypoints = true;
showConnections = true;
```

**Use Debug Menu** (Task 10.7):
```csharp
// Press F1 in Unity Editor
- Complete Current Quest
- Add Money
- Teleport to Pickup
- Infinite Time Mode
```

### Performance Optimization Tips

**1. Object Pooling**:
- Use for frequently spawned/destroyed objects
- Particles, projectiles, UI elements

**2. Distance Checks**:
```csharp
// Use sqrMagnitude instead of Distance
float sqrDist = (posA - posB).sqrMagnitude;
if (sqrDist < radius * radius) { } // No Sqrt!
```

**3. Update Optimization**:
```csharp
// Don't run every frame if not needed
float checkInterval = 0.5f;
float nextCheckTime = 0f;

void Update()
{
    if (Time.time >= nextCheckTime)
    {
        nextCheckTime = Time.time + checkInterval;
        PerformExpensiveCheck();
    }
}
```

**4. Cache Component References**:
```csharp
// Bad - GetComponent every frame
void Update()
{
    GetComponent<Rigidbody>().velocity = ...;
}

// Good - Cache in Awake/Start
private Rigidbody rb;
void Awake() { rb = GetComponent<Rigidbody>(); }
void Update() { rb.velocity = ...; }
```

**5. Avoid Allocations**:
```csharp
// Bad - allocates List every call
List<QuestData> GetQuests()
{
    return new List<QuestData> { quest1, quest2 };
}

// Good - reuse or return ReadOnlyCollection
private List<QuestData> questCache = new List<QuestData>();
IReadOnlyList<QuestData> GetQuests() => questCache;
```

---

## Technical Specifications

### Vehicle Physics

**WheelCollider Configuration**:
```
Mass: 1500 kg (car body Rigidbody)
Wheel Mass: 20 kg each
Suspension Distance: 0.2m
Suspension Spring: 35000 N/m
Suspension Damper: 4500 N
Friction:
  - Forward Stiffness: 1.0
  - Forward Asymptote Slip: 0.8
  - Forward Extremum Slip: 0.4
  - Sideways Stiffness: 1.0
  - Sideways Asymptote Slip: 0.5
  - Sideways Extremum Slip: 0.2
```

**Cargo Weight Effect**:
- Base mass: 1500 kg
- Cargo weight: 50-500 kg
- Applied as: `rb.mass = baseMass + cargoWeight`
- Center of mass raised slightly when cargo loaded

**Input Smoothing**:
- Steering: Lerp to target (smooth factor: 5.0)
- Throttle: Immediate response
- Brake: Immediate response

### Quest Generation Parameters

**Difficulty-Based Distances**:
```
Easy:   1000-2000m (1-2 km)
Medium: 2000-4000m (2-4 km)
Hard:   4000-6000m (4-6 km)
Expert: 6000-10000m (6-10 km)
```

**Time Calculation**:
```
Average Speed: 40 km/h = 11.11 m/s
Time = (Distance / AvgSpeed) * Multiplier

Multipliers:
  Easy:   2.0x (generous)
  Medium: 1.5x (moderate)
  Hard:   1.2x (tight)
  Expert: 1.0x (very tight)
```

**Reward Calculation**:
```
Base = (Distance * 0.1) + DifficultyBonus

Difficulty Bonuses:
  Easy:   $0
  Medium: $100
  Hard:   $250
  Expert: $500

Bonus = Base * 0.5 (awarded if 50%+ time remaining)

Special Quest Multipliers:
  Express:  2.0x base reward, 0.6x time
  Fragile:  1.0x base, +50% bonus for no damage
  Multi:    1.8x base per stop
  TimeTrial: 1.0x base, 100% bonus for fast time
```

**XP Rewards**:
```
Easy:   50 XP
Medium: 100 XP
Hard:   200 XP
Expert: 500 XP

Multi-Stop: Base XP * stopCount
```

**Quest Pool Management**:
```
Target Pool Size: 5 available quests
Auto-Refresh: Every 5 minutes (300s)
Manual Refresh Cooldown: 30 seconds
```

### Save System

**Save File Format**:
```json
{
  "playerData": {
    "money": 5000,
    "level": 5,
    "xp": 1200,
    "totalQuestsCompleted": 25,
    "totalDistanceTraveled": 50000.5,
    "unlockedAchievements": ["first_delivery", "speed_demon"]
  },
  "questData": {
    "activeQuests": [...],
    "availableQuests": [...],
    "completedQuestIDs": ["id1", "id2"],
    "currentQuestID": "id3"
  },
  "saveDate": "2026-01-27T15:30:00",
  "saveVersion": 1
}
```

**Save Location**:
- Path: `Application.persistentDataPath + "/savegame.json"`
- Windows: `C:/Users/[User]/AppData/LocalLow/[CompanyName]/Delivery Driver/`

**Auto-Save Triggers**:
- Quest completion
- OnApplicationQuit()
- Every 5 minutes (optional, can be enabled)

### Audio Specifications

**Music Tracks**:
- Format: Stereo, 44.1 kHz, OGG/MP3
- Loop: Seamless looping enabled
- Crossfade: 2-second fade-out/fade-in

**Sound Effects**:
- Format: Mono, 44.1 kHz, WAV
- One-shot playback
- Volume: Relative to SFX master volume

**Recommended Audio Lengths**:
- Quest Accept: 0.5-1.0s
- Pickup/Delivery: 1.0-2.0s
- Time Warning: 1.5-3.0s (looping alarm)
- Level Up: 2.0-4.0s (victory jingle)

### Particle Effect Specifications

**Quest Marker Particles**:
- Emission: 10-20 particles/second
- Lifetime: 2-3 seconds
- Start size: 0.2-0.5 units
- Start color: Cyan/Blue (pickup) or Green/Yellow (delivery)
- Blend mode: Additive
- Gravity: Disabled (rising effect)

**Pickup/Delivery Effects**:
- Emission: 30-50 burst particles
- Lifetime: 1.0-2.0 seconds
- Explosion radius: 2-3 units
- Color gradient: Bright → Fade out

**Damage Effect**:
- Emission: 10-15 burst particles
- Lifetime: 0.5-1.0 seconds
- Color: Orange/Red (sparks)
- Gravity: Enabled (falling sparks)

**Performance**:
- Max active particles per system: 50-100
- Pool size: 10 reusable particle systems
- Auto-cleanup after particle lifetime

---

## Integration Points

### Connecting QuestManager to Player

**Step 1**: Assign references in Inspector
```
QuestManager GameObject:
  - playerTransform: Drag player vehicle transform
  - playerController: Drag player CarController component
```

**Step 2**: Subscribe to quest events in player script (optional)
```csharp
void Start()
{
    QuestManager.Instance.OnQuestStarted.AddListener(OnQuestStarted);
    QuestManager.Instance.OnQuestCompleted.AddListener(OnQuestCompleted);
}

void OnQuestStarted(QuestData quest)
{
    Debug.Log("Player started quest: " + quest.QuestName);
}
```

### Connecting RoadGraph to QuestManager

**Step 1**: RoadGraphBuilder builds graph on Start()
```csharp
void Start()
{
    BuildRoadGraph(); // Generates waypoints from roads
}
```

**Step 2**: QuestManager auto-finds RoadGraphBuilder
```csharp
void Awake()
{
    if (roadGraphBuilder == null)
    {
        roadGraphBuilder = FindAnyObjectByType<RoadGraphBuilder>();
    }
}
```

**Step 3**: Use graph for location generation
```csharp
QuestLocation GenerateRandomLocation(string prefix)
{
    var result = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
    // ... use waypoint position
}
```

### Connecting SaveManager to QuestManager

**Auto-connection** (both singletons):
```csharp
// In QuestManager.Start()
if (SaveManager.Instance != null)
{
    GameSaveData saveData = SaveManager.Instance.LoadGame();
    if (saveData != null)
    {
        LoadSaveData(saveData.QuestData);
    }
}

// In QuestManager.CompleteQuest()
SaveManager.Instance?.TriggerAutoSave();
```

### Connecting UI to QuestManager

**QuestUIManager** acts as bridge:
```csharp
void Start()
{
    // Subscribe to quest events
    QuestManager.Instance.OnQuestStarted.AddListener(OnQuestStarted);
    QuestManager.Instance.OnQuestUpdated.AddListener(OnQuestUpdated);
    QuestManager.Instance.OnQuestCompleted.AddListener(OnQuestCompleted);
}

void OnQuestStarted(QuestData quest)
{
    questListUI.Hide();
    activeQuestUI.Show(quest);
}

void OnQuestUpdated(QuestData quest)
{
    activeQuestUI.UpdateDisplay(quest);
}
```

### Connecting Audio to Events

**Music transitions** (automatic in QuestManager):
```csharp
// On quest accept
AcceptQuest()
{
    // ... accept logic
    SwitchToDeliveryMusic(); // Automatically called
}

// On quest complete/fail
CompleteQuest() / FailQuest()
{
    // ... complete/fail logic
    SwitchToExplorationMusic(); // Automatically called
}
```

**Level up audio** (called from PlayerProgressionManager):
```csharp
void LevelUp()
{
    currentLevel++;
    // ...

    // Call QuestManager for audio/effects
    QuestManager.Instance?.PlayLevelUpSound();
    QuestManager.Instance?.PlayLevelUpEffect(playerPosition);
}
```

---

## Future Roadmap

### Phase 11: Advanced Features (Post-Launch)

**11.1 Minimap System**
- Real-time minimap camera
- Quest marker indicators
- Compass direction to objectives
- Zoom levels

**11.2 Vehicle Upgrades**
- Faster vehicles unlock at higher levels
- Better handling upgrades
- Increased cargo capacity
- Visual customization

**11.3 Weather System**
- Rain affects driving (reduced friction)
- Fog reduces visibility
- Day/night cycle
- Dynamic time limits based on weather

**11.4 Story Campaign**
- 20+ narrative-driven quests
- Character dialogues
- Unlock special vehicles/areas
- Boss deliveries

**11.5 Multiplayer**
- Leaderboards (fastest deliveries)
- Ghost racing (replay system)
- Cooperative multi-stop quests
- Competitive delivery races

### Phase 12: Content Expansion

**12.1 New Map Areas**
- Industrial district
- Rural countryside
- Mountain roads
- Coastal highway

**12.2 Special Events**
- Weekend challenges (3x rewards)
- Seasonal events (holidays)
- Time-limited vehicles
- Special cargo types

**12.3 NPC Interactions**
- Random encounters on road
- Passenger pickup missions
- Emergency deliveries
- Roadside assistance

### Phase 13: Polish & Optimization

**13.1 Mobile Port**
- Touch controls
- Simplified UI for smaller screens
- Performance optimization for mobile GPUs
- Cloud save integration

**13.2 Advanced Analytics**
- Heatmaps of player routes
- Quest completion statistics
- Difficulty balancing based on data
- A/B testing for quest rewards

**13.3 Modding Support**
- Custom quest editor
- Vehicle import tools
- Map editor
- Steam Workshop integration

---

## Common Tasks & Patterns

### Task: Add a New Manager Singleton

```csharp
public class NewManager : MonoBehaviour
{
    public static NewManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Optional: persist across scenes
        // DontDestroyOnLoad(gameObject);
    }

    // Your manager methods here
}
```

### Task: Create a ScriptableObject Database

```csharp
[CreateAssetMenu(fileName = "NewDatabase", menuName = "Game/New Database")]
public class NewDatabase : ScriptableObject
{
    public List<DataType> items = new List<DataType>();

    public DataType GetRandomItem()
    {
        if (items.Count == 0) return null;
        return items[Random.Range(0, items.Count)];
    }

    public DataType GetItemByID(string id)
    {
        return items.Find(item => item.id == id);
    }
}
```

### Task: Implement Object Pooling

```csharp
public class ObjectPool
{
    private Queue<GameObject> pool = new Queue<GameObject>();
    private GameObject prefab;
    private int size;

    public ObjectPool(GameObject prefab, int size)
    {
        this.prefab = prefab;
        this.size = size;

        for (int i = 0; i < size; i++)
        {
            GameObject obj = Object.Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject Get(Vector3 position)
    {
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            obj = Object.Instantiate(prefab);
        }

        obj.transform.position = position;
        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

### Task: Add a Unity Event

```csharp
// In the class that fires the event
public UnityEvent<DataType> OnEventName = new UnityEvent<DataType>();

// Fire the event
void SomeMethod()
{
    OnEventName.Invoke(data);
}

// In another class, subscribe
void Start()
{
    OtherClass.Instance.OnEventName.AddListener(HandleEvent);
}

void HandleEvent(DataType data)
{
    Debug.Log("Event received: " + data);
}

void OnDestroy()
{
    // Always unsubscribe to prevent memory leaks
    OtherClass.Instance.OnEventName.RemoveListener(HandleEvent);
}
```

### Task: Implement a Coroutine Timer

```csharp
private IEnumerator CountdownTimer(float duration)
{
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float remaining = duration - elapsed;

        // Update UI or logic here
        UpdateTimerDisplay(remaining);

        yield return null; // Wait one frame
    }

    // Timer expired
    OnTimerExpired();
}

// Start the coroutine
void StartTimer()
{
    StartCoroutine(CountdownTimer(300f)); // 5 minutes
}
```

### Task: Save/Load Custom Data

```csharp
[System.Serializable]
public class CustomSaveData
{
    public int value;
    public string name;
    public List<int> items;
}

// Save
void SaveData()
{
    CustomSaveData data = new CustomSaveData
    {
        value = 100,
        name = "Test",
        items = new List<int> { 1, 2, 3 }
    };

    string json = JsonUtility.ToJson(data, true);
    string path = Application.persistentDataPath + "/custom.json";
    File.WriteAllText(path, json);
}

// Load
CustomSaveData LoadData()
{
    string path = Application.persistentDataPath + "/custom.json";

    if (!File.Exists(path))
        return null;

    string json = File.ReadAllText(path);
    return JsonUtility.FromJson<CustomSaveData>(json);
}
```

---

## Troubleshooting

### Issue: Quest Markers Not Appearing

**Symptoms**: Quest zones spawn but no visual markers visible

**Causes**:
1. Marker prefabs not assigned in Inspector
2. Markers spawning underground
3. Marker materials not rendering

**Solutions**:
```csharp
// Check in QuestManager Inspector:
- pickupMarkerPrefab: Assigned?
- deliveryMarkerPrefab: Assigned?

// Check marker position (add debug)
Debug.Log("Marker spawned at: " + location.Position);

// Ensure prefab has:
- MeshRenderer with material
- Proper layer (not on "Ignore Raycast")
- Scale > 0
```

### Issue: NPCs Driving Off Road

**Symptoms**: NPC vehicles leave waypoints, drive into buildings

**Causes**:
1. RoadGraph not built correctly
2. Waypoint spacing too large
3. Steering parameters too weak

**Solutions**:
```csharp
// Check RoadGraphBuilder:
- sampleStepMeters: Try reducing to 3m (from 5m)
- showWaypoints: Enable to visualize waypoints

// Check NpcCarAgent:
- steerSmoothSpeed: Increase for tighter steering
- targetWaypointDistance: Reduce look-ahead distance
```

### Issue: Quest Timer Not Counting Down

**Symptoms**: Time remaining stays constant

**Causes**:
1. Quest not in Active status
2. UpdateTimer not being called
3. Time.deltaTime is 0 (paused game)

**Solutions**:
```csharp
// Check quest status
Debug.Log("Quest status: " + currentQuest.Status);

// Check Update() is running
Debug.Log("Update called: " + Time.frameCount);

// Verify timer update
void Update()
{
    if (currentQuest != null)
    {
        Debug.Log($"Time: {currentQuest.TimeRemaining}");
        currentQuest.UpdateTimer(Time.deltaTime);
    }
}
```

### Issue: Save File Not Loading

**Symptoms**: Game starts fresh every time, ignoring save file

**Causes**:
1. Save file doesn't exist
2. JSON deserialization fails
3. SaveManager not in scene

**Solutions**:
```csharp
// Check file exists
string path = Application.persistentDataPath + "/savegame.json";
Debug.Log("Save exists: " + File.Exists(path));
Debug.Log("Path: " + path);

// Check SaveManager singleton
Debug.Log("SaveManager: " + (SaveManager.Instance != null));

// Add try-catch for deserialization errors
try
{
    string json = File.ReadAllText(path);
    GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
}
catch (Exception e)
{
    Debug.LogError("Failed to load save: " + e.Message);
}
```

### Issue: Cargo Not Affecting Vehicle

**Symptoms**: Adding cargo doesn't change vehicle handling

**Causes**:
1. CarController doesn't have AddCargoWeight method
2. Rigidbody mass not updating
3. Method not being called

**Solutions**:
```csharp
// Check CarController has method
public void AddCargoWeight(float weight)
{
    Rigidbody rb = GetComponent<Rigidbody>();
    rb.mass += weight;
    Debug.Log($"Added {weight}kg, new mass: {rb.mass}");
}

// Check QuestManager calls it
void OnCargoPickedUp()
{
    // ...
    playerController.AddCargoWeight(currentQuest.Cargo.Weight);
}
```

### Issue: Music Not Playing

**Symptoms**: No background music during gameplay

**Causes**:
1. AudioSource not assigned
2. Audio clips not assigned
3. Volume set to 0
4. AudioListener missing from scene

**Solutions**:
```csharp
// Check QuestManager Inspector:
- musicSource: Assigned?
- explorationMusicClip: Assigned?
- deliveryMusicClip: Assigned?

// Check at runtime
Debug.Log("Music playing: " + musicSource.isPlaying);
Debug.Log("Volume: " + musicSource.volume);

// Ensure scene has AudioListener on camera
Camera.main.GetComponent<AudioListener>() != null
```

### Performance Issues

**Symptoms**: Low FPS, stuttering, long load times

**Diagnostics**:
1. Open Unity Profiler (Window > Analysis > Profiler)
2. Identify bottleneck (CPU/GPU/Memory)
3. Check specific systems:
   - Too many active NPCs?
   - Particle systems not pooled?
   - Update() methods doing heavy work?

**Common Fixes**:
```csharp
// Reduce NPC count
[SerializeField] private int maxNPCs = 10; // Lower this

// Increase update intervals
float checkInterval = 1.0f; // Check less frequently

// Use object pooling for particles
// (Already implemented in QuestManager Task 10.2)

// Cache component references
// Don't: GetComponent<T>() in Update()
// Do: Cache in Awake(), reuse cached reference
```

---

## Quick Reference

### Key Files to Modify for Common Changes

| What to Change | File to Edit | Notes |
|----------------|--------------|-------|
| Quest difficulty/rewards | `QuestManager.cs` → `GenerateQuestByDifficulty()` | Distance, time, reward formulas |
| Cargo types | `CargoLibrary.asset` (Inspector) | Add new cargo definitions |
| Quest types | `QuestEnums.cs` + `QuestManager.cs` | Add enum, then generation method |
| Player XP/leveling | `PlayerProgressionManager.cs` → `CalculateXPForLevel()` | XP formula, level caps |
| Achievement conditions | `PlayerProgressionManager.cs` → `CheckAchievements()` | Unlock logic |
| Vehicle handling | `CarController.cs` | Physics parameters, input response |
| NPC behavior | `NpcCarAgent.cs` | Steering, speed, waypoint following |
| Save data structure | `SaveData.cs` + `SaveManager.cs` | Serializable classes |
| UI layouts | Prefabs in `Assets/Prefabs/UI/` | Unity UI components |
| Audio clips | `QuestManager` Inspector | Assign audio files |
| Particle effects | `QuestManager` Inspector | Assign particle prefabs |

### Important Unity Inspector References

**QuestManager (Required)**:
- questDatabase (ScriptableObject)
- cargoLibrary (ScriptableObject)
- roadGraphBuilder (scene reference)
- playerTransform (player vehicle)
- playerController (CarController component)
- pickupMarkerPrefab (prefab)
- deliveryMarkerPrefab (prefab)
- questZonePrefab (prefab)
- Audio clips (all SFX and music)
- Particle prefabs (all effects)

**RoadGraphBuilder (Required)**:
- autoDetectRoads: true
- sampleStepMeters: 5.0
- connectionThresholdMeters: 3.0

**PlayerProgressionManager (Required)**:
- startingMoney: 500
- startingLevel: 1

### Git Workflow

**Branch Strategy**:
- `main` - Stable, production-ready code
- `feature/quest-system` - Quest system development (current)
- `feature/[feature-name]` - New features
- `bugfix/[issue]` - Bug fixes

**Commit Message Format**:
```
feat: brief description (50 chars)

Detailed explanation of changes:
- What was added/changed
- Why it was necessary
- How it works

Implements Task X.Y from implementation guide.
```

**Example**:
```
feat: implement multi-stop quest generation

- Add GenerateMultiStopQuest() with route optimization
- Use nearest-neighbor algorithm for stop ordering
- Scale rewards and time limits appropriately

Implements Task 9.3 from QUEST_SYSTEM_IMPLEMENTATION_GUIDE.md
```

### Testing Checklist

Before committing major changes, verify:

- [ ] No compilation errors
- [ ] No console errors during runtime
- [ ] Quest generation works (generate 10 quests)
- [ ] Quest acceptance works
- [ ] Pickup/delivery triggers work
- [ ] Quest completion rewards player
- [ ] Quest failure works
- [ ] Save/load preserves state
- [ ] UI updates correctly
- [ ] Audio plays at appropriate times
- [ ] Particle effects spawn correctly
- [ ] NPC vehicles don't get stuck
- [ ] Performance is acceptable (60 FPS)
- [ ] Code follows style guidelines
- [ ] Public methods have XML documentation
- [ ] Debug logs removed (unless intentional)

---

## Contact & Resources

### Documentation Files
- **This Guide**: `AGENT_GUIDE.md` - Comprehensive development reference
- **Implementation Guide**: `QUEST_SYSTEM_IMPLEMENTATION_GUIDE.md` - Phase-by-phase checklist
- **Unity Assets**: Search Unity Asset Store for "delivery game", "quest system", "vehicle physics"

### Useful Unity Documentation
- [WheelCollider Reference](https://docs.unity3d.com/Manual/class-WheelCollider.html)
- [New Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html)
- [ScriptableObjects](https://docs.unity3d.com/Manual/class-ScriptableObject.html)
- [Unity Events](https://docs.unity3d.com/Manual/UnityEvents.html)
- [JsonUtility](https://docs.unity3d.com/ScriptReference/JsonUtility.html)

### Community Resources
- Unity Forums: https://forum.unity.com/
- Unity Discord: https://discord.gg/unity
- r/Unity3D: https://reddit.com/r/Unity3D

---

## Conclusion

This guide covers everything needed to understand, maintain, and extend the Delivery Driver project. The quest system is modular and extensible, with clear separation of concerns and well-documented code.

**Key Takeaways**:
1. Quest system is data-driven using ScriptableObjects
2. All major systems use singleton pattern for easy access
3. Event system decouples UI from game logic
4. Object pooling is used for performance
5. Save system preserves complete game state
6. Code is organized by feature/system
7. Extensive comments and XML documentation

**Next Steps**:
1. Complete remaining Phase 10 tasks (minimap, tutorial, settings)
2. Playtest and balance quest rewards/difficulty
3. Optimize performance for target hardware
4. Add polish (animations, effects, juice)
5. Implement analytics for post-launch tuning

Good luck with development! 🚚📦
