using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;

namespace ChaosWarlords.Source.Commands
{
    public class DevourCardCommand : IGameCommand
    {
        private readonly Card _cardToDevour;

        public DevourCardCommand(Card card)
        {
            _cardToDevour = card;
        }

        // Implement the logic directly on the Interface method.
        // This works for both the real GameplayState and your Test Mocks.
        public void Execute(IGameplayState state)
        {
            // 1. Perform the Devour 
            // We use .MatchManager (Property) instead of ._matchManager (Field)
            // because the Interface exposes the Property.
            state.MatchManager.DevourCard(_cardToDevour);

            // 2. Signal that the targeting action is complete
            state.ActionSystem.CompleteAction();
        }

        // You can remove the concrete overload entirely if IGameCommand defines Execute(IGameplayState).
        // If your architecture requires this specific signature for some reason, just delegate it:
        public void Execute(GameplayState state)
        {
            Execute((IGameplayState)state);
        }
    }
}