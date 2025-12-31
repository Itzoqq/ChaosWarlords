# ChaosWarlords Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords` codebase, a digital adaptation of the board game *Chaos Warlords*. The design utilizes **Dependency Injection**, **Event-Driven Architecture**, and **Interface-Based Abstraction** to ensure testability, maintainability, and support for a future Multiplayer (Headless) port.

**Key Design Goals**:
- **Testability**: All components can be unit tested in isolation
- **Multiplayer Ready**: Logic separated from rendering for headless server support
- **Deterministic**: Seeded RNG and action sequencing for replay/sync
- **Maintainable**: Clear separation of concerns and single responsibility

---

## Organization Principles (Industry Standards)

### 1. Separation of Concerns (SRP)
**Logic != Rendering**: Game Logic (`GameplayState`) must never depend on `Microsoft.Xna.Framework.Graphics`. It delegates all visualization to an injected `IGameplayView`.

```csharp
// ❌ Bad: Logic depends on rendering
public class GameplayState
{
    private SpriteBatch _spriteBatch;  // Tight coupling!
    
    public void Update()
    {
        // Game logic mixed with rendering
        _spriteBatch.Draw(...);
    }
}

// ✅ Good: Logic delegates to interface
public class GameplayState
{
    private readonly IGameplayView _view;
    
    public GameplayState(IGameplayView view)
    {
        _view = view;  // Dependency injection
    }
    
    public void Update()
    {
        // Pure logic
        ProcessTurn();
        
        // Delegate rendering
        _view.UpdateDisplay();
    }
}
```

**Input != Action**: Input handling (`PlayerController`) detects *intent*, then delegates execution to the `InputCoordinator` or specific Managers.

### 2. Dependency Injection
**No Global Statics**: We avoid `static` managers to enable testing and multiplayer.

```csharp
// ❌ Bad: Global static state
public static class GameManager
{
    public static Player CurrentPlayer { get; set; }
}

// ✅ Good: Injected dependencies
public class TurnManager
{
    private readonly IPlayerStateManager _playerState;
    
    public TurnManager(IPlayerStateManager playerState)
    {
        _playerState = playerState;
    }
}
```

**Constructor Injection**: All dependencies are passed in the constructor.
**Composition Root**: `Game1.cs` and `MainMenuState.cs` act as Composition Roots, wiring dependencies (`IGameDependencies`) before injecting them into `GameplayState`.

**Parameter Object Pattern**: We use `IGameDependencies` to group core dependencies (Input, UI, Data) into a single object, simplifying constructors and preventing "parameter explosion".

**MatchContext**: Acts as the scoped container for a single match lifecycle, holding references to `IMapManager`, `ITurnManager`, etc.

### 3. Interface-Based Design
**Testability**: Components depend on `IInterface`, not concrete classes. This allows `NSubstitute` to mock dependencies during Unit Testing.

```csharp
// Production: Real implementation
var mapManager = new MapManager(nodes, sites, stateManager);

// Testing: Mock implementation
var mockMapManager = Substitute.For<IMapManager>();
mockMapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>())
              .Returns(true);
```

**Headless Support**: The Server can inject "Null" or "Headless" implementations of View interfaces (e.g., `IGameplayView`), allowing the exact same Game Logic to run without a GPU.

**Structure**: Interfaces are grouped by layer (Services, Input, Rendering, Logic, State, Data) in `Source/Core/Interfaces`.

### 4. Namespace Convention
Namespaces strictly follow the directory structure (e.g., `ChaosWarlords.Source.Core.Interfaces.Services`).

---

## Directory Structure & File Listing

The project uses a semantic folder structure. Below is a detailed listing of all files and their responsibilities.

