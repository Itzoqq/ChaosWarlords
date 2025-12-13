using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    public class MapNodeTests
    {
        [TestMethod]
        public void AddNeighbor_AddsToNeighborsListReciprocally()
        {
            var node1 = new MapNode(1, new(0, 0), null);
            var node2 = new MapNode(2, new(0, 0), null);

            node1.AddNeighbor(node2);

            CollectionAssert.Contains(node1.Neighbors, node2);
            CollectionAssert.Contains(node2.Neighbors, node1, "Neighbor relationship should be reciprocal.");
        }

        [TestMethod]
        public void IsOccupied_ReturnsCorrectState()
        {
            var node = new MapNode(1, new(0, 0), null);
            Assert.IsFalse(node.IsOccupied());

            node.Occupant = PlayerColor.Red;
            Assert.IsTrue(node.IsOccupied());
        }
    }
}