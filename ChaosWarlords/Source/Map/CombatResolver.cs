using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Map
{
    /// <summary>
    /// Handles combat-related map operations: deployment, assassination, troop movement.
    /// Extracted from MapManager to follow Single Responsibility Principle.
    /// </summary>
    public class CombatResolver
    {
        private readonly Func<MapNode, Site> _getSiteForNode;
        private readonly Action<Site, Player> _recalculateSiteState;
        private readonly Func<MatchPhase> _getCurrentPhase;
        private IPlayerStateManager _stateManager;

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public CombatResolver(
            Func<MapNode, Site> getSiteForNode,
            Action<Site, Player> recalculateSiteState,
            Func<MatchPhase> getCurrentPhase,
            IPlayerStateManager stateManager)
        {
            _getSiteForNode = getSiteForNode;
            _recalculateSiteState = recalculateSiteState;
            _getCurrentPhase = getCurrentPhase;
            _stateManager = stateManager;
        }

        /// <summary>
        /// Deploys a troop to the target node. Handles resource costs and validation.
        /// </summary>
        public void ExecuteDeploy(MapNode node, Player player)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (player == null) throw new ArgumentNullException(nameof(player));

            // FREE in Setup Phase
            if (_getCurrentPhase() != MatchPhase.Setup)
            {
                _stateManager.TrySpendPower(player, GameConstants.DEPLOY_POWER_COST);
            }

            _stateManager.RemoveTroops(player, 1);
            node.Occupant = player.Color;

            GameLogger.Log($"Deployed Troop at Node {node.Id}. Supply: {player.TroopsInBarracks}", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), player);
        }

        /// <summary>
        /// Assassinates an enemy troop at the target node.
        /// </summary>
        public void ExecuteAssassinate(MapNode node, Player attacker)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            node.Occupant = PlayerColor.None;
            _stateManager.AddTrophy(attacker);

            GameLogger.Log($"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), attacker);
        }

        /// <summary>
        /// Moves a troop from source to destination node.
        /// </summary>
        public void ExecuteMove(MapNode source, MapNode destination, Player activePlayer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (activePlayer == null) throw new ArgumentNullException(nameof(activePlayer));

            destination.Occupant = source.Occupant;
            source.Occupant = PlayerColor.None;

            GameLogger.Log($"Moved troop from {source.Id} to {destination.Id}.", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(source), activePlayer);
            _recalculateSiteState(_getSiteForNode(destination), activePlayer);
        }

        /// <summary>
        /// Returns a troop from a node to barracks.
        /// </summary>
        public void ExecuteReturnTroop(MapNode node, Player requestingPlayer)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (requestingPlayer == null) throw new ArgumentNullException(nameof(requestingPlayer));

            if (node.Occupant == requestingPlayer.Color)
            {
                node.Occupant = PlayerColor.None;
                _stateManager.AddTroops(requestingPlayer, 1);
                GameLogger.Log($"Returned friendly troop at Node {node.Id} to barracks.", LogChannel.Combat);
            }
            else if (node.Occupant != PlayerColor.None)
            {
                PlayerColor enemyColor = node.Occupant;
                node.Occupant = PlayerColor.None;
                GameLogger.Log($"Returned {enemyColor} troop at Node {node.Id} to their barracks.", LogChannel.Combat);
            }

            _recalculateSiteState(_getSiteForNode(node), requestingPlayer);
        }

        /// <summary>
        /// Supplants an enemy troop (assassinate + deploy in one action).
        /// </summary>
        public void ExecuteSupplant(MapNode node, Player attacker)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            // Atomic: Assassinate + Deploy
            node.Occupant = PlayerColor.None;
            _stateManager.AddTrophy(attacker);

            if (_getCurrentPhase() != MatchPhase.Setup)
            {
                _stateManager.TrySpendPower(attacker, GameConstants.DEPLOY_POWER_COST);
            }
            _stateManager.RemoveTroops(attacker, 1);
            node.Occupant = attacker.Color;

            GameLogger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall) and Deployed.", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), attacker);
        }
    }
}



