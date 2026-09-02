using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Input.Modes
{
    /// <summary>
    /// Handles ActionState.TargetingPromoteFromPile - EffectType.PromoteFromPile's immediate
    /// "promote a card right now from an expanded pool" flow (e.g. Matron Mother, Necromancer).
    /// Modeled on DevourInputMode's TargetingDevourInnerCircle branch: the card browser
    /// (opened by GameplayView, populated per TargetLocation) is the click target, same as
    /// Devour-from-Inner-Circle. Much simpler than DevourInputMode overall - no pre-commit-flow
    /// or optional-skip complexity applies here, since neither Matron Mother's nor Necromancer's
    /// PromoteFromPile effect is reached via the pre-commit devour path.
    /// </summary>
    public class PromoteFromPileInputMode : IInputMode
    {
        private readonly IGameplayState _gameplayState;
        private readonly IInputManager _inputManager;
        private readonly IActionSystem _actionSystem;

        public PromoteFromPileInputMode(IGameplayState gameplayState, IInputManager inputManager, IActionSystem actionSystem)
        {
            _gameplayState = gameplayState;
            _inputManager = inputManager;
            _actionSystem = actionSystem;

            _gameplayState.Logger.Log("Select a card to Promote.", LogChannel.General);
        }

        private int _updateFrames;
        private const int CooldownFrames = 10; // Slightly longer to ensure popup click is fully cleared

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (_updateFrames < CooldownFrames) return null;

            // Handle Cancellation (Right Click or Escape)
            if (evt.Type == Core.Events.InputEventType.RightClick || (evt.Type == Core.Events.InputEventType.KeyDown && evt.Key == Keys.Escape))
            {
                return HandleCancellation(actionSystem);
            }

            // Handle Selection (Left Click)
            if (evt.Type == Core.Events.InputEventType.LeftClick)
            {
                return HandleCardClick(actionSystem);
            }

            return null;
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            _updateFrames++;
            // The card browser's own hover state is driven by GameplayView.Update.
        }

        private Commands.PromoteCommand? HandleCardClick(IActionSystem actionSystem)
        {
            var targetCard = _gameplayState.GetHoveredBrowserCard();
            if (targetCard is null)
            {
                return null;
            }

            var cmd = actionSystem.HandlePromoteFromPileSelection(targetCard);

            if (cmd == null)
            {
                return null;
            }

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

        private Commands.SwitchToNormalModeCommand HandleCancellation(IActionSystem actionSystem)
        {
            _gameplayState.Logger.Log("Promote cancelled.", LogChannel.Info);
            actionSystem.CancelTargeting();

            // Explicitly switch state back to avoid stuck states.
            _gameplayState.SwitchToNormalMode();

            return new Commands.SwitchToNormalModeCommand();
        }
    }
}
