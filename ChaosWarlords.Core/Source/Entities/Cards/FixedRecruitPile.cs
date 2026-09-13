using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Cards
{
    /// <summary>
    /// Finite, face-up recruitment supply for a card that is not part of the shuffled market row.
    /// </summary>
    public sealed class FixedRecruitPile
    {
        public string DefinitionId { get; }
        public List<Card> Cards { get; }

        public Card? AvailableCard => Cards.FirstOrDefault();

        public FixedRecruitPile(string definitionId, IEnumerable<Card> cards)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            ArgumentNullException.ThrowIfNull(cards);

            DefinitionId = definitionId;
            Cards = cards.ToList();
            foreach (var card in Cards)
            {
                card.Location = CardLocation.Market;
            }
        }
    }
}
