using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    public interface IMapManager
    {
        IReadOnlyList<MapNode> Nodes { get; }
        IReadOnlyList<Site> Sites { get; }



        void SetPhase(MatchPhase phase);
        MatchPhase CurrentPhase { get; }

        // Events
        event Action OnSetupDeploymentComplete;

        void CenterMap(int screenWidth, int screenHeight);

        // Deployment & Checking
        bool TryDeploy(Player currentPlayer, MapNode targetNode);
        bool CanDeployAt(MapNode targetNode, PlayerColor player);
        bool HasPresence(MapNode targetNode, PlayerColor player);

        // --- Deadlock Prevention Checks ---
        // These check if a valid target exists AND is reachable by the player
        bool HasValidAssassinationTarget(Player activePlayer, bool requireNeutralTroop = false, bool ignoresPresence = false, Site? restrictToSite = null);
        bool HasValidReturnSpyTarget(Player activePlayer);
        bool HasValidReturnTroopTarget(Player activePlayer);
        bool HasValidPlaceSpyTarget(Player activePlayer);

        /// <summary>
        /// True if there's a site with EXACTLY ONE spy the active player could legally return
        /// right now (their own, anywhere - or an enemy's, only where they have Presence) - see
        /// EffectType.ReturnUnitOrSpy. Deliberately narrower than "any returnable spy exists
        /// somewhere": a site with 2+ simultaneously-eligible spies can't be resolved by a single
        /// site click today (see SpySubsystem.HandleReturnUnitOrSpySite's own doc comment for why
        /// this isn't rare, and isn't fixed here) - counting it here would let this "no more
        /// legal targets" check report true while the player has no way to actually complete
        /// that repeat, risking a soft-lock. Not reachable via multiple isolated single-spy
        /// sites; only excludes a genuinely ambiguous one.
        /// </summary>
        bool HasValidReturnAnySpyTarget(Player activePlayer);
        // ---------------------------------------

        // Navigation / Queries
        // Note: screen-space hit-testing (GetNodeAt/GetSiteAt) is deliberately NOT on this
        // facade - it's an Input-layer concern (a headless server never needs it; network
        // clients always send resolved node/site IDs). See
        // ChaosWarlords/Source/Input/MapHitTestExtensions.cs.
        Site? GetSiteForNode(MapNode node);
        List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer);

        /// <summary>
        /// Looks up a node by id, or null if it doesn't exist. Every command's Validate()/
        /// Execute() used to do this same `Nodes.FirstOrDefault(n => n.Id == id)` scan
        /// independently (DeployTroopCommand, MoveTroopCommand, AssassinateCommand,
        /// SupplantCommand, ReturnTroopCommand, StateRestorer, DtoMapper, ActionSystem) - one
        /// shared lookup instead of ~15 copies of the same line.
        /// </summary>
        MapNode? GetNodeById(int id);

        // Actions
        bool CanAssassinate(MapNode target, Player attacker, bool requireNeutralTroop = false, bool ignoresPresence = false);
        void Assassinate(MapNode node, Player attacker);
        void Supplant(MapNode node, Player attacker);
        bool CanReturnTroop(MapNode node, Player requestingPlayer);
        void ReturnTroop(MapNode node, Player requestingPlayer);

        // Spy Actions
        void PlaceSpy(Site site, Player player);
        bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor);
        bool CanReturnOwnSpy(Site site, Player activePlayer);
        bool ReturnOwnSpy(Site site, Player activePlayer);

        /// <summary>
        /// All spy colors currently at a site, regardless of ownership (unlike
        /// GetEnemySpiesAtSite, which excludes activePlayer's own color) - EffectType.
        /// ReturnUnitOrSpy's candidate list for a site click.
        /// </summary>
        List<PlayerColor> GetAllSpiesAtSite(Site site);

        /// <summary>
        /// Can spyColor's spy be returned from this site by activePlayer right now? Own spy
        /// (spyColor == activePlayer.Color): no Presence needed, matching CanReturnTroop's own-
        /// unit precedent. Enemy spy: Presence needed at the site, matching
        /// CanReturnSpecificSpy's existing enemy-only check. EffectType.ReturnUnitOrSpy's
        /// (Intellect Devourer) target-type union - unlike CanReturnSpecificSpy, does not reject
        /// spyColor == activePlayer.Color.
        /// </summary>
        bool CanReturnAnySpy(Site site, Player activePlayer, PlayerColor spyColor);

        /// <summary>
        /// Returns spyColor's spy from the site to ITS OWNER'S barracks (own or enemy - see
        /// CanReturnAnySpy). Delegates to the existing, already-fixed ExecuteReturnOwnSpy/
        /// ExecuteReturnSpy rather than a 3rd parallel implementation.
        /// </summary>
        bool ReturnAnySpy(Site site, Player activePlayer, PlayerColor spyColor);


        // Move troop action
        bool HasValidMoveSource(Player activePlayer);
        bool CanMoveSource(MapNode node, Player activePlayer);
        bool CanMoveDestination(MapNode node);
        void MoveTroop(MapNode source, MapNode destination, Player activePlayer);

        // Game State / Rewards
        void DistributeStartOfTurnRewards(Player activePlayer);
        void RecalculateSiteState(Site site, Player activePlayer);
    }
}



