using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using System;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Pure Logic Class. Responsible solely for executing the mechanics of card effects.
    /// </summary>
    public class CardEffectProcessor
    {
        public static void ResolveEffects(Card card, MatchContext context, bool hasFocus, IGameLogger logger)
        {
            // Filter effects based on focus requirement
            var effectQueue = new System.Collections.Generic.List<CardEffect>();
            foreach (var effect in card.Effects)
            {
                if (!effect.RequiresFocus || hasFocus)
                {
                    effectQueue.Add(effect);
                }
            }
            
            // Process effects sequentially (important for optional effects)
            ProcessNextEffect(effectQueue, 0, card, context, logger);
        }

        private static void ProcessNextEffect(System.Collections.Generic.List<CardEffect> effects, int index, Card card, MatchContext context, IGameLogger logger)
        {
            if (index >= effects.Count) return; // All effects processed

            var effect = effects[index];
            
            // Check if effect is optional
            if (effect.IsOptional)
            {
                ProcessOptionalEffect(effect, effects, index, card, context, logger);
            }
            else
            {
                ProcessMandatoryEffect(effect, effects, index, card, context, logger);
            }
        }

        private static void ProcessOptionalEffect(CardEffect effect, System.Collections.Generic.List<CardEffect> effects, int index, Card card, MatchContext context, IGameLogger logger)
        {
            // If UIEventMediator is null (test scenario), skip the optional effect
            if (context.UIEventMediator == null)
            {
                logger.Log($"Skipped optional {effect.Type} (no UI mediator)", LogChannel.Info);
                ProcessNextEffect(effects, index + 1, card, context, logger);
                return;
            }

            // Skip if effect chain is invalid (deep validation)
            if (ShouldSkipOptionalEffect(effect, card, context, logger))
            {
                ProcessNextEffect(effects, index + 1, card, context, logger);
                return;
            }

            // Request user decision
            context.UIEventMediator.RequestOptionalEffect(
                card,
                effect,
                onAccept: () => {
                    ApplyEffect(effect, card, context, logger);
                    ProcessNextEffect(effects, index + 1, card, context, logger);
                },
                onDecline: () => {
                    logger.Log($"Skipped optional {effect.Type}", LogChannel.Info);
                    ProcessNextEffect(effects, index + 1, card, context, logger);
                }
            );
        }

        private static bool ShouldSkipOptionalEffect(CardEffect effect, Card card, MatchContext context, IGameLogger logger)
        {
            // Use CardRuleEngine.IsEffectChainValid for deep validation
            // This checks if the current effect AND all subsequent effects in the chain are viable
            if (!context.CardRuleEngine.IsEffectChainValid(context.ActivePlayer, effect, card))
            {
                logger.Log($"Skipped optional {effect.Type}: Effect chain is invalid (targets missing).", LogChannel.Info);
                return true;
            }
            return false;
        }

        private static void ProcessMandatoryEffect(CardEffect effect, System.Collections.Generic.List<CardEffect> effects, int index, Card card, MatchContext context, IGameLogger logger)
        {
            ApplyEffect(effect, card, context, logger);
            ProcessNextEffect(effects, index + 1, card, context, logger);
        }

        private static void ApplyEffect(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            // Trace log for debugging chains
            logger.Log($"Applying effect {effect.Type} for {sourceCard.Name}...", LogChannel.Debug);

            // 1. Check Condition
            if (!context.CardRuleEngine.IsConditionMet(context.ActivePlayer, effect))
            {
                logger.Log($"{sourceCard.Name}: Condition not met, skipping effect.", LogChannel.Info);
                return;
            }

            // 2. Execute Effect logic
            Action action = effect.Type switch
            {
                EffectType.GainResource => () => ApplyGainResource(effect, sourceCard, context, logger),
                EffectType.DrawCard => () => ApplyDrawCard(effect, context),
                EffectType.Promote => () => ApplyPromote(effect, sourceCard, context, logger),
                EffectType.MoveUnit => () => ApplyMoveUnit(sourceCard, context, logger),
                EffectType.Assassinate => () => ApplyAssassinate(sourceCard, context, logger),
                EffectType.Supplant => () => ApplySupplant(sourceCard, context, logger),
                EffectType.PlaceSpy => () => ApplyPlaceSpy(sourceCard, context, logger),
                EffectType.ReturnUnit => () => ApplyReturnUnit(sourceCard, context, logger),
                EffectType.Devour => () => ApplyDevour(effect, sourceCard, context, logger, effect.OnSuccess != null ? () => ApplyEffect(effect.OnSuccess, sourceCard, context, logger) : null,  
                                            effect.OnSuccess != null && ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.IsTargetingEffect(effect.OnSuccess.Type)),
                _ => () => { }
            };

            action();
        }

        private static void ApplyGainResource(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (effect.TargetResource == ResourceType.Power)
                context.PlayerStateManager.AddPower(context.ActivePlayer, effect.Amount);
            else if (effect.TargetResource == ResourceType.Influence)
                context.PlayerStateManager.AddInfluence(context.ActivePlayer, effect.Amount);
            else if (effect.TargetResource == ResourceType.Troops)
            {
                // Troops from cards go to PendingFreeTroops (free deployments this turn)
                context.ActivePlayer.PendingFreeTroops += effect.Amount;
                logger.Log($"{sourceCard.Name}: Gained {effect.Amount} free troop deployment(s) this turn.", LogChannel.Info);
            }
            
            // Auto-trigger recursive effects for instant actions
            if (effect.OnSuccess != null)
            {
                // Instant effects (GainResource, DrawCard) complete immediately, so we can chain immediately.
                ApplyEffect(effect.OnSuccess, sourceCard, context, logger); 
            }
        }
        
        // REFACTOR: ApplyEffect needs to handle the recursion for Instant effects too? 
        // Or should each ApplyX method handle it? 
        // Better: ApplyEffect handles it via "OnActionCompleted" event? No, avoiding event spaghetti.
        // Simple synchronous chaining for instant effects. Callback chaining for async (Targeting) effects.
        
        // Let's stick to the user request: Devour -> Supplant. Devour is async.
        // Valid handling for ApplyDevour above. 
        // For simple effects, we might need a general handling.
        

        private static void ApplyDrawCard(CardEffect effect, MatchContext context)
        {
            context.PlayerStateManager.DrawCards(context.ActivePlayer, effect.Amount, context.Random);
        }

        private static void ApplyPromote(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            context.TurnManager.CurrentTurnContext.AddPromotionCredit(sourceCard, effect.Amount);
            logger.Log($"Promotion pending! Added {effect.Amount} point(s) from {sourceCard.Name}.", LogChannel.Info);
        }

        private static void ApplyMoveUnit(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.MapManager.HasValidMoveSource(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingMoveSource, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a unit to Move.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid units to move.", LogChannel.Warning);
            }
        }

        private static void ApplyAssassinate(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.MapManager.HasValidAssassinationTarget(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingAssassinate, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a valid target to Assassinate.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid targets to Assassinate.", LogChannel.Warning);
            }
        }

        private static void ApplySupplant(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            // Delegate to ActionSystem which now handles Pre-Targets and Validation
            context.ActionSystem.TryStartSupplant(sourceCard);
        }

        private static void ApplyPlaceSpy(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.MapManager.HasValidPlaceSpyTarget(context.ActivePlayer) && context.ActivePlayer.SpiesInBarracks > 0)
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingPlaceSpy, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a Site to Place Spy.", LogChannel.Input);
            }
            else
            {
                if (context.ActivePlayer.SpiesInBarracks <= 0) logger.Log($"{sourceCard.Name}: Cannot Place Spy (No Spies in Barracks).", LogChannel.Warning);
                else logger.Log($"{sourceCard.Name}: No valid sites to Place Spy.", LogChannel.Warning);
            }
        }

        private static void ApplyReturnUnit(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.MapManager.HasValidReturnTroopTarget(context.ActivePlayer))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingReturn, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a unit to Return.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid units to Return.", LogChannel.Warning);
            }
        }

        private static void ApplyDevour(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            // Lookahead validation: Skip if dependent effect has no valid targets
            if (ShouldSkipDevourChain(effect, sourceCard, context, logger, defer))
            {
                return;
            }

            // Use strategy pattern to handle different devour locations
            var strategy = DevourStrategyFactory.GetStrategy(effect.TargetLocation);
            strategy.Execute(sourceCard, context, logger, onComplete, defer);
        }

        private static bool ShouldSkipDevourChain(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger, bool defer)
        {
            if (!defer || effect.OnSuccess == null)
            {
                return false;
            }

            if (!ChaosWarlords.Source.Mechanics.Actions.CardPlaySystem.IsTargetingEffect(effect.OnSuccess.Type))
            {
                return false;
            }

            // Lookahead: If the dependent effect has no valid targets, abort the chain early
            if (!context.CardRuleEngine.HasValidTargets(context.ActivePlayer, effect.OnSuccess.Type, sourceCard))
            {
                logger.Log($"{sourceCard.Name}: Cannot start Devour chain - dependent effect {effect.OnSuccess.Type} has no valid targets.", LogChannel.Warning);
                return true;
            }

            return false;
        }
    }
}


