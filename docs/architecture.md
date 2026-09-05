# ChaosWarlords Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords` codebase, a digital adaptation of the board game *Tyrants of the Underdark*. The design utilizes **Dependency Injection**, **Event-Driven Architecture**, and **Interface-Based Abstraction** to ensure testability, maintainability, and support for a future Multiplayer (Headless) port.

**Key Design Goals**:
- **Testability**: All components can be unit tested in isolation
- **Multiplayer Ready**: Logic separated from rendering for headless server support
- **Deterministic**: Seeded RNG and action sequencing for replay/sync
- **Maintainable**: Clear separation of concerns and single responsibility

---

## The Four Projects

The solution is four projects, each with a specific role in keeping the "headless server" goal real rather than aspirational:

| Project | Holds | MonoGame? | References |
|---|---|---|---|
| `ChaosWarlords.Core` | All game logic - `MatchContext` and everything it composes (entities, managers, mechanics, factories, DTOs) | **Zero** package references | (none - the leaf project) |
| `ChaosWarlords` | The MonoGame client - rendering, input, UI, state machine | `MonoGame.Framework.DesktopGL` | `ChaosWarlords.Core` |
| `ChaosWarlords.Tests` | The main test suite - covers everything, including the client/UI/input layers | Transitively, via the client | `ChaosWarlords` (and so, transitively, `Core`) |
| `ChaosWarlords.Core.Tests` | A smaller, headless-only test suite | **Never** | `ChaosWarlords.Core` **only** |

`ChaosWarlords.Core` having zero MonoGame package references is what makes "headless server support" a compiled property rather than a convention enforced only by coding-guidelines.md's "no `Graphics` types" rule - if a MonoGame-dependent type ever leaked in, the project simply wouldn't build.

`ChaosWarlords.Core.Tests` exists for the same reason, one layer up: `ChaosWarlords.Tests` builds and runs fine today, but only because it also happens to carry MonoGame along for the ride via its reference to the client project - nothing structurally proved the *test suite itself* could run in isolation on a machine with no graphics stack at all. `ChaosWarlords.Core.Tests` references `ChaosWarlords.Core` and nothing else, so that gap is now closed the same way: a compiler error, not an assumption. It deliberately holds a small, curated slice rather than a full migration of every Core-only test in the main suite - a handful of already-fully-headless unit tests (`Pcg32Tests`, `SeededGameRandomTests`, `LogicVector2Tests`, `LogicRectangleTests`) plus one integration-style smoke test (`HeadlessCompositionSmokeTests`) that builds a real match via `MatchFactory` and runs real commands through a real `CommandDispatcher` - proving the whole composition root works standalone, not just that individual leaf types happen to compile in isolation. `ChaosWarlords.Tests` remains the primary, much larger suite; migrating more of its Core-only tests into `ChaosWarlords.Core.Tests` is optional future cleanup, not something either project depends on.

