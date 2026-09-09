using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class ReturnTroopCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.ReturnTroop;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.ReturnTroopCommandDto
            {
                NodeId = TargetNodeId,
                CardId = CardId
            };
        }
        public int TargetNodeId { get; }
        public string? CardId { get; }

        public ReturnTroopCommand(int targetNodeId, string? cardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node == null) return context.RejectValidation(nameof(ReturnTroopCommand), $"node {TargetNodeId} not found.");

            // Delegates to MapManager.CanReturnTroop - the single authoritative check
            // (occupied, not Neutral, and Presence required only for an enemy troop, not the
            // requester's own). This used to reimplement those conditions independently,
            // which is exactly how the Presence-for-own-troops bug (see CanReturnTroop's
            // comment) could have been fixed in one of the two places and not the other.
            if (!context.MapManager.CanReturnTroop(node, context.TurnManager.ActivePlayer, RequiresEnemyOnly(context)))
            {
                return context.RejectValidation(nameof(ReturnTroopCommand), $"MapManager rejected returning the troop at node {TargetNodeId} (empty, Neutral, no Presence, or own troop excluded by an enemy-only effect).");
            }
            return true;
        }

        // Re-derives the enemy-only restriction from the currently pending CardEffect (High
        // Priest of Myrkul's "Return ANOTHER PLAYER'S troop or spy") rather than trusting
        // anything the caller claims, since Validate() is the real defense once a client can
        // send commands directly - same pattern as AssassinateCommand.RequiresNeutralTarget.
        // ReturnTroopCommand is ALSO used by the plain EffectType.ReturnUnit (which has no such
        // flag), so this only ever restricts the EffectType.ReturnUnitOrSpy-driven case.
        private static bool RequiresEnemyOnly(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            return pendingEffect != null && pendingEffect.Type == EffectType.ReturnUnitOrSpy && pendingEffect.ReturnEnemyOnly;
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node != null)
            {
                context.MapManager.ReturnTroop(node, context.TurnManager.ActivePlayer);
                context.RecordAction("ReturnTroop", $"Returned troop at {node.Id}");
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
