using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class MapNodeDtoTests
    {
        [TestMethod]
        public void Constructor_WithValidNode_PreservesNodeData()
        {
            // Arrange
            var node = new MapNodeBuilder()
                .WithId(5)
                .OccupiedBy(PlayerColor.Blue)
                .Build();
            
            // Act
            var dto = new MapNodeDto(node);
            
            // Assert
            Assert.AreEqual(5, dto.Id);
            Assert.AreEqual(PlayerColor.Blue, dto.Occupant);
        }

        [TestMethod]
        public void Constructor_WithNullNode_HandlesGracefully()
        {
            // Act
            var dto = new MapNodeDto(null!);
            
            // Assert
            Assert.AreEqual(0, dto.Id);
            Assert.AreEqual(PlayerColor.None, dto.Occupant);
        }

        [TestMethod]
        public void ToEntity_ThrowsNotImplementedException()
        {
            // Arrange
            var dto = new MapNodeDto { Id = 1, Occupant = PlayerColor.Red };
            
            // Act & Assert
            try
            {
                dto.ToEntity();
                Assert.Fail("Expected NotImplementedException was not thrown.");
            }
            catch (NotImplementedException)
            {
                // Success
            }
        }

        [TestMethod]
        public void DefaultConstructor_InitializesWithDefaults()
        {
            // Act
            var dto = new MapNodeDto();
            
            // Assert
            Assert.AreEqual(0, dto.Id);
            Assert.AreEqual(PlayerColor.None, dto.Occupant);
        }

        [TestMethod]
        public void SetProperties_PreservesValues()
        {
            // Arrange
            var dto = new MapNodeDto();
            
            // Act
            dto.Id = 42;
            dto.Occupant = PlayerColor.Black;
            
            // Assert
            Assert.AreEqual(42, dto.Id);
            Assert.AreEqual(PlayerColor.Black, dto.Occupant);
        }

        [TestMethod]
        public void Constructor_WithUnoccupiedNode_PreservesNoneOccupant()
        {
            // Arrange
            var node = new MapNodeBuilder()
                .WithId(10)
                .OccupiedBy(PlayerColor.None)
                .Build();
            
            // Act
            var dto = new MapNodeDto(node);
            
            // Assert
            Assert.AreEqual(10, dto.Id);
            Assert.AreEqual(PlayerColor.None, dto.Occupant);
        }

        [TestMethod]
        public void Constructor_WithDifferentColors_PreservesEachColor()
        {
            // Arrange
            var redNode = new MapNodeBuilder().WithId(1).OccupiedBy(PlayerColor.Red).Build();
            var blueNode = new MapNodeBuilder().WithId(2).OccupiedBy(PlayerColor.Blue).Build();
            var blackNode = new MapNodeBuilder().WithId(3).OccupiedBy(PlayerColor.Black).Build();
            var orangeNode = new MapNodeBuilder().WithId(4).OccupiedBy(PlayerColor.Orange).Build();
            
            // Act
            var redDto = new MapNodeDto(redNode);
            var blueDto = new MapNodeDto(blueNode);
            var blackDto = new MapNodeDto(blackNode);
            var orangeDto = new MapNodeDto(orangeNode);
            
            // Assert
            Assert.AreEqual(PlayerColor.Red, redDto.Occupant);
            Assert.AreEqual(PlayerColor.Blue, blueDto.Occupant);
            Assert.AreEqual(PlayerColor.Black, blackDto.Occupant);
            Assert.AreEqual(PlayerColor.Orange, orangeDto.Occupant);
        }
    }
}
