using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Interfaces;

namespace ChaosWarlords.Source.States.Input
{
    /// <summary>
    /// Defines the contract for input handling modes.
    /// </summary>
    public interface IInputMode
    {
        IGameCommand HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem);
    }
}