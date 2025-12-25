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
        public void Execute(IGameplayState state)
        {
            // Access system managers via public properties
            var actionSystem = state.ActionSystem;

            // 1. Finalize the pending card
            if (actionSystem.PendingCard != null)
            {
                // Delegated to MatchManager which now handles Effect Resolution + Movement + Focus
                // We no longer call ResolveCardEffects or MoveCardToPlayed manually here.
                state.MatchManager.PlayCard(actionSystem.PendingCard);
            }

            // 2. Reset the action system state
            actionSystem.CancelTargeting();

            // 3. Return to normal input mode
            state.SwitchToNormalMode();

            // 4. Log the completion
            GameLogger.Log("Action Complete.", LogChannel.General);
        }
    }
}