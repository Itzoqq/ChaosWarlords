using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Rulebook p.12's Place-a-Spy exception: with an empty barracks, the player may return one
    /// of their own spies first, then place. This command is the RETURN half only - it
    /// deliberately does NOT call ActionSystem.CompleteAction(): the pending PlaceSpy effect is
    /// still open, this only replenishes the barracks by one so the very next click resolves as
    /// an ordinary PlaceSpyCommand instead (see SpySubsystem.HandlePlaceSpy). Distinct from
    /// ReturnOwnSpyCommand (e.g. Cloaker's ReturnOwnSpy -> Assassinate chain), which completes a
    /// SEPARATE EffectType.ReturnOwnSpy targeting step of its own - this one is a sub-step of
    /// EffectType.PlaceSpy itself, the same "same click means something different depending on
    /// live resource state" shape MoveUnitStrategy's source/destination pair uses, except here
    /// both sub-steps share ONE ActionState (TargetingPlaceSpy) rather than two.
    /// </summary>
    public class ReturnSpyToPlaceCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ReturnSpyToPlace;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ReturnSpyToPlaceCommandDto
            {
                SiteId = TargetSiteId,
                CardId = CardId
            };
        }

        public int TargetSiteId { get; }
        public string? CardId { get; }

        public ReturnSpyToPlaceCommand(int targetSiteId, string? cardId = null)
        {
            TargetSiteId = targetSiteId;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return context.RejectValidation(nameof(ReturnSpyToPlaceCommand), $"site {TargetSiteId} not found.");

            // Same authorization shape as PlaceSpyCommand/ReturnOwnSpyCommand - this has no
            // basic-action shape at all, only ever legitimate while a Place Spy effect is the
            // pending targeting state.
            if (context.ActionSystem.CurrentState != ActionState.TargetingPlaceSpy)
            {
                return context.RejectValidation(nameof(ReturnSpyToPlaceCommand), "no pending Place Spy effect is open - this command is never a standalone basic action.");
            }

            var player = context.TurnManager.ActivePlayer;

            // Only legal while the barracks is genuinely empty - re-derived rather than trusted,
            // matching every other command's don't-trust-the-client precedent. Once the
            // barracks has a spy, the SAME click must build a PlaceSpyCommand instead
            // (SpySubsystem.HandlePlaceSpy's own branch), never this one.
            if (player.SpiesInBarracks > 0)
            {
                return context.RejectValidation(nameof(ReturnSpyToPlaceCommand), "barracks is not empty - the return-then-place exception does not apply.");
            }

            if (!context.MapManager.CanReturnOwnSpy(site, player))
            {
                return context.RejectValidation(nameof(ReturnSpyToPlaceCommand), $"no {player.Color} spy at site '{site.Name}' to return.");
            }

            return true;
        }

        public void Execute(MatchContext context)
        {
            var site = context.MapManager.Sites.FirstOrDefault(s => s.Id == TargetSiteId);
            if (site == null) return;

            if (context.MapManager.ReturnOwnSpy(site, context.TurnManager.ActivePlayer))
            {
                context.RecordAction("ReturnSpyToPlace", $"Returned own spy from {site.Name} to place elsewhere (barracks was empty).");
            }

            // Deliberately no ActionSystem.CompleteAction() call here - see class doc comment.
            // CurrentState stays TargetingPlaceSpy, awaiting the actual placement click.
        }
    }
}
