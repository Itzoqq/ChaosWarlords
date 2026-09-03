using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// EffectType.PromoteFromPile - the immediate "promote a card from an expanded pool"
    /// primitive (Matron Mother, Necromancer), NOT the deferred end-of-turn promotion-credit
    /// flow (see EffectType.Promote, handled directly by CardEffectProcessor.ApplyPromote -
    /// unrelated to this class). TargetLocation selects the pool: DiscardPile means "discard
    /// pile only" (Matron Mother), HandOrDiscard means "Hand + DiscardPile + the source card
    /// itself" (Necromancer).
    /// </summary>
    public class PromoteFromPileStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.PromoteFromPile;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingPromoteFromPile;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            var effect = sourceCard != null ? EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.PromoteFromPile) : null;
            if (effect == null) return false;

            return effect.TargetLocation switch
            {
                CardLocation.DiscardPile => player.DiscardPile.Count > 0,
                // The source card itself is normally still sitting in PlayedCards at
                // resolution time, making this shape always have a target in practice today
                // (Necromancer). But that's not guaranteed by anything - a card that removes
                // itself from PlayedCards before this resolves (e.g. via
                // Card.RedirectsToSupplyOnDevourOrPromote) could leave hand, discard, AND the
                // source card all unavailable, so check the actual pool instead of assuming.
                CardLocation.HandOrDiscard => player.Hand.Count > 0 || player.DiscardPile.Count > 0 || sourceCard?.Location == CardLocation.Played,
                _ => false
            };
        }
    }
}