```text
Source/
├── Core/
│   ├── Composition/                     # Dependency Injection composition roots
│   │   ├── GameDependencies.cs          # Concrete dependency container
│   │   └── IGameDependencies.cs         # Dependency contract for GameplayState
│   ├── Contexts/                        # Data Holders (The "Glue")
│   │   ├── ExecutedAction.cs            # Record capturing a single game event
│   │   │                                  - Sequence number for ordering
│   │   │                                  - Action type (PlayCard, Deploy, etc.)
│   │   │                                  - Player who executed the action
│   │   │                                  - Used for replay and multiplayer sync
│   │   ├── MatchContext.cs              # Scoped DI container for a single match
│   │   │                                  - Holds IMapManager, IMarketManager, etc.
│   │   │                                  - Created by MatchFactory
│   │   │                                  - Disposed when match ends
│   │   └── TurnContext.cs               # Transient state for current turn
│   │                                      - Action history (for undo/replay)
│   │                                      - Turn-specific flags and counters
│   │                                      - Cleared at end of turn
│   │
│   ├── Data/                            # Data Transfer Objects
│   │   ├── CardDto.cs                   # Serializable card data
│   │   ├── GameStateDto.cs              # Serializable game state snapshot
│   │   ├── MapDto.cs                    # Serializable map data
│   │   └── PlayerDto.cs                 # Serializable player data
│   │
│   ├── Events/                          # Event System
│   │   ├── GameEvent.cs                 # Base record for all game events
│   │   ├── IEventManager.cs             # Event publishing/subscription contract
│   │   ├── EventManager.cs              # Event bus implementation
│   │   ├── GameEventLogger.cs           # Logs events for debugging/replay
│   │   └── StateChangeEvent.cs          # Event for state mutations
│   │
│   ├── Interfaces/                      # Contracts (API Definitions)
│   │   ├── Data/
│   │   │   └── ICardDatabase.cs         # Contract for retrieving card definitions
│   │   │                                  - GetCardById(string id)
│   │   │                                  - GetAllCards()
│   │   │                                  - Implemented by CardDatabase
│   │   ├── Input/
│   │   │   ├── IGameplayInputCoordinator.cs # Manages input flow during gameplay
│   │   │   │                              - Coordinates between Controller and Modes
│   │   │   │                              - Handles mode transitions
│   │   │   ├── IInputManager.cs         # Abstraction for raw input (keyboard/mouse)
│   │   │   │                              - GetKeyboardState()
│   │   │   │                              - GetMouseState()
│   │   │   ├── IInputMode.cs            # Strategy pattern for input handling modes
│   │   │   │                              - HandleInput(InputState)
│   │   │   │                              - Implementations: Normal, Targeting, Market, etc.
│   │   │   ├── IInputProvider.cs        # Provider for input state snapshots
│   │   │   └── IInteractionMapper.cs    # Maps screen coordinates to game entities
│   │   │                                  - GetCardAtPosition(x, y)
│   │   │                                  - GetNodeAtPosition(x, y)
│   │   ├── Logic/
│   │   │   ├── IActionSystem.cs         # Manages complex multi-step actions
│   │   │   │                              - StartAction(ActionType)
│   │   │   │                              - CompleteAction()
│   │   │   │                              - CancelAction()
│   │   │   ├── ICommandValidator.cs     # Validates commands before execution
│   │   │   └── IGameCommand.cs          # Command pattern interface for game actions
│   │   │                                  - Execute(IGameplayState)
│   │   │                                  - All commands implement this
│   │   ├── Rendering/
│   │   │   ├── IButtonManager.cs        # Manages UI buttons and interactions
│   │   │   ├── IGameplayView.cs         # The contract for Gameplay visualization
│   │   │   │                              - Draw()
│   │   │   │                              - Update(GameTime)
│   │   │   │                              - LoadContent()
│   │   │   ├── IMainMenuView.cs         # The contract for Main Menu visualization
│   │   │   └── IUIManager.cs            # Contract for high-level UI management
│   │   ├── Services/
│   │   │   ├── ICommandDispatcher.cs    # Funnel for command execution & recording
│   │   │   │                              - Dispatch(command, state)
│   │   │   ├── IGameRandom.cs           # Contract for deterministic RNG
│   │   │   │                              - NextInt(min, max)
│   │   │   │                              - Shuffle<T>(IList<T>)
│   │   │   │                              - Seed property for replay
│   │   │   ├── IMapManager.cs           # API for map logic and queries
│   │   │   │                              - TryDeploy(Player, MapNode)
│   │   │   │                              - PlaceSpy(Site, Player)
│   │   │   │                              - Assassinate(MapNode, Player)
│   │   │   ├── IMarketManager.cs        # API for market economy and card rows
│   │   │   │                              - BuyCard(Player, Card)
│   │   │   │                              - RefreshMarket()
│   │   │   │                              - GetAvailableCards()
│   │   │   ├── IMatchManager.cs         # API for match lifecycle (win/loss)
│   │   │   │                              - CheckVictoryConditions()
│   │   │   │                              - EndMatch(Player winner)
│   │   │   ├── IPlayerStateManager.cs   # API for centralized player mutations
│   │   │   │                              - AddPower(Player, int amount)
│   │   │   │                              - AddInfluence(Player, int amount)
│   │   │   │                              - AddVictoryPoints(Player, int amount)
│   │   │   │                              - All resource changes go through here
│   │   │   ├── IReplayManager.cs        # API for replay recording/playback
│   │   │   └── ITurnManager.cs          # API for turn rotation and player state
│   │   │                                  - EndTurn()
│   │   │                                  - GetCurrentPlayer()
│   │   │                                  - AdvanceToNextPlayer()
│   │   └── State/
│   │       ├── IGameplayState.cs        # Contract for the main game loop state
│   │       ├── IState.cs                # Generic state interface (Update/Draw/Load)
│   │       └── IStateManager.cs         # Service for managing the state stack
│   │                                      - PushState(IState)
│   │                                      - PopState()
│   │                                      - ChangeState(IState)
│   │
│   ├── Logic/                           # Core Game Logic
│   │   └── CommandValidator.cs         # Validates commands before execution
│   │                                      - Checks preconditions
│   │                                      - Prevents invalid moves
│   │
│   └── Utilities/                       # Infrastructure & Constants
│       ├── CachedIntText.cs             # Caches integer-to-string conversions
│       ├── CardDatabase.cs              # Implementation of the card library
│       │                                  - Loads card definitions from data
│       │                                  - Provides card lookup by ID
│       ├── CollectionHelpers.cs         # Extension methods for generic collections
│       │                                  - Shuffle<T>()
│       │                                  - RemoveWhere<T>()
│       ├── GameConstants.cs             # Global configuration values
│       │                                  - DeployPowerCost = 1
│       │                                  - MaxHandSize = 5
│       │                                  - StartingDeckSize = 10
│       ├── GameEnums.cs                 # Enums (PlayerColor, ResourceType, etc.)
│       │                                  - PlayerColor: Red, Blue, Green, Yellow
│       │                                  - ResourceType: Power, Influence, VictoryPoints
│       │                                  - CardAspect: Warlord, Shadow, Sorcery, Neutral
│       ├── BufferedAsyncLogger.cs       # Async-optimized logging implementation
│       │                                  - Writes logs to file in background thread
│       │                                  - Non-blocking IGameLogger implementation
│       │              
│       ├── MapGenerationConfig.cs       # Parameters for procedural map generation
│       ├── MapGeometry.cs               # Helper for hexagonal grid math
│       │                                  - CalculateDistance(hex1, hex2)
│       │                                  - GetNeighbors(hex)
│       ├── MapLayoutEngine.cs           # Procedural map generation logic
│       │                                  - GenerateMap(config)
│       │                                  - Creates nodes, sites, and connections
│       ├── MapTopology.cs               # Calculates distances and recursive paths
│       │                                  - FindPath(start, end)
│       │                                  - GetNodesWithinDistance(node, distance)
│       ├── SeededGameRandom.cs          # Deterministic RNG implementation
│       │                                  - Uses System.Random with fixed seed
│       │                                  - Ensures same results across clients
│       └── TextCache.cs                 # Caches string measurements for performance
│                                          - Avoids repeated MeasureString calls
│
├── Entities/                            # Domain Models (Pure Data + Behavior)
│   ├── Actors/
│   │   └── Player.cs                    # Represents a human or AI player
│   │                                      - Properties: Hand, Deck, DiscardPile
│   │                                      - Resources: Power, Influence, VictoryPoints
│   │                                      - Units: TroopsInBarracks, SpiesInBarracks
│   │                                      - Methods: DrawCard(), DiscardCard(), etc.
│   ├── Cards/
│   │   ├── Card.cs                      # Data model for a playable card
│   │   │                                  - Properties: Name, Cost, Aspect, Effects
│   │   │                                  - Location: Hand, Deck, DiscardPile, etc.
│   │   │                                  - VP values: DeckVP, InnerCircleVP
│   │   ├── CardEffects.cs               # Definitions for card effects and mechanics
│   │   │                                  - CardEffect record (Type, Amount, Target)
│   │   │                                  - EffectType enum (GainResource, DrawCard, etc.)
│   │   └── Deck.cs                      # Manages a collection of cards
│   │                                      - Draw(count, random)
│   │                                      - Shuffle(random)
│   │                                      - AddToTop/Bottom(card)
│   │                                      - ReshuffleDiscard(random)
│   └── Map/
│       ├── CitySite.cs                  # Represents a Capturable City
│       │                                  - Generates resources when controlled
│       │                                  - Higher VP value than non-cities
│       ├── MapNode.cs                   # A graph node representing a location
│       │                                  - Properties: Id, Position, Occupant
│       │                                  - Neighbors: List<MapNode>
│       │                                  - Site: Optional Site at this location
│       │                                  - Methods: AddNeighbor(), RemoveNeighbor()
│       ├── NonCitySite.cs               # Represents a neutral/resource site
│       │                                  - Generates resources when controlled
│       │                                  - Can have spies placed on them
│       ├── Route.cs                     # A path connection between two MapNodes
│       │                                  - Used for visualization
│       ├── Site.cs                      # Abstract base class for all sites
│       │                                  - Properties: Name, ResourceType, Amount
│       │                                  - Spies: List<PlayerColor>
│       │                                  - Control tracking
│       └── StartingSite.cs              # Special site where players spawn
│                                          - Cannot be captured
│                                          - Provides starting resources
│
├── Factories/                           # Object Creation Logic (Composition Root)
│   ├── CardFactory.cs                   # Creates Card instances from data
│   │                                      - CreateCard(CardDto)
│   │                                      - Builds cards with effects
│   ├── MapFactory.cs                    # Generates the map graph and nodes
│   │                                      - CreateMap(config, random)
│   │                                      - Uses MapLayoutEngine
│   │                                      - Creates nodes, sites, connections
│   └── MatchFactory.cs                  # Assembles all dependencies for a new match
│                                          - CreateMatch(players, seed)
│                                          - Wires up all managers and systems
│                                          - Returns MatchContext
│
├── GameStates/                          # Application State Machine
│   ├── GameplayState.cs                 # The Core Game Loop (Logic Only)
│   │                                      - Constructor accepts `IGameDependencies`
│   │                                      - Update(): Processes game logic
│   │                                      - Delegates rendering to `IGameplayView`
│   │                                      - Coordinates managers and systems
│   │                                      - Handles turn flow and win conditions
│   ├── MainMenuState.cs                 # Entry Point / Composition Root
│   │                                      - Initializes game
│   │                                      - Handles menu navigation
│   │                                      - Creates new matches
│   └── StateManager.cs                  # Stack-based State Machine implementation
│                                          - Manages state transitions
│                                          - Supports state stacking (pause, etc.)
│
├── Input/                               # Human Interface Layer
│   ├── Controllers/
│   │   └── PlayerController.cs          # High-Level Intent Parser
│   │                                      - Translates raw input to game intent
│   │                                      - "User clicked card" → "Play this card"
│   │                                      - Delegates to InputCoordinator
│   │   └── ReplayController.cs          # Replay Workflow Orchestrator
│   │   │                                      - Loops playback logic
│   │   │                                      - Handles Save/Load input (F5/F6)
│   ├── Modes/                           # Input State Machine (Strategy Pattern)
│   │   ├── DevourInputMode.cs           # Input mode for trashing a card
│   │   │                                  - Click card → Devour it
│   │   ├── MarketInputMode.cs           # Input mode for interacting with market
│   │   │                                  - Click card → Buy it
│   │   │                                  - Click outside → Close market
│   │   ├── NormalPlayInputMode.cs       # Default input mode for standard play
│   │   │                                  - Click card → Play it
│   │   │                                  - Click node → Deploy troop
│   │   │                                  - Right-click → Context menu
│   │   ├── PromoteInputMode.cs          # Input mode for upgrading units/sites
│   │   │                                  - Click card → Promote to inner circle
│   │   └── TargetingInputMode.cs        # Input mode for selecting targets
│   │                                      - Click node → Target for effect
│   │                                      - ESC → Cancel targeting
│   ├── Processors/
│   │   ├── GameplayInputCoordinator.cs  # Orchestrates input flow
│   │   │                                  - Manages mode transitions
│   │   │                                  - Routes input to current mode
│   │   │                                  - Validates actions before execution
│   │   └── InteractionMapper.cs         # Translates Screen(X,Y) → Entity
│   │                                      - Hit-testing for cards, nodes, buttons
│   │                                      - Uses view bounds for accuracy
│   └── Services/
│       └── InputManager.cs              # Raw MonoGame Input Wrapper
│                                          - Wraps Keyboard.GetState()
│                                          - Wraps Mouse.GetState()
│                                          - Provides input snapshots
│
├── Managers/                            # Business Logic Services
│   ├── CommandDispatcher.cs             # Central Command Processor
│   │                                      - Records command for replay
│   │                                      - Executes via command.Execute(state)
│   ├── MapManager.cs                    # Facade for Board Logic
│   │                                      - TryDeploy(player, node): Deploy troops
│   │                                      - PlaceSpy(site, player): Place spy
│   │                                      - Assassinate(node, player): Remove unit
│   │                                      - Supplant(node, player): Replace unit
│   │                                      - Delegates to subsystems (Combat, Spy, etc.)
│   ├── MarketManager.cs                 # Manages the Card Market
│   │                                      - BuyCard(player, card): Purchase logic
│   │                                      - RefreshMarket(): Replenish cards
│   │                                      - GetAvailableCards(): Query market state
│   │                                      - Pricing and availability logic
│   ├── MatchManager.cs                  # Manages Victory Conditions
│   │                                      - CheckVictoryConditions(): Check for winner
│   │                                      - EndMatch(winner): Cleanup and transition
│   │                                      - Tracks game state and progression
│   ├── PlayerStateManager.cs            # Centralized player mutations
│   │                                      - AddPower/Influence/VictoryPoints()
│   │                                      - AddTroops/Spies()
│   │                                      - All resource changes logged here
│   │                                      - Emits events for UI updates
│   ├── ReplayManager.cs                 # Replay recording and playback
│   │                                      - RecordAction(ExecutedAction)
│   │                                      - PlaybackActions(List<ExecutedAction>)
│   │                                      - Save/Load replay files
│   ├── TurnManager.cs                   # Manages Turn Order and Phase Transitions
│   │                                      - EndTurn(): Advance to next player
│   │                                      - GetCurrentPlayer(): Query active player
│   │                                      - Phase management (Setup, Playing, End)
│   ├── UIEventMediator.cs               # Decouples Game Logic from UI Events
│   │                                      - Emits events for popups, notifications
│   │                                      - View subscribes to these events
│   │                                      - Prevents logic from depending on UI
│   └── UIManager.cs                     # Manages layout and state of UI widgets
│                                          - Button positions and states
│                                          - Panel visibility
│                                          - UI element coordination
│
├── Map/                                 # Map-Specific Subsystems
│   ├── CombatResolver.cs                # Determines outcomes of battles
│   │                                      - ExecuteAssassinate(node, player)
│   │                                      - ExecuteSupplant(node, player)
│   │                                      - Combat logic and trophy awards
│   ├── MapRewardSystem.cs               # Calculates resource generation from sites
│   │                                      - CalculateRewards(player): Sum site bonuses
│   │                                      - Applies at end of turn
│   ├── MapTopology.cs                   # Pathfinding and distance calculations
│   │                                      - FindPath(start, end): A* pathfinding
│   │                                      - GetDistance(node1, node2): Hex distance
│   │                                      - GetNodesWithinRange(node, range)
│   └── SpyOperations.cs                 # Handles spy placement and removal
│                                          - ExecutePlaceSpy(site, player)
│                                          - ExecuteReturnSpy(site, player)
│                                          - Spy validation and state updates
│
├── Mechanics/                           # The "Rules" of the Game
│   ├── Actions/
│   │   ├── ActionSystem.cs              # Handles targeting logic for multi-step actions
│   │   │                                  - StartAction(type, card): Begin targeting
│   │   │                                  - SelectTarget(node): Record target
│   │   │                                  - CompleteAction(): Execute with target
│   │   │                                  - CancelAction(): Abort targeting
│   │   └── CardPlaySystem.cs            # Validates conditions for playing cards
│   │                                      - CanPlayCard(player, card): Check resources
│   │                                      - PlayCard(player, card): Execute play
│   │                                      - Triggers effect processing
│   ├── Commands/                        # Command Pattern (Undo/Replay Support)
│   │   ├── ActionCompletedCommand.cs    # Signals an action was successfully finished
│   │   ├── BuyCardCommand.cs            # Command to purchase a card from market
│   │   ├── CancelActionCommand.cs       # Command to cancel current targeting
│   │   ├── DeployTroopCommand.cs        # Command to place a unit on the board
│   │   ├── DevourCardCommand.cs         # Command to trash a card for resources
│   │   ├── EndTurnCommand.cs            # Command to pass turn to next player
│   │   ├── PlayCardCommand.cs           # Command to play a card from hand
│   │   ├── ResolveSpyCommand.cs         # Command to execute spy mechanics
│   │   ├── StartAssassinateCommand.cs   # Command to initiate assassination targeting
│   │   ├── StartReturnSpyCommand.cs     # Command to initiate spy return targeting
│   │   ├── SwitchToNormalModeCommand.cs # Command to reset input mode
│   │   └── ToggleMarketCommand.cs       # Command to open/close market view
│   │
│   └── Rules/                           # Pure Logic Engines
│       ├── CardEffectProcessor.cs       # Applies the effects of played cards
│       │                                  - ProcessEffects(card, player, context)
│       │                                  - Handles all effect types (GainResource, etc.)
│       │                                  - Delegates to appropriate managers
│       ├── MapRuleEngine.cs             # Validates movement and placement rules
│       │                                  - CanDeploy(player, node): Check adjacency
│       │                                  - CanMove(from, to): Check path validity
│       └── SiteControlSystem.cs         # Manages ownership changes of sites
│                                          - UpdateControl(site): Recalculate owner
│                                          - Based on spy count and occupancy
│
└── Rendering/                           # Presentation Layer (The "View")
    ├── UI/
    │   ├── ButtonManager.cs             # Handles button registration and hit-testing
    │   │                                  - RegisterButton(id, bounds, action)
    │   │                                  - HandleClick(x, y): Find and invoke button
    │   ├── SimpleButton.cs              # Basic UI button implementation
    │   │                                  - Properties: Bounds, Text, IsEnabled
    │   │                                  - OnClick event
    │   └── UIRenderer.cs                # Renders UI elements
    │                                      - DrawBar(resource, value, max)
    │                                      - DrawButton(button)
    │                                      - DrawPanel(bounds, title)
    ├── ViewModels/                      # MVVM State
    │   └── CardViewModel.cs             # View-Logic wrapper for Card
    │                                      - Animation state (position, rotation)
    │                                      - Visual effects (glow, highlight)
    │                                      - Separates view state from domain model
    └── Views/
        ├── CardRenderer.cs              # Draws individual cards to screen
        │                                  - DrawCard(card, position, scale)
        │                                  - Handles card art, text, effects
        ├── GameplayView.cs              # Concrete Implementation of IGameplayView
        │                                  - Implements all rendering for gameplay
        │                                  - Coordinates renderers (Map, Card, UI)
        │                                  - No game logic, only visualization
        ├── MainMenuView.cs              # Main Menu screen renderer
        │                                  - Draws menu options
        │                                  - Handles menu animations
        └── MapRenderer.cs               # Draws the hex map and units
                                           - DrawNode(node, position)
                                           - DrawRoute(route)
                                           - DrawUnit(node, color)
```

