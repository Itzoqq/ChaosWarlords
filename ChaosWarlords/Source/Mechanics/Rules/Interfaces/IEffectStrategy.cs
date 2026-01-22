using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules.Interfaces
{
    public interface IEffectStrategy
    {
        EffectType EffectType { get; }
        
        /// <summary>
        /// Determines the UI ActionState required for targeting this effect.
        /// Returns ActionState.Normal if no external targeting is required.
        /// </summary>
        ActionState GetTargetingState(CardEffect effect);

        /// <summary>
        /// Checks if there are valid targets for this effect in the current game state.
        /// </summary>
        bool HasValidTargets(MatchContext context, Player player, Card? sourceCard);

        /// <summary>
        /// Returns true if this effect requires user interaction (targeting).
        /// </summary>
        bool IsTargetingEffect { get; }
    }
}
