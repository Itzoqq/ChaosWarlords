using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    public class SiteControlSystem
    {
        private readonly IPlayerStateManager _stateManager;
        private readonly IGameLogger _logger;

        public SiteControlSystem(IPlayerStateManager stateManager, IGameLogger logger)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // REMOVED: SetPlayerStateManager. Dependency is now immutable.

        public void RecalculateSiteState(Site site, Player activePlayer)
        {
            if (site is null) return;

            PlayerColor previousOwner = site.Owner;
            bool previousTotal = site.HasTotalControl;

            PlayerColor newOwner = CalculateSiteOwner(site);
            bool newTotalControl = CalculateTotalControl(site, newOwner);

            site.Owner = newOwner;
            site.HasTotalControl = newTotalControl;

            HandleControlChange(site, activePlayer, previousOwner, newOwner);
            HandleTotalControlChange(site, activePlayer, previousTotal, newTotalControl, newOwner);
        }

        // Every color a troop space can be occupied by, excluding PlayerColor.None (which
        // means "empty," not "a competing faction"). Rulebook p.10's own worked example
        // ("red has 2... black has 1, and there is 1 white [Neutral] troop") counts Neutral
        // alongside every player color - this used to only count Red/Blue, which meant Black/
        // Orange troops (3-4 player games, per PlayerColor's own 4-color range) could never
        // win or contribute to a majority at all. See planning.txt.
        private static readonly PlayerColor[] ControlContestingColors =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black, PlayerColor.Orange, PlayerColor.Neutral
        };

        private static PlayerColor CalculateSiteOwner(Site site)
        {
            // RULE: Control is determined by TROOPS ONLY (Spies do not count for majority).
            // "You control a site when there are more troops of your color there than troops
            // of any other single color" (rulebook p.10) - the single highest count, as long
            // as no other color ties it.
            var counts = ControlContestingColors.ToDictionary(
                color => color,
                color => site.NodesInternal.Count(n => n.Occupant == color));

            int maxCount = counts.Values.Max();
            if (maxCount == 0) return PlayerColor.None;

            var leaders = counts.Where(kv => kv.Value == maxCount).ToList();
            if (leaders.Count != 1) return PlayerColor.None;

            // Neutral (white/unaligned) troops only ever block a majority, same as the
            // pre-fix Red/Blue-only version's behavior - they're not a player and can never
            // themselves "control" a site for scoring purposes, even holding the outright
            // majority (matches the original code never returning Neutral here either).
            var leader = leaders[0].Key;
            return leader == PlayerColor.Neutral ? PlayerColor.None : leader;
        }

        private static bool CalculateTotalControl(Site site, PlayerColor owner)
        {
            if (owner == PlayerColor.None) return false;

            // RULE: Total Control = You Control Site AND No Enemy Presence (Troops OR Spies)
            // Empty nodes are ALLOWED.

            // 1. Check for Enemy Troops
            bool hasEnemyTroops = site.NodesInternal.Any(n => n.Occupant != owner && n.Occupant != PlayerColor.None);
            if (hasEnemyTroops) return false;

            // 2. Check for Enemy Spies
            bool hasEnemySpy = site.Spies.Any(spyColor => spyColor != owner && spyColor != PlayerColor.None);
            if (hasEnemySpy) return false;

            return true;
        }

        private void HandleControlChange(Site site, Player activePlayer, PlayerColor oldOwner, PlayerColor newOwner)
        {
            if (newOwner != oldOwner)
            {
                // RULE: City Sites grant Immediate Influence when you take control
                if (activePlayer is not null && newOwner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    _logger.Log($"Seized Control of {site.Name}! (+{site.ControlAmount} {site.ControlResource})", LogChannel.Economy);
                }
            }
        }

        private void HandleTotalControlChange(Site site, Player activePlayer, bool wasTotal, bool isTotal, PlayerColor owner)
        {
            if (isTotal == wasTotal) return;

            if (isTotal)
            {
                HandleTotalControlGain(site, activePlayer, owner);
            }
            else
            {
                HandleTotalControlLoss(site, activePlayer, owner);
            }
        }

        private void HandleTotalControlGain(Site site, Player activePlayer, PlayerColor owner)
        {
            // RULE: City Sites grant Immediate VP when you take TOTAL control
            if (activePlayer is not null && owner == activePlayer.Color && site.IsCity)
            {
                ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                _logger.Log($"Total Control established in {site.Name}! (+{site.TotalControlAmount} {site.TotalControlResource})", LogChannel.Economy);
            }
        }

        private void HandleTotalControlLoss(Site site, Player activePlayer, PlayerColor owner)
        {
            if (activePlayer is not null && activePlayer.Color == owner)
            {
                _logger.Log($"Lost Total Control of {site.Name}.", LogChannel.Combat);
            }
        }

        public void DistributeStartOfTurnRewards(IReadOnlyList<Site> sites, Player activePlayer)
        {
            if (sites is null) return;
            foreach (var site in sites)
            {
                // RULE: Only City Sites grant passive income (at Start of Turn)
                // RULE: Rewards are ADDITIVE (Control + Total Control)
                if (site.IsCity && site.Owner == activePlayer.Color)
                {
                    // 1. Base Control Reward
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    _logger.Log($"Income ({site.Name}): +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);

                    // 2. Total Control Bonus
                    if (site.HasTotalControl)
                    {
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                        _logger.Log($"Total Control Bonus ({site.Name}): +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                    }
                }
            }
        }

        private void ApplyReward(Player player, ResourceType type, int amount)
        {
            // Delegating to State Manager (Required)
            if (type == ResourceType.Power) _stateManager.AddPower(player, amount);
            if (type == ResourceType.Influence) _stateManager.AddInfluence(player, amount);
            if (type == ResourceType.VictoryPoints) _stateManager.AddVictoryPoints(player, amount);
        }
}
}


