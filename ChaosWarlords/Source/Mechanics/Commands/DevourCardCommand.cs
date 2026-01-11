using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class DevourCardCommand : IGameCommand
    {
        public Card CardToDevour { get; }
        public Card? SourceCard { get; set; } // Optional: For "Replace With Source" mechanic

        public bool IsDeferred { get; set; }

        public DevourCardCommand(Card card)
        {
            CardToDevour = card;
        }

        public bool Validate(MatchContext context)
        {
            // Complex to fully validate without finding the card again, but assuming CardToDevour is valid instance:
            return true;
        }

        public void Execute(MatchContext context)
        {
            if (IsDeferred)
            {
                context.ActionSystem.DeferDevour(CardToDevour);
            }
            else if (CardToDevour.Location == CardLocation.Market)
            {
                context.MatchManager.DevourMarketCard(CardToDevour, SourceCard);
                context.ActionSystem.CompleteAction();
            }
            else
            {
                context.MatchManager.DevourCard(CardToDevour);
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
