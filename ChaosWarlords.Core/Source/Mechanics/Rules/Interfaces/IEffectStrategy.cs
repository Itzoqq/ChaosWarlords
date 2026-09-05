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

        /// <summary>
        /// True if this targeting effect treats CardEffect.Amount as "how many separate targets
        /// to pick" (e.g. Deathblade: "Assassinate 2 troops") rather than ignoring it. Defaults
        /// to false so every existing effect - including every OTHER targeting effect type - is
        /// completely unaffected; only strategies that explicitly opt in support repeats.
        /// </summary>
        bool SupportsRepeat => false;
    }
}
