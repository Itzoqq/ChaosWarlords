using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Map
{
    public class MapNode
    {
        // Data
        public int Id { get; private set; }
        public Vector2 Position { get; set; }
        public PlayerColor Occupant { get; internal set; } = PlayerColor.None;

        // Navigation
        public List<MapNode> Neighbors { get; private set; } = new List<MapNode>();

        // Logic Constant (Used for Hit-Testing)
        public const int Radius = 20;

        public MapNode(int id, Vector2 position)
        {
            Id = id;
            Position = position;
        }

        public void AddNeighbor(MapNode node)
        {
            if (!Neighbors.Contains(node))
            {
                Neighbors.Add(node);
                node.Neighbors.Add(this);
            }
        }

        public bool IsOccupied()
        {
            return Occupant != PlayerColor.None;
        }
    }
}

