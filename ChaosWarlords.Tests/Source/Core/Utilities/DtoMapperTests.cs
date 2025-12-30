using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using System.Collections.Generic;
using System;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    [TestClass]
    [TestCategory("Unit")]
    public class DtoMapperTests
    {
        [TestMethod]
        public void ToDto_Card_ReturnsCorrectDto()
        {
            // Arrange
            var card = new Card("id_1", "Test Card", 5, CardAspect.Warlord, 1, 1, 1);
            card.Location = CardLocation.Hand;

            // Act
            var dto = DtoMapper.ToDto(card, 2);

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual("id_1", dto.DefinitionId);
            Assert.AreEqual(CardLocation.Hand, dto.Location);
            Assert.AreEqual(2, dto.ListIndex);
        }

        [TestMethod]
        public void ToDto_Player_ReturnsCorrectDto()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, Guid.NewGuid(), "Red Player");
            player.Power = 10;
            player.Hand.Add(new Card("c1", "C1", 1, CardAspect.Neutral, 0, 0, 0));

            // Act
            var dto = DtoMapper.ToDto(player);

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual(player.PlayerId, dto.PlayerId);
            Assert.AreEqual(10, dto.Power);
            Assert.HasCount(1, dto.Hand);
            Assert.AreEqual("c1", dto.Hand[0].DefinitionId);
            Assert.AreEqual(0, dto.Hand[0].ListIndex);
        }

        [TestMethod]
        public void ToDto_PlayCardCommand_ReturnsCorrectDto()
        {
            // Arrange
            var player = new Player(PlayerColor.Blue);
            var card = new Card("c_fireball", "Fireball", 3, CardAspect.Sorcery, 0, 0, 0);
            player.Hand.Add(card);

            var command = new PlayCardCommand(card);

            // Act
            var dto = DtoMapper.ToDto(command, 42, player);

            // Assert
            Assert.IsNotNull(dto);
            Assert.IsInstanceOfType(dto, typeof(PlayCardCommandDto));
            var playDto = (PlayCardCommandDto)dto;
            Assert.AreEqual(42, playDto.Seq);
            Assert.AreEqual(player.SeatIndex, playDto.Seat);
            Assert.AreEqual("c_fireball", playDto.CardId);
            Assert.AreEqual(0, playDto.HandIdx);
        }

        [TestMethod]
        public void HydrateCommand_PlayCard_ReturnsCorrectCommand()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, Guid.NewGuid());
            player.SeatIndex = 0;
            var card = new Card("c_bolt", "Bolt", 2, CardAspect.Sorcery, 0, 0, 0);
            player.Hand.Add(card);

            var dto = new PlayCardCommandDto
            {
                Seat = 0,
                HandIdx = 0,
                CardId = "c_bolt"
            };

            var state = Substitute.For<IGameplayState>();
            var turnManager = Substitute.For<ITurnManager>();

            // Setup mocking infrastructure
            turnManager.Players.Returns(new List<Player> { player });
            state.TurnManager.Returns(turnManager);

            // Act
            var resultCommand = DtoMapper.HydrateCommand(dto, state);

            // Assert
            Assert.IsInstanceOfType(resultCommand, typeof(PlayCardCommand));
            var playCmd = resultCommand as PlayCardCommand;
            Assert.IsNotNull(playCmd);
            Assert.AreEqual(card.Id, playCmd.Card.Id);
            Assert.AreEqual(card.Name, playCmd.Card.Name);
        }
    }
}