---

## Key Systems Breakdown

### 1. Decoupled Rendering System
This architecture supports multiplayer by strictly separating Logic from Views.

**How it works**:
- **`IGameplayView`**: The contract. Defines methods like `Draw`, `Update`, `LoadContent`.
- **`GameplayState`**: Takes `IGameplayView` in its constructor. It calculates *what* happens, then tells the View *what* to update.
- **`InteractionMapper`**: Translates Mouse interactions using the `IGameplayView` interface to find screen elements, ensuring hit-testing matches rendering.

**Example**:
```csharp
// GameplayState (Logic)
public class GameplayState
{
    private readonly IGameplayView _view;
    private readonly IMapManager _mapManager;
    
    public void ProcessPlayerAction(MapNode targetNode)
    {
        // Pure logic - no rendering
        bool success = _mapManager.TryDeploy(currentPlayer, targetNode);
        
        // Tell view to update (but don't specify HOW)
        if (success)
        {
            _view.UpdateNodeDisplay(targetNode);
        }
    }
}

// GameplayView (Rendering)
public class GameplayView : IGameplayView
{
    public void UpdateNodeDisplay(MapNode node)
    {
        // Rendering logic - no game rules
        _mapRenderer.DrawNode(node, GetNodePosition(node));
    }
}
```

