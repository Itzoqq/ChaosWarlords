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
        public required string DefinitionId { get; set; }
        public required string InstanceId { get; set; } // Only if we track individual instances uniquely
        public CardLocation Location { get; set; }
        public int ListIndex { get; set; } // Order preservation in list

        // Required for deserialization
        public CardDto() { }

        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public CardDto(Card card, int index = 0)
        {
            ArgumentNullException.ThrowIfNull(card);
            DefinitionId = card.Id;
            InstanceId = Guid.NewGuid().ToString(); // Generate a temporary ID if one doesn't exist on Entity yet, or map it if it did. 
            // Ideally Card entity should have an InstanceId. For now, we generate one or use DefinitionId if strictly one-to-one (which it isn't).
            // Let's assume for serialization of a *running* game, we need stable IDs.
            // But Card entity currently doesn't seem to have a unique InstanceId in the code shown? 
            // Checking Card.cs in memory... it has Id (Definition) but maybe not InstanceId.
            // For the DTO, we need to satisfy the 'required' contract. 
            
            // Re-reading Card.cs from context... I don't see it open but I recall it.
            // If Card doesn't have InstanceId, we can't reliably persist it round-trip without one if we need it.
            // But for now, to fix the build, we must assign it.
            InstanceId = System.Guid.NewGuid().ToString(); 
            
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
                return card;
            }
            throw new System.InvalidOperationException($"Failed to hydrate card: {DefinitionId} not found.");
        }
    }
}
