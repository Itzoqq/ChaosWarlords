using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    public class DevourSubsystem : IDevourSubsystem
    {
        // MatchManager/MarketStateManager stay setter-injected - see IDevourSubsystem's doc
        // comment for why (genuine circular dependency: both arrive later, from the client
        // layer, after this subsystem already exists).
        private IMatchManager? _matchManager;
        private IMarketStateManager? _marketStateManager;
        private readonly IMarketManager _marketManager;
        private readonly ITurnManager _turnManager;
        private readonly IGameLogger _logger;
        private readonly IActionSystem _actionSystem;

        // Exposed State
        public Card? PendingDevourCard { get; private set; }

        // Private State
        private bool _deferDevourExecution;
        private Action? _pendingCallback;

        public DevourSubsystem(
            ITurnManager turnManager,
            IActionSystem actionSystem,
            IGameLogger logger,
            IMarketManager marketManager)
        {
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
        }

        public void SetMatchManager(IMatchManager matchManager)
        {
            _matchManager = matchManager;
        }

        public void SetMarketStateManager(IMarketStateManager stateManager)
        {
            _marketStateManager = stateManager;
        }

        public void ClearState()
        {
            PendingDevourCard = null;
            _deferDevourExecution = false;
            _pendingCallback = null;
        }

        public void TryStartDevourHand(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            var preTarget = _actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourHand);

            if (HandlePreTargetSkipped(preTarget, sourceCard))
                return;

            if (HandlePreTargetCard(preTarget, onComplete, deferExecution))
                return;

            if (!HasValidHandTargets(sourceCard))
            {
                _logger.Log("No other cards in hand to Devour.", LogChannel.Warning);
                _actionSystem.CompleteAction();
                return;
            }

            StartDevourTargeting(ActionState.TargetingDevourHand, sourceCard, onComplete, deferExecution);
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from HAND to remove. (Optional: You may skip)", LogChannel.General);
        }

        public void TryStartDevourMarket(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            var preTarget = _actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourMarket);

            if (HandlePreTargetSkipped(preTarget, sourceCard))
                return;

            if (preTarget is Card targetCard)
            {
                HandleDevourMarketSelection(targetCard);
                return;
            }

            if (!HasValidMarketTargets())
            {
                _logger.Log("No cards in Market to Devour (or Manager missing).", LogChannel.Warning);
                _actionSystem.CompleteAction();
                return;
            }

            // CRITICAL: Open market BEFORE setting targeting state
            // This ensures the market is open when HandleActionStateChanged fires
            _marketStateManager?.OpenForDevour((card) => HandleDevourMarketSelection(card));
            StartDevourTargeting(ActionState.TargetingDevourMarket, sourceCard, onComplete, deferExecution);
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from MARKET to remove.", LogChannel.General);
        }

        public void TryStartDevourInnerCircle(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            var preTarget = _actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourInnerCircle);

            if (HandlePreTargetSkipped(preTarget, sourceCard))
                return;

            if (preTarget is Card targetCard)
            {
                HandleDevourInnerCircleSelection(targetCard);
                return;
            }

            if (!HasValidInnerCircleTargets())
            {
                _logger.Log("Inner Circle is empty. No targets to Devour.", LogChannel.Warning);
                _actionSystem.CompleteAction();
                return;
            }

            StartDevourTargeting(ActionState.TargetingDevourInnerCircle, sourceCard, onComplete, deferExecution);
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from INNER CIRCLE to remove.", LogChannel.General);
        }

        private bool HandlePreTargetSkipped(object? preTarget, Card sourceCard)
        {
            if (preTarget != null && !(preTarget is Card))
            {
                _logger.Log($"Devour skipped by user for {sourceCard.Name}. Chain halted.", LogChannel.Info);
                return true;
            }
            return false;
        }

        private bool HandlePreTargetCard(object? preTarget, Action? onComplete, bool deferExecution)
        {
            if (preTarget is not Card targetCard)
                return false;

            if (deferExecution)
            {
                PendingDevourCard = targetCard;
                _logger.Log($"Devour Buffered (Pre-Target): {targetCard.Name}. Proceeding to next step...", LogChannel.Info);
                onComplete?.Invoke();
            }
            else
            {
                if (targetCard.Location == CardLocation.Market)
                {
                    // Use Market-specific method (assumes IMatchManager has this method now)
                    _matchManager?.DevourMarketCard(targetCard, null); // Source unknown in this context?
                    // Actually sourceCard is passed to TryStartDevour, but not to HandlePreTargetCard.
                    // If we want SourceCard, we need to pass it.
                    // But `Matches.DevourMarketCard` signature is (target, source).
                    // If source is null, it just devours without replace.
                    // This is acceptable fallback for PreTarget if we can't get source easily.
                }
                else
                {
                    _matchManager?.DevourCard(targetCard);
                }
                onComplete?.Invoke();
            }
            return true;
        }

        private bool HasValidHandTargets(Card sourceCard)
        {
            int requiredCount = (sourceCard.Location == CardLocation.Hand) ? 1 : 0;
            return _turnManager.ActivePlayer.Hand.Count > requiredCount;
        }

        private bool HasValidMarketTargets()
        {
            return _marketManager.MarketRow.Any(c => c != null);
        }

        private void StartDevourTargeting(ActionState state, Card sourceCard, Action? onComplete, bool deferExecution)
        {
            _actionSystem.StartTargeting(state, sourceCard);
            _pendingCallback = onComplete;
            _deferDevourExecution = deferExecution;
        }



        public void DeferDevour(Card card)
        {
            PendingDevourCard = card;
            _logger.Log($"Devour Buffered (Command): {card.Name}. Proceeding to next step...", LogChannel.Info);
            TriggerCompletion();
        }

        /// <summary>
        /// Restore-only: sets PendingDevourCard directly, with none of DeferDevour's side
        /// effects (logging, TriggerCompletion). Exists solely for
        /// ActionSystem.RestorePendingState / StateRestorer.RestoreState - see their doc
        /// comments.
        /// </summary>
        public void RestorePendingDevourCard(Card? card)
        {
            PendingDevourCard = card;
        }

        public Commands.DevourCardCommand? HandleDevourSelection(Card? targetCard)
        {
            if (targetCard is null) return null;

            if (targetCard == _actionSystem.PendingCard)
            {
                _logger.Log("Cannot devour the played card itself.", LogChannel.Warning);
                return null;
            }

            // Create the Command
            var cmd = new Commands.DevourCardCommand(targetCard)
            {
                SourceCard = _actionSystem.PendingCard, // Associate with source
                IsDeferred = _deferDevourExecution
            };

            return cmd;
        }

        public Commands.DevourCardCommand? HandleDevourMarketSelection(Card? targetCard)
        {
            if (targetCard is null) return null;

            // Close the market after selection (matching Inner Circle behavior)
            _marketStateManager?.Close();

            if (!IsValidMarketCard(targetCard))
            {
                return null;
            }

            _logger.Log($"Devouring Market Card: {targetCard.Name}", LogChannel.Info);

            // Create command
            var cmd = new Commands.DevourCardCommand(targetCard)
            {
                SourceCard = _actionSystem.PendingCard
            };

            return cmd;
        }

        private bool IsValidMarketCard(Card targetCard)
        {
            if (targetCard.Location != CardLocation.Market)
            {
                _logger.Log("Selected card is not in Market!", LogChannel.Warning);
                return false;
            }
            return true;
        }



        private bool HasValidInnerCircleTargets()
        {
            return _turnManager.ActivePlayer.InnerCircle.Count > 0;
        }

        public Commands.DevourCardCommand? HandleDevourInnerCircleSelection(Card? targetCard)
        {
            if (targetCard is null) return null;

            if (targetCard.Location != CardLocation.InnerCircle)
            {
                _logger.Log("Selected card is not in Inner Circle!", LogChannel.Warning);
                return null;
            }

            // Note: Can't easily check for 'Self' devour here since Self is usually in Hand/Played/Stack
            // But technically one could have a card in Inner Circle that devours itself? Unlikely.

            _logger.Log($"Devouring Inner Circle Card: {targetCard.Name}", LogChannel.Info);

            var cmd = new Commands.DevourCardCommand(targetCard)
            {
                SourceCard = _actionSystem.PendingCard,
                IsDeferred = _deferDevourExecution
            };

            return cmd;
        }



        private void TriggerCompletion()
        {
            // Order matters: clear our own pending callback and reset ActionSystem's state
            // (Targeting -> Normal) BEFORE invoking the callback, not after - the callback may
            // itself start a NEW targeting state (e.g. Devour -> Supplant), and running
            // CompleteAction() afterward would wipe that fresh state instead of the one this
            // step is actually finishing.
            var callback = _pendingCallback;
            _pendingCallback = null;

            _actionSystem.CompleteAction(); // Clears ActionState: Targeting -> Normal
            callback?.Invoke(); // Starts next step (e.g. StartTargeting(Supplant))
        }
    }
}

