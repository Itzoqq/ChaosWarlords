using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Source.Core.Interfaces.Logic
{
    /// <summary>
    /// Defines a contract for validating commands before they are executed.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to validate.</typeparam>
    public interface ICommandValidator<TCommand> where TCommand : IGameCommand
    {
        /// <summary>
        /// Validates the command against the current game state.
        /// </summary>
        /// <param name="command">The command instance to validate.</param>
        /// <param name="state">The current gameplay state.</param>
        /// <returns>A ValidationResult indicating success or failure.</returns>
        ValidationResult Validate(TCommand command, IGameplayState state);
    }
}
