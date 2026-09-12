using ChaosWarlords.Source.Core.Interfaces.Services;
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
        private readonly Func<PlayerColor, Player?> _getPlayerByColor;
        private readonly IPlayerStateManager _stateManager;
        private readonly IGameLogger _logger;

        public CombatResolver(
            Func<MapNode, Site> getSiteForNode,
            Action<Site, Player> recalculateSiteState,
            Func<MatchPhase> getCurrentPhase,
            Func<PlayerColor, Player?> getPlayerByColor,
            IPlayerStateManager stateManager,
            IGameLogger logger)
        {
            _getSiteForNode = getSiteForNode;
            _recalculateSiteState = recalculateSiteState;
            _getCurrentPhase = getCurrentPhase;
            _getPlayerByColor = getPlayerByColor;
            _stateManager = stateManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Deploys a troop to the target node. Handles resource costs and validation.
        /// </summary>
        public void ExecuteDeploy(MapNode node, Player player)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(player);

            // A card credit waives Power; the physical troop still comes from barracks.
            if (player.PendingFreeTroops > 0)
            {
                player.PendingFreeTroops--;
                _logger.Log($"Deployed FREE troop from card effect. Remaining free: {player.PendingFreeTroops}", LogChannel.Combat);
            }
            // Priority 2: Use barracks troops (free in Setup, costs Power otherwise)
            else
            {
                if (_getCurrentPhase() != MatchPhase.Setup && player.TroopsInBarracks > 0)
                {
                    _stateManager.TrySpendPower(player, GameConstants.DeployPowerCost);
                }
            }

            _stateManager.RemoveTroops(player, 1);
            _logger.Log($"Deployed troop from barracks supply. Remaining: {player.TroopsInBarracks}", LogChannel.Combat);
            node.Occupant = player.Color;
            _recalculateSiteState(_getSiteForNode(node), player);
        }

        /// <summary>
        /// Deploys player's OWN troop to an empty node, funded by a trophy hall reservoir
        /// instead of the normal barracks/PendingFreeTroops supply (Mummy Lord: "take a white
        /// troop from any trophy hall and deploy it anywhere on the board" -
        /// TROPHY-HALL-AS-TROOP-RESERVOIR, planning.txt). Removing the trophy-hall troop is the
        /// caller's responsibility (IPlayerStateManager.RemoveTrophy) - this method only ever
        /// performs the board-side half, matching ExecuteAssassinate/ExecuteSupplant's own
        /// "CombatResolver owns every node.Occupant mutation" convention. Unlike ExecuteDeploy,
        /// this is unconditionally free - it never touches PendingFreeTroops, TroopsInBarracks,
        /// or Power, since the troop's source is neither of those pools.
        /// </summary>
        public void ExecuteDeployFromTrophyHall(MapNode node, Player player)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(player);

            node.Occupant = player.Color;
            _logger.Log($"Deployed troop from a trophy hall reservoir to Node {node.Id}.", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), player);
        }

        /// <summary>
        /// Assassinates an enemy troop at the target node.
        /// </summary>
        public void ExecuteAssassinate(MapNode node, Player attacker)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(attacker);
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            // Captured BEFORE the mutation below erases it - see Player.TrophyHallByColor.
            PlayerColor removedColor = node.Occupant;
            node.Occupant = PlayerColor.None;
            _stateManager.AddTrophy(attacker, removedColor);

            _logger.Log($"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), attacker);
        }

        /// <summary>
        /// Moves a troop from source to destination node.
        /// </summary>
        public void ExecuteMove(MapNode source, MapNode destination, Player activePlayer)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(activePlayer);

            destination.Occupant = source.Occupant;
            source.Occupant = PlayerColor.None;

            _logger.Log($"Moved troop from {source.Id} to {destination.Id}.", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(source), activePlayer);
            _recalculateSiteState(_getSiteForNode(destination), activePlayer);
        }

        /// <summary>
        /// Returns a troop from a node to barracks.
        /// </summary>
        public void ExecuteReturnTroop(MapNode node, Player requestingPlayer)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(requestingPlayer);

            if (node.Occupant == requestingPlayer.Color)
            {
                node.Occupant = PlayerColor.None;
                _stateManager.AddTroops(requestingPlayer, 1);
                _logger.Log($"Returned friendly troop at Node {node.Id} to barracks.", LogChannel.Combat);
            }
            else if (node.Occupant != PlayerColor.None)
            {
                PlayerColor enemyColor = node.Occupant;
                node.Occupant = PlayerColor.None;

                // Find the enemy player and return their troop to barracks
                var enemyPlayer = _getPlayerByColor(enemyColor);
                if (enemyPlayer != null)
                {
                    _stateManager.AddTroops(enemyPlayer, 1);
                }

                _logger.Log($"Returned {enemyColor} troop at Node {node.Id} to their barracks.", LogChannel.Combat);
            }

            _recalculateSiteState(_getSiteForNode(node), requestingPlayer);
        }

        /// <summary>
        /// Supplants an enemy troop (assassinate + deploy in one action).
        /// </summary>
        public void ExecuteSupplant(MapNode node, Player attacker)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(attacker);
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            // Atomic: Assassinate + Deploy
            // Captured BEFORE the mutation below erases it - see Player.TrophyHallByColor.
            PlayerColor removedColor = node.Occupant;
            node.Occupant = PlayerColor.None;
            _stateManager.AddTrophy(attacker, removedColor);

            // Supplant includes its own free deploy, preserving unrelated card credits.
            if (attacker.TroopsInBarracks > 0)
            {
                _stateManager.RemoveTroops(attacker, 1);
                _logger.Log($"Supplanted with troop from barracks (FREE as part of Supplant action). Supply: {attacker.TroopsInBarracks}", LogChannel.Combat);
                node.Occupant = attacker.Color;
            }
            // Priority 3: Barracks empty - rulebook p.12/22 ("Deploy a Troop," cross-
            // referenced by "Supplant a Troop," p.14): gain 1 VP instead of the deploy
            // half. The assassinate half already happened above; the vacated space stays
            // empty (node.Occupant is already PlayerColor.None from above).
            else
            {
                _stateManager.AddVictoryPoints(attacker, 1);
                _logger.Log($"{attacker.DisplayName}'s barracks is empty - gained 1 VP instead of the Supplant deploy.", LogChannel.General);
            }

            _logger.Log($"Supplanted enemy at Node {node.Id} (Added to Trophy Hall).", LogChannel.Combat);
            _recalculateSiteState(_getSiteForNode(node), attacker);
        }
    }
}
