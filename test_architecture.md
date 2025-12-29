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
├── Core/
│   ├── Contexts/
│   │   └── TurnContextTests.cs          [Unit] Tests TurnContext action history tracking
│   ├── Data/
│   │   ├── CardDtoTests.cs              [Unit] Tests CardDto validation and hydration
│   │   ├── GameStateDtoTests.cs         [Unit] Tests full game state DTO composition
│   │   ├── MapDtoTests.cs               [Unit] Tests map DTO collection handling
│   │   ├── MapNodeDtoTests.cs           [Unit] Tests node DTO properties and defaults
│   │   └── PlayerDtoTests.cs            [Unit] Tests PlayerDto serialization
│   ├── Events/
│   │   ├── EventManagerTests.cs         [Integration] Tests event publishing and subscriptions
│   │   └── StateChangeEventTests.cs     [Unit] Tests state change record creation and strings
│   ├── Logic/
│   │   └── CommandValidatorTests.cs     [Unit] Tests command validation logic
│   ├── Performance/
│   │   └── PerformanceTests.cs          [Performance] Benchmarks for critical systems:
│   │                                      - Deck shuffling (1000 cards < 50ms)
│   │                                      - Card drawing (100 draws < 10ms)
│   │                                      - Resource updates (1000 updates < 250ms)
│   │                                      - Effect resolution (100 cards < 30ms)
│   │                                      - Map neighbor lookup (1000 lookups < 10ms)
│   │                                      - Random generation (10000 calls < 20ms)
│   │                                      - Hand manipulation (1000 ops < 15ms)
│   └── Utilities/
│       ├── CachedIntTextTests.cs        [Unit] Tests text caching for performance
│       ├── CardDatabaseTests.cs         [Unit] Tests card data loading and queries
│       ├── MapLayoutEngineTests.cs      [Unit] Tests procedural map generation
│       └── SeededGameRandomTests.cs     [Unit] Tests deterministic RNG
│
├── Entities/
│   ├── CardTests.cs                     [Unit] Tests Card entity (properties, effects)
│   ├── DeckTests.cs                     [Unit] Tests Deck operations (shuffle, draw, discard)
│   ├── MapNodeTests.cs                  [Unit] Tests MapNode (neighbors, occupancy)
│   ├── PlayerTests.cs                   [Unit] Tests Player entity (resources, hand, deck)
│   ├── SiteTests.cs                     [Unit] Tests Site mechanics (spies, control)
│   └── StartingSiteTests.cs             [Unit] Tests starting site special rules
│
├── Factories/
│   ├── CardFactoryTests.cs              [Integration] Tests card creation from data
│   ├── MapFactoryTests.cs               [Integration] Tests map generation with all components
│   └── MatchFactoryTests.cs             [Integration] Tests full match setup and DI
│
├── GameStates/
│   ├── GameplayStateTests.cs            [Integration] Tests main game loop coordination
│   ├── MainMenuStateTests.cs            [Integration] Tests menu state and navigation
│   └── StateManagerTests.cs             [Integration] Tests state stack management
│
├── Input/
│   ├── Controllers/
│   │   └── PlayerControllerTests.cs    [Integration] Tests input handling and delegation
│   ├── Modes/
│   │   ├── DevourInputModeTests.cs     [Integration] Tests devour card input mode
│   │   ├── MarketInputModeTests.cs     [Integration] Tests market interaction mode
│   │   ├── NormalPlayInputModeTests.cs [Integration] Tests standard play mode
│   │   ├── PromoteInputModeTests.cs    [Integration] Tests card promotion mode
│   │   └── TargetingInputModeTests.cs  [Integration] Tests targeting mode for effects
│   ├── Processors/
│   │   ├── GameplayInputCoordinatorTests.cs [Integration] Tests input flow coordination
│   │   └── InteractionMapperTests.cs   [Integration] Tests screen-to-entity mapping
│   └── Services/
│       └── InputManagerTests.cs        [Integration] Tests input state management
│
├── Managers/
│   ├── GameEventLoggerTests.cs          [Unit] Tests logging of game events and subscriptions
│   ├── MapManagerTests.cs               [Integration] Tests map operations (deploy, spy, combat)
│   │                                      - Deployment validation and execution
│   │                                      - Spy placement and removal
│   │                                      - Combat resolution
│   │                                      - Site control updates
│   ├── MarketManagerTests.cs            [Unit] Tests market economy (buy, refresh, pricing)
│   ├── MatchManagerTests.cs             [Integration] Tests match lifecycle and win conditions
│   ├── PlayerStateManagerTests.cs       [Unit] Tests centralized player mutations
│   │                                      - Resource management (Power, Influence, VP)
│   │                                      - Troop/spy allocation
│   │                                      - State validation
│   ├── ReplayManagerTests.cs            [Unit] Tests replay recording and playback
│   ├── TurnManagerTests.cs              [Unit] Tests turn rotation and phase management
│   ├── UIEventMediatorTests.cs          [Unit] Tests event mediation between systems
│   └── UIManagerTests.cs                [Unit] Tests UI state management
│
├── Map/
│   ├── CombatResolverTests.cs           [Unit] Tests combat logic (assassinate, supplant)
│   ├── MapRewardSystemTests.cs          [Unit] Tests reward distribution for map control
│   ├── MapTopologyTests.cs              [Unit] Tests distance calculation and pathfinding
│   └── SpyOperationsTests.cs            [Unit] Tests spy placement and removal logic
│
├── Mechanics/
│   ├── Actions/
│   │   ├── ActionSystemTests.cs         [Unit] Tests action validation and execution
│   │   │                                  - Power requirements
│   │   │                                  - Action state management
│   │   │                                  - Cancellation logic
│   │   └── CardPlaySystemTests.cs       [Unit] Tests card play mechanics
│   │                                      - Play validation
│   │                                      - Effect triggering
│   │                                      - Location updates
│   ├── Commands/
│   │   ├── ActionCompletedCommandTests.cs [Unit] Tests action completion handling
│   │   ├── BuyCardCommandTests.cs       [Unit] Tests market purchase logic
│   │   ├── CancelActionCommandTests.cs  [Unit] Tests action cancellation
│   │   ├── DeployTroopCommandTests.cs   [Unit] Tests troop deployment command
│   │   ├── DevourCardCommandTests.cs    [Unit] Tests card devour mechanic
│   │   ├── EndTurnCommandTests.cs       [Unit] Tests turn end processing
│   │   ├── PlayCardCommandTests.cs      [Unit] Tests card play command
│   │   ├── ResolveSpyCommandTests.cs    [Unit] Tests spy resolution
│   │   ├── StartAssassinateCommandTests.cs [Unit] Tests assassinate initiation
│   │   ├── StartReturnSpyCommandTests.cs [Unit] Tests spy return command
│   │   ├── SwitchToNormalModeCommandTests.cs [Unit] Tests mode switching
│   │   └── ToggleMarketCommandTests.cs  [Unit] Tests market toggle
│   └── Rules/
│       ├── CardEffectProcessorTests.cs  [Unit] Tests effect processing logic
│       │                                  - Resource gain effects
│       │                                  - Targeting effects (MoveUnit, Assassinate)
│       │                                  - Card draw effects
│       │                                  - Devour effects
│       ├── CardEffectTests.cs           [Unit] Tests card effect definitions
│       ├── MapRuleEngineTests.cs        [Unit] Tests map rule enforcement
│       └── SiteControlSystemTests.cs    [Unit] Tests site control calculations
│
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

