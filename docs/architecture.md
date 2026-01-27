# ChaosWarlords Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords` codebase, a digital adaptation of the board game *Chaos Warlords*. The design utilizes **Dependency Injection**, **Event-Driven Architecture**, and **Interface-Based Abstraction** to ensure testability, maintainability, and support for a future Multiplayer (Headless) port.

**Key Design Goals**:
- **Testability**: All components can be unit tested in isolation
- **Multiplayer Ready**: Logic separated from rendering for headless server support
- **Deterministic**: Seeded RNG and action sequencing for replay/sync
- **Maintainable**: Clear separation of concerns and single responsibility

---

## Directory Structure & File Listing

The project uses a semantic folder structure. Below is a detailed listing of all files and their responsibilities.

```text
ChaosWarlords/                   # Project Root
├── ChaosWarlords.csproj         # Project File
├── app.manifest                 # Windows Application Manifest
├── Program.cs                   # Application Entry Point
├── Game1.cs                     # MonoGame Main Loop
└── Source/
    ├── Core/
    │   ├── Composition/                     # Dependency Injection composition roots
    │   │   └── GameDependencies.cs          # Concrete dependency container
    │   ├── Contexts/                        # Data Holders (The "Glue")
    │   │   ├── EffectContext.cs             # Context for stack-based effect execution
    │   │   ├── ExecutedAction.cs            # Record capturing a single game event
    │   │   ├── MatchContext.cs              # Scoped DI container for a single match
    │   │   └── TurnContext.cs               # Transient state for current turn
    │   ├── Data/
    │   │   ├── Dtos/                        # Data Transfer Objects
    │   │   │   ├── CardDto.cs               # Serializable card data
    │   │   │   ├── CommandDto.cs            # Serializable command data
    │   │   │   ├── EffectContextDto.cs      # Serializable effect stack state
    │   │   │   ├── GameStateDto.cs          # Serializable game state snapshot
    │   │   │   ├── MapDto.cs                # Serializable map data
    │   │   │   ├── PlayerDto.cs             # Serializable player data
    │   │   │   ├── ReplayDataDto.cs         # Serializable replay container
    │   │   │   ├── ScoreBreakdownDto.cs     # Serializable victory score details
    │   │   │   └── VictoryDto.cs            # Serializable victory state data
    │   │   ├── Enums/                       # New home for Enums
    │   │   │   └── CommandType.cs           # Enum for command identification
    │   │   └── LogicVector2.cs              # Deterministic integer vector struct
    │   ├── Events/                          # Event System
    │   │   ├── GameEvent.cs                 # Base record for all game events
    │   │   ├── InputEventArgs.cs            # Event args for input interactions
    │   │   └── StateChangeEvent.cs          # Event for state mutations
    │   ├── Interfaces/                      # Contracts (API Definitions)
    │   │   ├── Composition/
    │   │   │   └── IGameDependencies.cs     # Service container interface
    │   │   ├── Data/
    │   │   │   ├── ICardDatabase.cs         # Contract for retrieving card definitions
    │   │   │   └── IDto.cs                  # Marker interface for DTOs
    │   │   ├── Input/
    │   │   │   ├── IGameplayInputCoordinator.cs
    │   │   │   ├── IInputManager.cs
    │   │   │   ├── IInputMode.cs
    │   │   │   ├── IInputProvider.cs
    │   │   │   └── IInteractionMapper.cs
    │   │   ├── Logic/
    │   │   │   ├── IActionSystem.cs
    │   │   │   ├── ICommandValidator.cs
    │   │   │   └── IGameCommand.cs
    │   │   ├── Rendering/
    │   │   │   ├── IButtonManager.cs
    │   │   │   ├── IGameplayView.cs
    │   │   │   ├── IMainMenuView.cs
    │   │   │   ├── IUIManager.cs
    │   │   │   └── IVictoryView.cs
    │   │   ├── Services/
    │   │   │   ├── ICommandDispatcher.cs
    │   │   │   ├── IEventManager.cs
    │   │   │   ├── IGameLogger.cs
    │   │   │   ├── IGameRandom.cs
    │   │   │   ├── IMapManager.cs
    │   │   │   ├── IMarketManager.cs
    │   │   │   ├── IMarketStateManager.cs
    │   │   │   ├── IMatchManager.cs
    │   │   │   ├── INetworkProvider.cs      # Abstraction for network transport
    │   │   │   ├── IPlayerStateManager.cs
    │   │   │   ├── IReplayManager.cs
    │   │   │   ├── ITurnManager.cs
    │   │   │   ├── IUIEventMediator.cs
    │   │   │   └── IVictoryManager.cs
    │   │   └── State/
    │   │       ├── IDrawableState.cs
    │   │       ├── IGameplayState.cs
    │   │       ├── IState.cs
    │   │       └── IStateManager.cs
    │   └── Utilities/                       # Infrastructure & Constants
    │       ├── BufferedAsyncLogger.cs       # Async-optimized logging
    │       ├── CardDatabase.cs              # Implementation of card library
    │       ├── DtoMapper.cs                 # Mapping logic between Entities and DTOs
    │       ├── GameConstants.cs             # Global configuration values
    │       ├── GameEnums.cs                 # Enums (PlayerColor, ResourceType, etc.)
    │       ├── MapGenerationConfig.cs       # Parameters for procedural map generation
    │       ├── MapGeometry.cs               # Deterministic geometry helper (LogicVector2 based)
    │       ├── MapLayoutEngine.cs           # Procedural map generation logic
    │       ├── ObjectPool.cs                # Generic object pooling implementation
    │       ├── PooledRectangle.cs           # Disposable wrapper for Rectangles
    │       ├── PooledVector2.cs             # Disposable wrapper for Vector2s
    │       ├── SeededGameRandom.cs          # Deterministic RNG implementation
    │       ├── TextCache.cs                 # Caches string measurements
    │       └── ValidationResult.cs          # Standardized validation response
    │
    ├── Entities/                            # Domain Models (Pure Data + Behavior)
    │   ├── Actors/
    │   │   └── Player.cs                    # Represents a human or AI player
    │   ├── Cards/
    │   │   ├── Card.cs                      # Data model for a playable card
    │   │   ├── CardEffects.cs               # Definitions for card effects
    │   │   ├── Deck.cs                      # Manages a collection of cards
    │   │   └── EffectCondition.cs           # Condition requirements for effects
    │   └── Map/
    │       ├── CitySite.cs                  # Represents a Capturable City
    │       ├── MapNode.cs                   # A graph node representing a location
    │       ├── NonCitySite.cs               # Represents a neutral/resource site
    │       ├── Route.cs                     # A path connection between two MapNodes
    │       ├── Site.cs                      # Abstract base class for all sites
    │       └── StartingSite.cs              # Special site where players spawn
    │
    ├── Factories/                           # Object Creation Logic
    │   ├── CardFactory.cs                   # Creates Card instances from data
    │   ├── MapFactory.cs                    # Generates the map graph and nodes
    │   ├── MatchFactory.cs                  # Assembles all dependencies for a new match
    │   └── WrapperFactory.cs
    │
    ├── GameStates/                          # Application State Machine
    │   ├── GameplayState.cs                 # The Core Game Loop (Logic Only)
    │   ├── MainMenuState.cs                 # Entry Point / Composition Root
    │   ├── StateManager.cs                  # Stack-based State Machine implementation
    │   └── VictoryState.cs                  # Post-game summary state
    │
    ├── Input/                               # Human Interface Layer
    │   ├── Controllers/
    │   │   ├── PlayerController.cs          # High-Level Intent Parser
    │   │   └── ReplayController.cs          # Replay Workflow Orchestrator
    │   ├── Modes/                           # Input State Machine
    │   │   ├── DevourInputMode.cs           # Input mode for trashing a card
    │   │   ├── MarketInputMode.cs           # Input mode for interacting with market
    │   │   ├── NormalPlayInputMode.cs       # Default input mode for standard play
    │   │   ├── PromoteInputMode.cs          # Input mode for upgrading units/sites
    │   │   └── TargetingInputMode.cs        # Input mode for selecting targets
    │   ├── Processors/
    │   │   ├── GameplayInputCoordinator.cs  # Orchestrates input flow
    │   │   └── InteractionMapper.cs         # Translates Screen(X,Y) -> Entity
    │   └── Services/
    │       ├── InputManager.cs              # Raw MonoGame Input Wrapper
    │       └── MonoGameInputProvider.cs     # Concrete provider for MonoGame input
    │
    ├── Managers/                            # Business Logic Services
    │   ├── CommandDispatcher.cs             # Central Command Processor
    │   ├── EventManager.cs                  # Pub/Sub event system backend
    │   ├── GameEventLogger.cs               # Logs events for debugging/replay
    │   ├── MapManager.cs                    # Facade for Board Logic
    │   ├── MarketManager.cs                 # Manages the Card Market
    │   ├── MarketStateManager.cs            # Manages logic for market interactions
    │   ├── MatchManager.cs                  # Manages Match & Victory
    │   ├── PlayerStateManager.cs            # Centralized player mutations
    │   ├── PoolManager.cs                   # Manages object pools and contexts
    │   ├── ReplayManager.cs                 # Replay recording and playback
    │   ├── TurnManager.cs                   # Manages Turn Order and Phase Transitions
    │   ├── UIEventMediator.cs               # Decouples Game Logic from UI Events
    │   ├── UIManager.cs                     # Manages layout and state of UI widgets
    │   └── VictoryManager.cs                # Manages victory conditions
    │
    ├── Map/                                 # Map-Specific Subsystems
    │   ├── CombatResolver.cs                # Determines outcomes of battles
    │   ├── MapRewardSystem.cs               # Calculates resource generation
    │   ├── MapTopology.cs                   # Pathfinding logic
    │   └── SpyOperations.cs                 # Handles spy placement and removal
    │
    ├── Mechanics/                           # The "Rules" of the Game
    │   ├── Actions/
    │   │   ├── Subsystems/                  # Logic Sub-modules
    │   │   │   ├── DevourSubsystem.cs       # Devour mechanics
    │   │   │   └── SpySubsystem.cs          # Spy mechanics
    │   │   ├── ActionSystem.cs              # Handles targeting logic
    │   │   ├── CardPlaySystem.cs            # Validates and conducts card plays
    │   │   └── PreTargetHandler.cs          # Internal helper for pre-target auto-execution
    │   ├── Commands/                        # Command Pattern Implementations
    │   │   ├── ActionCompletedCommand.cs    # Signals action completion
    │   │   ├── AssassinateCommand.cs        # Execute assassination
    │   │   ├── BuyCardCommand.cs            # Purchase card
    │   │   ├── CancelActionCommand.cs       # Cancel targeting
    │   │   ├── DeployTroopCommand.cs        # Place unit
    │   │   ├── DevourCardCommand.cs         # Trash card
    │   │   ├── EndTurnCommand.cs            # End turn
    │   │   ├── MoveTroopCommand.cs          # Move unit between nodes
    │   │   ├── PlaceSpyCommand.cs           # Place spy on site
    │   │   ├── PlayCardCommand.cs           # Play card
    │   │   ├── PromoteCommand.cs            # Upgrade unit/site
    │   │   ├── ResolveSpyCommand.cs         # Execute spy action
    │   │   ├── ReturnTroopCommand.cs        # Return unit to hand
    │   │   ├── StartAssassinateCommand.cs   # Initiate assassination
    │   │   ├── StartReturnSpyCommand.cs     # Initiate spy return
    │   │   ├── SupplantCommand.cs           # Replace enemy unit
    │   │   ├── SwitchToNormalModeCommand.cs # Reset input mode
    │   │   └── ToggleMarketCommand.cs       # Open/Close market
    │   └── Rules/                           # Pure Logic Engines
    │       ├── CardEffectProcessor.cs       # Applies card effects
    │       ├── CardRuleEngine.cs            # Validates card conditions
    │       ├── DevourStrategyFactory.cs     # Strategy pattern for devour operations
    │       ├── MapRuleEngine.cs             # Validates map rules
    │       ├── SiteControlSystem.cs         # Manages site ownership
    │       └── TargetingStateEngine.cs      # Determines targeting state sequences
    │
    └── Rendering/                           # Presentation Layer (The "View")
        ├── UI/                              # UI Components
        │   ├── ButtonManager.cs             # Handles button registration
        │   ├── ButtonRenderer.cs            # Renders buttons
        │   ├── CardCollectionBrowser.cs     # Browser UI for card collections
        │   ├── OptionalEffectPopup.cs       # Popup for optional choices
        │   ├── Popup.cs                     # Base popup class
        │   ├── PopupBuilder.cs              # Fluent builder for popups
        │   ├── SimpleButton.cs              # Basic UI button implementation
        │   └── UIRenderer.cs                # General UI rendering
        ├── ViewModels/                      # MVVM State
        │   └── CardViewModel.cs             # View-Logic wrapper for Card
        ├── Views/                           # Concrete Views
        │   ├── GameplayView.cs              # Main gameplay renderer
        │   ├── MainMenuView.cs              # Main Menu screen renderer
        │   └── VictoryView.cs               # Victory screen renderer
        └── World/                           # In-Game Object Renderers
            ├── CardRenderer.cs              # Draws individual cards to screen
            └── MapRenderer.cs               # Draws the hex map and units
```

