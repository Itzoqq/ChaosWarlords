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

            // --- 3. RESOLVE EFFECTS (The Missing Link) ---
            // Now that the card is "played", we trigger its game logic.
            // We pass the 'hasFocus' snapshot we calculated earlier.
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus, _logger);

            // Trigger automatic processing of the stack (e.g. for instant effects like GainResource)
            _context.ActionSystem.ProcessStack();

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

                // Push the child effect to the stack
                var child = devourEffect.OnSuccess;
                var state = _context.CardRuleEngine.GetStrategy(child.Type).GetTargetingState(child);
                bool requiresInput = _context.CardRuleEngine.GetStrategy(child.Type).IsTargetingEffect || child.IsOptional;

                var childCtx = new Core.Contexts.EffectContext(
                   state,
                   sourceCard,
                   requiresInput,
                   $"Successor Effect: {child.Type}",
                   (success) => { }, // Recursive/Standard handling
                   child
               );
                _context.ActionSystem.PushEffect(childCtx);

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

            if (_context.TurnManager.CurrentTurnContext.PendingPromotionsCount > 0)
            {
                // Optional: Could block here if strictly enforcing cleanup
            }

            reason = string.Empty;
            return true;
        }

        public int RoundNumber { get; private set; } = 1;
        public int TotalTurnCount { get; private set; } = 1;

        private bool _endGamePending;
        private string _pendingVictoryReason = string.Empty;

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

            // 2. Cleanup: Move Hand + Played -> Discard
            _context.PlayerStateManager.CleanUpTurn(_context.ActivePlayer);

            // 3. Draw New Hand
            _context.PlayerStateManager.DrawCards(_context.ActivePlayer, GameConstants.HandSize, _context.Random);

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


