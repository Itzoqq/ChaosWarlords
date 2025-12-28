using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;

using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.States.Input
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

            GameLogger.Log($"Select {_cardsLeftToPromote} card(s) from your PLAYED pile to Promote.", LogChannel.General);
        }

        public IGameCommand HandleInput(IInputManager input, IMarketManager market, IMapManager map, Player activePlayer, IActionSystem actionSystem)
        {
            if (input.IsRightMouseJustClicked() || input.IsKeyJustPressed(Keys.Escape))
            {
                // Strict Rule Enforcement:
                GameLogger.Log("Mandatory Action: You must select a card to promote.", LogChannel.Warning);
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




