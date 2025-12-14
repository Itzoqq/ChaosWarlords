using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Command executed after a successful card action or targeting phase is complete.
    /// It finalizes the card play and resets the action system.
    /// </summary>
    public class ActionCompletedCommand : IGameCommand
    {
        public void Execute(GameplayState state)
        {
            var actionSystem = state._actionSystem;
            var turnManager = state._turnManager;

            // 1. Finalize the pending card (pay cost, move to played)
            if (actionSystem.PendingCard != null)
            {
                state.ResolveCardEffects(actionSystem.PendingCard);
                state.MoveCardToPlayed(actionSystem.PendingCard);
            }

            // 2. Reset the action system state
            actionSystem.CancelTargeting();

            // *** FIX: Switch the input mode back to normal ***
            state.SwitchToNormalMode();

            // 3. Log the completion
            GameLogger.Log("Action Complete.", LogChannel.General);
        }
    }
}