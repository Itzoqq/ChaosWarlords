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
        // 1. Add the Processor to handle the logic
        private readonly CardEffectProcessor _effectProcessor;
        private readonly IVictoryManager _victoryManager;
        private bool _gameOver;

        public MatchManager(MatchContext context, IGameLogger logger, IVictoryManager victoryManager)
        {
            _context = context;
            _context.MatchManager = this;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _victoryManager = victoryManager ?? throw new ArgumentNullException(nameof(victoryManager));
            _effectProcessor = new CardEffectProcessor();
            _gameOver = false;
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
                Mechanics.Rules.CardEffectProcessor.PushSuccessorEffect(devourEffect.OnSuccess, sourceCard, _context, _logger);

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

        public bool CanEndTurn(out string reason)
        {
            // A targeting sequence in progress (including a deferred "up to N" promotion
            // redemption or a forced-discard flow) must resolve or be explicitly declined
            // first - EndTurnCommand.Validate() enforces this too (the actual authoritative
            // gate), but checking it here as well gives the player an informative reason
            // instead of a silent command-layer rejection. See planning.txt.
            if (_context.ActionSystem.IsTargeting())
            {
                reason = "You must finish or cancel your current action before ending your turn.";
                return false;
            }

            if (_context.CurrentPhase == MatchPhase.Setup)
            {
                // Check if current player has deployed a troop
                bool hasDeployed = _context.MapManager.Nodes.Any(n => n.Occupant == _context.ActivePlayer.Color);

                if (!hasDeployed)
                {
                    reason = "You must deploy your army before ending your turn.";
                    return false;
                }
            }

            // NOTE: deliberately does NOT read TurnContext.PendingPromotionsCount here (a past
            // version did, in a dead branch that checked-and-did-nothing with it). That getter
            // lazily materializes any pending "any number" unbounded credit (TurnContext.
            // ExpandPendingUnboundedCredits, High Priest of Myrkul) against PlayedCards AS OF
            // THE MOMENT IT'S READ - CanEndTurn is reachable speculatively (e.g. clicking "End
            // Turn" with cards still unplayed, which opens a confirmation popup the player can
            // cancel and keep playing), so reading it here would risk permanently freezing an
            // unbounded credit's count before the turn's real played-card set is final. Every
            // real redemption entry point (UIEventMediator.HandleEndTurnWithPromotionCheck)
            // already calls TurnContext.ForfeitUnsatisfiableCredits - the actual, safe trigger
            // for that expansion - before ever trusting PendingPromotionsCount itself.
            reason = string.Empty;
            return true;
        }

        public int RoundNumber { get; private set; } = 1;
        public int TotalTurnCount { get; private set; } = 1;

        private bool _endGamePending;
        private string _pendingVictoryReason = string.Empty;

        // Ephemeral orchestration state for cross-player forced-discard sequencing - Neogi's
        // end-of-turn "each opponent discards" phase AND Umber Hulk's mid-turn reactive one
        // (EnqueueReactiveDiscard) share this SAME queue, since the "ask this player to discard,
        // one at a time, skipping anyone with an empty hand" mechanics are identical either way -
        // only what happens once it fully drains differs, tracked by _discardPhaseEndsTurn.
        // Deliberately NOT on MatchContext/DTO-backed. It only needs to survive across frames
        // within a single still-in-progress gesture, not across a save/replay boundary. A
        // mid-sequence rollback (CommandDispatcher's rollback-on-exception) would restore
        // MatchContext.PendingOpponentDiscardTriggers and ActionSystem's own state correctly via
        // StateRestorer, but NOT this field - the same category of gap the DTO snapshot already
        // has for _endGamePending/_pendingVictoryReason below, both also plain private fields.
        // Acceptable for now: nothing in this codebase rolls back mid-multi-frame-sequence today.
        // One entry per discard OWED - a player who owes 2 (stacking, e.g. 2 Neogis played
        // the same turn) appears twice in a row, so they're asked again immediately rather
        // than cycling through every other opponent first.
        private readonly Queue<Player> _pendingDiscardQueue = new();

        // True only while the CURRENT discard phase was started from EndTurn (Neogi) - gates
        // whether AdvanceOpponentDiscard's queue-drained branch should also complete the
        // deferred end-of-turn player-switch, or (a mid-turn reactive-only phase, started by
        // ResumeReactiveDiscardQueue) just stop there, since the chain that queued it has
        // already completed on its own by the time this phase even starts.
        private bool _discardPhaseEndsTurn;

        public bool IsResolvingOpponentDiscard => _pendingDiscardQueue.Count > 0;

        public void EnqueueReactiveDiscard(Player player)
        {
            if (_pendingDiscardQueue.Count == 0)
            {
                // Defensive: guards against a stale _discardPhaseEndsTurn=true left over from an
                // earlier Neogi phase that was abandoned mid-sequence (e.g. a CommandDispatcher
                // rollback-on-exception, which does NOT restore this field - see its own doc
                // comment above) rather than draining normally and resetting it itself. Only
                // safe to force false here specifically because the queue being EMPTY at this
                // point means this call is starting a genuinely NEW, reactive-only phase, not
                // appending to an already-active one (Neogi's or an earlier reactive one) whose
                // _discardPhaseEndsTurn must be left exactly as-is.
                _discardPhaseEndsTurn = false;
            }

            _pendingDiscardQueue.Enqueue(player);
            _logger.Log($"{player.DisplayName} queued for a reactive forced discard.", LogChannel.Info);
        }

        /// <summary>
        /// Called from ActionSystem.ReleaseForcedActingPlayerIfOwnedByExecutionStack - the one
        /// place that already distinguishes "MatchManager's discard queue owns ForcedActingPlayer
        /// right now" from "nothing does, safe to release" (IsResolvingOpponentDiscard). Umber
        /// Hulk's ReactiveDiscardEffect (EnqueueReactiveDiscard) resolves via DiscardCardCommand's
        /// bare-ApplyEffect dispatch, from INSIDE the discarding chain's own resolution, BEFORE
        /// that chain's own CompleteAction()/ResolveOpponentDiscard call - so starting a new
        /// targeting sequence synchronously there would just have its CurrentState immediately
        /// overwritten the instant ClearState() (which runs right after) resets CurrentState to
        /// Normal. This method is that call's LAST step instead (after CurrentState is already
        /// Normal, nothing runs after it), the only point that's actually safe.
        ///
        /// Only ever reached with a non-empty queue when a reactive entry was JUST queued during
        /// the chain currently unwinding: this method is reachable only via an ExecutionStack-
        /// driven ClearState() (ProcessStack's stack-drain, CompleteAction()'s no-stack fallback,
        /// CancelTargeting()'s no-snapshot branch) - paths Neogi's own end-of-turn phase never
        /// touches at all (it drives entirely through AdvanceOpponentDiscard/ResolveOpponentDiscard,
        /// calling StartTargeting directly, no ExecutionStack involved) - so there's no risk of
        /// double-resuming an already-independently-progressing Neogi phase.
        /// </summary>
        public void ResumeReactiveDiscardQueue()
        {
            AdvanceOpponentDiscard();
        }

        public void EndTurn()
        {
            // 1. Map Rewards - REMOVED (Now Start of Turn)

            // 1b. Process Turn End Devour (Self-Devour effects)
            foreach (var card in _context.CardsMarkedForTurnEndDevour.ToList())
            {
                _logger.Log($"Processing Turn End Devour: {card.Name} -> Void", LogChannel.Info);

                // Remove from wherever it is (likely Played or Hand)
                _context.ActivePlayer.RemoveFromPlayed(card);
                _context.ActivePlayer.RemoveFromHand(card);

                // Move to Void
                card.Location = CardLocation.Void;
                _context.VoidPile.Add(card);
            }
            _context.CardsMarkedForTurnEndDevour.Clear();

            // 1c. Process Turn End Promote (Self-Promote effects, e.g. Revenant) - before
            // Cleanup below moves anything still in Played to the discard pile.
            // PlayerStateManager.TryPromoteCard already finds and removes the card from
            // wherever it currently sits (Hand/Played/Discard), unlike the Devour loop above,
            // and already logs success/failure itself.
            foreach (var card in _context.CardsMarkedForTurnEndPromote.ToList())
            {
                _context.PlayerStateManager.TryPromoteCard(_context.ActivePlayer, card, out _);
            }
            _context.CardsMarkedForTurnEndPromote.Clear();

            // 1d. Process deferred Promote-effect completions (e.g. Blue Dragon: "...then gain 1
            // VP for every 3 cards in your inner circle") - see CardEffect.
            // PromotionCompletionEffect/TurnContext.DrainPromotionCompletionEffects for why this
            // (the always-recorded, always-replayed EndTurnCommand) is the only safe firing
            // point. Runs after 1c above so a Revenant-style self-promote sharing this same turn
            // is already reflected too, though no shipped card combines the two.
            foreach (var (source, completionEffect) in _context.TurnManager.CurrentTurnContext.DrainPromotionCompletionEffects())
            {
                CardEffectApplier.ApplyEffect(completionEffect, source, _context, _logger);
            }

            // 2. Cleanup: Move Hand + Played -> Discard
            _context.PlayerStateManager.CleanUpTurn(_context.ActivePlayer);

            // 3. Draw New Hand
            _context.PlayerStateManager.DrawCards(_context.ActivePlayer, GameConstants.HandSize, _context.Random);

            // 3b. Opponent-forced-discard triggers (e.g. Neogi's "at end of turn, each
            // opponent must discard a card") - resolved before the real player switch, since
            // they're framed as happening at the end of THIS (still-active) player's turn.
            // Both prior steps only ever touch the ending player, so they're unaffected by
            // this deferral.
            if (_context.PendingOpponentDiscardTriggers.Count > 0)
            {
                BeginOpponentDiscardPhase();
                return; // Player-switch deferred - see AdvanceOpponentDiscard/ResolveOpponentDiscard.
            }

            CompleteEndTurnSwitch();
        }

        private void BeginOpponentDiscardPhase()
        {
            int owedPerOpponent = _context.PendingOpponentDiscardTriggers.Count;
            var endingPlayer = _context.ActivePlayer;

            var opponentsInSeatOrder = _context.TurnManager.GetOpponentsInSeatOrder(endingPlayer);

            foreach (var opponent in opponentsInSeatOrder)
            {
                for (int i = 0; i < owedPerOpponent; i++)
                {
                    _pendingDiscardQueue.Enqueue(opponent);
                }
            }

            _context.PendingOpponentDiscardTriggers.Clear();
            _logger.Log($"Opponent-discard phase starting: {_pendingDiscardQueue.Count} opponent(s) queued, {owedPerOpponent} discard(s) each.", LogChannel.Info);

            _discardPhaseEndsTurn = true;
            AdvanceOpponentDiscard();
        }

        private void AdvanceOpponentDiscard()
        {
            // Skip any opponent with nothing left to discard - matches DiscardStrategy's own
            // HasValidTargets check for the same-player case. Also correctly handles a
            // stacked opponent running out of cards partway through their owed discards
            // (their remaining queued entries all skip too, one at a time).
            while (_pendingDiscardQueue.Count > 0 && _pendingDiscardQueue.Peek().Hand.Count == 0)
            {
                var skipped = _pendingDiscardQueue.Dequeue();
                _logger.Log($"{skipped.DisplayName} has no cards to discard - skipped.", LogChannel.Info);
            }

            if (_pendingDiscardQueue.Count == 0)
            {
                _context.TurnManager.EndForcedActingPlayer();
                if (_discardPhaseEndsTurn)
                {
                    _discardPhaseEndsTurn = false;
                    CompleteEndTurnSwitch();
                }
                return;
            }

            var next = _pendingDiscardQueue.Peek();
            _context.TurnManager.BeginForcedActingPlayer(next);
            _context.ActionSystem.StartTargeting(ActionState.TargetingDiscard);
            _logger.Log($"{next.DisplayName} must discard a card.", LogChannel.Info);
        }

        public void ResolveOpponentDiscard(Card discardedCard)
        {
            if (_pendingDiscardQueue.Count == 0)
            {
                _logger.Log("ResolveOpponentDiscard called with no opponent-discard sequence in progress.", LogChannel.Warning);
                return;
            }

            var player = _pendingDiscardQueue.Dequeue();
            _logger.Log($"{player.DisplayName} discarded {discardedCard.Name}.", LogChannel.Info);

            AdvanceOpponentDiscard();
        }

        private void CompleteEndTurnSwitch()
        {
            // --- Check Round / Turn Status BEFORE switching ---
            // We need to know if the CURRENT active player is the last one in the cycle.
            // TurnManager doesn't expose Index directly, but we know the list order.
            var players = _context.TurnManager.Players;
            int currentIndex = players.IndexOf(_context.ActivePlayer);
            bool isLastPlayerInRound = currentIndex == players.Count - 1;

            // 4. Switch Player
            _context.TurnManager.EndTurn();
            TotalTurnCount++;

            // 4b. Log Turn Start (New)
            _logger.Log($"Turn Started for {_context.ActivePlayer.DisplayName} (Round {RoundNumber}, Turn Total {TotalTurnCount})", LogChannel.Info);

            // 5. START OF TURN Actions for the NEW active player

            // Phase Check: Transition from Setup to Playing?
            if (_context.CurrentPhase == MatchPhase.Setup)
            {
                // Check if ALL players have placed their initial troop
                // (Assuming 1 troop per player for Setup)
                bool allDeployed = _context.TurnManager.Players.All(p =>
                    _context.MapManager.Nodes.Any(n => n.Occupant == p.Color));

                // SAFEGUARD: If any player has cards in Discard Pile, the game has clearly started (Setup phase doesn't use cards).
                // This prevents getting stuck in Setup if a player is wiped or deployment logic fails.
                bool gameHasProgressed = _context.TurnManager.Players.Any(p => p.DiscardPile.Count > 0);

                if (allDeployed || gameHasProgressed)
                {
                    _logger.Log("All armies deployed (or game in progress). The War Begins! (Entering Playing Phase)", LogChannel.General);
                    _context.CurrentPhase = MatchPhase.Playing;
                    _context.MapManager.SetPhase(MatchPhase.Playing);
                }
            }

            _context.MapManager.DistributeStartOfTurnRewards(_context.ActivePlayer);

            // --- DEFERRED VICTORY CHECK ---

            // Check if end game conditions are met NOW (e.g. barracks empty)
            // But do not trigger immediately if the round is not over.
            if (!_endGamePending)
            {
                if (_victoryManager.CheckEndGameConditions(_context, out var reason))
                {
                    _endGamePending = true;
                    _pendingVictoryReason = reason;
                    _logger.Log($"End-Game Condition Met: {_pendingVictoryReason}. Waiting for round to finish...", LogChannel.Info);
                }
            }

            // If we just finished the turn of the last player in the round...
            if (isLastPlayerInRound)
            {
                // If game ends is pending, trigger it now.
                if (_endGamePending)
                {
                    TriggerGameOver();
                }
                else
                {
                    // Otherwise, proceed to next round
                    RoundNumber++;
                    _logger.Log($"Round {RoundNumber} Started.", LogChannel.Info);
                }
            }
        }

        public bool IsGameOver()
        {
            return _gameOver;
        }

        public Core.Data.Dtos.VictoryDto? VictoryResult { get; private set; }

        public void TriggerGameOver()
        {
            if (_gameOver) return; // Already triggered

            _gameOver = true;

            // Calculate and cache victory result using Mapper logic (or direct use if mapper logic was in VictoryManager)
            // Since our VictoryManager calculates scores and DtoMapper organizes them, we should use DtoMapper here to utilize the method we just wrote.
            VictoryResult = Core.Utilities.DtoMapper.ToVictoryDto(_context, _victoryManager);

            if (VictoryResult != null)
            {
                _logger.Log($"Game Over triggered! Winner: {VictoryResult.WinnerName ?? "None"} - Reason: {VictoryResult.VictoryReason}", LogChannel.General);
            }
        }

        public IReadOnlyList<Card> VoidPile => _context.VoidPile;
    }
}


