using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Forces the active player to discard a card from their OWN hand (e.g. Insane Outcast's
    /// own cost - "discard a card from your hand"). Neogi's cross-player forced discard
    /// (each OPPONENT discards) is a separate, deferred-to-end-of-turn flow orchestrated by
    /// MatchManager, not resolved through this strategy's HasValidTargets/targeting path -
    /// see MatchManager.AdvanceOpponentDiscard.
    /// </summary>
    public class DiscardStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.DiscardCard;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingDiscard;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // By the time an effect resolves, the source card has already left Hand (moved to
            // Played by PlayerStateManager.PlayCard before ResolveEffects runs) - no need to
            // exclude it the way DevourStrategy does for its own hand-target lookahead.
            return player.Hand.Count > 0;
        }
    }
}
