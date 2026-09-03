using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;


namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class DevourStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.Devour;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return effect.TargetLocation switch
            {
                CardLocation.Market => ActionState.TargetingDevourMarket,
                CardLocation.InnerCircle => ActionState.TargetingDevourInnerCircle,
                CardLocation.Self => ActionState.Normal,
                _ => ActionState.TargetingDevourHand
            };
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // We need to check EACH effect on the card that is Devour, or rely on passed context if we had specific effect.
            // But HasValidTargets signature takes Card? sourceCard.
            // If sourcecard is null, we can't fully know.
            // Assuming sourceCard is provided as per Interface.

            if (sourceCard == null) return HasHandTargets(player, sourceCard);

            var devourEffect = EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.Devour);
            if (devourEffect == null) return HasHandTargets(player, sourceCard);

            return devourEffect.TargetLocation switch
            {
                CardLocation.Self => true,
                CardLocation.Market => HasMarketTargets(context),
                CardLocation.Deck => HasDeckTargets(player),
                CardLocation.InnerCircle => HasInnerCircleTargets(player),
                _ => HasHandTargets(player, sourceCard)
            };
        }

        // Helper methods (Logic moved from CardRuleEngine)

        private static bool HasMarketTargets(MatchContext context)
        {
             return context.MarketManager.MarketRow.Count > 0;
             // Logging removed from strategy to keep it pure, or could inject logger?
             // Simplification: pure logic.
        }

        private static bool HasDeckTargets(Player player)
        {
            return player.Deck.Count > 0;
        }

        private static bool HasInnerCircleTargets(Player player)
        {
            return player.InnerCircle.Count > 0;
        }

        private static bool HasHandTargets(Player player, Card? sourceCard)
        {
            return player.Hand.Any(c => c != sourceCard);
        }
    }
}
