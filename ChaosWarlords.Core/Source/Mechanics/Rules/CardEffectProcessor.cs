using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts; // For EffectContext
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules.Interfaces;

namespace ChaosWarlords.Source.Mechanics.Rules
{
    /// <summary>
    /// Effect-tree construction/repeat-expansion pipeline: walks a card's authored CardEffect
    /// tree, resolves who each node actually acts as/on, checks its Condition/HasValidTargets
    /// (falling back to Alternative on failure), and pushes the resulting EffectContext onto
    /// ActionSystem's execution stack, wiring OnSuccess/Alternative continuations back through
    /// itself. Distinct from the effect-APPLICATION half (CardEffectApplier.ApplyEffect and its
    /// EffectType-keyed handler dictionary, which decides HOW to apply a node this pipeline has
    /// already decided to push) - neither class calls into the other except via
    /// DynamicAmountResolver, which both share.
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
        /// Public entry point for continuing an effect chain from outside this class - the
        /// exact same node-push/OnSuccess-wiring PushEffectNode uses internally, exposed for
        /// MatchManager.ResumeDevourChain (a Devour effect resolved outside the normal
        /// ProcessStack flow, so nothing else pushes its OnSuccess child). This is the ONLY
        /// mechanism that propagates a chain node forward - an effect's own handler (e.g.
        /// ApplyGainResource) applies just that one node and must not also call ApplyEffect on
        /// effect.OnSuccess itself, or a chain two levels deep would apply its second level
        /// twice (once here, once from the handler).
        /// </summary>
        public static void PushSuccessorEffect(CardEffect? effect, Card card, MatchContext context, IGameLogger logger)
        {
            PushEffectNode(effect, card, context, logger);
        }

        /// <summary>
        /// Transparently swaps <paramref name="effect"/> for an equivalent, purely transient
        /// expanded chain before any of PushEffectContext's normal logic ever sees it, if it
        /// authored one of the 2 runtime tree-expansion repeat primitives: CardEffect.
        /// ChooseCount ("Choose N times: Deploy a troop. Or, Assassinate a white troop." -
        /// Weaponmaster) or CardEffect.ChainedRepeatCount ("Return any number of your spies ->
        /// Supplant a troop at each of the returned spies' sites" - Graz'zt). Every existing card
        /// (both default to 0) is unaffected and returns <paramref name="effect"/> unchanged.
        /// </summary>
        private static CardEffect ExpandRepeatPrimitivesIfNeeded(CardEffect effect)
        {
            if (effect.ChooseCount > 1)
            {
                return ExpandChoiceRepeat(effect, effect.ChooseCount);
            }

            if (effect.ChainedRepeatCount > 1)
            {
                return ExpandChainedRepeat(effect, effect.ChainedRepeatCount);
            }

            return effect;
        }

        /// <summary>
        /// Computes the targeting state, resolves WHO this effect actually acts as (normally
        /// context.ActivePlayer, but see the TargetsAffectedPlayer branch below), checks the
        /// effect's own Condition and HasValidTargets against that resolved actor (falling back
        /// to <paramref name="effect"/>'s own Alternative if either fails - "Choose one" cards
        /// must still grant the Alternative, not nothing, e.g. Wight played with an empty
        /// hand), and builds + pushes the resulting EffectContext, wiring its OnSuccess/
        /// Alternative continuations back through PushEffectNode. Shared by ResolveEffects (the
        /// top-level effect list) and PushEffectNode (a single chained node) - the two used to
        /// duplicate this whole sequence independently, differing only in the EffectContext's
        /// description prefix.
        /// </summary>
        private static void PushEffectContext(CardEffect effect, Card card, MatchContext context, string descriptionPrefix, IGameLogger logger)
        {
            effect = ExpandRepeatPrimitivesIfNeeded(effect);

            var strategy = context.CardRuleEngine.GetStrategy(effect.Type);
            var state = strategy.GetTargetingState(effect);
            bool requiresInput = strategy.IsTargetingEffect || effect.IsOptional;

            // "Assassinate one white troop for each site you control" (Quaggoth) - a repeat
            // COUNT computed from live game state, unlike DynamicAmountSource's usual role of
            // computing a resource/draw AMOUNT. Resolved and gated here, BEFORE TryResolveActor,
            // because a resolved count of 0 must skip the whole effect entirely (0 sites
            // controlled -> no Assassinate at all) rather than the Math.Max(1, ...) floor every
            // other repeat-capable effect gets below - the same "no valid target" path
            // TryResolveActor's own failure takes. See ResolveDynamicRepeatCount's own doc
            // comment for why this is a no-op for every DynamicAmountSource card shipped so far.
            int? dynamicRepeatCount = ResolveDynamicRepeatCount(effect, strategy, context, logger);
            if (dynamicRepeatCount == 0)
            {
                PushEffectNode(effect.Alternative, card, context, logger);
                return;
            }

            if (!TryResolveActor(effect, card, context, requiresInput, logger, out var actor))
            {
                PushEffectNode(effect.Alternative, card, context, logger);
                return;
            }

            // Only now that every gate has passed do we actually switch whose turn this is
            // for - avoids ever having to unwind a speculative ForcedActingPlayer switch if a
            // check inside TryResolveActor had failed instead.
            if (effect.TargetsAffectedPlayer)
            {
                context.TurnManager.BeginForcedActingPlayer(actor);
            }

            BuildAndPushEffectContext(effect, card, context, descriptionPrefix, logger, strategy, state, requiresInput, dynamicRepeatCount);
        }

