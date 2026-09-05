using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Lightweight representation of a Card for serialization.
    /// Static data (Cost, Name, Effects) is NOT serialized; only stateful data is.
    /// </summary>
    public class CardDto : IDto<Card>
    {
        // Card.DefinitionId, NOT Card.Id - Id carries a CardFactory.GenerateUniqueId suffix
        // (so two copies of the same catalog card get distinct ids), which ICardDatabase.
        // GetCardById can never resolve. DefinitionId is the plain, un-suffixed catalog key
        // GetCardById actually looks up by. See Card.cs's doc comments on both properties.
        public required string DefinitionId { get; set; }

        // Card.RuntimeId - identifies this specific card instance (as opposed to another copy
        // of the same catalog card) within the live match, so a restored Hand/Market/etc. still
        // matches whatever RuntimeId a pending command/UI selection is already holding.
        public Guid RuntimeId { get; set; }

        // Card.Id - the CardFactory.GenerateUniqueId-suffixed instance id (distinct from
        // DefinitionId above). A fresh ICardDatabase.GetCardById call always generates a NEW
        // suffix, so without capturing the original value here, a restored card's Id would
        // silently change on every rollback - either non-deterministically (Guid.NewGuid()) or,
        // worse, by consuming the match's shared IGameRandom stream on routine, often-unrecorded
        // actions like ActionSystem.CancelTargeting(), desyncing replay. See ResolveCard.
        public required string Id { get; set; }

        public CardLocation Location { get; set; }
        public int ListIndex { get; set; } // Order preservation in list

        // Required for deserialization
        public CardDto() { }

        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public CardDto(Card card, int index = 0)
        {
            ArgumentNullException.ThrowIfNull(card);
            DefinitionId = card.DefinitionId;
            Id = card.Id;
            RuntimeId = card.RuntimeId;
            Location = card.Location;
            ListIndex = index;
        }

        public Card ToEntity()
        {
            // Note: ToEntity() usually requires dependencies (like CardDatabase) to look up the definition.
            // Since DTOs are simple data, we might need a factory or an extension method context to hydrate them fully.
            // For now, this returns a shell or throws, implying the manager should handle hydration.
            throw new InvalidOperationException("CardDto requires ICardDatabase to hydrate. Use CardDto.ToEntity(ICardDatabase) instead.");
        }

        public Card ToEntity(ICardDatabase cardDb)
        {
            var card = cardDb.GetCardById(DefinitionId)?.Clone();
            if (card is not null)
            {
                card.Location = Location;
                card.RuntimeId = RuntimeId;
                return card;
            }
            throw new InvalidOperationException($"Failed to hydrate card: {DefinitionId} not found.");
        }
    }
}
