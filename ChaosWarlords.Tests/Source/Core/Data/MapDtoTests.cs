using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class MapDtoTests
    {
        [TestMethod]
        public void DefaultConstructor_InitializesNodesList()
        {
            // Act
            var dto = new MapDto();
            
            // Assert
            Assert.IsNotNull(dto.Nodes);
            Assert.AreEqual(0, dto.Nodes.Count);
        }

        [TestMethod]
        public void AddNodes_MaintainsList()
        {
            // Arrange
            var dto = new MapDto();
            var node1 = new MapNodeDto { Id = 1, Occupant = PlayerColor.Red };
            var node2 = new MapNodeDto { Id = 2, Occupant = PlayerColor.Blue };
            
            // Act
            dto.Nodes.Add(node1);
            dto.Nodes.Add(node2);
            
            // Assert
            Assert.AreEqual(2, dto.Nodes.Count);
            Assert.AreEqual(1, dto.Nodes[0].Id);
            Assert.AreEqual(2, dto.Nodes[1].Id);
        }

        [TestMethod]
        public void AddNodes_PreservesOrder()
        {
            // Arrange
            var dto = new MapDto();
            
            // Act
            for (int i = 0; i < 10; i++)
            {
                dto.Nodes.Add(new MapNodeDto { Id = i, Occupant = PlayerColor.None });
            }
            
            // Assert
            Assert.AreEqual(10, dto.Nodes.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i, dto.Nodes[i].Id);
            }
        }

        [TestMethod]
        public void RemoveNodes_UpdatesList()
        {
            // Arrange
            var dto = new MapDto();
            var node1 = new MapNodeDto { Id = 1, Occupant = PlayerColor.Red };
            var node2 = new MapNodeDto { Id = 2, Occupant = PlayerColor.Blue };
            dto.Nodes.Add(node1);
            dto.Nodes.Add(node2);
            
            // Act
            dto.Nodes.Remove(node1);
            
            // Assert
            Assert.AreEqual(1, dto.Nodes.Count);
            Assert.AreEqual(2, dto.Nodes[0].Id);
        }

        [TestMethod]
        public void ClearNodes_EmptiesList()
        {
            // Arrange
            var dto = new MapDto();
            dto.Nodes.Add(new MapNodeDto { Id = 1 });
            dto.Nodes.Add(new MapNodeDto { Id = 2 });
            dto.Nodes.Add(new MapNodeDto { Id = 3 });
            
            // Act
            dto.Nodes.Clear();
            
            // Assert
            Assert.AreEqual(0, dto.Nodes.Count);
        }

        [TestMethod]
        public void AddMultipleNodesWithSameOccupant_MaintainsAll()
        {
            // Arrange
            var dto = new MapDto();
            
            // Act
            dto.Nodes.Add(new MapNodeDto { Id = 1, Occupant = PlayerColor.Red });
            dto.Nodes.Add(new MapNodeDto { Id = 2, Occupant = PlayerColor.Red });
            dto.Nodes.Add(new MapNodeDto { Id = 3, Occupant = PlayerColor.Red });
            
            // Assert
            Assert.AreEqual(3, dto.Nodes.Count);
            Assert.IsTrue(dto.Nodes.All(n => n.Occupant == PlayerColor.Red));
        }
    }
}