**Benefits**:
- **Testable**: Can test `GameplayState` without `GraphicsDevice`
- **Multiplayer**: Server uses `NullGameplayView`, clients use `GameplayView`
- **Flexible**: Can swap rendering implementations (2D, 3D, ASCII)

### 2. Input Coordination System
We use a layered approach to handle complex inputs (Targeting, Market, etc.):

**Input Flow**:
1. **`InputManager`**: "Key A was pressed." (Raw Data)
2. **`PlayerController`**: "User wants to End Turn." (Intent)
3. **`GameplayInputCoordinator`**: "Can we End Turn? Yes → Delegate to Manager." (Orchestration)
4. **`IInputMode`**: "We are in Targeting Mode, so clicks select Nodes, not Cards." (Contextual Interpretation)

**Example**:
```csharp
// 1. InputManager - Raw input
var mouseState = _inputManager.GetMouseState();
bool clicked = mouseState.LeftButton == ButtonState.Pressed;

// 2. PlayerController - Intent detection
if (clicked)
{
    var intent = _controller.DetectIntent(mouseState);
    // intent = "PlayCard" or "DeployTroop" or "EndTurn"
}

// 3. InputCoordinator - Orchestration
_coordinator.HandleIntent(intent);
// Checks current mode, validates action, delegates to manager

// 4. InputMode - Contextual handling
if (_currentMode is TargetingInputMode)
{
    // Clicks select targets, not cards
    var target = _mapper.GetNodeAtPosition(x, y);
    _actionSystem.SelectTarget(target);
}
```

