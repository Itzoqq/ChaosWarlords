using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Map
{
    /// <summary>
    /// Handles spy-related operations: placement, return, and spy queries.
    /// Extracted from MapManager to follow Single Responsibility Principle.
    /// </summary>
    public class SpyOperations
    {
        private readonly Action<Site, Player> _recalculateSiteState;
        private IPlayerStateManager _stateManager;

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public SpyOperations(Action<Site, Player> recalculateSiteState, IPlayerStateManager stateManager)
        {
            _recalculateSiteState = recalculateSiteState;
            _stateManager = stateManager;
        }

        /// <summary>
        /// Places a spy at the target site.
        /// </summary>
        public void ExecutePlaceSpy(Site site, Player player)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (player == null) throw new ArgumentNullException(nameof(player));

            if (site.Spies.Contains(player.Color))
            {
                GameLogger.Log("You already have a spy at this site.", LogChannel.Error);
                return;
            }

            if (player.SpiesInBarracks > 0)
            {
                _stateManager.RemoveSpies(player, 1);
                site.Spies.Add(player.Color);
                
                GameLogger.Log($"Spy placed at {site.Name}.", LogChannel.Combat);
                _recalculateSiteState(site, player);
            }
            else
            {
                GameLogger.Log("No Spies left in supply!", LogChannel.Error);
            }
        }

        /// <summary>
        /// Returns a specific enemy spy from a site.
        /// </summary>
        public bool ExecuteReturnSpy(Site site, Player activePlayer, PlayerColor targetSpyColor)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (activePlayer == null) throw new ArgumentNullException(nameof(activePlayer));

            if (!site.Spies.Contains(targetSpyColor) || targetSpyColor == activePlayer.Color)
            {
                GameLogger.Log($"Cannot return spy: Invalid Target.", LogChannel.Error);
                return false;
            }

            site.Spies.Remove(targetSpyColor);
            
            GameLogger.Log($"Returned {targetSpyColor} Spy from {site.Name} to barracks.", LogChannel.Combat);
            _recalculateSiteState(site, activePlayer);
            return true;
        }

        /// <summary>
        /// Gets list of enemy spies at a site (excluding the active player's spies).
        /// </summary>
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer)
        {
            if (site == null) return new List<PlayerColor>();
            return site.Spies.Where(s => s != activePlayer.Color && s != PlayerColor.None).ToList();
        }
    }
}



