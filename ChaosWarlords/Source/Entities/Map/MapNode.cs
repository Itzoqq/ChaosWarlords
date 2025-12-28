using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Map
{
    /// <summary>
    /// Represents a discrete location on the game map (a "node" in the graph).
    /// Can contain troops, spies, and connect to other nodes.
    /// </summary>
    public class MapNode
    {
        // Data
        public int Id { get; private set; }
        public Vector2 Position { get; set; }
        
        /// <summary>
        /// The player currently occupying this node with troops.
        /// </summary>
        public PlayerColor Occupant { get; internal set; } = PlayerColor.None;

        // Navigation
        /// <summary>
        /// List of adjacent nodes directly connected to this one.
        /// </summary>
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

