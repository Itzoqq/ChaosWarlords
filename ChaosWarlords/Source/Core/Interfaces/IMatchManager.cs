using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
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