**Benefits**:
- **Separation**: Input detection separate from action execution
- **Flexibility**: Easy to add new input modes
- **Testability**: Can test each layer independently

### 3. Command Pattern (Mechanics/Commands/)
All significant game actions (Move, Attack, Buy) are encapsulated in `IGameCommand` objects.

**Structure**:
```csharp
public interface IGameCommand
{
    void Execute(IGameplayState state);
}

public class PlayCardCommand : IGameCommand
{
    private readonly Player _player;
    private readonly Card _card;
    
    public PlayCardCommand(Player player, Card card)
    {
        _player = player;
        _card = card;
    }
    
    public void Execute(IGameplayState state)
    {
        // Validate
        if (!state.CardPlaySystem.CanPlayCard(_player, _card))
            return;
            
        // Execute
        state.CardPlaySystem.PlayCard(_player, _card);
        
        // Record
        state.TurnContext.RecordAction(ActionType.PlayCard, _card);
    }
}
```

**Benefits**:
- **Execution**: `Command.Execute(IGameplayState)`
- **Traceability**: Every command execution is recorded in the `TurnContext.ActionHistory`
- **Replay**: Can replay match by re-executing commands
- **Undo**: Can implement undo by storing command state
- **Testing**: Easy to test individual commands in isolation

### 4. Multiplayer Readiness & Determinism
The architecture is specifically designed for multiplayer synchronization without a shared memory model.

