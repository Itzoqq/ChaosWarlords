# ChaosWarlords Test Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords.Tests` test suite. The test design follows **AAA Pattern** (Arrange-Act-Assert), uses **Test Data Builders** for readability, and implements **Test Categories** for efficient filtering. The suite ensures code quality, prevents regressions, and validates both unit-level logic and integration between components.

## Test Organization Principles

### 1. Test Categories
Tests are categorized by scope and dependencies:

- **Unit Tests** (`[TestCategory("Unit")]`): Test single classes in isolation with mocked dependencies. Fast execution, no external dependencies.
- **Integration Tests** (`[TestCategory("Integration")]`): Test multiple components working together. May use real implementations.
- **Performance Tests** (`[TestCategory("Performance")]`): Benchmark critical operations with time thresholds.

**Usage**:
```bash
dotnet test --filter "TestCategory=Unit"        # Run only unit tests (fast)
dotnet test --filter "TestCategory=Integration" # Run integration tests
dotnet test --filter "TestCategory=Performance" # Run performance benchmarks
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
```

**3. Avoid: Raw Constructors**
Avoid `new ClassName(...)` unless creating DTOs or simple value objects. Raw constructors are brittle and hard to read.

```csharp
// ❌ Bad: Hard to read, breaks if constructor signature changes
var card = new Card("id", "name", 5, CardAspect.Warlord, 1, 2, 0);
```

---

## Directory Structure & Test Listing

The test project mirrors the main project structure. Each test file corresponds to a production file.

```text
ChaosWarlords.Tests/Source/
├── Core/                        # Unit Tests for Core Infrastructure
│   ├── Data/                    # Unit Tests for Data Structures
│   │   └── LogicVector2Tests.cs
│   ├── Events/
│   │   └── EventManagerTests.cs
│   └── Utilities/               # Unit Tests for Utilities
│       ├── BufferedAsyncLoggerTests.cs
│       ├── CachedIntTextTests.cs
│       ├── CardDatabaseIntegrationTests.cs
│       ├── CardDatabaseTests.cs
│       ├── DtoMapperTests.cs
│       ├── MapGeometryTest.cs
│       ├── MapLayoutEngineTests.cs
│       └── SeededGameRandomTests.cs
│
├── Doubles/                     # Test Doubles & Mocks
│   └── State/
│       └── TestGameplayState.cs
│
├── Entities/                    # Unit Tests for Domain Entities
│   ├── Cards/
│   │   └── EffectConditionTests.cs
│   ├── CardTests.cs
│   ├── DeckTests.cs
│   ├── MapNodeTests.cs
│   ├── PlayerTests.cs
│   ├── SiteTests.cs
│   └── StartingSiteTests.cs
│
├── Input/                       # Unit Tests for Input Logic
│   └── Controllers/
│       └── ReplayControllerTests.cs
│
├── Integration/                 # Integration Tests (Component Interaction)
│   ├── Core/
│   │   └── Events/
│   │       └── EventManagerTests.cs
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
│   │   ├── Modes/
│   │   │   ├── DevourFromInnerCircleIntegrationTests.cs
│   │   │   ├── DevourInputModeTests.cs
│   │   │   ├── MarketInputModeTests.cs
│   │   │   ├── NormalPlayInputModeTests.cs
│   │   │   ├── PromoteInputModeTests.cs
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
│       ├── ActionSystemCancellationTests.cs
│       ├── ConditionalEffectTests.cs
│       ├── DevourFromInnerCircleIntegrationTests.cs
│       ├── DevourMechanicsTests.cs
│       ├── MarketDevourChainTests.cs
│       ├── SelfDevourIntegrationTests.cs
│       └── TransactionalCommandTests.cs
│
├── Managers/                    # Unit Tests for Business Logic Managers
│   ├── CommandDispatcherTests.cs
│   ├── GameEventLoggerTests.cs
│   ├── MarketManagerTests.cs
│   ├── MarketStateManagerTests.cs
│   ├── PlayerStateManagerTests.cs
│   ├── ReplayManagerTests.cs
│   ├── TurnManagerTests.cs
│   ├── UIEventMediatorTests.cs
│   ├── UIManagerTests.cs
│   └── VictoryManagerTests.cs
│
├── Map/                         # Unit Tests for Map Subsystems
│   ├── CombatResolverTests.cs
│   ├── MapRewardSystemTests.cs
│   ├── MapTopologyTests.cs
│   └── SpyOperationsTests.cs
│
├── Mechanics/                   # Unit Tests for Game Mechanics
│   ├── Actions/
│   │   ├── Subsystems/
│   │   │   ├── DevourSubsystemTests.cs
│   │   │   └── SpySubsystemTests.cs
│   │   ├── ActionSystemTests.cs
│   │   ├── ActionSystemDevourChainTests.cs
│   │   ├── ActionSystemTransactionTests.cs
│   │   └── CardPlaySystemTests.cs
│   ├── Commands/
│   │   ├── ActionCompletedCommandTests.cs
│   │   ├── BuyCardCommandTests.cs
│   │   ├── CancelActionCommandTests.cs
│   │   ├── CommandSerializationTests.cs
│   │   ├── DeployTroopCommandTests.cs
│   │   ├── DevourCardCommandTests.cs
│   │   ├── EndTurnCommandTests.cs
│   │   ├── PlayCardCommandTests.cs
│   │   ├── ResolveSpyCommandTests.cs
│   │   ├── StartAssassinateCommandTests.cs
│   │   ├── StartReturnSpyCommandTests.cs
│   │   ├── SwitchToNormalModeCommandTests.cs
│   │   └── ToggleMarketCommandTests.cs
│   └── Rules/
│       ├── CardEffectProcessorTests.cs
│       ├── CardEffectTests.cs
│       ├── CardRuleEngineLookaheadTests.cs
│       ├── CardRuleEngineTests.cs
│       ├── MapRuleEngineTests.cs
│       ├── SiteControlSystemTests.cs
│       └── TargetingStateEngineTests.cs
│
├── Rendering/                   # Unit Tests for Rendering Components
│   └── UI/
│       └── PopupBuilderTests.cs
│
├── Replay/                      # Unit Tests for Replay System
│   └── ReplayManagerTests.cs
│
└── Utilities/                   # Unit Tests for Utilities
    └── TestLogger.cs
├── TestData.cs                          # Centralized test data factory
│   ├── TestData.Cards                   # Pre-configured card instances
│   │   ├── CheapCard()                  # Low-cost card (2 cost)
│   │   ├── ExpensiveCard()              # High-cost card (10 cost)
│   │   ├── FreeCard()                   # Zero-cost card
│   │   ├── AssassinCard()               # Card with Assassinate effect
│   │   ├── PowerCard()                  # Generates Power resource
│   │   ├── InfluenceCard()              # Generates Influence resource
│   │   ├── DrawCard()                   # Draws additional cards
│   │   ├── MoveUnitCard()               # Moves units on map
│   │   └── SupplantCard()               # Replaces enemy units
│   ├── TestData.Players                 # Pre-configured player instances
│   │   ├── RedPlayer()                  # Standard red player (10/10/10/5 resources)
│   │   ├── BluePlayer()                 # Standard blue player
│   │   ├── PoorPlayer()                 # Player with no resources
│   │   └── RichPlayer()                 # Player with abundant resources (100/100/50/20)
│   ├── TestData.MapNodes                # Pre-configured map nodes
│   │   ├── Node1(), Node2(), Node3()    # Generic nodes for testing
│   │   ├── RedNode()                    # Node occupied by red player
│   │   ├── BlueNode()                   # Node occupied by blue player
│   │   └── EmptyNode()                  # Unoccupied node
│   └── TestData.Sites                   # Pre-configured sites
│       ├── PowerCity()                  # City that generates Power
│       ├── InfluenceSite()              # Site that generates Influence
│       └── NeutralSite()                # Generic neutral site
│
└── TestHelpers.cs                       # Test utility functions and builders
    ├── CardBuilder                      # Fluent builder for Card instances
    │   ├── WithName(string)             # Sets card ID
    │   ├── WithDescription(string)      # Sets card name (display)
    │   ├── WithCost(int)                # Sets resource cost
    │   ├── WithAspect(CardAspect)       # Sets card aspect
    │   ├── WithEffect(...)              # Adds card effect
    │   ├── InHand()                     # Sets location to Hand
    │   ├── InDeck()                     # Sets location to Deck
    │   ├── InDiscard()                  # Sets location to DiscardPile
    │   ├── InInnerCircle()              # Sets location to InnerCircle
    │   └── Build()                      # Creates Card instance
    ├── PlayerBuilder                    # Fluent builder for Player instances
    │   ├── WithColor(PlayerColor)       # Sets player color
    │   ├── WithPower(int)               # Sets Power resource
    │   ├── WithInfluence(int)           # Sets Influence resource
    │   ├── WithVictoryPoints(int)       # Sets Victory Points
    │   ├── WithTroops(int)              # Sets troops in barracks
    │   ├── WithSpies(int)               # Sets spies in barracks
    │   └── Build()                      # Creates Player instance
    └── MapNodeBuilder                   # Fluent builder for MapNode instances
        ├── WithId(int)                  # Sets node ID
        ├── At(int x, int y)             # Sets node position
        ├── OccupiedBy(PlayerColor)      # Sets occupant
        └── Build()                      # Creates MapNode instance
```

