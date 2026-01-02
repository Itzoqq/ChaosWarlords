using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class SupplantCommand : IGameCommand
    {
        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public SupplantCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
        {
            TargetNodeId = targetNodeId;
            CardId = cardId;
            DevourCardId = devourCardId;
        }

        public void Execute(IGameplayState state)
        {
            // Transactional Devour Handling
            if (!string.IsNullOrEmpty(DevourCardId))
            {
                var cardToDevour = state.MatchContext.CardDatabase.GetCardById(DevourCardId);
                // Note: GetCardById returns a fresh instance normally, but for runtime state we need the actual card instance?
                // The CardDatabase likely returns New instances. 
                // We need the instance in the Player's Hand?
                // MatchManager.DevourCard uses the Card object.
                // We typically find the card in the player's Hand by ID.
                
                // Assuming ID is unique enough for lookup in Hand + Played + Limbo.
                // In a perfect world, we have a global card lookup/registry for runtime cards.
                // For now, let's search Player's Hand.
                
                var player = state.TurnManager.ActivePlayer;
                // Search Hand
                var instance = player.Hand.FirstOrDefault(c => c.Id == DevourCardId);
                
                // If not in hand (Unlikely if transactional), could be elsewhere.
                if (instance != null)
                {
                    state.MatchManager.DevourCard(instance);
                }
                else
                {
                   // Log error or ignore? 
                   // Replay safety: If card is already gone, maybe fine.
                }
            }

            var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                state.ActionSystem.PerformSupplant(node, CardId);
                state.MatchContext?.RecordAction("Supplant", $"Supplanted troop at {node.Id} [Devoured: {DevourCardId ?? "None"}]");
            }
        }
    }
}
