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
            foreach (var effect in card.Effects)
            {
                // Logic to Gate Effects based on Focus
                if (effect.RequiresFocus && !hasFocus) continue;

                ApplyEffect(effect, card, context, logger);
            }
        }

        private static void ApplyEffect(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
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
                EffectType.Devour => () => ApplyDevour(sourceCard, context, logger, effect.OnSuccess != null ? () => ApplyEffect(effect.OnSuccess, sourceCard, context, logger) : null, 
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

        private static void ApplyDevour(Card sourceCard, MatchContext context, IGameLogger logger, Action? onComplete, bool defer)
        {
            if (context.ActivePlayer.Hand.Count > 0)
            {
                context.ActionSystem.TryStartDevourHand(sourceCard, onComplete, defer);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: Hand empty, cannot Devour.", LogChannel.Warning);
            }
        }
    }
}


