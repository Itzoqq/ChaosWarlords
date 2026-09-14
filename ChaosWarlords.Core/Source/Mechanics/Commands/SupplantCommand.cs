using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class SupplantCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Supplant;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.SupplantCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId,
                DevourCardId = DevourCardId
            };
        }

        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public SupplantCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
            DevourCardId = devourCardId;
        }

        public bool Validate(MatchContext context)
        {
            // Supplant = Assassinate + Deploy: "Recall an enemy troop, then place one of your
            // troops at that site." Mirrors ActionInputController.HandleSupplant's checks. An
            // empty barracks doesn't invalidate the command - the deploy half grants 1 VP
            // instead (rulebook p.12/22), handled by MapManager.Supplant/ExecuteSupplant -
            // only the assassinate half's target requirement gates this.
            var node = context.MapManager.GetNodeById(TargetNodeId);
            var player = context.TurnManager.ActivePlayer;

            if (node == null) return context.RejectValidation(nameof(SupplantCommand), $"target node {TargetNodeId} not found.");

            // Supplant has no basic-action shape at all (it only ever happens via a card-
            // granted EffectType.Supplant) - re-derived from ActionSystem's own trusted
            // CurrentState rather than trusting that a command was only ever built through
            // the real click path. Without this, a directly-dispatched command outside its
            // owning targeting state would mutate the board (recall an enemy troop, deploy
            // the active player's own) for free.
            if (context.ActionSystem.CurrentState != ActionState.TargetingSupplant)
            {
                return context.RejectValidation(nameof(SupplantCommand), "no pending Supplant effect is open - this command is never a standalone basic action.");
            }

            if (!context.MapManager.CanAssassinate(node, player, RequiresNeutralTarget(context), IgnoresPresenceRequirement(context)))
            {
                return context.RejectValidation(nameof(SupplantCommand), $"MapManager rejected node {TargetNodeId} (presence/ownership/neutral-only check).");
            }

            if (!IsAtRequiredSite(context, node, out var requiredSiteName))
            {
                return context.RejectValidation(nameof(SupplantCommand), $"node {TargetNodeId} is not at the required site ({requiredSiteName}).");
            }

            return true;
        }

        // Re-derives the neutral-only restriction from the currently pending CardEffect (e.g.
        // Ravenous Zombies' "Assassinate a white troop") rather than trusting anything the
        // caller claims, since Validate() is the real defense once a client can send commands
        // directly. Mirrors AssassinateCommand.RequiresNeutralTarget.
        private static bool RequiresNeutralTarget(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.Supplant && pendingEffect.TargetNeutralTroopOnly;
        }

        // Re-derives "anywhere on the board" (Ogre Zombie) the same way - see
        // RequiresNeutralTarget's own doc comment for why this reads CurrentSourceEffect
        // instead of trusting the caller.
        private static bool IgnoresPresenceRequirement(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.Supplant && pendingEffect.IgnoresPresenceRequirement;
        }

        // Site-scoped Supplant (Graz'zt's chain-in from ReturnOwnSpy: "Supplant a troop at
        // [the just-returned spy's] site") - ActionInputController.HandleSupplant already
        // enforces this for a click-built command, but Validate() is the only real defense
        // against a directly-dispatched, forged command bypassing that UI-layer check entirely
        // (untrusted client, once one exists - see docs/testing.md's STANDING TEST MATRIX row
        // 5). Mirrors AssassinateCommand.IsAtRequiredSite exactly (same ActionSystem.PendingSite
        // field, same Minotaur-Skeleton-and-Cloaker precedent).
        private static bool IsAtRequiredSite(MatchContext context, MapNode node, out string requiredSiteName)
        {
            var pendingSite = context.ActionSystem.PendingSite;
            requiredSiteName = pendingSite?.Name ?? string.Empty;
            return pendingSite == null || pendingSite.NodesInternal.Contains(node);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node != null)
            {
                // Delegates to ActionSystem.PerformSupplant rather than duplicating the
                // MapManager/CompleteAction calls here, because that's also where the
                // transactional "Devour a card -> Supplant" handling lives (DevourCardId) -
                // see planning.txt KNOWN BUGS for why this matters: duplicating the logic
                // here previously meant a deferred devour (e.g. the Wight card) never
                // actually happened.
                context.ActionSystem.PerformSupplant(node, CardId, DevourCardId);
            }
        }
    }
}
