using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Utilities
{
    // --- HELPER CLASSES (Data Models) ---
    public class MapData
    {
        public List<NodeData> Nodes { get; set; }
        public List<RouteData> Routes { get; set; }
        public List<SiteData> Sites { get; set; }
    }

    public class NodeData
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Occupant { get; set; }
    }

    public class RouteData
    {
        public int From { get; set; }
        public int To { get; set; }
    }

    public class SiteData
    {
        public string Name { get; set; }
        public bool IsCity { get; set; }
        public List<int> NodeIds { get; set; }
        public string ControlResource { get; set; }
        public int ControlAmount { get; set; }
        public string TotalControlResource { get; set; }
        public int TotalControlAmount { get; set; }
    }

    // --- FACTORY CLASS ---
    public static class MapFactory
    {
        // 1. Main Orchestrator (Low Complexity)
        public static (List<MapNode>, List<Site>) LoadFromFile(string filePath, Texture2D nodeTexture)
        {
            try
            {
                var data = ReadMapData(filePath);

                var nodes = CreateNodes(data.Nodes, nodeTexture);
                CreateRoutes(data.Routes, nodes);
                var sites = CreateSites(data.Sites, nodes);

                return (nodes, sites);
            }
            catch (System.Exception ex)
            {
                return HandleLoadError(ex, nodeTexture);
            }
        }

        // 2. Extracted Logic Methods
        private static MapData ReadMapData(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MapData>(json);
        }

        private static List<MapNode> CreateNodes(List<NodeData> nodeDataList, Texture2D texture)
        {
            var nodes = new List<MapNode>();
            if (nodeDataList == null) return nodes;

            foreach (var n in nodeDataList)
            {
                var newNode = new MapNode(n.Id, new Vector2(n.X, n.Y), texture);

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

                if (nodeA != null && nodeB != null)
                {
                    nodeA.AddNeighbor(nodeB);
                }
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

                // Logic: "Cities" have 'City' in the name
                newSite.IsCity = s.Name.Contains("City");

                foreach (int nodeId in s.NodeIds)
                {
                    var node = nodes.FirstOrDefault(n => n.Id == nodeId);
                    if (node != null) newSite.AddNode(node);
                }
                sites.Add(newSite);
            }
            return sites;
        }

        // 3. Error Handling & Fallbacks
        private static (List<MapNode>, List<Site>) HandleLoadError(System.Exception ex, Texture2D nodeTexture)
        {
            GameLogger.Log(ex);
            GameLogger.Log("Map load failed. Reverting to Test Map.", LogChannel.Error);

            var fallbackNodes = CreateTestMap(nodeTexture);
            var fallbackSites = new List<Site>();

            // Create a basic fallback site so the game doesn't crash on scoring
            var s = new Site("Fallback City", ResourceType.VictoryPoints, 1, ResourceType.VictoryPoints, 2);

            if (fallbackNodes.Count >= 2)
            {
                s.AddNode(fallbackNodes[0]);
                s.AddNode(fallbackNodes[1]);
            }

            fallbackSites.Add(s);

            return (fallbackNodes, fallbackSites);
        }

        public static List<MapNode> CreateTestMap(Texture2D texture)
        {
            var nodes = new List<MapNode>();

            var node1 = new MapNode(1, new Vector2(600, 300), texture);
            var node2 = new MapNode(2, new Vector2(700, 200), texture);
            var node3 = new MapNode(3, new Vector2(800, 300), texture);

            node1.AddNeighbor(node2);
            node2.AddNeighbor(node3);

            nodes.Add(node1);
            nodes.Add(node2);
            nodes.Add(node3);

            return nodes;
        }
    }
}