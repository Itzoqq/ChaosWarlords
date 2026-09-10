using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Applies a single already-resolved CardEffect node: ApplyEffect looks up and invokes the
    /// matching Apply* handler below, keyed by EffectType. Never calls CardEffectProcessor - the
    /// effect-tree construction/repeat-expansion pipeline that decides WHICH node to apply and
    /// WHEN lives there; this class only knows how to apply one node it's been handed. Both share
    /// DynamicAmountResolver for CardEffect.DynamicAmountSource.
    /// </summary>
    public class CardEffectApplier
    {
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

            // ApplyEffect applies ONLY this single node - it never pushes effect.OnSuccess
            // itself. Propagation to a chain's next node happens via the OnResolved callback
            // CardEffectProcessor.PushEffectContext wires onto every EffectContext
            // (ResolveEffects for the top-level list, PopAndResolve for a still-on-the-stack
            // chain node, PushSuccessorEffect for MatchManager.ResumeDevourChain's Direct-API
            // resumption) - see PushSuccessorEffect's doc comment. A caller that invokes
            // ApplyEffect directly without ever building an EffectContext for it at all
            // (DiscardCardCommand's ReactiveDiscardEffect, e.g. Grimlock) gets no further
            // propagation - fine while no shipped ReactiveDiscardEffect chains beyond one node,
            // but a real gap the day one does (see planning.txt).
        }

        private static readonly Dictionary<EffectType, Action<CardEffect, Card, MatchContext, IGameLogger>> _effectHandlers = new()
        {
            [EffectType.GainResource] = (effect, card, ctx, log) => ApplyGainResource(effect, card, ctx, log),
            [EffectType.DrawCard] = (effect, card, ctx, log) => ApplyDrawCard(effect, ctx, log),
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
            [EffectType.PromoteFromPile] = (effect, card, ctx, log) => ApplyPromoteFromPile(card, ctx, log),
            [EffectType.PromoteSelf] = (effect, card, ctx, log) => ApplyPromoteSelf(card, ctx, log),
            [EffectType.ReturnUnitOrSpy] = (effect, card, ctx, log) => ApplyReturnUnitOrSpy(card, ctx, log),
            [EffectType.DeployFromTrophyHall] = (effect, card, ctx, log) => ApplyDeployFromTrophyHall(effect, card, ctx, log),
            [EffectType.ReturnEnemySpy] = (effect, card, ctx, log) => ApplyReturnEnemySpy(card, ctx, log),
            [EffectType.DeployTroop] = (effect, card, ctx, log) => ApplyDeployTroop(card, ctx, log),
            [EffectType.ForceRecruit] = (effect, card, ctx, log) => ApplyForceRecruit(effect, card, ctx, log),
            [EffectType.ForceCausingOpponentDiscard] = (effect, card, ctx, log) => ApplyForceCausingOpponentDiscard(card, ctx, log),
            [EffectType.PromoteTopOfDeck] = (effect, card, ctx, log) => ApplyPromoteTopOfDeck(ctx, log)
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

        // Like ApplyReturnOwnSpy/ApplyReturnUnitOrSpy/every other mandatory targeting-effect
        // handler in this dictionary, this is structurally unreachable via the normal per-effect
        // resolution flow (ActionExecutionEngine.HandleInputRequiredEffect routes a mandatory
        // targeting effect straight to SetupTargetingForRequiredEffect/EnterTargetingState, never
        // through ApplyEffect) - kept anyway for consistency with those siblings.
        private static void ApplyReturnEnemySpy(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.ReturnEnemySpy, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingReturnSpy, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a site to return an enemy spy from.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No enemy spies to return.", LogChannel.Warning);
            }
        }

        private static void ApplyReturnUnitOrSpy(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.ReturnUnitOrSpy, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingReturnUnitOrSpy, sourceCard);
                logger.Log($"{sourceCard.Name}: Select a troop or spy to return.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid troops or spies to return.", LogChannel.Warning);
            }
        }

        // Like ApplyAssassinate/ApplyReturnOwnSpy/ApplyReturnUnitOrSpy/every other mandatory
        // targeting-effect handler in this dictionary, this is structurally unreachable via the
        // normal per-effect resolution flow (ActionExecutionEngine.HandleInputRequiredEffect
        // routes a mandatory targeting effect straight to SetupTargetingForRequiredEffect/
        // EnterTargetingState, never through ApplyEffect) - kept anyway for consistency with
        // those siblings, per the same call made for EffectType.ReturnUnitOrSpy (see
        // planning.txt/RESOLVED.txt). The REAL resolution of WHICH trophy hall to draw from
        // lives in ActionExecutionEngine.ResolvePendingTrophyHallSource instead - see that
        // method's own doc comment for why this handler isn't it.
        private static void ApplyDeployFromTrophyHall(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.DeployFromTrophyHall, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingDeployFromTrophyHall, sourceCard);
                logger.Log($"{sourceCard.Name}: Select an empty space to deploy the trophy hall troop.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid trophy hall troop to take.", LogChannel.Warning);
            }
        }

        private static void ApplyDeployTroop(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardRuleEngine.HasValidTargets(context.ActivePlayer, EffectType.DeployTroop, sourceCard))
            {
                context.ActionSystem.StartTargeting(ActionState.TargetingDeployTroop, sourceCard);
                logger.Log($"{sourceCard.Name}: Select an empty space to deploy a troop.", LogChannel.Input);
            }
            else
            {
                logger.Log($"{sourceCard.Name}: No valid space to deploy a troop.", LogChannel.Warning);
            }
        }

        /// <summary>
        /// Immediately gives effect.TargetCardId (Math.Max(1, effect.Amount) copies) to either
        /// context.ActivePlayer (default - see EffectType.ForceRecruit's doc comment for why
        /// "the active player" here is whoever an earlier EffectType.SelectOpponent step chose,
        /// TurnManager.ForcedActingPlayer, not the real card-playing player) or EVERY opponent
        /// of the real card-playing player when effect.AppliesToEachOpponent is set
        /// (Demogorgon/Ghoul - no SelectOpponent step involved at all, since there's no choice
        /// to make).
        /// </summary>
        private static void ApplyForceRecruit(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (string.IsNullOrEmpty(effect.TargetCardId))
            {
                logger.Log($"{sourceCard.Name}: ForceRecruit effect has no TargetCardId configured.", LogChannel.Warning);
                return;
            }

            int copies = Math.Max(1, effect.Amount);
            IEnumerable<Player> recipients = effect.AppliesToEachOpponent
                ? context.TurnManager.GetOpponentsInSeatOrder(context.ActivePlayer)
                : new[] { context.ActivePlayer };

            foreach (var recipient in recipients)
            {
                for (int i = 0; i < copies; i++)
                {
                    var forcedCard = context.CardDatabase.GetCardById(effect.TargetCardId, context.Random);
                    if (forcedCard == null)
                    {
                        logger.Log($"{sourceCard.Name}: ForceRecruit target card '{effect.TargetCardId}' not found in CardDatabase.", LogChannel.Warning);
                        return; // Same missing card definition every iteration - retrying is pointless.
                    }

                    context.PlayerStateManager.AcquireCard(recipient, forcedCard);
                    logger.Log($"{sourceCard.Name}: {recipient.DisplayName} was forced to recruit {forcedCard.Name}.", LogChannel.Info);
                }
            }
        }

        private static void ApplyMarkOpponentDiscardAtEndOfTurn(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            context.PendingOpponentDiscardTriggers.Add(sourceCard);
            logger.Log($"{sourceCard.Name}: Each opponent will discard a card at end of turn.", LogChannel.Info);
        }

        /// <summary>
        /// EffectType.ForceCausingOpponentDiscard (Umber Hulk's ReactiveDiscardEffect only -
        /// "If an opponent causes you to discard this, they must discard a card"). Only ever
        /// invoked from DiscardCardCommand's bare-ApplyEffect dispatch, at which point
        /// context.ActivePlayer resolves to the player who's discarding (TurnManager.
        /// ForcedActingPlayer), NOT the opponent who caused it - the causing opponent is instead
        /// whoever this TURN actually belongs to, context.TurnManager.CurrentTurnContext.
        /// ActivePlayer, which stays the real turn-owner throughout a forced-discard sequence
        /// regardless of ForcedActingPlayer overrides. Just banks the queue entry
        /// (MatchManager.EnqueueReactiveDiscard) - see that method's own doc comment for why the
        /// actual targeting prompt can't start synchronously from here.
        /// </summary>
        private static void ApplyForceCausingOpponentDiscard(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            var causingOpponent = context.TurnManager.CurrentTurnContext.ActivePlayer;
            if (causingOpponent == context.ActivePlayer)
            {
                // Defensive: this effect only makes sense when someone ELSE forced sourceCard's
                // owner to discard it - shouldn't be reachable today (DiscardCardCommand only
                // invokes ReactiveDiscardEffect at all while ForcedActingPlayer differs from the
                // real turn-owner), but guards against ever queuing a player to reactively
                // discard against themselves.
                logger.Log($"{sourceCard.Name}: no distinct causing opponent to force a discard onto - skipped.", LogChannel.Warning);
                return;
            }

            context.MatchManager.EnqueueReactiveDiscard(causingOpponent);
            logger.Log($"{sourceCard.Name}: {causingOpponent.DisplayName} will be forced to discard a card.", LogChannel.Info);
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
            int amount = DynamicAmountResolver.ResolveAmount(effect, context, logger);

            if (effect.TargetResource == ResourceType.Power)
                context.PlayerStateManager.AddPower(context.ActivePlayer, amount);
            else if (effect.TargetResource == ResourceType.Influence)
                context.PlayerStateManager.AddInfluence(context.ActivePlayer, amount);
            else if (effect.TargetResource == ResourceType.Troops)
            {
                // Troops from cards go to PendingFreeTroops (free deployments this turn)
                context.ActivePlayer.PendingFreeTroops += amount;
                logger.Log($"{sourceCard.Name}: Gained {amount} free troop deployment(s) this turn.", LogChannel.Info);
            }
            else if (effect.TargetResource == ResourceType.VictoryPoints)
                context.PlayerStateManager.AddVictoryPoints(context.ActivePlayer, amount);

            // Does NOT also propagate effect.OnSuccess itself - every caller of ApplyEffect
            // (ProcessAutomaticEffect, HandleOptionalEffectAccepted, ResumeDevourChain via
            // PushSuccessorEffect) already resolves the owning EffectContext afterwards, which
            // pushes effect.OnSuccess through PushEffectNode. Doing it again here would apply a
            // two-level-deep chain's second level twice.
        }

        private static void ApplyDrawCard(CardEffect effect, MatchContext context, IGameLogger logger)
        {
            int amount = DynamicAmountResolver.ResolveAmount(effect, context, logger);
            context.PlayerStateManager.DrawCards(context.ActivePlayer, amount, context.Random);
        }

        private static void ApplyPromote(CardEffect effect, Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (effect.PromoteAnyNumber)
            {
                context.TurnManager.CurrentTurnContext.AddUnboundedPromotionCredit(sourceCard, effect.RequiredPromotionCreatureType);
                logger.Log($"Promotion pending! {sourceCard.Name} may promote any number of eligible cards played this turn.", LogChannel.Info);
            }
            else
            {
                context.TurnManager.CurrentTurnContext.AddPromotionCredit(sourceCard, effect.Amount, effect.PromotionCreditIsOptional, effect.RequiredPromotionAspect, effect.RequiredPromotionCreatureType);
                logger.Log($"Promotion pending! Added {effect.Amount} point(s) from {sourceCard.Name}.", LogChannel.Info);
            }

            if (effect.PromotionCompletionEffect != null)
            {
                context.TurnManager.CurrentTurnContext.RegisterPromotionCompletionEffect(sourceCard, effect.PromotionCompletionEffect);
            }
        }

        /// <summary>
        /// Marks sourceCard itself to be promoted at end of turn (e.g. Revenant) - the
        /// CardEffect.Condition gate on ApplyEffect already decided whether this runs at all.
        /// Guards against a duplicate add the same way ApplyDevourWithChain's Self case does
        /// (defense in depth against a stray repeated resolution marking the same card twice).
        /// </summary>
        private static void ApplyPromoteSelf(Card sourceCard, MatchContext context, IGameLogger logger)
        {
            if (context.CardsMarkedForTurnEndPromote.Contains(sourceCard))
            {
                return;
            }

            context.CardsMarkedForTurnEndPromote.Add(sourceCard);
            logger.Log($"{sourceCard.Name}: Marked for self-promotion at end of turn.", LogChannel.Info);
        }

        /// <summary>
        /// EffectType.PromoteTopOfDeck (Hezrou, Nalfeshnee, Elder Brain). No targeting, no
        /// player choice - just resolves immediately against context.ActivePlayer's own deck.
        /// Failure (nothing left in deck or discard to promote) is only logged, same as every
        /// other automatic effect in this dictionary (e.g. ApplyDrawCard never checks either) -
        /// not surfaced as a card-play failure.
        /// </summary>
        private static void ApplyPromoteTopOfDeck(MatchContext context, IGameLogger logger)
        {
            context.PlayerStateManager.TryPromoteTopOfDeck(context.ActivePlayer, context.Random, out _);
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
