using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
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

        /// <summary>
        /// The full set of ActionStates belonging to this strategy's OWN targeting sub-flow,
        /// including its entry state (GetTargetingState's return value) - e.g. MoveUnitStrategy
        /// (a 2-click source-then-destination pick) returns BOTH TargetingMoveSource and
        /// TargetingMoveDestination. Single-click strategies default to just their own entry
        /// state via GetTargetingState. Used by DeclineRepeatCommand/TargetingInputMode to ask
        /// "is the CURRENT ActionState still part of this same effect's own in-progress
        /// attempt" - i.e. safe to reset/decline from - as opposed to some genuinely different
        /// effect/chain step. See CardEffect.AllowPartialRepeat's "up to N" primitive.
        /// </summary>
        IEnumerable<ActionState> GetOwnedActionStates(CardEffect effect)
        {
            yield return GetTargetingState(effect);
        }

        /// <summary>
        /// Discards whatever in-flight, NOT-YET-COMMITTED sub-pick this strategy currently owns
        /// (e.g. MoveUnitStrategy clears ActionSystem.PendingMoveSource) - WITHOUT reverting
        /// anything the effect has already committed via a real dispatched command. Used both
        /// to "step back" mid-repeat on a mistimed right-click, and defensively before a
        /// button-triggered decline that might be mid-sub-step. Single-click strategies have
        /// nothing partial to clear, so this defaults to a no-op.
        /// </summary>
        void ResetInProgressSelection(IActionSystem actionSystem) { }
    }
}
