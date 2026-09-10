using System.Linq;
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
        // mandatory "promote a card played this turn"), an optional aspect filter
        // (CardEffect.RequiredPromotionAspect - "promote an Obedience card played this turn",
        // Air/Fire/Water Elemental Myrmidon), and an optional creature-type filter
        // (CardEffect.RequiredPromotionCreatureType - "promote an Undead card played this turn",
        // High Priest of Myrkul). Null RequiredAspect/RequiredCreatureType means no filter at
        // all on that dimension.
        private readonly record struct PromotionCredit(Card Source, bool IsOptional, CardAspect? RequiredAspect = null, CardCreatureType? RequiredCreatureType = null);
        private readonly List<PromotionCredit> _promotionCredits;

        // "...promote ANY NUMBER of Undead cards played this turn" (High Priest of Myrkul) -
        // registered by AddUnboundedPromotionCredit (via CardEffectApplier.ApplyPromote when
        // CardEffect.PromoteAnyNumber is set) and materialized into real, redeemable
        // PromotionCredit entries by ExpandPendingUnboundedCredits the first time this turn's
        // credits are actually queried/consumed - see that method's own doc comment for why
        // deferring the count computation to that point (rather than at registration time) is
        // what makes "any number... played this turn" correct even for cards played AFTER this
        // one resolves.
        private readonly List<(Card Source, CardCreatureType? RequiredCreatureType)> _pendingUnboundedCredits = new();

        // "...THEN gain 1 VP for every 3 cards in your inner circle" (Blue Dragon) - effects
        // registered by AddPromotionCredit (when a Promote effect authored a
        // CardEffect.PromotionCompletionEffect) and drained/applied once by MatchManager.EndTurn,
        // once this turn's whole deferred promotion-credit redemption has concluded (every
        // credit either consumed via a real PromoteCommand or explicitly declined) - never
        // sooner, so a dynamic amount like "cards in your inner circle" is computed AFTER the
        // redemption's own promotions have actually happened. A list, not a dictionary keyed by
        // source: 2 distinct physical copies of the same completion-effect-bearing card played
        // the same turn (unusual, but not prevented by anything) each register and each fire
        // independently. Not tied to per-credit-unit consumption tracking at all - unlike
        // Vampire's OnSuccess-off-PromoteFromPile chain, this deferred flow has no reliable
        // "this specific credit was just resolved" signal that also fires correctly during
        // replay (PromoteInputMode, which calls ConsumeCreditFor/ForfeitRemainingPromotions,
        // deliberately never runs during replay - see GameplayState.SwitchToTargetingMode) - only
        // EndTurn() itself (driven by the always-recorded, always-replayed EndTurnCommand) fires
        // identically in both live play and replay.
        private readonly List<(Card Source, CardEffect Effect)> _promotionCompletionEffects = new();

        // --- Action Sequencing ---
        private int _actionSequence;
        private readonly List<ExecutedAction> _actionHistory = new();

        public IReadOnlyDictionary<CardAspect, int> PlayedAspectCounts => _playedAspectCounts;
        public IReadOnlyList<ExecutedAction> ActionHistory => _actionHistory;

        /// <summary>
        /// Expose count for UI checks. Deliberately does NOT call ExpandPendingUnboundedCredits
        /// - unlike HasValidCreditFor/ConsumeCreditFor/ForfeitUnsatisfiableCredits/
        /// CanDeclineRemainingPromotions below, this property is also read from
        /// MatchManager.CanEndTurn, which is reachable SPECULATIVELY (e.g. clicking "End Turn"
        /// while cards are still unplayed opens a confirmation popup the player can cancel and
        /// keep playing). Expanding here would risk permanently freezing an unbounded credit's
        /// count against an incomplete PlayedCards set before the turn is actually over. Every
        /// genuine redemption entry point (UIEventMediator.HandleEndTurnWithPromotionCheck)
        /// always calls ForfeitUnsatisfiableCredits - which DOES expand - before ever reading
        /// this property, so by the time it matters here, expansion has already happened via
        /// that call. A future caller of this property must not assume it reflects unbounded
        /// credits unless something has already forced expansion (ForfeitUnsatisfiableCredits,
        /// HasValidCreditFor, or ConsumeCreditFor) earlier in the same turn.
        /// </summary>
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

        public void AddPromotionCredit(Card source, int amount, bool isOptional = false, CardAspect? requiredAspect = null, CardCreatureType? requiredCreatureType = null)
        {
            for (int i = 0; i < amount; i++)
            {
                _promotionCredits.Add(new PromotionCredit(source, isOptional, requiredAspect, requiredCreatureType));
            }
        }

        /// <summary>
        /// Registers an "any number" Promote credit (High Priest of Myrkul: "...promote ANY
        /// NUMBER of Undead cards played this turn") - the actual count isn't known yet (more
        /// matching cards could still be played later this same turn), so nothing is added to
        /// _promotionCredits here. See ExpandPendingUnboundedCredits for when/how this actually
        /// materializes into redeemable credits. Null requiredCreatureType means no filter at
        /// all - "promote any number of cards played this turn," matching how null means
        /// "unfiltered" everywhere else on PromotionCredit (RequiredAspect/RequiredCreatureType)
        /// - NOT the same as CardCreatureType.None, which means "not tracked as any specific
        /// type" and would wrongly exclude every typed card instead of including all of them.
        /// </summary>
        public void AddUnboundedPromotionCredit(Card source, CardCreatureType? requiredCreatureType)
        {
            _pendingUnboundedCredits.Add((source, requiredCreatureType));
        }

        /// <summary>
        /// Materializes every pending unbounded credit into real, individually-redeemable
        /// PromotionCredit entries - exactly one per currently-eligible played card (excluding
        /// the credit's own source), always optional (an unbounded "any number" credit can never
        /// be mandatory). Idempotent (a no-op once _pendingUnboundedCredits is empty) and called
        /// from HasValidCreditFor/ConsumeCreditFor/ForfeitUnsatisfiableCredits/
        /// CanDeclineRemainingPromotions - deliberately NOT from PendingPromotionsCount (see that
        /// property's own doc comment for why) - rather than requiring callers to remember a
        /// separate expansion step. Safe to defer this late specifically because every real
        /// caller of the 4 methods above only ever runs once the player has committed to ending
        /// their turn (UIEventMediator.HandleEndTurnWithPromotionCheck, GameplayInputCoordinator.
        /// CreatePromoteMode), by which point ActivePlayer.PlayedCards for this turn is already
        /// final - nothing more can be played after redemption starts. 2+ High Priests played
        /// the same turn would each expand against the SAME eligible pool (over-crediting beyond
        /// the real number of distinct targets), but that's harmless: the extra credits become
        /// unsatisfiable the moment the real targets are all promoted and get swept by
        /// ForfeitUnsatisfiableCredits like any other stranded optional credit.
        /// </summary>
        private void ExpandPendingUnboundedCredits()
        {
            if (_pendingUnboundedCredits.Count == 0)
            {
                return;
            }

            foreach (var (source, requiredCreatureType) in _pendingUnboundedCredits)
            {
                int eligibleCount = ActivePlayer.PlayedCards.Count(c => c != source && (!requiredCreatureType.HasValue || c.CreatureType == requiredCreatureType.Value));
                AddPromotionCredit(source, eligibleCount, isOptional: true, requiredCreatureType: requiredCreatureType);
            }

            _pendingUnboundedCredits.Clear();
        }

        /// <summary>
        /// Registers a nested effect (Blue Dragon's "...then gain 1 VP for every 3 cards in your
        /// inner circle") to be applied once, later, by MatchManager.EndTurn - see
        /// _promotionCompletionEffects' own doc comment for why EndTurn is the correct/only
        /// safe firing point.
        /// </summary>
        public void RegisterPromotionCompletionEffect(Card source, CardEffect effect)
        {
            _promotionCompletionEffects.Add((source, effect));
        }

        /// <summary>
        /// Returns every completion effect registered this turn and clears the list - called
        /// exactly once per turn, by MatchManager.EndTurn, so a given registration is never
        /// applied twice.
        /// </summary>
        public IReadOnlyList<(Card Source, CardEffect Effect)> DrainPromotionCompletionEffects()
        {
            if (_promotionCompletionEffects.Count == 0)
            {
                return Array.Empty<(Card Source, CardEffect Effect)>();
            }

            var drained = _promotionCompletionEffects.ToArray();
            _promotionCompletionEffects.Clear();
            return drained;
        }

        /// <summary>
        /// A credit can promote <paramref name="target"/> if it didn't come from that same
        /// card (a card can never promote itself) AND, if the credit carries an aspect filter
        /// (RequiredAspect - Air/Fire/Water Elemental Myrmidon) or a creature-type filter
        /// (RequiredCreatureType - High Priest of Myrkul), <paramref name="target"/> matches it.
        /// Shared by HasValidCreditFor/ConsumeCreditFor so the 2 checks can never diverge.
        /// </summary>
        private static bool CreditAllows(PromotionCredit credit, Card target)
        {
            if (credit.Source == target) return false;
            if (credit.RequiredAspect.HasValue && target.Aspect != credit.RequiredAspect.Value) return false;
            if (credit.RequiredCreatureType.HasValue && target.CreatureType != credit.RequiredCreatureType.Value) return false;
            return true;
        }

        /// <summary>
        /// Checks if there is a promotion point available that did NOT come from the target
        /// card, and (if that credit carries a filter) matches the target accordingly.
        /// </summary>
        public bool HasValidCreditFor(Card target)
        {
            ExpandPendingUnboundedCredits();
            return _promotionCredits.Any(credit => CreditAllows(credit, target));
        }

        /// <summary>
        /// Consumes a credit suitable for the target - among every credit satisfying
        /// CreditAllows, prefers one carrying a filter (RequiredAspect or RequiredCreatureType)
        /// over a fully unfiltered one. An unfiltered credit can still promote ANY future
        /// target, so preserving it keeps the most flexibility for whatever's promoted next; a
        /// filtered credit only ever helps a narrower pool anyway, so spending it now while it
        /// still matches loses nothing. This reduces (without a hard matching-theoretic
        /// guarantee of eliminating) how often 2+ filtered mandatory credits sharing a scarce
        /// pool of matching targets end up stranding each other purely due to click/consumption
        /// order - see ForfeitUnsatisfiableCredits for the actual safety net against that.
        /// </summary>
        public void ConsumeCreditFor(Card target)
        {
            ExpandPendingUnboundedCredits();
            int index = FindPreferredCreditIndexFor(target);

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

        // True for any credit carrying a filter (aspect or creature-type) - see
        // ConsumeCreditFor's own doc comment for why a filtered credit is preferred over an
        // unfiltered one once both allow the same target.
        private static bool IsFiltered(PromotionCredit credit) => credit.RequiredAspect.HasValue || credit.RequiredCreatureType.HasValue;

        /// <summary>
        /// Finds the index of the credit ConsumeCreditFor should spend on <paramref name="target"/>
        /// - the first filtered credit that allows it, or (if none) the first unfiltered one
        /// that does. Returns -1 if nothing in _promotionCredits allows this target at all.
        /// </summary>
        private int FindPreferredCreditIndexFor(Card target)
        {
            int fallbackIndex = -1;

            for (int i = 0; i < _promotionCredits.Count; i++)
            {
                if (!CreditAllows(_promotionCredits[i], target)) continue;
                if (IsFiltered(_promotionCredits[i])) return i;
                if (fallbackIndex == -1) fallbackIndex = i;
            }

            return fallbackIndex;
        }

        /// <summary>
        /// Forfeits any outstanding credit for which NONE of <paramref name="playedCards"/> can
        /// satisfy it (CreditAllows) - a credit can become unsatisfiable this way even while
        /// still mandatory, e.g. 2 aspect-filtered mandatory credits (Air + Fire Elemental
        /// Myrmidon, both requiring an Obedience card) competing over a single shared Obedience
        /// card played that same turn: once one credit consumes it, the other has zero
        /// remaining legal targets and would otherwise soft-lock the mandatory redemption flow
        /// forever (CanDeclineRemainingPromotions stays false since it's still mandatory, so
        /// Right-click/Escape keeps refusing, and every remaining click keeps getting rejected
        /// by HasValidCreditFor - no path back to Normal at all). Matches this codebase's own
        /// established "gracefully resolve early instead of waiting for an impossible target"
        /// philosophy elsewhere (e.g. ActionExecutionEngine.ShouldRepeatCurrentEffect). Safe to
        /// call on optional credits too - one that's genuinely unsatisfiable could never have
        /// been redeemed anyway, so dropping it changes nothing observable except no longer
        /// inflating PendingPromotionsCount. Call both BEFORE a redemption session opens (a
        /// credit can be dead on arrival, e.g. zero Obedience cards played at all) and again
        /// after each single credit is consumed (consuming one can strand a sibling).
        /// </summary>
        /// <returns>How many credits were forfeited, so a caller tracking its own "credits left
        /// to resolve" counter (e.g. PromoteInputMode) can stay in sync.</returns>
        public int ForfeitUnsatisfiableCredits(IReadOnlyCollection<Card> playedCards)
        {
            ExpandPendingUnboundedCredits();
            return _promotionCredits.RemoveAll(credit => !playedCards.Any(card => CreditAllows(credit, card)));
        }

        /// <summary>
        /// True whenever every currently-outstanding promotion credit is voluntarily
        /// declinable (CardEffect.PromotionCreditIsOptional - "up to N", e.g. Cultist of
        /// Myrkul; or a High Priest of Myrkul unbounded credit, always optional by
        /// construction) - vacuously true once no credits remain at all. False if even ONE
        /// outstanding credit is the plain, mandatory shape (e.g. core_noble's "promote a card
        /// played this turn"), which must still be resolved before the player can stop.
        /// PromoteInputMode reads this to decide whether a Right-click/Escape may end the
        /// redemption flow early (forfeiting whatever's left) instead of refusing outright.
        /// </summary>
        public bool CanDeclineRemainingPromotions
        {
            get
            {
                ExpandPendingUnboundedCredits();
                return _promotionCredits.All(credit => credit.IsOptional);
            }
        }

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


