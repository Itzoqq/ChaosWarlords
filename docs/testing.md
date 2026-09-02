# ChaosWarlords Test Architecture & Organization

## Overview
This document outlines the architecture of the test suite, split across two projects: `ChaosWarlords.Tests` (the primary suite - everything, including the client/UI/input layers) and `ChaosWarlords.Core.Tests` (a smaller, headless-only suite that references `ChaosWarlords.Core` alone, proving the logic layer is testable in isolation from MonoGame, not just buildable - see [architecture.md](architecture.md#the-four-projects)). Both follow the same conventions: **AAA Pattern** (Arrange-Act-Assert), **Test Data Builders** for readability, and **Test Categories** for efficient filtering.

## Test Organization Principles

### 1. Test Categories
Tests are categorized by scope and dependencies:

- **Unit Tests** (`[TestCategory("Unit")]`): Test single classes in isolation with mocked dependencies. Fast execution, no external dependencies.
- **Integration Tests** (`[TestCategory("Integration")]`): Test multiple components working together. May use real implementations.
- **Performance Tests** (`[TestCategory("Performance")]`): Benchmark critical operations with time thresholds.

Every test class in both projects carries a category - this is enforced by convention, not the compiler, so a newly-added test class that forgets the attribute will silently be excluded from category-filtered runs until someone notices. Check for this before adding a new test file.

**Usage**:
```bash
dotnet test --filter "TestCategory=Unit"        # Run only unit tests (fast)
dotnet test --filter "TestCategory=Integration" # Run integration tests
dotnet test --filter "TestCategory=Performance" # Run performance benchmarks
dotnet test ChaosWarlords.Core.Tests/ChaosWarlords.Core.Tests.csproj  # Headless-only subset
```

### 2. Test Structure (AAA Pattern)
All tests follow the Arrange-Act-Assert pattern:

```csharp
[TestMethod]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange: Set up test data and dependencies
    var player = new PlayerBuilder().WithPower(10).Build();

    // Act: Execute the method under test
    var result = player.CanAfford(5);

    // Assert: Verify the outcome
    Assert.IsTrue(result);
}
```

### 3. Test Data Strategy

We use a tiered approach for creating test data to ensure maintainability and readability.

**1. Primary Preference: `TestData.cs`**
Use `TestData` for standard, shared object instances. This reduces duplication and keeps tests clean.

```csharp
// ✅ Best: Reusable, consistent
var player = TestData.Players.RedPlayer();
var card = TestData.Cards.PowerCard();
```

**2. Secondary Preference: Builders (`TestHelpers.cs`)**
Use fluent builders when you need a specific configuration not covered by standard `TestData` or need to test edge cases.

```csharp
// ✅ Good: Readable, customizable for specific test case
var card = new CardBuilder()
    .WithName("expensive_card")
    .WithCost(99)
    .Build();

// ✅ Also good: MatchContext is the single most-constructed object in the suite
// (7 dependencies + optional seed) - default everything to a mock, override only
// what this test actually needs.
var context = new MatchContextBuilder()
    .WithTurnManager(turnManager)
    .WithSeed(999)
    .Build();
```

**3. Avoid: Raw Constructors**
Avoid `new ClassName(...)` unless creating DTOs or simple value objects, or unless a builder would need to override MOST of the constructor's arguments anyway (at that point the builder buys nothing - see `MatchContextBuilder`'s own doc comment for real examples of when raw construction is still the pragmatic choice). Raw constructors are otherwise brittle and hard to read.

```csharp
// ❌ Bad: Hard to read, breaks if constructor signature changes
var card = new Card("id", "name", 5, CardAspect.Warlord, 1, 2, 0);
```

---

## Directory Structure & Test Listing

Both test projects mirror their corresponding source tree. Each test file generally corresponds to one production file; `Integration/` subtrees hold multi-component tests instead.

### ChaosWarlords.Tests/ (primary suite)

```text
ChaosWarlords.Tests/
├── ChaosWarlords.Tests.csproj
├── MSTestSettings.cs                    # [assembly: DoNotParallelize]
└── Source/
    ├── TestData.cs                          # Centralized test data factory (Cards/Players/MapNodes/Sites)
    ├── TestHelpers.cs                       # Fluent builders - see "Builders Reference" below
    ├── MatchContextBuilderTests.cs          # Tests for MatchContextBuilder itself
    │
    ├── Core/
    │   ├── Contexts/
    │   │   ├── MatchContextConstructorTests.cs
    │   │   ├── MatchContextHashingTests.cs      # GetStateHash() determinism
    │   │   └── TurnContextTests.cs
    │   ├── Data/
    │   │   ├── CardDtoTests.cs
    │   │   ├── Dtos/
    │   │   │   └── CommandDtoTests.cs
    │   │   ├── GameStateDtoTests.cs
    │   │   ├── MapDtoTests.cs
    │   │   ├── MapNodeDtoTests.cs
    │   │   ├── PlayerDtoTests.cs
    │   │   └── SnapshotSerializationTests.cs    # EffectStack + ActionSystem targeting-state serialization
    │   ├── Performance/
    │   │   └── PerformanceTests.cs              # All [TestCategory("Performance")] benchmarks live here
    │   └── Utilities/
    │       ├── BufferedAsyncLoggerTests.cs
    │       ├── CachedIntTextTests.cs
    │       ├── CardDatabaseIntegrationTests.cs
    │       ├── CardDatabaseTests.cs
    │       ├── DtoMapperTests.cs
    │       ├── LocalizationManagerTests.cs
    │       ├── MapGeometryTest.cs
    │       ├── MapLayoutEngineTests.cs
    │       ├── ObjectPoolTests.cs
    │       ├── PooledPrimitivesTests.cs
    │       └── StateHasherTests.cs
    │
    ├── Doubles/State/
    │   └── TestGameplayState.cs             # Hand-rolled IGameplayState fake (auto-wires its own MatchContext)
    │
    ├── Entities/
    │   ├── CardTests.cs
    │   ├── Cards/
    │   │   └── EffectConditionTests.cs
    │   ├── DeckTests.cs
    │   ├── MapNodeTests.cs
    │   ├── PlayerTests.cs
    │   ├── SiteTests.cs
    │   └── StartingSiteTests.cs
    │
    ├── Input/
    │   ├── Controllers/
    │   │   ├── PlayerControllerTests.cs
    │   │   └── ReplayControllerTests.cs
    │   └── MapHitTestExtensionsTests.cs
    │
    ├── Integration/                         # Multi-component tests, real implementations where it matters
    │   ├── Factories/
    │   │   ├── CardFactoryTests.cs
    │   │   ├── MapFactoryTests.cs
    │   │   └── MatchFactoryTests.cs
    │   ├── GameStates/
    │   │   ├── GameplayStateTests.cs
    │   │   ├── MainMenuStateTests.cs
    │   │   ├── StateManagerTests.cs
    │   │   └── VictoryStateTests.cs
    │   ├── Input/
    │   │   ├── Controllers/
    │   │   │   └── PlayerControllerTests.cs
    │   │   ├── InputBlockingTests.cs
    │   │   ├── Modes/
    │   │   │   ├── DevourInputModePreCommitFlowIntegrationTests.cs
    │   │   │   ├── DevourInputModeTests.cs
    │   │   │   ├── DiscardInputModeTests.cs
    │   │   │   ├── MarketInputModeTests.cs
    │   │   │   ├── NormalPlayInputModeTests.cs
    │   │   │   ├── PromoteInputModeTests.cs     # PromoteFromPileInputMode has no sibling test file yet - see note below
    │   │   │   └── TargetingInputModeTests.cs
    │   │   ├── Processors/
    │   │   │   ├── GameplayInputCoordinatorTests.cs
    │   │   │   └── InteractionMapperTests.cs
    │   │   └── Services/
    │   │       └── InputManagerTests.cs
    │   ├── Managers/
    │   │   ├── MapManagerTests.cs
    │   │   └── MatchManagerTests.cs
    │   └── Mechanics/
    │       ├── ActionSystemCancelTargetingSnapshotTests.cs  # CancelTargeting's snapshot/restore path specifically (MatchContext wired)
    │       ├── ActionSystemCancellationTests.cs             # CancelTargeting's fallback path (no MatchContext wired)
    │       ├── ActionSystemCompletionTests.cs
    │       ├── CloakerMechanicsTests.cs
    │       ├── ConditionalEffectTests.cs
    │       ├── CultistOfMyrkulMechanicsTests.cs
    │       ├── DevourFromInnerCircleIntegrationTests.cs
    │       ├── DevourIntegrationTests.cs
    │       ├── InsaneOutcastMechanicsTests.cs
    │       ├── MandatoryInnerCircleDevourIntegrationTests.cs
    │       ├── NeogiMechanicsTests.cs
    │       ├── ReturnUnitMechanicsTests.cs
    │       ├── SelfDevourIntegrationTests.cs
    │       ├── TransactionalCommandTests.cs
    │       ├── UlitharidMechanicsTests.cs
    │       └── WightMechanicsTests.cs
    │
    ├── Managers/
    │   ├── ActionInputControllerTests.cs     # Direct branch-level coverage of all 7 targeting-state click routes
    │   ├── CommandDispatcherTests.cs
    │   ├── MarketManagerTests.cs
    │   ├── MarketStateManagerTests.cs
    │   ├── PlayerStateManagerTests.cs
    │   ├── ReplayManagerTests.cs
    │   ├── StateRestorerRealCardIdentityTests.cs
    │   ├── StateRestorerTests.cs             # Rollback coverage incl. ActionSystem's own Pending*/CurrentState
    │   ├── TurnManagerTests.cs
    │   ├── UIEventMediatorTests.cs
    │   ├── UIManagerTests.cs
    │   └── VictoryManagerTests.cs
    │
    ├── Map/
    │   ├── CombatResolverTests.cs
    │   ├── MapRewardSystemTests.cs
    │   ├── MapTopologyTests.cs
    │   └── SpyOperationsTests.cs
    │
    ├── Mechanics/
    │   ├── Actions/
    │   │   ├── Subsystems/
    │   │   │   ├── DevourSubsystemTests.cs
    │   │   │   └── SpySubsystemTests.cs
    │   │   ├── ActionSystemDevourChainTests.cs
    │   │   ├── ActionSystemTests.cs
    │   │   ├── ActionSystemTransactionTests.cs
    │   │   ├── CardPlaySystemTests.cs
    │   │   ├── ObsoleteMethodRemovalTests.cs
    │   │   └── PreTargetHandlerTests.cs
    │   ├── Commands/                        # One file per IGameCommand, plus:
    │   │   ├── ActionCompletedCommandTests.cs
    │   │   ├── AssassinateCommandTests.cs
    │   │   ├── BuyCardCommandTests.cs
    │   │   ├── CancelActionCommandTests.cs
    │   │   ├── CommandSerializationTests.cs
    │   │   ├── DeployTroopCommandTests.cs
    │   │   ├── DevourCardCommandTests.cs     # incl. Validate() rejecting an unresolvable RuntimeId
    │   │   ├── DiscardCardCommandTests.cs
    │   │   ├── EndTurnCommandTests.cs
    │   │   ├── MoveTroopCommandTests.cs
    │   │   ├── NewCommandDtoRoundTripTests.cs
    │   │   ├── PlaceSpyCommandTests.cs
    │   │   ├── PlayCardCommandTests.cs
    │   │   ├── PlayFromMarketCommandTests.cs
    │   │   ├── PromoteCommandTests.cs
    │   │   ├── ResolveSpyCommandTests.cs
    │   │   ├── ReturnOwnSpyCommandTests.cs
    │   │   ├── ReturnTroopCommandTests.cs
    │   │   ├── SelectOpponentCommandTests.cs
    │   │   ├── StartAssassinateCommandTests.cs
    │   │   ├── StartReturnSpyCommandTests.cs
    │   │   ├── SupplantCommandTests.cs
    │   │   ├── SwitchToNormalModeCommandTests.cs
    │   │   └── ToggleMarketCommandTests.cs
    │   └── Rules/
    │       ├── CardEffectProcessorTests.cs
    │       ├── CardEffectTests.cs
    │       ├── CardRuleEngineLookaheadTests.cs
    │       ├── CardRuleEngineTests.cs
    │       ├── DevourStrategyFactoryTests.cs      # IDevourStrategy (target-location) implementations
    │       ├── MapRuleEngineTests.cs
    │       ├── SiteControlSystemTests.cs
    │       ├── Strategies/
    │       │   └── EffectStrategiesTests.cs      # Direct tests for 9 of the 12 registered IEffectStrategy implementations (Assassinate/Default/Devour/MoveUnit/PlaceSpy/ReturnUnit/Supplant/SelectOpponent/PromoteFromPile) - Discard/ReturnOwnSpy/PlayFromMarket aren't covered directly here
    │       └── TargetingStateEngineTests.cs
    │
    ├── Functional/
    │   ├── MatchScenario.cs                  # Scenario harness - see "Functional/Scenario Test Harness" below
    │   ├── AlwaysLegalCommandsScenarioTests.cs   # Spam/idempotency of always-legal no-target commands, not a per-card scenario
    │   ├── BansheeInfiltratorScenarioTests.cs
    │   ├── CarrionCrawlerScenarioTests.cs
    │   ├── CloakerScenarioTests.cs
    │   ├── CraniumRatsScenarioTests.cs
    │   ├── CultistOfMyrkulScenarioTests.cs
    │   ├── InsaneOutcastScenarioTests.cs
    │   ├── MarketCorruptorScenarioTests.cs
    │   ├── MatronMotherNecromancerScenarioTests.cs
    │   ├── NeogiScenarioTests.cs
    │   ├── SkeletalHordeScenarioTests.cs
    │   ├── TrivialPrimitiveCardsScenarioTests.cs # Batched matrix pass across 8 no-special-mechanic cards, not one file per card
    │   ├── UlitharidScenarioTests.cs
    │   └── WightScenarioTests.cs
    │
    ├── Rendering/
    │   ├── LogicVectorExtensionsTests.cs
    │   └── UI/
    │       └── PopupBuilderTests.cs
    │
    ├── Replay/
    │   ├── ReplayDesyncTests.cs             # RNG call-count parity between a live run and its replay
    │   ├── ReplayFidelityTests.cs           # Full scripted game through CommandDispatcher; live vs. replayed GetStateHash() match after EVERY command
    │   ├── ReplayScenarioTests.cs
    │   └── ReplaySystemTests.cs
    │
    └── Utilities/
        └── TestLogger.cs                     # Shared IGameLogger for tests (BufferedAsyncLogger-backed)
```

**Known coverage gap:** `PromoteFromPileInputMode.cs` (the client input mode for the
PromoteFromPile primitive) shipped without a dedicated `Integration/Input/Modes/
PromoteFromPileInputModeTests.cs` the way `DevourInputModeTests.cs` exists for its sibling
`DevourInputMode`. This is a real, currently-open gap, not an omission from this doc.

### ChaosWarlords.Core.Tests/ (headless-only suite)

```text
ChaosWarlords.Core.Tests/
├── ChaosWarlords.Core.Tests.csproj      # References ChaosWarlords.Core ONLY - never the client project
├── MSTestSettings.cs                    # [assembly: DoNotParallelize]
└── Source/
    ├── NullTestLogger.cs                    # Minimal IGameLogger - doesn't depend on ChaosWarlords.Tests.Utilities.TestLogger
    ├── Core/
    │   ├── Data/
    │   │   ├── LogicRectangleTests.cs
    │   │   └── LogicVector2Tests.cs
    │   └── Utilities/
    │       ├── Pcg32Tests.cs                # The RNG algorithm itself: determinism, non-constant output, bounded range, coarse distribution check
    │       └── SeededGameRandomTests.cs
    └── Integration/
        └── HeadlessCompositionSmokeTests.cs # Builds a real match via MatchFactory, runs real commands through a real CommandDispatcher - proves the whole composition root works with zero MonoGame in this project's dependency graph
```

---

## Builders Reference (`TestHelpers.cs`)

| Builder | For | Key methods |
|---|---|---|
| `CardBuilder` | `Card` | `WithName`, `WithDescription`, `WithCost`, `WithAspect`, `WithPower`/`WithInfluence`/`WithVP`, `WithEffect`, `WithFocusEffect`, `InHand`/`InDeck`/`InDiscard`/`InInnerCircle`/`InPlayed`, `Build()` |
| `PlayerBuilder` | `Player` | `WithColor`, `WithSeatIndex`, `WithName`, `WithPower`/`WithInfluence`/`WithVP`, `WithTroops`, `WithSpies`, `WithCardsInHand`/`InDeck`/`InDiscard`/`InInnerCircle`, `Build()` |
| `MapNodeBuilder` | `MapNode` | `At(x, y)`, `WithId`, `OccupiedBy`, `ConnectedTo`, `Build()` |
| `MatchContextBuilder` | `MatchContext` | `WithTurnManager`, `WithMapManager`, `WithMarketManager`, `WithActionSystem`, `WithCardDatabase`, `WithPlayerStateManager`, `WithLogger`, `WithSeed`, `Build()` - every dependency defaults to a fresh `Substitute.For<T>()`, overridable one at a time |

`MockInputProvider` and `InputTestHelpers` (also in `TestHelpers.cs`) provide raw MonoGame `MouseState`/`KeyboardState` simulation for input-pipeline tests - client-project-only concerns, so they live in `ChaosWarlords.Tests`, not `ChaosWarlords.Core.Tests`.

---

## Test Counts (as of 2026-09-02)

**Total: 1184 tests** across both projects, all passing (19 in `ChaosWarlords.Core.Tests`, 1165 in `ChaosWarlords.Tests`).

Run `dotnet test` for the combined total; see the `--filter` commands above to break it down. This number drifts as tests are added - treat it as "order of magnitude and how to check", not a value to keep manually in sync here (the per-category breakdown that used to live in this table was already stale by the time it was last checked, which is exactly why it's gone now rather than just re-counted).

---

## Functional/Scenario Test Harness (`MatchScenario`)

`ChaosWarlords.Tests/Source/Functional/MatchScenario.cs` is the standard pattern for testing a
card's mechanics **from this point forward** (established 2026-09-01, see planning.txt TIER 1).
It exists to close two specific, nameable gaps that let real shipped bugs slip through a fully
green suite before it existed:

1. **Tests bypassing the real entry point.** Calling `ActionSystem` methods or
   `command.Execute(context)` directly instead of going through
   `PlayCardCommand -> CommandDispatcher` skips `Validate()`, replay recording, and rollback-
   on-exception entirely - the exact gap that hid a real bug where Carrion Crawler's mandatory
   market-Devour never actually opened the market when played for real, only when a test
   called `ActionSystem.TryStartDevourMarket` directly.
2. **Tests hand-typing a card's `CardEffect` tree** in a C# helper (e.g. a `GetWightCard()`
   method) instead of loading it from the shipped `cards.json`. This matches what the test
   *author* intended to ship, not necessarily what's actually in the file - a later JSON edit
   can silently drift from the hand-typed copy with every test still green. This is exactly
   how Wight and Cultist of Myrkul's broken Choose-one mutual exclusivity slipped through.

`MatchScenario.Build()` loads the REAL `cards.json` into a REAL `CardDatabase` and wires a
REAL `MatchFactory`-built match through a REAL `CommandDispatcher` - the same composition
`ChaosWarlords.Core.Tests/Source/Integration/HeadlessCompositionSmokeTests.cs` already proves
works end to end, just with a real card database instead of an empty `Substitute`. Typical
usage:

```csharp
var scenario = MatchScenario.Build();
var red = scenario.AsActivePlayer(PlayerColor.Red);       // seat order is randomized per-seed
var wight = scenario.GiveCard(PlayerColor.Red, "wight");  // pulled from the REAL database

scenario.PlayCard(wight);                                 // dispatches PlayCardCommand for real
scenario.RespondToLatestInteraction(accept: true);        // answers the "choose one" popup

scenario.SelectDevourCard(someHandCard);                  // HandleDevourSelection -> dispatch
scenario.ClickTarget(targetNode, null);                   // HandleTargetClick -> dispatch

Assert.AreEqual(...);                                     // assert on scenario.Context / scenario.Player(color)
```

Or drive an illegal command by hand and assert it's rejected with no state change (`Dispatch`
is the low-level primitive `PlayCard`/`ClickTarget` build on):

