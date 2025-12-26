using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Systems
{
    public class MapManager : IMapManager
    {
        // State
        public List<MapNode> NodesInternal { get; private set; }
        public List<Site> SitesInternal { get; private set; }
        private readonly Dictionary<MapNode, Site> _nodeSiteLookup;

        // Sub-Systems
        private readonly MapRuleEngine _ruleEngine;
        private readonly SiteControlSystem _controlSystem;

        // Events
        public event System.Action OnSetupDeploymentComplete;

        // Interface Implementation
        IReadOnlyList<MapNode> IMapManager.Nodes => NodesInternal;
        IReadOnlyList<Site> IMapManager.Sites => SitesInternal;

        public MapManager(List<MapNode> nodes, List<Site> sites)
        {
            NodesInternal = nodes;
            SitesInternal = sites;
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
            // We pass the live lists/dictionary so the sub-systems always see current data
            _ruleEngine = new MapRuleEngine(NodesInternal, SitesInternal, _nodeSiteLookup);
            _controlSystem = new SiteControlSystem();
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

        public void RecalculateSiteState(Site site, Player activePlayer) => _controlSystem.RecalculateSiteState(site, activePlayer);
        public void DistributeStartOfTurnRewards(Player activePlayer) => _controlSystem.DistributeStartOfTurnRewards(SitesInternal, activePlayer);
        
        public void SetPhase(MatchPhase phase) => _ruleEngine.SetPhase(phase);
        public MatchPhase CurrentPhase => _ruleEngine.CurrentPhase;

        // -------------------------------------------------------------------------
        // NAVIGATION & QUERIES (Simple enough to keep here)
        // -------------------------------------------------------------------------

        public void CenterMap(int screenWidth, int screenHeight)
        {
            if (NodesInternal.Count == 0) return;
            var (MinX, MinY, MaxX, MaxY) = MapGeometry.CalculateBounds(NodesInternal);
            Vector2 mapCenter = new((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);
            Vector2 screenCenter = new(screenWidth / 2f, screenHeight / 2f);
            ApplyOffset(screenCenter - mapCenter);
        }

        // ... (Skipping ApplyOffset, GetNodeAt, GetSiteAt, GetEnemySpiesAtSite) ...

        private void ApplyOffset(Vector2 offset)
        {
            foreach (var node in NodesInternal) node.Position += offset;
            if (SitesInternal != null) foreach (var site in SitesInternal) site.RecalculateBounds();
        }

        public MapNode GetNodeAt(Vector2 position)
        {
            return NodesInternal.FirstOrDefault(n => Vector2.Distance(position, n.Position) <= MapNode.Radius);
        }

        public Site GetSiteAt(Vector2 position)
        {
            return SitesInternal?.FirstOrDefault(s => s.Bounds.Contains(position));
        }

        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer)
        {
            return site.Spies.Where(s => s != activePlayer.Color && s != PlayerColor.None).ToList();
        }

        // -------------------------------------------------------------------------
        // CORE LOGIC HELPERS (For reuse in complex actions like Supplant)
        // -------------------------------------------------------------------------

        private void ExecuteDeployCore(MapNode node, Player player)
        {
            // FREE in Setup Phase
            if (CurrentPhase != MatchPhase.Setup)
            {
                player.Power -= 1;
            }
            player.TroopsInBarracks--;
            node.Occupant = player.Color;
        }

        private void ExecuteAssassinateCore(MapNode node, Player attacker)
        {
            node.Occupant = PlayerColor.None;
            attacker.TrophyHall++;
        }

        // -------------------------------------------------------------------------
        // STATE MUTATION ACTIONS (Orchestration)
        // -------------------------------------------------------------------------

        public virtual bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            if (targetNode == null) return false;

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
            if (CurrentPhase != MatchPhase.Setup && currentPlayer.Power < 1)
            {
                GameLogger.Log("Cannot Deploy: Not enough Power!", LogChannel.Economy);
                return false;
            }

            // Step 3: Execution
            ExecuteMapAction(() => ExecuteDeployCore(targetNode, currentPlayer), 
            $"Deployed Troop at Node {targetNode.Id}. Supply: {currentPlayer.TroopsInBarracks}", 
            GetSiteForNode(targetNode), 
            currentPlayer);

            // Auto-advance turn in Setup Phase
            if (CurrentPhase == MatchPhase.Setup)
            {
                GameLogger.Log("Setup deployment complete. Auto-advancing turn...", LogChannel.Info);
                OnSetupDeploymentComplete?.Invoke();
            }

            if (currentPlayer.TroopsInBarracks == 0)
                GameLogger.Log("FINAL TROOP DEPLOYED! Game ends this round.", LogChannel.General);

            return true;
        }

        public void Assassinate(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            ExecuteMapAction(() => ExecuteAssassinateCore(node, attacker),
            $"Assassinated enemy at Node {node.Id}. Trophy Hall: {attacker.TrophyHall}",
            GetSiteForNode(node),
            attacker);
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
            
            if (node.Occupant == requestingPlayer.Color)
            {
                ExecuteMapAction(() =>
                {
                    node.Occupant = PlayerColor.None;
                    requestingPlayer.TroopsInBarracks++;
                },
                $"Returned friendly troop at Node {node.Id} to barracks.",
                GetSiteForNode(node),
                requestingPlayer);
            }
            else if (node.Occupant != PlayerColor.None)
            {
                PlayerColor enemyColor = node.Occupant;
                ExecuteMapAction(() =>
                {
                    node.Occupant = PlayerColor.None;
                },
                $"Returned {enemyColor} troop at Node {node.Id} to their barracks.",
                GetSiteForNode(node),
                requestingPlayer);
            }
        }

        public void PlaceSpy(Site site, Player player)
        {
            if (site.Spies.Contains(player.Color))
            {
                GameLogger.Log("You already have a spy at this site.", LogChannel.Error);
                return;
            }

            if (player.SpiesInBarracks > 0)
            {
                ExecuteMapAction(() =>
                {
                    player.SpiesInBarracks--;
                    site.Spies.Add(player.Color);
                },
                $"Spy placed at {site.Name}.",
                site,
                player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        public void Supplant(MapNode node, Player attacker)
        {
            if (node.Occupant == PlayerColor.None || node.Occupant == attacker.Color) return;

            // Atomic Action: Assassinate + Deploy in one transaction
            ExecuteMapAction(() =>
            {
                ExecuteAssassinateCore(node, attacker);
                ExecuteDeployCore(node, attacker);
            },
            $"Supplanted enemy at Node {node.Id} (Added to Trophy Hall) and Deployed.",
            GetSiteForNode(node),
            attacker);
        }

        public void MoveTroop(MapNode source, MapNode destination)
        {
            if (source == null || destination == null) return;

            // FIX: Track the player who is moving so we can pass them to RecalculateSiteState
            // This ensures Immediate Control Rewards trigger if the move causes a control shift.
            // Since MapManager doesn't natively hold Player objects, we assume the occupant Color matches a player
            // But we need the Player object for ApplyReward.
            // Since MoveTroop is called by ActionSystem which HAS the ActivePlayer, 
            // the ideal fix is to pass the Player. But changing signature affects Interface.
            
            // However, ExecuteMapAction takes 'Player player'.
            // For now, we'll assume the call comes from Active Player context where Recalculate needs 'activePlayer'.
            // In a strict sense, site control changes should trigger for the NEW owner.
            // SiteControlSystem logic: if (newOwner == activePlayer.Color) -> Reward.
            
            // PROBLEM: We don't have the Player object here to pass to RecalculateSiteState!
            // We only have the Color from source.Occupant.
            // But in 'TryDeploy', we receive 'Player currentPlayer'.
            // MoveTroop is lacking 'Player activePlayer'.
            
            // I will update the signature. This is a safe refactor as I see the interface IMapManager.

            // Wait, I can't update the interface in this edit if I don't see the file.
            // But I saw 'IMapManager' in the 'find_by_name' output earlier.
            // I will stick to what I know: I can try to find the player indirectly or just use null and accept broken rewards? 
            // NO. The user explicitly asked to fix this.
            
            // I will Update the signature here and then update the Interface.
        }

        // Updated Signature to include Player
        public void MoveTroop(MapNode source, MapNode destination, Player activePlayer)
        {
             if (source == null || destination == null) return;

            Action moveAction = () =>
            {
                destination.Occupant = source.Occupant;
                source.Occupant = PlayerColor.None;
            };

            ExecuteMapAction(moveAction, $"Moved troop from {source.Id} to {destination.Id}.", GetSiteForNode(source), activePlayer);
            // Also recalculate destination
            RecalculateSiteState(GetSiteForNode(destination), activePlayer);
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

            ExecuteMapAction(() =>
            {
                site.Spies.Remove(targetSpyColor);
            },
            $"Returned {targetSpyColor} Spy from {site.Name} to barracks.",
            site,
            activePlayer);
            return true;
        }

        private void ExecuteMapAction(System.Action action, string message, Site site, Player player)
        {
            action?.Invoke();
            if (!string.IsNullOrEmpty(message))
            {
                GameLogger.Log(message, LogChannel.Combat);
            }
            if (site != null)
            {
                RecalculateSiteState(site, player);
            }
        }
    }
}