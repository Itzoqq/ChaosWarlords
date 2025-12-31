using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Responsible for dispatching, recording, and executing commands.
    /// Acts as the central funnel for all game actions.
    /// </summary>
    public interface ICommandDispatcher
    {
        void Dispatch(IGameCommand command, IGameplayState state);
    }
}