```csharp
long before = scenario.Context.SequenceNumber;
scenario.Dispatch(new DiscardCardCommand(PlayerColor.Red, "card_that_does_not_exist"));
Assert.AreEqual(before, scenario.Context.SequenceNumber); // rejected commands must not advance it
```

See `WightScenarioTests.cs`, `CarrionCrawlerScenarioTests.cs`, `InsaneOutcastScenarioTests.cs`,
`CloakerScenarioTests.cs`, `UlitharidScenarioTests.cs`, `CultistOfMyrkulScenarioTests.cs`,
`NeogiScenarioTests.cs`, `CraniumRatsScenarioTests.cs`, `BansheeInfiltratorScenarioTests.cs`,
`MatronMotherNecromancerScenarioTests.cs`, `MarketCorruptorScenarioTests.cs`, and
`SkeletalHordeScenarioTests.cs` (same directory) for worked positive + adversarial examples. Note
`MatchScenario.Build()` always creates exactly 2 players (Red/Blue) - a scenario needing 3+
players (e.g. Neogi's seat-order/stacking behavior across more than one opponent) still needs
the older hand-typed-`TurnManager` pattern for that specific case; `NeogiScenarioTests.cs`
covers the 2-player real-path shape only, deliberately not duplicating
`NeogiMechanicsTests.cs`'s 3-player coverage.

**Adversarial/negative scenarios are a first-class use of this harness, not an afterthought.**
Per the testing policy every card/mechanic added from 2026-09-01 onward must satisfy (see
`planning.txt` section 2): both POSITIVE coverage (through the real `CommandDispatcher` path)
and NEGATIVE/adversarial coverage covering, at minimum, wrong-player dispatch, an out-of-range
or nonexistent target, insufficient resources, and a command re-dispatched against state where
it's already resolved (double-spend/replay). Once multiplayer exists, an untrusted client can
send any command directly - `Validate()` is the only real defense, so it needs to be exercised
as deliberately as the effect logic itself.

**Not a mandate to retrofit every existing test file.** The ~15 pre-existing
`*MechanicsTests.cs`/`*IntegrationTests.cs` files (hand-typed cards, direct `.Execute()`
calls) are NOT being deleted or rewritten in one pass - several of them pin ActionSystem-
internal behavior (exact stack shape, interaction-request counts) the harness doesn't
re-assert, and mass-migrating them is separate, lower-priority cleanup, not a blocker. New
card/mechanic work should use `MatchScenario`; touching an old file for unrelated reasons is a
fine time to consider migrating it, not an obligation.

---

## Test Patterns & Best Practices
For detailed code examples, naming conventions, and mocking patterns, please refer to:
**[Coding Guidelines: Testing Patterns](coding-guidelines.md#14-testing-patterns)**

This section in the guidelines covers:
*   **Naming Conventions** (`MethodName_Scenario_ExpectedBehavior`)
*   **Parameterized Tests** (Using `[DataRow]` correctly)
*   **Test Isolation** (Fresh instances per test)
*   **Mocking** (Using NSubstitute)
*   **Deterministic RNG** (Using `SeededGameRandom`)

## Test Data Reference

### TestData.Cards
All methods return **new instances** to prevent state pollution.

| Method | Description | Cost | Effects |
|--------|-------------|------|---------|
| `CheapCard()` | Low-cost neutral card | 2 | None |
| `ExpensiveCard()` | High-cost neutral card | 10 | None |
| `FreeCard()` | Zero-cost card | 0 | None |
| `AssassinCard()` | Shadow aspect | 3 | Assassinate(1) |
| `PowerCard()` | Warlord aspect | 2 | GainResource(Power, 3) |
| `InfluenceCard()` | Neutral aspect | 2 | GainResource(Influence, 2) |
| `DrawCard()` | Sorcery aspect | 1 | DrawCard(2) |
| `MoveUnitCard()` | Warlord aspect | 2 | MoveUnit(1) |
| `SupplantCard()` | Shadow aspect | 4 | Supplant(1) |

### TestData.Players
All methods return **new instances** with fresh state.

| Method | Description | Power | Influence | Troops | Spies |
|--------|-------------|-------|-----------|--------|-------|
| `RedPlayer()` | Standard red player | 10 | 10 | 10 | 5 |
| `BluePlayer()` | Standard blue player | 10 | 10 | 10 | 5 |
| `PoorPlayer()` | No resources | 0 | 0 | 0 | 0 |
| `RichPlayer()` | Abundant resources | 100 | 100 | 50 | 20 |

### TestData.MapNodes
All methods return **new instances** with unique IDs.

| Method | Description | ID | Position | Occupant |
|--------|-------------|----|---------|----|
| `Node1()` | Generic node | 1 | (10, 10) | None |
| `Node2()` | Generic node | 2 | (20, 10) | None |
| `Node3()` | Generic node | 3 | (30, 10) | None |
| `RedNode()` | Red-occupied | 10 | (100, 100) | Red |
| `BlueNode()` | Blue-occupied | 11 | (110, 100) | Blue |
| `EmptyNode()` | Unoccupied | 99 | (200, 200) | None |

---

### Integration Tests Details

**Input Blocking Tests (`InputBlockingTests.cs`)**
Verifies that blocking popups (Pause Menu, Confirmation, Optional Effects) strictly prevent gameplay interaction.
- Uses `Substitute.For<IInputManager>()` to fire simulated events.
- Asserts `DidNotReceive()` on command execution when popups are open.

**Mechanics Verification (`WightMechanicsTests.cs`)**
The gold standard for complex mechanics. Verifies "Deep Lookahead" logic:
- Sets up board state (Map, Nodes occupied/unoccupied).
- Plays a Card with multi-step mechanics (`Wight`).
- Asserts that Popups ONLY appear if the FULL chain is valid (Lookahead).

**Replay Fidelity (`ReplayFidelityTests.cs`)**
Plays a real multi-turn game through the actual `CommandDispatcher`, records it, replays the recording into a completely separate, independently-constructed `MatchContext` (same seed), and asserts `GetStateHash()` matches after *every* command - not just at the end, so a divergence points at exactly which command caused it.

**CancelTargeting Snapshot Path (`ActionSystemCancelTargetingSnapshotTests.cs`)**
`ActionSystemCancellationTests.cs` never wires a `MatchContext` into its `ActionSystem`, so its own tests only exercise `CancelTargeting`'s no-snapshot fallback. This file wires a real `MatchContext` (mirroring `StateRestorerTests.cs`'s setup) so the actual snapshot/restore path gets exercised too - including a case proving a resource spent mid-targeting correctly reverts, and a case proving a cancel mid-way through a multi-step chain reverts the *whole* sequence, not just the latest step.

---

## Advanced Mocking Patterns

### Mocking Input Events
To test event-driven input, use NSubstitute's `Raise.Event` to trigger the `OnInputEvent` handler.

```csharp
// Arrange
var mockInput = Substitute.For<IInputManager>();
var coordinator = new GameplayInputCoordinator(..., mockInput, ...);

// Act: Simulate User Click
mockInput.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(
    this,
    new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100))
);

// Assert
// Verify command was executed or state changed
```
