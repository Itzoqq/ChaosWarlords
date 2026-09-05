# Coding Guidelines

**Status**: Established Patterns (Mandatory)  
**Last Updated**: 2026-09-02

These are **established patterns** that all contributors must follow. Violations will cause multiplayer desyncs, test failures, or architectural degradation.

---

## 1. Deterministic RNG (CRITICAL)

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

**Enforcement**:
- `CollectionHelpers.Shuffle()` was removed - use `IGameRandom.Shuffle()` instead
- All managers require `IGameRandom` in constructor (no default/nullable)
- Tests must provide `IGameRandom` mock or `SeededGameRandom` instance

---

## 2. Centralized Resource Management

**Rule**: All player resource changes MUST go through `IPlayerStateManager`.

**Why**: Centralized mutation enables logging, validation, event emission, and multiplayer sync.

```csharp
// ❌ WRONG: Direct mutation bypasses logging and events
player.Power += 5;
player.Influence -= 3;

// ✅ CORRECT: Centralized, logged, and validated
_playerStateManager.AddPower(player, 5);
_playerStateManager.TrySpendInfluence(player, 3);
```

Spend operations (`TrySpendPower`, `TrySpendInfluence`) return `bool` - they check affordability themselves and no-op on failure rather than throwing or letting a caller under/overspend, so always check the return value rather than assuming success.

**Covered Resources**:
- Power, Influence, Victory Points
- Troops, Spies (barracks counts)
- Card operations (Draw, Discard, Promote)

**Benefits**:
- All changes are logged for debugging
- Events emitted for UI updates
- Validation prevents invalid states
- Multiplayer sync point

---

## 3. Interface-Based Dependencies

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

**Convention**: Every manager/service should have a corresponding interface in `Source/Core/Interfaces/Services/`.

---

## 4. Separation of Logic and Rendering

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

