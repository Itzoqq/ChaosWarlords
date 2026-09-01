using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    /// <summary>
    /// Generic "target a player" primitive - the active player chooses one opponent with a
    /// hand size exceeding an eligibility threshold (e.g. Cranium Rats). HasValidTargets
    /// mirrors PlayFromMarketStrategy's pattern of reading the relevant effect's own data
    /// (here, the hand-size threshold) off sourceCard.Effects, since the interface only
    /// passes a bare Card?.
    /// </summary>
    public class SelectOpponentStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.SelectOpponent;

        public bool IsTargetingEffect => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingOpponentSelect;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            int threshold = FindThreshold(sourceCard);
            return context.TurnManager.Players.Any(p => p != player && p.Hand.Count > threshold);
        }

        private static int FindThreshold(Card? sourceCard)
        {
            if (sourceCard == null) return 0;
            var effect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.SelectOpponent);
            return effect?.Amount ?? 0;
        }
    }
}
