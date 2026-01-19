using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;

namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Defines the contract for input handling modes.
    /// Reformatted for Event-Driven Architecture (Jan 2026).
    /// </summary>
    public interface IInputMode
    {
        /// <summary>
        /// Handles discrete events like Clicks or Key Presses.
        /// Returns a command if the event triggers a game action.
        /// </summary>
        IGameCommand? HandleInteraction(InputEventArgs evt, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem);

        /// <summary>
        /// Handles continuous updates like Hover states or Tooltips.
        /// Called every frame.
        /// </summary>
        void HandleUpdate(IInputManager inputManager, IMapManager mapManager, Player activePlayer);
    }
}



