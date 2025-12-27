using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Commands
{
    public class PlayCardCommand : IGameCommand
    {
        private readonly Card _card;
        public PlayCardCommand(Card card) { _card = card; }

        public void Execute(IGameplayState state)
        {
            // We can now call the PlayCard logic directly on the state interface
            // The logic inside PlayCard handles all the checks, targeting switches,
            // and final resolution.
            state.PlayCard(_card);
        }
    }
}
