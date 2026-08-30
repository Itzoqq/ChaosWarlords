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
        /// The interpolated/rendered position of the node (cached for rendering), in the
        /// same scaled fixed-point space as <see cref="LogicPosition"/> - see
        /// <see cref="Core.Data.LogicVector2.ScaleFactor"/>. Can be shifted by
        /// MapTopology.ApplyOffset for screen centering; decoupled from LogicPosition so
        /// that shift never affects deterministic logic. Converted to pixel-space Vector2
        /// only at the point of rendering (Source/Rendering/LogicVectorExtensions.cs).
        /// </summary>
        public Core.Data.LogicVector2 Position { get; internal set; }

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
            Position = logicPosition;
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

