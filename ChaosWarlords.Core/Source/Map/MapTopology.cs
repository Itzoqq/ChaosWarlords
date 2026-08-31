using ChaosWarlords.Source.Core.Data;
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
        /// screenWidth/screenHeight are pixel units; everything else here stays in
        /// LogicVector2's scaled fixed-point space (see LogicVector2.ScaleFactor) so the
        /// centering math is pure deterministic integer arithmetic, not float.
        /// </summary>
        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (_nodes.Count == 0) return;

            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(_nodes);

            var mapCenter = new LogicVector2((MinX + MaxX) / 2, (MinY + MaxY) / 2);
            var screenCenter = new LogicVector2(
                screenWidth / 2 * LogicVector2.ScaleFactor,
                screenHeight / 2 * LogicVector2.ScaleFactor);

            ApplyOffset(screenCenter - mapCenter);
        }

        /// <summary>
        /// Applies a position offset to all nodes and recalculates site bounds.
        /// </summary>
        public void ApplyOffset(LogicVector2 offset)
        {
            foreach (var node in _nodes)
            {
                node.Position += offset;
            }

            if (_sites is not null)
            {
                foreach (var site in _sites)
                {
                    site.RecalculateBounds();
                }
            }
        }

        // Screen-space hit-testing (GetNodeAt/GetSiteAt) deliberately lives outside this
        // class now - see ChaosWarlords/Source/Input/MapHitTestExtensions.cs. It's an
        // Input-layer concern (a headless server never needs it), and the math only needs
        // the already-public Nodes/Sites collections, so no seam was needed here.
    }
}



