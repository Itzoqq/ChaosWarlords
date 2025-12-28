using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Serialization DTO for Player state.
    /// Includes resources and card collections.
    /// </summary>
    public class PlayerDto : IDto<Player>
    {
        public Guid PlayerId { get; set; }
        public string DisplayName { get; set; }
        public PlayerColor Color { get; set; }

        public int Power { get; set; }
        public int Influence { get; set; }
        public int VictoryPoints { get; set; }
        public int Troops { get; set; }
        public int Spies { get; set; }

        public List<CardDto> Hand { get; set; } = new List<CardDto>();
        public List<CardDto> InnerCircle { get; set; } = new List<CardDto>();
        public List<CardDto> PlayedCards { get; set; } = new List<CardDto>();
        public List<CardDto> Deck { get; set; } = new List<CardDto>();
        public List<CardDto> DiscardPile { get; set; } = new List<CardDto>();

        public PlayerDto() { }

        public static PlayerDto FromEntity(Player player)
        {
            if (player == null) return null;

            var dto = new PlayerDto
            {
                PlayerId = player.PlayerId,
                DisplayName = player.DisplayName,
                Color = player.Color,
                Power = player.Power,
                Influence = player.Influence,
                VictoryPoints = player.VictoryPoints,
                Troops = player.TroopsInBarracks,
                Spies = player.SpiesInBarracks,
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
