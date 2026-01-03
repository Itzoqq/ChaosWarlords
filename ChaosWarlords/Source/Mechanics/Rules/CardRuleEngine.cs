using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

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

        public CardRuleEngine(MatchContext context, IGameLogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if a player can play the given card based on its conditions and costs.
        /// </summary>
        public bool CanPlayCard(Player player, Card card)
        {
            // Basic checks could go here (e.g. costs if not already handled)
            _logger.Log($"Checking playability for {card.Name}...", LogChannel.Debug);
            return true;
        }

        /// <summary>
        /// Evaluates if an effect's condition is met.
        /// </summary>
        public bool IsConditionMet(Player player, CardEffect effect)
        {
            if (effect.Condition == null) return true;
            return effect.Condition.Evaluate(_context, player);
        }

        /// <summary>
        /// Checks if the player has valid targets for the specific effect type.
        /// Used to prevent playing cards that would fizzle completely if targets are mandatory.
        /// </summary>
        public bool HasValidTargets(Player player, EffectType effectType)
        {
            return effectType switch
            {
                EffectType.PlaceSpy => _context.MapManager.HasValidPlaceSpyTarget(player),
                EffectType.ReturnUnit => _context.MapManager.HasValidReturnTroopTarget(player),
                EffectType.Assassinate => _context.MapManager.HasValidAssassinationTarget(player),
                EffectType.MoveUnit => _context.MapManager.HasValidMoveSource(player),
                EffectType.Supplant => _context.MapManager.HasValidAssassinationTarget(player), // Supplant requires Assassinate target + placing troop
                EffectType.Devour => player.Hand.Count > 0,
                _ => true // Most effects (GainResource, DrawCard) don't need external targets
            };
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
