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
Solution Root
├── ChaosWarlords.sln            # Visual Studio Solution
├── README.md                    # Project Documentation
├── docs/                        # Architecture & Testing Specs
└── ChaosWarlords/               # Project Directory
    ├── ChaosWarlords.csproj     # Project File
    ├── app.manifest             # Windows Application Manifest
    ├── Program.cs               # Application Entry Point
    ├── Game1.cs                 # MonoGame Main Loop
    └── Source/
        ├── Core/
│   ├── Composition/                     # Dependency Injection composition roots
│   │   └── GameDependencies.cs          # Concrete dependency container
│   ├── Contexts/                        # Data Holders (The "Glue")
│   │   ├── ExecutedAction.cs            # Record capturing a single game event
│   │   ├── MatchContext.cs              # Scoped DI container for a single match
│   │   └── TurnContext.cs               # Transient state for current turn
│   ├── Data/
│   │   └── Dtos/                        # Data Transfer Objects
│   │       ├── CardDto.cs               # Serializable card data
│   │       ├── CommandDto.cs            # Serializable command data
│   │       ├── GameStateDto.cs          # Serializable game state snapshot
│   │       ├── MapDto.cs                # Serializable map data
│   │       ├── PlayerDto.cs             # Serializable player data
│   │       ├── ReplayDataDto.cs         # Serializable replay container
│   │       ├── ScoreBreakdownDto.cs     # Serializable victory score details
│   │       └── VictoryDto.cs            # Serializable victory state data
│   ├── Events/                          # Event System
│   │   ├── GameEvent.cs                 # Base record for all game events
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
│       ├── CardDatabase.cs              # Implementation of the card library
│       ├── DtoMapper.cs                 # Mapping logic between Entities and DTOs
│       ├── GameConstants.cs             # Global configuration values
│       ├── GameEnums.cs                 # Enums (PlayerColor, ResourceType, etc.)
│       ├── MapGenerationConfig.cs       # Parameters for procedural map generation
│       ├── MapGeometry.cs               # Helper for hexagonal grid math
│       ├── MapLayoutEngine.cs           # Procedural map generation logic
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
│   └── MatchFactory.cs                  # Assembles all dependencies for a new match
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
│   │   └── InteractionMapper.cs         # Translates Screen(X,Y) → Entity
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
│   │   ├── ActionSystem.cs              # Handles targeting logic (Refactored: CC 26→6)
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
│       ├── CardEffectProcessor.cs       # Applies card effects (Refactored: CC 14→7, 16→6)
│       ├── CardRuleEngine.cs            # Validates card conditions
│       ├── DevourStrategyFactory.cs     # Strategy pattern for devour operations
│       ├── MapRuleEngine.cs             # Validates map rules
│       ├── SiteControlSystem.cs         # Manages site ownership
│       └── TargetingStateEngine.cs      # Determines targeting state sequences (Refactored: CC 26→8)
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
The architecture supports multiplayer by strictly separating Game Logic from Rendering. `GameplayState` (Logic) delegates all visualization to the `IGameplayView` interface, ensuring it never depends on `GraphicsDevice` or MonoGame types directly. This allows the server to run with a `NullGameplayView` while clients use full rendering.

### 2. Input Coordination System
Input is handled via a tiered approach:
1. **InputManager** detects raw key/mouse states.
2. **PlayerController** translates raw input into high-level intent (e.g., "Player wants to Play Card").
3. **GameplayInputCoordinator** orchestrates the intent, checking validity and delegating execution.
4. **IInputMode Strategy** (Normal, Targeting, Market) interprets the specific context of the input (e.g., clicks select targets vs playing cards).

### 3. Command Pattern (Mechanics/Commands/)
All significant game actions (Move, Attack, Buy) are encapsulated in `IGameCommand` objects. This ensures traceability, enables replay systems by re-executing commands, and supports multiplayer synchronization.

### 4. Transactional Command Execution
Complex multi-step actions (like Devour mechanics) utilize the `ActionSystem` (and its **Subsystems**) with **Deferred Execution** to support atomic transactions. Targets are buffered until the entire chain is valid, preventing partial state changes. If a user cancels partway through, the transaction is aborted with no state change.

### 5. Card Rule Engine
Card logic is validated by a centralized `CardRuleEngine` using a Chain of Responsibility pattern. `EffectCondition` definitions allow data-driven rules (defined in JSON), separating validation logic from effect execution.

### 6. Multiplayer Readiness
To ensure synchronization without shared memory:
- **Centralized Mutation**: All resource changes flow through `IPlayerStateManager`.
- **Action Sequencing**: Actions are assigned sequence numbers.
- **Seeded RNG**: `IGameRandom` ensures identical random number sequences across all clients.

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
- Zero regressions (600/600 tests passing)
