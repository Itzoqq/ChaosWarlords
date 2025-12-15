using System.Collections.Generic;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public interface ITurnManager
    {
        // Publicly accessible list of all players
        List<Player> Players { get; }

        // The player whose turn it currently is
        Player ActivePlayer { get; }

        // Tracks played aspects for features like 'Focus' (from Tyrants of the Underdark rules)
        Dictionary<CardAspect, int> PlayedAspectCounts { get; }

        // Actions
        void PlayCard(Card card);
        void EndTurn();
    }
}