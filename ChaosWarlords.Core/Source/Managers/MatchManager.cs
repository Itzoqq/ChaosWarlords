using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Source.Managers
{
    public class MatchManager : IMatchManager
    {
        private readonly MatchContext _context;
        private readonly IGameLogger _logger;
        private readonly TurnLifecycleSubsystem _turnLifecycle;

        public MatchManager(MatchContext context, IGameLogger logger, IVictoryManager victoryManager)
        {
            _context = context;
            _context.MatchManager = this;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // End-turn/reactive-discard/victory lifecycle lives in its own class - see
            // TurnLifecycleSubsystem's own doc comment and planning.txt TIER 2 item 13. Neither
            // half calls into the other, so unlike MapActionSubsystem's split off ActionSystem,
            // this needed no back-reference and no new interface members.
            _turnLifecycle = new TurnLifecycleSubsystem(context, logger, victoryManager);
        }

        public void PlayCard(Card card)
        {
            // --- 1. PRE-CALCULATION (SNAPSHOT) ---
            // We must calculate Focus BEFORE moving the card to 'Played' or modifying the turn stats.
            // Focus Condition: Played another card of same aspect OR Reveal one from hand.

            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool playedAnother = currentCount > 0;

            // Check hand for a DIFFERENT card of the same aspect
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            bool hasFocus = playedAnother || canRevealFromHand;

            // --- 2. STATE MUTATION ---

            // Verify Ownership: Cannot play a card that isn't in your hand!
            if (!_context.ActivePlayer.Hand.Contains(card))
            {
                _logger.Log($"Attempted to play card {card.Name} which is NOT in active player's hand.", LogChannel.Error);
                return;
            }

            // Use PlayerStateManager for centralized mutation
            _context.PlayerStateManager.PlayCard(_context.ActivePlayer, card);

            // Diagnostic Logging: Log hand contents after play
            var remainingCards = string.Join(", ", _context.ActivePlayer.Hand.Select(c => $"{c.Name}({c.Id})"));
            _logger.Log($"[Hand State] After playing {card.Name}, Hand ({_context.ActivePlayer.Hand.Count}): [{remainingCards}]", LogChannel.Info);

            // Snapshot BEFORE resolving effects, not just when a targeting UI actually opens -
            // a card shaped "automatic mutation, THEN mandatory targeting" (e.g. Matron
            // Mother, Cranium Rats) already mutates state before StartTargeting/
            // EnterTargetingState would otherwise take this snapshot. See
            // EnsureTargetingSnapshot's doc comment and planning.txt.
            _context.ActionSystem.EnsureTargetingSnapshot();

            // --- 3. RESOLVE EFFECTS (The Missing Link) ---
            // Now that the card is "played", we trigger its game logic.
            // We pass the 'hasFocus' snapshot we calculated earlier.
            // ResolveEffects pushes the card's effects AND processes the stack itself (see its
            // own "Start Stack Processing" step) - do NOT also call ProcessStack() here. Doing
            // so was a real, pre-existing bug (predates this session - see planning.txt): the
            // second call re-processed the same still-pending top-of-stack effect a second
            // time, which for an optional/blocking effect meant ProcessOptionalEffect fired
            // twice per single card play - doubling the interaction-request/popup-accept flow
            // and, for Devour->Supplant chains, leaving an extra un-consumed TargetingSupplant
            // effect buried in the stack that would resurface and force Supplant targeting on
            // an unrelated later card play.
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus, _logger);

            // --- 4. UPDATE STATS ---
            // Finally, register the card with the turn manager to update Aspect counts for future Focus checks.
            _context.TurnManager.PlayCard(card);
        }

        public void DevourCard(Card card, Card? sourceCard = null)
        {
            var player = _context.ActivePlayer;
            var validCard = FindCardInPlayerCollections(card, player);

            if (validCard == null)
            {
                _logger.Log($"DevourCard Failed: Card {card.Name} ({card.Id}) not found in player's collections.", LogChannel.Warning);
                return;
            }

            _context.PlayerStateManager.DevourCard(_context.ActivePlayer, validCard);

            if (validCard.Location == CardLocation.Void)
            {
                _context.VoidPile.Add(validCard);
            }

            if (sourceCard != null && ShouldResumeDevourChain(sourceCard))
            {
                ResumeDevourChain(sourceCard);
            }
        }

        public void PlayCardFromMarket(Card marketCard, Card sourceCard)
        {
            if (marketCard.Location != CardLocation.Market)
            {
                _logger.Log($"PlayCardFromMarket Failed: {marketCard.Name} is not currently in the Market.", LogChannel.Warning);
                return;
            }

            // "As if it was in your hand" - Focus is computed off the MARKET CARD's own
            // aspect, not sourceCard's (Ulitharid's) - matches PlayCard's own Focus snapshot,
            // just keyed to a different card.
            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(marketCard.Aspect);
            bool playedAnother = currentCount > 0;
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == marketCard.Aspect);
            bool hasFocus = playedAnother || canRevealFromHand;

            // One-shot: once marketCard's own effect chain fully resolves (whether instantly
            // or after several frames of targeting), remove it from the market row and send
            // it to Void - the standard Devour-from-Market removal (see
            // CheckAndReplaceMarketCard's else branch), never PlayerStateManager.PlayCard
            // (requires Hand.Contains(card), which a market card never satisfies - would
            // silently no-op) and never Player.PlayedCards (would make CleanUpTurn() try to
            // discard a card that's about to be devoured).
            EventHandler? onMarketCardResolved = null;
            onMarketCardResolved = (s, e) =>
            {
                _context.ActionSystem.OnActionCompleted -= onMarketCardResolved;
                _context.MarketManager.RemoveCard(marketCard);
                marketCard.Location = CardLocation.Void;
                _context.VoidPile.Add(marketCard);
                _logger.Log($"{marketCard.Name} devoured after being played from the Market by {sourceCard.Name}.", LogChannel.Info);
            };
            _context.ActionSystem.OnActionCompleted += onMarketCardResolved;

            // Snapshot BEFORE resolving effects - see PlayCard's matching call and
            // EnsureTargetingSnapshot's doc comment.
            _context.ActionSystem.EnsureTargetingSnapshot();

            CardEffectProcessor.ResolveEffects(marketCard, _context, hasFocus, _logger);

            // Aspect-focus tracking for the market card's own aspect, matching "as if in hand".
            _context.TurnManager.PlayCard(marketCard);
        }

        public void DevourMarketCard(Card targetCard, Card? sourceCard)
        {
            if (targetCard.Location != CardLocation.Market)
            {
                _logger.Log("DevourMarketCard Failed: Selected card is not in Market!", LogChannel.Warning);
                return;
            }

            CheckAndReplaceMarketCard(targetCard, sourceCard);

            if (sourceCard != null && ShouldResumeDevourChain(sourceCard))
            {
                ResumeDevourChain(sourceCard);
            }
        }

        private static Card? FindCardInPlayerCollections(Card card, Player player)
        {
            if (player.Hand.Contains(card) || player.InnerCircle.Contains(card) || player.PlayedCards.Contains(card))
            {
                return card;
            }

            var instance = player.Hand.FirstOrDefault(c => c.Id == card.Id);
            if (instance != null) return instance;

            instance = player.InnerCircle.FirstOrDefault(c => c.Id == card.Id);
            return instance;
        }

        private bool ShouldResumeDevourChain(Card sourceCard)
        {
            if (_context.ActionSystem is ActionSystem realActionSystem)
            {
                bool sourceCardOnStack = realActionSystem.ExecutionStack.Count > 0 &&
                                        realActionSystem.ExecutionStack.Any(ctx => ctx.SourceCard == sourceCard);

                if (!sourceCardOnStack)
                {
                    _logger.Log($"Direct API call detected (source card not on stack). Manually resuming chain.", LogChannel.Debug);
                    return true;
                }

                _logger.Log($"Stack-based flow detected (source card on stack, size: {realActionSystem.ExecutionStack.Count}). Callback will handle chain.", LogChannel.Debug);
                return false;
            }

            _logger.Log($"Mocked ActionSystem detected. Manually resuming chain.", LogChannel.Debug);
            return true;
        }

        private void CheckAndReplaceMarketCard(Card targetCard, Card? sourceCard)
        {
            var currentPlayer = _context.ActivePlayer;
            bool shouldReplace = false;

            if (sourceCard != null)
            {
                var devourEffect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.Devour && e.TargetLocation == CardLocation.Market);
                shouldReplace = devourEffect?.ReplaceWithSource ?? false;
            }

            if (shouldReplace && sourceCard != null)
            {
                _logger.Log($"Replacing Market Card {targetCard.Name} with {sourceCard.Name}", LogChannel.Info);
                _context.PlayerStateManager.MoveCardToMarket(currentPlayer, sourceCard);
                _context.MarketManager.ReplaceCard(targetCard, sourceCard);
                targetCard.Location = CardLocation.Void;
                _context.VoidPile.Add(targetCard);
            }
            else
            {
                _context.MarketManager.RemoveCard(targetCard);
                targetCard.Location = CardLocation.Void;
                _context.VoidPile.Add(targetCard);
            }
        }

        public void ResumeDevourChain(Card sourceCard)
        {
            // Find the Devour effect that likely initiated this chain.
            var devourEffect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.Devour);

            if (devourEffect != null && devourEffect.OnSuccess != null)
            {
                _logger.Log($"Resuming Devour Chain for {sourceCard.Name} -> {devourEffect.OnSuccess.Type}", LogChannel.Info);

                // CardEffectProcessor.PushSuccessorEffect wires this child's own OnSuccess
                // continuation the same way the normal stack-based flow does - a hand-built
                // EffectContext here (with no such wiring) would silently drop anything chained
                // beyond this one child, e.g. Zuggtmoy's Devour -> GainResource -> Promote.
                CardEffectProcessor.PushSuccessorEffect(devourEffect.OnSuccess, sourceCard, _context, _logger);

                // Process immediately
                _context.ActionSystem.ProcessStack();
            }
            else
            {
                _logger.Log($"ResumeDevourChain: No successor effect found for {sourceCard.Name}.", LogChannel.Info);
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            _context.PlayerStateManager.PlayCard(_context.ActivePlayer, card);
        }

        // --- End-turn/reactive-discard/victory lifecycle - thin delegations to
        // TurnLifecycleSubsystem, so IMatchManager's public contract - and every existing
        // caller - is completely unchanged. See that class's own doc comment. ---

        public int RoundNumber => _turnLifecycle.RoundNumber;
        public int TotalTurnCount => _turnLifecycle.TotalTurnCount;

        public bool IsResolvingOpponentDiscard => _turnLifecycle.IsResolvingOpponentDiscard;

        public bool CanEndTurn(out string reason) => _turnLifecycle.CanEndTurn(out reason);

        public void EnqueueReactiveDiscard(Player player) => _turnLifecycle.EnqueueReactiveDiscard(player);

        public void ResumeReactiveDiscardQueue() => _turnLifecycle.ResumeReactiveDiscardQueue();

        public void EndTurn() => _turnLifecycle.EndTurn();

        public void ResolveOpponentDiscard(Card discardedCard) => _turnLifecycle.ResolveOpponentDiscard(discardedCard);

        public bool IsGameOver() => _turnLifecycle.IsGameOver();

        public Core.Data.Dtos.VictoryDto? VictoryResult => _turnLifecycle.VictoryResult;

        public void TriggerGameOver() => _turnLifecycle.TriggerGameOver();

        public IReadOnlyList<Card> VoidPile => _context.VoidPile;
    }
}
