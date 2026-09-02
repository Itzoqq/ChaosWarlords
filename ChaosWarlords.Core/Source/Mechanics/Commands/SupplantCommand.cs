using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
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

            if (node == null) return false;

            // Re-derives the neutral-only restriction from the currently pending CardEffect
            // (e.g. Ravenous Zombies' "Assassinate a white troop") rather than trusting
            // anything the caller claims, since Validate() is the real defense once a client
            // can send commands directly.
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            bool requireNeutral = pendingEffect != null && pendingEffect.Type == EffectType.Supplant && pendingEffect.TargetNeutralTroopOnly;

            return context.MapManager.CanAssassinate(node, player, requireNeutral);
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
