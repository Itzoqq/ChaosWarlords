# Contributing to ChaosWarlords

Thank you for your interest in contributing! This document provides guidelines and standards for contributing to the project.

## Quick Start for Contributors

1. **Fork** the repository
2. **Clone** your fork: `git clone https://github.com/YOUR_USERNAME/ChaosWarlords.git`
3. **Create a branch**: `git checkout -b feature/your-feature-name`
4. **Make changes** following our coding guidelines
5. **Run tests**: `dotnet test` (all must pass)
6. **Commit**: `git commit -m "feat: your feature description"`
7. **Push**: `git push origin feature/your-feature-name`
8. **Open a Pull Request**

## Development Setup

See [docs/setup.md](docs/setup.md) for detailed environment setup instructions.

### Prerequisites

- .NET 10.0 SDK
- IDE: Visual Studio 2022, VS Code, or Rider
- Git

### Build and Test

```bash
dotnet restore
dotnet build
dotnet test
```

## Coding Standards

**CRITICAL**: Read [docs/coding-guidelines.md](docs/coding-guidelines.md) before contributing. Key rules:

### 1. Deterministic RNG (MANDATORY)
```csharp
// ❌ NEVER do this
var random = new Random();

// ✅ Always do this
public void Method(IGameRandom random)
```

### 2. Use Interfaces
```csharp
// ✅ Correct
public class MyManager : IMyManager
{
    private readonly IPlayerStateManager _stateManager;
}
```

### 3. No Rendering in Logic
```csharp
// ❌ Wrong - logic depends on MonoGame
public class MapManager
{
    private SpriteBatch _spriteBatch;
}

// ✅ Correct - logic uses interfaces
public class MapManager : IMapManager
{
    public event Action<MapNode> NodeUpdated;
}
```

### 4. Centralized Resource Management
```csharp
// ❌ Wrong
player.Power += 5;

// ✅ Correct
_playerStateManager.AddPower(player, 5);
```

## Testing Requirements

### All PRs Must:
- Include tests for new features
- Maintain or improve code coverage
- Pass all existing tests (516 tests must pass)
- Follow AAA pattern (Arrange-Act-Assert)

### Test Categories
```bash
# Run unit tests (fast, required before commit)
dotnet test --filter "TestCategory=Unit"

# Run all tests (required before PR)
dotnet test
```

### Test Patterns
```csharp
[TestMethod]
[TestCategory("Unit")]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var player = TestData.Players.RedPlayer();
    
    // Act
    var result = player.CanAfford(5);
    
    // Assert
    Assert.IsTrue(result);
}
```

## Pull Request Process

### PR Checklist
- [ ] Code follows [coding guidelines](docs/coding-guidelines.md)
- [ ] All tests pass (`dotnet test`)
- [ ] New tests added for new features
- [ ] No `System.Random` usage (use `IGameRandom`)
- [ ] No MonoGame types in logic layer
- [ ] Documentation updated if needed
- [ ] Commit messages follow convention

### Commit Message Convention

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>: <description>

[optional body]
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `docs`: Documentation changes
- `perf`: Performance improvements
- `chore`: Maintenance tasks

**Examples**:
```
feat: add deterministic RNG system
fix: resolve multiplayer desync in card shuffle
refactor: extract coding guidelines to separate doc
test: add unit tests for SeededGameRandom
docs: update README with quick start guide
```

### Code Review

PRs will be reviewed for:
1. **Correctness**: Does it work as intended?
2. **Testing**: Are there adequate tests?
3. **Architecture**: Does it follow established patterns?
4. **Determinism**: No non-deterministic code paths
5. **Separation**: Logic doesn't depend on rendering

## What to Contribute

### Good First Issues
- Bug fixes
- Test coverage improvements
- Documentation improvements
- Performance optimizations

### Feature Development
- Discuss in an issue first
- Follow architecture patterns
- Include comprehensive tests
- Update documentation

### Areas Needing Help
- Victory system implementation
- Additional card effects
- UI/UX improvements
- Performance benchmarks
- Multiplayer networking (future)

## Questions?

- **Architecture**: See [docs/architecture.md](docs/architecture.md)
- **Testing**: See [docs/testing.md](docs/testing.md)
- **Guidelines**: See [docs/coding-guidelines.md](docs/coding-guidelines.md)
- **Issues**: [GitHub Issues](https://github.com/Itzoqq/ChaosWarlords/issues)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
