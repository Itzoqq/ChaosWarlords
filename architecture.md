# ChaosWarlords Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords` codebase, a digital adaptation of the board game *Chaos Warlords*. The design utilizes **Dependency Injection**, **Event-Driven Architecture**, and **Interface-Based Abstraction** to ensure testabilty, maintainability, and support for a future Multiplayer (Headless) port.

## Organization Principles (Industry Standards)

### 1. Separation of Concerns (SRP)
- **Logic != Rendering**: Game Logic (`GameplayState`) must never depend on `Microsoft.Xna.Framework.Graphics`. It delegates all visualization to an injected `IGameplayView`.
- **Input != Action**: Input handling (`PlayerController`) detects *intent*, then delegates execution to the `InputCoordinator` or specific Managers.

### 2. Dependency Injection
- **No Global Statics**: We avoid `static` managers.
- **Constructor Injection**: All dependencies are passed in the constructor.
- **MatchContext**: Acts as the scoped container for a single match lifecycle, holding references to `IMapManager`, `ITurnManager`, etc.

### 3. Interface-Based Design
- **Testability**: Components depend on `IInterface`, not concrete classes. This allows `NSubstitute` to mock dependencies during Unit Testing.
- **Headless Support**: The Server can inject "Null" or "Headless" implementations of View interfaces (e.g., `IGameplayView`), allowing the exact same Game Logic to run without a GPU.
- **Structure**: Interfaces are grouped by layer (Services, Input, Rendering, Logic, State, Data) in `Source/Core/Interfaces`.

### 4. Namespace Convention
- Namespaces strictly follow the directory structure (e.g., `ChaosWarlords.Source.Core.Interfaces.Services`).

---

## Directory Structure & File Listing

The project uses a semantic folder structure. Below is a detailed listing of all files and their responsibilities.

