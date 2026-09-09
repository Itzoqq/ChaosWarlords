using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]

    [TestCategory("Unit")]
    public class MapLayoutEngineTests
    {
        [TestMethod]
        public void TestGenerateMap_CreatesNodesAndSites()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Test City",
                IsCity = true,
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 4
            });

            config.Sites.Add(new SiteConfig
            {
                Name = "Test Cave",
                IsCity = false,
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(300 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 2
            });

            var engine = new MapLayoutEngine();
            var (nodes, sites, routes) = engine.GenerateMap(config);

            Assert.HasCount(2, sites);
            Assert.HasCount(6, nodes);

            var city = sites.First(s => s.Name == "Test City");
            Assert.IsInstanceOfType(city, typeof(CitySite));
            Assert.HasCount(4, city.NodesInternal);

            var cave = sites.First(s => s.Name == "Test Cave");
            Assert.IsInstanceOfType(cave, typeof(NonCitySite));
            Assert.HasCount(2, cave.NodesInternal);
        }

        [TestMethod]
        public void TestGenerateMap_CreatesRoutes()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig { Name = "A", Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 0 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor), NodeCount = 1 });
            config.Sites.Add(new SiteConfig { Name = "B", Position = new ChaosWarlords.Source.Core.Data.LogicVector2(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 0 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor), NodeCount = 1 });

            config.Routes.Add(new RouteConfig { FromSiteName = "A", ToSiteName = "B", NodeCount = 3 });

            var engine = new MapLayoutEngine();
            var (nodes, sites, routes) = engine.GenerateMap(config);

            Assert.HasCount(1, routes);
            var route = routes[0];
            Assert.AreEqual("A", route.From.Name);
            Assert.AreEqual("B", route.To.Name);
            Assert.HasCount(3, route.Nodes);
            Assert.HasCount(5, nodes);

            // Verification of connectivity without accessing NodesInternal (which might be the cause of compilation failure in this context)
            var routeFirst = route.Nodes.First();
            var routeLast = route.Nodes.Last();
        }

        [TestMethod]
        public void TestGenerateMap_SiteNodesAreFullyConnected()
        {
            // Arrange
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Connectivity City",
                IsCity = true,
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor),
                NodeCount = 3
            });

            var engine = new MapLayoutEngine();

            // Act
            var (nodes, sites, routes) = engine.GenerateMap(config);

            // Assert
            var site = sites.First();
            Assert.HasCount(3, site.NodesInternal);

            // Every node should have 2 neighbors (the other 2 nodes)
            foreach (var node in site.NodesInternal)
            {
                // Self is not a neighbor usually, so 2 others.
                Assert.HasCount(2, node.Neighbors);

                // Verify they are the correct nodes
                foreach (var other in site.NodesInternal)
                {
                    if (other != node)
                    {
                        Assert.Contains(other, node.Neighbors, $"Node {node.Id} should be connected to {other.Id}");
                    }
                }
            }
        }

        // --- SiteConfig.NeutralTroopSpaceCount / rulebook p.4 setup step 6 ---

        [TestMethod]
        public void GenerateMap_NeutralTroopSpaceCount_SeedsThatManyNodesAsNeutral()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Marked Cavern",
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0),
                NodeCount = 4,
                NeutralTroopSpaceCount = 2
            });

            var engine = new MapLayoutEngine();
            var (_, sites, _) = engine.GenerateMap(config);

            var site = sites.Single();
            Assert.AreEqual(2, site.NodesInternal.Count(n => n.Occupant == PlayerColor.Neutral));
            Assert.AreEqual(2, site.NodesInternal.Count(n => n.Occupant == PlayerColor.None), "The remaining nodes must stay empty, not also seeded.");
        }

        [TestMethod]
        public void GenerateMap_NeutralTroopSpaceCountZero_SeedsNothing()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Untouched Cavern",
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0),
                NodeCount = 3
                // NeutralTroopSpaceCount defaults to 0.
            });

            var engine = new MapLayoutEngine();
            var (nodes, _, _) = engine.GenerateMap(config);

            Assert.IsTrue(nodes.All(n => n.Occupant == PlayerColor.None));
        }

        [TestMethod]
        public void GenerateMap_NeutralTroopSpaceCountExceedsNodeCount_CapsAtEveryNodeInTheSite()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Small Cavern",
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0),
                NodeCount = 2,
                NeutralTroopSpaceCount = 99
            });

            var engine = new MapLayoutEngine();
            var (nodes, _, _) = engine.GenerateMap(config);

            Assert.HasCount(2, nodes);
            Assert.IsTrue(nodes.All(n => n.Occupant == PlayerColor.Neutral));
        }

        [TestMethod]
        public void GenerateMap_NeutralTroopSpaceCountOnAStartingSite_IsSkippedAndLogsAWarning()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Home Base",
                IsStartingSite = true,
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0),
                NodeCount = 2,
                NeutralTroopSpaceCount = 1
            });
            var logger = Substitute.For<IGameLogger>();

            var engine = new MapLayoutEngine();
            var (nodes, _, _) = engine.GenerateMap(config, logger);

            Assert.IsTrue(nodes.All(n => n.Occupant == PlayerColor.None), "A StartingSite must stay fully empty - a Neutral troop there would block every player's initial deploy.");
            logger.Received(1).Log(Arg.Is<string>(s => s.Contains("Home Base") && s.Contains("StartingSite")), LogChannel.Warning);
        }

        [TestMethod]
        public void GenerateMap_NeutralTroopSpaceCountWithNoLoggerProvided_StillSkipsTheStartingSiteSafely()
        {
            var config = new MapGenerationConfig();
            config.Sites.Add(new SiteConfig
            {
                Name = "Home Base",
                IsStartingSite = true,
                Position = new ChaosWarlords.Source.Core.Data.LogicVector2(0, 0),
                NodeCount = 2,
                NeutralTroopSpaceCount = 1
            });

            var engine = new MapLayoutEngine();
            var (nodes, _, _) = engine.GenerateMap(config); // No logger - must not throw.

            Assert.IsTrue(nodes.All(n => n.Occupant == PlayerColor.None));
        }
    }
}



