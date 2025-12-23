using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
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