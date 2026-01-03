using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;


namespace ChaosWarlords.Source.Input.Modes
{
    public class NormalPlayInputMode : IInputMode
    {
        private readonly IGameplayState _state; // Interface is enough now
        private readonly IInputManager _inputManager;
        private readonly IUIManager _uiManager;
        private readonly IMapManager _mapManager;
        private readonly ITurnManager _turnManager;
        private readonly IActionSystem _actionSystem;

        public NormalPlayInputMode(IGameplayState state, IInputManager inputManager, IUIManager uiManager, IMapManager mapManager, ITurnManager turnManager, IActionSystem actionSystem)
        {
            _state = state;
            _inputManager = inputManager;
            _uiManager = uiManager;
            _mapManager = mapManager;
            _turnManager = turnManager;
            _actionSystem = actionSystem;
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (inputManager.IsLeftMouseJustClicked())
            {
                // 1. Check Card Click
                Card? clickedCard = _state.GetHoveredHandCard();
                if (clickedCard is not null)
                {
                    // Pre-Commit Check for Devour cards
                    var devourEffect = clickedCard.Effects.FirstOrDefault(e => e.Type == ChaosWarlords.Source.Utilities.EffectType.Devour);
                    if (devourEffect != null)
                    {
                        // CRITICAL: Skip pre-commit targeting for optional devour effects
                        // The popup will handle the player's choice
                        if (devourEffect.IsOptional)
                        {
                            // Just play the card - popup will appear during effect resolution
                            return new PlayCardCommand(clickedCard);
                        }

                         _actionSystem.TryStartDevourHand(clickedCard);
                         
                         // Fix: Only switch mode if we successfully entered targeting (Cards existed to devour)
                         // If failed (no targets), fall through to PlayCardCommand to execute base effects.
                         if (_actionSystem.IsTargeting())
                         {
                             _state.SwitchToTargetingMode();
                             return null; 
                         }
                         // Fall through...
                    }

                    return new PlayCardCommand(clickedCard);
                }

                // 2. Check Map Click
                var clickedNode = mapManager.GetNodeAt(inputManager.MousePosition);
                if (clickedNode is not null && mapManager.CanDeployAt(clickedNode, activePlayer.Color))
                {
                    return new DeployTroopCommand(clickedNode);
                }
            }

            return null;
        }
    }
}