---

## Key Systems Breakdown
### 1. Decoupled Rendering System
The architecture supports multiplayer by strictly separating Game Logic from Rendering. `GameplayState` (Logic) delegates all visualization to the `IGameplayView` interface, ensuring it never depends on `GraphicsDevice` or MonoGame types directly.

To maintain 60 FPS performance, the rendering layer enforces a **Zero-Allocation** policy using `ObjectPool<T>` (via `PooledRectangle` and `PooledVector2`). This prevents GC spikes during the render loop.

### 2. Input Coordination System
Input is handled via a **Event-Driven** tiered approach:
1.  **InputManager** detects raw key/mouse states and fires `OnInputEvent`.
2.  **GameplayInputCoordinator** subscribes to events and checks **Blocking Logic** (e.g. `IsInputBlocked()` returns true if Pause/Popup is open).
3.  **IInputMode Strategy** (Normal, Targeting, Market) interprets the specific context of the input ONLY if not blocked.
4.  **PlayerController** remains available for high-level global intents (like Toggle Menu) but discrete gameplay interactions flow through the Coordinator.

### 3. Command Pattern (Mechanics/Commands/)
All significant game actions (Move, Attack, Buy) are encapsulated in `IGameCommand` objects. This ensures traceability, enables replay systems by re-executing commands, and supports multiplayer synchronization.

