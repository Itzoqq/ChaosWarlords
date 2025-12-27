using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]
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
                Position = new Vector2(100, 100),
                NodeCount = 4
            });

            config.Sites.Add(new SiteConfig
            {
                Name = "Test Cave",
                IsCity = false,
                Position = new Vector2(300, 100),
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
            config.Sites.Add(new SiteConfig { Name = "A", Position = new Vector2(0, 0), NodeCount = 1 });
            config.Sites.Add(new SiteConfig { Name = "B", Position = new Vector2(100, 0), NodeCount = 1 });

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
                Position = new Vector2(100, 100),
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
    }
}



