using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Needed for the Spy Selection Popup
    /// </summary>
    public class ResolveSpyCommand : IGameCommand
    {
        private readonly PlayerColor _spyColor;
        public ResolveSpyCommand(PlayerColor spyColor) { _spyColor = spyColor; }

        public void Execute(IGameplayState state)
        {
            // We just call the method. 
            // If it succeeds, ActionSystem fires OnActionCompleted.
            // If it fails, ActionSystem fires OnActionFailed.
            // The GameplayState listens to these events and handles the rest.
            state.ActionSystem.FinalizeSpyReturn(_spyColor);
        }
    }
}



