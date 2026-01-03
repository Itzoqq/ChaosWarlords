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

