using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Utilities
{
    public static class MapFactory
    {
        public static List<MapNode> CreateTestMap(Texture2D texture)
        {
            var nodes = new List<MapNode>();

            // Create 3 nodes in a triangle shape (representing a Route between sites)
            var node1 = new MapNode(1, new Vector2(600, 300), texture);
            var node2 = new MapNode(2, new Vector2(700, 200), texture);
            var node3 = new MapNode(3, new Vector2(800, 300), texture);

            // Connect them (Routes are connected paths)
            node1.AddNeighbor(node2);
            node2.AddNeighbor(node3);

            // Let's add a "Neutral" unit to node 2 (The White Troops mentioned in rules)
            node2.Occupant = PlayerColor.Neutral;

            nodes.Add(node1);
            nodes.Add(node2);
            nodes.Add(node3);

            return nodes;
        }
    }
}