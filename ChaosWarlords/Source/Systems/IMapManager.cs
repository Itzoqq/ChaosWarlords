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
        bool CanDeployAt(MapNode targetNode, PlayerColor player);
        bool HasPresence(MapNode targetNode, PlayerColor player);

        // --- NEW: Deadlock Prevention Checks ---
        // These check if a valid target exists AND is reachable by the player
        bool HasValidAssassinationTarget(Player activePlayer);
        bool HasValidReturnSpyTarget(Player activePlayer);
        bool HasValidPlaceSpyTarget(Player activePlayer);
        // ---------------------------------------

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

        // Game State / Rewards
        void DistributeControlRewards(Player activePlayer);
        void RecalculateSiteState(Site site, Player activePlayer);
    }
}