### Unit Tests (249 tests)
**Purpose**: Test single classes in isolation  
**Characteristics**: Fast, no external dependencies, use mocks  
**Run Time**: ~0.8 seconds

**Categories**:
- **Entities** (6 files): Domain models (Card, Deck, Player, MapNode, Site)
- **Mechanics** (18 files): Game rules, commands, actions, effects
- **Managers** (6 files): State managers (PlayerState, Market, Turn, UI, Replay)
- **Core/Utilities** (7 files): Infrastructure (TurnContext, Dto, Random, Database)
- **Map Components** (4 files): Map subsystems (Combat, Rewards, Topology, Spies)

### Integration Tests (142 tests)
**Purpose**: Test component interactions  
**Characteristics**: Slower, use real implementations, test coordination  
**Run Time**: ~0.9 seconds

**Categories**:
- **Managers** (3 files): Complex managers (Map, Match, EventManager)
- **Factories** (3 files): Object creation with dependencies (Card, Map, Match)
- **Game States** (3 files): State machine and coordination (Gameplay, Menu, StateManager)
- **Input** (9 files): Input handling pipeline (Controllers, Modes, Processors)

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

### 1. Naming Convention
```csharp
[TestMethod]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Example: AddPower_WithPositiveAmount_IncreasesPlayerPower
}
```

