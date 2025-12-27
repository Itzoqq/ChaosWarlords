using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    public class MapNodeTests
    {
        [TestMethod]
        public void AddNeighbor_AddsToNeighborsListReciprocally()
        {
            // Removed null texture arg
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


