using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Contexts
{
    public class TurnContext
    {
        public Player ActivePlayer { get; private set; }
        private readonly Dictionary<CardAspect, int> _playedAspectCounts;
        private readonly IGameLogger _logger;

        // Each entry represents 1 promotion point provided by a source Card, plus whether
        // that specific point is voluntarily declinable (CardEffect.PromotionCreditIsOptional
        // - e.g. Cultist of Myrkul/Zuggtmoy's "up to N", as opposed to core_noble's plain,
        // mandatory "promote a card played this turn").
        private readonly record struct PromotionCredit(Card Source, bool IsOptional);
        private readonly List<PromotionCredit> _promotionCredits;

        // --- Action Sequencing ---
        private int _actionSequence;
        private readonly List<ExecutedAction> _actionHistory = new();

        public IReadOnlyDictionary<CardAspect, int> PlayedAspectCounts => _playedAspectCounts;
        public IReadOnlyList<ExecutedAction> ActionHistory => _actionHistory;

        // Expose count for UI checks
        public int PendingPromotionsCount => _promotionCredits.Count;

        public TurnContext(Player activePlayer, IGameLogger logger)
        {
            ActivePlayer = activePlayer;
            _playedAspectCounts = new Dictionary<CardAspect, int>();
            _promotionCredits = new List<PromotionCredit>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        public void AddPromotionCredit(Card source, int amount, bool isOptional = false)
        {
            for (int i = 0; i < amount; i++)
            {
                _promotionCredits.Add(new PromotionCredit(source, isOptional));
            }
        }

        /// <summary>
        /// Checks if there is a promotion point available that did NOT come from the target card.
        /// </summary>
        public bool HasValidCreditFor(Card target)
        {
            // We need at least one credit where CreditSource != Target
            return _promotionCredits.Any(credit => credit.Source != target);
        }

        /// <summary>
        /// Consumes a credit suitable for the target.
        /// Prioritizes credits from other cards.
        /// </summary>
        public void ConsumeCreditFor(Card target)
        {
            // Find the first credit that is NOT from the target
            int index = _promotionCredits.FindIndex(credit => credit.Source != target);

            if (index >= 0)
            {
                _promotionCredits.RemoveAt(index);
            }
            else
            {
                // Fallback (Should be prevented by HasValidCreditFor check,
                // but handles forced cases if necessary)
                if (_promotionCredits.Count > 0)
                    _promotionCredits.RemoveAt(0);
            }
        }

        /// <summary>
        /// True whenever every currently-outstanding promotion credit is voluntarily
        /// declinable (CardEffect.PromotionCreditIsOptional - "up to N", e.g. Cultist of
        /// Myrkul) - vacuously true once no credits remain at all. False if even ONE
        /// outstanding credit is the plain, mandatory shape (e.g. core_noble's "promote a card
        /// played this turn"), which must still be resolved before the player can stop.
        /// PromoteInputMode reads this to decide whether a Right-click/Escape may end the
        /// redemption flow early (forfeiting whatever's left) instead of refusing outright.
        /// </summary>
        public bool CanDeclineRemainingPromotions => _promotionCredits.All(credit => credit.IsOptional);

        /// <summary>
        /// Forfeits every currently-outstanding promotion credit - called once
        /// CanDeclineRemainingPromotions has been confirmed true, so this is only ever reached
        /// when every remaining credit was voluntarily declinable to begin with. Harmless even
        /// without this call (a fresh TurnContext replaces this one at end of turn regardless),
        /// but explicit rather than relying on that turn-boundary side effect.
        /// </summary>
        public void ForfeitRemainingPromotions()
        {
            _promotionCredits.Clear();
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

            _logger.Log($"[Action {action.Sequence}] {ActivePlayer.DisplayName}: {summary}", LogChannel.Info);
        }
    }
}


