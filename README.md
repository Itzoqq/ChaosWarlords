# ChaosWarlords

A digital adaptation of the board game *Tyrants of the Underdark*, built with MonoGame and C#. Features a clean architecture designed for testability, multiplayer support, and deterministic gameplay.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Tests](https://img.shields.io/badge/tests-863%20passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

- **Deterministic Gameplay**: A from-scratch PCG32 RNG (no `System.Random`) ensures reproducible games for multiplayer and replay, independent of .NET version
- **Test-Driven Development**: 863 unit, integration, and performance tests across two test projects
- **Multiplayer-Ready Architecture**: Logic lives in a separate `ChaosWarlords.Core` project with zero MonoGame references - a compiled headless boundary, not just a convention - and `ChaosWarlords.Core.Tests` proves the test suite is headless-runnable too
- **Command Pattern**: All actions are replayable and undoable, with transactional rollback (snapshot/restore) on both command failure and targeting cancellation
- **Event-Driven**: Decoupled systems communicate via events

## Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Windows, macOS, or Linux
- IDE: Visual Studio 2022, VS Code, or Rider

### Build and Run

```bash
# Clone the repository
git clone https://github.com/Itzoqq/ChaosWarlords.git
cd ChaosWarlords

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the game
dotnet run --project ChaosWarlords

# Run tests
dotnet test
```

## Technology Stack

- **Framework**: .NET 10.0
- **Game Engine**: MonoGame 3.8
- **Testing**: MSTest + NSubstitute
- **Architecture**: Dependency Injection, Command Pattern, Event-Driven

## Project Structure

Four projects - see [Architecture Guide](docs/architecture.md#the-four-projects) for the full breakdown of why:

```
ChaosWarlords/
├── ChaosWarlords.Core/          # Headless game logic - ZERO MonoGame package references
│   └── Source/                  # Entities, Managers, Mechanics, Factories, DTOs, Interfaces
├── ChaosWarlords/                # Main game project (MonoGame client)
│   ├── Source/
│   │   ├── Core/                # Client-only composition, input/rendering/state interfaces
│   │   ├── GameStates/           # Application state machine
│   │   ├── Input/                # Input pipeline (Manager -> Coordinator -> InputMode)
│   │   ├── Managers/             # UI-adjacent services (UIEventMediator, UIManager)
│   │   └── Rendering/            # MonoGame rendering layer
│   └── Content/                  # Game assets (sprites, fonts)
├── ChaosWarlords.Tests/          # Primary test suite (844 tests) - references the client project
├── ChaosWarlords.Core.Tests/     # Headless-only test suite (19 tests) - references Core ONLY
└── docs/                         # Documentation
```

## Documentation

- **[Architecture Guide](docs/architecture.md)** - System design and component breakdown
- **[Coding Guidelines](docs/coding-guidelines.md)** - Established patterns and best practices
- **[Testing Guide](docs/testing.md)** - Test organization and patterns
- **[Setup Guide](docs/setup.md)** - Development environment setup
- **[Contributing](CONTRIBUTING.md)** - How to contribute to the project

## Key Design Principles

### Deterministic RNG
All randomness uses seeded `IGameRandom` for multiplayer synchronization:
```csharp
var random = new SeededGameRandom(seed, logger);
deck.Shuffle(random);  // Same seed = same results
```

### Separation of Concerns
Game logic is completely independent of rendering:
```csharp
// Logic layer - no MonoGame dependencies
public class GameplayState
{
    private readonly IGameplayView _view;  // Interface, not concrete class
}
```

### Testability
All components use dependency injection and interfaces:
```csharp
var mockManager = Substitute.For<IMapManager>();
var state = new GameplayState(mockManager);  // Easy to test
```

## Running Tests
 
 ### 1. Basic Execution
 ```bash
 # Run all tests
 dotnet test
 
 # Run with coverage
 dotnet test /p:CollectCoverage=true
 ```
 
 ### 2. Filter by Category
 ```bash
 # Fast unit tests only
 dotnet test --filter "TestCategory=Unit"
 
 # Integration tests only
 dotnet test --filter "TestCategory=Integration"
 
 # Performance benchmarks only
 dotnet test --filter "TestCategory=Performance"
 ```
 
 ### 3. Filter by Name name
 ```bash
 # Run all Player-related tests
 dotnet test --filter "FullyQualifiedName~Player"
 
 # Run specific test method
 dotnet test --filter "Name=AddPower_WithPositiveAmount"
 ```

 ### 4. Headless-only subset
 ```bash
 # Runs only ChaosWarlords.Core.Tests - proves the logic layer's own tests
 # build and run with zero MonoGame in the dependency graph
 dotnet test ChaosWarlords.Core.Tests/ChaosWarlords.Core.Tests.csproj
 ```

## Contributing

We welcome contributions! Please read our [Contributing Guide](CONTRIBUTING.md) for:
- Code style and standards
- Pull request process
- Testing requirements
- Development workflow

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Based on *Tyrants of the Underdark* by Wizards of the Coast
- Built with [MonoGame](https://www.monogame.net/)
- Testing with [NSubstitute](https://nsubstitute.github.io/)

## Contact

- **GitHub**: [@Itzoqq](https://github.com/Itzoqq)
- **Issues**: [GitHub Issues](https://github.com/Itzoqq/ChaosWarlords/issues)