**Allowed in Logic**:
- Interfaces (`IGameplayView`, `IUIManager`)
- DTOs (`GameStateDto`, `CardDto`)
- Domain models (`Player`, `Card`, `MapNode`)
- Primitives (`LogicVector2`/`LogicRectangle` for positions - see Rule #17; `Color` enum). `Microsoft.Xna.Framework.Vector2` itself is never allowed in logic, not even for positions - Core has zero references to any `Microsoft.Xna.Framework` type at all.

**NOT Allowed in Logic**:
- `SpriteBatch`
- `Texture2D`
- `GraphicsDevice`
- `SpriteFont`
- Any `Microsoft.Xna.Framework.Graphics.*` types

---

## 5. Command Pattern for Actions

**Rule**: All significant game actions must be encapsulated as `IGameCommand`.

**Why**: Enables replay, undo, logging, and multiplayer command transmission.

```csharp
// ✅ CORRECT: Action as command (trimmed from the real PlayCardCommand)
public class PlayCardCommand : IGameCommand
{
    public CommandType Type => CommandType.PlayCard;

    // Carries only IDs, not object references - resolved against the active
    // player's hand fresh in Validate()/Execute(), so the command itself stays
    // serializable/replay-safe.
    public Guid CardRuntimeId { get; }
    public string CardId { get; }

    public PlayCardCommand(Card card) { CardRuntimeId = card.RuntimeId; CardId = card.Id; }

    private Card? ResolveCard(MatchContext context) =>
        context.TurnManager.ActivePlayer.Hand.FirstOrDefault(c => c.RuntimeId == CardRuntimeId);

    public bool Validate(MatchContext context) => ResolveCard(context) != null;

    public void Execute(MatchContext context)
    {
        var card = ResolveCard(context);
        if (card != null) context.MatchManager.PlayCard(card);
    }

    public GameCommandDto ToDto() => new PlayCardCommandDto { CardId = CardId, CardRuntimeId = CardRuntimeId };
}
```

**Commands must**:
- Implement `IGameCommand` (`Type`, `ToDto()`, `Validate(MatchContext)`, `Execute(MatchContext)` - there is no `Execute(IGameplayState)` overload; commands only ever touch `MatchContext`, never the client-only `IGameplayState`)
- Be serializable (use IDs, not object references for multiplayer/replay)
- Be stateless (all data passed in constructor)
- Get recorded by `CommandDispatcher` (which snapshots before `Execute()` and rolls back on exception) - a command doesn't record itself

**Examples**: See `Source/Mechanics/Commands/` for all implemented commands.

---

## 6. No Global State

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

**Exceptions** (allowed static usage):
- Constants (`GameConstants`)
- Pure utility functions (no state)
- Enums (`PlayerColor`, `CardAspect`)

Logging is NOT one of these exceptions: `IGameLogger` is constructor-injected everywhere (e.g. `SiteControlSystem(IGameLogger logger)`), never a static/global logger - there is no static `Logger` class anywhere in the codebase. It follows the same Rule #7 constructor-injection discipline as every other dependency.

---

## 7. Constructor Injection

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

**Benefits**:
- Dependencies are explicit
- Objects are always in valid state
- Easier to test (clear what needs to be mocked)
- Prevents null reference exceptions

---

## Quick Reference Checklist

This covers Rules 1-7 only (the ones with the highest "easy to violate without noticing" risk) - it's a fast pre-PR pass, not full coverage of Rules 8-23. For card/mechanic work specifically, also run the `add-card` skill's own Tests section (step 3) and, for anything non-trivial, the risk-hotspot check (`master.md`'s "Risk-hotspot check" section).

Before submitting a PR, verify:

- [ ] No `new Random()` - use `IGameRandom`
- [ ] No direct `player.Power +=` - use `IPlayerStateManager`
- [ ] Dependencies are interfaces, not concrete classes
- [ ] No `SpriteBatch` or MonoGame types (or bare `Vector2`) in logic layer
- [ ] Actions are `IGameCommand` implementations
- [ ] No `static` game state
- [ ] All dependencies via constructor

---

## See Also

- [Architecture Guide](architecture.md) - System design and structure
- [Testing Guide](testing.md) - Test patterns and organization
- [Contributing Guide](../CONTRIBUTING.md) - PR process and workflow

---

## 8. Card Rule Engine (New Standard)

**Rule**: Use `CardRuleEngine` for all card validation and conditional logic.

**Why**: Centralizing validation (Chain of Responsibility) prevents duplicated logic and allows data-driven card definition.

```csharp
// ❌ WRONG: Hardcoding logic in EffectProcessor
if (effect.Type == EffectType.GainResource && player.ControlsSite)
{
    // Apply bonus
}

// ✅ CORRECT: Use CardRuleEngine
if (context.CardRuleEngine.IsConditionMet(player, effect))
{
    // Processor only executes, Engine validates
    ApplyEffect(effect);
}
```

**Key Components**:
- **CardRuleEngine**: The service (injected via `MatchContext`) that evaluates rules.
- **EffectCondition**: The data object (from JSON) defining requirements (e.g., `ControlsSite`).
- **HasValidTargets**: Checks if an effect can even initiate (e.g., prevents playing "Devour" with empty hand).
- **IEffectStrategy** / **`CardRuleEngine.GetStrategy(EffectType)`**: the extension point for a new effect type's targeting behavior. Twelve implementations live in `Mechanics/Rules/Strategies/` (one per `EffectType` family - `AssassinateStrategy`, `DevourStrategy`, `PromoteFromPileStrategy`, etc.; `EffectTreeSearch.cs` in the same folder is a shared helper, not an `IEffectStrategy`), each answering `IsTargetingEffect`/`HasValidTargets`/`SupportsRepeat` for its effect type. Adding a new targeting-shaped `EffectType` means adding a strategy here, not a new `if`/`switch` branch in `CardEffectProcessor`.

**Pattern**:
1. Check `HasValidTargets` early (in `CardPlaySystem` or UI).
2. Check `IsConditionMet` before applying specific sub-effects.
3. Keep `CardEffectProcessor` dumb (execution only) - it asks `CardRuleEngine.GetStrategy` rather than branching on `EffectType` itself.

---


---

## 9. Input Coordination System

**Rule**: Gameplay input follows an event-driven flow: `InputManager` fires raw events, and `GameplayInputCoordinator` handles them by checking blocking state and delegating to the active `IInputMode`, which decides what (if anything) happens.

**Why**: Separates raw input detection from intent and execution, allowing context-aware flexibility (e.g., clicking a card in Normal mode plays it, but in Targeting mode selects it).

`GameplayInputCoordinator` and `PlayerController` are independent subscribers to the same `InputManager.OnInputEvent` - the coordinator does not receive its events *through* the controller. `PlayerController` handles high-level/global concerns (e.g. toggling the pause menu); `GameplayInputCoordinator` handles gameplay input and is blocked outright while any popup/menu is open, so the two never compete for the same event.

```csharp
// Real GameplayInputCoordinator.HandleInputEvent (trimmed)
private void HandleInputEvent(object? sender, InputEventArgs e)
{
    // BLOCKING CHECK: if any overlay/popup is open, gameplay input is not processed here at all.
    if (_state.IsPauseMenuOpen || _state.IsConfirmationPopupOpen || _state.IsOptionalEffectPopupOpen)
        return;

    // Delegate straight to the active mode - it returns a command, or null if the click means nothing right now.
    IGameCommand? command = _currentMode.HandleInteraction(
        e, _context.MarketManager, _context.MapManager, _context.ActivePlayer, _context.ActionSystem);

    if (command != null)
        _state.RecordAndExecuteCommand(command);
}
```

Each `IInputMode.HandleInteraction` interprets the same click differently depending on context - e.g. `TargetingInputMode` resolves a click to a map node/site and asks `ActionSystem` whether it's a valid target, where `NormalPlayInputMode` would instead resolve a click to a card to play.

---

## 10. Transactional Command Execution

**Rule**: Complex multi-step actions (e.g., "Devour card then Supplant unit") must use `ActionSystem` deferred execution.

**Why**: Ensures atomic execution. If the player cancels partway through (e.g., after picking a card to devour but before picking a unit to supplant), the game state must roll back completely.

**Pattern**:
1.  **Deferred Execution**: User selects targets, but action holds off until chain is complete.
2.  **State Buffering**: Selections are held in temporary state (e.g., `PendingDevourCard`).
3.  **Atomic Commit**: The command executes only when all steps are valid and confirmed.

```csharp
// Example: Wight Card (Devour -> Supplant)

// 1. User chooses Devour path
ActionSystem.TryStartDevourHand(wightCard, 
    onComplete: () => ActionSystem.StartTargeting(ActionState.TargetingSupplant, wightCard),
    deferExecution: true);  // Don't execute yet!

// 2. User selects card -> Buffered in PendingDevourCard (not removed from hand yet)
ActionSystem.HandleDevourSelection(targetCard);

// 3. User selects node for Supplant -> SupplantCommand executes
// ONLY NOW does the entire transaction execute atomically
```

**Cancellation (as of 2026-08)**: `ActionSystem.StartTargeting` takes a full `GameStateDto`
snapshot exactly once per targeting sequence (not once per step of a multi-step chain -
cancelling any step has always meant undoing the whole attempt). `CancelTargeting()` restores
from that snapshot - reverting map state, player resources, market, void, the effect stack,
and `ActionSystem`'s own targeting state all at once - instead of clearing fields one at a
time. This means a future mechanic that mutates state mid-targeting does **not** need its own
bespoke undo step here; the snapshot/restore covers it automatically. The one thing the
snapshot can't reach is a played card's move from Hand to Played (that happens before
targeting starts), which `TryRestoreCardToHand` still handles separately, by `RuntimeId` (see
Rule #22 below - `Card.Id` is not safe to use here). See `ActionSystem.CancelTargeting`'s own
doc comment for the full breakdown, and
[architecture.md](architecture.md#4-actionsystem-targeting-state-machine-and-execution-stack-engine).

**Snapshot timing for "automatic effect, then mandatory targeting" cards (as of 2026-09)**:
The snapshot above is only correct if it's taken *before* a card's first effect mutates state.
`StartTargeting`/`EnterTargetingState` take it automatically, but a card whose first effect is
an unconditional mutation (e.g. Matron Mother's `MoveDeckToDiscard`, Cranium Rats'
`GainResource`) followed by a *later* effect that requires targeting (`PromoteFromPile`,
`SelectOpponent`) mutates state before either of those ever runs - by the time targeting
starts, it's too late to snapshot the pre-mutation state, and cancelling silently keeps the
mutation instead of reverting it. This shipped as a real, exploitable bug on two cards (Matron
Mother could dump the whole deck to discard, then cancel the promote step and keep the card).
**Rule**: any new top-level card-resolution entry point (i.e. anything that calls
`CardEffectProcessor.ResolveEffects`, mirroring `MatchManager.PlayCard`/
`PlayCardFromMarket`) must call `ActionSystem.EnsureTargetingSnapshot()` *before* resolving a
card's effects, not just rely on targeting entry points to snapshot on their own.
`EnsureTargetingSnapshot()` is idempotent (no-ops once a sequence is already in flight), so
calling it defensively is always safe.

```csharp
// ✅ CORRECT: snapshot before ANY effect (automatic or targeting) can run
_context.ActionSystem.EnsureTargetingSnapshot();
CardEffectProcessor.ResolveEffects(card, _context, hasFocus, _logger);
```

---

## 11. Lookahead Validation Engine

**Rule**: Before allowing a card play or action initiation, the ENTIRE chain of required effects and targets must be validated recursively.

**Why**: Prevents "Action Fizzling" where a player pays a cost (e.g., plays a card) but cannot complete the main effect (e.g., no valid targets for the result). Also prevents UI deadlock where a player enters a targeting mode with no valid targets.

**Pattern**: `CardRuleEngine.IsEffectChainValid(player, effect, sourceCard)`

1.  **Recursive Validation**: Checks the current effect's requirements (e.g., `HasValidTargets`).
2.  **Breadth & Depth**: If the current effect succeeds, it recursively checks `effect.OnSuccess`.
3.  **Atomic Validity**: Returns `true` only if the *entire* path is actionable.

```csharp
// Example: Validate entire chain before showing "Play" button
if (_cardRuleEngine.IsEffectChainValid(player, card.Effects.First(), card))
{
    // Enable Play interaction
}
```

**Deadlock Prevention Helpers**:

---

## 12. Replay System & Determinism

**Rule**: All Game Logic must be deterministic and serializable to support the Replay Manager.

**Why**: To support "Save/Load Replay" and multiplayer sync, the game must be able to reconstruct the exact state from a sequence of commands and a seed.

**Requirements**:
1.  **Seeded RNG**: As per Rule #1, never use system time or unseeded randoms.
2.  **Command Pattern**: All mutations must happen via `IGameCommand` (Rule #5).
3.  **DTO Mapping**: Every `IGameCommand` must have a corresponding `GameCommandDto` in `DtoMapper`.
4.  **No Logic in Views**: Views are not recorded, so logic cannot depend on them (Rule #4).

**Pattern**: `ReplayManager.RecordCommand()`
- Automatically hooks into `CommandDispatcher`.
- Serializes commands to JSON DTOs.
- `DtoMapper` acts as the translator between runtime objects (Entities) and storage objects (DTOs).

---

## 13. UI Event Mediator Pattern

**Rule**: The `UIEventMediator` is the **ONLY** bridge between UI events (popups, pause menu) and Game Logic.

**Why**: Prevents circular dependencies between Logic and UI. The UI Manager shouldn't know about `GameplayState`, and `GameplayState` shouldn't manage UI popups directly.

**Pattern**:
1.  **UI Request**: UI Manager fires an event (e.g., `OnAssassinateRequest`).
2.  **Mediator Intercepts**: `UIEventMediator` handles the event (e.g., calls `_actionSystem.TryStartAssassinate()`).
3.  **Logic executes**: The logic systems do their work.
4.  **Feedback**: Logic emits events (`OnActionCompleted`) which the Mediator listens to, to reset UI state (switching modes).

```csharp
// Example: Mediator handling an external request
private void HandleAssassinateRequest(object sender, EventArgs e)
{
    // 1. Call Logic
    _actionSystem.TryStartAssassinate();
    
    // 2. Adjust UI State based on Logic result
    if (_actionSystem.IsTargeting())
    {
        _gameState.SwitchToTargetingMode();
    }
}
```

---

## 14. Testing Patterns

**Rule**: Tests must be readable, isolated, and deterministic.

### 14.1 Naming Convention
Use `MethodName_Scenario_ExpectedBehavior` to clearly describe the test intent.

```csharp
[TestMethod]
public void AddPower_WithPositiveAmount_IncreasesPlayerPower()
{
    // ...
}
```

### 14.2 Parameterized Tests
Use `[DataRow]` to test multiple scenarios without code duplication. **CRITICAL**: Always create fresh instances (Arrange) inside the test method, never reuse shared state between DataRows.

```csharp
[TestMethod]
[DataRow(0, 1, false)]  // Scenario 1
[DataRow(1, 1, true)]   // Scenario 2
public void CanDeploy_ValidatesCapabilities(int power, int troops, bool expected)
{
    // Arrange: Create FRESH builder for each run
    var player = new PlayerBuilder().WithPower(power).WithTroops(troops).Build();
    
    // Act & Assert...
}
```

### 14.3 Test Data Usage
*   **Use `TestData` helper**: For common, standard scenarios (e.g., `TestData.Players.RedPlayer()`).
*   **Use Builders**: For specific edge cases or unique configurations (e.g., `new PlayerBuilder().WithPower(0).Build()`, or `new MatchContextBuilder().WithTurnManager(turnManager).WithSeed(999).Build()` - every dependency defaults to a fresh mock, override only what the test needs).
*   **Avoid**: Raw constructors in tests, as they are brittle to change - except where a builder wouldn't actually reduce anything (e.g. a test that overrides most of `MatchContext`'s 7 dependencies with real, specifically-configured instances anyway; see `MatchContextBuilder`'s own doc comment in `TestHelpers.cs`).

### 14.4 Mocking with NSubstitute
Always substitute interfaces, not concrete classes.

```csharp
// Accept ANY IGameRandom to robustly handle method signature changes
_mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(deck);
```

### 14.5 Deterministic RNG in Tests
**Rule**: NEVER use `new Random()` in tests.
*   **Unit Tests**: Mock `IGameRandom`.
*   **Integration/Logic Tests**: Use `SeededGameRandom(seed)` to ensure reproducibility.


---

## 15. Object Pooling & Zero-Allocation Rendering

**Rule**: **NEVER** allocate `new Rectangle` or `new Vector2` inside `Draw()` calls or tight loops.

**Why**: Frequent small allocations trigger Gen0 Garbage Collection, causing frame stutters ("hiccups") in the rendering loop.

**Pattern**: Use `PooledRectangle` and `PooledVector2` with the `using` statement for automatic return.

```csharp
// ❌ WRONG: Generates 60 allocations/sec per call
public void Draw(SpriteBatch sb)
{
    var rect = new Rectangle(0, 0, 100, 100); // Bad!
    sb.Draw(texture, rect, Color.White);
}

// ✅ CORRECT: Zero allocations (reuses memory)
public void Draw(SpriteBatch sb)
{
    using var pooled = PooledRectangle.Rent(0, 0, 100, 100);
    sb.Draw(texture, pooled.Value, Color.White);
    // Automatically returned to pool at end of scope
}
```

**Scope Guidelines**:
- **Hot Paths**: REQUIRED. (`Draw`, `Update` loops)
- **Cold Paths**: OPTIONAL. (Initialization, User Clicks) - Optimizing here adds complexity for little gain.
- **Implementation**: The wrapper types (`PooledRectangle`) are mutable. You can update `pooled.Value` in a loop to reuse the same instance multiple times.

```csharp
// Advanced: Reusing one instance for many items
using var pooledRect = PooledRectangle.Rent(0, 0, 0, 0);

foreach (var item in items)
{
    pooledRect.Value = new Rectangle(item.X, item.Y, 10, 10); // Struct copy (fast), no heap allocation
    sb.Draw(texture, pooledRect.Value, Color.White);
}
```

---

## 16. Event-Driven Input Implementation

**Rule**: Discrete Gameplay Logic must subscribe to `InputManager` events rather than polling state.

**Why**: Polling (checking `IsKeyDown` every frame) is prone to "Double-Fire" bugs (action executes twice in 2 frames) or "Missed Input" (if framerate dips). Events (`LeftClick`, `KeyDown`) guarantee exactly one execution per physical interaction.

```csharp
// ❌ WRONG: Polling in Logic Update
public void Update()
{
    if (_inputManager.IsLeftMouseDown())
    {
        // Fires every frame button is held! Hard to control.
        FireWeapon(); 
    }
}

// ✅ CORRECT: Subscribing to Event
public void Initialize()
{
    _inputManager.OnInputEvent += HandleInput;
}

private void HandleInput(object sender, InputEventArgs e)
{
    if (e.Type == InputEventType.LeftClick)
    {
        // Fires exactly once per click
        FireWeapon();
    }
}
```

**Exceptions**: 
- **Continuous Actions**: Camera panning or Dragging items *should* use polling (`IsKeyDown(Keys.W)`), as they happen every frame.
- **UI Hover State**: Checking mouse position for tooltips consumes polling data.

---

## 17. Deterministic Geometry (Time/Space Independence)

**Rule**: All Game Logic positions must use `LogicVector2` (int-based) instead of `Vector2` (float-based).

**Why**: Floating point arithmetic is non-deterministic across different CPUs/architectures. Multiplayer sync requires identical calculations.

```csharp
// ❌ WRONG: Float vectors in logic
public Vector2 Position { get; set; }
public void Move() { Position += new Vector2(0.5f, 0); } // Precision drift!

// ✅ CORRECT: Logic Vectors
public LogicVector2 Position { get; set; }
public void Move() { Position += new LogicVector2(1, 0); } // Exact integer math
```

**Usage**:
- **Logic Layer**: Use `LogicVector2`.
- **View Layer**: Convert to `Vector2` *only* for drawing: `spriteBatch.Draw(..., entity.Position.ToVector2(), ...)`



---

## 18. Strict Encapsulation (Collections)

**Rule**: NEVER expose mutable collections (`List<T>`, `Dictionary<T,K>`) directly in public properties. Use `IReadOnlyList<T>` or `IEnumerable<T>`.

**Why**: Prevents consumers from modifying internal state without going through proper methods (bypassing validation and events).

```csharp
// ❌ WRONG: External code can .Clear() your list!
public List<Card> Hand { get; private set; }

// ✅ CORRECT: Safe for exposure
private readonly List<Card> _hand = new();
public IReadOnlyList<Card> Hand => _hand;

internal void AddCard(Card card) { ... } // Mutation controlled
```

---

## 19. Fail-Fast Constructor Injection

**Rule**: All constructor arguments must be validated for nullity immediately.

**Why**: Prevents "Silent Failures" where a system runs in a zombie state (e.g., logging disabled because logger was null).

```csharp
// ❌ WRONG: Silent failure later
public SiteControlSystem(IGameLogger logger)
{
    _logger = logger; // If null, CronJob crashes 10 mins later
}

// ✅ CORRECT: Fail immediately
public SiteControlSystem(IGameLogger logger)
{
    ArgumentNullException.ThrowIfNull(logger);
    _logger = logger;
}
```

---

## 20. State Hashing for Multiplayer Sync

**Rule**: State hashes MUST be deterministic and culture-invariant.

**Why**: Multiplayer clients must generate identical hashes to detect desyncs. Non-deterministic hashing (locale-dependent formatting, unordered collections) causes false positives.

**Pattern**: Use `InvariantCulture` for all hash string conversions

```csharp
// ❌ WRONG: Locale-dependent formatting
return hash.ToString("X");

// ✅ CORRECT: Culture-invariant
return hash.ToString("X", System.Globalization.CultureInfo.InvariantCult ure);
```

**Collection Ordering**: Always use `.OrderBy()` when iterating collections for hashing

```csharp
// ❌ WRONG: Non-deterministic iteration order
foreach (var node in MapManager.Nodes)
{
    hash = hash * 31 + node.Id;
}

// ✅ CORRECT: Deterministic ordering
foreach (var node in MapManager.Nodes.OrderBy(n => n.Id))
{
    hash = hash * 31 + node.Id;
}
```

**Coverage Requirements**:
- Sequence/turn metadata (turn number, phase, seed)
- Player resources (Power, Influence, VP, Troops)
- Map state (node occupancy)
- Market state (card count, card IDs)

---

## 21. Network Abstraction

**Status**: no networking exists yet, and nothing in the codebase currently constructs, injects, or calls `INetworkProvider` - `CommandDispatcher` and every other real caller talk to `MatchContext` directly. This rule documents the *planned* shape for when networking is added, not an already-wired pattern - don't read the snippets below as "this is how it works today."

**Rule**: Game logic must NEVER depend on concrete network implementations.

**Why**: Allows switching between Local/SignalR/TCP transports without modifying game logic. Supports testing with mock network providers.

**Pattern**: Use `INetworkProvider` interface

```csharp
// ❌ WRONG: Direct dependency on transport
public class CommandDispatcher
{
    private readonly SignalRConnection _connection;
    
    public void SendCommand(GameCommandDto dto)
    {
        _connection.InvokeAsync("SendCommand", dto);
    }
}

// ✅ CORRECT: Interface-based abstraction
public class CommandDispatcher
{
    private readonly INetworkProvider _network;
    
    public async Task SendCommandAsync(GameCommandDto dto)
    {
        await _network.SendCommandAsync(dto);
    }
}
```

**Event-Driven Reception**: Subscribe to network events, don't poll

```csharp
// Setup in initialization
_network.OnCommandReceived += HandleIncomingCommand;
_network.OnStateReceived += HandleStateSnapshot;
```

**Testing**: Mock `INetworkProvider` for unit tests

```csharp
var mockNetwork = Substitute.For<INetworkProvider>();
mockNetwork.IsConnected.Returns(true);
```

---

## 22. Card Identity Across a State Restore

**Rule**: Any code that captures a `Card` reference or identifier *before* a
`StateRestorer.RestoreState()` call and needs to find the "same" card again *after* it must key
on `Card.RuntimeId`, never `Card.Id`.

**Why**: `StateRestorer` rebuilds `Hand`/`PlayedCards`/`DiscardPile` etc. as brand-new `Card`
instances via `CardFactory`. `CardFactory` regenerates `Card.Id` (the per-instance-suffixed
string) on every rebuild, so a pre-restore `Id` captured earlier will never match anything in
the post-restore collections. `Card.RuntimeId` (a `Guid`) and `Card.DefinitionId` are the only
identifiers `CardFactory` carries across a restore unchanged. This is not hypothetical: it
shipped as a real bug in `ActionSystem.TryRestoreCardToHand`, which matched on the stale
pre-restore `Id` and silently failed to find the restored card, leaving it stuck in
`PlayedCards` instead of returning to `Hand`.

**Applies to**: any consumer of the snapshot/restore machinery, not just `ActionSystem` -
`CommandDispatcher`'s rollback-on-exception uses the same `StateRestorer.RestoreState()` path.

```csharp
// ❌ WRONG: Id is regenerated by CardFactory on every restore-rebuild
var cardId = PendingCard?.Id;
// ... StateRestorer.RestoreState(...) runs, rebuilding Hand/PlayedCards ...
var card = CurrentPlayer.PlayedCards.FirstOrDefault(c => c.Id == cardId); // never matches

// ✅ CORRECT: RuntimeId survives the rebuild unchanged
var cardRuntimeId = PendingCard?.RuntimeId;
// ... StateRestorer.RestoreState(...) runs ...
var card = CurrentPlayer.PlayedCards.FirstOrDefault(c => c.RuntimeId == cardRuntimeId);
```

See `ActionSystem.CancelTargeting`/`TryRestoreCardToHand` for the real fix, and
`Card.RuntimeId`'s own doc comment for why it - and not `Id` - is stable across a restore.

---

## 23. Comment Hygiene: Explain Current Behavior, Not History

**Rule**: Code comments and doc comments explain what the code does and why it's built this way *right now*. They are not a changelog - don't narrate previous versions, which commit/session/review found a bug, or phrases like "as of 2026-XX," "this used to X," "previously Y," "shipped as a bug and was fixed."

**Why**: A comment that reads like a diary of the code's history goes stale the moment the next change lands (nobody reliably updates or deletes it), and it makes the file harder to read for someone who just needs to know what the code does *today*. That history already has a real, permanent home - commit messages (the full "why did this change" story, permanently in `git log -p`) and `RESOLVED.txt` (the terse "what got fixed, which commit" ledger). Comments duplicating either of those just rot in place.

```csharp
// ❌ WRONG: narrates history instead of explaining current behavior
// (as of 2026-09-04) Fixed a bug where this used to strand the ExecutionStack -
// see RESOLVED.txt [a5e5d42] for the full investigation. Previously this only
// handled EffectType.Devour on accept; now it also handles non-targeting effects.
private void HandleOptionalEffectAccepted(EffectContext effect) { ... }

// ✅ CORRECT: explains what the code does and why, nothing else
// Devour resolves via its own strategy (see below); any other non-targeting
// optional effect is applied and the stack resolved immediately on accept -
// a targeting effect instead waits for the eventual click to resolve it.
private void HandleOptionalEffectAccepted(EffectContext effect) { ... }
```

**What's still fine to keep** (this rule targets *narration*, not all context):
- Explaining *why* a non-obvious design choice exists, when the "why" is still true today (e.g. Rule #22 above - "keyed on `RuntimeId`, not `Id`, because `CardFactory` regenerates `Id` on every restore-rebuild" is present-tense reasoning about current behavior, not a history lesson).
- A pointer to a *related*, still-relevant doc/skill (`planning.txt`, the `tyrants-rules` skill) when the reader genuinely needs it to understand a constraint - not as a changelog citation.
- A short "not obvious, don't 'simplify' this" warning for a real footgun, stated as a present-tense fact about the code, not a story about who found it or when.

**Where history actually belongs**: the commit message and `RESOLVED.txt`'s one-line-plus-commit-hash ledger. Neither needs a mirror copy living in the source file.
