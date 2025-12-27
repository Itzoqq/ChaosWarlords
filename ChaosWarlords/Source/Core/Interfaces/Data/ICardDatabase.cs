using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    public interface ICardDatabase
    {
        /// <summary>
        /// Returns all cards available for the Market Deck.
        /// </summary>
        List<Card> GetAllMarketCards();

        /// <summary>
        /// Retrieves a specific card definition by its ID (useful for networking/modding).
        /// </summary>
        Card GetCardById(string id);
    }
}



