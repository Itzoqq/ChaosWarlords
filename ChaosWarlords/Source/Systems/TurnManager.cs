using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;

namespace ChaosWarlords.Source.Systems
{
    public class TurnManager : ITurnManager
    {
        public List<Player> Players { get; private set; }
        private int _currentPlayerIndex = 0;

        // The distinct data object for the current turn
        public TurnContext CurrentTurnContext { get; private set; }

        // Convenience property
        public Player ActivePlayer => CurrentTurnContext?.ActivePlayer ?? Players[_currentPlayerIndex];

        public TurnManager(List<Player> players)
        {
            // Industry Standard: "Guard Clauses"
            if (players == null || players.Count == 0)
            {
                throw new ArgumentException("TurnManager requires at least one player.", nameof(players));
            }

            Players = players;
            StartTurn();
        }

        private void StartTurn()
        {
            Player nextPlayer = Players[_currentPlayerIndex];

            // Create a fresh context for the new turn
            CurrentTurnContext = new TurnContext(nextPlayer);
        }

        public void PlayCard(Card card)
        {
            // Delegate state tracking to the context
            CurrentTurnContext.RecordPlayedCard(card.Aspect);
        }

        public void EndTurn()
        {
            // 1. Advance Player Index
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;

            // 2. Start the next turn (Creates new Context)
            StartTurn();
        }
    }
}