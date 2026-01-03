using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.GameStates;

namespace ChaosWarlords.Source.Commands
{
    public class DevourCardCommand : IGameCommand
    {
        public Card CardToDevour { get; }
        public DevourCardCommand(Card card)
        {
            CardToDevour = card;
        }

        // Implement the logic directly on the Interface method.
        // This works for both the real GameplayState and your Test Mocks.
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("Devour", $"Devoured card {CardToDevour.Name}");
            // 1. Perform the Devour 
            // We use .MatchManager (Property) instead of ._matchManager (Field)
            // because the Interface exposes the Property.
            state.MatchManager.DevourCard(CardToDevour);

            // 2. Signal that the targeting action is complete
            state.ActionSystem.CompleteAction();
        }

    }
}


