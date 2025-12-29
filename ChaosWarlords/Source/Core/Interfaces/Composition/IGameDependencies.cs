#nullable enable
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Core.Interfaces.Composition
{
    /// <summary>
    /// Encapsulates the core infrastructure dependencies required by GameplayState.
    /// This pattern (Parameter Object) simplifies the constructor signature and enables 
    /// easy mocking of the entire dependency graph for unit tests.
    /// </summary>
    public interface IGameDependencies
    {
        Game? Game { get; }
        IInputManager InputManager { get; }
        ICardDatabase CardDatabase { get; }
        IGameLogger Logger { get; }
        IUIManager UIManager { get; }
        IGameplayView? View { get; }
        
        // Configuration
        int ViewportWidth { get; }
        int ViewportHeight { get; }
    }
}
