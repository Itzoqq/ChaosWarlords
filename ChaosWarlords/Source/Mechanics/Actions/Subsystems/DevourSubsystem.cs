using System;
using System.Linq;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    public class DevourSubsystem : IDevourSubsystem
    {
        private IMatchManager? _matchManager;
        private IMarketManager? _marketManager;
        private readonly ITurnManager _turnManager;
        private readonly IGameLogger _logger;
        private readonly IActionSystem _actionSystem;
        private IPlayerStateManager? _playerStateManager;
        private IMarketStateManager? _marketStateManager;

        // Exposed State
        public Card? PendingDevourCard { get; private set; }
        
        // Private State
        private bool _deferDevourExecution;
        private Action? _pendingCallback;

        public DevourSubsystem(
            ITurnManager turnManager,
            IActionSystem actionSystem,
            IGameLogger logger)
        {
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _actionSystem = actionSystem ?? throw new ArgumentNullException(nameof(actionSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetMatchManager(IMatchManager matchManager)
        {
            _matchManager = matchManager;
        }

        public void SetMarketManager(IMarketManager marketManager)
        {
            _marketManager = marketManager;
        }

        public void SetPlayerStateManager(IPlayerStateManager stateManager)
        {
            _playerStateManager = stateManager;
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
            // 1. Check for Pre-Selected Target (Pre-Commit Flow)
            var preTarget = _actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourHand);
            
            // Note: ActionSystem.SkippedTarget is generic object, so we might need to verify reference or similar logic
            // Since we can't access ActionSystem.SkippedTarget static field via Interface if it's not in interface?
            // Actually it's public static on ActionSystem class. We can access it via Type.
            
            // Check if SkippedTarget match involves reflection or we just assume logic:
            // "if preTarget is object o && o.ToString() == "ChaosWarlords.Source.Managers.ActionSystem+SkippedTarget" -- NO.
            // Better: We see if preTarget is NOT null and NOT Card.
            // Or access ActionSystem.SkippedTarget if we add namespace.

            if (preTarget != null && !(preTarget is Card))
            {
               // Assume it is the SkippedTarget marker object
                _logger.Log($"Devour optional cost skipped by user for {sourceCard.Name}. Chain halted (Supplant will not trigger).", LogChannel.Info);
                return;
            }

            if (preTarget is Card targetCard)
            {
                if (deferExecution)
                {
                    // BUFFER the choice (Pre-Target)
                    PendingDevourCard = targetCard;
                    _logger.Log($"Devour Buffered (Pre-Target): {targetCard.Name}. Proceeding to next step...", LogChannel.Info);
                    
                    // Proceed to next step without executing
                    onComplete?.Invoke();
                    return;
                }
                else
                {
                    // Execute immediately!
                    _matchManager?.DevourCard(targetCard);
                    onComplete?.Invoke();
                    return;
                }
            }

            // Dynamic Threshold:
            // If the source card is in Hand, we need at least one OTHER card (Count > 1).
            // If the source card is Played (e.g. during resolution), we just need any card in Hand (Count > 0).
            int requiredCount = (sourceCard.Location == CardLocation.Hand) ? 1 : 0;
            var currentPlayer = _turnManager.ActivePlayer;

            if (currentPlayer.Hand.Count <= requiredCount)
            {
                _logger.Log("No other cards in hand to Devour.", LogChannel.Warning);
                _actionSystem.CompleteAction(); // This might trigger generic completion
                return;
            }

            _actionSystem.StartTargeting(ActionState.TargetingDevourHand, sourceCard);
            _pendingCallback = onComplete;
            _deferDevourExecution = deferExecution; 
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from HAND to remove. (Optional: You may skip)", LogChannel.General);
        }

        public void TryStartDevourMarket(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            // 1. Check for Pre-Selected Target
            var preTarget = _actionSystem.GetAndClearPreTarget(sourceCard, ActionState.TargetingDevourMarket);
            
            if (preTarget != null && !(preTarget is Card))
            {
                _logger.Log($"Devour (Market) skipped by user. Chain halted.", LogChannel.Info);
                return;
            }

            if (preTarget is Card targetCard)
            {
                 // Handle Pre-Selection (Transactional)
                 HandleDevourMarketSelection(targetCard);
                 return;
            }

            // 2. Validate Market Availability
            if (_marketManager == null || _marketManager.MarketRow.All(c => c == null))
            {
                _logger.Log("No cards in Market to Devour (or Manager missing).", LogChannel.Warning);
                _actionSystem.CompleteAction();
                return;
            }

            // 3. Start Targeting
            _actionSystem.StartTargeting(ActionState.TargetingDevourMarket, sourceCard);
            _pendingCallback = onComplete;
            _deferDevourExecution = deferExecution; 
            
            // 4. Open Market UI via Manager
            _marketStateManager?.OpenForDevour(HandleDevourMarketSelection);
            
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from MARKET to remove.", LogChannel.General);
        }

        public void TryStartDevourDeck(Card sourceCard, Action? onComplete = null, bool deferExecution = false)
        {
            var player = _turnManager.ActivePlayer;
            if (player.Deck.Count > 0)
            {
                var drawnCards = player.DeckManager.Draw(1, null!); 
                var cardToDevour = drawnCards[0];
                
                _logger.Log($"{sourceCard.Name} devoured {cardToDevour.Name} from deck.", LogChannel.Info);
                
                _matchManager?.DevourCard(cardToDevour);
                onComplete?.Invoke();
            }
            else
            {
                _logger.Log($"{sourceCard.Name}: Deck is empty, cannot devour.", LogChannel.Warning);
                onComplete?.Invoke(); 
            }
        }

        public void HandleDevourSelection(Card? targetCard)
        {
            if (targetCard is null) return;
            
            if (targetCard == _actionSystem.PendingCard) 
            {
                 _logger.Log("Cannot devour the played card itself.", LogChannel.Warning);
                 return;
            }

            if (_deferDevourExecution)
            {
                // BUFFER the choice
                PendingDevourCard = targetCard;
                _logger.Log($"Devour Buffered: {targetCard.Name}. Proceeding to next step...", LogChannel.Info);
                
                // We use CompleteAction here but we need to ensure it triggers the callback we stored
                // The ActionSystem has its own CompleteAction but that one triggers ActionSystem's pending callback.
                // We stored our callback locally in _pendingCallback.
                
                // CRITIAL: We must chain execution.
                TriggerCompletion();
            }
            else
            {
                // Immediate Execution
                _matchManager?.DevourCard(targetCard);
                TriggerCompletion();
            }
        }

        public void HandleDevourMarketSelection(Card? targetCard)
        {
            if (targetCard is null) return;
            
            _marketStateManager?.OpenForBrowsing();

            if (targetCard.Location != CardLocation.Market)
            {
                _logger.Log("Selected card is not in Market!", LogChannel.Warning);
                return;
            }

            _logger.Log($"Devouring Market Card: {targetCard.Name}", LogChannel.Info);

            if (_deferDevourExecution)
            {
                PendingDevourCard = targetCard;
                TriggerCompletion();
            }
            else
            {
                var pendingCard = _actionSystem.PendingCard;
                var currentPlayer = _turnManager.ActivePlayer;
                
                var devourEffect = pendingCard?.Effects.FirstOrDefault(e => e.Type == EffectType.Devour && e.TargetLocation == CardLocation.Market);
                bool shouldReplace = devourEffect?.ReplaceWithSource ?? false;

                // Ensure Managers are available
                if (_marketManager == null)
                {
                     _logger.Log("MarketManager is missing!", LogChannel.Error);
                     return;
                }

                if (shouldReplace && _playerStateManager != null && pendingCard != null)
                {
                    _logger.Log($"Replacing Market Card {targetCard.Name} with {pendingCard.Name}", LogChannel.Info);

                    _playerStateManager.MoveCardToMarket(currentPlayer, pendingCard);
                    _marketManager.ReplaceCard(targetCard, pendingCard);
                    targetCard.Location = CardLocation.Void;
                }
                else
                {
                    _marketManager.RemoveCard(targetCard);
                    targetCard.Location = CardLocation.Void;
                }
                
                TriggerCompletion();
            }
        }

        private void TriggerCompletion()
        {
            // We invoke OUR callback then ask ActionSystem to complete.
            // But ActionSystem.CompleteAction clears state.
            
            // To maintain correct flow:
            // 1. Invoke local callback (next step in chain)
            // 2. Clear local transient state (callback)
            
            var callback = _pendingCallback;
            _pendingCallback = null;
            
            // We must call ActionSystem.CompleteAction() to reset ActionSystem state (Targeting -> Normal)
            // BUT if the callback starts a NEW targeting state (e.g. Supplant), calling CompleteAction AFTER might wipe it?
            
            // Depends on ActionSystem implementation.
            // ActionSystem.CompleteAction() calls ClearState() then invokes its own Callbacks.
            
            // In the monolithic version: _pendingCallback was stored in ActionSystem.
            // CompleteAction called ClearState THEN invoked callback.
            
            // So here:
            // We should tell ActionSystem "We are done with this step".
            // But we don't have access to ActionSystem's internal _pendingCallback storage to inject ours.
            
            // Ideally validation/execution should end with ActionSystem.CompleteAction().
            
            // Wait, if we invoke callback here, we might be starting the NEXT step.
            // So we should:
            // 1. Clear ActionSystem State (ActionSystem.ClearState() is private, oops.)
            // ActionSystem.CompleteAction() is public.
            
            // If we call ActionSystem.CompleteAction(), it will clear state.
            // Then we invoke our callback?
            
            _actionSystem.CompleteAction(); // Clears ActionState: Targeting -> Normal
            
            callback?.Invoke(); // Starts next step (e.g. StartTargeting(Supplant))
        }
    }
}

