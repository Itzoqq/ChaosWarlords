using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Systems
{
    public interface IMapManager
    {
        IReadOnlyList<MapNode> Nodes { get; }
        IReadOnlyList<Site> Sites { get; }

        void CenterMap(int screenWidth, int screenHeight);

        bool TryDeploy(Player currentPlayer, MapNode targetNode);
        Site GetSiteForNode(MapNode node);
        MapNode GetNodeAt(Microsoft.Xna.Framework.Vector2 position);
        Site GetSiteAt(Microsoft.Xna.Framework.Vector2 position);
        void DistributeControlRewards(Player activePlayer);
        List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer);
    }
}