### 4. Transactional Command Execution
Complex multi-step actions (like Devour mechanics) utilize the `ActionSystem` with a **Stack-Based Architecture** (`EffectContext` Stack). This allows actions to be paused (e.g., waiting for user input on an optional effect), new actions to be pushed and resolved (nested transactions), and then the original action to resume. Targets are buffered until the entire chain is valid (`Deferred Execution`).

### 5. Card Rule Engine
Card logic is validated by a centralized `CardRuleEngine` using a Chain of Responsibility pattern. `EffectCondition` definitions allow data-driven rules (defined in JSON), separating validation logic from effect execution.

### 6. Multiplayer Readiness

The architecture now includes concrete infrastructure for network synchronization:

**State Verification:**
- **State Hashing**: `MatchContext.GetStateHash()` generates deterministic hashes for desync detection
- **Hash Coverage**: Sequence numbers, turn metadata, map state, player resources, market state
- **Culture-Invariant**: Uses `InvariantCulture` for consistent formatting across locales

**Network Abstraction:**
- **INetworkProvider Interface**: Defines contract for command transmission and state sync
- **Transport Agnostic**: Supports future implementations (Local/SignalR/TCP)
- **Event-Driven**: Callbacks for `OnCommandReceived` and `OnStateReceived`
- **Async Operations**: All network calls use `Task` for non-blocking I/O

