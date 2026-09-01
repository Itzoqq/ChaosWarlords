using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Play a card in the market costing Amount or less "as if it was in your hand" (e.g.
    /// Ulitharid). HasValidTargets mirrors DevourStrategy's pattern of reading the relevant
    /// effect's own data (here, the cost cap) off sourceCard.Effects, since the interface
    /// only passes a bare Card?.
    /// </summary>
    public class PlayFromMarketStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.PlayFromMarket;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingPlayFromMarket;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            int maxCost = FindMaxCost(sourceCard);
            return context.MarketManager.MarketRow.Any(c => c.Cost <= maxCost);
        }

        private static int FindMaxCost(Card? sourceCard)
        {
            if (sourceCard == null) return 0;
            var effect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.PlayFromMarket);
            return effect?.Amount ?? 0;
        }
    }
}
