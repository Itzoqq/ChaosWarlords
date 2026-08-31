using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class DevourCardCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DevourCard;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DevourCardCommandDto
            {
                CardId = CardId,
                CardRuntimeId = CardRuntimeId,
                Location = LocationAtConstruction.ToString(),
                SourceCardId = SourceCardId,
                SourceCardRuntimeId = SourceCardRuntimeId
            };
        }

        /// <summary>
        /// The specific card copy to devour. Resolved against the player's Hand/InnerCircle/
        /// PlayedCards and the market row in Execute() - matches the ID-based pattern the
        /// other commands use, and avoids the Card.Id ambiguity a duplicate-copy card would
        /// hit if this only matched on the shared definition id.
        /// </summary>
        public System.Guid CardRuntimeId { get; }
        public string CardId { get; }

        /// <summary>
        /// The card's Location at the moment this command was constructed (from hydration,
        /// or from the live card when initiated by a click). Execute() re-resolves the card
        /// and reads its then-current Location for its own Market/non-Market branch, so this
        /// snapshot is informational/DTO round-trip data, not what Execute() branches on.
        /// </summary>
        public CardLocation LocationAtConstruction { get; }

        /// <summary>
        /// Optional: for "Replace With Source" mechanics and devour-chain resumption.
        /// Setter-only bridge from a live Card (used by DevourSubsystem's object-initializer
        /// construction: <c>new DevourCardCommand(card) { SourceCard = ... }</c>) onto the
        /// stored id fields actually used by Validate()/Execute()/ToDto().
        /// </summary>
        public Card? SourceCard
        {
            set
            {
                SourceCardRuntimeId = value?.RuntimeId;
                SourceCardId = value?.Id;
            }
        }
        public System.Guid? SourceCardRuntimeId { get; private set; }
        public string? SourceCardId { get; private set; }

        public bool IsDeferred { get; set; }

        public DevourCardCommand(Card card)
        {
            CardRuntimeId = card.RuntimeId;
            CardId = card.Id;
            LocationAtConstruction = card.Location;
        }

        public DevourCardCommand(System.Guid cardRuntimeId, string cardId, CardLocation locationAtConstruction)
        {
            CardRuntimeId = cardRuntimeId;
            CardId = cardId;
            LocationAtConstruction = locationAtConstruction;
        }

        private static Card? ResolveCard(MatchContext context, System.Guid runtimeId)
        {
            Player player = context.TurnManager.ActivePlayer;
            return player.Hand.FirstOrDefault(c => c.RuntimeId == runtimeId)
                ?? player.InnerCircle.FirstOrDefault(c => c.RuntimeId == runtimeId)
                ?? player.PlayedCards.FirstOrDefault(c => c.RuntimeId == runtimeId)
                ?? context.MarketManager.MarketRow?.FirstOrDefault(c => c.RuntimeId == runtimeId);
        }

        public bool Validate(MatchContext context)
        {
            // Mirrors PlayCardCommand/BuyCardCommand's own Validate(): resolve the same way
            // Execute() will and reject if the card can't be found (already devoured, already
            // moved, or simply never existed - matters once an untrusted client can send this
            // command directly to a server, not just a trusted single-process replay).
            return ResolveCard(context, CardRuntimeId) != null;
        }

        public void Execute(MatchContext context)
        {
            var cardToDevour = ResolveCard(context, CardRuntimeId);
            if (cardToDevour == null) return;

            var sourceCard = SourceCardRuntimeId.HasValue ? ResolveCard(context, SourceCardRuntimeId.Value) : null;

            if (IsDeferred)
            {
                context.ActionSystem.DeferDevour(cardToDevour);
            }
            else if (cardToDevour.Location == CardLocation.Market)
            {
                context.MatchManager.DevourMarketCard(cardToDevour, sourceCard);
                context.ActionSystem.CompleteAction();
            }
            else
            {
                context.MatchManager.DevourCard(cardToDevour, sourceCard);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
