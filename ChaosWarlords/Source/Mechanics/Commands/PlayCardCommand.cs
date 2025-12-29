using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Commands
{
    public class PlayCardCommand : IGameCommand
    {
        public Card Card { get; }
        public PlayCardCommand(Card card) { Card = card; }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("PlayCard", $"Played {Card.Name}");
            // We can now call the PlayCard logic directly on the state interface
            // The logic inside PlayCard handles all the checks, targeting switches,
            // and final resolution.
            state.PlayCard(Card);
        }
    }
}



