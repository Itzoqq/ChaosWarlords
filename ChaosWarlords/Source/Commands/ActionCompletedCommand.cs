using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Command executed after a successful card action or targeting phase is complete.
    /// It finalizes the card play and resets the action system.
    /// </summary>
    public class ActionCompletedCommand : IGameCommand
    {
        // Update signature to use the new IGameplayState interface
        public void Execute(IGameplayState state)
        {
            // Access system managers via public properties
            var actionSystem = state.ActionSystem;

            // 1. Finalize the pending card (pay cost, move to played)
            if (actionSystem.PendingCard != null)
            {
                // Use the public methods on IGameplayState
                state.ResolveCardEffects(actionSystem.PendingCard);
                state.MoveCardToPlayed(actionSystem.PendingCard);
            }

            // 2. Reset the action system state
            actionSystem.CancelTargeting();

            state.SwitchToNormalMode();

            // 3. Log the completion
            GameLogger.Log("Action Complete.", LogChannel.General);
        }
    }
}