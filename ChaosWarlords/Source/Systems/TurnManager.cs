using System.Collections.Generic;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    // System to manage player context and turn flow
    public class TurnManager : ITurnManager
    {
        public List<Player> Players { get; private set; }
        private int _currentPlayerIndex = 0;

        // This is the active player all other systems (UI, Map, Action) will query
        public Player ActivePlayer => Players[_currentPlayerIndex];

        // Dictionary for "Focus" mechanic (tracks aspects played this turn)
        public Dictionary<CardAspect, int> PlayedAspectCounts { get; private set; }

        public TurnManager(List<Player> players)
        {
            Players = players;
            // The list should already be sorted by turn order (e.g., Red then Blue)
            ResetTurnContext();
        }

        private void ResetTurnContext()
        {
            // Reset counters for the active player (important for 'Focus' mechanic)
            PlayedAspectCounts = new Dictionary<CardAspect, int>();
        }

        public void PlayCard(Card card)
        {
            // Future logic for 'Focus' tracking
            if (PlayedAspectCounts.ContainsKey(card.Aspect))
            {
                PlayedAspectCounts[card.Aspect]++;
            }
            else
            {
                PlayedAspectCounts[card.Aspect] = 1;
            }
        }

        // Called when the active player presses 'Enter' or clicks the 'End Turn' button
        public void EndTurn()
        {
            // 1. Map Cleanup (The map hands out VP)
            // Note: The player cleanup (Discard/Draw) still happens in GameplayState.EndTurn for now,
            // but for cleaner architecture, you should eventually move ActivePlayer.CleanUpTurn() 
            // and ActivePlayer.DrawCards(5) here, and update GameplayState.EndTurn() to just call TurnManager.EndTurn().

            // 2. Switch to the next player
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;

            // 3. Reset the context for the new player
            ResetTurnContext();
        }
    }
}