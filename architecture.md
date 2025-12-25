# ChaosWarlords Architecture & Organization

## Overview
This document outlines the reorganization of the `ChaosWarlords` codebase. The goal is to separate concerns, improve navigability, and adhere to "Clean Architecture" principles adapted for a MonoGame Engine context.

## Core Philosophy
We have moved away from a flat "Systems" directory that mixed *State Management*, *Input Handling*, *Game Logic*, and *Interfaces*, towards a structure that clearly delineates these responsibilities.

### The Solution: Functional Layers
We redistributed files into valid semantic groupings.

## Directory Structure

```text
Source/
├── Core/                   # Fundamental types, Contexts, and Utilities
│   ├── Interfaces/         # ALL Interfaces (IManager, ISystem, ICardDatabase, IInputMode)
│   ├── Contexts/           # Data contexts (MatchContext, TurnContext)
│   └── Utilities/          # Helper classes (CardDatabase)
├── Entities/               # Pure Domain Objects (Card, Player, Site)
├── Factories/              # Object Creation (TestWorldFactory, CardFactory, MapFactory)
├── GameStates/             # State Machine (GameplayState, StateManager)
├── Input/                  # Input Handling
│   ├── Services/           # providers (MonoGameInputProvider)
│   ├── Processors/         # Logic (InteractionMapper)
│   └── Modes/              # Input State Logic (TargetingInputMode, MarketInputMode)
├── Managers/               # State Holders (MapManager, TurnManager, MarketManager)
├── Mechanics/              # Game Logic, Rules, and Command Pattern
│   ├── Actions/            # ActionSystem
│   ├── Commands/           # GameCommands, DevourCardCommand
│   └── Rules/              # SiteControlSystem, MapRuleEngine, CardEffects
└── Rendering/              # Visuals and UI (was Views)
    ├── World/              # MapRenderer, CardRenderer
    └── UI/                 # UIRenderer, CardViewModel
```

## Detailed Remapping

| File | Role |
|------|------|
| `I*.cs` (All Interfaces) | **Centralized Contracts** in `Core/Interfaces/` |
| `CardFactory.cs`, `MapFactory.cs` | **Factories** in `Factories/` |
| `TestWorldFactory.cs` | **Procedural Gen** in `Factories/` |
| `InputManager.cs` | **Raw Input State** in `Input/Services/` |
| `*InputMode.cs` | **Input State Strategies** in `Input/Modes/` |
| `InteractionMapper.cs` | **Logic Mapping** in `Input/Processors/` |
| `MapManager.cs`, `TurnManager.cs` | **Persistent Services** in `Managers/` |
| `ActionSystem.cs` | **Action Validator** in `Mechanics/Actions/` |
| `SiteControlSystem.cs` | **Rule Logic** in `Mechanics/Rules/` |
| `GameCommands.cs` | **Command Implementations** in `Mechanics/Commands/` |
| `MapRenderer.cs`, `GameplayView.cs` | **Visuals** in `Rendering/` |

## Chaos Warlords Compliance Notes
To act as an expert on *Chaos Warlords*, we must ensure the `Mechanics` layer is robust.
- **SiteControlSystem**: Handles "Total Control" (2 VP/turn) vs "Control" (1 VP/turn or resource).
- **CardEffectProcessor**: Handles *Promote* (Inner Circle), *Deploy*, *Assassinate*, *Supplant*, and *Return* actions.
