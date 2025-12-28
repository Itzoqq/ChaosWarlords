using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    public interface IMarketManager
    {
        bool TryBuyCard(Player player, Card card, IPlayerStateManager stateManager);
        List<Card> MarketRow { get; }
    }
}



