using System;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Contexts
{
    public class TurnContext
    {
        public Player ActivePlayer { get; private set; }
        private readonly Dictionary<CardAspect, int> _playedAspectCounts;

        // --- CHANGED: Track credits by Source Card ---
        // Each entry represents 1 promotion point provided by 'Card'
        private readonly List<Card> _promotionCredits;

        // --- Action Sequencing ---
        private int _actionSequence;
        private readonly List<ExecutedAction> _actionHistory = new();

        public IReadOnlyDictionary<CardAspect, int> PlayedAspectCounts => _playedAspectCounts;
        public IReadOnlyList<ExecutedAction> ActionHistory => _actionHistory;

        // Expose count for UI checks
        public int PendingPromotionsCount => _promotionCredits.Count;

        public TurnContext(Player activePlayer)
        {
            ActivePlayer = activePlayer;
            _playedAspectCounts = new Dictionary<CardAspect, int>();
            _promotionCredits = new List<Card>();
        }

        public void RecordPlayedCard(CardAspect aspect)
        {
            if (_playedAspectCounts.TryGetValue(aspect, out int count))
                _playedAspectCounts[aspect] = count + 1;
            else
                _playedAspectCounts[aspect] = 1;
        }

        public int GetAspectCount(CardAspect aspect)
        {
            return _playedAspectCounts.GetValueOrDefault(aspect, 0);
        }

        // --- Credit Management ---

        public void AddPromotionCredit(Card source, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                _promotionCredits.Add(source);
            }
        }

        /// <summary>
        /// Checks if there is a promotion point available that did NOT come from the target card.
        /// </summary>
        public bool HasValidCreditFor(Card target)
        {
            // We need at least one credit where CreditSource != Target
            return _promotionCredits.Any(source => source != target);
        }

        /// <summary>
        /// Consumes a credit suitable for the target.
        /// Prioritizes credits from other cards.
        /// </summary>
        public void ConsumeCreditFor(Card target)
        {
            // Find the first credit that is NOT from the target
            var validCredit = _promotionCredits.FirstOrDefault(source => source != target);

            if (validCredit != null)
            {
                _promotionCredits.Remove(validCredit);
            }
            else
            {
                // Fallback (Should be prevented by HasValidCreditFor check, 
                // but handles forced cases if necessary)
                if (_promotionCredits.Count > 0)
                    _promotionCredits.RemoveAt(0);
            }
        }

        // --- Action Sequencing ---

        public int GetNextSequence()
        {
            return _actionSequence++;
        }

        public void RecordAction(string actionType, string summary)
        {
            var action = new ExecutedAction(
                GetNextSequence(),
                actionType,
                ActivePlayer.PlayerId,
                summary,
                DateTime.Now // Local time for logging, sequence is primary for logic
            );
            _actionHistory.Add(action);

            GameLogger.Log($"[Action {action.Sequence}] {ActivePlayer.DisplayName}: {summary}", LogChannel.Info);
        }
    }
}


