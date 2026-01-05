# Game Logic Flow Visualization

This document visualizes the data flow and component interactions within the ChaosWarlords engine, ordered from high-level concepts to detailed implementation flows.

## 1. High-Level Architecture
**Concept**: The 10,000ft view. How the Headless Logic interacts with the Input and View layers.

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

    classDef logic fill:#e1f5fe,stroke:#01579b,stroke-width:2px,color:black;
    classDef input fill:#fff3e0,stroke:#ff6f00,stroke-width:2px,color:black;
    classDef view fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:black;

    class GS,TM,MM,CD logic;
    class IM,PC,GIC input;
    class GV,UIM view;
```

> **Key Takeaway**: The **Logic Layer** runs the game physics/rules and can run without a screen (Headless). The **Presentation Layer** just listens and draws. The **Input Layer** converts your mouse clicks into "Intents" which are validated before becoming Commands.

---

## 2. Data Lifecycles
**Concept**: Before understanding *how* things happen, understand *what* is being manipulated (Cards & Victory Points).

### 2.1 Card Lifecycle
A card's journey from the Market through a player's hand and eventually to their scoring pile.

```mermaid
stateDiagram-v2
    classDef location fill:#f3e5f5,stroke:#4a148c,stroke-width:2px,color:black;
    classDef terminal fill:#eeeeee,stroke:#616161,stroke-width:2px,color:black,stroke-dasharray: 5 5;

    [*] --> AbstractDeck: Game Start
    
    state AbstractDeck {
        MarketDeck --> MarketRow: Refill
    }

    MarketRow --> DiscardPile: Buy (Spend Resources)
    MarketRow --> Void: Devour (From Market)
    
    state PlayerCollection {
        DiscardPile --> Deck: Reshuffle
        Deck --> Hand: Draw
        Hand --> PlayedArea: Play Card
        PlayedArea --> DiscardPile: End Turn
        
        PlayedArea --> InnerCircle: Promote (Spend Power)
        Hand --> Void: Devour (From Hand)
        PlayedArea --> Void: Devour (From Play)
    }

    InnerCircle --> [*]: End Game (Scoring)
    Void --> [*]: Removed from Game

    class MarketDeck,MarketRow,DiscardPile,Deck,Hand,PlayedArea,InnerCircle location;
    class Void terminal;
```

> **Key Takeaway**: Cards flow from the **Market** to your **Hand**. When played, they go to your **Played Area**. At the end of the turn, they are **Discarded**. You can only **Promote** cards from the Played Area to your Inner Circle (Score Pile). Devouring a card removes it from the game permanently (Void).

### 2.2 Victory & Scoring Flows
The ultimate goal of the game: how the winner is decided.

```mermaid
flowchart TD
    CheckEnd[End Turn Check] --> Condition1{Any Player\nTroops == 0?}
    CheckEnd --> Condition2{Market Deck\nEmpty?}
    
    Condition1 -- Yes --> TriggerEnd[Trigger Game End]
    Condition1 -- No --> Condition2
    Condition2 -- Yes --> TriggerEnd
    Condition2 -- No --> Continue[Next Turn]
    
    TriggerEnd --> CalcScore[Calculate Final Scores]
    
    subgraph Scoring Formulation
        CalcScore --> S1[VP Tokens]
        CalcScore --> S2[Site Control]
        CalcScore --> S3[Trophy Hall]
        S3 --> S3_1[1 VP per Trophy]
        
        CalcScore --> S4[Deck Value]
        S4 --> S4_1[Sum Card.DeckVP]
        
        CalcScore --> S5[Inner Circle]
        S5 --> S5_1[Sum Card.InnerCircleVP]
        
        S2 --> S2_Detail{Site Owner?}
        S2_Detail -- Yes --> SiteVP
        SiteVP --> TotalControl{Total Control?}
        TotalControl -- Yes --> Bonus[+2 Bonus VP]
    end
    
    S1 & S2 & S3 & S4 & S5 --> Total[Total Score]
    Total --> TieBreaker{Tie?}
    
    TieBreaker -- No --> Winner
    TieBreaker -- Yes --> TB1[Most Troops Deployed]
    TB1 --> Winner
    
    style TriggerEnd fill:#ffecb3,stroke:#ff6f00,color:black
    style Total fill:#c8e6c9,stroke:#1b5e20,color:black
```

> **Key Takeaway**: The game ends if the **Market Deck is empty** OR if **any player runs out of Troops**. Final score is a sum of direct VPs, Site Control (with bonuses), Trophies, Deck Value, and Inner Circle Value. **Ties** are broken by "Most Troops Deployed" (aggressive play wins ties).

---

## 3. Game State & Turn Lifecycle
**Concept**: The "Pulse" of the game. How the application moves between states and how a single turn is structured.

```mermaid
stateDiagram-v2
    classDef state fill:#e1f5fe,stroke:#01579b,stroke-width:2px,color:black;
    classDef substate fill:#fff3e0,stroke:#ff6f00,stroke-width:2px,color:black;

    [*] --> MainMenuState
    
    MainMenuState --> GameplayState: Start New Match
    
    state GameplayState {
        [*] --> SetupPhase: Initialize Match
        
        state TurnLoop {
            [*] --> StartTurn
            StartTurn --> InputPhase: Reset Resources
            
            state InputPhase {
                [*] --> Idle
                Idle --> Targeting: Select Action
                Targeting --> Execution: Confirm
                Execution --> Idle: Command Complete
            }
            
            InputPhase --> EndTurn: End Turn Command
            EndTurn --> StartTurn: Switch Active Player
        }
        
        SetupPhase --> TurnLoop: Deployment Complete
    }

    GameplayState --> VictoryState: Win Condition Met
    VictoryState --> MainMenuState: Return to Menu

    class MainMenuState,GameplayState,VictoryState state;
    class SetupPhase,TurnLoop,StartTurn,InputPhase,EndTurn substate;
