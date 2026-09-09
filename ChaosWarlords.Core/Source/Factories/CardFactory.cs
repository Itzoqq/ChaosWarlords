using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Utilities
{
    public static class CardFactory
    {
        private static string GenerateUniqueId(string baseId, IGameRandom? random = null)
        {
            if (random != null)
            {
                return $"{baseId}_{random.NextInt(1000000).ToString("x6", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            return $"{baseId}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
        }

        public static Card CreateSoldier(IGameRandom? random = null)
        {
            var card = new Card(GenerateUniqueId("soldier", random), "Soldier", 0, CardAspect.Neutral, 0, 0, 0, definitionId: "soldier");
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Power));
            card.Description = "+1 Power";
            return card;
        }

        public static Card CreateNoble(IGameRandom? random = null)
        {
            var card = new Card(GenerateUniqueId("noble", random), "Noble", 0, CardAspect.Neutral, 0, 0, 0, definitionId: "noble");
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.Description = "+1 Influence";
            return card;
        }

        public static Card CreateFromData(CardData data, ILocalizationService localization, IGameRandom? random = null, IGameLogger? logger = null)
        {
            Enum.TryParse(data.Aspect, true, out CardAspect aspect);

            // Name/Description are resolved from the localization bundle, keyed off the
            // card's definitional Id (NOT the randomized runtime Card.Id generated below) -
            // "{Id}_name"/"{Id}_description". See CardDatabase's CardData doc comment.
            string name = localization.GetString($"{data.Id}_name");

            // Using 0 for influence as default
            var card = new Card(GenerateUniqueId(data.Id, random), name, data.Cost, aspect, data.DeckVP, data.InnerCircleVP, 0, definitionId: data.Id);

            card.Description = localization.GetString($"{data.Id}_description");
            card.RedirectsToSupplyOnDevourOrPromote = data.RedirectsToSupplyOnDevourOrPromote;

            if (data.Effects is not null)
            {
                foreach (var effectData in data.Effects)
                {
                    var effect = CreateEffect(effectData, logger);
                    if (effect != null)
                    {
                        card.AddEffect(effect);
                    }
                }
            }

            ParseReactiveDiscardEffect(data, card, logger);

            return card;
        }

        private static void ParseReactiveDiscardEffect(CardData data, Card card, IGameLogger? logger)
        {
            if (data.ReactiveDiscardEffect is null)
            {
                return;
            }

            card.ReactiveDiscardEffect = CreateEffect(data.ReactiveDiscardEffect, logger);

            // DiscardCardCommand applies this via a bare CardEffectProcessor.ApplyEffect call
            // with no follow-up ResolveCurrentEffect - unlike every other ApplyEffect call site,
            // nothing ever pushes OnSuccess/Alternative onto ExecutionStack for it. A card whose
            // ReactiveDiscardEffect chains would silently drop that chain in play instead of
            // erroring - warn loudly at load time instead, so this is caught before a card
            // ships, not discovered as "this card underperforms."
            bool hasUnsupportedChain = card.ReactiveDiscardEffect?.OnSuccess != null || card.ReactiveDiscardEffect?.Alternative != null;
            if (hasUnsupportedChain)
            {
                logger?.Log($"[CardFactory] {data.Id}: ReactiveDiscardEffect has an OnSuccess/Alternative chain, which DiscardCardCommand does not resolve - it will be silently dropped in play. Not supported yet.", LogChannel.Warning);
            }
        }

        private static CardEffect? CreateEffect(CardEffectData data, IGameLogger? logger)
        {
            if (!Enum.TryParse(data.Type, true, out EffectType type))
                return null;

            var effect = CreateBaseEffect(data, type);
            ParseTargetLocation(data, effect, logger);
            ParseRecursiveEffect(data, effect, logger);
            ParseCondition(data, effect);
            ParseOptionalFlags(data, effect);
            ParseDynamicAmount(data, effect, logger);
            ParseGainResourcePerRepeat(data, effect, logger);
            ParsePromotionCompletionEffect(data, effect, logger);
            WarnIfChooseCountShapeIsUnsupported(data, effect, logger);
            WarnIfChainedRepeatCountShapeIsUnsupported(data, effect, logger);
            WarnIfGainResourcePerRepeatShapeIsUnsupported(data, effect, logger);
            WarnIfPromotionCompletionEffectShapeIsUnsupported(data, effect, logger);

            return effect;
        }

        // Shared between ChooseCount and ChainedRepeatCount's own sanity warnings below - both
        // are runtime tree-expansion recursion depths (ExpandChoiceRepeat/ExpandChainedRepeat),
        // so an unreasonably large authored value is equally likely a typo either way.
        private const int RepeatChainSanityCeiling = 10;

        // CardEffect.ChooseCount > 1 needs a real 2nd branch to alternate with (Alternative), and
        // CardEffectProcessor.ExpandChoiceRepeat OVERWRITES this node's and its Alternative's own
        // OnSuccess/Alternative on every round except the last - warn loudly at load time rather
        // than let an author's authored chain silently vanish in play (same "catch it before it
        // ships" precedent as ParseReactiveDiscardEffect's own warning above). Does NOT warn on
        // ChooseCount set on a non-IsOptional node - HandleInputRequiredEffect only ever raises
        // the accept/decline popup for an IsOptional effect, so a mandatory node would never
        // actually offer a real per-round choice (Alternative only reached via "no valid
        // target"); left unflagged since no card needs this shape and it's unclear whether it's
        // ever intentional.
        private static void WarnIfChooseCountShapeIsUnsupported(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (effect.ChooseCount <= 1)
            {
                return;
            }

            if (effect.ChooseCount > RepeatChainSanityCeiling)
            {
                logger?.Log($"[CardFactory] {data.Type}: ChooseCount={effect.ChooseCount} is unusually large (>{RepeatChainSanityCeiling}) - likely a typo (ExpandChoiceRepeat recurses this deep on every play); proceeding anyway.", LogChannel.Warning);
            }

            if (effect.Alternative == null)
            {
                logger?.Log($"[CardFactory] {data.Type}: ChooseCount={effect.ChooseCount} has no Alternative to choose between - not a real 2-option choice.", LogChannel.Warning);
                return;
            }

            WarnIfChooseCountChainWouldBeOverridden(data, effect, logger);
        }

        private static void WarnIfChooseCountChainWouldBeOverridden(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            bool hasChainThatWillBeOverridden = effect.OnSuccess != null || effect.Alternative!.OnSuccess != null || effect.Alternative.Alternative != null;
            if (!hasChainThatWillBeOverridden)
            {
                return;
            }

            logger?.Log($"[CardFactory] {data.Type}: ChooseCount={effect.ChooseCount}'s own OnSuccess/Alternative chain is only honored on the LAST round - every earlier round overrides it with the next round's continuation.", LogChannel.Warning);
        }

        // CardEffect.ChainedRepeatCount > 1 needs a real OnSuccess child to repeat as a pair
        // (Graz'zt: ReturnOwnSpy.OnSuccess = Supplant) - without one, ExpandChainedRepeat just
        // repeats a single-step node N times with nothing to converge, which is likely not what
        // was intended. Same "catch it before it ships" precedent as ChooseCount's warning above.
        private static void WarnIfChainedRepeatCountShapeIsUnsupported(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (effect.ChainedRepeatCount <= 1)
            {
                return;
            }

            if (effect.ChainedRepeatCount > RepeatChainSanityCeiling)
            {
                logger?.Log($"[CardFactory] {data.Type}: ChainedRepeatCount={effect.ChainedRepeatCount} is unusually large (>{RepeatChainSanityCeiling}) - likely a typo (ExpandChainedRepeat recurses this deep on every play); proceeding anyway.", LogChannel.Warning);
            }

            if (effect.OnSuccess == null)
            {
                logger?.Log($"[CardFactory] {data.Type}: ChainedRepeatCount={effect.ChainedRepeatCount} has no OnSuccess to repeat as a pair - each round would just re-offer the same single step with nothing chained after it.", LogChannel.Warning);
            }

            // Without IsOptional, HandleInputRequiredEffect never reaches ProcessOptionalEffect/
            // HasUnreachableOnSuccess for this node at all (see SetupTargetingForRequiredEffect's
            // own branch) - CardEffect.SkipUnreachableOnSuccessCheck (also set by
            // ExpandChainedRepeat) would then have nothing to bypass, and "return any number,
            // including zero" becomes a MANDATORY forced repeat for as many rounds as valid
            // targets exist instead.
            if (!effect.IsOptional)
            {
                logger?.Log($"[CardFactory] {data.Type}: ChainedRepeatCount={effect.ChainedRepeatCount} is not IsOptional - each round would be a MANDATORY repeat, not a voluntary 'any number, including zero' sequence.", LogChannel.Warning);
            }
        }

        private static CardEffect CreateBaseEffect(CardEffectData data, EffectType type)
        {
            ResourceType resType = ResourceType.None;
            if (!string.IsNullOrEmpty(data.TargetResource))
            {
                Enum.TryParse(data.TargetResource, true, out resType);
            }

            var effect = new CardEffect(type, data.Amount, resType);
            effect.RequiresFocus = data.RequiresFocus;
            return effect;
        }

        private static void ParseTargetLocation(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (string.IsNullOrEmpty(data.TargetLocation))
                return;

            if (Enum.TryParse(data.TargetLocation, true, out CardLocation targetLoc))
            {
                effect.TargetLocation = targetLoc;
            }
            else
            {
                logger?.Log($"[CardFactory] FAILED to parse TargetLocation: {data.TargetLocation}", LogChannel.Warning);
            }
        }

        private static void ParseRecursiveEffect(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (data.OnSuccess != null)
            {
                effect.OnSuccess = CreateEffect(data.OnSuccess, logger);
            }

            if (data.Alternative != null)
            {
                effect.Alternative = CreateEffect(data.Alternative, logger);
            }
        }

        private static void ParseCondition(CardEffectData data, CardEffect effect)
        {
            if (string.IsNullOrEmpty(data.ConditionType))
                return;

            if (!Enum.TryParse(data.ConditionType, true, out ConditionType condType))
                return;

            ResourceType condRes = ResourceType.None;
            if (!string.IsNullOrEmpty(data.ConditionResource))
            {
                Enum.TryParse(data.ConditionResource, true, out condRes);
            }

            SitePresenceType? condPresenceType = null;
            if (!string.IsNullOrEmpty(data.ConditionPresenceType) &&
                Enum.TryParse(data.ConditionPresenceType, true, out SitePresenceType parsedPresenceType))
            {
                condPresenceType = parsedPresenceType;
            }

            effect.Condition = new EffectCondition(condType, data.ConditionThreshold, condRes, condPresenceType);
        }

        private static void ParseOptionalFlags(CardEffectData data, CardEffect effect)
        {
            effect.IsOptional = data.IsOptional;
            effect.ReplaceWithSource = data.ReplaceWithSource;
            effect.TargetNeutralTroopOnly = data.TargetNeutralTroopOnly;
            effect.IgnoresPresenceRequirement = data.IgnoresPresenceRequirement;
            effect.TargetsAffectedPlayer = data.TargetsAffectedPlayer;
            effect.AllowPartialRepeat = data.AllowPartialRepeat;
            effect.RestrictRepeatsToFirstTargetSite = data.RestrictRepeatsToFirstTargetSite;
            effect.PromotionCreditIsOptional = data.PromotionCreditIsOptional;
            effect.ChooseCount = data.ChooseCount;
            effect.ChainedRepeatCount = data.ChainedRepeatCount;
            effect.SkipUnreachableOnSuccessCheck = data.SkipUnreachableOnSuccessCheck;
            effect.RequiresAdjacencyToRecentDeploys = data.RequiresAdjacencyToRecentDeploys;
            effect.TargetCardId = data.TargetCardId;
        }

        private static void ParseDynamicAmount(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            effect.DynamicAmountDivisor = data.DynamicAmountDivisor;

            if (string.IsNullOrEmpty(data.DynamicAmountSource))
                return;

            if (Enum.TryParse(data.DynamicAmountSource, true, out DynamicAmountSource source))
            {
                effect.DynamicAmountSource = source;
            }
            else
            {
                logger?.Log($"[CardFactory] FAILED to parse DynamicAmountSource: {data.DynamicAmountSource}", LogChannel.Warning);
            }
        }

        private static void ParseGainResourcePerRepeat(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (string.IsNullOrEmpty(data.GainResourcePerRepeat))
                return;

            if (Enum.TryParse(data.GainResourcePerRepeat, true, out ResourceType resource))
            {
                effect.GainResourcePerRepeat = resource;
            }
            else
            {
                logger?.Log($"[CardFactory] FAILED to parse GainResourcePerRepeat: {data.GainResourcePerRepeat}", LogChannel.Warning);
            }
        }

        // CardEffect.GainResourcePerRepeat is only ever read by ActionSystem.PerformAssassinate -
        // authoring it on any other EffectType would parse and clone fine but silently never
        // fire, exactly the "catch it before it ships" gap ChooseCount/ChainedRepeatCount's own
        // warnings above exist to close.
        private static void WarnIfGainResourcePerRepeatShapeIsUnsupported(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (effect.GainResourcePerRepeat == ResourceType.None)
            {
                return;
            }

            if (effect.Type != EffectType.Assassinate)
            {
                logger?.Log($"[CardFactory] {data.Type}: GainResourcePerRepeat={effect.GainResourcePerRepeat} is only wired for EffectType.Assassinate - it will parse and clone but never actually fire on this effect type.", LogChannel.Warning);
            }
        }

        private static void ParsePromotionCompletionEffect(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (data.PromotionCompletionEffect != null)
            {
                effect.PromotionCompletionEffect = CreateEffect(data.PromotionCompletionEffect, logger);
            }
        }

        // CardEffect.PromotionCompletionEffect is only ever read by ApplyPromote (registered
        // into TurnContext, applied later by MatchManager.EndTurn) - authoring it on any other
        // EffectType would parse and clone fine but silently never fire. Also warns if the
        // completion effect itself chains further (OnSuccess/Alternative) - MatchManager.EndTurn
        // applies it via a direct CardEffectProcessor.ApplyEffect call with no EffectContext ever
        // built for it, the same "chain not propagated" limitation ParseReactiveDiscardEffect's
        // own warning already flags for Grimlock's mechanism.
        private static void WarnIfPromotionCompletionEffectShapeIsUnsupported(CardEffectData data, CardEffect effect, IGameLogger? logger)
        {
            if (effect.PromotionCompletionEffect == null)
            {
                return;
            }

            if (effect.Type != EffectType.Promote)
            {
                logger?.Log($"[CardFactory] {data.Type}: PromotionCompletionEffect is only wired for EffectType.Promote - it will parse and clone but never actually fire on this effect type.", LogChannel.Warning);
            }

            if (effect.PromotionCompletionEffect.OnSuccess != null || effect.PromotionCompletionEffect.Alternative != null)
            {
                logger?.Log($"[CardFactory] {data.Type}: PromotionCompletionEffect has an OnSuccess/Alternative chain, which MatchManager.EndTurn does not resolve - it will be silently dropped in play. Not supported yet.", LogChannel.Warning);
            }
        }
    }
}


