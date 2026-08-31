using System.Linq;
using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Input
{
    /// <summary>
    /// Screen-space hit-testing ("what entity is at this click position") for the map.
    /// Lives in the client's Input layer, not on IMapManager/MapManager, because a headless
    /// server never needs this - network clients always send resolved node/site IDs directly
    /// (see AssassinateCommand etc.). Operates entirely on IMapManager's already-public
    /// Nodes/Sites collections, so no Core-side seam was needed to move it here. See
    /// planning.txt's architecture backlog for the history.
    /// </summary>
    public static class MapHitTestExtensions
    {
        /// <summary>
        /// Finds the node at the given position (within click radius).
        /// </summary>
        public static MapNode? GetNodeAt(this IMapManager mapManager, LogicVector2 position)
        {
            long radiusScaled = (long)MapNode.Radius * LogicVector2.ScaleFactor;
            return mapManager.Nodes.FirstOrDefault(n =>
                LogicVector2.DistanceSquared(position, n.Position) <= radiusScaled * radiusScaled);
        }

        /// <summary>
        /// Finds the site containing the given position.
        /// </summary>
        public static Site? GetSiteAt(this IMapManager mapManager, LogicVector2 position)
        {
            return mapManager.Sites.FirstOrDefault(s => s.Bounds.Contains(position));
        }
    }
}
