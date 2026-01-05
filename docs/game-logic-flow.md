# Game Logic Flow Visualization

This document visualizes the data flow and component interactions within the ChaosWarlords engine.

## 1. High-Level Architecture

The engine strictly separates **Logic** (Game State) from **Presentation** (View).

```mermaid
graph TD
    subgraph "Logic Layer (Headless)"
        GS[GameplayState] -->|Update| TM[TurnManager]
        GS -->|Update| MM[MatchManager]
        GS -->|Execute| CD[CommandDispatcher]
    end

    subgraph "Input Layer"
        IM[InputManager] -->|Raw Input| PC[PlayerController]
        PC -->|Intent| GIC[InputCoordinator]
        GIC -->|Valid Command| CD
    end

    subgraph "Presentation Layer"
        GV[GameplayView] -.->|Read Only| GS
        GV -->|Subscribe| UIM[UIEventMediator]
        GS -->|Publish Events| UIM
    end

    classDef logic fill:#e1f5fe,stroke:#01579b,stroke-width:2px;
    classDef input fill:#fff3e0,stroke:#ff6f00,stroke-width:2px;
    classDef view fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px;

    class GS,TM,MM,CD logic;
    class IM,PC,GIC input;
    class GV,UIM view;
```

---

## 2. Input Processing Pipeline

How a user click becomes a game action (e.g., Playing a Card).

```mermaid
sequenceDiagram
    participant User
    participant InputManager as InputManager
    participant Controller as PlayerController
    participant Coordinator as InputCoordinator
    participant Mode as NormalInputMode
    participant System as CardPlaySystem
    participant Command as CommandDispatcher

    User->>InputManager: Click(Left, X, Y)
    InputManager->>Controller: GetInputState()
    Controller->>Coordinator: HandleInput(State)
    Coordinator->>Mode: HandleInput(State)
    
    Note over Mode: Hit Test: Screen -> Card
    
    Mode->>System: TryPlayCard(Player, Card)
    
    alt Resources Available
        System->>Command: Dispatch(PlayCardCommand)
    else Insufficient Power
        System-->>Coordinator: Result(Failed)
        Coordinator-->>User: Show Notification
    end
```

---

## 3. Transactional Action Flow

Visualizing the "Lookahead" and atomic execution of complex actions (e.g., Devour -> Gain Resource).

```mermaid
stateDiagram-v2
    [*] --> Idle
    
    Idle --> Targeting: StartAction(Devour)
    Targeting --> Validating: SelectTarget(Card)
    
    state Validating {
        [*] --> CheckConditions
        CheckConditions --> SimulateEffect: Valid
        SimulateEffect --> CheckResult: Apply Temporary State
        CheckResult --> Rollback: Invalid for Next Step
        CheckResult --> Success: Valid Chain
    }

    Validating --> Targeting: Invalid\n(User Notification)
    Validating --> Execution: Success
    
    Execution --> [*]: Dispatch CommandChain
```

---

## 4. Replay & Determinism System

How the game ensures every client sees the same result.

```mermaid
flowchart LR
    subgraph Recording
        CMD[Command Object] -->|Serialize| DTO[CommandDto]
        DTO -->|Add to List| REPLAY[Replay Context]
    end

    subgraph Playback
        REPLAY -->|Deserialize| CMD_NEW[Command Object]
        CMD_NEW -->|Execute| STATE[GameState]
        STATE -->|Update| RNG[Seeded RNG]
    end
    
    CMD -->|Execute| STATE
    
    style RNG fill:#f8bbd0,stroke:#880e4f,stroke-width:2px,stroke-dasharray: 5 5
```

---

## 5. Detailed Systems Interaction

### 5.1 Card Effect Resolution
This sequence details how a card's effects are processed after it is played. This involves validation, target acquisition, and integration with resource managers.

```mermaid
sequenceDiagram
    participant PlaySys as CardPlaySystem
    participant RuleEng as CardRuleEngine
    participant Processor as CardEffectProcessor
    participant PlayerState as PlayerStateManager
    participant MapMgr as MapManager
    participant Log as GameEventLogger

    PlaySys->>RuleEng: ValidatePlay(Card, Player)
    RuleEng-->>PlaySys: Valid
    PlaySys->>Processor: ProcessEffects(Card, Player)
    
    loop Each Effect
        processor->>Processor: Get Target
        
        alt Resource Effect (Gain Power)
            Processor->>PlayerState: AddPower(Amount)
            PlayerState->>Log: LogEvent(GainResource)
        else Map Effect (Assassinate)
            Processor->>MapMgr: GetMapContext()
            MapMgr-->>Processor: Context
            Processor->>MapMgr: ExecuteAssassinate(Target)
            MapMgr->>Log: LogEvent(UnitKilled)
        else Card Effect (Draw)
            Processor->>PlayerState: DrawCards(Amount)
        end
    end
    
    PlaySys->>PlayerState: MoveCardToDiscard(Card)
```

### 5.2 Troop Deployment Validation
The `MapRuleEngine` ensures all deployments follow graph connectivity and occupancy rules.

```mermaid
flowchart TD
    Start([TryDeploy Request]) --> HasTroops{Has Troops in Barracks?}
    HasTroops -- No --> Fail[Return False]
    HasTroops -- Yes --> IsOccupied{Node Occupied?}
    
    IsOccupied -- Yes --> Fail
    IsOccupied -- No --> CheckAdjacency{Is Adjacent to Controlled Site?}
    
    CheckAdjacency -- No --> Fail
    CheckAdjacency -- Yes --> CheckDistance{Distance <= MaxRange?}
    
    CheckDistance -- No --> Fail
    CheckDistance -- Yes --> Success[Return True]
    
    Success --> UpdateMap[MapManager.DeployTroop]
    UpdateMap --> UpdateSite[SiteControlSystem.UpdateControl]
    
    style Fail fill:#ffcdd2,stroke:#ba000d
    style Success fill:#c8e6c9,stroke:#1b5e20
```

### 5.3 Combat & Spy Operations (Assassination)
This flow shows the interaction between the `CombatResolver`, `SpyOperations`, and `MapManager` when resolving an assassination attempt.

```mermaid
sequenceDiagram
    participant Command as PerformAssassinateCommand
    participant MapMgr as MapManager
    participant Combat as CombatResolver
    participant SpyOps as SpyOperations
    participant SiteSys as SiteControlSystem

    Command->>MapMgr: Assassinate(Node, Attacker)
    MapMgr->>Combat: CanAssassinate(Node, Attacker)
    
    rect rgb(240, 248, 255)
        Note over Combat: Validation Logic
        Combat->>SpyOps: GetSpiesAtLocation(Node)
        SpyOps-->>Combat: SpyCount
        
        alt Has Spy || Adjacent Unit
            Combat-->>MapMgr: True
        else No Presence
            Combat-->>MapMgr: False
        end
    end

    MapMgr->>Combat: ExecuteAssassinate(Node)
    Combat->>MapMgr: RemoveUnit(Node)
    Combat->>SpyOps: RemoveSpy(Node, Attacker) -- If Spy used
    
    Note right of Combat: Unit Removed
    
    MapMgr->>SiteSys: UpdateControl(Node.Site)
    SiteSys->>SiteSys: Recalculate Ownership
```
