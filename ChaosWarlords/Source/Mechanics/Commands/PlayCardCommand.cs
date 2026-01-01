using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
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

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("PlayCard", $"Played {Card.Name} (Bypass: {BypassChecks})");
            
            if (BypassChecks)
            {
                // Directly execute play logic, bypassing CardPlaySystem's targeting checks
                // This is used for Pre-Commit flows where targeting is already handled
                state.MatchManager.PlayCard(Card);
            }
            else
            {
                state.PlayCard(Card);
            }
        }
    }
}



