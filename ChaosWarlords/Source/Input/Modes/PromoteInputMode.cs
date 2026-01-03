using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Input.Modes
{
    public class PromoteInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState; // Changed type to concrete to access EndTurn easily
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private int _cardsLeftToPromote;

        public PromoteInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem, int amountToPromote)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _cardsLeftToPromote = amountToPromote;

            _gameplayState.Logger.Log($"Select {_cardsLeftToPromote} card(s) from your PLAYED pile to Promote.", LogChannel.General);
        }

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (inputManager.IsRightMouseJustClicked() || inputManager.IsKeyJustPressed(Keys.Escape))
            {
                // Strict Rule Enforcement:
                _gameplayState.Logger.Log("Mandatory Action: You must select a card to promote.", LogChannel.Warning);
                return null;
            }

            if (inputManager.IsLeftMouseJustClicked())
            {
                Card? targetCard = _gameplayState.GetHoveredPlayedCard();

                if (targetCard is not null)
                {
                    var context = _gameplayState.MatchContext.TurnManager.CurrentTurnContext;

                    // --- Safety Check ---
                    // Prevent a card from promoting itself if it is the only source of points
                    if (!context.HasValidCreditFor(targetCard))
                    {
                        _gameplayState.Logger.Log("Invalid Target: This card cannot promote itself!", LogChannel.Warning);
                        return null;
                    }

                    _cardsLeftToPromote--;
                    _gameplayState.Logger.Log($"Promoted {targetCard.Name} to Inner Circle!", LogChannel.Economy);

                    context.ConsumeCreditFor(targetCard);

                    // 1. Manually execute the promote command immediately
                    var promoteCmd = new ChaosWarlords.Source.Commands.PromoteCommand(targetCard.Id);
                    _gameplayState.RecordAndExecuteCommand(promoteCmd);

                    // 2. Check if we are done
                    if (_cardsLeftToPromote <= 0)
                    {
                        actionSystem.CancelTargeting();
                        
                        // 3. Return EndTurn command to be executed by Coordinator immediately after
                        return new ChaosWarlords.Source.Commands.EndTurnCommand();
                    }

                    // 4. If not done, return null (Command already executed above)
                    return null;
                }
            }

            return null;
        }
    }
}
