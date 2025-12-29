using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    [TestCategory("Unit")]
    public class MapNodeTests
    {
        [TestMethod]
        public void AddNeighbor_AddsToNeighborsListReciprocally()
        {
            // Removed null texture arg
            var node1 = TestData.MapNodes.Node1();
            var node2 = TestData.MapNodes.Node2();

            node1.AddNeighbor(node2);

            CollectionAssert.Contains(node1.Neighbors, node2);
            CollectionAssert.Contains(node2.Neighbors, node1, "Neighbor relationship should be reciprocal.");
        }

        [TestMethod]
        public void IsOccupied_ReturnsCorrectState()
        {
            var node = TestData.MapNodes.Node1();
            Assert.IsFalse(node.IsOccupied());

            node.Occupant = PlayerColor.Red;
            Assert.IsTrue(node.IsOccupied());
        }
    }
}


