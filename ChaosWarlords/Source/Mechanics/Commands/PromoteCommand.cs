using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class PromoteCommand : IGameCommand
    {
        public string? CardId { get; }

        public PromoteCommand(string? cardId)
        {
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            if (string.IsNullOrEmpty(CardId)) return;

            var player = state.TurnManager.ActivePlayer;
            // Try to find the card in Hand, Discard, or Played area.
            // Promotion usually happens from Hand or Played.
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) 
                    ?? player.PlayedCards.FirstOrDefault(c => c.Id == CardId);

            if (card != null)
            {
                if (state.MatchContext.PlayerStateManager.TryPromoteCard(player, card, out var error))
                {
                     state.MatchContext.RecordAction("Promote", $"Promoted {card.Name} to Inner Circle.");
                }
                else
                {
                    // If logic fails (e.g. no credits or full), we should at least log it?
                    // But command execution implies "Do it". 
                    // However, we rely on the ActionSystem or source to validate preconditions.
                }
            }
        }
    }
}