---

## Test Categories Breakdown

**Total Test Suite: 680 tests** (401 Unit + 272 Integration + 7 Performance)

### Unit Tests (401 tests)
**Purpose**: Test single classes in isolation  
**Characteristics**: Fast, no external dependencies, use mocks  
**Run Time**: ~0.9 seconds

**Categories**:
- **Entities** (7 files): Domain models (Card, Deck, Player, EffectCondition, etc.)
- **Mechanics** (18 files): Game rules, commands, actions, effects
- **Managers** (9 files): State managers (PlayerState, Market, Turn, UI, Replay, Command Dispatcher)
- **Core/Utilities** (7 files): Infrastructure (TurnContext, Dto, Random, Database)
- **Map Components** (4 files): Map subsystems (Combat, Rewards, Topology, Spies)

### Integration Tests (272 tests)
**Purpose**: Test component interactions  
**Characteristics**: Slower, use real implementations, test coordination  
**Run Time**: ~4.7 seconds

**Organization**: All integration tests now in dedicated `Integration/` folder

**Categories**:
- **Mechanics** (2 files): Transactional commands & Conditional effects
- **Managers** (2 files): Complex managers (Map, Match)
- **Factories** (3 files): Object creation with dependencies (Card, Map, Match)
- **Game States** (4 files): State machine and coordination (Gameplay, Menu, Victory, StateManager)
- **Input** (9 files): Input handling pipeline (Controllers, Modes, Processors, Services)
- **Core/Events** (1 file): Event publishing and subscriptions

### Performance Tests (7 tests)
**Purpose**: Benchmark critical operations  
**Characteristics**: Time-based assertions, measure execution speed  
**Run Time**: ~0.7 seconds

**Benchmarks**:
- Deck operations (shuffle, draw)
- Resource management (1000 updates)
- Effect resolution (100 cards)
- Map queries (neighbor lookup)
- Random generation (10000 calls)
- Hand manipulation (1000 operations)

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