        /// <summary>
        /// Builds the actual EffectContext (OnResolved/onCancelled wiring back through
        /// PushEffectNode) and pushes it - split out from PushEffectContext purely to keep that
        /// method's own cyclomatic complexity down; every gate that can still say "don't push
        /// anything at all" (TryResolveActor, the dynamic-repeat-count zero case) has already
        /// run by the time this is called.
        /// </summary>
        private static void BuildAndPushEffectContext(CardEffect effect, Card card, MatchContext context, string descriptionPrefix, IGameLogger logger, IEffectStrategy strategy, ActionState state, bool requiresInput, int? dynamicRepeatCount)
        {
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
                ctx.RemainingRepeats = dynamicRepeatCount ?? Math.Max(1, effect.Amount);
            }

            context.ActionSystem.PushEffect(ctx);
        }

        /// <summary>
        /// Returns null (meaning "use the plain, fixed effect.Amount instead") unless
        /// <paramref name="effect"/> is BOTH repeat-capable (IEffectStrategy.SupportsRepeat) AND
        /// carries a DynamicAmountSource - the combination Quaggoth introduces ("Assassinate one
        /// white troop for each site you control"). Every DynamicAmountSource card shipped
        /// before Quaggoth targets GainResource/DrawCard, neither of which supports repeats, so
        /// this is structurally a no-op for all of them - this method exists only to keep
        /// PushEffectContext's own cyclomatic complexity down, not to gate anything by card.
        /// </summary>
        private static int? ResolveDynamicRepeatCount(CardEffect effect, IEffectStrategy strategy, MatchContext context, IGameLogger logger)
        {
            if (!strategy.SupportsRepeat || effect.DynamicAmountSource == DynamicAmountSource.None)
            {
                return null;
            }
            return DynamicAmountResolver.ResolveAmount(effect, context, logger);
        }

        /// <summary>
        /// Builds an equivalent, purely transient OnSuccess/Alternative chain for a
        /// CardEffect.ChooseCount > 1 node (Weaponmaster: "Choose three times: Deploy a troop.
        /// Or, Assassinate a white troop.") - a genuinely different primitive from
        /// IEffectStrategy.SupportsRepeat (repeats the SAME effect type/targeting state N times)
        /// and from a plain one-shot IsOptional+Alternative pair (ChooseCount &lt;= 1, e.g.
        /// Kobold - unaffected): this repeats an INDEPENDENT choice between 2 different effect
        /// types, N times in a row. Deliberately does NOT touch the resolution engine
        /// (ActionExecutionEngine/HandleOptionalEffectAccepted/Declined/TryResolveActor) at all -
        /// it only changes what CardEffect TREE SHAPE gets fed into that already-proven-correct
        /// engine, recomputed fresh every PushEffectContext call (never written back onto
        /// Card.Effects).
        ///
        /// Known gap (flagged, unresolved): because these round-specific clones never get
        /// written back onto Card.Effects, EffectTreeSearch.FindFirstEffect-based lookups
        /// (AssassinateStrategy/SupplantStrategy/DevourStrategy/PromoteFromPileStrategy.
        /// HasValidTargets, and StateRestorer.RestoreEffect's post-rollback SourceEffect lookup)
        /// always resolve against the AUTHORED, un-expanded node on Card.Effects - never the
        /// actual in-flight round's clone. Harmless today because every clone ExpandChoiceRepeat
        /// produces is field-identical to the authored template (TargetNeutralTroopOnly,
        /// IgnoresPresenceRequirement, etc. never vary by round) - but a future ChooseCount card
        /// wanting PER-ROUND-VARYING targeting constraints would silently validate every round
        /// against the wrong (first/authored) round's constraints instead. Also: StateRestorer.
        /// RestoreEffect already gives every restored EffectContext dummy no-op OnResolved/
        /// OnCancelled callbacks (a pre-existing, accepted limitation for every chain card, not
        /// introduced here) - for a ChooseCount sequence specifically, a CommandDispatcher
        /// rollback mid-round therefore silently truncates ALL remaining rounds (not just the one
        /// node a simpler chain would lose), with no round-index persisted anywhere
        /// (EffectContextDto has no such field) to ever recover it. Narrow trigger (needs an
        /// actual exception mid-Execute, not the common CancelTargeting()/DeclineRepeatCommand
        /// paths) - not fixed by this pass.
        ///
        /// Both branches of round N converge on round N+1: the accepted branch's OnSuccess, the
        /// declined branch's OnSuccess (its own target resolved successfully), AND - critically -
        /// the declined branch's own Alternative (its TryResolveActor found NO valid target at
        /// all, e.g. no white troop left) all point at the SAME round N+1 continuation. Without
        /// that last one, a round where the decline option turns out to be impossible would
        /// silently swallow every REMAINING round too (PushEffectContext's normal "no valid
        /// target -> Alternative, full stop" behavior, which is exactly right for a plain
        /// one-shot choice but wrong here since a whole sequence is still in progress).
        ///
        /// The LAST round (remainingRounds &lt;= 1) is a clone with ChooseCount forced to 0 (so
        /// PushEffectContext's entry guard never re-expands it) but otherwise whatever OnSuccess/
        /// Alternative the card actually authored for "after all N rounds" is preserved as-is -
        /// null for every card that exists today, ending the whole effect there, but this leaves
        /// room for a future "choose N times, THEN X" card for free.
        /// </summary>
        private static CardEffect ExpandChoiceRepeat(CardEffect effect, int remainingRounds)
        {
            if (remainingRounds <= 1)
            {
                var final = Card.CloneEffect(effect);
                final.ChooseCount = 0;
                if (final.Alternative != null)
                {
                    final.Alternative.ChooseCount = 0;
                }
                return final;
            }

            var continuation = ExpandChoiceRepeat(effect, remainingRounds - 1);

            var expanded = Card.CloneEffect(effect);
            expanded.ChooseCount = 0;
            expanded.OnSuccess = continuation;

            if (expanded.Alternative != null)
            {
                expanded.Alternative.ChooseCount = 0;
                expanded.Alternative.OnSuccess = continuation;
                expanded.Alternative.Alternative = continuation;
            }

            return expanded;
        }

        /// <summary>
        /// Builds an equivalent, purely transient OnSuccess chain for a CardEffect.
        /// ChainedRepeatCount > 1 node (Graz'zt: "Return any number of your spies -> Supplant a
        /// troop at each of the returned spies' sites") - see CardEffect.ChainedRepeatCount's
        /// doc comment for how this differs from ChooseCount/SupportsRepeat. Each round is a
        /// clone of <paramref name="effect"/> itself (preserving IsOptional, so the player can
        /// decline any round and end the sequence there - "any number" includes zero); a round's
        /// OWN OnSuccess (Graz'zt: Supplant) has BOTH its OnSuccess and Alternative pointed at
        /// the SAME next-round continuation, so a round whose chained step turns out to have no
        /// valid target (e.g. no troop at the site the just-returned spy vacated) still lets the
        /// player attempt another round, rather than silently ending the whole "any number"
        /// sequence early - the identical convergence trick ExpandChoiceRepeat uses. The FINAL
        /// round (remainingRounds &lt;= 1) is a clone with ChainedRepeatCount forced to 0 (so
        /// PushEffectContext's entry guard never re-expands it) whose own OnSuccess is left
        /// exactly as authored, un-overridden, since there is no round after it to converge into.
        /// </summary>
        private static CardEffect ExpandChainedRepeat(CardEffect effect, int remainingRounds)
        {
            if (remainingRounds <= 1)
            {
                var final = Card.CloneEffect(effect);
                final.ChainedRepeatCount = 0;
                final.SkipUnreachableOnSuccessCheck = true;
                return final;
            }

            var continuation = ExpandChainedRepeat(effect, remainingRounds - 1);

            var expanded = Card.CloneEffect(effect);
            expanded.ChainedRepeatCount = 0;
            expanded.SkipUnreachableOnSuccessCheck = true;

            if (expanded.OnSuccess != null)
            {
                expanded.OnSuccess.OnSuccess = continuation;
                expanded.OnSuccess.Alternative = continuation;
            }
            else
            {
                expanded.OnSuccess = continuation;
            }

            return expanded;
        }

        /// <summary>
        /// Resolves WHO this effect acts as/on - normally context.ActivePlayer, but for
        /// CardEffect.TargetsAffectedPlayer (Mindwitness), the outcome-affected opponent (see
        /// ResolveAffectedOpponent) - and gates on that resolved actor's Condition and
        /// HasValidTargets (both take an explicit Player rather than reading
        /// context.ActivePlayer internally, so this needs no ForcedActingPlayer switch to
        /// evaluate). A targeting effect's own Condition used to only be checked for AUTOMATIC
        /// effects (via ApplyEffect's IsConditionMet gate) - a conditional targeting effect
        /// (e.g. Mindwitness's DiscardCard, gated on the affected opponent's hand size) was
        /// never checked here at all, so it would have been pushed and opened for input
        /// unconditionally.
        /// </summary>
        /// <returns>False if this effect should not be pushed at all (the caller falls through
        /// to effect.Alternative in every such case, matching the pre-existing "no valid
        /// targets -> Alternative" behavior) - <paramref name="actor"/> is only meaningful when
        /// this returns true.</returns>
        private static bool TryResolveActor(CardEffect effect, Card card, MatchContext context, bool requiresInput, IGameLogger logger, out Player actor)
        {
            actor = context.ActivePlayer;
            if (effect.TargetsAffectedPlayer)
            {
                var affectedOpponent = ResolveAffectedOpponent(context);
                if (affectedOpponent == null)
                {
                    logger.Log($"{card.Name}: {effect.Type} has no outcome-affected opponent to target (the removed troop/spy wasn't another player's). Effect skipped.", LogChannel.Info);
                    return false;
                }
                actor = affectedOpponent;
            }

            if (!context.CardRuleEngine.IsConditionMet(actor, effect))
            {
                logger.Log($"{card.Name}: {effect.Type}'s condition was not met for {actor.DisplayName}. Effect skipped.", LogChannel.Info);
                return false;
            }

            if (requiresInput && !context.CardRuleEngine.HasValidTargets(actor, effect.Type, card))
            {
                logger.Log($"{card.Name}: No valid targets for {effect.Type}. Effect skipped.", LogChannel.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves CardEffect.TargetsAffectedPlayer to the actual Player whose troop/spy the
        /// immediately preceding Assassinate/Supplant step removed (ActionSystem.
        /// PendingAffectedPlayerColor), or null if that step didn't affect a genuine opponent of
        /// the player currently playing this card - the removed troop was Neutral/unaligned
        /// (belongs to no player), or PendingAffectedPlayerColor was never set at all (e.g. this
        /// flag was set on an effect not actually chained beneath an Assassinate/Supplant).
        /// Evaluated against context.ActivePlayer, which is still the real card-playing player
        /// at this point - nothing has called BeginForcedActingPlayer for THIS effect yet.
        /// </summary>
        private static Player? ResolveAffectedOpponent(MatchContext context)
        {
            var color = context.ActionSystem.PendingAffectedPlayerColor;
            if (color is null || color == PlayerColor.None || color == PlayerColor.Neutral) return null;
            if (color == context.ActivePlayer.Color) return null;

            return context.TurnManager.GetPlayerByColor(color.Value);
        }
    }
}
