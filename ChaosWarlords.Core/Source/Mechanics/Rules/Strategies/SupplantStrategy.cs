using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class SupplantStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.Supplant;

        public bool IsTargetingEffect => true;

        // "Supplant 2 white troops" (Demogorgon) - see IEffectStrategy.SupportsRepeat's doc
        // comment. Generic (RemainingRepeats/ShouldRepeatCurrentEffect live in
        // ActionExecutionEngine, effect-type-agnostic) - flipping this on needed no other engine
        // change, only this one bool, matching AssassinateStrategy's (Deathblade) precedent.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingSupplant;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            // Supplant = Assassinate + Deploy. An empty barracks doesn't block it - the
            // deploy half grants 1 VP instead (rulebook p.12/22, same as the plain Deploy
            // action), so only the assassinate half's target requirement gates this.
            var effect = sourceCard != null ? EffectTreeSearch.FindFirstEffect(sourceCard.Effects, EffectType.Supplant) : null;

            // ActionSystem.PendingSite - set by an earlier chain-link step (ReturnOwnSpyCommand:
            // Cloaker/Graz'zt, or PlaceSpyCommand: Green Dragon) - must restrict this lookahead
            // exactly the way SupplantCommand.Validate()'s own IsAtRequiredSite check already
            // unconditionally restricts the actual click. Without this, a site that has NO
            // supplantable troop could still pass this check purely because the board globally
            // has one elsewhere, pushing a TargetingSupplant state where every click - at the
            // real target OR at PendingSite - is rejected, with nothing to auto-resolve the
            // effect as a clean "no valid target" instead (a real, reproducible soft-lock, not
            // just a theoretical gap - see RESOLVED.txt).
            return context.MapManager.HasValidAssassinationTarget(player, effect?.TargetNeutralTroopOnly ?? false, effect?.IgnoresPresenceRequirement ?? false, context.ActionSystem.PendingSite);
        }
    }
}
