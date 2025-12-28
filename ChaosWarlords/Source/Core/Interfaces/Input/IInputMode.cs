using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Defines the contract for input handling modes.
    /// </summary>
    public interface IInputMode
    {
        IGameCommand? HandleInput(IInputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem);
    }
}



