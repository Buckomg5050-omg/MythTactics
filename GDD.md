# MythTactics - Game Design Document

This document outlines the core design decisions for the MythTactics TRPG.

## 1. Core Gameplay Mechanics

### 1.1. Speed-Based Turn Order System
- **Turn Mechanism:** Units have an "Action Counter." When a unit's Action Counter reaches 1000 points, it takes its turn. After the turn, the counter resets to 0.
- **Effective Speed (Rate of Action Counter Accumulation):**
    - `EffectiveSpeed = FinalCalculatedBaseUnitSpeed + TotalEquipmentSpeedModifier (flat) + TotalStatusEffectSpeedModifier (flat)`
- **Base Unit Speed Calculation:**
    - `RawCalculatedBaseUnitSpeed = RaceSpeedBonus + ClassSpeedBonus + (Echo * 2) + (Glimmer * 1)`
        - `Echo` (Dexterity equivalent) has a factor of 2.
        - `Glimmer` (Wisdom equivalent) has a factor of 1.
        - `RaceSpeedBonus` and `ClassSpeedBonus` are flat values determined by unit's race/class.
    - `FinalCalculatedBaseUnitSpeed = Max(1, RawCalculatedBaseUnitSpeed)` (Ensures minimum speed of 1).
- **Turn Recalculation:** Turn order forecast updates if `EffectiveSpeed` changes mid-battle.

### 1.2. Movement System
- **Movement Range:** `CalculatedMoveRange = BaseMovementFromRaceClass + Floor(Echo / 5)`
    - `BaseMovementFromRaceClass` examples: Slow/Tanky=2, Standard/Versatile=3, Fast/Agile=4.
- **Terrain Costs (Movement Points per tile):**
    - Plains: 1
    - Forest: 2
    - Hills: 2
    - RockyGround: 2
    - Swamp: 3
    - ShallowWater: 2 (for walking units)
    - MountainPeak: 255 (Impassable)
    - DeepWater: 255 (Impassable)
    - Boundary: 255 (Impassable, for map edges)
    - Specific units (e.g., flyers) may have different interactions.
- **Unit Collision & Blocking:**
    - A tile can only be occupied by one unit at a time.
    - If a tile is occupied (by ally or enemy), other units cannot move onto or through that tile.

### 1.3. Square Tile System
- **Directional Movement:** 4-directional (North, East, South, West). No diagonal movement.
- **Tile Size:** Sprites are 64x64 pixels.
- **Pixels Per Unit (PPU):** Set to 64 for tile sprites, making 1 tile = 1x1 Unity world unit.
- **Tile Prefab:** To be created with renderer and potentially colliders.

## 2. Unit Characteristics and Stats

### 2.1. Primary Attributes
- **Core** (Strength equivalent, associated with Stamina Points - SP)
- **Echo** (Dexterity equivalent, associated with Charge Time - CT / Speed & Accuracy)
- **Pulse** (Constitution equivalent, associated with Vitality Points - VP)
- **Spark** (Intelligence equivalent, associated with Mana Points - MP)
- **Glimmer** (Wisdom equivalent, associated with Focus Points - FP / Evasion & Secondary Speed)
- **Aura** (Charisma equivalent, associated with Influence Points - IP)
- **Starting Base Range:** 1-20 (before racial modifiers/leveling).
- **Starting Distributions:** Defined per archetype (e.g., Tanky Warrior: High Pulse, Med Core, Low Spark; Glass Cannon Mage: High Spark, Med Glimmer, Low Pulse).

### 2.2. Attribute Scaling with Level
- **Hybrid System:**
    - Automatic class-based gains (e.g., +1 to primary stat every level, +1 to secondary stat every 2 levels, defined in `ClassDataSO`).
    - +2 allocatable attribute points per level for player units.
- **Diminishing Returns for Allocated Points:**
    - Cost to raise an attribute by +1 (using allocatable points):
        - Current Value 1-9: Costs 1 allocatable point.
        - Current Value 10-19: Costs 2 allocatable points.
        - Current Value 20-29: Costs 3 allocatable points.
        - Current Value 30-39: Costs 4 allocatable points (etc., cost increases by 1 per 10 attribute points).

