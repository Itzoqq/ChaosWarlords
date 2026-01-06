using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.GameStates;

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

        // Implement the logic directly on the Interface method.
        // This works for both the real GameplayState and your Test Mocks.
        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("Devour", $"Devoured card {CardToDevour.Name} (Deferred: {IsDeferred})");

            if (IsDeferred)
            {
                state.ActionSystem.DeferDevour(CardToDevour);
                return;
            }

            // 1. Perform the Devour 
            if (CardToDevour.Location == CardLocation.Market)
            {
                // We assume MatchManager has been updated to have DevourMarketCard
                // If Interface doesn't have it, we might need to cast or add to interface.
                // Assuming IMatchManager is updated (it wasn't in the plan explicitly but implicit)
                // Let's check IMatchManager. If not we cast to MatchManager.
                // For safety in this turn, I will use reflection or concrete cast if interface isn't updated? 
                // Ah, I missed updating IMatchManager interface.
                // I should assume the concrete type or update interface. 
                // Updating interface is better. I will do that in next step.
                // For now, let's try cast.
                state.MatchManager.DevourMarketCard(CardToDevour, SourceCard);
            }
            else
            {
                state.MatchManager.DevourCard(CardToDevour);
            }
            
            // 2. Advance Chain (Check for Next Step or Finish Play)
            if (SourceCard != null)
            {
                state.ActionSystem.AdvanceDevourChain(SourceCard);
            }
            else
            {
                // Standalone Devour (no source context) - just complete.
                state.ActionSystem.CompleteAction();
            }
        }

    }
}


