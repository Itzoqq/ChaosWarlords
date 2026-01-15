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
        /// Gets the type of the command for identification.
        /// </summary>
        Core.Data.Enums.CommandType Type { get; }

        /// <summary>
        /// Converts the command to a serializable Data Transfer Object.
        /// </summary>
        /// <returns>The serializable DTO representing this command.</returns>
        Core.Data.Dtos.GameCommandDto ToDto();

        /// <summary>
        /// Validates if the command can be executed in the current context.
        /// </summary>
        /// <param name="context">The match context containing data about the game.</param>
        /// <returns>True if the command is valid, false otherwise.</returns>
        bool Validate(Source.Contexts.MatchContext context);

        /// <summary>
        /// Executes the command logic against the provided match context.
        /// </summary>
        /// <param name="context">The context in which to execute (allows deterministic simulation).</param>
        void Execute(Source.Contexts.MatchContext context);
    }
}
