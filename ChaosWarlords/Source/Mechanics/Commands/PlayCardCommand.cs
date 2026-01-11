using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Commands
{
    public class PlayCardCommand : IGameCommand
    {
        public Card Card { get; }
        public bool BypassChecks { get; }

        public PlayCardCommand(Card card, bool bypassChecks = false)
        {
            Card = card;
            BypassChecks = bypassChecks;
        }

        public bool Validate(MatchContext context)
        {
            // Can Play if in hand
            var player = context.TurnManager.ActivePlayer;
            return player.Hand.Contains(Card);
        }

        public void Execute(MatchContext context)
        {
            context.MatchManager.PlayCard(Card);
        }
    }
}
