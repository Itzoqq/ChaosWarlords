using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Source.Map
{
    /// <summary>
    /// Handles reward distribution from controlled sites.
    /// Extracted from MapManager to follow Single Responsibility Principle.
    /// Delegates to SiteControlSystem for actual calculations.
    /// </summary>
    public class MapRewardSystem
    {
        private readonly SiteControlSystem _controlSystem;

        public MapRewardSystem(SiteControlSystem controlSystem)
        {
            _controlSystem = controlSystem;
        }

        /// <summary>
        /// Distributes start-of-turn rewards to the active player based on site control.
        /// </summary>
        public void DistributeStartOfTurnRewards(List<Site> sites, Player activePlayer)
        {
            _controlSystem.DistributeStartOfTurnRewards(sites, activePlayer);
        }

        /// <summary>
        /// Recalculates control state for a specific site.
        /// </summary>
        public void RecalculateSiteState(Site site, Player activePlayer)
        {
            _controlSystem.RecalculateSiteState(site, activePlayer);
        }
    }
}



