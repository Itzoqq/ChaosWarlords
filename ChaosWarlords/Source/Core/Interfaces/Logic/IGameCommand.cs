using ChaosWarlords.Source.Core.Interfaces.State;

using ChaosWarlords.Source.GameStates;

namespace ChaosWarlords.Source.Core.Interfaces.Logic
{
    /// <summary>
    /// Represents a discrete game action encapsulated as a command object.
    /// Used for the Command Pattern to support Replays, Network Synchronization, and Undo/Redo.
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>
        /// Executes the command logic against the provided game state.
        /// </summary>
        /// <param name="state">The context in which to execute (allows deterministic simulation).</param>
        void Execute(IGameplayState state);
    }
}