**Key Features**:

**Centralized Mutation (`PlayerStateManager`)**: All resource changes (Power, Influence, Troops) flow through this single point.

```csharp
// ❌ Bad: Direct mutation
player.Power += 5;

// ✅ Good: Centralized mutation
_playerStateManager.AddPower(player, 5);
// Logged, validated, and broadcast to all clients
```

**Action Sequencing**: Every player move is assigned a unique sequence number.

```csharp
public record ExecutedAction(
    int Sequence,           // 1, 2, 3, ...
    ActionType Type,        // PlayCard, Deploy, etc.
    PlayerColor Player,     // Who did it
    object? Data            // Action-specific data
);
```

**Seeded RNG**: Match-wide deterministic randomness ensures same results on all clients.

```csharp
// Create match with seed
var random = new SeededGameRandom(12345);

// All clients use same seed
// Same shuffle order, same combat outcomes
deck.Shuffle(random);
```

**Headless Portability**: The strict separation of Logic from MonoGame types (via interfaces) allows the Core engine to run on a server without a display.

```csharp
// Client: Full rendering
var view = new GameplayView(graphicsDevice, content);
var state = new GameplayState(view, managers);

// Server: No rendering
var view = new NullGameplayView();  // Does nothing
var state = new GameplayState(view, managers);
// Same logic, no GPU required
```

---

## Design Patterns Used

### 1. Dependency Injection
**Where**: Throughout the codebase  
**Why**: Testability, flexibility, decoupling

```csharp
public class MapManager : IMapManager
{
    private readonly IPlayerStateManager _stateManager;
    private readonly CombatResolver _combat;
    private readonly SpyOperations _spyOps;
    
    public MapManager(
        List<MapNode> nodes,
        List<Site> sites,
        IPlayerStateManager stateManager)
    {
        _stateManager = stateManager;
        _combat = new CombatResolver(stateManager);
        _spyOps = new SpyOperations(stateManager);
    }
}
```

### 2. Strategy Pattern
**Where**: Input modes (`IInputMode`)  
**Why**: Different input behaviors based on game state

```csharp
public interface IInputMode
{
    void HandleInput(InputState input);
}

// Different strategies for different contexts
public class NormalPlayInputMode : IInputMode { }
public class TargetingInputMode : IInputMode { }
public class MarketInputMode : IInputMode { }
```

### 3. Command Pattern
**Where**: All game actions (`IGameCommand`)  
**Why**: Encapsulation, undo/replay, logging

```csharp
public interface IGameCommand
{
    void Execute(IGameplayState state);
}

// Each action is a command
public class BuyCardCommand : IGameCommand { }
public class PlayCardCommand : IGameCommand { }
public class EndTurnCommand : IGameCommand { }
```

### 4. Facade Pattern
**Where**: Managers (e.g., `MapManager`)  
**Why**: Simplify complex subsystems

```csharp
public class MapManager : IMapManager
{
    // Facade hides complexity of subsystems
    private readonly CombatResolver _combat;
    private readonly SpyOperations _spyOps;
    private readonly MapRuleEngine _rules;
    
    public bool TryDeploy(Player player, MapNode node)
    {
        // Coordinates multiple subsystems
        if (!_rules.CanDeploy(player, node)) return false;
        _combat.ExecuteDeploy(node, player);
        return true;
    }
}
```

### 5. Factory Pattern
**Where**: Factories (e.g., `MatchFactory`)  
**Why**: Complex object creation, dependency wiring

```csharp
public class MatchFactory
{
    public MatchContext CreateMatch(List<Player> players, int seed)
    {
        // Complex creation logic
        var random = new SeededGameRandom(seed);
        var map = _mapFactory.CreateMap(config, random);
        var stateManager = new PlayerStateManager();
        var mapManager = new MapManager(map.Nodes, map.Sites, stateManager);
        // ... wire up all dependencies
        
        return new MatchContext(mapManager, marketManager, ...);
    }
}
```

### 6. Observer Pattern (Event-Driven)
**Where**: Event system, UI updates  
**Why**: Decoupling, reactive updates

```csharp
// Publisher
public class PlayerStateManager
{
    public event Action<Player, int> PowerChanged;
    
    public void AddPower(Player player, int amount)
    {
        player.Power += amount;
        PowerChanged?.Invoke(player, amount);
    }
}

// Subscriber
public class UIEventMediator
{
    public void Subscribe(IPlayerStateManager stateManager)
    {
        stateManager.PowerChanged += OnPowerChanged;
    }
    
    private void OnPowerChanged(Player player, int amount)
    {
        // Update UI
    }
}
```

---

## Testing Strategy

**Total Test Suite: 516 tests** (367 Unit + 142 Integration + 7 Performance)

### Unit Tests
**Target**: Individual classes in isolation  
**Tools**: MSTest, NSubstitute  
**Coverage**: 367 tests

