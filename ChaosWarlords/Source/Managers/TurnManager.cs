using System;
using System.Collections.Generic;
using System.Linq;
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

            // Randomize Player Order
            Players = players.OrderBy(x => Guid.NewGuid()).ToList();
            StartTurn();
        }

        private void StartTurn()
        {
            Player nextPlayer = Players[_currentPlayerIndex];

            // Create a fresh context for the new turn
            CurrentTurnContext = new TurnContext(nextPlayer);

            // New Rule: Distribute Rewards at START of Turn
            // We need access to MapManager. Since TurnManager doesn't hold MapManager, 
            // we should invoke this where the turn starts or inject it.
            // BUT TurnManager doesn't reference MapManager in constructor.
            // MatchManager calls TurnManager.EndTurn().
            // We should do it in MatchManager.EndTurn() right before or after switching players?
            // Actually, TurnManager.StartTurn is private.
            
            // Re-evaluating Design:
            // MatchManager orchestrates the game loop.
            // MatchManager.EndTurn:
            //   1. Cleanup Old Player
            //   2. TurnManager.EndTurn() -> Switches Index, Creates Context.
            
            // So MatchManager is the right place to trigger "Start of Turn Actions" for the NEW player.
            // Let's modify MatchManager to trigger rewards AFTER switching the player.
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