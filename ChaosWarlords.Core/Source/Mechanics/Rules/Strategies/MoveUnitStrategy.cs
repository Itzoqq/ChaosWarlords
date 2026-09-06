using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class MoveUnitStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.MoveUnit;

        public bool IsTargetingEffect => true;

        // "Move up to 2 enemy troops" (Council Member) - see IEffectStrategy.SupportsRepeat's
        // doc comment. Safe for the existing single-move card (test_displacer, Amount=1):
        // PushEffectContext clamps RemainingRepeats to Math.Max(1, Amount), so
        // ShouldRepeatCurrentEffect's RemainingRepeats<=1 short-circuit still fires
        // immediately for it, completely unchanged.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingMoveSource;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            return context.MapManager.HasValidMoveSource(player);
        }

        // 2-click sub-flow (source, then destination) - see IEffectStrategy.
        // GetOwnedActionStates's own doc comment for why this needs overriding.
        public IEnumerable<ActionState> GetOwnedActionStates(CardEffect effect)
        {
            yield return ActionState.TargetingMoveSource;
            yield return ActionState.TargetingMoveDestination;
        }

        // Clears the not-yet-committed source pick (nothing has been dispatched yet at this
        // point - only a real MoveTroopCommand, once both source AND destination are chosen,
        // counts as committed) - see IEffectStrategy.ResetInProgressSelection's own doc comment.
        // Uses ClearPendingMoveSource(), NOT SetMoveSource(null): this method must be safe to
        // call even when nothing is actually pending (e.g. declining right at this effect's own
        // entry state, before any source was ever picked) - SetMoveSource(null) would have
        // unconditionally forced a real TargetingMoveSource -> TargetingMoveDestination
        // transition every time, even when there's nothing to clear.
        public void ResetInProgressSelection(IActionSystem actionSystem)
        {
            actionSystem.ClearPendingMoveSource();
        }
    }
}