### 2.3. Derived Attributes & Resources
- **Movement Range:** (See 1.2) `CalculatedMoveRange = BaseMovementFromRaceClass + Floor(Echo / 5)`.
- **Max Vitality Points (MaxVP):** `BaseVP_From_Race_And_Class + (Pulse * 5) + MaxVP_From_Equipment`.
- **Max Mana Points (MaxMP):** `BaseMP_From_Race_And_Class + (Spark * 2) + MaxMP_From_Equipment`.
- **Max Stamina Points (MaxSP):** `BaseSP_From_Race_And_Class + Core + MaxSP_From_Equipment`.
- **Max Focus Points (MaxFP):** `BaseFP_From_Race_And_Class + Glimmer + MaxFP_From_Equipment`.
- **Max Influence Points (MaxIP):** `BaseIP_From_Race_And_Class + Aura + MaxIP_From_Equipment`.
- **Physical Attack Damage Bonus:** `Floor(Core / 4)` added to base weapon damage.
- **Magical Potency Bonus (Damage/Healing):** `Floor(Spark / 4)` added to base spell power.
- **Hit Chance (Physical, d100 roll <= Hit%):**
    - `Hit% = (AttackerWeaponBaseAccuracy + Floor(AttackerEcho / 2)) - (DefenderArmorBaseEvasion + Floor(DefenderGlimmer / 2) + Floor(DefenderSpark / 4) + DefenderCoverBonusFromTile)`
    - Result clamped (e.g., 5% - 95%).
    - `AttackerWeaponBaseAccuracy`: From weapon (e.g., 60-90).
    - `DefenderArmorBaseEvasion`: From armor/shields.
    - `CoverBonusFromTile`: From `TileTypeSO` (e.g., +10 or +20).

## 3. Combat System Overview

### 3.1. Action Point (AP) System
- **Max AP:** 2 AP per unit per turn (standard).
- **AP Regeneration:** Full regeneration to max (2 AP) at start of unit's turn. No carry-over.
- **Action Costs (Examples):**
    - Move Action (use full `MoveRange`): 1 AP
    - Standard Attack: 1 AP
    - Use Standard Item: 1 AP
    - Basic Skill/Spell: 1 AP
    - Powerful Skill/Spell: 2 AP
- Special abilities can modify AP costs or grant AP.

### 3.2. Attack Resolution
- **Dice Roll:** d100 system for hit chance.
- **Critical Hits:**
    - If a standard attack roll successfully hits, a check for critical hit is made.
    - **BaseCritChance:** 5% (universal).
    - **Melee/Physical Crit Chance (Total):** `BaseCritChance + Floor(AttackerCore / 4) + Floor(AttackerEcho / 4) + Equipment/AbilityCritBonuses`.
    - **Magical Crit Chance (Total):** `BaseCritChance + Floor(AttackerSpark / 4) + Floor(AttackerGlimmer / 4) + Equipment/AbilityCritBonuses`.
    - **Crit Damage Multiplier:** 1.5x normal damage.
    - Special effects can trigger on critical hits (defined by ability/weapon).
- **Damage Types (Initial):**
    - Physical
    - Magical (general non-elemental arcane)
    - Elemental: Fire, Cold/Ice, Lightning.
- Resistances/Vulnerabilities will apply to these types.

## 4. Progression System

### 4.1. Experience Points (XP)
- **XP Awarded per Enemy Defeated:**
    - `AdjustedXPValue = BaseXPValue_from_EnemyDefinition * LevelDifferenceModifier * EnemyRankModifier`
    - **`LevelDifferenceModifier`**: `1.0 + ((EnemyLevel - AveragePartyLevel) * 0.1)`. (Result clamped, e.g., between 0.5 and 2.0).
    - **`EnemyRankModifier`**: Standard=1.0x, Elite=1.5x-2.0x, Boss=3.0x-5.0x.
- **XP Distribution:**
    - Equal distribution of total battle XP among all participating/surviving player units.

### 4.2. Leveling Up
- Units gain attributes as per section 2.2 (automatic class gains + allocatable points with diminishing returns).
- New skills/abilities may become available (defined by `ClassDataSO` or skill tree system).

## 5. Grid System Implementation (Planned)

### 5.1. Core Components

#### 5.1.1. Grid Manager
- **Responsibilities:** Oversees the entire grid.
- **Storage:** Stores the grid as a 2D array of `Tile` objects (`private Tile[,] _tiles`). Dimensions determined by loaded map (e.g., 25x25 up to 100x100 playable area).
- **Initialization:**
    - Loads map data (target format: JSON).
    - Initializes internal `_tiles` array to `(playableWidth + 2) x (playableHeight + 2)`.
    - Populates the inner `playableWidth x playableHeight` area from map data.
    - Automatically creates a 1-tile thick impassable "Boundary" `TerrainType` around the playable area.
