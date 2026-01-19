# Coding Guidelines

**Status**: Established Patterns (Mandatory)  
**Last Updated**: 2025-12-31

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
_playerStateManager.SpendInfluence(player, 3);
```

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
- Primitives (`Vector2` for positions, `Color` enum)

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
- Be serializable (use IDs, not object references for multiplayer)
- Record execution in `TurnContext` or `ReplayManager`
- Be stateless (all data passed in constructor)

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
- Logging (`IGameLogger`)
- Enums (`PlayerColor`, `CardAspect`)

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

Before submitting a PR, verify:

- [ ] No `new Random()` - use `IGameRandom`
- [ ] No direct `player.Power +=` - use `IPlayerStateManager`
- [ ] Dependencies are interfaces, not concrete classes
- [ ] No `SpriteBatch` or MonoGame types in logic layer
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

**Pattern**:
1. Check `HasValidTargets` early (in `CardPlaySystem` or UI).
2. Check `IsConditionMet` before applying specific sub-effects.
3. Keep `CardEffectProcessor` dumb (execution only).

---


---

## 9. Input Coordination System

**Rule**: Application of input must follow the tiered orchestration flow: `InputManager` -> `Controller` -> `Coordinator` -> `InputMode`.

**Why**: Separates raw input detection from intent and execution, allowing context-aware flexibility (e.g., clicking a card in Normal mode plays it, but in Targeting mode selects it).

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

**Cancellation**: `ActionSystem.CancelTargeting()` clears the buffer, restoring the hand state as if nothing happened.

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
*   **Use Builders**: For specific edge cases or unique configurations (e.g., `new PlayerBuilder().WithPower(0).Build()`).
*   **Avoid**: Raw constructors in tests, as they are brittle to change.

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


