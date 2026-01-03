using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using System.Linq;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Contexts;

using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions
{
    public class CardPlaySystem
    {
        private readonly MatchContext _matchContext;
        private readonly IMatchManager _matchManager;
        private readonly Action _onTargetingStarted;
        private readonly IGameLogger _logger;
        private readonly IReplayManager _replayManager; // Injected logic

        public CardPlaySystem(MatchContext matchContext, IMatchManager MatchManager, IReplayManager replayManager, Action onTargetingStarted, IGameLogger logger)
        {
            _matchContext = matchContext;
            _matchManager = MatchManager;
            _replayManager = replayManager ?? throw new ArgumentNullException(nameof(replayManager));
            _onTargetingStarted = onTargetingStarted;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void PlayCard(Card card)
        {
            bool enteredTargeting = false;

            // CRITICAL: If this card has ANY optional targeting effects, skip pre-commit targeting entirely
            // The popup will handle the player's choice, and targeting will start only if they accept
            bool hasOptionalTargeting = card.Effects.Any(e => e.IsOptional && IsTargetingEffect(e.Type));
            if (hasOptionalTargeting)
            {
                _logger.Log($"Card {card.Name} has optional targeting effects. Skipping pre-commit targeting - popup will handle it.", LogChannel.Debug);
                _matchManager.PlayCard(card);
                return;
            }

            // 1. Check for Targeting Effects
            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    // Use CardRuleEngine for validation
                    if (_matchContext.CardRuleEngine.HasValidTargets(_matchContext.ActivePlayer, effect.Type))
                    {
                        var state = GetTargetingState(effect.Type);
                        _matchContext.ActionSystem.StartTargeting(state, card);
                        _onTargetingStarted?.Invoke();
                        enteredTargeting = true;
                        break; // Stop checking other effects once we enter targeting
                    }
                    else
                    {
                        // SKIPPING TARGETING:
                        // If no targets exist, we define this as "Targeting phase complete/skipped"
                        // and proceed to play the card data.
                        _logger.Log($"Skipping targeting for {card.Name}: No valid targets for {effect.Type}.", LogChannel.Info);
                    }
                }
            }

            // 2. Play immediately if no targeting was started
            if (!enteredTargeting)
            {
                _matchManager.PlayCard(card);
            }
        }

        public bool HasViableTargets(Card card)
        {
            if (card is null) return false;

            // Optimization: checking Any directly
            if (!card.Effects.Any(e => IsTargetingEffect(e.Type))) return true;

            foreach (var effect in card.Effects)
            {
                if (IsTargetingEffect(effect.Type))
                {
                    if (_matchContext.CardRuleEngine.HasValidTargets(_matchContext.ActivePlayer, effect.Type)) return true;
                }
            }
            return false;
        }

        public static bool IsTargetingEffect(EffectType type)
        {
            return type == EffectType.Assassinate ||
                   type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant ||
                   type == EffectType.PlaceSpy ||
                   type == EffectType.MoveUnit ||
                   type == EffectType.Devour;
        }

        public static ActionState GetTargetingState(EffectType type)
        {
            return type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                EffectType.MoveUnit => ActionState.TargetingMoveSource,
                EffectType.Devour => ActionState.TargetingDevourHand,
                _ => ActionState.Normal
            };
        }
    }
}



