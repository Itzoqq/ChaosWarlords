using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Commands
{
    public class PlayCardCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.PlayCard;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.PlayCardCommandDto
            {
                CardId = CardId,
                CardRuntimeId = CardRuntimeId,
                HandIdx = -1 // Cannot determine without context, relying on ID
            };
        }

        /// <summary>
        /// The specific card copy to play. Resolved against the active player's hand in
        /// Validate()/Execute() - the command itself carries only IDs (network/replay-safe),
        /// matching AssassinateCommand et al.'s pattern.
        /// </summary>
        public System.Guid CardRuntimeId { get; }

        /// <summary>
        /// The card's shared definition id, kept alongside CardRuntimeId for DTO
        /// serialization/logging and as a fallback when hydrating older replay data that
        /// predates CardRuntimeId.
        /// </summary>
        public string CardId { get; }

        public bool BypassChecks { get; }

        public PlayCardCommand(Card card, bool bypassChecks = false)
        {
            CardRuntimeId = card.RuntimeId;
            CardId = card.Id;
            BypassChecks = bypassChecks;
        }

        public PlayCardCommand(System.Guid cardRuntimeId, string cardId, bool bypassChecks = false)
        {
            CardRuntimeId = cardRuntimeId;
            CardId = cardId;
            BypassChecks = bypassChecks;
        }

        private Card? ResolveCard(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            return player.Hand.FirstOrDefault(c => c.RuntimeId == CardRuntimeId);
        }

        public bool Validate(MatchContext context)
        {
            // Can Play if in hand
            return ResolveCard(context) != null;
        }

        public void Execute(MatchContext context)
        {
            var card = ResolveCard(context);
            if (card != null)
            {
                context.MatchManager.PlayCard(card);
            }
        }
    }
}