```csharp
[TestMethod]
public void AddPower_WithPositiveAmount_IncreasesPlayerPower()
{
    // Arrange
    var player = new Player(PlayerColor.Red);
    var stateManager = new PlayerStateManager();
    
    // Act
    stateManager.AddPower(player, 5);
    
    // Assert
    Assert.AreEqual(5, player.Power);
}
```

### Integration Tests
**Target**: Multiple components working together  
**Tools**: MSTest, Real implementations  
**Coverage**: 142 tests

```csharp
[TestMethod]
public void TryDeploy_WithValidConditions_DeploysUnitAndUpdatesState()
{
    // Arrange
    var stateManager = new PlayerStateManager();
    var mapManager = new MapManager(nodes, sites, stateManager);
    var player = new Player(PlayerColor.Red) { Power = 10, TroopsInBarracks = 5 };
    
    // Act
    bool result = mapManager.TryDeploy(player, targetNode);
    
    // Assert
    Assert.IsTrue(result);
    Assert.AreEqual(PlayerColor.Red, targetNode.Occupant);
    Assert.AreEqual(4, player.TroopsInBarracks);
}
```

### Performance Tests
**Target**: Critical operations  
**Tools**: MSTest, Stopwatch  
**Coverage**: 7 benchmarks

```csharp
[TestMethod]
public void DeckShuffle_CompletesWithin50ms_For1000Cards()
{
    // Arrange
    var deck = CreateDeckWith1000Cards();
    var random = new SeededGameRandom(12345);
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    deck.Shuffle(random);
    
    // Assert
    stopwatch.Stop();
    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 50);
}
```

**See `test_architecture.md` for complete testing documentation.**

---

## Code Quality Metrics

**Total Files**: ~120  
**Total Lines**: ~15,000  
**Test Coverage**: Available via `run-coverage.ps1`  
**Test Count**: 398 (249 Unit, 142 Integration, 7 Performance)

**Architectural Compliance**:
- ✅ All managers have interfaces
- ✅ No rendering code in logic layer
- ✅ All dependencies injected
- ✅ Deterministic RNG used throughout
- ✅ Command pattern for all actions

---

## Coding Guidelines

These are **established patterns** that all contributors must follow. Violations of these patterns will cause multiplayer desyncs, test failures, or architectural degradation.

### 1. Deterministic RNG (CRITICAL)

**Rule**: NEVER use `System.Random` directly. Always use `IGameRandom`.

**Why**: Multiplayer synchronization requires identical random sequences on all clients. Using unseeded `Random` will cause desyncs.

```csharp
// ❌ WRONG: Will cause multiplayer desync
public void ShuffleDeck()
{
    var random = new Random();
    _cards.Shuffle(random);
}

// ✅ CORRECT: Deterministic and replayable
public void ShuffleDeck(IGameRandom random)
{
    random.Shuffle(_cards);
}
```

**Pattern**: All methods requiring randomness must accept `IGameRandom` as a parameter:
- Deck shuffling: `Deck.Shuffle(IGameRandom random)`
- Card drawing: `Deck.Draw(int count, IGameRandom random)`
- Turn order: `TurnManager(List<Player> players, IGameRandom random, ...)`
- Market setup: `MarketManager(ICardDatabase db, IGameRandom random)`

**Testing**: Use `SeededGameRandom` for reproducible tests or `Substitute.For<IGameRandom>()` for mocks.

### 2. Centralized Resource Management

**Rule**: All player resource changes MUST go through `IPlayerStateManager`.

**Why**: Centralized mutation enables logging, validation, event emission, and multiplayer sync.

```csharp
// ❌ WRONG: Direct mutation bypasses logging and events
player.Power += 5;
player.Influence -= 3;

// ✅ CORRECT: Centralized, logged, and validated
_playerStateManager.AddPower(player, 5);
_playerStateManager.SpendInfluence(player, 3);
```

**Covered Resources**:
- Power, Influence, Victory Points
- Troops, Spies (barracks counts)
- Card operations (Draw, Discard, Promote)

### 3. Interface-Based Dependencies

**Rule**: Components must depend on `IInterface`, not concrete classes.

**Why**: Enables mocking for tests and supports headless server deployment.

```csharp
// ❌ WRONG: Depends on concrete class
public class GameplayState
{
    private readonly MapManager _mapManager;
    
    public GameplayState(MapManager mapManager)
    {
        _mapManager = mapManager;
    }
}

// ✅ CORRECT: Depends on interface
public class GameplayState
{
    private readonly IMapManager _mapManager;
    
    public GameplayState(IMapManager mapManager)
    {
        _mapManager = mapManager;
    }
}
```

**Testing Benefit**:
```csharp
// Easy to mock in tests
var mockMapManager = Substitute.For<IMapManager>();
mockMapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>())
              .Returns(true);
```

### 4. Separation of Logic and Rendering

**Rule**: Game logic classes MUST NOT reference `Microsoft.Xna.Framework.Graphics` types.

**Why**: Enables headless server deployment and unit testing without GPU.

```csharp
// ❌ WRONG: Logic depends on rendering
public class MapManager
{
    private SpriteBatch _spriteBatch;
    
    public void TryDeploy(Player player, MapNode node)
    {
        node.Occupant = player.Color;
        _spriteBatch.Draw(...);  // Logic shouldn't render!
    }
}

// ✅ CORRECT: Logic emits events, view handles rendering
public class MapManager : IMapManager
{
    public event Action<MapNode> NodeUpdated;
    
    public void TryDeploy(Player player, MapNode node)
    {
        node.Occupant = player.Color;
        NodeUpdated?.Invoke(node);  // View subscribes to this
    }
}
```

