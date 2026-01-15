using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Source.Input.Modes
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

            _sourceCard = actionSystem.PendingCard; // Capture which card triggered this

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                _gameplayState.Logger.Log("Select a card from your INNER CIRCLE to Devour.", LogChannel.General);
            }
            else
            {
                _gameplayState.Logger.Log("Select a card from your HAND to Devour (Remove from game).", LogChannel.General);
            }
        }

        private int _updateFrames;
        private const int COOLDOWN_FRAMES = 10; // Slightly longer to ensure popup click is fully cleared

        public IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            _updateFrames++;

            if (_updateFrames < COOLDOWN_FRAMES) return null;

            if (ShouldCancel(inputManager))
                return HandleCancellation(actionSystem);

            if (ShouldSkipOptionalCost(inputManager))
                return HandleSkipOptionalCost(actionSystem);

            if (inputManager.IsLeftMouseJustClicked())
                return HandleCardClick(actionSystem);

            return null;
        }

        private static bool ShouldCancel(IInputManager inputManager)
        {
            return inputManager.IsRightMouseJustClicked() || inputManager.IsKeyJustPressed(Keys.Escape);
        }

        private IGameCommand? HandleCancellation(IActionSystem actionSystem)
        {
            actionSystem.CancelTargeting();
            _gameplayState.SwitchToNormalMode();
            _gameplayState.Logger.Log("Cancelled Devour action.", LogChannel.General);
            return null;
        }

        private static bool ShouldSkipOptionalCost(IInputManager inputManager)
        {
            return inputManager.IsKeyJustPressed(Keys.Space);
        }

        private Commands.PlayCardCommand? HandleSkipOptionalCost(IActionSystem actionSystem)
        {
            if (!IsPreCommitFlow())
            {
                return null; // Skip only supported for pre-commit flow
            }

            actionSystem.SetPreTarget(_sourceCard!, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            if (actionSystem.AdvancePreCommitTargeting(_sourceCard!))
            {
                // Advanced to next targeting state
                return null;
            }

            // No more targeting needed, commit the play
            actionSystem.CompleteAction();
            _gameplayState.SwitchToNormalMode();
            return new Commands.PlayCardCommand(_sourceCard!, true);
        }

        private IGameCommand? HandleCardClick(IActionSystem actionSystem)
        {
            Card? targetCard = null;

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                targetCard = _gameplayState.GetHoveredBrowserCard();
            }
            else
            {
                // Default to Hand
                targetCard = _gameplayState.GetHoveredHandCard();
            }

            if (targetCard is null)
            {
                return null;
            }

            if (!IsValidDevourTarget(targetCard))
            {
                return null;
            }

            if (actionSystem.CurrentState == ActionState.TargetingDevourInnerCircle)
            {
                var cmd = actionSystem.HandleDevourInnerCircleSelection(targetCard);
                return cmd;
            }

            return IsPreCommitFlow()
                ? HandlePreCommitSelection(targetCard, actionSystem)
                : HandleStandardFlowSelection(targetCard);
        }

        private bool IsPreCommitFlow()
        {
            return _sourceCard != null && _sourceCard.Location == CardLocation.Hand;
        }

        private bool IsValidDevourTarget(Card targetCard)
        {
            if (targetCard == _sourceCard)
            {
                _gameplayState.Logger.Log("Invalid Target: Cannot devour the card currently being played!", LogChannel.Warning);
                return false;
            }
            return true;
        }

        private Commands.PlayCardCommand? HandlePreCommitSelection(Card targetCard, IActionSystem actionSystem)
        {
            actionSystem.SetPreTarget(_sourceCard!, ActionState.TargetingDevourHand, targetCard);

            if (actionSystem.AdvancePreCommitTargeting(_sourceCard!))
            {
                // Advanced to next targeting state
                return null;
            }

            // Chain complete, commit the play
            actionSystem.CompleteAction();
            _gameplayState.SwitchToNormalMode();
            return new Commands.PlayCardCommand(_sourceCard!, true);
        }

        private Commands.DevourCardCommand? HandleStandardFlowSelection(Card targetCard)
        {
            var cmd = _actionSystem.HandleDevourSelection(targetCard);

            if (_actionSystem.IsTargeting())
            {
                _gameplayState.SwitchToTargetingMode();
            }
            else
            {
                _gameplayState.SwitchToNormalMode();
            }

            return cmd;
        }
    }
}




