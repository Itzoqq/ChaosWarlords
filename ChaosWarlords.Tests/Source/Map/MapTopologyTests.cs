using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Data;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]

    [TestCategory("Unit")]
    public class MapTopologyTests
    {
        private List<MapNode> _nodes = null!;
        private List<Site> _sites = null!;
        private MapTopology _topology = null!;

        private static LogicVector2 Scaled(int x, int y) => new(x * LogicVector2.ScaleFactor, y * LogicVector2.ScaleFactor);

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

        // GetNodeAt/GetSiteAt moved out of MapTopology (Core) to
        // ChaosWarlords/Source/Input/MapHitTestExtensions.cs (client Input layer) - see
        // ChaosWarlords.Tests/Source/Input/MapHitTestExtensionsTests.cs for their coverage now.

        [TestMethod]
        public void ApplyOffset_MovesAllNodes()
        {
            // Arrange
            var offset = Scaled(50, 50);
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
            Assert.AreNotEqual(Scaled(100, 100), _nodes[0].Position);
        }
    }
}
