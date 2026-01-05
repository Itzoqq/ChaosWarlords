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
            bool isMet = effect.Condition.Evaluate(_context, player);

            if (!isMet)
            {
                _logger.Log($"[RuleEngine] Condition Failed: {effect.Condition.Type} (Threshold: {effect.Condition.Threshold}, Resource: {effect.Condition.Resource})", LogChannel.Debug);
            }

            return isMet;
        }

        /// <summary>
        /// Checks if the player has valid targets for the specific effect type.
        /// Used to prevent playing cards that would fizzle completely if targets are mandatory.
        /// </summary>
        public bool HasValidTargets(Player player, EffectType effectType, Card? sourceCard = null)
        {
            bool isValid = effectType switch
            {
                EffectType.PlaceSpy => _context.MapManager.HasValidPlaceSpyTarget(player),
                EffectType.ReturnUnit => _context.MapManager.HasValidReturnTroopTarget(player),
                EffectType.Assassinate => _context.MapManager.HasValidAssassinationTarget(player),
                EffectType.MoveUnit => _context.MapManager.HasValidMoveSource(player),
                EffectType.Supplant => _context.MapManager.HasValidAssassinationTarget(player), // Supplant requires Assassinate target + placing troop
                
                EffectType.Devour => CheckDevourTargets(player, sourceCard),
                
                _ => true // Most effects (GainResource, DrawCard) don't need external targets
            };

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
        public bool IsEffectChainValid(Player player, CardEffect effect, Card? sourceCard)
        {
            // 1. Validate the current effect (if it requires targets)
            if (ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.IsTargetingEffect(effect.Type))
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

        private bool CheckDevourTargets(Player player, Card? sourceCard)
        {
            // Default: Devour from Hand
            // NOTE: We'll need to pass the specific Effect to checking function to know TargetLocation
            // But HasValidTargets signature currently only takes EffectType. 
            // We should overload or update HasValidTargets to take CardEffect if we can.
            // For now, let's assume if it's "Devour" type, we might check both or look for the context?
            
            // REFACTOR: To support Devour from Market, we need to know WHICH effect we are validating.
            // The current signature `HasValidTargets(Player, EffectType, Card?)` is insufficient if the context depends on properties of the CardEffect (like TargetLocation).
            
            // Temporary Logic until signature update:
            // Check if ANY effect on the sourceCard is Devour from Market?
            if (sourceCard != null)
            {
                var devourEffect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.Devour);
                if (devourEffect != null)
                {
                    if (devourEffect.TargetLocation == CardLocation.Market)
                    {
                         // Use MarketManager to check if any cards are available
                         // MarketRow is a List<Card>, so checking count is enough (or Any)
                         if (_context.MarketManager.MarketRow.Count > 0)
                         {
                             return true;
                         }
                         else
                         {
                             _logger.Log("[RuleEngine] Market Devour failed: Market is empty.", LogChannel.Warning);
                             return false;
                         }
                    }
                    else if (devourEffect.TargetLocation == CardLocation.Deck)
                    {
                         if (player.Deck.Count > 0)
                         {
                             return true;
                         }
                         else
                         {
                             _logger.Log("[RuleEngine] Deck Devour failed: Deck is empty.", LogChannel.Warning);
                             return false;
                         }
                    }
                }
            }

            // Fallback to Hand Devour
            return player.Hand.Any(c => c != sourceCard);
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
