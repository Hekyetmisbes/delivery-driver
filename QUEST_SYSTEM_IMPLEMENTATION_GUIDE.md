# Quest System Implementation Guide
## Delivery Driver - Cargo Transport Mission System

---

## Table of Contents
1. [System Overview](#system-overview)
2. [Phase 1: Core Data Structures](#phase-1-core-data-structures)
3. [Phase 2: Quest Manager](#phase-2-quest-manager)
4. [Phase 3: UI System](#phase-3-ui-system)
5. [Phase 4: Quest Zones & Triggers](#phase-4-quest-zones--triggers)
6. [Phase 5: Cargo System](#phase-5-cargo-system)
7. [Phase 6: Timer & Scoring](#phase-6-timer--scoring)
8. [Phase 7: Rewards & Progression](#phase-7-rewards--progression)
9. [Phase 8: Save/Load System](#phase-8-saveload-system)
10. [Phase 9: Quest Generation](#phase-9-quest-generation)
11. [Phase 10: Polish & Integration](#phase-10-polish--integration)

---

## System Overview

### Core Features
- **Delivery Quests**: Transport cargo from pickup to delivery locations
- **Time-Based Challenges**: Complete deliveries within time limits
- **Dynamic Difficulty**: Quest difficulty scales with distance and time pressure
- **Reward System**: Earn currency/points for completed deliveries
- **Quest Progression**: Unlock harder deliveries as you progress
- **Multiple Quest Types**: Standard delivery, express delivery, fragile cargo, multi-stop routes

### Architecture Integration Points
- **RoadGraphBuilder**: Use existing road network for quest location placement
- **CarController**: Track player vehicle for delivery detection
- **NPC System**: Optional interaction with traffic (avoid collisions for bonus)
- **UI Layer**: New Canvas-based UI for quest display

---

## Phase 1: Core Data Structures

### Task 1.1: Create Quest Data Enums
- [x] Create `Assets/Scripts/Quest/QuestEnums.cs`
- [x] Define `QuestType` enum:
  - `StandardDelivery` - Basic delivery with normal time limit
  - `ExpressDelivery` - Tight time limit, higher reward
  - `FragileDelivery` - Damage penalty for collisions
  - `MultiStopDelivery` - Multiple pickup/delivery locations
  - `TimeTrial` - Fastest delivery wins bonus
- [x] Define `QuestStatus` enum:
  - `NotStarted` - Quest available but not accepted
  - `Active` - Quest accepted and in progress
  - `Completed` - Successfully completed
  - `Failed` - Time ran out or cargo destroyed
  - `Expired` - Quest no longer available
- [x] Define `QuestDifficulty` enum:
  - `Easy` - Short distance, generous time (1-2 km, 3-5 min)
  - `Medium` - Medium distance, moderate time (2-4 km, 4-7 min)
  - `Hard` - Long distance, tight time (4-6 km, 6-10 min)
  - `Expert` - Very long, very tight (6+ km, 8-12 min)

**Implementation Notes:**
```csharp
// File: Assets/Scripts/Quest/QuestEnums.cs
namespace DeliveryDriver.Quest
{
    public enum QuestType { /* ... */ }
    public enum QuestStatus { /* ... */ }
    public enum QuestDifficulty { /* ... */ }
}
```

---

### Task 1.2: Create Quest Location Data Structure
- [x] Create `Assets/Scripts/Quest/QuestLocation.cs`
- [x] Add `QuestLocation` class with properties:
  - `Vector3 Position` - World position
  - `string LocationName` - Display name (e.g., "Downtown Warehouse")
  - `int RoadSegmentIndex` - Reference to road segment
  - `int WaypointIndex` - Reference to specific waypoint
  - `float TriggerRadius` - Detection radius (default: 10m)
  - `GameObject VisualMarker` - Optional 3D marker prefab reference
- [x] Add constructor: `QuestLocation(Vector3 pos, string name, float radius)`
- [x] Add method: `bool IsPlayerInRange(Transform playerTransform)`
- [x] Add method: `void ShowMarker()` / `void HideMarker()`

**Implementation Details:**
- Use `Vector3.Distance()` for range checking
- Markers should be simple colored columns/beacons visible from distance
- Store reference to instantiated marker GameObject for enable/disable

---

### Task 1.3: Create Cargo Data Structure
- [x] Create `Assets/Scripts/Quest/CargoData.cs`
- [x] Add `CargoData` class with properties:
  - `string CargoName` - Display name (e.g., "Medical Supplies", "Electronics")
  - `float Weight` - Affects vehicle handling (0-500 kg)
  - `bool IsFragile` - If true, collisions reduce cargo health
  - `float CargoHealth` - Current health (0-100, only for fragile)
  - `Sprite CargoIcon` - UI display icon
  - `string Description` - Flavor text
- [x] Add method: `void TakeDamage(float amount)` - Reduces health
- [x] Add method: `bool IsDestroyed()` - Returns true if health <= 0

**Cargo Types to Support:**
1. Standard Boxes (not fragile)
2. Fragile Electronics (high damage penalty)
3. Medical Supplies (time-critical)
4. Heavy Machinery (affects vehicle speed)

---

### Task 1.4: Create Quest Data Structure
- [x] Create `Assets/Scripts/Quest/QuestData.cs`
- [x] Add `QuestData` class with properties:
  - `string QuestID` - Unique identifier (GUID)
  - `string QuestName` - Display name
  - `string QuestDescription` - Full description
  - `QuestType QuestType` - Type of quest
  - `QuestDifficulty Difficulty` - Difficulty level
  - `QuestStatus Status` - Current status
  - `QuestLocation PickupLocation` - Where to get cargo
  - `List<QuestLocation> DeliveryLocations` - Where to deliver (supports multi-stop)
  - `CargoData Cargo` - What to transport
  - `float TimeLimit` - Seconds allowed
  - `float TimeRemaining` - Current countdown
  - `int BaseReward` - Currency earned
  - `int BonusReward` - Extra for fast completion
  - `float BonusTimeThreshold` - Time under this = bonus (e.g., 50% of limit)
  - `int RequiredLevel` - Player level requirement
  - `bool IsRepeatable` - Can be done multiple times
- [x] Add constructor with default initialization
- [x] Add method: `void StartQuest()` - Sets status to Active, starts timer
- [x] Add method: `void UpdateTimer(float deltaTime)` - Decrements time
- [x] Add method: `bool IsTimeExpired()` - Returns true if time <= 0
- [x] Add method: `int CalculateFinalReward()` - Base + bonus logic

**Reward Calculation Logic:**
```csharp
if (TimeRemaining > TimeLimit * BonusTimeThreshold)
    return BaseReward + BonusReward;
else
    return BaseReward;
```

---

### Task 1.5: Create Quest Database ScriptableObject
- [x] Create `Assets/Scripts/Quest/QuestDatabase.cs`
- [x] Add `QuestDatabase` ScriptableObject with:
  - `List<QuestTemplate> AvailableQuests` - All quest templates
  - Method: `QuestData GetQuestByID(string id)`
  - Method: `List<QuestData> GetQuestsByDifficulty(QuestDifficulty diff)`
  - Method: `QuestData GenerateRandomQuest(QuestDifficulty diff)`
- [x] Create `QuestTemplate` inner class:
  - Similar to QuestData but without runtime status
  - Used as blueprint for generating QuestData instances
- [x] Create menu item: `Assets/Create/Quest System/Quest Database`
- [x] Create first database instance: `Assets/Resources/QuestDatabase.asset`

**Template Examples to Pre-configure:**
1. "Downtown Express" (Easy, Standard Delivery)
2. "Medical Emergency" (Medium, Express + Fragile)
3. "Cross-City Haul" (Hard, Multi-Stop)
4. "VIP Package" (Expert, Express + Fragile + High Reward)

---

## Phase 2: Quest Manager

### Task 2.1: Create Quest Manager Singleton
- [x] Create `Assets/Scripts/Quest/QuestManager.cs`
- [x] Implement Singleton pattern with `Instance` property
- [x] Add fields:
  - `QuestDatabase questDatabase` - Reference to database
  - `List<QuestData> activeQuests` - Currently active quests (max 3-5)
  - `List<QuestData> availableQuests` - Quests player can accept
  - `List<QuestData> completedQuests` - History
  - `QuestData currentQuest` - The quest player is currently doing
  - `Transform playerTransform` - Reference to player vehicle
  - `CarController playerController` - Reference to player car
- [ ] Add Unity events:
  - `UnityEvent<QuestData> OnQuestStarted`
  - `UnityEvent<QuestData> OnQuestCompleted`
  - `UnityEvent<QuestData> OnQuestFailed`
  - `UnityEvent<QuestData> OnQuestUpdated`

**Singleton Pattern:**
```csharp
public static QuestManager Instance { get; private set; }
void Awake()
{
    if (Instance != null && Instance != this)
        Destroy(gameObject);
    else
        Instance = this;
}
```

---

### Task 2.2: Implement Quest Lifecycle Methods
- [x] Add `void GenerateAvailableQuests(int count)`:
  - Generate `count` random quests from database
  - Use RoadGraph to pick valid locations on road network
  - Add to `availableQuests` list
  - Ensure variety (different difficulties/types)
- [x] Add `bool AcceptQuest(string questID)`:
  - Move quest from available to active
  - Set as `currentQuest`
  - Call `quest.StartQuest()`
  - Spawn pickup location marker
  - Invoke `OnQuestStarted` event
  - Return false if quest not found/already active
- [x] Add `void AbandonQuest(string questID)`:
  - Remove from active quests
  - Clear current quest if it matches
  - Clean up markers
  - Return quest to available (if repeatable)
- [x] Add `void CompleteQuest(QuestData quest)`:
  - Add to completed list
  - Award rewards (call PlayerProgressionManager)
  - Invoke `OnQuestCompleted` event
  - Clean up markers and references
  - Generate new available quest
- [x] Add `void FailQuest(QuestData quest, string reason)`:
  - Mark quest as Failed
  - Invoke `OnQuestFailed` event
  - Show failure UI message
  - Clean up and generate replacement

---

### Task 2.3: Implement Quest Update Loop
- [x] Override `void Update()`:
  - Early exit if `currentQuest == null`
  - Update quest timer: `currentQuest.UpdateTimer(Time.deltaTime)`
  - Check time expiration: if `currentQuest.IsTimeExpired()` → `FailQuest()`
  - Check pickup location proximity (if not picked up yet)
  - Check delivery location proximity (if cargo picked up)
  - Update fragile cargo health based on collision events
  - Invoke `OnQuestUpdated` event for UI refresh
- [x] Add `void CheckPickupProximity()`:
  - If player in pickup radius and hasn't picked up cargo yet
  - Call `OnCargoPickedUp()`
  - Show delivery marker
  - Hide pickup marker
- [x] Add `void CheckDeliveryProximity()`:
  - If player in delivery radius and has cargo
  - For multi-stop: check current delivery location, then move to next
  - If last delivery → `CompleteQuest()`
- [x] Add collision detection hook for fragile cargo (see Phase 5)

---

### Task 2.4: Implement Location Generation System
- [x] Add `QuestLocation GenerateRandomLocation(string prefix)`:
  - Use `RoadGraphBuilder.Instance.RoadGraph.GetRandomWaypoint()`
  - Convert waypoint to QuestLocation
  - Generate location name: `prefix + Random area name`
  - Set appropriate trigger radius (10-15m)
  - Return QuestLocation instance
- [x] Add `bool AreLocationsValid(QuestLocation pickup, QuestLocation delivery)`:
  - Calculate distance between locations
  - Ensure minimum distance (500m for Easy, 1km+ for Medium/Hard)
  - Ensure both are on valid road segments
  - Return true if valid placement
- [x] Add location name generator:
  - Create string arrays: `["North", "South", "East", "West", "Central"]`
  - `["Warehouse", "Depot", "Station", "Hub", "Terminal"]`
  - Combine randomly: "North Warehouse", "Central Station", etc.

**Distance Calculation:**
```csharp
float distance = Vector3.Distance(pickup.Position, delivery.Position);
if (difficulty == QuestDifficulty.Easy && distance < 500f) return false;
// etc...
```

---

### Task 2.5: Implement Quest State Persistence Hooks
- [x] Add `QuestSaveData GetSaveData()`:
  - Convert active/available/completed quests to serializable format
  - Store current quest progress (time remaining, cargo health, etc.)
  - Return `QuestSaveData` object
- [x] Add `void LoadSaveData(QuestSaveData data)`:
  - Reconstruct quest lists from saved data
  - Restore quest markers
  - Resume current quest if exists
  - Invoke appropriate events
- [x] Create `QuestSaveData` serializable class in separate file (see Phase 8)

---

## Phase 3: UI System

### Task 3.1: Create Quest UI Canvas Structure
- [x] Create Canvas GameObject in scene: `QuestUICanvas`
- [x] Set Canvas properties:
  - Render Mode: Screen Space - Overlay
  - Canvas Scaler: Scale With Screen Size (1920x1080 reference)
  - Pixel Perfect: Disabled
- [x] Add EventSystem if not present
- [x] Create main UI panels as children:
  - `QuestListPanel` (shows available quests)
  - `ActiveQuestPanel` (shows current quest info)
  - `QuestCompletePanel` (completion/failure popup)
  - `QuestMarkerContainer` (world-space markers)

**Hierarchy Structure:**
```
QuestUICanvas
├── QuestListPanel (left side, can be toggled)
├── ActiveQuestPanel (top-right, always visible)
├── QuestCompletePanel (center, popup)
└── QuestMarkerContainer (empty, for dynamic markers)
```

---

### Task 3.2: Create Quest List Panel UI
- [x] Create `Assets/Scripts/Quest/UI/QuestListUI.cs`
- [x] Design panel layout (anchored left side):
  - Header: "Available Deliveries"
  - Scroll View with Vertical Layout Group
  - Quest entry prefab template (see next task)
  - Toggle button to show/hide panel (Q key)
- [x] Add script fields:
  - `GameObject questEntryPrefab` - Prefab for each quest entry
  - `Transform questEntriesContainer` - Parent for spawned entries
  - `Button closeButton`
  - `Animator panelAnimator` (for slide in/out)
- [x] Implement methods:
  - `void PopulateQuestList(List<QuestData> quests)` - Create entry for each quest
  - `void ClearQuestList()` - Destroy all entries
  - `void TogglePanel()` - Show/hide with animation
  - `void OnQuestEntryClicked(QuestData quest)` - Accept quest handler

**Panel Design Notes:**
- Semi-transparent dark background (alpha ~0.8)
- Slide animation from left edge (250ms ease-out)
- Max height: 80% of screen, scrollable if needed

---

### Task 3.3: Create Quest Entry Prefab
- [ ] Create prefab: `Assets/Prefabs/UI/QuestEntryPrefab.prefab`
- [ ] Layout (using Horizontal/Vertical Layout Groups):
  - Difficulty indicator (colored bar: green/yellow/orange/red)
  - Quest name (bold TextMeshPro)
  - Quest type icon (sprite: box/clock/fragile icons)
  - Distance info ("2.3 km")
  - Time limit ("5:00")
  - Reward amount ("$500")
  - Accept button
- [ ] Create `Assets/Scripts/Quest/UI/QuestEntryUI.cs`:
  - `TextMeshProUGUI questNameText`
  - `TextMeshProUGUI distanceText`
  - `TextMeshProUGUI timeText`
  - `TextMeshProUGUI rewardText`
  - `Image difficultyBar`
  - `Image typeIcon`
  - `Button acceptButton`
  - `QuestData questData` - Reference to represented quest
- [ ] Implement `void Initialize(QuestData quest)`:
  - Populate all UI fields from quest data
  - Set difficulty bar color based on difficulty
  - Set type icon based on quest type
  - Add button listener to call `QuestManager.Instance.AcceptQuest()`

**Color Scheme:**
- Easy: Green (#4CAF50)
- Medium: Yellow (#FFC107)
- Hard: Orange (#FF9800)
- Expert: Red (#F44336)

---

### Task 3.4: Create Active Quest Panel UI
- [ ] Create panel anchored to top-right corner
- [ ] Layout components:
  - Quest objective text (e.g., "Deliver to Central Station")
  - Progress indicator:
    - If not picked up: "Go to pickup location"
    - If picked up: "Deliver cargo (X of Y)" for multi-stop
  - Timer display (MM:SS format, color-coded by urgency)
  - Distance to next objective
  - Cargo health bar (only for fragile cargo)
  - Mini-map/compass indicator (optional, advanced)
- [ ] Create `Assets/Scripts/Quest/UI/ActiveQuestUI.cs`:
  - `TextMeshProUGUI objectiveText`
  - `TextMeshProUGUI timerText`
  - `TextMeshProUGUI distanceText`
  - `Image cargoHealthFill`
  - `GameObject cargoHealthPanel` (hidden if not fragile)
  - `QuestData currentQuest` - Reference
- [ ] Implement methods:
  - `void UpdateQuestDisplay(QuestData quest)` - Refresh all fields
  - `void UpdateTimer(float timeRemaining, float timeLimit)` - Format and color timer
  - `void UpdateDistance(float distance)` - Show distance to objective
  - `void UpdateCargoHealth(float health)` - Update health bar fill
  - `void Hide()` / `void Show()` - Toggle visibility

**Timer Color Logic:**
```csharp
if (timeRemaining > timeLimit * 0.5f) color = Color.green;
else if (timeRemaining > timeLimit * 0.25f) color = Color.yellow;
else color = Color.red; // Urgent!
```

---

### Task 3.5: Create Quest Complete/Failed Popup
- [ ] Create centered popup panel: `QuestCompletePanel`
- [ ] Layout (vertical):
  - Large result text ("DELIVERY COMPLETE" or "DELIVERY FAILED")
  - Quest name
  - Statistics section:
    - Time taken / Time limit
    - Distance traveled (optional)
    - Cargo condition (if fragile)
  - Reward earned ("+ $500" with bonus indicator)
  - Continue button ("OK" / "Next Delivery")
- [ ] Create `Assets/Scripts/Quest/UI/QuestCompleteUI.cs`:
  - `GameObject completedPanel` / `GameObject failedPanel`
  - `TextMeshProUGUI resultText`
  - `TextMeshProUGUI questNameText`
  - `TextMeshProUGUI statsText`
  - `TextMeshProUGUI rewardText`
  - `Button continueButton`
  - `AudioSource successSound` / `AudioSource failureSound`
- [ ] Implement methods:
  - `void ShowCompleteScreen(QuestData quest, int reward)`
  - `void ShowFailedScreen(QuestData quest, string reason)`
  - `void Hide()`
- [ ] Add button listener to close popup and refresh available quests

**Popup Animation:**
- Fade in background overlay (black, alpha 0.7)
- Scale popup from 0 to 1 (0.3s, ease-out-back)
- Play success/failure sound effect

---

### Task 3.6: Create World-Space Quest Markers
- [ ] Create prefab: `Assets/Prefabs/Quest/QuestMarkerPickup.prefab`
  - Cylinder or beacon model (colored, emissive material)
  - Particle effect (glowing particles rising)
  - Rotating animation (slow Y-axis rotation)
  - Optional: Vertical oscillating animation (bob up/down)
  - Scale: ~3m tall, visible from distance
  - Color: Blue/Cyan for pickup
- [ ] Create prefab: `Assets/Prefabs/Quest/QuestMarkerDelivery.prefab`
  - Similar to pickup but different color (Green/Yellow)
  - Larger particle effect
- [ ] Create `Assets/Scripts/Quest/QuestMarker.cs`:
  - `Transform target` - Position to mark
  - `float bobSpeed` - Oscillation speed
  - `float bobHeight` - Oscillation amplitude
  - `float rotationSpeed` - Y-axis rotation speed
  - `ParticleSystem particles`
- [ ] Implement `Update()`:
  - Rotate around Y-axis
  - Bob up/down using `Mathf.Sin(Time.time)`
- [ ] Add minimap icon support (optional, advanced):
  - UI Image that tracks world position
  - Distance indicator

**Shader/Material Notes:**
- Use Unlit or Emission shader for always-visible effect
- Bright emissive colors (HDR values)
- Additive blend mode for particles

---

### Task 3.7: Create Quest Manager UI Controller
- [ ] Create `Assets/Scripts/Quest/UI/QuestUIManager.cs`
- [ ] Add singleton pattern
- [ ] Add references to all UI panels:
  - `QuestListUI questListUI`
  - `ActiveQuestUI activeQuestUI`
  - `QuestCompleteUI questCompleteUI`
- [ ] Subscribe to QuestManager events in `Start()`:
  - `OnQuestStarted` → Hide quest list, show active quest panel
  - `OnQuestCompleted` → Show complete screen, award rewards
  - `OnQuestFailed` → Show failed screen
  - `OnQuestUpdated` → Refresh active quest display
- [ ] Implement `void Update()`:
  - Toggle quest list with Q key
  - Update active quest distance calculation
  - Update cargo health display
- [ ] Add methods:
  - `void RefreshQuestList()` - Calls QuestListUI to repopulate
  - `void ShowQuestDetails(QuestData quest)` - Optional detailed view

**Input Handling:**
```csharp
if (Input.GetKeyDown(KeyCode.Q))
    questListUI.TogglePanel();
```

---

## Phase 4: Quest Zones & Triggers

### Task 4.1: Create Quest Zone Component
- [ ] Create `Assets/Scripts/Quest/QuestZone.cs`
- [ ] Inherit from MonoBehaviour
- [ ] Add components:
  - `SphereCollider` or `BoxCollider` (IsTrigger = true)
  - Tag: "QuestZone"
- [ ] Add fields:
  - `QuestLocation location` - Associated location data
  - `QuestZoneType zoneType` - Enum: Pickup or Delivery
  - `bool isActive` - Whether this zone is currently valid
  - `UnityEvent<Transform> OnPlayerEntered`
  - `UnityEvent<Transform> OnPlayerExited`
- [ ] Implement trigger methods:
  - `void OnTriggerEnter(Collider other)` - Check if player, invoke event
  - `void OnTriggerExit(Collider other)` - Track exit
- [ ] Add `void SetActive(bool active)`:
  - Enable/disable collider
  - Show/hide visual marker

**Player Detection:**
```csharp
if (other.CompareTag("Player") && isActive)
{
    OnPlayerEntered?.Invoke(other.transform);
    QuestManager.Instance.OnPlayerEnteredZone(this);
}
```

---

### Task 4.2: Integrate Zones with Quest Manager
- [ ] In `QuestManager.cs`, add fields:
  - `GameObject questZonePrefab` - Prefab with QuestZone component
  - `List<QuestZone> activeZones` - Currently spawned zones
- [ ] Add `QuestZone SpawnQuestZone(QuestLocation location, QuestZoneType type)`:
  - Instantiate zone prefab at location.Position
  - Set up collider size based on location.TriggerRadius
  - Attach appropriate visual marker (from Phase 3.6)
  - Add to activeZones list
  - Return zone reference
- [ ] Add `void OnPlayerEnteredZone(QuestZone zone)`:
  - Check if zone is pickup location → call `OnCargoPickedUp()`
  - Check if zone is delivery location → call `OnCargoDelivered()`
  - Play sound effect
  - Show UI notification
- [ ] Add `void ClearAllZones()`:
  - Destroy all GameObjects in activeZones
  - Clear list

**Zone Spawning on Quest Start:**
```csharp
void OnQuestStarted(QuestData quest)
{
    QuestZone pickupZone = SpawnQuestZone(quest.PickupLocation, QuestZoneType.Pickup);
    // Delivery zones spawned after pickup
}
```

---

### Task 4.3: Implement Cargo Pickup Logic
- [ ] In `QuestManager.cs`, add field:
  - `bool hasPickedUpCargo` - Tracks if player collected cargo
- [ ] Add `void OnCargoPickedUp()`:
  - Set `hasPickedUpCargo = true`
  - Hide/destroy pickup zone marker
  - Spawn delivery zone markers
  - Update active quest UI ("Go to delivery location")
  - Play pickup sound/animation
  - Apply cargo weight to player vehicle (see Phase 5)
  - Show notification: "Cargo loaded! Deliver to [location]"
- [ ] Add validation to prevent double pickup

**UI Feedback:**
```csharp
activeQuestUI.UpdateObjective("Deliver cargo to " + quest.DeliveryLocation.LocationName);
```

---

### Task 4.4: Implement Cargo Delivery Logic
- [ ] Add `void OnCargoDelivered()`:
  - For standard quest: Call `CompleteQuest(currentQuest)`
  - For multi-stop quest:
    - Increment delivery counter
    - Remove current delivery marker
    - If more stops exist: Spawn next delivery marker
    - If last stop: Complete quest
  - Calculate final reward (base + bonus)
  - Play delivery success sound
  - Show visual feedback (particle effect at delivery zone)
- [ ] Add validation to prevent delivery without pickup
- [ ] Add `void OnCargoDestroyed()` (for fragile cargo):
  - Call `FailQuest(currentQuest, "Cargo destroyed")`
  - Show failure UI

**Multi-Stop Handling:**
```csharp
void OnCargoDelivered()
{
    currentDeliveryIndex++;
    if (currentDeliveryIndex >= currentQuest.DeliveryLocations.Count)
        CompleteQuest(currentQuest);
    else
        SpawnNextDeliveryZone();
}
```

---

### Task 4.5: Add Zone Visualization in Editor
- [ ] Add `OnDrawGizmos()` to QuestZone.cs:
  - Draw wire sphere/cube at trigger size
  - Color based on zone type (blue=pickup, green=delivery)
  - Draw label with location name
- [ ] Add custom inspector (optional):
  - Create `Assets/Scripts/Quest/Editor/QuestZoneEditor.cs`
  - Show location info, zone type
  - Button to snap to nearest road waypoint
  - Preview marker appearance

**Gizmo Drawing:**
```csharp
void OnDrawGizmos()
{
    Gizmos.color = zoneType == QuestZoneType.Pickup ? Color.cyan : Color.green;
    Gizmos.DrawWireSphere(transform.position, location.TriggerRadius);
}
```

---

## Phase 5: Cargo System

### Task 5.1: Create Cargo Visual Component
- [ ] Create `Assets/Scripts/Quest/CargoVisual.cs`
- [ ] Design cargo model:
  - Simple box mesh (or use existing asset)
  - Multiple variations (wooden crate, metal box, cardboard, etc.)
  - Attach to player vehicle when picked up
  - Position: Above car roof or in truck bed
- [ ] Add fields:
  - `Transform attachPoint` - Where cargo attaches (on player vehicle)
  - `GameObject[] cargoModels` - Different visual variants
  - `ParticleSystem damageEffect` - Sparks/smoke for damage
  - `AudioSource damageSound`
- [ ] Implement methods:
  - `void AttachCargo(CargoData cargo)` - Show cargo model
  - `void DetachCargo()` - Hide cargo model
  - `void PlayDamageEffect()` - Show particles/play sound

**Cargo Attachment:**
```csharp
void AttachCargo(CargoData cargo)
{
    GameObject cargoModel = SelectModelByType(cargo);
    cargoModel.transform.SetParent(attachPoint);
    cargoModel.transform.localPosition = Vector3.zero;
    cargoModel.SetActive(true);
}
```

---

### Task 5.2: Implement Cargo Weight System
- [ ] In `CarController.cs`, add fields:
  - `float baseRigidbodyMass` - Store original mass
  - `float currentCargoWeight` - Added weight from cargo
- [ ] Add `void AddCargoWeight(float weight)`:
  - Store in currentCargoWeight
  - Update Rigidbody.mass: `baseRigidbodyMass + weight`
  - Adjust center of mass to account for cargo (slightly higher)
- [ ] Add `void RemoveCargoWeight()`:
  - Reset mass to baseRigidbodyMass
  - Reset center of mass
- [ ] Store baseMass in `Start()`:
  - `baseRigidbodyMass = rb.mass`

**Integration with Quest System:**
```csharp
// In QuestManager.OnCargoPickedUp():
playerController.AddCargoWeight(currentQuest.Cargo.Weight);
```

**Handling Effect:**
- Heavier cargo = slower acceleration, longer braking distance
- Higher center of mass = more body roll in turns
- More realistic driving challenge

---

### Task 5.3: Implement Fragile Cargo Damage System
- [ ] In `QuestManager.cs`, add field:
  - `float lastCollisionTime` - Debounce collision damage
- [ ] Hook into CarController collision detection:
  - Option A: Add collision forwarding in CarController
  - Option B: Add separate collision detector component to player vehicle
- [ ] Add `void OnVehicleCollision(Collision collision)`:
  - Early exit if current quest is null or cargo not fragile
  - Calculate collision force: `collision.impulse.magnitude / Time.fixedDeltaTime`
  - If force exceeds threshold (e.g., 10000):
    - Calculate damage: `damage = (force - threshold) / 1000`
    - Call `currentQuest.Cargo.TakeDamage(damage)`
    - Play damage effect via CargoVisual
    - Update cargo health UI
    - If cargo destroyed → `FailQuest()`
- [ ] Add damage threshold multipliers:
  - Low speed collision (< 20 km/h): No damage
  - Medium (20-40 km/h): 10-30% damage
  - High (> 40 km/h): 50-100% damage (instant fail)

**Collision Integration (Option A):**
```csharp
// In CarController.cs:
void OnCollisionEnter(Collision collision)
{
    QuestManager.Instance?.OnVehicleCollision(collision);
}
```

---

### Task 5.4: Add Cargo Sound Effects
- [ ] Create audio files or use asset store sounds:
  - `CargoPickup.wav` - Cargo loading sound (beep, hydraulic)
  - `CargoDeliver.wav` - Delivery success sound (chime, beep)
  - `CargoDamage.wav` - Damage sound (crash, breaking glass)
  - `CargoDestroyed.wav` - Failure sound (alarm, explosion)
- [ ] In `QuestManager.cs`, add AudioSource component:
  - `AudioSource questAudioSource`
  - `AudioClip pickupSound`
  - `AudioClip deliverSound`
  - `AudioClip damageSound`
  - `AudioClip destroyedSound`
- [ ] Play sounds at appropriate events:
  - `OnCargoPickedUp()` → Play pickupSound
  - `OnCargoDelivered()` → Play deliverSound
  - `OnVehicleCollision()` (if damage) → Play damageSound
  - `OnCargoDestroyed()` → Play destroyedSound

**3D Sound Setup:**
- Set AudioSource to 3D spatial blend
- Attach to player vehicle for positional audio
- Set max distance ~50m

---

### Task 5.5: Create Cargo Type Variants
- [ ] Create `Assets/Scripts/Quest/CargoLibrary.cs` ScriptableObject:
  - `List<CargoData> cargoTypes` - Predefined cargo types
  - Method: `CargoData GetRandomCargo()`
  - Method: `CargoData GetCargoByName(string name)`
- [ ] Define cargo variants:
  1. **Standard Boxes**:
     - Weight: 100-200 kg
     - Fragile: false
     - Icon: Box sprite
  2. **Electronics**:
     - Weight: 50-100 kg
     - Fragile: true (high damage)
     - Icon: Circuit board sprite
  3. **Medical Supplies**:
     - Weight: 50-150 kg
     - Fragile: true (medium damage)
     - Icon: Cross/medkit sprite
     - Special: Often paired with Express delivery
  4. **Heavy Machinery**:
     - Weight: 300-500 kg
     - Fragile: false
     - Icon: Gear sprite
     - Special: Significantly affects vehicle handling
  5. **Fragile Glass**:
     - Weight: 100 kg
     - Fragile: true (very high damage, breaks easily)
     - Icon: Wine glass sprite
- [ ] Create menu item: `Assets/Create/Quest System/Cargo Library`
- [ ] Create instance: `Assets/Resources/CargoLibrary.asset`

**Cargo Assignment in Quest Generation:**
```csharp
CargoData cargo = cargoLibrary.GetRandomCargo();
if (questType == QuestType.FragileDelivery)
    cargo = cargoLibrary.GetCargoByFragility(true);
```

---

## Phase 6: Timer & Scoring

### Task 6.1: Implement Quest Timer System
- [ ] In `QuestData.cs`, enhance timer logic:
  - `float startTime` - When quest was accepted (Time.time)
  - `float pausedTime` - Accumulated pause time (optional)
- [ ] In `QuestManager.Update()`:
  - Call `currentQuest.UpdateTimer(Time.deltaTime)`
  - Check `if (currentQuest.IsTimeExpired())` → `FailQuest()`
  - Calculate time remaining percentage for UI color coding
- [ ] Add pause functionality (optional):
  - `void PauseQuest()` - Stops timer (for pause menu)
  - `void ResumeQuest()` - Resumes timer
  - Store pause start time and accumulate when resumed

**Timer Display Format:**
```csharp
string FormatTime(float seconds)
{
    int minutes = Mathf.FloorToInt(seconds / 60);
    int secs = Mathf.FloorToInt(seconds % 60);
    return $"{minutes:00}:{secs:00}";
}
```

---

### Task 6.2: Implement Bonus Time Calculation
- [ ] In `QuestData.cs`, add property:
  - `bool EarnedBonus { get; private set; }`
- [ ] In `CalculateFinalReward()`:
  - Calculate completion percentage: `completionPercent = TimeRemaining / TimeLimit`
  - If `completionPercent >= BonusTimeThreshold` (default 0.5 = 50%):
    - Set `EarnedBonus = true`
    - Return `BaseReward + BonusReward`
  - Else:
    - Return `BaseReward`
- [ ] Add speed bonus multiplier (optional):
  - Very fast (75%+ time left): 1.5x bonus
  - Fast (50-75% time left): 1.0x bonus
  - Normal (< 50% time left): No bonus

**Bonus Display:**
```csharp
// In QuestCompleteUI:
if (quest.EarnedBonus)
    rewardText.text = $"${baseReward} + ${bonusReward} (SPEED BONUS!)";
```

---

### Task 6.3: Implement Distance Tracking
- [ ] In `QuestData.cs`, add fields:
  - `float totalDistanceTraveled` - For statistics
  - `Vector3 lastPosition` - Track movement
- [ ] In `QuestManager.Update()`:
  - If quest active and cargo picked up:
    - Calculate distance: `Vector3.Distance(playerTransform.position, currentQuest.lastPosition)`
    - Add to `totalDistanceTraveled`
    - Update `lastPosition`
- [ ] Display in completion screen:
  - "Distance: 2.3 km"
  - Optional: Compare to optimal route distance (straight line * 1.5)
  - Bonus for efficient routing (optional, advanced)

**Distance Display:**
```csharp
float distanceKm = totalDistanceTraveled / 1000f;
statsText.text += $"\nDistance: {distanceKm:F1} km";
```

---

### Task 6.4: Implement Collision Penalty System (Optional)
- [ ] In `QuestData.cs`, add fields:
  - `int collisionCount` - Number of collisions
  - `int npcCollisionCount` - Collisions with NPC vehicles
- [ ] In `QuestManager.OnVehicleCollision()`:
  - Increment collision counters
  - Apply reward penalty:
    - -$10 per minor collision (< 30 km/h)
    - -$50 per major collision (> 30 km/h)
    - -$100 per NPC collision (traffic violation)
- [ ] Deduct penalties from final reward in `CalculateFinalReward()`:
  - `int penalty = (collisionCount * 10) + (npcCollisionCount * 100)`
  - `return Mathf.Max(0, baseReward - penalty)`
- [ ] Display in completion screen

**Clean Delivery Bonus (Optional):**
- If `collisionCount == 0`: +$200 "PERFECT DELIVERY" bonus

---

### Task 6.5: Implement Scoring Tiers
- [ ] In `QuestData.cs`, add rating system:
  - `enum PerformanceRating { F, D, C, B, A, S }`
  - `PerformanceRating rating { get; private set; }`
- [ ] Add `CalculateRating()` method:
  - S Rank: Bonus earned + zero collisions + cargo health > 90%
  - A Rank: Bonus earned + < 2 collisions
  - B Rank: Completed with time remaining
  - C Rank: Completed barely (< 10% time remaining)
  - D Rank: Cargo damaged significantly
  - F Rank: Failed
- [ ] Call in `CompleteQuest()` and display in UI
- [ ] Store ratings in quest history for statistics

**Rating Display:**
```csharp
// Show star rating or letter grade
resultText.text = $"DELIVERY COMPLETE\nRank: {rating}";
// S rank = gold color, A = silver, etc.
```

---

### Task 6.6: Add Combo/Streak System (Optional Enhancement)
- [ ] In `QuestManager.cs`, add fields:
  - `int consecutiveSuccesses` - Streak counter
  - `float streakMultiplier` - Reward multiplier
- [ ] Increment on successful completion:
  - Each consecutive success: `streakMultiplier += 0.1` (max 2.0x)
- [ ] Reset streak on failure
- [ ] Apply multiplier to rewards:
  - `finalReward = baseReward * streakMultiplier`
- [ ] Display streak in UI: "5 Streak! 1.5x Reward Multiplier"

---

## Phase 7: Rewards & Progression

### Task 7.1: Create Player Progression Manager
- [ ] Create `Assets/Scripts/Quest/PlayerProgressionManager.cs`
- [ ] Implement Singleton pattern
- [ ] Add fields:
  - `int currentMoney` - Player currency
  - `int currentLevel` - Player level (1-50)
  - `int currentXP` - Experience points
  - `int xpToNextLevel` - XP needed for level up
  - `int totalQuestsCompleted` - Statistics
  - `int totalDistanceTraveled` - Statistics
  - `float totalTimePlayed` - Statistics
- [ ] Add Unity events:
  - `UnityEvent<int> OnMoneyChanged`
  - `UnityEvent<int> OnLevelUp`
  - `UnityEvent<int> OnXPGained`

**Level Progression Formula:**
```csharp
int CalculateXPForLevel(int level)
{
    return 100 * level * level; // Exponential growth
}
```

---

### Task 7.2: Implement Reward System
- [ ] Add methods to PlayerProgressionManager:
  - `void AwardMoney(int amount)`:
    - Add to currentMoney
    - Invoke OnMoneyChanged event
    - Show UI notification: "+$500"
  - `void AwardXP(int amount)`:
    - Add to currentXP
    - Check if level up: `if (currentXP >= xpToNextLevel)`
    - Call `LevelUp()`
    - Invoke OnXPGained event
  - `void LevelUp()`:
    - Increment currentLevel
    - Reset currentXP (carry over excess)
    - Calculate new xpToNextLevel
    - Invoke OnLevelUp event
    - Show level up UI/animation
    - Unlock new content (harder quests)
- [ ] In `QuestManager.CompleteQuest()`:
  - Call `PlayerProgressionManager.Instance.AwardMoney(finalReward)`
  - Call `PlayerProgressionManager.Instance.AwardXP(quest.XPReward)`

**XP Reward Scaling:**
```csharp
int CalculateXPReward(QuestDifficulty difficulty)
{
    switch(difficulty)
    {
        case Easy: return 50;
        case Medium: return 100;
        case Hard: return 200;
        case Expert: return 500;
    }
}
```

---

### Task 7.3: Create Progression UI
- [ ] Add UI elements to canvas:
  - Money display (top-right): "$1,250"
  - Level display (top-right): "Level 5"
  - XP bar (top-right): Progress bar showing XP progress
- [ ] Create `Assets/Scripts/Quest/UI/ProgressionUI.cs`:
  - `TextMeshProUGUI moneyText`
  - `TextMeshProUGUI levelText`
  - `Image xpFillBar`
  - `Animator moneyAnimator` (for reward popup)
- [ ] Subscribe to progression events:
  - `OnMoneyChanged` → Update money text + play coin animation
  - `OnLevelUp` → Show level up popup + particle effect
  - `OnXPGained` → Animate XP bar fill
- [ ] Implement smooth XP bar animation:
  - Use Coroutine or DOTween to lerp fill amount

**Money Reward Animation:**
```csharp
void OnMoneyChanged(int newAmount)
{
    // Animate text color flash
    moneyText.text = $"${newAmount}";
    moneyAnimator.SetTrigger("MoneyGained");
}
```

---

### Task 7.4: Implement Quest Unlock System
- [ ] In `QuestDatabase.cs`, add to QuestTemplate:
  - `int requiredLevel` - Level needed to unlock
  - `bool isUnlocked` - Unlocked status
- [ ] In `QuestManager.GenerateAvailableQuests()`:
  - Filter quests: Only include if `playerLevel >= quest.requiredLevel`
  - Prioritize mix of difficulties appropriate for level
- [ ] Create unlock progression:
  - Level 1-5: Easy and Medium quests
  - Level 6-15: Medium and Hard quests
  - Level 16-30: Hard and Expert quests
  - Level 31+: All quests + special challenges
- [ ] Show locked quests in UI (grayed out with lock icon):
  - Display "Requires Level X"

**Quest Filtering:**
```csharp
List<QuestTemplate> GetAvailableQuestsForLevel(int level)
{
    return questDatabase.AvailableQuests
        .Where(q => q.requiredLevel <= level)
        .ToList();
}
```

---

### Task 7.5: Add Achievement System (Optional)
- [ ] Create `Assets/Scripts/Quest/Achievement.cs`:
  - `string achievementID`
  - `string name`
  - `string description`
  - `Sprite icon`
  - `bool isUnlocked`
  - `int rewardMoney` - Bonus for unlocking
- [ ] Define achievements:
  - "First Delivery" - Complete 1 quest
  - "Delivery Pro" - Complete 10 quests
  - "Speed Demon" - Earn 5 speed bonuses
  - "Perfect Run" - Complete quest with S rank
  - "Marathon Driver" - Travel 100 km total
  - "Heavy Hauler" - Deliver 500 kg total weight
  - "Fragile Expert" - Deliver 10 fragile cargos undamaged
- [ ] In PlayerProgressionManager:
  - `List<Achievement> achievements`
  - Method: `void CheckAchievements()` - Called after each quest
  - Method: `void UnlockAchievement(string id)` - Award achievement
- [ ] Create achievement notification popup (similar to level up)

---

### Task 7.6: Implement Daily Challenges (Optional)
- [ ] In `QuestManager.cs`, add fields:
  - `QuestData dailyChallenge` - Special daily quest
  - `DateTime lastDailyChallengeDate` - Track reset
- [ ] Add `void GenerateDailyChallenge()`:
  - Called once per day (check on game start)
  - Create special quest with:
    - Higher difficulty
    - 2x reward multiplier
    - Unique requirements (e.g., "No collisions allowed")
    - 24-hour availability
- [ ] Add UI indicator for daily challenge (star icon)
- [ ] Reset daily challenge at midnight (use DateTime.Now)

**Daily Challenge Check:**
```csharp
void Start()
{
    if (DateTime.Now.Date != lastDailyChallengeDate.Date)
        GenerateDailyChallenge();
}
```

---

## Phase 8: Save/Load System

### Task 8.1: Create Save Data Structures
- [ ] Create `Assets/Scripts/Quest/SaveSystem/SaveData.cs`
- [ ] Define `GameSaveData` class (mark `[System.Serializable]`):
  - `PlayerProgressionData playerData`
  - `QuestSaveData questData`
  - `string saveDate` - Timestamp
  - `int saveVersion` - For backwards compatibility
- [ ] Define `PlayerProgressionData`:
  - `int money`
  - `int level`
  - `int xp`
  - `int totalQuestsCompleted`
  - `float totalDistanceTraveled`
  - `List<string> unlockedAchievements`
- [ ] Define `QuestSaveData`:
  - `List<SerializedQuest> activeQuests`
  - `List<SerializedQuest> availableQuests`
  - `List<string> completedQuestIDs` - History
  - `SerializedQuest currentQuest` - In-progress quest
- [ ] Define `SerializedQuest`:
  - All QuestData fields but simplified (no Unity references)
  - Use Vector3Serializable for positions

**Serializable Vector3:**
```csharp
[System.Serializable]
public struct Vector3Serializable
{
    public float x, y, z;
    public Vector3Serializable(Vector3 v) { x=v.x; y=v.y; z=v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}
```

---

### Task 8.2: Implement Save System
- [ ] Create `Assets/Scripts/Quest/SaveSystem/SaveManager.cs`
- [ ] Implement singleton pattern
- [ ] Add fields:
  - `string saveFilePath` - Default: Application.persistentDataPath + "/savegame.json"
  - `GameSaveData currentSaveData`
- [ ] Add method: `void SaveGame()`:
  - Gather data from PlayerProgressionManager
  - Gather data from QuestManager
  - Create GameSaveData instance
  - Serialize to JSON using JsonUtility or Newtonsoft.Json
  - Write to file using File.WriteAllText()
  - Show "Game Saved" notification
- [ ] Add method: `GameSaveData LoadGame()`:
  - Check if save file exists
  - Read file using File.ReadAllText()
  - Deserialize JSON to GameSaveData
  - Return data (or null if no save)
- [ ] Add auto-save triggers:
  - After quest completion
  - On application quit
  - Every 5 minutes (optional)

**JSON Serialization:**
```csharp
string json = JsonUtility.ToJson(saveData, true);
File.WriteAllText(saveFilePath, json);
```

---

### Task 8.3: Implement Load System
- [ ] In `QuestManager.Start()`:
  - Check if save exists: `SaveManager.Instance.LoadGame()`
  - If save exists:
    - Call `LoadSaveData(saveData.questData)`
    - Restore active quests and markers
  - If no save:
    - Generate initial available quests
- [ ] In `PlayerProgressionManager.Start()`:
  - Load player progression from save
  - If no save: Initialize to default (level 1, $500 starting money)
- [ ] Add `void LoadSaveData(QuestSaveData data)` to QuestManager:
  - Reconstruct QuestData objects from SerializedQuest
  - Respawn quest zones if quest was in progress
  - Resume timer for current quest

**Save File Location:**
- Windows: `C:/Users/[User]/AppData/LocalLow/[CompanyName]/[GameName]/savegame.json`
- Use `Application.persistentDataPath` for cross-platform compatibility

---

### Task 8.4: Add Save/Load UI
- [ ] Add buttons to pause menu (if exists) or settings:
  - "Save Game" button
  - "Load Game" button (restart to last save)
- [ ] Create `Assets/Scripts/Quest/UI/SaveLoadUI.cs`:
  - `Button saveButton`
  - `Button loadButton`
  - `TextMeshProUGUI statusText` - "Last saved: [time]"
- [ ] Add button listeners:
  - Save → Call SaveManager.SaveGame() → Show confirmation
  - Load → Call SaveManager.LoadGame() → Reload scene with loaded data
- [ ] Display last save time in UI
- [ ] Add "New Game" option that deletes save file

**Save Confirmation:**
```csharp
void OnSaveButtonClicked()
{
    SaveManager.Instance.SaveGame();
    statusText.text = "Game Saved!";
    StartCoroutine(ClearStatusAfterDelay(2f));
}
```

---

### Task 8.5: Implement Cloud Save (Optional, Advanced)
- [ ] Integrate with platform-specific cloud save:
  - Steam Cloud (if Steam build)
  - Unity Gaming Services Cloud Save
  - Google Play Game Services (Android)
- [ ] Add cloud sync option in settings
- [ ] Handle save conflicts (local vs cloud)
- [ ] Add "Sync Save" button to UI

**This is optional and platform-dependent - skip if not needed**

---

## Phase 9: Quest Generation

### Task 9.1: Implement Procedural Location Picker
- [ ] In `QuestManager.cs`, enhance `GenerateRandomLocation()`:
  - Add location type preference: `enum LocationType { Warehouse, Residential, Commercial, Industrial }`
  - Filter road segments by area (if road names contain keywords)
  - Ensure variety: Don't reuse same locations consecutively
  - Weight selection by road segment length (prefer longer roads for easier placement)
- [ ] Add `List<Vector3> usedLocations` with cooldown system:
  - Track recently used positions
  - Ensure minimum distance between consecutive quest locations (500m+)
  - Clear cooldown after 3-5 quests
- [ ] Add location validation:
  - Check if location is accessible (not inside building mesh)
  - Raycast down to ensure ground exists
  - Verify road segment is valid and active

**Location Type Filtering (Example):**
```csharp
if (roadSegment.name.Contains("Highway"))
    locationTypes.Add(LocationType.Industrial);
else if (roadSegment.name.Contains("Street"))
    locationTypes.Add(LocationType.Residential);
```

---

### Task 9.2: Implement Difficulty-Based Quest Generation
- [ ] Add `QuestData GenerateQuestByDifficulty(QuestDifficulty difficulty)`:
  - Pick appropriate cargo type:
    - Easy: Standard cargo, not fragile
    - Medium: Random cargo, possible fragile
    - Hard: Heavier cargo or fragile
    - Expert: Heavy + fragile or multi-stop
  - Calculate distance requirement:
    - Easy: 1000-2000m
    - Medium: 2000-4000m
    - Hard: 4000-6000m
    - Expert: 6000-10000m
  - Generate pickup and delivery locations with correct distance
  - Retry if locations invalid (max 10 attempts)
  - Calculate time limit based on distance:
    - `timeLimit = (distance / averageSpeed) * difficultyMultiplier`
    - Average speed: 40 km/h = ~11 m/s
    - Difficulty multipliers: Easy=2.0, Medium=1.5, Hard=1.2, Expert=1.0
  - Set rewards based on difficulty and distance:
    - `baseReward = (distance * 0.1) + difficultyBonus`

**Distance Validation Loop:**
```csharp
for (int attempt = 0; attempt < 10; attempt++)
{
    QuestLocation pickup = GenerateRandomLocation("Pickup");
    QuestLocation delivery = GenerateRandomLocation("Delivery");
    float distance = Vector3.Distance(pickup.Position, delivery.Position);

    if (distance >= minDist && distance <= maxDist)
        return CreateQuest(pickup, delivery, difficulty);
}
```

---

### Task 9.3: Implement Multi-Stop Quest Generation
- [ ] Add `QuestData GenerateMultiStopQuest(int stopCount, QuestDifficulty difficulty)`:
  - Generate 2-4 delivery locations (for stopCount > 1)
  - Create logical route (attempt to order locations by proximity)
  - Calculate total route distance
  - Scale time limit: `baseTimeLimit * stopCount * 1.5`
  - Scale reward: `baseReward * stopCount * 1.8`
- [ ] Add route optimization (optional):
  - Use simple nearest-neighbor algorithm to order stops
  - Minimize total distance while maintaining start/end
- [ ] Add visual route preview on map (if minimap exists)

**Route Optimization:**
```csharp
List<QuestLocation> OptimizeRoute(QuestLocation start, List<QuestLocation> stops)
{
    List<QuestLocation> optimized = new List<QuestLocation>();
    QuestLocation current = start;

    while (stops.Count > 0)
    {
        QuestLocation nearest = FindNearest(current, stops);
        optimized.Add(nearest);
        stops.Remove(nearest);
        current = nearest;
    }

    return optimized;
}
```

---

### Task 9.4: Implement Special Quest Types
- [ ] Add `QuestData GenerateExpressDelivery()`:
  - Standard single-stop delivery
  - Time limit = 0.6x normal (very tight)
  - Reward = 2.0x normal
  - Name prefix: "EXPRESS:"
- [ ] Add `QuestData GenerateFragileDelivery()`:
  - Force fragile cargo selection
  - Slightly longer time limit (player must drive carefully)
  - Bonus for zero damage: +50% reward
  - Name prefix: "FRAGILE:"
- [ ] Add `QuestData GenerateTimeTrial()`:
  - Very short time limit (0.5x normal)
  - Reward scales with remaining time (more time = more money)
  - Name prefix: "TIME TRIAL:"
  - Optional: Remove traffic for pure speed challenge
- [ ] Add quest type selection logic:
  - 60% Standard Delivery
  - 20% Express Delivery
  - 15% Fragile Delivery
  - 5% Multi-Stop

**Quest Name Generation:**
```csharp
string GenerateQuestName(QuestType type, QuestLocation delivery)
{
    string prefix = type switch
    {
        QuestType.ExpressDelivery => "EXPRESS: ",
        QuestType.FragileDelivery => "FRAGILE: ",
        QuestType.TimeTrial => "TIME TRIAL: ",
        _ => ""
    };

    return prefix + $"Deliver to {delivery.LocationName}";
}
```

---

### Task 9.5: Implement Quest Pool Refresh System
- [ ] In `QuestManager.cs`, add field:
  - `float questRefreshInterval` - Time between refresh (default: 300s = 5 min)
  - `float timeSinceLastRefresh`
- [ ] In `Update()`:
  - Increment `timeSinceLastRefresh`
  - If interval reached: Call `RefreshAvailableQuests()`
- [ ] Add `void RefreshAvailableQuests()`:
  - Remove old quests that haven't been accepted
  - Generate new quests to maintain pool size (e.g., always 5 available)
  - Ensure variety in new quests
  - Notify player: "New deliveries available!"
- [ ] Add manual refresh option:
  - Button in quest list UI: "Refresh Deliveries"
  - Cooldown to prevent spam (30 seconds)

**Quest Pool Size:**
- Maintain 3-5 available quests at all times
- Replace completed quests immediately
- Refresh unaccepted quests periodically

---

## Phase 10: Polish & Integration

### Task 10.1: Add Audio & Music
- [ ] Create audio sources in QuestManager:
  - `AudioSource musicSource` - Background music
  - `AudioSource sfxSource` - Quest sound effects
- [ ] Add sound effects:
  - Quest accepted: Confirmation beep
  - Quest completed: Success fanfare
  - Quest failed: Failure buzzer
  - Time warning: Alarm when < 30 seconds
  - Level up: Victory jingle
- [ ] Add background music system (optional):
  - Calm music during exploration
  - Intense music during active delivery
  - Transition smoothly between tracks
- [ ] Add music volume controls in settings

**Time Warning Audio:**
```csharp
void Update()
{
    if (currentQuest.TimeRemaining < 30f && !warningPlayed)
    {
        sfxSource.PlayOneShot(timeWarningSound);
        warningPlayed = true;
    }
}
```

---

### Task 10.2: Add Particle Effects
- [ ] Create particle effects:
  - **Quest marker particles**: Glowing rising particles at pickup/delivery zones
  - **Pickup effect**: Burst of particles when cargo loaded
  - **Delivery effect**: Confetti/fireworks when delivery successful
  - **Damage effect**: Sparks/smoke when fragile cargo damaged
  - **Level up effect**: Golden particle burst
- [ ] Integrate effects with quest events:
  - Instantiate at appropriate positions
  - Use object pooling for performance (reuse particle systems)
- [ ] Add trail effect to quest markers for visibility

**Particle System Setup:**
- Use UnityEngine.ParticleSystem
- Set lifetime, emission rate, color over lifetime
- Additive/Alpha blend materials for glow effect

---

### Task 10.3: Add Minimap/Compass Integration (Optional)
- [ ] Create minimap UI (circular or rectangular):
  - Canvas with RawImage showing top-down camera
  - Position: Bottom-right corner
  - Size: 200x200 pixels
- [ ] Add minimap camera:
  - Orthographic camera above player
  - Follows player position (X,Z) only
  - Fixed Y rotation (top-down view)
  - Renders to RenderTexture
- [ ] Add quest markers to minimap:
  - Pickup location: Blue icon
  - Delivery location: Green icon
  - Distance indicators
- [ ] Add compass HUD element:
  - Shows direction to next objective
  - Rotates based on player heading

**Minimap Camera Setup:**
```csharp
minimapCamera.orthographic = true;
minimapCamera.orthographicSize = 100f;
minimapCamera.transform.position = player.position + Vector3.up * 200f;
minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
```

---

### Task 10.4: Add Tutorial System
- [ ] Create `Assets/Scripts/Quest/TutorialManager.cs`
- [ ] Add tutorial steps:
  1. "Welcome to Delivery Driver! Press Q to open quest menu."
  2. "Select a delivery to begin."
  3. "Drive to the blue marker to pick up cargo."
  4. "Now deliver to the green marker before time runs out!"
  5. "Complete deliveries to earn money and level up."
- [ ] Implement tutorial overlay:
  - Semi-transparent panel with instructions
  - Arrow pointing to relevant UI elements
  - "Next" button or auto-advance on action completion
- [ ] Track tutorial progress:
  - Save tutorial completion in PlayerPrefs
  - Option to replay tutorial in settings
- [ ] Add tooltips to UI elements:
  - Hover over quest entries to see details
  - Hover over rewards to see breakdown

**Tutorial Trigger:**
```csharp
void Start()
{
    if (!PlayerPrefs.HasKey("TutorialCompleted"))
        StartTutorial();
}
```

---

### Task 10.5: Add Settings & Options
- [ ] Create settings menu UI panel:
  - Audio volume sliders (Master, Music, SFX)
  - Quest difficulty preference (affect generation)
  - UI scale slider
  - Control rebinding (if custom input)
  - Save/Load options
- [ ] Create `Assets/Scripts/Quest/GameSettings.cs`:
  - Save settings to PlayerPrefs
  - Apply settings on load
  - Default values
- [ ] Add pause menu integration:
  - Access settings during gameplay
  - Pause quest timer when paused

**Settings Storage:**
```csharp
PlayerPrefs.SetFloat("MusicVolume", musicVolume);
PlayerPrefs.SetInt("QuestDifficulty", (int)difficulty);
PlayerPrefs.Save();
```

---

### Task 10.6: Optimize Performance
- [ ] Implement object pooling for quest markers:
  - Reuse marker GameObjects instead of Instantiate/Destroy
  - Pool size: 10 markers (more than enough)
- [ ] Optimize UI updates:
  - Only update active quest UI when values change (use dirty flag)
  - Cache UI component references
  - Use StringBuilder for string concatenation
- [ ] Optimize quest distance calculations:
  - Calculate distance once per frame, not per UI element
  - Use sqrMagnitude where possible (avoid Sqrt)
- [ ] Add LOD for quest markers:
  - Simple billboard sprite when far away
  - Full 3D model when nearby
- [ ] Profile and optimize hotspots:
  - Use Unity Profiler to identify bottlenecks
  - Optimize any Update() methods with frequent calls

**Distance Check Optimization:**
```csharp
float sqrDistance = (player.position - target.position).sqrMagnitude;
if (sqrDistance < triggerRadius * triggerRadius) // No Sqrt!
    OnPlayerInRange();
```

---

### Task 10.7: Add Debugging Tools
- [ ] Create debug menu (toggle with F1 key):
  - Button: "Complete Current Quest" (instant win)
  - Button: "Fail Current Quest" (instant fail)
  - Button: "Add $1000"
  - Button: "Add XP"
  - Button: "Unlock All Quests"
  - Button: "Teleport to Pickup/Delivery"
  - Toggle: "Infinite Time" (pause timer)
  - Toggle: "Invincible Cargo" (no damage)
- [ ] Create `Assets/Scripts/Quest/DebugQuestMenu.cs`:
  - Only compile in Development builds: `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
  - OnGUI menu or Canvas panel
- [ ] Add debug visualization:
  - Gizmos showing quest zones
  - Lines showing route between pickup/delivery
  - Text labels showing quest state

**Debug Menu Toggle:**
```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
void Update()
{
    if (Input.GetKeyDown(KeyCode.F1))
        debugMenuActive = !debugMenuActive;
}
#endif
```

---

### Task 10.8: Test & Balance
- [ ] Playtest each quest difficulty:
  - Ensure Easy quests are completable by casual players
  - Ensure Expert quests provide challenge for skilled players
  - Adjust time limits based on actual completion times
- [ ] Balance rewards:
  - Ensure progression feels rewarding (not too fast/slow)
  - Test money accumulation rate
  - Adjust XP requirements for level ups
- [ ] Test edge cases:
  - Multiple quests active simultaneously
  - Abandoning quest with cargo
  - Player death/respawn (if implemented)
  - Loading save mid-quest
  - Quest locations spawning in water/out of bounds
- [ ] Test performance:
  - Multiple quest markers active
  - Long play sessions (memory leaks?)
  - Frame rate with many UI elements
- [ ] Test on target platform:
  - Different screen resolutions
  - Input devices (keyboard, controller)

**Balance Checklist:**
- [ ] Easy quest completable in ~80% of time limit (generous)
- [ ] Medium quest requires ~60% efficiency
- [ ] Hard quest requires ~45% efficiency (tight but fair)
- [ ] Expert quest requires ~35% efficiency (challenging)
- [ ] Level 1-10 takes ~30 minutes of gameplay
- [ ] Full progression (level 50) takes ~20-30 hours

---

### Task 10.9: Add Analytics & Statistics (Optional)
- [ ] Create statistics screen:
  - Total quests completed
  - Success rate (completed / attempted)
  - Total money earned
  - Total distance traveled
  - Average delivery time
  - Fastest delivery
  - Perfect deliveries (S rank count)
  - Favorite cargo type
- [ ] Track detailed statistics in PlayerProgressionManager
- [ ] Display in main menu or pause menu
- [ ] Add graphs/charts (optional, advanced):
  - Quest completion over time
  - Money earned per day
  - Level progression curve

**Statistics Display:**
```csharp
void DisplayStats()
{
    statsText.text = $@"
    Total Deliveries: {totalCompleted}
    Success Rate: {successRate:F1}%
    Distance Traveled: {totalDistance:F1} km
    Money Earned: ${totalMoney}
    ";
}
```

---

### Task 10.10: Final Integration & Documentation
- [ ] Ensure all systems work together:
  - Test full gameplay loop: Accept → Pickup → Deliver → Reward → Repeat
  - Verify save/load preserves all state
  - Check UI updates in response to all events
- [ ] Add code documentation:
  - XML comments on public methods
  - README file explaining quest system architecture
  - Inline comments for complex algorithms
- [ ] Create configuration ScriptableObjects:
  - QuestSystemSettings (time multipliers, reward scales, etc.)
  - Expose tuning parameters for designers
- [ ] Clean up debug code:
  - Remove unnecessary Debug.Log statements
  - Organize scene hierarchy
  - Tag and layer assignments correct
- [ ] Create video tutorial (optional):
  - Record gameplay showing quest system
  - Explain how to add new quest types
  - Show configuration in Unity Editor

**Final Checklist:**
- [ ] All quest types functional
- [ ] UI responsive and bug-free
- [ ] Saving/loading works
- [ ] Performance acceptable (60+ FPS)
- [ ] Code is clean and documented
- [ ] Balancing feels good
- [ ] No critical bugs
- [ ] Ready for build

---

## Post-Implementation Enhancements (Future Roadmap)

### Optional Advanced Features
- [ ] **Dynamic Traffic Interaction**: Delivery delays if stuck in traffic
- [ ] **Weather System**: Rain/fog affects driving, adjusts time limits
- [ ] **Vehicle Upgrades**: Buy better vehicles with faster speed/handling
- [ ] **Multiplayer Deliveries**: Compete with other players for quests
- [ ] **Story Campaign**: Scripted quest chain with narrative
- [ ] **Custom Quest Editor**: In-game tool to create custom quests
- [ ] **Leaderboards**: Global/friend rankings for fastest deliveries
- [ ] **Photo Mode**: Take pictures at delivery locations (tourism element)
- [ ] **NPC Passengers**: Pick up passengers along with cargo
- [ ] **Fuel System**: Manage fuel consumption, find gas stations

---

## Implementation Timeline Estimate

**Phase 1-2 (Core Systems)**: Week 1-2
**Phase 3-4 (UI & Zones)**: Week 2-3
**Phase 5-6 (Cargo & Timer)**: Week 3-4
**Phase 7-8 (Progression & Saving)**: Week 4-5
**Phase 9 (Generation)**: Week 5-6
**Phase 10 (Polish)**: Week 6-8

**Total Estimated Time**: 6-8 weeks (part-time development)

---

## Resources & Assets Needed

### 3D Models
- [ ] Cargo box variants (5-10 models)
- [ ] Quest marker beacon/column
- [ ] UI icons (cargo types, quest types)

### Audio
- [ ] Background music (2-3 tracks)
- [ ] Sound effects (10-15 sounds)
  - Pickup/delivery sounds
  - UI feedback sounds
  - Success/failure jingles

### UI Assets
- [ ] Button sprites
- [ ] Panel backgrounds
- [ ] Icons and badges
- [ ] Font (for UI text)

### Particles
- [ ] Marker glow effect
- [ ] Pickup burst
- [ ] Delivery celebration
- [ ] Damage sparks

---

## Testing Checklist

### Functionality Tests
- [ ] Quest acceptance flow works
- [ ] Pickup detection triggers correctly
- [ ] Delivery detection triggers correctly
- [ ] Timer counts down properly
- [ ] Rewards awarded correctly
- [ ] Fragile cargo takes damage on collision
- [ ] Multi-stop quests progress through all stops
- [ ] Save/load preserves all data
- [ ] Level up system works
- [ ] UI updates in real-time

### Edge Cases
- [ ] Accept quest then immediately abandon
- [ ] Complete quest with 0 seconds remaining
- [ ] Destroy cargo (fragile) before delivery
- [ ] Leave quest zone and re-enter
- [ ] Load save with active quest
- [ ] Multiple quests in completion queue
- [ ] Quest location spawns on top of NPC car
- [ ] Player drives out of map bounds during quest

### Performance Tests
- [ ] 60 FPS maintained with 5 active quest markers
- [ ] No memory leaks after 1 hour gameplay
- [ ] UI responsive on low-end hardware
- [ ] Save file size reasonable (< 1 MB)

---

## Completion Criteria

This quest system implementation is considered COMPLETE when:
1. ✅ Player can browse available quests
2. ✅ Player can accept and start quests
3. ✅ Player can pick up cargo at marked location
4. ✅ Player can deliver cargo to destination
5. ✅ Timer counts down and fails quest when expired
6. ✅ Rewards are awarded and progression tracked
7. ✅ Fragile cargo takes damage from collisions
8. ✅ Multi-stop quests work correctly
9. ✅ UI displays all relevant information clearly
10. ✅ Save/load system preserves game state
11. ✅ No critical bugs or crashes
12. ✅ Performance is acceptable (60 FPS target)

**When all checkboxes in this document are filled, the quest system is production-ready!**

---

## Notes & Tips

### Development Best Practices
- **Test frequently**: Don't wait until the end to test integration
- **Use version control**: Commit after completing each major task
- **Profile early**: Don't optimize prematurely, but profile often
- **Iterate on feedback**: Playtest with others and iterate on balancing
- **Keep it simple**: Don't over-engineer early - you can always refactor later
- **Document as you go**: Future you will thank you

### Common Pitfalls to Avoid
- ❌ Hardcoding values instead of using serialized fields
- ❌ Forgetting to unsubscribe from events (memory leaks)
- ❌ Not handling null references (especially in Update loops)
- ❌ Creating too many GameObjects per frame (use pooling)
- ❌ Using string comparisons for quest IDs (use constants/enums)
- ❌ Not testing on target platform until too late

### Recommended Unity Assets (Optional)
- **DOTween**: Smooth UI animations
- **TextMesh Pro**: Better text rendering (included in modern Unity)
- **Odin Inspector**: Better editor workflow
- **Rewired**: Advanced input management
- **PlayMaker**: Visual scripting for designers (if non-programmer will configure quests)

---

**Good luck with your implementation! Remember to check off each task as you complete it. 🚚📦**
