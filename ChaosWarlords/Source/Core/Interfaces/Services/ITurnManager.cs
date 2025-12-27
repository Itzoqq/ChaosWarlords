using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    public interface ITurnManager
    {
        // Publicly accessible list of all players
        List<Player> Players { get; }

        // The player whose turn it currently is (Shortcut to CurrentTurnContext.ActivePlayer)
        Player ActivePlayer { get; }

        // The data object for the current turn
        TurnContext CurrentTurnContext { get; }

        // Actions
        void PlayCard(Card card);
        void EndTurn();
    }
}



