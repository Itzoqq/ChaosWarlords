using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class AssassinateCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Assassinate;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.AssassinateCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId,
                DevourCardId = DevourCardId
            };
        }
        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public AssassinateCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
            DevourCardId = devourCardId;
        }

        public bool Validate(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node == null) return context.RejectValidation(nameof(AssassinateCommand), $"target node {TargetNodeId} not found.");

            var player = context.TurnManager.ActivePlayer; // Assassinate is usually active player action

            if (!HasSufficientPower(player))
            {
                return context.RejectValidation(nameof(AssassinateCommand), $"insufficient Power (has {player.Power}, needs {GameConstants.AssassinatePowerCost}).");
            }

            if (!context.MapManager.CanAssassinate(node, player, RequiresNeutralTarget(context)))
            {
                return context.RejectValidation(nameof(AssassinateCommand), $"MapManager rejected node {TargetNodeId} (presence/ownership/neutral-only check).");
            }

            if (!IsAtRequiredSite(context, node, out var requiredSiteName))
            {
                return context.RejectValidation(nameof(AssassinateCommand), $"node {TargetNodeId} is not at the required site ({requiredSiteName}).");
            }

            return true;
        }

        // When not fed by a card, this costs Power - enforced here (not just in the input
        // layer) so a directly-dispatched command can't grant a free assassination.
        private bool HasSufficientPower(Player player) =>
            !string.IsNullOrEmpty(CardId) || player.Power >= GameConstants.AssassinatePowerCost;

        // Re-derives the neutral-only restriction from the currently pending CardEffect (e.g.
        // Ravenous Zombies' "Assassinate a white troop") rather than trusting anything the
        // caller claims, since Validate() is the real defense once a client can send commands
        // directly.
        private static bool RequiresNeutralTarget(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.Assassinate && pendingEffect.TargetNeutralTroopOnly;
        }

        // Site-scoped Assassinate (Cloaker's chain-in from ReturnOwnSpy, or Minotaur Skeleton's
        // own later repeats via CardEffect.RestrictRepeatsToFirstTargetSite) -
        // ActionInputController.HandleAssassinate already enforces this for a click-built
        // command, but Validate() is the only real defense against a directly-dispatched,
        // forged command bypassing that UI-layer check entirely (untrusted client, once one
        // exists - see docs/testing.md's STANDING TEST MATRIX row 5).
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
                // Delegates to ActionSystem.PerformAssassinate (Power cost + MapManager +
                // CompleteAction) rather than duplicating those calls here, because that's
                // also where the transactional "Devour a card -> Assassinate" handling
                // lives (DevourCardId) - see planning.txt KNOWN BUGS for why this matters:
                // duplicating the logic here previously meant a deferred devour never
                // actually happened.
                context.ActionSystem.PerformAssassinate(node, CardId, DevourCardId);
            }
        }
    }
}
