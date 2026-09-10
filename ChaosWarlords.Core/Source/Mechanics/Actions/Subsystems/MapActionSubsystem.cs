using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    public class MapActionSubsystem : IMapActionSubsystem
    {
        private readonly IMapManager _mapManager;
        private readonly ITurnManager _turnManager;
        private readonly IGameLogger _logger;
        private readonly IPlayerStateManager _playerStateManager;
        private readonly IActionSystem _actionSystem;
        private readonly IDevourSubsystem _devourSubsystem;

        // MatchManager stays setter-injected - see IMapActionSubsystem's own doc comment.
        private IMatchManager? _matchManager;

        private Player CurrentPlayer => _turnManager.ActivePlayer;

        public MapActionSubsystem(
            IMapManager mapManager,
            ITurnManager turnManager,
            IGameLogger logger,
            IPlayerStateManager playerStateManager,
            IActionSystem actionSystem,
            IDevourSubsystem devourSubsystem)
        {
            _mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _playerStateManager = playerStateManager ?? throw new ArgumentNullException(nameof(playerStateManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _devourSubsystem = devourSubsystem ?? throw new ArgumentNullException(nameof(devourSubsystem));
        }

        public void SetMatchManager(IMatchManager matchManager)
        {
            _matchManager = matchManager;
        }

        public void PerformAssassinate(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            ConsumePendingDevour(devourCardId);

            bool isPaidByCard = !string.IsNullOrEmpty(cardId);

            if (!isPaidByCard)
            {
                SpendAssassinateCost();
            }

            // Must be captured BEFORE MapManager.Assassinate mutates the node - see
            // IActionSystem.PendingAffectedPlayerColor's doc comment (Mindwitness).
            _actionSystem.SetPendingAffectedPlayerColor(node.Occupant);

            // Minotaur Skeleton: "assassinate up to three white troops AT A SINGLE SITE" -
            // read CurrentSourceEffect (still the top-of-stack repeat context; CompleteAction()
            // below hasn't popped/decremented it yet) BEFORE it's gone. Only binds on the
            // FIRST repeat (PendingSite still null) - every later repeat of this same effect is
            // then confined to that site by the existing PendingSite guards
            // (ActionInputController.HandleAssassinate, AssassinateCommand.Validate). See
            // CardEffect.RestrictRepeatsToFirstTargetSite's doc comment.
            if (_actionSystem.PendingSite == null && _actionSystem.CurrentSourceEffect?.RestrictRepeatsToFirstTargetSite == true)
            {
                _actionSystem.SetPendingSiteForChain(_mapManager.GetSiteForNode(node));
            }

            _mapManager.Assassinate(node, CurrentPlayer);

            // Death Tyrant: "Assassinate up to 3 troops at a single site. For each troop
            // removed, gain Influence" - read CurrentSourceEffect BEFORE CompleteAction() below
            // (same timing reasoning as RestrictRepeatsToFirstTargetSite above), and grant it
            // on EVERY repeat, not just the first: the EffectContext this reads stays the same
            // object across the whole repeat sequence (ResolveCurrentEffect's repeat branch
            // re-enters targeting without popping the stack), so this fires once per successful
            // removal - unlike DynamicAmountSource, which computes a single amount from live
            // board state, generally read only once after a whole effect tree finishes.
            if (_actionSystem.CurrentSourceEffect?.GainResourcePerRepeat is ResourceType resource && resource != ResourceType.None)
            {
                GrantResourcePerRepeat(resource);
            }

            _actionSystem.CompleteAction();
        }

        private void SpendAssassinateCost()
        {
            _playerStateManager.TrySpendPower(CurrentPlayer, GameConstants.AssassinatePowerCost);
        }

        /// <summary>
        /// Grants 1 of <paramref name="resource"/> to CurrentPlayer - the per-repeat side effect
        /// CardEffect.GainResourcePerRepeat describes, called once for each successful
        /// individual repeat of a SupportsRepeat effect (currently only wired for Assassinate,
        /// via PerformAssassinate). Deliberately supports only the resources that make sense to
        /// grant repeatedly and immediately (Power/Influence/VictoryPoints) - Troops (which
        /// CardEffectApplier.ApplyGainResource instead credits to PendingFreeTroops, a
        /// deferred pool spent later through the normal Deploy flow) has no shipped card
        /// wanting it here yet, so it's treated as unsupported rather than silently guessed at.
        /// </summary>
        private void GrantResourcePerRepeat(ResourceType resource)
        {
            switch (resource)
            {
                case ResourceType.Power:
                    _playerStateManager.AddPower(CurrentPlayer, 1);
                    break;
                case ResourceType.Influence:
                    _playerStateManager.AddInfluence(CurrentPlayer, 1);
                    break;
                case ResourceType.VictoryPoints:
                    _playerStateManager.AddVictoryPoints(CurrentPlayer, 1);
                    break;
                default:
                    _logger.Log($"MapActionSubsystem.GrantResourcePerRepeat: no case wired for ResourceType.{resource} - no resource granted.", LogChannel.Warning);
                    break;
            }
        }

        public void PerformReturnTroop(MapNode node, string? cardId)
        {
            // CompleteAction(), not a manual OnActionCompleted+ClearState() - see
            // PerformSupplant's comment (same fix, same reasoning: a manual clear never pops
            // ExecutionStack, so it would strand an EffectContext there if this were ever
            // reached via a chained effect). Confirmed via grep this method is currently
            // unreachable from any live path (no command or test calls it - see planning.txt
            // RESOLVED) - fixed anyway so it isn't a landmine for whatever wires it up next,
            // matching this file's PerformAssassinate/PerformSupplant precedent from earlier
            // this session.
            _mapManager.ReturnTroop(node, CurrentPlayer);
            _actionSystem.CompleteAction();
        }

        public void PerformSupplant(MapNode node, string? cardId, string? devourCardId = null)
        {
            // Transactional Devour Handling (Logic Layer)
            ConsumePendingDevour(devourCardId);

            // Must be captured BEFORE MapManager.Supplant mutates the node - see
            // IActionSystem.PendingAffectedPlayerColor's doc comment. No shipped card chains
            // off Supplant's outcome yet (Mindwitness uses Assassinate), but Supplant's
            // assassinate-half removes a troop the exact same way, so this is set here too for
            // consistency rather than leaving it Assassinate-only.
            _actionSystem.SetPendingAffectedPlayerColor(node.Occupant);
            _mapManager.Supplant(node, CurrentPlayer);
            // CompleteAction(), not a manual OnActionCompleted+ClearState() - matches
            // PerformAssassinate's pattern. When Supplant was reached via a chained effect
            // (e.g. Wight's Devour -> Supplant), its TargetingSupplant EffectContext is still
            // sitting on ExecutionStack; CompleteAction() pops it via ResolveCurrentEffect,
            // which is what actually fires OnActionCompleted once the stack is genuinely
            // empty (see HandleStackEmptyState). The manual raise+clear this replaced left
            // that effect stuck on the stack forever when Supplant came from a chain - it
            // would resurface and force Supplant targeting on the next unrelated card played
            // (see planning.txt RESOLVED). For a direct, non-chained Supplant (stack already
            // empty), CompleteAction()'s fallback branch does exactly what this used to do.
            _actionSystem.CompleteAction();
        }

        /// <summary>
        /// Mummy Lord's "take a white troop from any trophy hall and deploy it anywhere on the
        /// board" - removes one troopColor troop from sourcePlayerColor's trophy hall (the
        /// player TrophyHallRuleEngine resolved as the sole eligible source, before targeting
        /// even opened - see IActionSystem.PendingTrophyHallSourceColor), then deploys
        /// CurrentPlayer's OWN troop at node, funded by that reservoir instead of the normal
        /// barracks/PendingFreeTroops supply. DeployFromTrophyHallCommand.Validate() has already
        /// re-confirmed both the removal is still legal and sourcePlayerColor still matches
        /// PendingTrophyHallSourceColor by the time this runs.
        /// </summary>
        public void PerformDeployFromTrophyHall(MapNode node, PlayerColor sourcePlayerColor, PlayerColor troopColor, string? cardId)
        {
            // Only deploy if the trophy was actually removed - a troop must never materialize
            // on the board without its reservoir cost actually being paid. Validate() has
            // already confirmed this will succeed, so this is defense-in-depth (a stale/forged
            // command bypassing Validate() entirely), not an expected path - CompleteAction()
            // still runs regardless, matching ExecuteAssassinate/ExecuteSupplant's own
            // no-op-but-still-complete precedent for an unreachable-in-practice guard.
            var sourcePlayer = _turnManager.GetPlayerByColor(sourcePlayerColor);
            if (sourcePlayer != null && _playerStateManager.RemoveTrophy(sourcePlayer, troopColor))
            {
                _mapManager.DeployFromTrophyHall(node, CurrentPlayer);
            }

            _actionSystem.CompleteAction();
        }

        /// <summary>
        /// "Deploy 2 troops, then choose an opponent with a troop adjacent to at least 1 of
        /// them" (Gibbering Mouther) - deploys CurrentPlayer's OWN troop at node, funded the
        /// same way GainResource(Troops) is (PendingFreeTroops, granted here immediately
        /// rather than credited for later spend), then records node so a chained
        /// EffectType.SelectOpponent(RequiresAdjacencyToRecentDeploys) can find it. See
        /// ActionState.TargetingDeployTroop's doc comment for why this is a distinct effect
        /// from GainResource(Troops) rather than reusing it.
        /// </summary>
        public void PerformDeployTroop(MapNode node, string? cardId)
        {
            CurrentPlayer.PendingFreeTroops++;

            // Validate() already confirmed CanDeployAt(node) moments ago with nothing else
            // mutating state in between, so TryDeploy failing here is unreachable today - but
            // unlike PerformAssassinate/PerformSupplant's underlying MapManager calls (which
            // are void), TryDeploy DOES report success/failure, so a silent false here would
            // otherwise record a node as deployed-to (PendingDeployedNodes) that never actually
            // changed the board. Logged rather than silently swallowed, same defense-in-depth
            // reasoning as PerformDeployFromTrophyHall's own "not an expected path" guard.
            if (!_mapManager.TryDeploy(CurrentPlayer, node))
            {
                _logger.Log($"MapActionSubsystem.PerformDeployTroop: TryDeploy unexpectedly failed for node {node.Id} despite Validate() having already accepted it.", LogChannel.Warning);
            }

            _actionSystem.AddPendingDeployedNode(node);
            _actionSystem.CompleteAction();
        }

        /// <summary>
        /// Consumes a devour that was deferred earlier in a chained effect (e.g. Wight's
        /// "Devour a card in your hand -> Supplant a troop" - see CardEffectApplier.
        /// ApplyDevourWithChain / DevourSubsystem's deferExecution flow). Prefers the
        /// explicit devourCardId (authoritative, carried on the command DTO for replay);
        /// falls back to PendingDevourCard for the pre-target/replay flow, which doesn't
        /// thread an id through PerformSupplant's caller.
        ///
        /// Always clears the devour subsystem's pending state afterward, whether or not
        /// there was anything to consume - PendingDevourCard is deliberately NOT cleared
        /// by ActionSystem.ClearState()/CompleteAction() so it can survive across the
        /// chained targeting steps, but that means it would otherwise leak into the next
        /// unrelated Assassinate/Supplant the player makes this turn (ActionInputController
        /// reads PendingDevourCard unconditionally), and MatchManager.FindCardInPlayerCollections
        /// falls back to matching by Card.Id, so a stale reference could wrongly devour an
        /// unrelated duplicate-copy card later.
        /// </summary>
        private void ConsumePendingDevour(string? devourCardId)
        {
            if (!string.IsNullOrEmpty(devourCardId))
            {
                var cardToDevour = CurrentPlayer.Hand.FirstOrDefault(c => c.Id == devourCardId);
                if (cardToDevour != null) _matchManager?.DevourCard(cardToDevour);
                _devourSubsystem.ClearState();
            }
            else if (_actionSystem.PendingDevourCard != null)
            {
                _matchManager?.DevourCard(_actionSystem.PendingDevourCard);
                _devourSubsystem.ClearState();
            }
        }

        public void PerformMoveTroop(MapNode source, MapNode dest, string? cardId)
        {
            _mapManager.MoveTroop(source, dest, CurrentPlayer);
            _actionSystem.CompleteAction();
        }
    }
}
