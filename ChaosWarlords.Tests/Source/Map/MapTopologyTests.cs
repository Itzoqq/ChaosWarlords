using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]
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
                new MapNode(1, new Vector2(100, 100)),
                new MapNode(2, new Vector2(200, 200)),
                new MapNode(3, new Vector2(300, 300))
            };

            var testSite = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
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
            var searchPosition = new Vector2(105, 105); // Close to node 1

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
            // Arrange - position within site bounds
            var searchPosition = new Vector2(150, 150);

            // Act
            var result = _topology.GetSiteAt(searchPosition);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("TestSite", result.Name);
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