```

> **Key Takeaway**: The game loops through **Turns**. In each turn, a player enters an **Input Phase** where they can perform multiple actions (Idle -> Target -> Execute) until they explicitly choose to **End Turn**. **Setup Phase** happens once at the start.

---

## 4. Input Processing Pipeline
**Concept**: How a user interacts with the running loop. From "Click" to "Command".

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

> **Key Takeaway**: Inputs are filtered three times: 
> 1. **InputManager**: "Did they click?" 
> 2. **Coordinator**: "Is this a valid UI state?"
> 3. **InputMode**: "Is the target valid?" 
> Only then is a **Command** dispatched.

---

## 5. Detailed Systems Interaction
**Concept**: The specific mechanics that run when a command is executed.

### 5.1 Card Effect Resolution
This sequence details how a card's effects are processed after it is played.

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
        Processor->>Processor: Get Target
        
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

> **Key Takeaway**: Playing a card triggers a chain of events. The **CardRuleEngine** first validates the play. Then, the **Processor** iterates through every effect on the card. Each effect (Resource, Map, Draw) is handled by a specific Manager.

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
    
    style Fail fill:#ffcdd2,stroke:#ba000d,color:black
    style Success fill:#c8e6c9,stroke:#1b5e20,color:black
```

> **Key Takeaway**: You can't just put troops anywhere. The **MapRuleEngine** enforces specific rules: You must have troops available, the node must be empty, it must be adjacent to your site, AND within range. All checks must pass.

### 5.3 Combat & Spy Operations (Assassination)
This flow shows the interaction between the Command, MapManager, and CombatResolver.

```mermaid
sequenceDiagram
    participant Command as PerformAssassinateCommand
    participant MapMgr as MapManager
    participant Combat as CombatResolver
    participant SpyOps as SpyOperations
    participant SiteSys as SiteControlSystem

    Command->>MapMgr: Assassinate(Node, Attacker)
    MapMgr->>Combat: CanAssassinate(Node, Attacker)
    
    rect rgb(30, 30, 30)
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

> **Key Takeaway**: Assassination is complex. It checks for Spies and adjacent units. If a Spy is present at the target location, it can enable the kill! **SpyOperations** handles this check essentially acting as a "buff" to your assassination attempt.

---

## 6. Transactional Action Flow
**Concept**: Advanced Topic. How complex, multi-step actions (like Devour) affect the game state attomically using "Lookahead".

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

> **Key Takeaway**: For multi-step actions (like Devour), we don't commit until the end. We stick the game in a **Validating** state, apply the changes completely in memory, see if the result is valid, and if so, commit. If not, we **Rollback** as if nothing happened.

---

## 7. Serialization & Replay System
**Concept**: Infrastructure. How the system ensures consistency across network/save-loads by converting "Live Objects" into "Data Transfer Objects" (DTOs).

### 7.1 DTO Mapping Strategy
The bridge between complex Runtime Entities and simple Serializable Data.

```mermaid
flowchart LR
    subgraph Live State
        E1[Card Entity]
        E2[Command Object]
        E3[Player Entity]
    end

    Mapper[DtoMapper]

    subgraph Serialized Data (DTOs)
        D1[CardDto]
        D2[CommandDto]
        D3[PlayerDto]
    end
    
    E1 & E2 & E3 -->|ToDto()| Mapper
    Mapper -->|Hydrate()| E1
    
    Mapper --> D1 & D2 & D3
    D1 & D2 & D3 -->|JSON| Storage[Disk / Network]
    
    style Mapper fill:#fff9c4,stroke:#fbc02d,stroke-width:2px,color:black
    style Storage fill:#eeeeee,stroke:#616161,stroke-width:2px,stroke-dasharray: 5 5,color:black
```

> **Key Takeaway**: **DtoMapper** is the translator. It takes complex game objects like `Player` (with logic methods) and turns them into dumb data `PlayerDto` (just numbers and strings) that can be saved to a file or sent over the internet.

### 7.2 Replay Loop
How the game ensures every client sees the same result by re-executing serialized commands.

```mermaid
flowchart LR
    subgraph Recording
        CMD[Command Object] -->|ToDto| DTO[CommandDto]
        DTO -->|Add to List| REPLAY[Replay Context]
    end

    subgraph Playback
        REPLAY -->|Deserialize| CMD_NEW[Command Object]
        CMD_NEW -->|Execute| STATE[GameState]
        STATE -->|Update| RNG[Seeded RNG]
    end
    
    CMD -->|Execute| STATE
    
    style RNG fill:#f8bbd0,stroke:#880e4f,stroke-width:2px,stroke-dasharray: 5 5,color:black
```

> **Key Takeaway**: To show a replay, we don't record video. We just record the **List of Commands** (as DTOs). To "play" it back, we just feed those commands into the game engine one by one. The **Seeded RNG** ensures that all "random" events happen in the exact same way every time.
