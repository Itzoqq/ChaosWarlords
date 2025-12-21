using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.States.Input
{
    public class PromoteInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState; // Changed type to concrete to access EndTurn easily
        private readonly InputManager _inputManager;
        private readonly IActionSystem _actionSystem;
        private int _cardsLeftToPromote;

        public PromoteInputMode(IGameplayState gameplayState, InputManager inputManager, IActionSystem actionSystem, int amountToPromote)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;
            _cardsLeftToPromote = amountToPromote;

            GameLogger.Log($"Select {_cardsLeftToPromote} card(s) from your PLAYED pile to Promote.", LogChannel.General);
        }

        public IGameCommand HandleInput(InputManager input, IMarketManager market, IMapManager map, Player activePlayer, IActionSystem actionSystem)
        {
            if (input.IsRightMouseJustClicked() || input.IsKeyJustPressed(Keys.Escape))
            {
                actionSystem.CancelTargeting();
                _gameplayState.SwitchToNormalMode();
                GameLogger.Log("Cancelled End Turn. Finish playing cards.", LogChannel.General);
                return null;
            }

            if (input.IsLeftMouseJustClicked())
            {
                Card targetCard = _gameplayState.GetHoveredPlayedCard();

                if (targetCard != null)
                {
                    var context = _gameplayState.MatchContext.TurnManager.CurrentTurnContext;

                    // --- Safety Check ---
                    // Prevent a card from promoting itself if it is the only source of points
                    if (!context.HasValidCreditFor(targetCard))
                    {
                        GameLogger.Log("Invalid Target: This card cannot promote itself!", LogChannel.Warning);
                        return null;
                    }

                    if (activePlayer.PlayedCards.Contains(targetCard))
                    {
                        activePlayer.PlayedCards.Remove(targetCard);
                        activePlayer.InnerCircle.Add(targetCard);

                        // --- CHANGED: Consume specific credit ---
                        context.ConsumeCreditFor(targetCard);

                        _cardsLeftToPromote--;
                        GameLogger.Log($"Promoted {targetCard.Name} to Inner Circle!", LogChannel.Economy);

                        if (_cardsLeftToPromote <= 0)
                        {
                            actionSystem.CancelTargeting();
                            _gameplayState.EndTurn();
                        }
                    }
                }
            }

            return null;
        }
    }
}