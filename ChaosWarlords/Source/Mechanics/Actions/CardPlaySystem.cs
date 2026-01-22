using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Contexts;

using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

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
            if (ShouldSkipPreCommitTargeting(card))
            {
                _matchManager.PlayCard(card);
                return;
            }

            if (!TryStartPreCommitTargeting(card))
            {
                _matchManager.PlayCard(card);
            }
        }

        private bool ShouldSkipPreCommitTargeting(Card card)
        {
            bool hasOptionalTargeting = card.Effects.Any(e => e.IsOptional && _matchContext.CardRuleEngine.GetStrategy(e.Type).IsTargetingEffect);

            if (hasOptionalTargeting)
            {
                _logger.Log($"Card {card.Name} has optional targeting effects. Skipping pre-commit targeting - popup will handle it.", LogChannel.Debug);
                return true;
            }

            return false;
        }

        private bool TryStartPreCommitTargeting(Card card)
        {
            foreach (var effect in card.Effects)
            {
                // Use Strategy via RuleEngine
                var strategy = _matchContext.CardRuleEngine.GetStrategy(effect.Type);
                if (!strategy.IsTargetingEffect)
                    continue;

                if (ShouldStartTargetingForEffect(card, effect))
                {
                    var state = strategy.GetTargetingState(effect);
                    _matchContext.ActionSystem.StartTargeting(state, card);
                    _onTargetingStarted?.Invoke();
                    return true;
                }
            }

            return false;
        }

        private bool ShouldStartTargetingForEffect(Card card, CardEffect effect)
        {
            // Use Strategy via RuleEngine
            var strategy = _matchContext.CardRuleEngine.GetStrategy(effect.Type);
            var state = strategy.GetTargetingState(effect);

            _logger.Log($"[CardPlaySystem] Effect {effect.Type} (Loc: {effect.TargetLocation}) -> State: {state}", LogChannel.Debug);

            // Market targeting happens post-play (sequential)
            if (state == ActionState.TargetingDevourMarket)
            {
                _logger.Log($"[CardPlaySystem] Skipping Pre-Commit for Market Devour.", LogChannel.Debug);
                return false;
            }

            // Delegated validation to Strategy via RuleEngine
            if (!strategy.HasValidTargets(_matchContext, _matchContext.ActivePlayer, card))
            {
                _logger.Log($"Skipping targeting for {card.Name}: No valid targets for {effect.Type}.", LogChannel.Info);
                return false;
            }

            return true;
        }

        public bool HasViableTargets(Card card)
        {
            if (card is null) return false;

            // Simple check first
            // Note: We need strategy to know IsTargetingEffect.
            // Iterating strategies is slightly slower than static check but cleaner.
            
            bool anyTargeting = false;
            foreach(var effect in card.Effects)
            {
                var strategy = _matchContext.CardRuleEngine.GetStrategy(effect.Type);
                if (strategy.IsTargetingEffect)
                {
                    anyTargeting = true;
                    if (strategy.HasValidTargets(_matchContext, _matchContext.ActivePlayer, card))
                    {
                        return true;
                    }
                }
            }

            if (!anyTargeting) return true; // If no targeting effects, it's playable/viable (non-targeting)
            // Wait, original logic: if !card.Effects.Any(Targeting) return true.
            // If it has targeting loops, and NONE have targets -> return false.
            // So logic matches.

            return false;
        }

        [Obsolete("Use CardRuleEngine.GetStrategy(type).IsTargetingEffect instead.")]
        public static bool IsTargetingEffect(EffectType type)
        {
            return type == EffectType.Assassinate ||
                   type == EffectType.ReturnUnit ||
                   type == EffectType.Supplant ||
                   type == EffectType.PlaceSpy ||
                   type == EffectType.MoveUnit ||
                   type == EffectType.Devour;
        }

        [Obsolete("Use CardRuleEngine.GetStrategy(type).GetTargetingState(effect) instead.")]
        public static ActionState GetTargetingState(CardEffect effect)
        {
            return effect.Type switch
            {
                EffectType.Assassinate => ActionState.TargetingAssassinate,
                EffectType.ReturnUnit => ActionState.TargetingReturn,
                EffectType.Supplant => ActionState.TargetingSupplant,
                EffectType.PlaceSpy => ActionState.TargetingPlaceSpy,
                EffectType.MoveUnit => ActionState.TargetingMoveSource,
                EffectType.Devour => effect.TargetLocation switch
                {
                    CardLocation.Market => ActionState.TargetingDevourMarket,
                    CardLocation.InnerCircle => ActionState.TargetingDevourInnerCircle,
                    CardLocation.Self => ActionState.Normal,
                    _ => ActionState.TargetingDevourHand
                },
                _ => ActionState.Normal
            };
        }
    }
}
