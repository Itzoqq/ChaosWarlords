using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.States.Input
{
    // Defines the interface for all input/update modes within the GameplayState
    public interface IInputMode
    {
        // Executes the update and handles all relevant input for this mode.
        // It returns an IGameCommand that the GameplayState should execute.
        IGameCommand HandleInput(InputManager inputManager, IMarketManager marketManager, IMapManager mapManager, Player activePlayer, IActionSystem actionSystem);
    }
}