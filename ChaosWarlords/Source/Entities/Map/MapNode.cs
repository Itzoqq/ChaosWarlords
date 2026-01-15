using Microsoft.Xna.Framework;
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

        /// <summary>
        /// The deterministic logic position of the node.
        /// </summary>
        public Core.Data.LogicVector2 LogicPosition { get; private set; }

        /// <summary>
        /// The interpolated/rendered position of the node (cached for rendering).
        /// Can be modified by MapTopology for centering (Screen Space).
        /// Decoupled from LogicPosition to ensure Logic remains deterministic 0,0 based or World based.
        /// </summary>
        public Vector2 Position { get; internal set; }

        /// <summary>
        /// The player currently occupying this node with troops.
        /// </summary>
        public PlayerColor Occupant { get; internal set; } = PlayerColor.None;

        // Navigation
        /// <summary>
        /// List of adjacent nodes directly connected to this one.
        /// </summary>
        public List<MapNode> Neighbors { get; private set; } = [];

        // Logic Constant (Used for Hit-Testing)
        public const int Radius = 20;

        public MapNode(int id, Core.Data.LogicVector2 logicPosition)
        {
            Id = id;
            LogicPosition = logicPosition;
            Position = logicPosition.ToVector2();
        }

        public MapNode(int id, Vector2 position)
        {
            Id = id;
            LogicPosition = Core.Data.LogicVector2.FromVector2(position);
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

