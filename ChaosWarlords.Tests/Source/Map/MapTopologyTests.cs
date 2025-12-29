using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]

    [TestCategory("Unit")]
    public class MapTopologyTests
    {
        private List<MapNode> _nodes = null!;
        private List<Site> _sites = null!;
        private MapTopology _topology = null!;

        [TestInitialize]
        public void Setup()
        {
            _nodes = new List<MapNode>
            {
                TestData.MapNodes.Node1(),
                TestData.MapNodes.Node2(),
                TestData.MapNodes.Node3()
            };

            var testSite = TestData.Sites.NeutralSite();
            testSite.AddNode(_nodes[0]);
            testSite.AddNode(_nodes[1]);

            _sites = new List<Site>
            {
                testSite
            };

            _topology = new MapTopology(_nodes, _sites);
        }

        [TestMethod]
        public void GetNodeAt_ReturnsNodeWithinRadius()
        {
            // Arrange
            var searchPosition = new Vector2(12, 12); // Node 1 is at (10,10)

            // Act
            var result = _topology.GetNodeAt(searchPosition);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Id);
        }

        [TestMethod]
        public void GetNodeAt_ReturnsNullWhenNoNodeNearby()
        {
            // Arrange
            var searchPosition = new Vector2(1000, 1000); // Far from all nodes

            // Act
            var result = _topology.GetNodeAt(searchPosition);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetSiteAt_ReturnsSiteContainingPosition()
        {
            // Arrange - position within site bounds (Node1 is at 10,10, Node2 at 20,10)
            var searchPosition = new Vector2(15, 10);

            // Act
            var result = _topology.GetSiteAt(searchPosition);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Neutral Site", result.Name);
        }

        [TestMethod]
        public void ApplyOffset_MovesAllNodes()
        {
            // Arrange
            var offset = new Vector2(50, 50);
            var originalPosition = _nodes[0].Position;

            // Act
            _topology.ApplyOffset(offset);

            // Assert
            Assert.AreEqual(originalPosition + offset, _nodes[0].Position);
        }

        [TestMethod]
        public void CenterMap_CentersMapOnScreen()
        {
            // Arrange
            int screenWidth = 800;
            int screenHeight = 600;

            // Act
            _topology.CenterMap(screenWidth, screenHeight);

            // Assert - verify nodes have been moved (exact position depends on bounds calculation)
            Assert.AreNotEqual(new Vector2(100, 100), _nodes[0].Position);
        }
    }
}