Namespaces are `ChaosWarlords.Source.*` across `Core` and the client (physical project boundary, not namespace, is what's enforced), and `ChaosWarlords.Core.Tests.*` / `ChaosWarlords.Tests.*` for the two test projects respectively.

A few logic-adjacent types that used to take MonoGame's `Vector2`/`Rectangle` directly (`MapNode`, `Site`, `MapTopology`, `MapManager`) use the deterministic, fixed-point `LogicVector2`/`LogicRectangle` instead; conversion to/from MonoGame's types lives in `ChaosWarlords/Source/Rendering/LogicVectorExtensions.cs` in the client project, at the rendering boundary. A few `internal` members (e.g. `Site.Spies`, `Player.DrawCards`) that used to be visible to same-assembly callers only are exposed to the client and both test projects via `ChaosWarlords.Core/AssemblyInfo.cs`'s `[InternalsVisibleTo]`, rather than being loosened to `public` just to cross the assembly boundaries.

Below is a detailed listing of all files and their responsibilities, organized by project.

```text
ChaosWarlords.Core/                 # Logic Project Root (zero MonoGame package references)
├── ChaosWarlords.Core.csproj       # Project File
├── AssemblyInfo.cs                 # InternalsVisibleTo(ChaosWarlords, ChaosWarlords.Tests, ChaosWarlords.Core.Tests)
└── Source/
    ├── Core/
    │   ├── Contexts/                        # Data Holders (The "Glue")
    │   │   ├── EffectContext.cs             # Context for stack-based effect execution
    │   │   ├── ExecutedAction.cs            # Record capturing a single game event
    │   │   ├── InteractionRequest.cs        # Logic->UI interaction request (see Key Systems #4)
    │   │   ├── MatchContext.cs              # Scoped DI container for a single match
    │   │   └── TurnContext.cs               # Transient state for current turn
    │   ├── Data/
    │   │   ├── Dtos/                        # Data Transfer Objects
    │   │   │   ├── CardDto.cs               # Serializable card data
    │   │   │   ├── CommandDto.cs            # Serializable command data
    │   │   │   ├── EffectContextDto.cs      # Serializable effect stack state
    │   │   │   ├── GameStateDto.cs          # Serializable game state snapshot (incl. ActionSystem's targeting state - see Key Systems #4)
    │   │   │   ├── MapDto.cs                # Serializable map data
    │   │   │   ├── PlayerDto.cs             # Serializable player data
    │   │   │   ├── ReplayDataDto.cs         # Serializable replay container
    │   │   │   ├── ScoreBreakdownDto.cs     # Serializable victory score details
    │   │   │   └── VictoryDto.cs            # Serializable victory state data
    │   │   ├── Enums/                       # New home for Enums
    │   │   │   └── CommandType.cs           # Enum for command identification
    │   │   ├── LogicVector2.cs              # Deterministic integer vector struct
    │   │   └── LogicRectangle.cs            # Deterministic integer bounding box (site Bounds)
    │   ├── Interfaces/                      # Contracts (API Definitions)
    │   │   ├── Data/
    │   │   │   ├── ICardDatabase.cs         # Contract for retrieving card definitions
    │   │   │   ├── ILocalizationService.cs  # Contract for resolving "{CardId}_name"/"{CardId}_description" etc. to display text
    │   │   │   └── IDto.cs                  # Marker interface for DTOs
    │   │   ├── Logic/
    │   │   │   ├── IActionSystem.cs         # incl. OnInteractionRequested (Key Systems #4) + 3 engine-only methods ActionExecutionEngine calls back through
    │   │   │   ├── IDevourSubsystem.cs
    │   │   │   ├── IGameCommand.cs
    │   │   │   └── ISpySubsystem.cs
    │   │   └── Services/
    │   │       ├── ICommandDispatcher.cs
    │   │       ├── IGameLogger.cs
    │   │       ├── IGameRandom.cs
    │   │       ├── IMapManager.cs
    │   │       ├── IMarketManager.cs
    │   │       ├── IMarketStateManager.cs
    │   │       ├── IMatchManager.cs
    │   │       ├── INetworkProvider.cs      # Abstraction for network transport
    │   │       ├── IPlayerStateManager.cs
    │   │       ├── IReplayManager.cs        # GetNextCommand(MatchContext) - no IGameplayState dependency
    │   │       ├── ITurnManager.cs
    │   │       └── IVictoryManager.cs
    │   │       # IUIEventMediator.cs lives in the CLIENT project (ChaosWarlords/Source/Core/
    │   │       # Interfaces/Services/) - moved out of Core once ActionSystem stopped calling
    │   │       # it directly (see Key Systems #4). Nothing in Core references it any more.
    │   └── Utilities/                       # Infrastructure & Constants
    │       ├── BufferedAsyncLogger.cs       # Async-optimized logging
    │       ├── CardDatabase.cs              # Implementation of card library - CardData has no Name/Description, see LocalizationManager
    │       ├── DtoMapper.cs                 # Mapping logic between Entities and DTOs
    │       ├── GameConstants.cs             # Global configuration values
    │       ├── GameEnums.cs                 # Enums (PlayerColor, ResourceType, ActionState, etc.)
    │       ├── LocalizationManager.cs       # Loads Content/data/localization/en_US.json, resolves keys ("[MISSING:key]" fallback, never a crash)
    │       ├── MapGenerationConfig.cs       # Parameters for procedural map generation
    │       ├── MapGeometry.cs               # Deterministic geometry helper (LogicVector2 based)
    │       ├── MapLayoutEngine.cs           # Procedural map generation logic
    │       ├── ObjectPool.cs                # Generic object pooling implementation
    │       ├── Pcg32.cs                     # From-scratch PCG32 RNG algorithm (see Key Systems #6)
    │       ├── SeededGameRandom.cs          # Deterministic RNG - implements IGameRandom on top of Pcg32
    │       ├── StateHasher.cs               # FNV-1a mixing used by MatchContext.GetStateHash()
    │       └── TextCache.cs                 # Caches string measurements
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
    │       ├── MapNode.cs                   # A graph node - LogicVector2 position, not Vector2
    │       ├── NonCitySite.cs               # Represents a neutral/resource site
    │       ├── Route.cs                     # A path connection between two MapNodes
    │       ├── Site.cs                      # Abstract base class - LogicRectangle Bounds
    │       └── StartingSite.cs              # Special site where players spawn
    │
    ├── Factories/                           # Object Creation Logic
    │   ├── CardFactory.cs                   # Creates Card instances from data - resolves Name/Description via ILocalizationService
    │   ├── MapFactory.cs                    # Generates the map graph and nodes
    │   └── MatchFactory.cs                  # Assembles all dependencies for a new match
    │
    ├── Managers/                            # Business Logic Services
    │   ├── ActionInputController.cs         # Click-to-command routing for targeting (extracted from ActionSystem)
    │   ├── CommandDispatcher.cs             # Central Command Processor - snapshots before Execute(), rolls back on exception
    │   ├── MapManager.cs                    # Facade for Board Logic (LogicVector2-based queries)
    │   ├── MarketManager.cs                 # Manages the Card Market
    │   ├── MatchManager.cs                  # Manages Match & Victory
    │   ├── PlayerStateManager.cs            # Centralized player mutations
    │   ├── ReplayManager.cs                 # Replay recording and playback
    │   ├── StateRestorer.cs                 # Rebuilds MatchContext (incl. ActionSystem's targeting state) in-place from a GameStateDto snapshot
    │   ├── TurnManager.cs                   # Manages Turn Order and Phase Transitions
    │   └── VictoryManager.cs                # Calculates final scores and determines the winner
    │
    ├── Map/                                 # Map-Specific Subsystems
    │   ├── CombatResolver.cs                # Determines outcomes of battles
    │   ├── MapRewardSystem.cs               # Calculates resource generation
    │   ├── MapTopology.cs                   # Pathfinding/hit-testing, LogicVector2-based
    │   └── SpyOperations.cs                 # Handles spy placement and removal
    │
    └── Mechanics/                           # The "Rules" of the Game (100% MonoGame-free)
        ├── Actions/
        │   ├── Subsystems/                  # Logic Sub-modules
        │   │   ├── ActionExecutionEngine.cs # Execution-stack engine (ExecutionStack/PushEffect/ResolveCurrentEffect/ProcessStack) - see Key Systems #4
        │   │   ├── DevourSubsystem.cs       # Devour mechanics
        │   │   └── SpySubsystem.cs          # Spy mechanics
        │   ├── ActionSystem.cs              # Targeting state machine; delegates stack management to ActionExecutionEngine; raises OnInteractionRequested
        │   ├── CardPlaySystem.cs            # Validates and conducts card plays
        │   └── PreTargetHandler.cs          # Internal helper for pre-target auto-execution (shared by ActionSystem and ActionExecutionEngine)
        ├── Commands/                        # Command Pattern Implementations
        │   ├── ActionCompletedCommand.cs    # Signals action completion
        │   ├── AssassinateCommand.cs        # Execute assassination
        │   ├── BuyCardCommand.cs            # Purchase card
        │   ├── CancelActionCommand.cs       # Cancel targeting
        │   ├── DeployTroopCommand.cs        # Place unit
        │   ├── DevourCardCommand.cs         # Trash card
        │   ├── DiscardCardCommand.cs        # Discard a named card from a specific player's hand
        │   ├── EndTurnCommand.cs            # End turn
        │   ├── MoveTroopCommand.cs          # Move unit between nodes
        │   ├── PlaceSpyCommand.cs           # Place spy on site
        │   ├── PlayCardCommand.cs           # Play card
        │   ├── PlayFromMarketCommand.cs     # Play a market card "as if in hand" (e.g. Ulitharid), then devour it
        │   ├── PromoteCommand.cs            # Upgrade unit/site
        │   ├── ResolveSpyCommand.cs         # Execute spy action
        │   ├── ReturnOwnSpyCommand.cs       # Return one of the active player's OWN spies (e.g. Cloaker)
        │   ├── ReturnTroopCommand.cs        # Return unit to hand
        │   ├── SelectOpponentCommand.cs     # Resolve EffectType.SelectOpponent - choose which opponent to target
        │   ├── StartAssassinateCommand.cs   # Initiate assassination
        │   ├── StartReturnSpyCommand.cs     # Initiate spy return
        │   ├── SupplantCommand.cs           # Replace enemy unit
        │   ├── SwitchToNormalModeCommand.cs # Reset input mode
        │   └── ToggleMarketCommand.cs       # Open/Close market
        └── Rules/                           # Pure Logic Engines
            ├── Interfaces/
            │   └── IEffectStrategy.cs       # Strategy contract for per-effect-type validation
            ├── Strategies/                  # IEffectStrategy implementations (one per EffectType)
            │   ├── AssassinateStrategy.cs
            │   ├── DefaultStrategy.cs
            │   ├── DevourStrategy.cs
            │   ├── DiscardStrategy.cs
            │   ├── EffectTreeSearch.cs          # Shared FindFirstEffect helper (Assassinate/Supplant/Devour/PromoteFromPile strategies) - recurses into both OnSuccess and Alternative
            │   ├── MoveUnitStrategy.cs
            │   ├── PlaceSpyStrategy.cs
            │   ├── PlayFromMarketStrategy.cs
            │   ├── PromoteFromPileStrategy.cs   # EffectType.PromoteFromPile - see Key Systems #4
            │   ├── ReturnOwnSpyStrategy.cs
            │   ├── ReturnUnitStrategy.cs
            │   ├── SelectOpponentStrategy.cs    # EffectType.SelectOpponent - see Key Systems #4
            │   └── SupplantStrategy.cs
            ├── CardEffectProcessor.cs       # Applies card effects
            ├── CardRuleEngine.cs            # Validates card conditions, resolves IEffectStrategy per EffectType
            ├── DevourStrategyFactory.cs     # Strategy pattern for devour operations (by TargetLocation)
            ├── MapRuleEngine.cs             # Validates map rules
            ├── SiteControlSystem.cs         # Manages site ownership, control/total-control rewards
            └── TargetingStateEngine.cs      # Determines targeting state sequences

ChaosWarlords/                     # Client (Game) Project Root - references Core, adds MonoGame
├── ChaosWarlords.csproj           # Project File (MonoGame.Framework.DesktopGL, Content.Builder.Task)
├── app.manifest                   # Windows Application Manifest
├── Program.cs                     # Application Entry Point
├── Game1.cs                       # MonoGame Main Loop
└── Source/
    ├── Core/
    │   ├── Composition/                     # Dependency Injection composition roots
    │   │   └── GameDependencies.cs          # Concrete dependency container (holds MonoGame Game)
    │   ├── Events/
    │   │   └── InputEventArgs.cs            # Raw input event args (Vector2 position)
    │   ├── Interfaces/
    │   │   ├── Composition/
    │   │   │   └── IGameDependencies.cs     # Service container interface
    │   │   ├── Input/
    │   │   │   ├── IGameplayInputCoordinator.cs
    │   │   │   ├── IInputManager.cs
    │   │   │   ├── IInputMode.cs
    │   │   │   ├── IInputProvider.cs
    │   │   │   └── IInteractionMapper.cs
    │   │   ├── Rendering/
    │   │   │   ├── IButtonManager.cs
    │   │   │   ├── IGameplayView.cs
    │   │   │   ├── IMainMenuView.cs
    │   │   │   ├── IUIManager.cs
    │   │   │   └── IVictoryView.cs
    │   │   ├── Services/
    │   │   │   └── IUIEventMediator.cs      # Client-only - see Core/Interfaces/Services note above
    │   │   └── State/
    │   │       ├── IDrawableState.cs
    │   │       ├── IGameplayState.cs
    │   │       ├── IState.cs
    │   │       └── IStateManager.cs
    │   └── Utilities/
    │       ├── PooledRectangle.cs           # Zero-allocation rendering wrapper (see Key Systems #1)
    │       └── PooledVector2.cs             # Zero-allocation rendering wrapper (see Key Systems #1)
    ├── GameStates/                          # Application State Machine
    │   ├── GameplayState.cs                 # The Core Game Loop (Logic Only)
    │   ├── MainMenuState.cs                 # Entry Point / Composition Root
    │   ├── StateManager.cs                  # Stack-based State Machine implementation
    │   └── VictoryState.cs                  # Post-game summary state
    │
    ├── Input/                               # Human Interface Layer
    │   ├── MapHitTestExtensions.cs           # Screen-space hit-testing (GetNodeAt/GetSiteAt) - deliberately client-only, a headless server never needs it
    │   ├── Controllers/
    │   │   ├── PlayerController.cs          # High-Level Intent Parser
    │   │   └── ReplayController.cs          # Replay Workflow Orchestrator - increments MatchContext.SequenceNumber per replayed command (see Key Systems #6)
    │   ├── Modes/                           # Input State Machine
    │   │   ├── DevourInputMode.cs           # Input mode for trashing a card
    │   │   ├── DiscardInputMode.cs          # Input mode for a forced discard (own or cross-player, e.g. Insane Outcast/Neogi)
    │   │   ├── MarketInputMode.cs           # Input mode for interacting with market
    │   │   ├── NormalPlayInputMode.cs       # Default input mode for standard play
    │   │   ├── PromoteFromPileInputMode.cs  # Input mode for EffectType.PromoteFromPile's immediate promote-from-pile targeting
    │   │   ├── PromoteInputMode.cs          # Input mode for upgrading units/sites
    │   │   └── TargetingInputMode.cs        # Input mode for selecting targets
    │   ├── Processors/
    │   │   ├── GameplayInputCoordinator.cs  # Orchestrates input flow
    │   │   └── InteractionMapper.cs         # Translates Screen(X,Y) -> Entity
    │   └── Services/
    │       ├── InputManager.cs              # Raw MonoGame Input Wrapper
    │       └── MonoGameInputProvider.cs     # Concrete provider for MonoGame input
    │
    ├── Managers/                            # UI-Adjacent Services (not headless-safe)
    │   ├── MarketStateManager.cs            # Tracks market popup open/closed UI state
    │   ├── UIEventMediator.cs               # Implements IUIEventMediator; subscribes to
    │   │                                    # ActionSystem.OnInteractionRequested
    │   └── UIManager.cs                     # Manages layout and state of UI widgets
    │
    └── Rendering/                           # Presentation Layer (The "View")
        ├── LogicVectorExtensions.cs         # LogicVector2/LogicRectangle <-> Vector2/Rectangle
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

`ChaosWarlords.Tests/` and `ChaosWarlords.Core.Tests/` mirror the source trees above one-to-one (one test file per production file, plus `Integration/` subtrees for multi-component tests) - see [testing.md](testing.md) for the full test-project listing and test-category breakdown.

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

### 4. ActionSystem: Targeting State Machine and Execution-Stack Engine

`ActionSystem` used to be one 871-line class doing two jobs at once. As of 2026-08-31 it's split into two collaborating classes, following the same composition pattern already established for `DevourSubsystem`/`SpySubsystem`/`ActionInputController`/`PreTargetHandler`:

- **`ActionSystem`** owns the **targeting state machine**: `CurrentState`, `PendingCard`/`PendingSite`/`PendingMoveSource`/`PendingDevourCard`, `StartTargeting`/`CancelTargeting`, and every `TryStart*`/`Perform*` command-facing method. This is the half external callers (input modes, commands, tests) actually query and react to, and it still implements the full `IActionSystem` interface.
- **`ActionExecutionEngine`** (`Mechanics/Actions/Subsystems/`) owns the **execution-stack engine**: `ExecutionStack`, `PushEffect`, `ResolveCurrentEffect`, `ProcessStack`, and everything `ProcessStack` calls into - optional-effect confirmation, automatic-effect application, pre-target auto-execution. It takes `IActionSystem` as a collaborator (not the concrete class) and calls back into it through three narrow, engine-only interface methods (`EnterTargetingState`, `SetPendingCard`, `ResetTargetingToNormal`) for the handful of targeting-state transitions stack-processing needs to trigger.

`ActionSystem` delegates `ExecutionStack`/`PushEffect`/`CurrentEffect`/`ProcessStack`/`ResolveCurrentEffect` straight through to its own `ActionExecutionEngine` instance, so `IActionSystem`'s public contract - and every existing caller - is completely unchanged. The engine's own `OnActionCompleted`/`OnInteractionRequested`/`OnAutoExecuteCommand` events are forwarded by `ActionSystem`'s constructor as its own public events (C# events can only be raised by their declaring type, even through a shared interface reference, so this can't be a direct pass-through).

This allows actions to be paused (e.g., waiting for user input on an optional effect), new actions to be pushed and resolved (nested transactions), and then the original action to resume. Targets are buffered until the entire chain is valid (`Deferred Execution`).

**Cancellation - snapshot/reload, not field-by-field undo.** `StartTargeting` takes a full `GameStateDto` snapshot (the same `DtoMapper`/`StateRestorer` machinery `CommandDispatcher`'s rollback-on-exception uses - see Key Systems #6) exactly once per targeting *sequence*, not once per step (a multi-step chain like Wight's Devour → Supplant calls `StartTargeting` again for each step via `AdvancePreCommitTargeting`, and cancelling any step has always meant undoing the whole attempt, not just the latest step). `CancelTargeting` restores from that snapshot instead of clearing fields one at a time, so map state, player resources, market, void, the effect stack, and `ActionSystem`'s own targeting state all revert automatically - no per-mechanic undo code needed for future mechanics. It's best-effort: if the snapshot itself can't be taken (e.g. a lightly-mocked test double), `CancelTargeting` falls back to the original field-by-field clear rather than crashing. One thing the snapshot genuinely can't reach: a played card's move from Hand to Played, since `MatchManager.PlayCard` moves the card and pays its cost *before* pushing its effects onto the stack - `TryRestoreCardToHand` (run *after* the snapshot restore) still handles that one piece of the timeline. `TryRestoreCardToHand` matches by `Card.RuntimeId` (a `Guid`), not `Card.Id` (a `string`): the snapshot restore rebuilds the player's Hand/Played collections wholesale via `CardFactory`, which regenerates each `Card`'s `Id` fresh on every restore, so a pre-cancel `Id` is stale and would never match the post-restore collection - `RuntimeId` is the one identifier that survives a restore unchanged, so it's the only safe lookup key here (found via a real bug: cancelling correctly restored state via the snapshot but then failed to find the card by its now-stale pre-restore `Id`, leaving it stuck in `PlayedCards`).

`StartTargeting`'s own snapshot is only part of the story, though: as of 2026-09-02 there's also `ActionSystem.EnsureTargetingSnapshot()` (idempotent - snapshots only if `CurrentState == Normal` and no snapshot already exists for this sequence, extracted from `StartTargeting`'s own inline condition), called from `MatchManager.PlayCard`/`PlayCardFromMarket` *before* any of a played card's effects resolve (automatic or targeting - not just when a targeting UI actually opens), and again from `ActionSystem.EnterTargetingState` itself as defense-in-depth for any resumed-chain entry path that skips `PlayCard`/`PlayCardFromMarket` (e.g. a devour-chain resume). Why this mattered: a card shaped "an automatic effect that mutates state, THEN a mandatory targeting effect" (e.g. Matron Mother: `MoveDeckToDiscard` → `PromoteFromPile`; also Cranium Rats: `GainResource` → `SelectOpponent`) reaches its mandatory targeting step via the bare `ActionExecutionEngine.HandleInputRequiredEffect`/`SetupTargetingForRequiredEffect` → `EnterTargetingState` path, not the full `StartTargeting` method - so the automatic effect's mutation had already happened with no snapshot covering it, and cancelling the targeting step afterward left that mutation permanently applied. This was a real, exploitable bug for Matron Mother (a player could dump their whole deck to discard, then cancel, keeping the card to play again), not just a theoretical gap. `EnsureTargetingSnapshot()` closes it by snapshotting *before* any effect - automatic or targeting - resolves at all.

Optional effects (e.g. an accept/decline popup) don't call the UI layer directly from
`ActionSystem`/`ActionExecutionEngine`: it raises `OnInteractionRequested` with an `InteractionRequest` (card,
effect, and an `Action<bool> OnResponse` callback), and `UIEventMediator` (client project)
subscribes to that event in its existing `Initialize()`/`Cleanup()` and drives the actual
popup, calling `OnResponse` when the player answers. `ActionSystem` has no reference to
`IUIEventMediator` at all - no field, no `SetUIMediator` method.

**Newer immediate-targeting primitives.** Two `EffectType`/`IEffectStrategy`/`ActionState` triples were added in 2026-09, following the same established shape as `Assassinate`/`Supplant`/`PlaceSpy` (a new `EffectType` + `Strategies/` implementation + `ActionState`, resolved via the execution stack):
- `EffectType.SelectOpponent` / `SelectOpponentStrategy` / `ActionState.TargetingOpponentSelect` - the generic "target a player" primitive: the active player picks which opponent (matching an eligibility threshold) becomes `TurnManager.ForcedActingPlayer` for whatever `OnSuccess` chains off it, e.g. Cranium Rats' "choose one opponent with more than 3 cards to discard a card".
- `EffectType.PromoteFromPile` / `PromoteFromPileStrategy` / `ActionState.TargetingPromoteFromPile` - "promote a card right now from an expanded pool" (`CardLocation.DiscardPile`, or `CardLocation.HandOrDiscard` for hand+discard+the source card itself), e.g. Matron Mother/Necromancer. Explicitly a *different*, independent primitive from the pre-existing `EffectType.Promote` (the deferred end-of-turn promotion-credit flow driven by `PromoteInputMode`/`ActionState.SelectingCardToPromote`, e.g. Noble/Cultist of Myrkul) - the two are not to be conflated.

**Per-effect targeting filters and modifiers (2026-09-02 through 09-05).** A second wave of primitives, each a small field/flag rather than a new `EffectType`/`ActionState` pair, added to `CardEffect` (`Entities/Cards/CardEffects.cs`) and consumed by the existing strategies/engine:
- `CardEffect.TargetNeutralTroopOnly` - restricts an Assassinate/Supplant's valid targets to white/unaligned troops only (e.g. Ravenous Zombies).
- `CardEffect.IgnoresPresenceRequirement` - lets a Supplant's assassinate-half skip the normal Presence check at the target site (e.g. Ogre Zombie, Master of Melee-Magthere, Olhydra). `Card.CloneEffect` must copy this flag along with `TargetNeutralTroopOnly` - a past bug dropped it on clone, silently and permanently stripping the override the first time a targeting selection involving the card got cancelled or rolled back (see `reference/bug-log.md` in the `tyrants-rules` skill).
- `IEffectStrategy.SupportsRepeat` + `EffectContext.RemainingRepeats` - lets a targeting effect ask to be resolved N times in a row instead of once (currently only `AssassinateStrategy` opts in, e.g. Deathblade). `RemainingRepeats` is part of `EffectContextDto` and round-trips through the same snapshot/restore machinery as the rest of the execution stack (see Key Systems #6).
- `CardEffect.DynamicAmountSource` / `DynamicAmountDivisor` - computes an effect's numeric amount from live board state at resolution time (via `CardEffectProcessor.ResolveAmount`) instead of a fixed JSON constant, e.g. White Dragon.
- `Card.ReactiveDiscardEffect` - a top-level card field (not a `CardEffect`) applied by `DiscardCardCommand.Execute` when an opponent forces this card to be discarded from hand, e.g. Grimlock.

`IActionSystem.CurrentSourceEffect` is a related, smaller addition: a public read of "which `CardEffect` is currently driving targeting," used by `ActionInputController`/`AssassinateCommand`/`SupplantCommand` to apply the per-effect filters above (`TargetNeutralTroopOnly`, `IgnoresPresenceRequirement`) at click-validation time rather than only at resolution time.

### 5. Card Rule Engine
Card logic is validated by a centralized `CardRuleEngine` using a Chain of Responsibility pattern. `EffectCondition` definitions allow data-driven rules (defined in JSON), separating validation logic from effect execution.

`ConditionType` covers the usual resource/board-state checks (`ControlsSite`, `HasTroopsDeployed`, `HasResourceAmount`, `InnerCircleCount`, `HandSize`) plus, as of 2026-09, `ConditionType.OpponentPresentAtSite`: reads `ActionSystem.PendingSite` to check whether another player has a spy or troop (`SitePresenceType.Spy`/`SitePresenceType.Troop`) at the site a chained `PlaceSpy` effect just targeted, e.g. Banshee (bonus Power if an opponent has a spy there) and Infiltrator (bonus Power if an opponent has a troop there).

`CardRuleEngine.GetStrategy(EffectType)` is the lookup used throughout `CardEffectProcessor` to resolve the right `IEffectStrategy` (12 implementations in `Mechanics/Rules/Strategies/`, one per `EffectType` family - `EffectTreeSearch.cs` in the same folder is a shared helper, not an `IEffectStrategy`) for `IsTargetingEffect`/`HasValidTargets`/`SupportsRepeat` checks - this is the extension point for a new effect type's targeting behavior, not a hardcoded switch.

### 6. Multiplayer Readiness

The architecture includes concrete infrastructure for network synchronization:

**State Verification:**
- **State Hashing**: `MatchContext.GetStateHash()` generates deterministic hashes for desync detection (built on `StateHasher`, FNV-1a based)
- **Hash Coverage**: Sequence numbers, turn metadata, map state, player resources, market state
- **Culture-Invariant**: Uses `InvariantCulture` for consistent formatting across locales

**Deterministic RNG:**
- **`SeededGameRandom`** implements `IGameRandom` on top of **`Pcg32`** - a from-scratch, ~15-line implementation of the PCG32 algorithm (public domain, O'Neill's pcg-random.org). This replaced a direct `System.Random` wrapper: .NET does not guarantee `Random`'s algorithm/output stays identical across .NET versions, only that it stays deterministic *within* a fixed one - a real risk for a project whose entire replay/multiplayer story depends on "same seed → same sequence, forever, everywhere". `Pcg32` has no runtime dependency at all, so a seed's sequence is stable regardless of .NET version or OS.
- Bounded value generation uses the "Debiased Modulo (Once)" rejection scheme (the same approach OpenBSD's `arc4random_uniform` uses) to avoid modulo bias.

**Snapshot / Rollback Machinery:**
- **`DtoMapper.ToGameStateDto()`**: serializes the entire game state - map, players, market, void pile, transient marked-for-devour cards, the effect stack, and `ActionSystem`'s own targeting state (`CurrentState` + `Pending*`) - into a `GameStateDto`.
- **`StateRestorer.RestoreState()`**: rebuilds a live `MatchContext` in-place from a `GameStateDto`, mutating existing Map/Site/Node instances (so references other code holds stay valid) while re-resolving Card references fresh via `ICardDatabase.GetCardById` (a known, documented limitation - a restored card is a new instance with the same `Id`, not the original reference).
- Two independent callers use this machinery today: **`CommandDispatcher`** snapshots before every command's `Execute()` and rolls back on an unhandled exception (best-effort - proceeds without rollback capability if the snapshot itself can't be taken), and **`ActionSystem.CancelTargeting`** snapshots at the start of a targeting sequence and restores on cancel (see Key Systems #4). Both share the same DTO/restore code, not parallel implementations.

**Network Abstraction:**
- **INetworkProvider Interface**: Defines contract for command transmission and state sync
- **Transport Agnostic**: Supports future implementations (Local/SignalR/TCP)
- **Event-Driven**: Callbacks for `OnCommandReceived` and `OnStateReceived`
- **Async Operations**: All network calls use `Task` for non-blocking I/O

**Existing Infrastructure:**
- **Centralized Mutation**: All resource changes flow through `IPlayerStateManager`
- **Action Sequencing**: Commands track sequence numbers for ordering (incremented by `CommandDispatcher` before every live `Execute()`, and explicitly by `ReplayController.UpdatePlayback` for every replayed command too - replay bypasses `CommandDispatcher` entirely, so nothing else advances it)
- **Separation of Concerns**: Logic never touches UI, allowing headless execution
- **Compiled Boundary**: `ChaosWarlords.Core` has zero MonoGame package references, and `ChaosWarlords.Core.Tests` proves that boundary is exercised in test isolation too - see "The Four Projects" above


### 7. MatchContext vs IGameplayState: Separation of Concerns

**MatchContext** and **IGameplayState** serve distinct, complementary purposes:

#### MatchContext (Pure Game State & Logic)
- **Purpose**: Scoped DI container holding all game state and logic for a single match
- **Contains**: Managers, systems, and game state (players, cards, map, etc.)
- **Used By**: Game logic, commands, rules engines
- **Properties**:
  - `TurnManager`, `MapManager`, `MarketManager`, `MatchManager`
  - `ActionSystem`, `CardRuleEngine`, `PlayerStateManager`, `Logger`
  - `ActivePlayer`, `VoidPile`, `Random`
- **Lives in**: `ChaosWarlords.Core` (headless-buildable and headless-testable)

#### IGameplayState (UI State Machine & Input Coordination)
- **Purpose**: Manages UI state, input modes, and user interaction flow
- **Contains**: UI managers, input coordinators, state machine logic
- **Used By**: Input controllers, UI mediators, rendering layer
- **Properties**:
  - `IsPauseMenuOpen`, `IsMarketOpen`, `IsConfirmationPopupOpen`
  - `UIManager`, `MarketStateManager`, `InputCoordinator`
  - `SwitchToNormalMode()`, `SwitchToTargetingMode()`, `RecordAndExecuteCommand()`
- **Accesses**: `MatchContext` via `MatchContext` property for game state queries
- **Lives in**: `ChaosWarlords` (the client project - MonoGame-dependent)

#### Why Both Are Needed
- **MatchContext**: Headless-compatible, contains no UI dependencies, can run on server
- **IGameplayState**: Client-only, manages UI state machine and user interaction
- **Clear Boundary**: Game logic never touches UI state; UI layer accesses game state via MatchContext - enforced by a compiled assembly boundary, not just convention

This separation enables:
- ✅ Headless server support (`MatchContext` only) - `ChaosWarlords.Core` builds and runs with no MonoGame package at all, and `ChaosWarlords.Core.Tests` proves the test suite does too
- ✅ Clean testing (mock `MatchContext` for logic tests, mock `IGameplayState` for UI tests)
- ✅ Single Responsibility Principle (each handles one concern)

---

## Design Patterns Used

### 1. Dependency Injection
Used throughout the codebase to ensure testability and decoupling. Dependencies are strictly constructor-injected.

### 2. Strategy Pattern
Used in Input Modes (`IInputMode`) to handle different interaction contexts (Normal vs Targeting), and in `CardRuleEngine`/`DevourStrategyFactory` (`Mechanics/Rules/Strategies/`, `IEffectStrategy`) to resolve per-effect-type validation and devour-location logic without a giant `switch`.

### 3. Command Pattern
Used for all game actions (`IGameCommand`) to support replay capabilities, undo functionality, and network serialization.

### 4. Parameter Object
The `IGameDependencies` object groups core services to simplify composition roots and prevent constructor explosion.

---

## Code Quality & Maintainability

### EnsureTargetingSnapshot & the Cranium Rats/Matron Mother Cancellation Gap (2026-09-02)
See Key Systems #4's "Cancellation" paragraphs above for the full description. Summary (full detail in `RESOLVED.txt`/commits `34e8222`/`5b4e95a`):
- A 4-lens council review of the `PromoteFromPile` primitive found `CancelTargeting`'s snapshot was taken too late for cards shaped "automatic mutation, then mandatory targeting" - confirmed exploitable on Matron Mother (dump deck to discard, cancel the promote step, keep the card) and the pre-existing Cranium Rats gap. Fixed via `IActionSystem.EnsureTargetingSnapshot()`, called from `MatchManager.PlayCard`/`PlayCardFromMarket` before any effect resolves, plus a defense-in-depth call from `ActionSystem.EnterTargetingState`.
- That fix's own verification surfaced two further real production bugs: `TryRestoreCardToHand`/`CancelTargeting` were matching on the stale pre-restore `Card.Id` instead of the restore-stable `Card.RuntimeId`, leaving a cancelled card stuck in `PlayedCards` instead of returning to Hand; and `PlayerDto` was silently missing `Player.PendingFreeTroops` entirely (unlike every other per-turn resource field), so `StateRestorer.RestorePlayers` dropped it on *every* restore path, not just this one - including `CommandDispatcher`'s rollback-on-exception.

### ActionSystem Decomposition & Transactional Cancellation (2026-08)
See Key Systems #4 above for the full description. Summary:
- Extracted the execution-stack engine into `ActionExecutionEngine`, following the codebase's own established subsystem-composition pattern. `ActionSystem.cs` dropped from 871 to 680 lines at the time of this split; later feature work (new primitives, `CurrentSourceEffect`) has since added lines back, so don't treat either number as a current fact.
- Replaced `CancelTargeting`'s field-by-field imperative undo with a full-state snapshot/restore, reusing `CommandDispatcher`'s existing rollback machinery instead of a parallel implementation.
- Added `ChaosWarlords.Core.Tests` so "Core is headless" is provable at the test level, not just the build level.
- Replaced `SeededGameRandom`'s `System.Random` engine with a from-scratch `Pcg32` implementation for cross-.NET-version determinism.
- `StateRestorer` now also restores `ActionSystem`'s own targeting state (`CurrentState`/`Pending*`), not just Map/Player/Market/Void/EffectStack.

### Cyclomatic Complexity Reduction (2026-01)
The codebase underwent significant refactoring to reduce cyclomatic complexity and improve maintainability:

**Refactored Components:**
- `ActionSystem.StartTargeting`: Reduced from CC 26 to 6 (77% reduction)
- `TargetingStateEngine.TraverseForNext`: Reduced from CC 26 to 8 (69% reduction)
- `CardEffectProcessor.ApplyDevour`: Reduced from CC 16 to 6 (63% reduction)
- `CardEffectProcessor.ProcessNextEffect` (since renamed/absorbed into the `ActionExecutionEngine` stack-processing methods - see the 2026-08 entry above): Reduced from CC 14 to 7 (50% reduction)

**New Helper Classes:**
1. **PreTargetHandler** (Internal) - Encapsulates pre-target auto-execution logic extracted from `ActionSystem`
   - Handles target type detection (Devour, MapNode, Site)
   - Manages target consumption to prevent "zombie" executions
   - Single responsibility: pre-selected target execution

2. **DevourStrategyFactory** (Internal) - Strategy pattern for devour operations
   - `IDevourStrategy` interface with location-specific implementations
   - `DevourFromHandStrategy`, `DevourFromMarketStrategy`, `DevourSelfStrategy`, `DevourFromInnerCircleStrategy`
   - Eliminates conditional complexity in `CardEffectProcessor`

**Benefits:**
- All methods now meet industry standards (CC ≤ 10)
- Improved testability through focused, single-purpose methods
- Enhanced readability with reduced nesting depth (5 → 2)
- Better adherence to SOLID principles (Single Responsibility, Strategy Pattern)

### Encapsulation Hardening (2026-01)
To prevent "state leaks" and ensure system stability:
1. **Player Encapsulation**: Inherently unsafe collections (`List<Card>`) in `Player.cs` were replaced with `IReadOnlyList<Card>`. Helper methods like `AddToHand` were restricted to `internal` scope, accessible only by Managers.
2. **Match Construction**: The "God Object" anti-pattern in `GameplayState` was resolved by moving complex object graph construction to `MatchFactory`.
3. **Null Safety**: `SiteControlSystem` and other Managers defined strict constructor contracts, throwing `ArgumentNullException` immediately if dependencies are missing.

### Performance & Optimization
**New standard as of 2026-01**:
- **Zero-Allocation Rendering**: All "Hot Path" rendering loops (UI, Map, Cards) utilize `ObjectPool<T>` via `PooledRectangle` and `PooledVector2` wrappers - currently one shared static pool per type, fine for today's single-process/single-match client.
- **Allocation Budget**: `UIRenderer`, `MapRenderer`, and `CardRenderer` have a budget of **0 allocations per frame**.
- **Not yet built**: a multiplayer server hosting several concurrent matches will need per-context pools instead of the single shared one, so pooled objects from one match's simulation/rendering can't leak into another's. A `PoolManager`-style keyed-pool registry existed for this but was removed 2026-09-01 (unwired anywhere, unvalidated by a real second caller) - revisit designing it once there's an actual second context to isolate. See planning.txt.