- **Access:** Provides `public Tile GetTile(int x, int y)` (for playable coordinates, mapping to internal array) with bounds checking.
- **Coordinate System:**
    - Visual center of the grid will be at world origin (0,0).
    - Tile size is 1x1 Unity world units (64px sprites with PPU=64).
    - Implements `Vector2Int WorldToGrid(Vector3 worldPos)` and `Vector3 GridToWorld(Vector2Int gridPos)` for conversion, accounting for centered origin.
    - Helper methods: `List<Tile> GetNeighbors(Vector2Int pos, bool includeDiagonals = false)` (4-directional), `bool IsInBounds(Vector2Int pos)` (for playable area), `List<Tile> GetTilesInRange(Vector2Int center, int range)`.
- **Boundaries:**
    - Visual "Boundary" tiles define map edges.
    - Will assist in setting up invisible colliders to constrain camera movement to the playable area.
- **Event System (Observer Pattern):**
    - `public event Action<Tile, PropertyType> OnTileDataChanged;` (Invoked when a tile's data like Terrain, Occupancy, Height changes. `PropertyType` is an enum).
    - `public event Action OnGridReset;` (Invoked when a new map is fully loaded and the grid is rebuilt).

#### 5.1.2. Tile Class
- **Type:** Will be a `MonoBehaviour` attached to tile prefabs.
- **Properties:**
    - `GridPosition (Vector2Int)`: Its (x,y) in the logical playable grid.
    - `CurrentTerrainType (enum)`: e.g., Plains, Forest, Boundary. (Enum defined with types: Plains, Forest, Hills, RockyGround, Swamp, ShallowWater, MountainPeak, DeepWater, Boundary).
    - `HeightLevel (int)`: Simple integers (0, 1, 2...).
    - `OccupyingUnit (Unit)`: Reference to the unit on this tile.
    - `IsOccupied (bool)`: Read-only property based on `OccupyingUnit`.
- **Associated Data:** Retrieves data like movement cost, sprite, display name, and defensive bonuses from a `TileTypeSO` ScriptableObject corresponding to its `CurrentTerrainType`.
- **Initialization:** Method to be called by `GridManager` to set its properties.
- **Highlighting:**
    - Will have a method `SetHighlight(TileHighlightState state)` to change its visual appearance.
    - `TileHighlightState` enum: None, MovementRange, AttackRange, SelectedUnit, Hovered.

#### 5.1.3. Pathfinding System (Pathfinder Class)
- **Algorithm:** A* (A-star).
- **Heuristic (4-directional):** Manhattan Distance (`abs(dx) + abs(dy)`), scaled by min movement cost (1).
- **Movement Costs:**
    - Derived from `TileTypeSO.MovementCost` for the terrain type of each tile.
    - Occupied tiles (by any unit) are treated as impassable.
    - Considers unit-specific interactions (e.g., flying units treat most terrain as cost 1) by taking the `Unit` as a parameter for path requests.
- **Path Output:** Returns an ordered `List<Tile>` representing the path, or indicates no path found.
- **Reachable Tiles:** Will also provide functionality to find all reachable tiles within a given movement point range for a unit.
- **Path Caching:**
    - Paths cached primarily for the current turn's active unit.
    - Cache cleared/invalidated on unit's turn end or significant grid state changes.
- **Optimizations:**
    - Max search depth/iterations to prevent lag on impossible paths.
    - Multi-frame pathfinding is a "profile and optimize later if needed" feature.
    - Will likely use an internal `PathNode[]` (struct with F,G,H scores, parent, closed status) for A* working data, for performance.

### 5.2. Architecture Considerations (Grid System)

#### 5.2.1. TileTypeSO (ScriptableObject for Tile Types)
- **Purpose:** Define properties for each terrain type (e.g., "Plains.asset", "Forest.asset").
- **Properties (Examples):**
    - `DisplayName (string)`
    - `TileSprite (Sprite)`
    - `MovementCost (int)`
    - `EvasionBonus (int)` (e.g., for cover)
    - Potentially: `IsFlammable`, `IsDestructible`, `TransformToOnBurn (TileTypeSO)`, `OnTurnStartEffect (EffectSO)`, `OnOccupyEffect (EffectSO)`.
- **Stacking Bonuses:** Assumed additive for now (e.g., tile evasion + spell evasion).

#### 5.2.2. Observer Pattern for Grid State Changes (Grid System)
- Covered by `GridManager` events (`OnTileDataChanged`, `OnGridReset`).
- Active tile behaviors (damage zones, healing fountains) will likely be driven by data on `TileTypeSO` and processed by systems like `TurnManager` or `EffectSystem`.

#### 5.2.3. Data-Oriented Design for Performance (Grid System)
- **Pathfinding Data:** `Pathfinder` will use an array of `PathNode` structs for its A* calculations.
- **Dirty Flagging:** Handled via the `GridManager.OnTileDataChanged` event, allowing dependent systems to react.
- **Job System / Burst Compiler:** Advanced optimization tools to be considered later if performance profiling shows a clear need.

## 6. Unit Management System (Planned)

### 6.1. Components

#### 6.1.1. Unit Base Class (`UnitBase`)
- **Core Functionality:** Foundation for all player and AI units (will be a `MonoBehaviour`).
- **Primary Attributes Storage:**
    - Holds an instance of `UnitPrimaryAttributes` (serializable class with `Core, Echo, Pulse, Spark, Glimmer, Aura`).
    - Stores `BaseStats` (innate from race/class/level) and calculates `EffectiveStats` (including equipment/buffs/status effects).
- **Resource Management & Derived Stats:**
    - Tracks current/max for VP, AP, MP, SP, FP, IP.
    - Provides methods for consuming/regenerating these resources.
    - (Refer to section 2.3 and 3.1 for specific formulas and regeneration rules).
- **Equipment System Interaction:**
    - Will have defined equipment slots.
    - Manages equipped items and incorporates their stat modifiers and granted abilities.
- **Animation System Interaction:**
    - Holds reference to `Animator` component.
    - Methods to trigger state-driven animations (Idle, Move_Directional, Attack, Hurt, Die).
- **Other Properties:** `UnitName`, `FactionType`, `GridPosition`, `CurrentTile`, `IsAlive`.

#### 6.1.2. Unit Factory
- **Purpose:** Encapsulates creation and initialization of unit instances.
- **Unit Templates (`UnitTemplateSO`):** ScriptableObjects defining unit blueprints.
    - `UnitName (string)`, `Portrait (Sprite)`, `UnitPrefab (GameObject)`.
    - `BaseAttributes (UnitPrimaryAttributes)`: Starting attributes at level 1.
    - `UnitClassData (ClassDataSO)`: Reference to SO defining class specifics.
    - `DefaultFaction (FactionType)`.
    - `StartingAbilities (List<AbilitySO>)`.
    - `EnemySkillPool (List<WeightedAbilityEntry>)` (entry includes AbilitySO, weight, prerequisites).
    - `AllowsStatVariation (bool)` (for enemies).
- **Class Data (`ClassDataSO`):** ScriptableObjects defining class details.
    - `ClassName`, `Description`.
    - Automatic attribute gains per level.
    - Skill trees / ability progression rules.
    - Equipment proficiencies.
    - Base contributions to VP, MP, SP, FP, IP.
- **Level Scaling:** Calculates attributes based on unit level, applying automatic gains from `ClassDataSO`.
- **Enemy Variation:**
    - Applies minor randomization (+/- percentage) to primary attributes of created enemies.
    - Selects enemy skills from defined pools using weighted probabilities and prerequisites.

#### 6.1.3. Unit Controller (Player Input Handling)
- **Purpose:** Translates player input into actions for player-controlled units.
- **Input:** Uses Unity's Input System.
- **State Machine:** Manages input flow (e.g., SelectUnit -> SelectActionMenu -> SelectTarget -> ConfirmAction -> ExecuteAction).
- **Visualizations:** Works with `TileHighlightManager` for movement/attack ranges, path preview.
- **Action Confirmation & Preview:** Displays expected results (damage, hit chance) before commitment. Allows cancellation.
- **Undo Movement:** Allows undoing a move action if no subsequent combat action taken (refunds AP, reverts position).

#### 6.1.4. AI Controller (Computer-Controlled Units)
- **Purpose:** Manages decision-making and actions for AI units.
- **Decision-Making (Utility AI approach):** Perception -> Goal Setting (based on personality: Aggressive, Defensive, Support) -> Action Evaluation -> Scoring Actions -> Action Selection -> Execution.
- **Considers:** Threat assessment, damage/healing output, AP/resource costs, success probability, tactical positioning (terrain, formations), target priority (focus fire, weakest, high-value).
- **Difficulty Scaling:** Higher difficulty = more optimal choices; lower difficulty = more sub-optimal choices/randomness.

### 6.2. State Management (Unit Behaviors & Turns)

#### 6.2.1. State Pattern for Unit Behaviors
- `UnitBase` uses a state machine (`UnitState` base class): `UnitIdleState`, `UnitMovingState`, `UnitActingState`, `UnitDeadState`.
- Each state has `Enter()`, `Execute()`, `Exit()` methods.

#### 6.2.2. Resource Tracking & Regeneration (in `UnitBase`)
- **VP:** Max: `BaseVP + (Pulse * 5) + Equip`. Regen: Effects/skills in combat; full between battles.
- **AP:** Max: 2 (default). Regen: Full at start of unit's turn.
- **MP:** Max: `BaseMP + (Spark * 2) + Equip`. Regen: +1 or +2 per turn (default).
- **SP:** Max: `BaseSP + Core + Equip`. Regen: +1 per turn (default).
- **FP:** Max: `BaseFP + Glimmer + Equip`. Regen: +1 per turn (default).
- **IP:** Max: `BaseIP + Aura + Equip`. Regen: +1 per turn (default).
- Events fired on resource changes for UI updates.

#### 6.2.3. Turn Transitions (Handled by `TurnManager`/`CombatManager`)
- **Turn Order Mechanism:** "Action Counter to 1000".
    - Unit acts when `ActionCounter >= 1000`. Resets to 0 after turn.
- **Simultaneous Turn Tie-Breaking:** Highest `ActionCounter` -> `EffectiveSpeed` -> `Echo` -> `Glimmer` -> Random/Fixed.
- **Turn Phases:** Start of Turn (effects, regen) -> Action Phase (player/AI acts) -> End of Turn (effects, cleanup).
- **Event Hooks (in `TurnManager`):** `OnUnitTurnStart(UnitBase)`, `OnUnitTurnEnd(UnitBase)`.

## 7. Combat System (Planned)

### 7.1. Components

#### 7.1.1. Combat Manager
- **Purpose:** Orchestrates combat sequences.
- **Hit/Crit Resolution:** As defined in sections 2.3 and 3.2. Considers ability-specific accuracy modifiers.
- **Combat Action Queue/Sequencing:** Manages multi-hit abilities, simple counter-attacks.
- **Ability Targeting System:** Validates targets based on `AbilitySO` (Type, Range, LoS).
    - **LoS:** Units do NOT block. Terrain of sufficient height DOES block.
    - **AoE:** `AbilitySO` defines pattern (`Square`, `Cone`, `Line`, `Circle/Radius`) and size.
- **Execution Flow:** Verify costs -> Resolve Hit/Crit -> Call `DamageCalculator` -> Apply Damage/Effects -> VFX/SFX -> Reactions.

#### 7.1.2. Damage Calculator
- **Purpose:** Determines final damage after modifiers and mitigations.
- **Calculation Steps:**
    1. Base Outgoing Damage (Physical: `BaseWeaponDamage + Floor(Core / 4)`; Magical: `BaseSpellPower + Floor(Spark / 4)`).
    2. Apply 1.5x Crit Multiplier if critical.
    3. Apply Attacker's general damage % modifiers.
    4. Apply Defender's Mitigations:
        - True Damage skips mitigation.
        - Physical: `PDR% = ArmorValue / (ArmorValue + K_ArmorConstant)`. Apply Armor Penetration.
        - Magical/Elemental: Net Damage Multiplier from additive Resistances/Vulnerabilities.
    5. Apply +/- 10% Damage Variance.
    6. Ensure Minimum 1 Damage.
- **Damage Types:** Physical, Magical, Elemental (Fire, Cold/Ice, Lightning).

#### 7.1.3. Effect System
- **Purpose:** Manages status effects (buffs, debuffs).
- **`EffectSO` (Blueprints):** Define `EffectID`, `Name`, `Icon`, `Duration`, `MaxStacks`, `StackingBehavior`, `StatModifiers`, `EffectTriggers` (e.g., OnTurnStart: Deal X Poison Damage), `EffectTypeTags`.
- **`ActiveStatusEffect` (Instance on Unit):** `EffectSO` ref, `RemainingDuration`, `CurrentStacks`, `Caster`.
- **`UnitBase` Integration:** `UnitBase.ActiveEffects` list. Derived stats sum modifiers from active effects.
- **`EffectSystem` Manager:** `ApplyEffect`, `RemoveEffect`, `ProcessTurnStart/EndEffects`. Duration ticks at end of affected unit's turn. Handles immunities/cleansing via `EffectTypeTags`.

## 8. UI Development (Planned)

### 8.1. Core UI Components

#### 8.1.1. Unit Information Panel
- **Access:** Between battles (full character sheet) & during combat ("Info" button).
- **Content:** Basic (combat default) & Detailed views. Shows primary attributes (base/effective), derived stats (with calculation tooltips), active effects, equipment.
- **Interaction (Between Battles):** Equip/unequip items, inventory comparison, stat change previews.
- **Updates:** Event-driven refresh from `UnitBase`.

#### 8.1.2. Action Menu
- **Style:** Radial pop-up menu around selected player unit.
- **Main Buttons:** Move, Attack, Skills (submenu), Spells (submenu), Items (submenu), Wait/Defend, Info.
- **Context-Sensitive:** Disabled actions grayed out, tooltips show costs/reasons.
- **Submenus:** Secondary radial or list/grid panels for skills, spells, items.

#### 8.1.3. Turn Order Display
- **Style:** Timeline-based (horizontal/vertical), top-center of screen.
- **Content:** Forecasts next 5 units (expandable). Portrait position on timeline reflects `ActionCounter`. Active unit highlighted. Status icons. Tooltip on hover (Name, VP).
- **Updates:** Dynamic, from `TurnManager`.

#### 8.1.4. Combat Log
- **Style:** Expandable/collapsible scrollable text area in a screen corner.
- **Content:** Color-coded history of combat events (damage, healing, abilities, status, movement).
- **Formatting:** Turn number/unit name prefixes, dividers between turns.
- **Filtering (Initial):** Toggles for Combat, Status, Movement.

### 8.2. UI Architecture

#### 8.2.1. Architectural Approach (Simplified Model-View)
- **Model:** Game logic classes (`UnitBase`, managers).
- **View:** Unity UI GameObjects with scripts (`UnitInfoPanelUI.cs`, etc.).
- **Interaction:** UI scripts directly access Model data. Event-driven refresh (UI subscribes to Model events). Button clicks call methods on game logic controllers.

#### 8.2.2. Reusable UI Components (Prefabs)
- **`StatBar`:** Displays value (Health, Mana etc.) with fill, text, optional gradient.
- **`ButtonGroup` / Navigation:** Manages selection/input for button groups.
- **`TooltipSystem`:** Global manager for contextual tooltips (positioning, sizing).
- **`DialogSystem`:** Global manager for modal confirmations & non-modal info pop-ups.

#### 8.2.3. UI Manager (Global Control)
- **Panel Management:** Shows/hides UI panels/screens.
- **Layer System:** Ensures correct rendering order (via Canvases or layer containers).
- **Input Context Switching:** Works with `InputManager` (Action Maps) for InGame vs. MenuUI input.
- **UI Animation (Coordination):** May coordinate standard panel open/close animations.

## 9. AI Integration (Developer Workflow)

### 9.1. For Code Generation
- **Method:** Use external AI code assistants (e.g., ChatGPT, Copilot) with specific, detailed prompts.
- **Process:**
    1. Define clear requirements, input/output specifications, function signatures.
    2. Use for drafting algorithms (e.g., A*), state machine boilerplate, common patterns (Commands, SO templates), utility functions.
    3. **CRITICAL:** Thoroughly review, test, debug, and adapt AI-generated code. Understand it fully.
    4. Document AI-generated code blocks (e.g., with headers indicating source/prompt) and write unit tests.
- **Treat AI as an assistant, not a replacement.** Developer is responsible for code quality.

### 9.2. For Sprite Generation
- **Method:** Use external AI image generation tools (e.g., Midjourney, Stable Diffusion) for initial drafts and inspiration.
- **Process:**
    1. **Define Art Style Guidelines:** Clear vision for style (pixel art, painterly), dimensions (characters, tiles: 64x64), animation frame needs, perspective (3/4 top-down for characters), color palettes, reference sheets.
    2. **AI for Drafts:** Generate base poses, tile textures, UI icons, VFX concepts using iterative prompting.
    3. **Post-Processing & Cleanup (Manual):**
        - Convert to specific style (e.g., pixel art cleanup in Aseprite/Photoshop).
        - Ensure consistent dimensions, set pivot points correctly.
        - Create sprite sheets for animations.
        - Manually touch up details, add custom elements, combine AI outputs.
- **Human skill in image editing is essential.** Be mindful of ethical/legal terms of AI tool usage.