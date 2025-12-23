using ChaosWarlords.Source.Entities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Systems
{
    public interface IMarketManager
    {
        bool TryBuyCard(Player player, Card card);
        List<Card> MarketRow { get; }
    }
}