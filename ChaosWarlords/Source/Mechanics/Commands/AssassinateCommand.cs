using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class AssassinateCommand : IGameCommand
    {
        public int TargetNodeId { get; }
        public string? CardId { get; }
        public string? DevourCardId { get; }

        public AssassinateCommand(int targetNodeId, string? cardId = null, string? devourCardId = null)
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
                var player = state.TurnManager.ActivePlayer;
                var instance = player.Hand.FirstOrDefault(c => c.Id == DevourCardId);
                if (instance != null)
                {
                    state.MatchManager.DevourCard(instance);
                }
            }

            var node = state.MapManager.Nodes.FirstOrDefault(n => n.Id == TargetNodeId);
            if (node != null)
            {
                // 1. Execute the Action Logic
                state.ActionSystem.PerformAssassinate(node, CardId);
                
                // 2. Ensuring Consistency for Replay/Direct Execution
                // In normal gameplay, UIEventMediator handles playing the card via ActionSystem.PendingCard.
                // In Replay (or if PendingCard is null), we must play it manually to remove it from hand.
                if (!string.IsNullOrEmpty(CardId)) 
                {
                     if (state.ActionSystem.PendingCard == null)
                     {
                         var player = state.TurnManager.ActivePlayer;
                         var card = player.Hand.FirstOrDefault(c => c.Id == CardId);
                         if (card != null)
                         {
                             state.MatchManager.PlayCard(card);
                         }
                     }
                }
                
                state.MatchContext?.RecordAction("Assassinate", $"Assassinated troop at {node.Id}");
            }
        }
    }
}
