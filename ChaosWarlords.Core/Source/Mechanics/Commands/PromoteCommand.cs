using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class PromoteCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Promote;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.PromoteCommandDto
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
            // Check if card exists in Hand/Played. This must stay a pure read - CommandDispatcher
            // calls Validate() then Execute() on the same instance, so actually promoting here
            // (as this used to do via TryPromoteCard) removes the card from Hand/Played as a side
            // effect of "checking", leaving Execute()'s own TryPromoteCard call to find nothing.
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) ??
                       player.PlayedCards.FirstOrDefault(c => c.Id == CardId);

            return card != null;
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
                // else: card vanished from Hand/Played between Validate() and Execute() (e.g. a
                // chained effect moved it) - nothing to promote, so this is a silent no-op.
            }
        }
    }
}
