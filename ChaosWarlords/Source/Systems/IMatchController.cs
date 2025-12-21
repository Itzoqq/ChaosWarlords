using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
{
    public interface IMatchController
    {
        void PlayCard(Card card);
        void DevourCard(Card card);
        void ResolveCardEffects(Card card, bool hasFocus);
        void MoveCardToPlayed(Card card);
        bool CanEndTurn(out string reason);
        void EndTurn();
    }
}