```text
Source/
├── Core/
│   ├── Contexts/                   # Data Holders (The "Glue")
│   │   ├── ExecutedAction.cs       # Record capturing a single game event (Sequence, Type, Player).
│   │   ├── MatchContext.cs         # Scoped container dependencies for a single match (Map, Market, etc.).
│   │   └── TurnContext.cs          # Tracks transient state and Action History for the current turn.
│   ├── Interfaces/                 # Contracts (API Definitions)
│   │   ├── Data/
│   │   │   └── ICardDatabase.cs    # Contract for retrieving card definitions.
│   │   ├── Input/
│   │   │   ├── IGameplayInputCoordinator.cs # Manages input flow during gameplay.
│   │   │   ├── IInputManager.cs    # Abstraction for raw input (keyboard/mouse).
│   │   │   ├── IInputMode.cs       # Strategy pattern interface for input handling modes.
│   │   │   ├── IInputProvider.cs   # Provider for input state snapshots.
│   │   │   └── IInteractionMapper.cs # Maps screen coordinates to game entities.
│   │   ├── Logic/
│   │   │   ├── IActionSystem.cs    # Manages complex multi-step actions (Targeting).
│   │   │   └── IGameCommand.cs     # Command pattern interface for game actions.
│   │   ├── Rendering/
│   │   │   ├── IButtonManager.cs   # Manages UI buttons and interactions.
│   │   │   ├── IGameplayView.cs    # The contract for the Gameplay visualization layer.
│   │   │   ├── IMainMenuView.cs    # The contract for the Main Menu visualization.
│   │   │   └── IUIManager.cs       # Contract for high-level UI management.
│   │   ├── Services/
│   │   │   ├── IMapManager.cs      # API for map logic and queries.
│   │   │   ├── IMarketManager.cs   # API for market economy and card rows.
│   │   │   ├── IMatchManager.cs    # API for match lifecycle (win/loss).
│   │   │   ├── IPlayerStateManager.cs # API for centralized player mutations.
│   │   │   ├── IGameRandom.cs      # Contract for deterministic random number generation.
│   │   │   └── ITurnManager.cs     # API for turn rotation and player state.
│   │   └── State/
│   │       ├── IGameplayState.cs   # Contract for the main game loop state.
│   │       ├── IState.cs           # Generic state interface (Update/Draw/Load).
│   │       └── IStateManager.cs    # Service for managing the state stack.
│   └── Utilities/                  # Infrastructure & Constants
│       ├── CardDatabase.cs         # Implementation of the card library.
│       ├── CollectionHelpers.cs    # Extension methods for generic collections.
│       ├── GameConstants.cs        # Global configuration values.
│       ├── GameEnums.cs            # Enums (PlayerColor, ResourceType, etc.).
│       ├── GameLogger.cs           # Central logging facility.
│       ├── MapGenerationConfig.cs  # Parameters for procedural map generation.
│       ├── MapGeometry.cs          # Helper for hexagonal grid math.
│       ├── MapLayoutEngine.cs      # Procedural map generation logic.
│       ├── MapTopology.cs          # Calculates distances and recursive paths.
│       ├── SeededGameRandom.cs     # Deterministic RNG implementation.
│       └── TextCache.cs            # Caches string measurements for performance.
├── Entities/                       # Domain Models
│   ├── Actors/
│   │   └── Player.cs               # Represents a human or AI player (Hand, Resources).
│   ├── Cards/
│   │   ├── Card.cs                 # Data model for a playable card.
│   │   ├── CardEffects.cs          # Definitions for card effects and mechanics.
│   │   └── Deck.cs                 # Manages a collection of cards (Draw/Shuffle).
│   ├── Map/
│   │   ├── CitySite.cs             # Represents a Capturable City.
│   │   ├── MapNode.cs              # A graph node representing a location on the map.
│   │   ├── NonCitySite.cs          # Represents a neutral/resource site.
│   │   ├── Route.cs                # A path connection between two MapNodes.
│   │   ├── Site.cs                 # Abstract base class for all sites.
│   │   └── StartingSite.cs         # Special site where players spawn.
├── Factories/                      # Object Creation Logic
│   ├── CardFactory.cs              # Creates Card instances from data.
│   ├── MapFactory.cs               # Generates the map graph and nodes.
│   └── MatchFactory.cs             # Assembles all dependencies for a new match.
├── GameStates/                     # Application State Machine
│   ├── GameplayState.cs            # The Core Game Loop (Logic Only, no rendering code).
│   ├── MainMenuState.cs            # Entry Point / Composition Root for the game.
│   └── StateManager.cs             # Stack-based State Machine implementation.
├── Input/                          # Human Interface Layer
│   ├── Controllers/
│   │   └── PlayerController.cs     # High-Level Intent Parser.
│   ├── Modes/                      # Input State Machine (Strategy Pattern)
│   │   ├── DevourInputMode.cs      # Input mode for trashing a card.
│   │   ├── MarketInputMode.cs      # Input mode for interacting with the market.
│   │   ├── NormalPlayInputMode.cs  # Default input mode for standard play.
│   │   ├── PromoteInputMode.cs     # Input mode for upgrading units/sites.
│   │   └── TargetingInputMode.cs   # Input mode for selecting targets (hexes/units).
│   ├── Processors/
│   │   ├── GameplayInputCoordinator.cs # Orchestrates input flow between Controller and Modes.
│   │   └── InteractionMapper.cs    # Translates Screen(X,Y) -> Entity (MapNode, Card).
│   └── Services/
│       └── InputManager.cs         # Raw MonoGame Input Wrapper.
├── Managers/                       # Business Logic Services
│   ├── MapManager.cs               # Facade for Board Logic (Movement, Control).
│   ├── MarketManager.cs            # Manages the Card Market and purchasing.
│   ├── MatchManager.cs             # Manages Victory Conditions and End of Match.
│   ├── PlayerStateManager.cs       # Implementation of centralized player mutations.
│   ├── TurnManager.cs              # Manages Turn Order and Phase Transitions.
│   ├── UIEventMediator.cs          # Decouples Game Logic from UI Events/Popups.
│   └── UIManager.cs                # Manages layout and state of UI widgets.
├── Mechanics/                      # The "Rules" of the Game
│   ├── Actions/
│   │   ├── ActionSystem.cs         # Handles targeting logic for multi-step actions.
│   │   └── CardPlaySystem.cs       # Validates condition and costs for playing cards.
│   ├── Commands/                   # Command Pattern (Undo/Replay Support)
│   │   ├── ActionCompletedCommand.cs # Signals an action was successfully finished.
│   │   ├── BuyCardCommand.cs       # Command to purchase a card from market.
│   │   ├── CancelActionCommand.cs  # Command to cancel current targeting.
│   │   ├── DeployTroopCommand.cs   # Command to place a unit on the board.
│   │   ├── DevourCardCommand.cs    # Command to trash a card for resources.
│   │   ├── EndTurnCommand.cs       # Command to pass turn to next player.
│   │   ├── PlayCardCommand.cs      # Command to play a card from hand.
│   │   ├── ResolveSpyCommand.cs    # Command to execute spy mechanics.
│   │   ├── StartAssassinateCommand.cs # Command to initiate assassination targeting.
│   │   ├── StartReturnSpyCommand.cs # Command to initiate spy return targeting.
│   │   ├── SwitchToNormalModeCommand.cs # Command to reset input mode.
│   │   └── ToggleMarketCommand.cs  # Command to open/close market view.
│   └── Rules/                      # Pure Logic Engines
│       ├── CardEffectProcessor.cs  # Applies the effects of played cards.
│       ├── CombatResolver.cs       # Determines outcomes of battles.
│       ├── MapRewardSystem.cs      # Calculates resource generation from sites.
│       ├── MapRuleEngine.cs        # Validates movement and placement rules.
│       ├── MapTopology.cs          # Calculates distances and recursive paths.
│       ├── SiteControlSystem.cs    # Manages ownership changes of sites.
│       └── SpyOperations.cs        # Handles spy placement and removal logic.
└── Rendering/                      # Presentation Layer (The "View")
    ├── UI/
    │   ├── ButtonManager.cs        # Handles button registration and hit-testing.
    │   ├── SimpleButton.cs         # Basic UI button implementation.
    │   └── UIRenderer.cs           # Renders UI elements (Bars, Buttons, Overlays).
    ├── ViewModels/                 # MVVM State
    │   └── CardViewModel.cs        # View-Logic wrapper for Card animations/state.
    └── Views/
        ├── CardRenderer.cs         # Draws individual cards to screen.
        ├── GameplayView.cs         # The Concrete Implementation of IGameplayView.
        ├── MainMenuView.cs         # Main Menu screen renderer.
        └── MapRenderer.cs          # Draws the hex map and units.
```

