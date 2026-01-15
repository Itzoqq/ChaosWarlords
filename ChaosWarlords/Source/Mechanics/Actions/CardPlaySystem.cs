using ChaosWarlords.Source.Core.Interfaces.Services;
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
            bool hasOptionalTargeting = card.Effects.Any(e => e.IsOptional && IsTargetingEffect(e.Type));

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
                if (!IsTargetingEffect(effect.Type))
                    continue;

                if (ShouldStartTargetingForEffect(card, effect))
                {
                    var state = GetTargetingState(effect);
                    _matchContext.ActionSystem.StartTargeting(state, card);
                    _onTargetingStarted?.Invoke();
                    return true;
                }
            }

            return false;
        }

        private bool ShouldStartTargetingForEffect(Card card, CardEffect effect)
        {
            var state = GetTargetingState(effect);
            _logger.Log($"[CardPlaySystem] Effect {effect.Type} (Loc: {effect.TargetLocation}) -> State: {state}", LogChannel.Debug);

            // Market targeting happens post-play (sequential)
            if (state == ActionState.TargetingDevourMarket)
            {
                _logger.Log($"[CardPlaySystem] Skipping Pre-Commit for Market Devour.", LogChannel.Debug);
                return false;
            }

            if (!_matchContext.CardRuleEngine.HasValidTargets(_matchContext.ActivePlayer, effect.Type, card))
            {
                _logger.Log($"Skipping targeting for {card.Name}: No valid targets for {effect.Type}.", LogChannel.Info);
                return false;
            }

            return true;
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
                    if (_matchContext.CardRuleEngine.HasValidTargets(_matchContext.ActivePlayer, effect.Type, card)) return true;
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
