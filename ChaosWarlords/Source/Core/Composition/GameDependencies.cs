#nullable enable
using ChaosWarlords.Source.Core.Interfaces.Composition;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Core.Composition
{
    /// <summary>
    /// A simple data container for game dependencies.
    /// Used to pass infrastructure from the Composition Root (Game1) to GameplayState.
    /// </summary>
    public class GameDependencies : IGameDependencies
    {
        public Game? Game { get; init; }
        public required IInputManager InputManager { get; init; }
        public required ICardDatabase CardDatabase { get; init; }
        public required IGameLogger Logger { get; init; }
        public required IUIManager UIManager { get; init; }
        public IGameplayView? View { get; init; }
        public required IReplayManager ReplayManager { get; init; }
        
        public int ViewportWidth { get; init; } = 1920;
        public int ViewportHeight { get; init; } = 1080;
    }
}
