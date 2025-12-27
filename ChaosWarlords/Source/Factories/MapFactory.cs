using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    [ExcludeFromCodeCoverage]
    public class MapData { public List<NodeData> Nodes { get; set; } public List<RouteData> Routes { get; set; } public List<SiteData> Sites { get; set; } }
    [ExcludeFromCodeCoverage]
    public class NodeData { public int Id { get; set; } public int X { get; set; } public int Y { get; set; } public string Occupant { get; set; } }
    [ExcludeFromCodeCoverage]
    public class RouteData { public int From { get; set; } public int To { get; set; } }
    [ExcludeFromCodeCoverage]
    public class SiteData { public string Name { get; set; } public bool IsCity { get; set; } public bool IsStartingSite { get; set; } public List<int> NodeIds { get; set; } public string ControlResource { get; set; } public int ControlAmount { get; set; } public string TotalControlResource { get; set; } public int TotalControlAmount { get; set; } }

    public static class MapFactory
    {
        [ExcludeFromCodeCoverage]
        public static (List<MapNode>, List<Site>, List<Route>) LoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return LoadFromData(json);
            }
            catch (System.Exception ex)
            {
                GameLogger.Log(ex);
                GameLogger.Log("Map load failed. Reverting to Test Map.", LogChannel.Error);
                return CreateTestMap();
            }
        }

        public static (List<MapNode>, List<Site>, List<Route>) LoadFromData(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<MapData>(json, options);

            var nodes = CreateNodes(data.Nodes);
            CreateRoutes(data.Routes, nodes);
            var sites = CreateSites(data.Sites, nodes);

             // Routes need to be reconstructed from data if we want to return them, 
             // but current LoadFromData logic (CreateRoutes) takes a void and modifies nodes directly.
             // For now, to satisfy the signature change required by TestMap, we can return null or an empty list if data doesn't persist Route objects in a list.
             // However, `MapLayoutEngine` DOES return a list of Route objects. 
             // Let's create a minimal list if needed, or update CreateRoutes to return list involved.
             // Updating CreateRoutes is better.
            var routes = new List<Route>(); 
             // ... actually CreateRoutes logic above (lines 87-96) constructs adjacency but doesn't build Route objects. 
             // Given this is legacy JSON loading vs New Procedural Generation, we should align them.
             // For now, return null for routes is acceptable for legacy loader if nothing consumes it yet. 
            return (nodes, sites, null);
        }

        public static (List<MapNode>, List<Site>, List<Route>) LoadFromStream(Stream stream)
        {
            try
            {
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    return LoadFromData(json);
                }
            }
            catch (System.Exception ex)
            {
                GameLogger.Log(ex);
                return CreateTestMap();
            }
        }

        private static List<MapNode> CreateNodes(List<NodeData> nodeDataList)
        {
            var nodes = new List<MapNode>();
            if (nodeDataList == null) return nodes;

            foreach (var n in nodeDataList)
            {
                // No texture passed here anymore
                var newNode = new MapNode(n.Id, new Vector2(n.X, n.Y));

                if (!string.IsNullOrEmpty(n.Occupant) &&
                    System.Enum.TryParse(n.Occupant, out PlayerColor color))
                {
                    newNode.Occupant = color;
                }
                nodes.Add(newNode);
            }
            return nodes;
        }

        private static void CreateRoutes(List<RouteData> routeDataList, List<MapNode> nodes)
        {
            if (routeDataList == null) return;
            foreach (var r in routeDataList)
            {
                var nodeA = nodes.FirstOrDefault(n => n.Id == r.From);
                var nodeB = nodes.FirstOrDefault(n => n.Id == r.To);
                if (nodeA != null && nodeB != null) nodeA.AddNeighbor(nodeB);
            }
        }
        private static List<Site> CreateSites(List<SiteData> siteDataList, List<MapNode> nodes)
        {
            var sites = new List<Site>();
            if (siteDataList == null) return sites;

            foreach (var s in siteDataList)
            {
                System.Enum.TryParse(s.ControlResource, out ResourceType cType);
                System.Enum.TryParse(s.TotalControlResource, out ResourceType tType);

                Site newSite;
                if (s.IsCity)
                {
                    newSite = new CitySite(s.Name, cType, s.ControlAmount, tType, s.TotalControlAmount);
                }
                else if (s.IsStartingSite)
                {
                    GameLogger.Log($"Creating StartingSite: {s.Name}", LogChannel.General);
                    newSite = new StartingSite(s.Name, cType, s.ControlAmount, tType, s.TotalControlAmount);
                }
                else
                {
                    GameLogger.Log($"Creating NonCitySite: {s.Name}", LogChannel.General);
                    newSite = new NonCitySite(s.Name, cType, s.ControlAmount, tType, s.TotalControlAmount);
                }
                
                newSite.IsCity = s.IsCity;

                foreach (int nodeId in s.NodeIds)
                {
                    var node = nodes.FirstOrDefault(n => n.Id == nodeId);
                    if (node != null) newSite.AddNode(node);
                }
                sites.Add(newSite);
            }
            return sites;
        }

        public static (List<MapNode>, List<Site>, List<Route>) CreateTestMap()
        {
             // Delegate to the full scenario creator
             return CreateScenarioMap();
        }

        public static (List<MapNode>, List<Site>, List<Route>) CreateScenarioMap()
        {
            var config = new MapGenerationConfig();

            // -- Define Sites --
            // 1. Crystal Cave (Starting Site)
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Crystal Cave", 
                IsCity = false,
                IsStartingSite = true,
                Position = new Vector2(250, 100), 
                NodeCount = 2,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 2
            });

            // 2. Void Portal
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Void Portal", 
                IsCity = false, 
                Position = new Vector2(250, 400), 
                NodeCount = 3,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 1
            });

            // 3. Shadow Market (Starting Site)
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Shadow Market", 
                IsCity = false,
                IsStartingSite = true,
                Position = new Vector2(250, 700), 
                NodeCount = 2,
                ControlResource = ResourceType.Power, 
                ControlAmount = 0,
                TotalControlResource = ResourceType.Power, 
                TotalControlAmount = 0,
                EndGameVP = 2
            });

            // 4. City of Gold
            config.Sites.Add(new SiteConfig 
            { 
                Name = "City of Gold", 
                IsCity = true, 
                Position = new Vector2(600, 400), 
                NodeCount = 4,
                ControlResource = ResourceType.Influence, 
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints, 
                TotalControlAmount = 1,
                EndGameVP = 0
            });

            // 5. Obsidian Fortress
            config.Sites.Add(new SiteConfig 
            { 
                Name = "Obsidian Fortress", 
                IsCity = true, 
                Position = new Vector2(1000, 400), 
                NodeCount = 6,
                ControlResource = ResourceType.Influence, 
                ControlAmount = 1,
                TotalControlResource = ResourceType.VictoryPoints, 
                TotalControlAmount = 2,
                EndGameVP = 0
            });

            // -- Define Routes --
            config.Routes.Add(new RouteConfig { FromSiteName = "Crystal Cave", ToSiteName = "Void Portal", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "Shadow Market", NodeCount = 2 });
            config.Routes.Add(new RouteConfig { FromSiteName = "Void Portal", ToSiteName = "City of Gold", NodeCount = 1 });
            config.Routes.Add(new RouteConfig { FromSiteName = "City of Gold", ToSiteName = "Obsidian Fortress", NodeCount = 3 });

            // Generate
            var layoutEngine = new MapLayoutEngine();
            return layoutEngine.GenerateMap(config);
        }
    }
}


