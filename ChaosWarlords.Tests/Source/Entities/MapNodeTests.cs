using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    public class MapNodeTests
    {
        [TestMethod]
        public void AddNeighbor_AddsToNeighborsListReciprocally()
        {
            // UPDATED: Removed null texture arg
            var node1 = new MapNode(1, new Vector2(0, 0));
            var node2 = new MapNode(2, new Vector2(0, 0));

            node1.AddNeighbor(node2);

            CollectionAssert.Contains(node1.Neighbors, node2);
            CollectionAssert.Contains(node2.Neighbors, node1, "Neighbor relationship should be reciprocal.");
        }

        [TestMethod]
        public void IsOccupied_ReturnsCorrectState()
        {
            var node = new MapNode(1, new Vector2(0, 0));
            Assert.IsFalse(node.IsOccupied());

            node.Occupant = PlayerColor.Red;
            Assert.IsTrue(node.IsOccupied());
        }
    }
}