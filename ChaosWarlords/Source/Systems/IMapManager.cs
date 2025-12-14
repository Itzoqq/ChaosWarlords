using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework; // Added for Vector2/int types
using System.Collections.Generic; // Already there

namespace ChaosWarlords.Source.Systems
{
    public interface IMapManager
    {
        IReadOnlyList<MapNode> Nodes { get; }
        IReadOnlyList<Site> Sites { get; }

        void CenterMap(int screenWidth, int screenHeight); // <-- ADD THIS

        bool TryDeploy(Player currentPlayer, MapNode targetNode);
        Site GetSiteForNode(MapNode node);
        MapNode GetNodeAt(Microsoft.Xna.Framework.Vector2 position);
        Site GetSiteAt(Microsoft.Xna.Framework.Vector2 position);
        void DistributeControlRewards(Player activePlayer);
        System.Collections.Generic.List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer);
    }
}