using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Map
{
    /// <summary>
    /// Handles spatial queries and map layout operations.
    /// Extracted from MapManager to follow Single Responsibility Principle.
    /// </summary>
    public class MapTopology
    {
        private readonly List<MapNode> _nodes;
        private readonly List<Site> _sites;

        public MapTopology(List<MapNode> nodes, List<Site> sites)
        {
            _nodes = nodes;
            _sites = sites;
        }

        /// <summary>
        /// Centers the map on screen by calculating bounds and applying offset.
        /// </summary>
        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (_nodes.Count == 0) return;

            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(_nodes);
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            ApplyOffset(screenCenter - mapCenter);
        }

        /// <summary>
        /// Applies a position offset to all nodes and recalculates site bounds.
        /// </summary>
        public void ApplyOffset(Vector2 offset)
        {
            foreach (var node in _nodes)
            {
                node.Position += offset;
            }

            if (_sites != null)
            {
                foreach (var site in _sites)
                {
                    site.RecalculateBounds();
                }
            }
        }

        /// <summary>
        /// Finds the node at the given screen position (within click radius).
        /// </summary>
        public MapNode? GetNodeAt(Vector2 position)
        {
            return _nodes.FirstOrDefault(n => Vector2.Distance(position, n.Position) <= MapNode.Radius);
        }

        /// <summary>
        /// Finds the site containing the given screen position.
        /// </summary>
        public Site? GetSiteAt(Vector2 position)
        {
            return _sites?.FirstOrDefault(s => s.Bounds.Contains(position));
        }
    }
}



