using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Utilities
{
    /// <summary>
    /// Unit coverage for <see cref="CardCatalogValidator"/> - planning.txt TIER 1 item 9's
    /// fail-closed catalog validation gate. Constructs <see cref="CardData"/>/<see
    /// cref="CardEffectData"/> directly rather than through JSON, since this is testing the
    /// validator's own logic, not deserialization (that's already covered by
    /// CardDatabaseIntegrationTests.cs's end-to-end Load/LoadAdditionalFromJson tests).
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class CardCatalogValidatorTests
    {
        private static CardData ValidCard(string id = "some_card") => new()
        {
            Id = id,
            Aspect = "Warlord",
            HalfDeck = "Drow",
            Effects = [new CardEffectData { Type = "GainResource", TargetResource = "Power" }]
        };

        [TestMethod]
        public void Validate_WithAWellFormedCatalog_DoesNotThrow()
        {
            var cards = new List<CardData> { ValidCard("card_a"), ValidCard("card_b") };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        public void Validate_WithDuplicateIds_ThrowsAndNamesTheId()
        {
            var cards = new List<CardData> { ValidCard("dupe"), ValidCard("dupe") };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Duplicate card id 'dupe'");
        }

        [TestMethod]
        public void Validate_WithAnUnknownAspect_Throws()
        {
            var card = ValidCard();
            card.Aspect = "Malice"; // Real rulebook term, but not this codebase's CardAspect member name (Sorcery).
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Aspect 'Malice' is not a known CardAspect value");
        }

        [TestMethod]
        public void Validate_WithAnEmptyAspect_ThrowsBecauseAspectIsRequired()
        {
            var card = ValidCard();
            card.Aspect = "";
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Aspect is required but missing/empty");
        }

        [TestMethod]
        public void Validate_WithAnUnknownHalfDeck_Throws()
        {
            var card = ValidCard();
            card.HalfDeck = "NotARealHalfDeck";
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "HalfDeck 'NotARealHalfDeck' is not a known MarketHalfDeck value");
        }

        [TestMethod]
        public void Validate_WithANullHalfDeckOnAFixedRecruitPileCard_DoesNotThrow()
        {
            // A fixed recruit pile / Insane Outcast's supply pile card never reaches the
            // shuffled-market-selection path at all, so it has no meaningful HalfDeck - unlike a
            // real market card (see the two tests below), it's legitimately optional.
            var card = ValidCard();
            card.HalfDeck = null;
            card.FixedRecruitPileSize = 15;
            var cards = new List<CardData> { card };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        public void Validate_WithANullHalfDeckOnAMarketCard_ThrowsWhenTestPrefixesAreDisallowed()
        {
            // A missing HalfDeck on a real market card must be rejected here -
            // CardDatabase.IsInSelection silently excludes any card whose HalfDeck doesn't parse,
            // so an unset field means the card silently vanishes from the market instead of
            // failing loudly.
            var card = ValidCard();
            card.HalfDeck = null;
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "HalfDeck is required but missing/empty");
        }

        [TestMethod]
        public void Validate_WithANullHalfDeckOnAMarketCard_DoesNotThrowWhenTestPrefixesAreAllowed()
        {
            // LoadAdditionalFromJson's whole purpose is merging test-only fixtures (see
            // TestFixtureCards.cs) that are deliberately market-shaped but never HalfDeck-tagged,
            // since they must never enter the real shuffled market - required-HalfDeck is gated
            // on the same disallowTestPrefixedIds flag as ValidateTestPrefixedId's own check.
            var card = ValidCard();
            card.HalfDeck = null;
            var cards = new List<CardData> { card };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: false);
        }

        [TestMethod]
        public void Validate_WithANumericStringAspect_Throws()
        {
            // Enum.TryParse alone "successfully" parses a bare numeric string to that underlying
            // integer value even when no enum member defines it - only Enum.IsDefined catches it.
            var card = ValidCard();
            card.Aspect = "999";
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Aspect '999' is not a known CardAspect value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownCreatureType_Throws()
        {
            var card = ValidCard();
            card.CreatureType = "Wizard";
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "CreatureType 'Wizard' is not a known CardCreatureType value");
        }

        [TestMethod]
        public void Validate_WithZeroMarketCopyCountOnAMarketCard_Throws()
        {
            var card = ValidCard();
            card.MarketCopyCount = 0;
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "MarketCopyCount must be at least 1 (was 0)");
        }

        [TestMethod]
        public void Validate_WithNegativeMarketCopyCountOnAMarketCard_Throws()
        {
            var card = ValidCard();
            card.MarketCopyCount = -1;
            var cards = new List<CardData> { card };

            Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
        }

        [TestMethod]
        public void Validate_WithZeroMarketCopyCountOnAFixedRecruitPileCard_DoesNotThrow()
        {
            // House Guard/Priestess of Lolth-shaped cards never go through the market-copy
            // expansion path at all - MarketCopyCount is meaningless for them.
            var card = ValidCard();
            card.MarketCopyCount = 0;
            card.FixedRecruitPileSize = 15;
            var cards = new List<CardData> { card };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        public void Validate_WithZeroMarketCopyCountOnARedirectsToSupplyCard_DoesNotThrow()
        {
            // Insane Outcast-shaped cards likewise never go through market-copy expansion.
            var card = ValidCard();
            card.MarketCopyCount = 0;
            card.RedirectsToSupplyOnDevourOrPromote = true;
            var cards = new List<CardData> { card };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        public void Validate_WithAnUnknownEffectType_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "NotARealEffectType" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Type 'NotARealEffectType' is not a known EffectType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownTargetResource_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "GainResource", TargetResource = "Gold" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetResource 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownTargetLocation_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "Devour", TargetLocation = "Basement" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetLocation 'Basement' is not a known CardLocation value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownConditionType_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "GainResource", ConditionType = "IsFullMoon" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "ConditionType 'IsFullMoon' is not a known ConditionType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownConditionResource_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "GainResource", ConditionResource = "Gold" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "ConditionResource 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownConditionPresenceType_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "GainResource", ConditionPresenceType = "Ghost" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "ConditionPresenceType 'Ghost' is not a known SitePresenceType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownDynamicAmountSource_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "GainResource", DynamicAmountSource = "MoonPhase" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "DynamicAmountSource 'MoonPhase' is not a known DynamicAmountSource value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownRequiredPromotionAspect_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "Promote", RequiredPromotionAspect = "Ambition" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "RequiredPromotionAspect 'Ambition' is not a known CardAspect value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownRequiredPromotionCreatureType_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "Promote", RequiredPromotionCreatureType = "Zombie" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "RequiredPromotionCreatureType 'Zombie' is not a known CardCreatureType value");
        }

        [TestMethod]
        public void Validate_WithAnUnknownGainResourcePerRepeat_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "Assassinate", GainResourcePerRepeat = "Gold" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "GainResourcePerRepeat 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithATargetCardIdReferencingAnUnknownCard_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "ForceRecruit", TargetCardId = "not_a_real_card" }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetCardId 'not_a_real_card' does not reference any known card");
        }

        [TestMethod]
        public void Validate_WithATargetCardIdReferencingARealCardInTheSameBatch_DoesNotThrow()
        {
            var giver = ValidCard("giver");
            giver.Effects = [new CardEffectData { Type = "ForceRecruit", TargetCardId = "receiver" }];
            var receiver = ValidCard("receiver");
            var cards = new List<CardData> { giver, receiver };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        [DataRow("soldier")]
        [DataRow("noble")]
        public void Validate_WithATargetCardIdReferencingASyntheticStartingDeckId_DoesNotThrow(string syntheticId)
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "ForceRecruit", TargetCardId = syntheticId }];
            var cards = new List<CardData> { card };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true);
        }

        [TestMethod]
        public void Validate_WithABadEnumNestedUnderOnSuccess_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "Devour", OnSuccess = new CardEffectData { Type = "Supplant", TargetResource = "Gold" } }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetResource 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithABadEnumNestedUnderAlternative_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData { Type = "PlaceSpy", IsOptional = true, Alternative = new CardEffectData { Type = "NotReal" } }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "Type 'NotReal' is not a known EffectType value");
        }

        [TestMethod]
        public void Validate_WithABadEnumNestedUnderPromotionCompletionEffect_Throws()
        {
            var card = ValidCard();
            card.Effects = [new CardEffectData
            {
                Type = "Promote",
                PromotionCompletionEffect = new CardEffectData { Type = "GainResource", TargetResource = "Gold" }
            }];
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetResource 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithABadEnumInReactiveDiscardEffect_Throws()
        {
            var card = ValidCard();
            card.ReactiveDiscardEffect = new CardEffectData { Type = "GainResource", TargetResource = "Gold" };
            var cards = new List<CardData> { card };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "TargetResource 'Gold' is not a known ResourceType value");
        }

        [TestMethod]
        public void Validate_WithATestPrefixedIdAndDisallowTestPrefixedIdsTrue_Throws()
        {
            var cards = new List<CardData> { ValidCard("test_fixture") };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));
            StringAssert.Contains(ex.Message, "test-prefixed ids may not ship in production data");
        }

        [TestMethod]
        public void Validate_WithATestPrefixedIdAndDisallowTestPrefixedIdsFalse_DoesNotThrow()
        {
            // LoadAdditionalFromJson's whole purpose is merging test_-prefixed fixtures.
            var cards = new List<CardData> { ValidCard("test_fixture") };

            CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: false);
        }

        [TestMethod]
        public void CountHalfDeckCopies_SumsMarketCopyCountAcrossOnlyTheRequestedHalfDeck()
        {
            var drowOne = ValidCard("drow_one");
            drowOne.HalfDeck = "Drow";
            drowOne.MarketCopyCount = 3;
            var drowTwo = ValidCard("drow_two");
            drowTwo.HalfDeck = "Drow";
            drowTwo.MarketCopyCount = 2;
            var dragon = ValidCard("dragon_one");
            dragon.HalfDeck = "Dragons";
            dragon.MarketCopyCount = 40;
            var cards = new List<CardData> { drowOne, drowTwo, dragon };

            Assert.AreEqual(5, CardCatalogValidator.CountHalfDeckCopies(cards, MarketHalfDeck.Drow));
            Assert.AreEqual(40, CardCatalogValidator.CountHalfDeckCopies(cards, MarketHalfDeck.Dragons));
            Assert.AreEqual(0, CardCatalogValidator.CountHalfDeckCopies(cards, MarketHalfDeck.Undead));
        }

        [TestMethod]
        public void CountHalfDeckCopies_ExcludesFixedRecruitPileAndSupplyCardsEvenIfHalfDeckTagged()
        {
            var fixedPile = ValidCard("fixed_pile_card");
            fixedPile.HalfDeck = "Drow";
            fixedPile.MarketCopyCount = 15;
            fixedPile.FixedRecruitPileSize = 15;
            var supplyCard = ValidCard("supply_card");
            supplyCard.HalfDeck = "Drow";
            supplyCard.MarketCopyCount = 30;
            supplyCard.RedirectsToSupplyOnDevourOrPromote = true;
            var cards = new List<CardData> { fixedPile, supplyCard };

            Assert.AreEqual(0, CardCatalogValidator.CountHalfDeckCopies(cards, MarketHalfDeck.Drow));
        }

        [TestMethod]
        public void Validate_WithMultipleIndependentProblems_AggregatesAllOfThemIntoOneException()
        {
            var badAspect = ValidCard("card_one");
            badAspect.Aspect = "NotReal";
            var badEffect = ValidCard("card_two");
            badEffect.Effects = [new CardEffectData { Type = "GainResource", TargetResource = "Gold" }];
            var cards = new List<CardData> { badAspect, badEffect };

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => CardCatalogValidator.Validate(cards, disallowTestPrefixedIds: true));

            StringAssert.Contains(ex.Message, "card_one");
            StringAssert.Contains(ex.Message, "card_two");
        }
    }
}