**Allowed in Logic**: Interfaces (`IGameplayView`, `IUIManager`), DTOs, domain models  
**NOT Allowed in Logic**: `SpriteBatch`, `Texture2D`, `GraphicsDevice`, `SpriteFont`

### 5. Command Pattern for Actions

**Rule**: All significant game actions must be encapsulated as `IGameCommand`.

**Why**: Enables replay, undo, logging, and multiplayer command transmission.

```csharp
// ✅ CORRECT: Action as command
public class PlayCardCommand : IGameCommand
{
    private readonly Player _player;
    private readonly Card _card;
    
    public PlayCardCommand(Player player, Card card)
    {
        _player = player;
        _card = card;
    }
    
    public void Execute(IGameplayState state)
    {
        state.CardPlaySystem.PlayCard(_player, _card);
        state.TurnContext.RecordAction(ActionType.PlayCard, _card);
    }
}
```

**Commands must**:
- Implement `IGameCommand`
- Be serializable (use IDs, not object references)
- Record execution in `TurnContext` or `ReplayManager`

### 6. No Global State

**Rule**: Avoid `static` classes and singletons for game state.

**Why**: Prevents testing, breaks multiplayer, and creates hidden dependencies.

```csharp
// ❌ WRONG: Global static state
public static class GameState
{
    public static Player CurrentPlayer { get; set; }
    public static List<Card> MarketRow { get; set; }
}

// ✅ CORRECT: Injected dependencies
public class TurnManager : ITurnManager
{
    private readonly List<Player> _players;
    private int _currentPlayerIndex;
    
    public Player CurrentPlayer => _players[_currentPlayerIndex];
}
```

**Exceptions**: Constants (`GameConstants`), pure utility functions, logging

### 7. Constructor Injection

**Rule**: All dependencies must be passed via constructor, not properties or methods.

**Why**: Makes dependencies explicit and ensures objects are fully initialized.

```csharp
// ❌ WRONG: Property injection
public class MapManager
{
    public IPlayerStateManager StateManager { get; set; }
    
    public void Initialize()
    {
        // Object not usable until Initialize() called
    }
}

// ✅ CORRECT: Constructor injection
public class MapManager : IMapManager
{
    private readonly IPlayerStateManager _stateManager;
    
    public MapManager(
        List<MapNode> nodes,
        List<Site> sites,
        IPlayerStateManager stateManager)
    {
        _stateManager = stateManager;
        // Object fully initialized and ready to use
    }
}
```

---

## Future Guidelines for Contributors


### 1. Keep it Testable
If you add a new Manager, add an `IManager` interface.

```csharp
// ✅ Good
public interface INewManager { }
public class NewManager : INewManager { }

// ❌ Bad
public class NewManager { }  // No interface
```

### 2. Keep it Clean
Do not put drawing code in `Managers/` or `Mechanics/`. Use `Rendering/` or emit an event that the View subscribes to.

```csharp
// ❌ Bad
public class MapManager
{
    public void TryDeploy(...)
    {
        // Logic
        node.Occupant = player.Color;
        
        // Rendering - DON'T DO THIS!
        _spriteBatch.Draw(...);
    }
}

// ✅ Good
public class MapManager
{
    public event Action<MapNode> NodeUpdated;
    
    public void TryDeploy(...)
    {
        // Logic only
        node.Occupant = player.Color;
        
        // Notify view
        NodeUpdated?.Invoke(node);
    }
}
```

### 3. Keep it Safe
Use `NSubstitute` for all unit tests. Avoid using real `Game` or `GraphicsDevice` in tests.

```csharp
// ✅ Good
var mockView = Substitute.For<IGameplayView>();
var state = new GameplayState(mockView, managers);

// ❌ Bad
var game = new Game();  // Requires GPU!
var state = new GameplayState(game.View, managers);
```

### 4. Keep it Deterministic
**CRITICAL**: Always use `IGameRandom` for randomness, never `System.Random` directly.

```csharp
// ❌ Bad: Non-deterministic
var random = new Random();
deck.Shuffle(random);  // Will desync in multiplayer!

// ✅ Good: Deterministic, replayable
var random = new SeededGameRandom(seed, logger);
deck.Shuffle(random);  // Same seed = same results
```

**Why this matters**:
- **Multiplayer**: Both clients must produce identical game states
- **Replay**: Recorded games must replay exactly
- **Testing**: Tests must be reproducible

**Pattern**: All methods that need randomness must accept `IGameRandom` as a parameter:

```csharp
// ✅ Correct pattern
public List<Card> Draw(int count, IGameRandom random)
{
    if (_drawPile.Count == 0)
    {
        ReshuffleDiscard(random);  // Uses injected RNG
    }
    // ...
}

// ❌ Wrong pattern
public List<Card> Draw(int count)
{
    var random = new Random();  // NEVER do this!
    // ...
}
```

**Enforcement**:
- `CollectionHelpers.Shuffle()` was removed - use `IGameRandom.Shuffle()` instead
- All managers require `IGameRandom` in constructor (no default/nullable)
- Tests must provide `IGameRandom` mock or `SeededGameRandom` instance

### 5. Keep it Logged
Use `GameLogger` for important events, especially state changes.

```csharp
public void AddPower(Player player, int amount)
{
    player.Power += amount;
    GameLogger.Log($"{player.Color} gained {amount} Power", LogChannel.Economy);
}
```

---

## Related Documentation

- **`test_architecture.md`**: Complete testing guide and test suite organization
- **`README.md`**: Project setup and getting started
- **`CONTRIBUTING.md`**: Contribution guidelines and code standards

---

## Revision History

- **v1.0** (Initial): Basic architecture documentation
- **v2.0** (Current): Enhanced with detailed descriptions, examples, and patterns
