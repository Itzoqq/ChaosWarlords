using ChaosWarlords.Source.Core.Interfaces.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Source.Managers
{
    public class MapManager : IMapManager
    {
        // State
        public List<MapNode> NodesInternal { get; private set; }
        public List<Site> SitesInternal { get; private set; }
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;

        // Sub-Systems (Original)
        private readonly MapRuleEngine _ruleEngine;
        private readonly SiteControlSystem _controlSystem;

        // New Service Classes (Phase 2 Refactoring)
        private readonly MapTopology _topology;
        private readonly CombatResolver _combat;
        private readonly SpyOperations _spyOps;
        private readonly MapRewardSystem _rewards;
        private IPlayerStateManager _playerStateManager;

        // Events
        public event System.Action OnSetupDeploymentComplete;

        // Interface Implementation
        IReadOnlyList<MapNode> IMapManager.Nodes => NodesInternal;
        IReadOnlyList<Site> IMapManager.Sites => SitesInternal;

        public MapManager(List<MapNode> nodes, List<Site> sites, IPlayerStateManager playerState = null)
        {
            NodesInternal = nodes;
            SitesInternal = sites;
            _playerStateManager = playerState;
            _nodeSiteLookup = new Dictionary<MapNode, Site>();

            // Build Lookup
            if (sites != null)
            {
                foreach (var site in sites)
                {
                    site.RecalculateBounds();
                    foreach (var node in site.NodesInternal)
                        _nodeSiteLookup[node] = site;
                }
            }

            // Initialize Sub-Systems (Composition)
            _ruleEngine = new MapRuleEngine(NodesInternal, SitesInternal, _nodeSiteLookup);
            _controlSystem = new SiteControlSystem();
            if (_playerStateManager != null) _controlSystem.SetPlayerStateManager(_playerStateManager);

            // Initialize New Service Classes
            _topology = new MapTopology(NodesInternal, SitesInternal);
            _rewards = new MapRewardSystem(_controlSystem);
            _combat = new CombatResolver(
                node => GetSiteForNode(node),
                (site, player) => RecalculateSiteState(site, player),
                () => CurrentPhase,
                _playerStateManager
            );
            _spyOps = new SpyOperations((site, player) => RecalculateSiteState(site, player), _playerStateManager);
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
            _controlSystem.SetPlayerStateManager(stateManager);
            _combat.SetPlayerStateManager(stateManager);
            _spyOps.SetPlayerStateManager(stateManager);
        }

        // -------------------------------------------------------------------------
        // FACADE METHODS (Delegating to Sub-Systems)
        // -------------------------------------------------------------------------

        public bool HasPresence(MapNode targetNode, PlayerColor player) => _ruleEngine.HasPresence(targetNode, player);
        public bool CanDeployAt(MapNode targetNode, PlayerColor player) => _ruleEngine.CanDeployAt(targetNode, player);
        public bool CanAssassinate(MapNode target, Player attacker) => _ruleEngine.CanAssassinate(target, attacker);
        public bool CanMoveSource(MapNode node, Player activePlayer) => _ruleEngine.CanMoveSource(node, activePlayer);
        public bool CanMoveDestination(MapNode node) => _ruleEngine.CanMoveDestination(node);

        public bool HasValidAssassinationTarget(Player activePlayer) => _ruleEngine.HasValidAssassinationTarget(activePlayer);
        public bool HasValidReturnSpyTarget(Player activePlayer) => _ruleEngine.HasValidReturnSpyTarget(activePlayer);
        public bool HasValidReturnTroopTarget(Player activePlayer) => _ruleEngine.HasValidReturnTroopTarget(activePlayer);
        public bool HasValidPlaceSpyTarget(Player activePlayer) => _ruleEngine.HasValidPlaceSpyTarget(activePlayer);
        public bool HasValidMoveSource(Player activePlayer) => _ruleEngine.HasValidMoveSource(activePlayer);

        public Site GetSiteForNode(MapNode node) => _ruleEngine.GetSiteForNode(node);

        public void RecalculateSiteState(Site site, Player activePlayer) => _rewards.RecalculateSiteState(site, activePlayer);
        public void DistributeStartOfTurnRewards(Player activePlayer) => _rewards.DistributeStartOfTurnRewards(SitesInternal, activePlayer);

        public void SetPhase(MatchPhase phase) => _ruleEngine.SetPhase(phase);
        public MatchPhase CurrentPhase => _ruleEngine.CurrentPhase;

        // -------------------------------------------------------------------------
        // SPATIAL QUERIES (Delegated to MapTopology)
        // -------------------------------------------------------------------------

        public void CenterMap(int screenWidth, int screenHeight) => _topology.CenterMap(screenWidth, screenHeight);
        public void ApplyOffset(Vector2 offset) => _topology.ApplyOffset(offset);
        public MapNode GetNodeAt(Vector2 position) => _topology.GetNodeAt(position);
        public Site GetSiteAt(Vector2 position) => _topology.GetSiteAt(position);
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => _spyOps.GetEnemySpiesAtSite(site, activePlayer);

        // -------------------------------------------------------------------------
        // COMBAT OPERATIONS (Delegated to CombatResolver)
        // -------------------------------------------------------------------------

        // -------------------------------------------------------------------------
        // STATE MUTATION ACTIONS (Orchestration)
        // -------------------------------------------------------------------------

        public virtual bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            if (currentPlayer == null) throw new ArgumentNullException(nameof(currentPlayer));
            if (targetNode == null) throw new ArgumentNullException(nameof(targetNode));

            if (!ValidateDeployment(currentPlayer, targetNode))
            {
                return false;
            }

            // Step 3: Execution (Delegated to CombatResolver)
            _combat.ExecuteDeploy(targetNode, currentPlayer);

            HandlePostDeployment(currentPlayer);

            return true;
        }

        private bool ValidateDeployment(Player currentPlayer, MapNode targetNode)
        {
            // Step 1: Validation (Delegated)
            if (!CanDeployAt(targetNode, currentPlayer.Color))
            {
                GameLogger.Log($"Invalid Deployment at Node {targetNode.Id}: Occupied or No Presence.", LogChannel.Error);
                return false;
            }

            // Step 2: Resource Checks (Business Logic)
            if (currentPlayer.TroopsInBarracks <= 0)
            {
                GameLogger.Log("Cannot Deploy: Barracks Empty!", LogChannel.Error);
                return false;
            }

            // Power Check skipped in Setup Phase
            if (CurrentPhase != MatchPhase.Setup && currentPlayer.Power < GameConstants.DEPLOY_POWER_COST)
            {
                GameLogger.Log("Cannot Deploy: Not enough Power!", LogChannel.Economy);
                return false;
            }

            return true;
        }

        private void HandlePostDeployment(Player currentPlayer)
        {
            // Auto-advance turn in Setup Phase
            if (CurrentPhase == MatchPhase.Setup)
            {
                GameLogger.Log("Setup deployment complete. Auto-advancing turn...", LogChannel.Info);
                OnSetupDeploymentComplete?.Invoke();
            }

            if (currentPlayer.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);
        }

        public void Assassinate(MapNode node, Player attacker)
        {
            _combat.ExecuteAssassinate(node, attacker);
        }

        public bool CanReturnTroop(MapNode node, Player requestingPlayer)
        {
            if (node == null) return false;
            // Must have presence at the node (direct or adjacent)
            if (!HasPresence(node, requestingPlayer.Color)) return false;
            // Cannot return Neutral
            if (node.Occupant == PlayerColor.Neutral) return false;
            // Must be occupied
            if (node.Occupant == PlayerColor.None) return false;

            return true;
        }

        public void ReturnTroop(MapNode node, Player requestingPlayer)
        {
            if (!CanReturnTroop(node, requestingPlayer)) return;
            _combat.ExecuteReturnTroop(node, requestingPlayer);
        }

        public void PlaceSpy(Site site, Player player)
        {
            _spyOps.ExecutePlaceSpy(site, player);
        }

        public void Supplant(MapNode node, Player attacker)
        {
            _combat.ExecuteSupplant(node, attacker);
        }

        // Removed dead code: MoveTroop(MapNode, MapNode) overload
        // Only the 3-parameter version with Player is kept
        public void MoveTroop(MapNode source, MapNode destination, Player activePlayer)
        {
            _combat.ExecuteMove(source, destination, activePlayer);
        }

        public bool CanReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor)
        {
            if (site == null) return false;
            // Check Presence using the Rule Engine
            // We can check presence on any node in the site (if you have presence on one, you have presence on the site for interaction presumably, 
            // although strictly presence is per node. But for Site interactions, usually you need presence *at* the site.
            // The original code checked NodesInternal[0]. 

            // Safer check: Do we have presence at ANY node of the site?
            bool hasPresence = site.NodesInternal.Any(n => HasPresence(n, activePlayer.Color));
            if (!hasPresence) return false;

            if (!site.Spies.Contains(targetSpyColor) || targetSpyColor == activePlayer.Color) return false;

            return true;
        }

        public bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor)
        {
            if (!CanReturnSpecificSpy(site, activePlayer, targetSpyColor))
            {
                GameLogger.Log($"Cannot return spy: Invalid Target or No Presence.", LogChannel.Error);
                return false;
            }

            return _spyOps.ExecuteReturnSpy(site, activePlayer, targetSpyColor);
        }

    }
}


