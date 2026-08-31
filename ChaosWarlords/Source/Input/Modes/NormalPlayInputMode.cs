using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Rendering;
using Microsoft.Xna.Framework;


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

        public IGameCommand? HandleInteraction(Core.Events.InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem)
        {
            if (evt.Type != Core.Events.InputEventType.LeftClick)
            {
                return null;
            }

            // 1. Check Card Click
            Card? clickedCard = _state.GetHoveredHandCard();
            if (clickedCard is not null)
            {
                return HandleCardClick(clickedCard, actionSystem);
            }

            // 2. Check Map Click
            // Note: We use evt.Position instead of polling inputManager.MousePosition if we want precision,
            // but mapManager.GetNodeAt typically expects global mouse pos. 
            // The event carries the position of the click, which is safer.
            return HandleMapClick(evt.Position, mapManager, activePlayer);
        }

        public void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer)
        {
            // Continuous logic (e.g. Map Panning, Hover effects)
            // Normal mode doesn't explicitly handle panning yet (handled by Camera controller usually)
            // But we can add hover logic here if needed.
        }

        private PlayCardCommand? HandleCardClick(Card clickedCard, IActionSystem actionSystem)
        {
            // Check if this card has a devour effect that needs pre-commit handling
            var devourEffect = clickedCard.Effects.FirstOrDefault(e => e.Type == EffectType.Devour);

            if (devourEffect != null && ShouldHandleDevourPreCommit(devourEffect))
            {
                return HandleDevourCardClick(clickedCard, actionSystem);
            }

            // Normal card play
            return new PlayCardCommand(clickedCard);
        }

        private static bool ShouldHandleDevourPreCommit(CardEffect devourEffect)
        {
            // Skip pre-commit for optional devour (popup will handle it)
            if (devourEffect.IsOptional)
            {
                return false;
            }

            // Pre-commit (select the target BEFORE the card is played) only makes sense for
            // Hand-targeted devour: clicking a card in your own hand to devour it is exactly
            // the same UI gesture as this method already intercepts, so it can resolve before
            // PlayCardCommand ever dispatches. Market and InnerCircle devour targets are
            // Browse/Market-panel selections, not hand clicks - handling them "pre-commit" via
            // this method would call TryStartDevourInnerCircle/TryStartDevourMarket on a card
            // that was never Play()'d (never pushed onto ExecutionStack via ResolveEffects),
            // and MatchManager.ShouldResumeDevourChain's "source card not on stack -> manually
            // resume the OnSuccess chain" fallback would then resume the chain WITHOUT ever
            // actually playing the base card (moving it to Played, applying its non-devour
            // effects) - silently worse than doing nothing, not better. Both already correctly
            // fall through to normal play here, exactly like they always have; a MANDATORY
            // InnerCircle devour still works correctly today via the existing post-play
            // required-input path (ResolveEffects pushes it onto ExecutionStack, so it
            // naturally becomes TargetingDevourInnerCircle after PlayCardCommand runs, with the
            // card genuinely on the stack for ShouldResumeDevourChain to find) - it just can't
            // be pre-selected by clicking the source card in Hand. See planning.txt.
            if (devourEffect.TargetLocation != CardLocation.Hand)
            {
                return false;
            }

            return true;
        }

        private PlayCardCommand? HandleDevourCardClick(Card clickedCard, IActionSystem actionSystem)
        {
            actionSystem.TryStartDevourHand(clickedCard);

            // Only switch mode if we successfully entered targeting
            if (actionSystem.IsTargeting())
            {
                _state.SwitchToTargetingMode();
                return null;
            }

            // No valid devour targets - fall through to normal play
            return new PlayCardCommand(clickedCard);
        }

        private static DeployTroopCommand? HandleMapClick(Vector2 clickPosition, IMapManager mapManager, Player activePlayer)
        {
            var clickedNode = mapManager.GetNodeAt(clickPosition.ToLogicVector2());

            if (clickedNode is not null && mapManager.CanDeployAt(clickedNode, activePlayer.Color))
            {
                return new DeployTroopCommand(clickedNode);
            }

            return null;
        }
    }
}