### 2. Parameterized Tests
Use `[DataRow]` for testing multiple scenarios:

```csharp
[TestMethod]
[DataRow(0, 1, false)]  // No power - should fail
[DataRow(1, 0, false)]  // No troops - should fail
[DataRow(1, 1, true)]   // Valid - should succeed
public void TryDeploy_ValidatesRequirements(int power, int troops, bool shouldSucceed)
{
    // Test implementation
}
```

### 3. Test Isolation
**Problem**: Parameterized tests share state between DataRows  
**Solution**: Create fresh instances for each test execution

```csharp
[TestMethod]
[DataRow(10, 5)]
[DataRow(20, 10)]
public void TestMethod(int value1, int value2)
{
    // ✅ Create fresh instances for each DataRow
    var player = new PlayerBuilder().WithPower(value1).Build();
    var manager = new PlayerStateManager();
    
    // Test logic...
}
```

### 4. Using TestData
**When to use TestData**:
- Common scenarios repeated across tests
- Standard player/card/node configurations
- Performance tests needing consistent data

**When to use Builders**:
- Test-specific configurations
- Edge cases or unusual states
- When you need fine-grained control

```csharp
// ✅ Good: Use TestData for common scenarios
var player = TestData.Players.RedPlayer();

// ✅ Good: Use Builder for specific configurations
var player = new PlayerBuilder()
    .WithPower(0)  // Edge case: no power
    .WithTroops(100)  // Edge case: many troops
    .Build();
```

### 5. Mocking Dependencies
Use `NSubstitute` for interface mocking:

```csharp
[TestMethod]
public void PlayCard_CallsEffectProcessor()
{
    // Arrange
    var mockProcessor = Substitute.For<ICardEffectProcessor>();
    var system = new CardPlaySystem(mockProcessor);
    var card = TestData.Cards.PowerCard();
    
    // Act
    system.PlayCard(card);
    
    // Assert
    mockProcessor.Received(1).ProcessEffects(card);
}
```

---

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

## Running Tests

### By Category
```bash
# Fast unit tests only
dotnet test --filter "TestCategory=Unit"

# Integration tests only
dotnet test --filter "TestCategory=Integration"

# Performance benchmarks only
dotnet test --filter "TestCategory=Performance"

# Exclude performance tests (for CI)
dotnet test --filter "TestCategory!=Performance"
```

### By Name Pattern
```bash
# Run all Player-related tests
dotnet test --filter "FullyQualifiedName~Player"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CardEffectProcessorTests"

# Run specific test method
dotnet test --filter "Name=AddPower_WithPositiveAmount_IncreasesPlayerPower"
```

---

## Test Metrics

**Total Tests**: 446
**Unit Tests**: 297 (est)
**Integration Tests**: 142 (est)
**Performance Tests**: 7

**Execution Time**:
- Unit: ~0.8s
- Integration: ~0.9s
- Performance: ~0.7s
- **Total**: ~2.6s

---

## Future Improvements

1. **Increase TestData Usage**: Refactor existing tests to use TestData for common scenarios
2. **Add More Performance Tests**: Benchmark AI decision-making, pathfinding algorithms
3. **Integration Test Scenarios**: Add end-to-end game flow tests
4. **Mutation Testing**: Use Stryker.NET to verify test effectiveness
5. **Snapshot Testing**: For complex object comparisons (DTOs, game state)
