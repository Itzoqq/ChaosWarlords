using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;
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
    public class SiteData { public string Name { get; set; } public bool IsCity { get; set; } public List<int> NodeIds { get; set; } public string ControlResource { get; set; } public int ControlAmount { get; set; } public string TotalControlResource { get; set; } public int TotalControlAmount { get; set; } }

    public static class MapFactory
    {
        [ExcludeFromCodeCoverage]
        public static (List<MapNode>, List<Site>) LoadFromFile(string filePath)
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

        internal static (List<MapNode>, List<Site>) LoadFromData(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<MapData>(json, options);

            var nodes = CreateNodes(data.Nodes);
            CreateRoutes(data.Routes, nodes);
            var sites = CreateSites(data.Sites, nodes);

            return (nodes, sites);
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

                var newSite = new Site(s.Name, cType, s.ControlAmount, tType, s.TotalControlAmount);
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

        public static (List<MapNode>, List<Site>) CreateTestMap()
        {
            var nodes = new List<MapNode>();
            var node1 = new MapNode(1, new Vector2(600, 300));
            var node2 = new MapNode(2, new Vector2(700, 200));
            var node3 = new MapNode(3, new Vector2(800, 300));

            node1.AddNeighbor(node2);
            node2.AddNeighbor(node3);

            nodes.Add(node1); nodes.Add(node2); nodes.Add(node3);
            return (nodes, new List<Site>());
        }
    }
}