using ChaosWarlords.Source.Entities;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Systems
{
    public interface IMarketManager
    {
        bool TryBuyCard(Player player, Card card);
        List<Card> MarketRow { get; }
    }
}