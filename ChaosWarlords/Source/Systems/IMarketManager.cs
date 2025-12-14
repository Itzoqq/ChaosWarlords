using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
{
    public interface IMarketManager
    {
        bool TryBuyCard(Player player, Card card);
        System.Collections.Generic.List<Card> MarketRow { get; }
        void Update(Microsoft.Xna.Framework.Vector2 cursorPosition);
    }
}