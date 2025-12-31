# Development Setup Guide

This guide will help you set up your development environment for contributing to ChaosWarlords.

## Prerequisites

### Required Software

1. **.NET 10.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation: `dotnet --version`

2. **Git**
   - Download: https://git-scm.com/downloads
   - Verify installation: `git --version`

3. **IDE** (choose one):
   - **Visual Studio 2022** (recommended for Windows)
     - Community Edition is free
     - Install ".NET desktop development" workload
   - **Visual Studio Code**
     - Install C# extension
     - Install .NET SDK
   - **JetBrains Rider**
     - Full-featured, paid IDE

## Initial Setup

### 1. Clone the Repository

```bash
# Fork the repository on GitHub first, then:
git clone https://github.com/YOUR_USERNAME/ChaosWarlords.git
cd ChaosWarlords
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

Expected output: `Build succeeded`

### 4. Run Tests

```bash
dotnet test
```

Expected output: `Test summary: total: 516, failed: 0`

### 5. Run the Game

```bash
dotnet run --project ChaosWarlords
```

## IDE-Specific Setup

### Visual Studio 2022

1. Open `ChaosWarlords.sln`
2. Set `ChaosWarlords` as startup project
3. Press F5 to run
4. Use Test Explorer (Ctrl+E, T) to run tests

### Visual Studio Code

1. Open the project folder
2. Install recommended extensions:
   - C# (ms-dotnettools.csharp)
   - C# Dev Kit
3. Press F5 to run
4. Use Test Explorer in sidebar to run tests

### JetBrains Rider

1. Open `ChaosWarlords.sln`
2. Rider will automatically restore packages
3. Press Shift+F10 to run
4. Use Unit Tests window (Ctrl+Alt+U) to run tests

## Recommended Extensions/Tools

### Visual Studio Code Extensions
- C# (Microsoft)
- C# Dev Kit
- GitLens
- Error Lens
- Better Comments

### Visual Studio Extensions
- ReSharper (optional, paid)
- CodeMaid (code cleanup)
- Productivity Power Tools

## Running Tests

### All Tests
```bash
dotnet test
```

### Unit Tests Only (Fast)
```bash
dotnet test --filter "TestCategory=Unit"
```

### Integration Tests
```bash
dotnet test --filter "TestCategory=Integration"
```

### Performance Tests
```bash
dotnet test --filter "TestCategory=Performance"
```

### Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~SeededGameRandom"
```

### With Code Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Common Issues

### Build Errors

**Issue**: `error CS0246: The type or namespace name 'MonoGame' could not be found`

**Solution**: Restore NuGet packages
```bash
dotnet restore
```

**Issue**: `error MSB3644: The reference assemblies for .NETFramework,Version=v10.0 were not found`

**Solution**: Install .NET 10.0 SDK

### Test Failures

**Issue**: Tests fail with `NullReferenceException`

**Solution**: Ensure all mocks are configured properly. Check that `IGameRandom` mocks use `Arg.Any<IGameRandom>()`.

**Issue**: Performance tests fail

**Solution**: Performance tests may fail on slower machines. These are benchmarks, not strict requirements during development.

### Runtime Errors

**Issue**: `FileNotFoundException: Could not load file or assembly 'MonoGame.Framework'`

**Solution**: Rebuild the project
```bash
dotnet clean
dotnet build
```

## Development Workflow

1. **Create a branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make changes**
   - Follow [coding guidelines](coding-guidelines.md)
   - Write tests for new features

3. **Run tests frequently**
   ```bash
   dotnet test --filter "TestCategory=Unit"
   ```

4. **Commit changes**
   ```bash
   git add .
   git commit -m "feat: your feature description"
   ```

5. **Push and create PR**
   ```bash
   git push origin feature/your-feature-name
   ```

## Project Structure

```
ChaosWarlords/
├── ChaosWarlords/              # Main game project
│   ├── Source/                 # C# source code
│   ├── Content/                # Game assets
│   └── ChaosWarlords.csproj    # Project file
├── ChaosWarlords.Tests/        # Test project
│   ├── Source/                 # Test source code
│   └── ChaosWarlords.Tests.csproj
├── docs/                       # Documentation
├── README.md                   # Project overview
└── CONTRIBUTING.md             # Contribution guide
```

## Next Steps

- Read [Architecture Guide](architecture.md) to understand the codebase
- Review [Coding Guidelines](coding-guidelines.md) for established patterns
- Check [Testing Guide](testing.md) for test patterns
- See [CONTRIBUTING.md](../CONTRIBUTING.md) for PR process

## Getting Help

- **Documentation**: Check `/docs` directory
- **Issues**: [GitHub Issues](https://github.com/Itzoqq/ChaosWarlords/issues)
- **Questions**: Open a discussion on GitHub
