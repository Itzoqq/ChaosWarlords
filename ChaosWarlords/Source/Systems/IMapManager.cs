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

        // Deployment & Checking
        bool TryDeploy(Player currentPlayer, MapNode targetNode);
        bool CanDeployAt(MapNode targetNode, PlayerColor player); // Added based on typical usage, though not in your current error list, it's often paired with TryDeploy.
        bool HasPresence(MapNode targetNode, PlayerColor player);

        // Navigation / Queries
        Site GetSiteForNode(MapNode node);
        MapNode GetNodeAt(Microsoft.Xna.Framework.Vector2 position);
        Site GetSiteAt(Microsoft.Xna.Framework.Vector2 position);
        List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer);

        // Actions
        bool CanAssassinate(MapNode target, Player attacker);
        void Assassinate(MapNode node, Player attacker);
        void Supplant(MapNode node, Player attacker);
        void ReturnTroop(MapNode node, Player requestingPlayer);

        // Spy Actions
        void PlaceSpy(Site site, Player player);
        bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor);
        // Note: You might also want 'bool ReturnSpy(Site site, Player activePlayer);' if you use the auto-return logic elsewhere, but the errors specifically asked for ReturnSpecificSpy.

        // Game State / Rewards
        void DistributeControlRewards(Player activePlayer);
        void RecalculateSiteState(Site site, Player activePlayer); // Often needed for consistency
    }
}