using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Lightweight representation of a Card for serialization.
    /// Static data (Cost, Name, Effects) is NOT serialized; only stateful data is.
    /// </summary>
    public class CardDto : IDto<Card>
    {
        public string DefinitionId { get; set; }
        public string InstanceId { get; set; } // Only if we track individual instances uniquely
        public CardLocation Location { get; set; }
        public int ListIndex { get; set; } // Order preservation in list

        // Required for deserialization
        public CardDto() { }

        public CardDto(Card card, int index = 0)
        {
            if (card == null) return;
            DefinitionId = card.Id; 
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
            if (card != null)
            {
                card.Location = Location;
            }
            return card;
        }
    }
}
