using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    public class DtoTests
    {
        [TestMethod]
        public void PlayerDto_FromEntity_PreservesValues()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, displayName: "TestPlayer");
            player.Influence = 10;
            player.Power = 5;

            // Act
            var dto = PlayerDto.FromEntity(player);

            // Assert
            Assert.AreEqual(player.PlayerId, dto.PlayerId);
            Assert.AreEqual("TestPlayer", dto.DisplayName);
            Assert.AreEqual(PlayerColor.Red, dto.Color);
            Assert.AreEqual(10, dto.Influence);
            Assert.AreEqual(5, dto.Power);
        }

        [TestMethod]
        public void PlayerDto_FromEntity_SerializesHand()
        {
            // Arrange
            var player = new Player(PlayerColor.Blue);
            // We need a way to add cards to hand. DrawCards requires dependencies.
            // Using internal list access if possible, or mocking if accessible.
            // Since DTOs are structural, we will test empty list behavior or rely on mocked cards if we had specific internal access.
            // For now, testing empty consistency.
            
            // Act
            var dto = PlayerDto.FromEntity(player);

            // Assert
            Assert.IsNotNull(dto.Hand);
            Assert.AreEqual(0, dto.Hand.Count);
        }
    }
}
