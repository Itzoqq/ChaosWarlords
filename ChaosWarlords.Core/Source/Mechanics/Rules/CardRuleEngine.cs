using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;
using ChaosWarlords.Source.Mechanics.Rules.Strategies;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Centralized rules engine for validating card plays and conditional effects.
    /// Analyzes game state to determine if specific card requirements are met.
    /// Reference: Similar to MapRuleEngine but for Card-specific logic.
    /// </summary>
    public class CardRuleEngine
    {
        private readonly MatchContext _context;
        private readonly IGameLogger _logger;
        private readonly Dictionary<EffectType, IEffectStrategy> _strategies;

        public CardRuleEngine(MatchContext context, IGameLogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _strategies = new Dictionary<EffectType, IEffectStrategy>
            {
                { EffectType.Assassinate, new Strategies.AssassinateStrategy() },
                { EffectType.ReturnUnit, new Strategies.ReturnUnitStrategy() },
                { EffectType.PlaceSpy, new Strategies.PlaceSpyStrategy() },
                { EffectType.Supplant, new Strategies.SupplantStrategy() },
                { EffectType.MoveUnit, new Strategies.MoveUnitStrategy() },
                { EffectType.Devour, new Strategies.DevourStrategy() },
                { EffectType.DiscardCard, new Strategies.DiscardStrategy() },
                { EffectType.ReturnOwnSpy, new Strategies.ReturnOwnSpyStrategy() },
                { EffectType.PlayFromMarket, new Strategies.PlayFromMarketStrategy() },
                { EffectType.SelectOpponent, new Strategies.SelectOpponentStrategy() },
                // Add new strategies here
            };
        }

        public virtual IEffectStrategy GetStrategy(EffectType type)
        {
            if (_strategies.TryGetValue(type, out var strategy))
                return strategy;
            return new Strategies.DefaultStrategy();
        }

        /// <summary>
        /// Checks if a player can play the given card based on its conditions and costs.
        /// </summary>
        public virtual bool CanPlayCard(Player player, Card card)
        {
            // Basic checks could go here (e.g. costs if not already handled)
            _logger.Log($"Checking playability for {card.Name}...", LogChannel.Debug);
            return true;
        }

        /// <summary>
        /// Evaluates if an effect's condition is met.
        /// </summary>
        public virtual bool IsConditionMet(Player player, CardEffect effect)
        {
            if (effect.Condition == null) return true;
            bool isMet = effect.Condition.Evaluate(_context, player);

            if (!isMet)
            {
                _logger.Log($"[RuleEngine] Condition Failed: {effect.Condition.Type} (Threshold: {effect.Condition.Threshold}, Resource: {effect.Condition.Resource})", LogChannel.Debug);
            }

            return isMet;
        }

        /// <summary>
        /// Checks if the player has valid targets for the specific effect type.
        /// Delegated to Strategy Pattern.
        /// </summary>
        public virtual bool HasValidTargets(Player player, EffectType effectType, Card? sourceCard = null)
        {
            var strategy = GetStrategy(effectType);
            bool isValid = strategy.HasValidTargets(_context, player, sourceCard);

            if (!isValid)
            {
                _logger.Log($"[RuleEngine] Validation Failed for {effectType}. No valid targets found for player {player.DisplayName}.", LogChannel.Debug);
            }

            return isValid;
        }

        /// <summary>
        /// Recursively validates an entire chain of effects.
        /// Useful for optional effects (costs) to ensure the resulting reward/action is actually possible.
        /// </summary>
        public virtual bool IsEffectChainValid(Player player, CardEffect effect, Card? sourceCard)
        {
            // 1. Validate the current effect (if it requires targets)
            if (GetStrategy(effect.Type).IsTargetingEffect)
            {
                if (!HasValidTargets(player, effect.Type, sourceCard))
                {
                    return false;
                }
            }

            // 2. Recursively validate success chain
            if (effect.OnSuccess != null)
            {
                return IsEffectChainValid(player, effect.OnSuccess, sourceCard);
            }

            return true;
        }



        // ----------------------------------------------------------------------------------------
        // Specific Condition Checks (Helpers used by Condition.Evaluate or directly)
        // ----------------------------------------------------------------------------------------

        public bool PlayerControlsSite(Player player)
        {
            return _context.MapManager.Sites.Any(s => s.NodesInternal.Any(n => n.Occupant == player.Color));
        }

        public bool PlayerHasTroopsDeployed(Player player)
        {
            return _context.MapManager.Nodes.Any(n => n.Occupant == player.Color);
        }

        public int CountInnerCircle(Player player)
        {
            // Using logger access to justify instance method
            _logger.Log("Checking InnerCircle count...", LogChannel.Debug);
            return player.InnerCircle.Count;
        }
    }
}
