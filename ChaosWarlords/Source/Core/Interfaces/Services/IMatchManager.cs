using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    public interface IMatchManager
    {
        void PlayCard(Card card);
        void DevourCard(Card card);
        void MoveCardToPlayed(Card card);
        bool CanEndTurn(out string reason);
        void EndTurn();
    }
}



