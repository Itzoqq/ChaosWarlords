using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts; // For EffectContext
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

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
            var effectQueue = new List<CardEffect>();
            foreach (var effect in card.Effects)
            {
                if (!effect.RequiresFocus || hasFocus)
                {
                    effectQueue.Add(effect);
                }
            }

            // Push effects to Stack in REVERSE order (LIFO)
            for (int i = effectQueue.Count - 1; i >= 0; i--)
            {
                PushEffectContext(effectQueue[i], card, context, "Effect", logger);
            }

            // Start Stack Processing
            context.ActionSystem.ProcessStack();
        }

        // Pushes a single effect node (an OnSuccess continuation, an Alternative fallback, or
        // recursively either of those node's own OnSuccess/Alternative) onto the execution
        // stack. Nullable so callers (itself, via effect.OnSuccess/Alternative) can pass
        // "there is no next node" without a separate null check at every call site.
        private static void PushEffectNode(CardEffect? effect, Card card, MatchContext context, IGameLogger logger)
        {
            if (effect == null)
            {
                return;
            }

            PushEffectContext(effect, card, context, "Effect Node", logger);
        }

        /// <summary>
        /// Computes the targeting state, checks HasValidTargets (falling back to
        /// <paramref name="effect"/>'s own Alternative if there's no valid target - "Choose
        /// one" cards must still grant the Alternative, not nothing, e.g. Wight played with an
        /// empty hand), and builds + pushes the resulting EffectContext, wiring its
        /// OnSuccess/Alternative continuations back through PushEffectNode. Shared by
        /// ResolveEffects (the top-level effect list) and PushEffectNode (a single chained
        /// node) - the two used to duplicate this whole sequence independently, differing only
        /// in the EffectContext's description prefix.
        /// </summary>
        private static void PushEffectContext(CardEffect effect, Card card, MatchContext context, string descriptionPrefix, IGameLogger logger)
        {
            var strategy = context.CardRuleEngine.GetStrategy(effect.Type);
            var state = strategy.GetTargetingState(effect);
            bool requiresInput = strategy.IsTargetingEffect || effect.IsOptional;

            if (requiresInput && !context.CardRuleEngine.HasValidTargets(context.ActivePlayer, effect.Type, card))
            {
                logger.Log($"{card.Name}: No valid targets for {effect.Type}. Effect skipped.", LogChannel.Warning);
                PushEffectNode(effect.Alternative, card, context, logger);
                return;
            }

            var ctx = new EffectContext(
                state,
                card,
                requiresInput,
                $"{descriptionPrefix}: {effect.Type}",
                (success) =>
                {
                    // OnResolved callback (Executed after success)
                    // For blocking effects, we must explicitly push the child effect here
                    // because ApplyEffect is NOT called for them (they are handled by input)
                    if (success)
                    {
                        PushEffectNode(effect.OnSuccess, card, context, logger);
                    }
                },
                effect,
                onCancelled: effect.Alternative != null
                    ? () => PushEffectNode(effect.Alternative, card, context, logger)
                    : null
            );

            // "Assassinate 2 troops" (Deathblade) etc. - see IEffectStrategy.SupportsRepeat.
            // Amount otherwise means something completely different per EffectType (resource
            // quantity, card draw count, ...), so this is deliberately gated on the strategy
            // opting in, not applied to every effect unconditionally.
            if (strategy.SupportsRepeat)
            {
                ctx.RemainingRepeats = Math.Max(1, effect.Amount);
            }

            context.ActionSystem.PushEffect(ctx);
        }

        // Restored public ApplyEffect method
        public static void ApplyEffect(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            logger.Log($"Applying effect {effect.Type} for {sourceCard.Name}...", LogChannel.Debug);

            if (!context.CardRuleEngine.IsConditionMet(context.ActivePlayer, effect))
            {
                logger.Log($"{sourceCard.Name}: Condition not met, skipping effect.", LogChannel.Info);
                return;
            }

            if (_effectHandlers.TryGetValue(effect.Type, out var handler))
            {
                handler(effect, sourceCard, context, logger);
            }

            // Note: Standard ApplyEffect does not automatically push children to stack.
            // That logic is handled by ResolveEffects (for initial play) or OnResolved callbacks.
            // However, if this is called directly (e.g. legacy), we might miss children?
            // "Instant" children (GainResource->GainResource) are handled by ApplyGainResource chaining.
            // Stack-based children are pushed by OnResolved.
        }

        private static readonly Dictionary<EffectType, Action<CardEffect, Card, MatchContext, IGameLogger>> _effectHandlers = new()
        {
            [EffectType.GainResource] = (effect, card, ctx, log) => ApplyGainResource(effect, card, ctx, log),
            [EffectType.DrawCard] = (effect, card, ctx, log) => ApplyDrawCard(effect, ctx),
            [EffectType.Promote] = (effect, card, ctx, log) => ApplyPromote(effect, card, ctx, log),
            [EffectType.MoveUnit] = (effect, card, ctx, log) => ApplyMoveUnit(card, ctx, log),
            [EffectType.Assassinate] = (effect, card, ctx, log) => ApplyAssassinate(card, ctx, log),
            [EffectType.Supplant] = (effect, card, ctx, log) => ApplySupplant(card, ctx, log),
            [EffectType.PlaceSpy] = (effect, card, ctx, log) => ApplyPlaceSpy(card, ctx, log),
            [EffectType.ReturnUnit] = (effect, card, ctx, log) => ApplyReturnUnit(card, ctx, log),
            [EffectType.Devour] = (effect, card, ctx, log) => ApplyDevourWithChain(effect, card, ctx, log),
            [EffectType.DiscardCard] = (effect, card, ctx, log) => ApplyDiscardCard(card, ctx, log),
            [EffectType.MarkOpponentDiscardAtEndOfTurn] = (effect, card, ctx, log) => ApplyMarkOpponentDiscardAtEndOfTurn(card, ctx, log),
            [EffectType.ReturnOwnSpy] = (effect, card, ctx, log) => ApplyReturnOwnSpy(card, ctx, log),
            [EffectType.PlayFromMarket] = (effect, card, ctx, log) => ctx.ActionSystem.TryStartPlayFromMarket(card, effect.Amount),
            [EffectType.MoveDeckToDiscard] = (effect, card, ctx, log) => ctx.PlayerStateManager.MoveDeckToDiscard(ctx.ActivePlayer),
            [EffectType.PromoteFromPile] = (effect, card, ctx, log) => ApplyPromoteFromPile(card, ctx, log)
        };

        private static void ApplyReturnOwnSpy(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.ReturnOwnSpy, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingReturnOwnSpy, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a site to return one of your spies from.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No spies to return.", LogChannel.Warning);
            }
        }

        private static void ApplyMarkOpponentDiscardAtEndOfTurn(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            context.PendingOpponentDiscardTriggers.Add(sourceCard);
            logger.Log($"{sourceCard.Name}: Each opponent will discard a card at end of turn.", LogChannel.Info);
        }

        private static void ApplyDiscardCard(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            // Only the active-player, own-hand case (e.g. Insane Outcast's own cost) goes
            // through this path - Neogi's cross-player forced discard doesn't use this
            // EffectType's normal targeting flow at all, see EffectType.MarkOpponentDiscardAtEndOfTurn.
            if (context.ActivePlayer.Hand.Count > 0)
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingDiscard, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a card from your hand to discard.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No cards in hand to discard.", LogChannel.Warning);
            }
        }

        private static void ApplyDevourWithChain(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            // Note: We do NOT pass an onSuccess callback anymore. 
            // The Stack System handles the chain via OnResolved -> PushChildEffect.
            // MatchManager.DevourCard handles the chain for Direct API calls via ResumeDevourChain.
            Action? onSuccess = null;

            bool deferExecution = effect.OnSuccess != null
                && context.CardRuleEngine.GetStrategy(effect.OnSuccess.Type).IsTargetingEffect;

            ApplyDevour(effect, sourceCard, context, logger, onSuccess, deferExecution);
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
            // This is required for chains like GainResource -> GainResource where the second effect
            // might not be pushed to the stack by OnResolved if we are outside a full stack context (e.g. tests)
            // Or if the first effect was "Automatic" and not pushed as a "Blocking" effect.
            if (effect.OnSuccess != null)
            {
                ApplyEffect(effect.OnSuccess, sourceCard, context, logger);
            }
        }

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

        private static void ApplyPromoteFromPile(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.PromoteFromPile, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingPromoteFromPile, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a card to Promote.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid targets to Promote.", LogChannel.Warning);
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

            if (!context.CardRuleEngine.GetStrategy(effect.OnSuccess.Type).IsTargetingEffect)
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


