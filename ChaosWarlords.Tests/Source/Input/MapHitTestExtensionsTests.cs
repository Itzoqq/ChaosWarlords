using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Input;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Input
{
    [TestClass]
    [TestCategory("Unit")]
    public class MapHitTestExtensionsTests
    {
        private static LogicVector2 Scaled(int x, int y) => new(x * LogicVector2.ScaleFactor, y * LogicVector2.ScaleFactor);

        private static IMapManager BuildMapManager(List<MapNode> nodes, List<Site> sites)
        {
            var mapManager = Substitute.For<IMapManager>();
            mapManager.Nodes.Returns(nodes);
            mapManager.Sites.Returns(sites);
            return mapManager;
        }

        [TestMethod]
        public void GetNodeAt_ReturnsNodeWithinRadius()
        {
            // Arrange
            var node1 = TestData.MapNodes.Node1(); // At (10,10)
            var mapManager = BuildMapManager(new List<MapNode> { node1 }, new List<Site>());
            var searchPosition = Scaled(12, 12);

            // Act
            var result = mapManager.GetNodeAt(searchPosition);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Id);
        }

        [TestMethod]
        public void GetNodeAt_ReturnsNullWhenNoNodeNearby()
        {
            // Arrange
            var node1 = TestData.MapNodes.Node1();
            var mapManager = BuildMapManager(new List<MapNode> { node1 }, new List<Site>());
            var searchPosition = Scaled(1000, 1000); // Far from all nodes

            // Act
            var result = mapManager.GetNodeAt(searchPosition);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetNodeAt_ReturnsCorrectNode_ExactBoundary()
        {
            // Arrange
            var node1 = TestData.MapNodes.Node1();
            node1.Position = new LogicVector2(100 * LogicVector2.ScaleFactor, 100 * LogicVector2.ScaleFactor);
            var mapManager = BuildMapManager(new List<MapNode> { node1 }, new List<Site>());
            var insidePoint = new LogicVector2(105 * LogicVector2.ScaleFactor, 105 * LogicVector2.ScaleFactor);
            var outsidePoint = new LogicVector2(200 * LogicVector2.ScaleFactor, 200 * LogicVector2.ScaleFactor);

            // Act & Assert
            Assert.AreSame(node1, mapManager.GetNodeAt(insidePoint));
            Assert.IsNull(mapManager.GetNodeAt(outsidePoint));
        }

        [TestMethod]
        public void GetSiteAt_ReturnsSiteContainingPosition()
        {
            // Arrange - position within site bounds (Node1 is at 10,10, Node2 at 20,10)
            var node1 = TestData.MapNodes.Node1();
            var node2 = TestData.MapNodes.Node2();
            var site = TestData.Sites.NeutralSite();
            site.AddNode(node1);
            site.AddNode(node2);
            var mapManager = BuildMapManager(new List<MapNode> { node1, node2 }, new List<Site> { site });
            var searchPosition = Scaled(15, 10);

            // Act
            var result = mapManager.GetSiteAt(searchPosition);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Neutral Site", result.Name);
        }

        [TestMethod]
        public void GetSiteAt_ReturnsNullWhenNoSiteContainsPosition()
        {
            // Arrange
            var node1 = TestData.MapNodes.Node1();
            var node2 = TestData.MapNodes.Node2();
            var site = TestData.Sites.NeutralSite();
            site.AddNode(node1);
            site.AddNode(node2);
            var mapManager = BuildMapManager(new List<MapNode> { node1, node2 }, new List<Site> { site });
            var searchPosition = Scaled(1000, 1000);

            // Act
            var result = mapManager.GetSiteAt(searchPosition);

            // Assert
            Assert.IsNull(result);
        }
    }
}
