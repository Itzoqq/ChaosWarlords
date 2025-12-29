using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    public class SiteControlSystem
    {
        private IPlayerStateManager _stateManager = null!;

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _stateManager = stateManager;
        }

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

        private static PlayerColor CalculateSiteOwner(Site site)
        {
            // RULE: Control is determined by TROOPS ONLY (Spies do not count for majority)
            int redCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Red);
            int blueCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Blue);
            int neutralCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Neutral);

            if (redCount > blueCount && redCount > neutralCount) return PlayerColor.Red;
            if (blueCount > redCount && blueCount > neutralCount) return PlayerColor.Blue;

            return PlayerColor.None;
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
                    GameLogger.Log($"Seized Control of {site.Name}! (+{site.ControlAmount} {site.ControlResource})", LogChannel.Economy);
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
                GameLogger.Log($"Total Control established in {site.Name}! (+{site.TotalControlAmount} {site.TotalControlResource})", LogChannel.Economy);
            }
        }

        private static void HandleTotalControlLoss(Site site, Player activePlayer, PlayerColor owner)
        {
            if (activePlayer is not null && activePlayer.Color == owner)
            {
                GameLogger.Log($"Lost Total Control of {site.Name}.", LogChannel.Combat);
            }
        }

        public void DistributeStartOfTurnRewards(System.Collections.Generic.IReadOnlyList<Site> sites, Player activePlayer)
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
                    GameLogger.Log($"Income ({site.Name}): +{site.ControlAmount} {site.ControlResource}", LogChannel.Economy);

                    // 2. Total Control Bonus
                    if (site.HasTotalControl)
                    {
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                        GameLogger.Log($"Total Control Bonus ({site.Name}): +{site.TotalControlAmount} {site.TotalControlResource}", LogChannel.Economy);
                    }
                }
            }
        }

        private void ApplyReward(Player player, ResourceType type, int amount)
        {
            if (_stateManager is null)
            {
                if (type == ResourceType.Power) player.Power += amount;
                if (type == ResourceType.Influence) player.Influence += amount;
                if (type == ResourceType.VictoryPoints) player.VictoryPoints += amount;
                return;
            }

            if (type == ResourceType.Power) _stateManager.AddPower(player, amount);
            if (type == ResourceType.Influence) _stateManager.AddInfluence(player, amount);
            if (type == ResourceType.VictoryPoints) _stateManager.AddVictoryPoints(player, amount);
        }
    }
}


