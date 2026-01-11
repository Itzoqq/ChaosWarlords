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
        /// Validates if the command can be executed in the current context.
        /// </summary>
        /// <param name="context">The match context containing data about the game.</param>
        /// <returns>True if the command is valid, false otherwise.</returns>
        bool Validate(ChaosWarlords.Source.Contexts.MatchContext context);

        /// <summary>
        /// Executes the command logic against the provided match context.
        /// </summary>
        /// <param name="context">The context in which to execute (allows deterministic simulation).</param>
        void Execute(ChaosWarlords.Source.Contexts.MatchContext context);
    }
}