**Snapshot Serialization:**
- **Full State Capture**: `DtoMapper.ToGameStateDto()` serializes entire game state
- **Effect Stack Serialization**: `EffectContextDto` captures mid-action state for reconnection
- **Transient State Handling**: Marked-for-devour cards and pending effects included

**Existing Infrastructure:**
- **Centralized Mutation**: All resource changes flow through `IPlayerStateManager`
- **Action Sequencing**: Commands track sequence numbers for ordering
- **Seeded RNG**: `IGameRandom` ensures identical random sequences across all clients
- **Context Isolation**: `PoolManager` maintains separate object pools for logic (server) and rendering (client) to prevent state leaks
- **Separation of Concerns**: Logic never touches UI, allowing headless execution


### 7. MatchContext vs IGameplayState: Separation of Concerns

**MatchContext** and **IGameplayState** serve distinct, complementary purposes:

#### MatchContext (Pure Game State & Logic)
- **Purpose**: Scoped DI container holding all game state and logic for a single match
- **Contains**: Managers, systems, and game state (players, cards, map, etc.)
- **Used By**: Game logic, commands, rules engines
- **Properties**:
  - `TurnManager`, `MapManager`, `MarketManager`, `MatchManager`
  - `ActionSystem`, `CardRuleEngine`, `PlayerStateManager`
  - `ActivePlayer`, `VoidPile`, `Random`
- **Key Enhancement (2026-01)**: Added `MatchManager` property for direct access in commands