---

## Key Systems Breakdown

### 1. Decoupled Rendering System
This architecture supports multiplayer by strictly separating Logic from Views.
- **`IGameplayView`**: The contract. Defines methods like `Draw`, `Update`, `LoadContent`.
- **`GameplayState`**: Takes `IGameplayView` in its constructor. It calculates *what* happens, then tells the View *what* to update.
- **`InteractionMapper`**: Translates Mouse interactions using the `IGameplayView` interface to find screen elements, ensuring hit-testing matches rendering.

### 2. Input Coordination System
We use a layered approach to handle complex inputs (Targeting, Market, etc.):
1.  **`InputManager`**: "Key A was pressed." (Raw Data)
2.  **`PlayerController`**: "User wants to End Turn." (Intent)
3.  **`GameplayInputCoordinator`**: "Can we End Turn? Yes -> Delegate to Manager." (Orchestration)
4.  **`IInputMode`**: "We are in Targeting Mode, so clicks select Nodes, not Cards." (Contextual Interpretation)

### 3. Command Pattern (Mechanics/Commands/)
All significant game actions (Move, Attack, Buy) are encapsulated in `IGameCommand` objects.
- **Execution**: `Command.Execute(IGameplayState)`
- **Traceability**: Every command execution is recorded in the `TurnContext.ActionHistory`.
- **Benefit**: Allows for easier debugging, deterministic Replays, and specific Unit Testing of atomic actions.

### 4. Multiplayer Readiness & Determinism
The architecture is specifically designed for multiplayer synchronization without a shared memory model:
- **Centralized Mutation (`PlayerStateManager`)**: All resource changes (Power, Influence, Troops) flow through this single point, allowing for easy logging and broadcasting of state changes.
- **Action Sequencing**: Every player move is assigned a unique sequence number. This history allows clients to catch up or detect desyncs.
- **Seeded RNG**: Match-wide deterministic randomness ensures that the same deck shuffles and combat outcomes occur on all clients if given the same seed.
- **Headless Portability**: The strict separation of Logic from MonoGame types (via interfaces) allows the Core engine to run on a server without a display.

---

## Future Guidelines for Contributors

1.  **Keep it Testable**: If you add a new Manager, add an `IManager` interface.
2.  **Keep it Clean**: Do not put drawing code in `Managers/` or `Mechanics/`. Use `Rendering/` or emit an event that the View subscribes to.
3.  **Keep it Safe**: Use `NSubstitute` for all unit tests. Avoid using real `Game` or `GraphicsDevice` in tests.
