# ChaosWarlords Architecture & Organization

## Overview
This document outlines the architecture of the `ChaosWarlords` codebase, which is a digital adaptation of the board game *Chaos Warlords*. The design follows a strict separation of concerns, utilizing an Event-Driven architecture and Dependency Injection via a centralized Context.

## System Architecture

### 1. Functional Layers
We avoid a flat "Systems" directory in favor of semantic categorization:

```text
Source/
├── Core/                   # The Foundation
│   ├── Contexts/           # Data Holders (MatchContext, TurnContext) - The "Glue"
│   ├── Interfaces/         # Contracts (IMapManager, IActionSystem)
│   └── Utilities/          # Helpers (CardDatabase, LayoutConsts, Enums)
├── Entities/               # Domain Models (Pure Data)
│   ├── Card.cs, Player.cs  # State Containers
│   └── Site.cs, MapNode.cs # Spatial Graph Nodes
├── Factories/              # Creation Logic
│   ├── MapFactory.cs       # Object Composition
│   └── CardFactory.cs      # Data Injection
├── GameStates/             # High-Level Flow (State Machine)
│   └── GameplayState.cs    # The "Main Loop" Orchestrator
├── Input/                  # Logic for User Commands
│   ├── Services/           # Raw MonoGame Input Wrappers
│   ├── Processors/         # InteractionMapper (Space -> Logic)
│   └── Modes/              # State Pattern for Input (Targeting, Market)
├── Managers/               # State & Lifecycle Managers (Services)
│   ├── MapManager.cs       # Facade for Map Logic
│   └── TurnManager.cs      # Phase & Player Rotation
├── Mechanics/              # Business Logic (The Rules)
│   ├── Actions/            # ActionSystem (Targeting State Machine)
│   ├── Commands/           # Legacy Command Pattern
│   └── Rules/              # Pure Logic Engines (SiteControl, CardEffects, MapRules)
└── Rendering/              # Presentation Layer
    ├── World/              # Draw Calls (MapRenderer)
    └── UI/                 # HUD & Interactive Elements
```

### 2. Core Dependencies (MatchContext)
The `MatchContext` is the heart of the dependency injection. It is created once per match and passed to all systems.
*   **Role**: Service Locator / Dependency Container.
*   **Contains**: `ITurnManager`, `IMapManager`, `IMarketManager`, `IActionSystem`.
*   **Lifetime**: Scope of a single Match.

## Detailed System Breakdown

### A. Map & Area Control System (`Source/Mechanics/Rules/`)
The game board is a graph of `Sites` (collections of nodes) and `Routes`.
*   **`MapRuleEngine.cs` (The Judge)**: Pure logic component. Determines if a move is legal.
    *   *Presence Check*: Handles the critical rule where Spies grant presence at their site, but Troops grant presence to adjacent nodes.
*   **`SiteControlSystem.cs` (The Accountant)**: Handles ownership rules.
    *   *Control*: You have more troops than anyone else.
    *   *Total Control*: You "Control" the site AND no enemy presence exists anywhere on it.
    *   *Rewards*: Calculates immediate (Influence/VP) and turn-start income.

### B. Action & Card System (`Source/Mechanics/Actions/`)
*   **`ActionSystem.cs` (The Hand)**: A State Machine that handles the "Click-to-Target" flow.
    *   States: `Normal`, `TargetingAssassinate`, `TargetingPlaceSpy`, `TargetingSupplant`.
    *   **Validation Check**: Before entering a targeting state, it queries `MapManager` to ensure valid targets exist, preventing dead-ends for the user.
*   **`CardEffectProcessor.cs` (The Brain)**: Executes card text.
    *   Resolves: `Assassinate`, `Deploy`, `Promote`, `Devour`.
    *   Connects card data (Effects) to Game Systems (`ActionSystem`).

### C. Deck Building (`Source/Entities/Player.cs`)
Accurately models the *Chaos* deck zones:
*   **Deck**: Draw pile.
*   **Hand**: Current turn options.
*   **Played**: Active area.
*   **Discard**: Recycled when Deck is empty.
*   **Inner Circle**: Promoted cards (high VP, removed from cycling).
*   **Void**: Devoured cards (Removed from game entirely).

### D. Input System (`Source/Input/`)
Uses the State Pattern to change how clicks are interpreted.
*   **MarketMode**: Clicks buy cards.
*   **TargetingMode**: Clicks select map nodes.
*   **InteractionMapper**: Converts screen pixels -> World Coordinates -> `MapNode`.

## Planned Future Systems (Analysis)

### 1. Victory Conditions
*   **Current State**: Infinite Loop.
*   **Requirement**: Implement `VictoryManager`.
    *   Trigger 1: `EmptyBarracks` (Last troop deployed).
    *   Trigger 2: `EmptyMarket` (Market deck depleted).
    *   Scoring: Sum VP Tokens + Trophy Hall + Deck Value + Inner Circle Value + Site Control VP.

### 2. Start Phase
*   **Current State**: Puts players directly into Turn 1 with 0 board presence.
*   **Requirement**: Implement `SetupPhase`.
    *   Players take turns placing initial troops on "Starting Sites" (Neutral/Black start zones).

### 3. Event Bus
The architecture is moving towards C# Events for decoupling UI from Logic.
*   `ActionSystem.OnActionFailed` -> UI Message.
*   `TurnManager.OnTurnChanged` -> UI Turn Banner.

---
*Last Updated: 2025-12-26*
