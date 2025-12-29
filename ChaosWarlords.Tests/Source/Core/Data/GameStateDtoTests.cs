using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data.Dtos;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class GameStateDtoTests
    {
        [TestMethod]
        public void DefaultConstructor_InitializesCollections()
        {
            // Act
            var dto = new GameStateDto();
            
            // Assert
            Assert.IsNotNull(dto.Players);
            Assert.IsNotNull(dto.Market);
            Assert.IsNotNull(dto.VoidPile);
            Assert.IsNotNull(dto.Map);
            Assert.AreEqual(0, dto.Players.Count);
            Assert.AreEqual(0, dto.Market.Count);
            Assert.AreEqual(0, dto.VoidPile.Count);
        }

        [TestMethod]
        public void SetProperties_PreservesValues()
        {
            // Arrange
            var dto = new GameStateDto();
            
            // Act
            dto.Seed = 12345;
            dto.TurnNumber = 10;
            dto.Phase = MatchPhase.Playing;
            
            // Assert
            Assert.AreEqual(12345, dto.Seed);
            Assert.AreEqual(10, dto.TurnNumber);
            Assert.AreEqual(MatchPhase.Playing, dto.Phase);
        }

        [TestMethod]
        public void AddPlayers_MaintainsList()
        {
            // Arrange
            var dto = new GameStateDto();
            var player1 = new PlayerDto { PlayerId = Guid.NewGuid(), DisplayName = "Player1" };
            var player2 = new PlayerDto { PlayerId = Guid.NewGuid(), DisplayName = "Player2" };
            
            // Act
            dto.Players.Add(player1);
            dto.Players.Add(player2);
            
            // Assert
            Assert.AreEqual(2, dto.Players.Count);
            Assert.AreEqual("Player1", dto.Players[0].DisplayName);
            Assert.AreEqual("Player2", dto.Players[1].DisplayName);
        }

        [TestMethod]
        public void AddMarketCards_MaintainsList()
        {
            // Arrange
            var dto = new GameStateDto();
            var card1 = new CardDto { DefinitionId = "card1", InstanceId = Guid.NewGuid().ToString() };
            var card2 = new CardDto { DefinitionId = "card2", InstanceId = Guid.NewGuid().ToString() };
            
            // Act
            dto.Market.Add(card1);
            dto.Market.Add(card2);
            
            // Assert
            Assert.AreEqual(2, dto.Market.Count);
            Assert.AreEqual("card1", dto.Market[0].DefinitionId);
            Assert.AreEqual("card2", dto.Market[1].DefinitionId);
        }

        [TestMethod]
        public void AddVoidPileCards_MaintainsList()
        {
            // Arrange
            var dto = new GameStateDto();
            var card = new CardDto { DefinitionId = "void_card", InstanceId = Guid.NewGuid().ToString() };
            
            // Act
            dto.VoidPile.Add(card);
            
            // Assert
            Assert.AreEqual(1, dto.VoidPile.Count);
            Assert.AreEqual("void_card", dto.VoidPile[0].DefinitionId);
        }

        [TestMethod]
        public void SetMap_PreservesMapReference()
        {
            // Arrange
            var dto = new GameStateDto();
            var mapDto = new MapDto();
            mapDto.Nodes.Add(new MapNodeDto { Id = 1 });
            
            // Act
            dto.Map = mapDto;
            
            // Assert
            Assert.AreSame(mapDto, dto.Map);
            Assert.AreEqual(1, dto.Map.Nodes.Count);
        }

        [TestMethod]
        public void DefaultConstructor_SetsDefaultPhase()
        {
            // Act
            var dto = new GameStateDto();
            
            // Assert
            Assert.AreEqual(default(MatchPhase), dto.Phase);
        }

        [TestMethod]
        public void SetSeed_WithNegativeValue_PreservesValue()
        {
            // Arrange
            var dto = new GameStateDto();
            
            // Act
            dto.Seed = -999;
            
            // Assert
            Assert.AreEqual(-999, dto.Seed);
        }
    }
}
