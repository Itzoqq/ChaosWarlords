using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Serialization DTO for Player state.
    /// Includes resources and card collections.
    /// </summary>
    public class PlayerDto : IDto<Player>
    {
        public Guid PlayerId { get; set; }
        public required string DisplayName { get; set; }
        public PlayerColor Color { get; set; }
        public int SeatIndex { get; set; }

        public int Power { get; set; }
        public int Influence { get; set; }
        public int VictoryPoints { get; set; }
        public int Troops { get; set; }
        public int Spies { get; set; }
        public int PendingFreeTroops { get; set; }

        // Trophy hall composition by captured-troop color (string-keyed - matches SiteDto.
        // Spies's own List&lt;string&gt; convention for a PlayerColor collection). The total
        // count (Player.TrophyHall) is a computed property derived from this, so it isn't
        // separately serialized - see StateRestorer.RestoreTrophyHall.
        public Dictionary<string, int> TrophyHallByColor { get; set; } = [];

        public List<CardDto> Hand { get; set; } = [];
        public List<CardDto> InnerCircle { get; set; } = [];
        public List<CardDto> PlayedCards { get; set; } = [];
        public List<CardDto> Deck { get; set; } = [];
        public List<CardDto> DiscardPile { get; set; } = [];

        public PlayerDto() { }

        public static PlayerDto FromEntity(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            var dto = new PlayerDto
            {
                PlayerId = player.PlayerId,
                DisplayName = player.DisplayName,
                Color = player.Color,
                SeatIndex = player.SeatIndex,
                Power = player.Power,
                Influence = player.Influence,
                VictoryPoints = player.VictoryPoints,
                Troops = player.TroopsInBarracks,
                Spies = player.SpiesInBarracks,
                PendingFreeTroops = player.PendingFreeTroops,
                TrophyHallByColor = player.TrophyHallByColor.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                Hand = player.Hand.Select((c, i) => new CardDto(c, i)).ToList(),
                InnerCircle = player.InnerCircle.Select((c, i) => new CardDto(c, i)).ToList(),
                PlayedCards = player.PlayedCards.Select((c, i) => new CardDto(c, i)).ToList(),
                Deck = player.Deck.Select((c, i) => new CardDto(c, i)).ToList(),
                DiscardPile = player.DiscardPile.Select((c, i) => new CardDto(c, i)).ToList()
            };

            return dto;
        }

        public Player ToEntity()
        {
            // Similar to CardDto, hydration requires factories. 
            throw new InvalidOperationException("Use PlayerFactory.FromDto() or similar external hydration.");
        }
    }
}
