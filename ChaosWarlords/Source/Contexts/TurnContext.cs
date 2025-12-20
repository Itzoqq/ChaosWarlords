using System.Collections.Generic;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Contexts
{
    /// <summary>
    /// Holds the transient state for the current turn.
    /// This object is recreated every time a player starts their turn.
    /// </summary>
    public class TurnContext
    {
        public Player ActivePlayer { get; private set; }

        // Internal storage is private
        private readonly Dictionary<CardAspect, int> _playedAspectCounts;

        // Public access is Read-Only
        public IReadOnlyDictionary<CardAspect, int> PlayedAspectCounts => _playedAspectCounts;

        public TurnContext(Player activePlayer)
        {
            ActivePlayer = activePlayer;
            _playedAspectCounts = new Dictionary<CardAspect, int>();
        }

        public void RecordPlayedCard(CardAspect aspect)
        {
            if (_playedAspectCounts.ContainsKey(aspect))
                _playedAspectCounts[aspect]++;
            else
                _playedAspectCounts[aspect] = 1;
        }

        public int GetAspectCount(CardAspect aspect)
        {
            return _playedAspectCounts.GetValueOrDefault(aspect, 0);
        }
    }
}