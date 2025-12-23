using System.Linq;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class SiteControlSystem
    {
        public void RecalculateSiteState(Site site, Player activePlayer)
        {
            if (site == null) return;

            PlayerColor previousOwner = site.Owner;
            bool previousTotal = site.HasTotalControl;

            PlayerColor newOwner = CalculateSiteOwner(site);
            bool newTotalControl = CalculateTotalControl(site, newOwner);

            site.Owner = newOwner;
            site.HasTotalControl = newTotalControl;

            HandleControlChange(site, activePlayer, previousOwner, newOwner);
            HandleTotalControlChange(site, activePlayer, previousTotal, newTotalControl, newOwner);
        }

        private PlayerColor CalculateSiteOwner(Site site)
        {
            int redCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Red);
            int blueCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Blue);
            int neutralCount = site.NodesInternal.Count(n => n.Occupant == PlayerColor.Neutral);

            if (redCount > blueCount && redCount > neutralCount) return PlayerColor.Red;
            if (blueCount > redCount && blueCount > neutralCount) return PlayerColor.Blue;

            return PlayerColor.None;
        }

        private bool CalculateTotalControl(Site site, PlayerColor owner)
        {
            if (owner == PlayerColor.None) return false;
            bool ownsAllNodes = site.NodesInternal.All(n => n.Occupant == owner);
            if (!ownsAllNodes) return false;

            bool hasEnemySpy = site.Spies.Any(spyColor => spyColor != owner && spyColor != PlayerColor.None);
            return !hasEnemySpy;
        }

        private void HandleControlChange(Site site, Player activePlayer, PlayerColor oldOwner, PlayerColor newOwner)
        {
            if (newOwner != oldOwner)
            {
                if (activePlayer != null && newOwner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    GameLogger.Log($"Seized Control of {site.Name}!", LogChannel.Economy);
                }
            }
        }

        private void HandleTotalControlChange(Site site, Player activePlayer, bool wasTotal, bool isTotal, PlayerColor owner)
        {
            if (isTotal != wasTotal)
            {
                // FIX: Added 'activePlayer != null' checks to prevent crash during troop movement
                if (isTotal && activePlayer != null && owner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                    GameLogger.Log($"Total Control established in {site.Name}!", LogChannel.Economy);
                }
                else if (!isTotal && wasTotal && activePlayer != null && activePlayer.Color == owner)
                {
                    GameLogger.Log($"Lost Total Control of {site.Name} (Spies or Troops lost).", LogChannel.Combat);
                }
            }
        }

        public void DistributeControlRewards(System.Collections.Generic.IReadOnlyList<Site> sites, Player activePlayer)
        {
            if (sites == null) return;
            foreach (var site in sites)
            {
                if (site.Owner == activePlayer.Color && site.IsCity)
                {
                    ApplyReward(activePlayer, site.ControlResource, site.ControlAmount);
                    if (site.HasTotalControl)
                        ApplyReward(activePlayer, site.TotalControlResource, site.TotalControlAmount);
                }
            }
        }

        private void ApplyReward(Player player, ResourceType type, int amount)
        {
            if (type == ResourceType.Power) player.Power += amount;
            if (type == ResourceType.Influence) player.Influence += amount;
            if (type == ResourceType.VictoryPoints) player.VictoryPoints += amount;
        }
    }
}