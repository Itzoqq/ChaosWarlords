using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules.Strategies
{
    public class PlaceSpyStrategy : IEffectStrategy
    {
        public EffectType EffectType => EffectType.PlaceSpy;

        public bool IsTargetingEffect => true;

        // "Place 2 spies" (Masters of Sorcere) - see IEffectStrategy.SupportsRepeat's doc
        // comment. Each repeat's own click-time checks (SpiesInBarracks > 0, no spy already at
        // the clicked site) are re-evaluated fresh by SpySubsystem.HandlePlaceSpy/
        // PlaceSpyCommand.Validate for every click, same as Assassinate/MoveUnit's repeats.
        public bool SupportsRepeat => true;

        public ActionState GetTargetingState(CardEffect effect)
        {
            return ActionState.TargetingPlaceSpy;
        }

        public bool HasValidTargets(MatchContext context, Player player, Card? sourceCard)
        {
            if (player.SpiesInBarracks > 0)
            {
                return context.MapManager.HasValidPlaceSpyTarget(player);
            }

            // Empty barracks (rulebook p.12): "you may return one of your own spies first, then
            // place." Valid iff there's an own spy somewhere to return - returning always frees
            // that exact site back up, guaranteeing a legal placement target afterward AS LONG AS
            // every site holding a spy also has at least one troop space (HasValidPlaceSpyTarget's
            // own NodesInternal.Count > 0 filter, not re-checked here) - true of every shipped
            // site today. See SpySubsystem.HandlePlaceSpy for the click-handling half of this.
            return context.MapManager.HasOwnSpyOnBoard(player);
        }
    }
}
