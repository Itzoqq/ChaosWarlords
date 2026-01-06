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

            StartDevourTargeting(ActionState.TargetingDevourMarket, sourceCard, onComplete, deferExecution);
            _marketStateManager?.OpenForDevour(HandleDevourMarketSelection);
            _logger.Log($"Triggering Devour for {sourceCard.Name}. Select a card from MARKET to remove.", LogChannel.General);
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
                _matchManager?.DevourCard(targetCard);
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
            return _marketManager != null && _marketManager.MarketRow.Any(c => c != null);
        }

        private void StartDevourTargeting(ActionState state, Card sourceCard, Action? onComplete, bool deferExecution)
        {
            _actionSystem.StartTargeting(state, sourceCard);
            _pendingCallback = onComplete;
            _deferDevourExecution = deferExecution;
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

            if (!IsValidMarketCard(targetCard))
            {
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
                ExecuteImmediateMarketDevour(targetCard);
                TriggerCompletion();
            }
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

        private void ExecuteImmediateMarketDevour(Card targetCard)
        {
            var pendingCard = _actionSystem.PendingCard;
            var currentPlayer = _turnManager.ActivePlayer;
            
            var devourEffect = pendingCard?.Effects.FirstOrDefault(e => e.Type == EffectType.Devour && e.TargetLocation == CardLocation.Market);
            bool shouldReplace = devourEffect?.ReplaceWithSource ?? false;

            if (_marketManager == null)
            {
                _logger.Log("MarketManager is missing!", LogChannel.Error);
                return;
            }

            if (shouldReplace && _playerStateManager != null && pendingCard != null)
            {
                ReplaceMarketCard(targetCard, pendingCard, currentPlayer);
            }
            else
            {
                RemoveMarketCard(targetCard);
            }
        }

        private void ReplaceMarketCard(Card targetCard, Card pendingCard, ChaosWarlords.Source.Entities.Actors.Player currentPlayer)
        {
            _logger.Log($"Replacing Market Card {targetCard.Name} with {pendingCard.Name}", LogChannel.Info);

            _playerStateManager!.MoveCardToMarket(currentPlayer, pendingCard);
            _marketManager!.ReplaceCard(targetCard, pendingCard);
            targetCard.Location = CardLocation.Void;
        }

        private void RemoveMarketCard(Card targetCard)
        {
            _marketManager!.RemoveCard(targetCard);
            targetCard.Location = CardLocation.Void;
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

