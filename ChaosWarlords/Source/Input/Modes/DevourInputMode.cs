using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Source.States.Input
{
    public class DevourInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState;
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private readonly Card? _sourceCard; // The card causing the devour (to prevent self-devour)

        public DevourInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _sourceCard = actionSystem.PendingCard; // Capture which card triggered this

            _gameplayState.Logger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            // 1. Cancel / Back out
            if (inputManager.IsRightMouseJustClicked() || inputManager.IsKeyJustPressed(Keys.Escape))
            {
                actionSystem.CancelTargeting();
                _gameplayState.SwitchToNormalMode();
                _gameplayState.Logger.Log("Cancelled Devour action.", LogChannel.General);
                return null;
            }

            // 1.5 Skip Optional Cost (Spacebar)
            if (inputManager.IsKeyJustPressed(Keys.Space))
            {
                 if (_sourceCard != null && _sourceCard.Location == CardLocation.Hand)
                 {
                     actionSystem.SetPreTarget(_sourceCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);
                     
                     // Try to Advance to next targeting step (e.g. Supplant)
                     if (actionSystem.AdvancePreCommitTargeting(_sourceCard))
                     {
                         // Advanced to next targeting state (State Change event handles Input Mode switch)
                         return null;
                     }

                     // No more targeting needed, commit the play.
                     actionSystem.CompleteAction(); 
                     _gameplayState.SwitchToNormalMode();
                     return new ChaosWarlords.Source.Commands.PlayCardCommand(_sourceCard, true);
                 }
                 // Standard Flow Skip? (Not supported yet for played cards, but optional cost usually implies Pre-Commit)
            }

            // 2. Select Card
            if (inputManager.IsLeftMouseJustClicked())
            {
                // We specifically look at the HAND, not Played cards
                Card? targetCard = _gameplayState.GetHoveredHandCard();

                if (targetCard is not null)
                {
                    // Validation: Cannot devour the card itself while it's being played
                    if (targetCard == _sourceCard)
                    {
                        _gameplayState.Logger.Log("Invalid Target: Cannot devour the card currently being played!", LogChannel.Warning);
                        return null;
                    }

                    // Pre-Commit Check:
                    // If Source Card is in Hand, we are choosing targets BEFORE playing.
                    if (_sourceCard != null && _sourceCard.Location == CardLocation.Hand)
                    {
                        actionSystem.SetPreTarget(_sourceCard, ActionState.TargetingDevourHand, targetCard);
                        
                        // Try to Advance to next targeting step (e.g. Supplant)
                        if (actionSystem.AdvancePreCommitTargeting(_sourceCard))
                        {
                            // Advanced to next targeting state.
                            return null;
                        }

                        actionSystem.CompleteAction(); 
                        _gameplayState.SwitchToNormalMode(); 
                        // Return PlayCommand to Commit the play with BYPASS
                        return new ChaosWarlords.Source.Commands.PlayCardCommand(_sourceCard, true);
                    }

                    // Standard Flow (Card already played/in limbo)
                    // Usage of HandleDevourSelection handles both immediate and deferred execution
                    _actionSystem.HandleDevourSelection(targetCard);
                    
                    // If we entered a chained targeting state (e.g. Supplant), switch mode.
                    // If we completed the action, switch to Normal.
                    if (_actionSystem.IsTargeting())
                    {
                         _gameplayState.SwitchToTargetingMode();
                    }
                    else
                    {
                         _gameplayState.SwitchToNormalMode();
                    }
                }
            }

            return null;
        }
    }
}




