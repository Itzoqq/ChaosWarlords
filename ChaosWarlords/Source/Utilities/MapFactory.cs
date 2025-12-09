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
    // Placing them here makes them visible but keeps the file organized.
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
    }

    public class RouteData
    {
        public int From { get; set; }
        public int To { get; set; }
    }

    public class SiteData
    {
        public string Name { get; set; }
        public List<int> NodeIds { get; set; }
        public string ControlResource { get; set; } // Read as string -> Enum
        public int ControlAmount { get; set; }
        public string TotalControlResource { get; set; } // Read as string -> Enum
        public int TotalControlAmount { get; set; }
    }

    // --- FACTORY CLASS ---
    public static class MapFactory
    {
        // 1. JSON Loader
        public static (List<MapNode>, List<Site>) LoadFromFile(string filePath, Texture2D nodeTexture)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<MapData>(json);

                var nodes = new List<MapNode>();
                var sites = new List<Site>();

                // A. Create Nodes
                foreach (var n in data.Nodes)
                {
                    nodes.Add(new MapNode(n.Id, new Vector2(n.X, n.Y), nodeTexture));
                }

                // B. Create Routes (Connections)
                foreach (var r in data.Routes)
                {
                    var nodeA = nodes.FirstOrDefault(n => n.Id == r.From);
                    var nodeB = nodes.FirstOrDefault(n => n.Id == r.To);
                    if (nodeA != null && nodeB != null)
                    {
                        nodeA.AddNeighbor(nodeB);
                    }
                }

                // C. Create Sites
                if (data.Sites != null)
                {
                    foreach (var s in data.Sites)
                    {
                        // Parse Enums (Default to VP if missing/invalid)
                        System.Enum.TryParse(s.ControlResource, out ResourceType cType);
                        System.Enum.TryParse(s.TotalControlResource, out ResourceType tType);

                        var newSite = new Site(s.Name, cType, s.ControlAmount, tType, s.TotalControlAmount);

                        foreach (int nodeId in s.NodeIds)
                        {
                            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
                            if (node != null) newSite.AddNode(node);
                        }
                        sites.Add(newSite);
                    }
                }

                return (nodes, sites);
            }
            catch (System.Exception ex)
            {
                // If loading fails, log it and return the test map
                GameLogger.Log(ex);
                GameLogger.Log("Map load failed. Reverting to Test Map.", LogChannel.Error);

                var fallbackNodes = CreateTestMap(nodeTexture);
                var fallbackSites = new List<Site>();
                // Create a basic fallback site so the game doesn't crash on scoring
                var s = new Site("Fallback City", ResourceType.VictoryPoints, 1, ResourceType.VictoryPoints, 2);
                if (fallbackNodes.Count >= 2) { s.AddNode(fallbackNodes[0]); s.AddNode(fallbackNodes[1]); }
                fallbackSites.Add(s);

                return (fallbackNodes, fallbackSites);
            }
        }

        // 2. Hardcoded Test Map (Fallback)
        public static List<MapNode> CreateTestMap(Texture2D texture)
        {
            var nodes = new List<MapNode>();

            var node1 = new MapNode(1, new Vector2(600, 300), texture);
            var node2 = new MapNode(2, new Vector2(700, 200), texture);
            var node3 = new MapNode(3, new Vector2(800, 300), texture);

            node1.AddNeighbor(node2);
            node2.AddNeighbor(node3);

            // Note: Commented out the blocker so you can test control
            //node2.Occupant = PlayerColor.Neutral;

            nodes.Add(node1);
            nodes.Add(node2);
            nodes.Add(node3);

            return nodes;
        }
    }
}