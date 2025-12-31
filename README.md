# ChaosWarlords

A digital adaptation of the board game *Tyrants of the Underdark*, built with MonoGame and C#. Features a clean architecture designed for testability, multiplayer support, and deterministic gameplay.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Tests](https://img.shields.io/badge/tests-516%20passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

- **Deterministic Gameplay**: Seeded RNG ensures reproducible games for multiplayer and replay
- **Test-Driven Development**: 516 unit, integration, and performance tests
- **Multiplayer-Ready Architecture**: Logic separated from rendering for headless server support
- **Command Pattern**: All actions are replayable and undoable
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

```
ChaosWarlords/
├── ChaosWarlords/           # Main game project
│   ├── Source/
│   │   ├── Core/            # Core systems (DI, events, interfaces)
│   │   ├── Entities/        # Domain models (Player, Card, MapNode)
│   │   ├── Managers/        # Business logic services
│   │   ├── Mechanics/       # Game rules and commands
│   │   ├── Rendering/       # MonoGame rendering layer
│   │   └── Factories/       # Object creation and DI wiring
│   └── Content/             # Game assets (sprites, fonts)
├── ChaosWarlords.Tests/     # Test suite (516 tests)
└── docs/                    # Documentation
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

```bash
# Run all tests
dotnet test

# Run only unit tests (fast)
dotnet test --filter "TestCategory=Unit"

# Run with coverage
dotnet test /p:CollectCoverage=true
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