#### IGameplayState (UI State Machine & Input Coordination)
- **Purpose**: Manages UI state, input modes, and user interaction flow
- **Contains**: UI managers, input coordinators, state machine logic
- **Used By**: Input controllers, UI mediators, rendering layer
- **Properties**:
  - `IsPauseMenuOpen`, `IsMarketOpen`, `IsConfirmationPopupOpen`
  - `UIManager`, `MarketStateManager`, `InputCoordinator`
  - `SwitchToNormalMode()`, `SwitchToTargetingMode()`, `RecordAndExecuteCommand()`
- **Accesses**: `MatchContext` via `MatchContext` property for game state queries

#### Why Both Are Needed
- **MatchContext**: Headless-compatible, contains no UI dependencies, can run on server
- **IGameplayState**: Client-only, manages UI state machine and user interaction
- **Clear Boundary**: Game logic never touches UI state; UI layer accesses game state via MatchContext

This separation enables:
- ✅ Headless server support (MatchContext only)
- ✅ Clean testing (mock MatchContext for logic tests, mock IGameplayState for UI tests)
- ✅ Single Responsibility Principle (each handles one concern)

---

## Design Patterns Used

### 1. Dependency Injection
Used throughout the codebase to ensure testability and decoupling. Dependencies are strictly constructor-injected.

### 2. Strategy Pattern
Used in Input Modes (`IInputMode`) to handle different interaction contexts (Normal vs Targeting) effectively.

### 3. Command Pattern
Used for all game actions (`IGameCommand`) to support replay capabilities, undo functionality, and network serialization.

### 4. Parameter Object
The `IGameDependencies` object groups core services to simplify composition roots and prevent constructor explosion.

---

## Code Quality & Maintainability

### Cyclomatic Complexity Reduction (2026-01)
The codebase underwent significant refactoring to reduce cyclomatic complexity and improve maintainability:

**Refactored Components:**
- `ActionSystem.StartTargeting`: Reduced from CC 26 to 6 (77% reduction)
- `TargetingStateEngine.TraverseForNext`: Reduced from CC 26 to 8 (69% reduction)
- `CardEffectProcessor.ApplyDevour`: Reduced from CC 16 to 6 (63% reduction)
- `CardEffectProcessor.ProcessNextEffect`: Reduced from CC 14 to 7 (50% reduction)

**New Helper Classes:**
1. **PreTargetHandler** (Internal) - Encapsulates pre-target auto-execution logic extracted from `ActionSystem`
   - Handles target type detection (Devour, MapNode, Site)
   - Manages target consumption to prevent "zombie" executions
   - Single responsibility: pre-selected target execution

2. **DevourStrategyFactory** (Internal) - Strategy pattern for devour operations
   - `IDevourStrategy` interface with location-specific implementations
   - `DevourFromHandStrategy`, `DevourFromMarketStrategy`, `DevourFromDeckStrategy`, `DevourFromInnerCircleStrategy`
   - Eliminates conditional complexity in `CardEffectProcessor`

**Benefits:**
- All methods now meet industry standards (CC ≤ 10)
- Improved testability through focused, single-purpose methods
- Enhanced readability with reduced nesting depth (5 → 2)
- Better adherence to SOLID principles (Single Responsibility, Strategy Pattern)
- Zero regressions (713/713 tests passing)

### Encapsulation Hardening (2026-01)
To prevent "state leaks" and ensure system stability:
1. **Player Encapsulation**: Inherently unsafe collections (`List<Card>`) in `Player.cs` were replaced with `IReadOnlyList<Card>`. Helper methods like `AddToHand` were restricted to `internal` scope, accessible only by Managers.
2. **Match Construction**: The "God Object" anti-pattern in `GameplayState` was resolved by moving complex object graph construction to `MatchFactory`.
3. **Null Safety**: `SiteControlSystem` and other Managers defined strict constructor contracts, throwing `ArgumentNullException` immediately if dependencies are missing.

### Performance & Optimization
**New standard as of 2026-01**:
- **Zero-Allocation Rendering**: All "Hot Path" rendering loops (UI, Map, Cards) utilize `ObjectPool<T>` via `PooledRectangle` and `PooledVector2` wrappers.
- **Allocation Budget**: `UIRenderer`, `MapRenderer`, and `CardRenderer` have a budget of **0 allocations per frame**.
- **Context Awareness**: The `PoolManager` ensures that pooled objects are not shared between simulation ticks (e.g. server logic) and rendering frames (client view), preventing race conditions in future threaded implementation.

