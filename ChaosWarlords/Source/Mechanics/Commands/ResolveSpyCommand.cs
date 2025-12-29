using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Needed for the Spy Selection Popup
    /// </summary>
    public class ResolveSpyCommand : IGameCommand
    {
        public PlayerColor SpyColor { get; }
        public ResolveSpyCommand(PlayerColor spyColor) { SpyColor = spyColor; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("ResolveSpy", $"Selected {SpyColor} spy to return");
            // We just call the method. 
            // If it succeeds, ActionSystem fires OnActionCompleted.
            // If it fails, ActionSystem fires OnActionFailed.
            // The GameplayState listens to these events and handles the rest.
            state.ActionSystem.FinalizeSpyReturn(SpyColor);
        }
    }
}



