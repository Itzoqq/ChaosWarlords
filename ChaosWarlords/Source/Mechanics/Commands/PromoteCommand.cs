using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using System.Linq;

namespace ChaosWarlords.Source.Commands
{
    public class PromoteCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.Promote;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.PromoteCommandDto
            {
                CardId = CardId
            };
        }

        public string? CardId { get; }

        public PromoteCommand(string? cardId)
        {
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            // Check if card exists in Hand/Played
            // We need to find it first.
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) ?? 
                       player.PlayedCards.FirstOrDefault(c => c.Id == CardId);
                       
            return card != null && context.PlayerStateManager.TryPromoteCard(player, card, out _);
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) ?? 
                       player.PlayedCards.FirstOrDefault(c => c.Id == CardId);

            if (card != null)
            {
                if (context.PlayerStateManager.TryPromoteCard(player, card, out var error))
                {
                     context.RecordAction("Promote", $"Promoted {card.Name} to Inner Circle.");
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
