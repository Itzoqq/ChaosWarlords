using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Tests.Source.Functional;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]
    [TestCategory("Integration")]
    public class CardDatabaseIntegrationTests
    {
        [TestMethod]
        public void Load_GetMarketCards_ExpandsEachDefinitionByItsDeclaredCopyCount()
        {
            var database = new CardDatabase(new TestLocalizationService());
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
                [{ "id": "warlord_card", "aspect": "Warlord", "halfDeck": "Drow", "marketCopyCount": 3, "effects": [] }]
                """));
            database.Load(stream);

            var cards = database.GetMarketCards(new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons));

            Assert.HasCount(3, cards);
            Assert.IsTrue(cards.All(card => card.DefinitionId == "warlord_card"));
            Assert.HasCount(3, cards.Select(card => card.RuntimeId).Distinct());
        }

        [TestMethod]
        public void Load_WithZeroCopyCount_RejectsInvalidCardDataAtLoadTime()
        {
            // CardCatalogValidator (planning.txt TIER 1 item 9) now catches this eagerly at
            // Load() time, not lazily the first time a market is actually built - Load() itself
            // must throw here, not a later GetMarketCards() call.
            var database = new CardDatabase(new TestLocalizationService());
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
                [{ "id": "invalid_card", "aspect": "Warlord", "halfDeck": "Drow", "marketCopyCount": 0, "effects": [] }]
                """));

            Assert.ThrowsExactly<InvalidDataException>(() => database.Load(stream));
        }

        [TestMethod]
        public void LoadAdditionalFromJson_MergesNewCards_WithoutReplacingTheOriginalCatalog()
        {
            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
                [{ "id": "warlord_card", "aspect": "Warlord", "halfDeck": "Drow", "marketCopyCount": 1, "effects": [] }]
                """)))
            {
                database.Load(stream);
            }

            database.LoadAdditionalFromJson("""
                [{ "id": "test_fixture_card", "aspect": "Order", "marketCopyCount": 1, "effects": [] }]
                """);

            var ids = database.GetAllMarketCards().Select(card => card.DefinitionId).ToHashSet();
            Assert.Contains("warlord_card", ids, "The originally-loaded catalog must survive a merge, unlike Load's replace-everything semantics.");
            Assert.Contains("test_fixture_card", ids);
        }

        [TestMethod]
        public void LoadAdditionalFromJson_WithAnIdAlreadyInTheCatalog_ThrowsInsteadOfSilentlyDuplicating()
        {
            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
                [{ "id": "warlord_card", "aspect": "Warlord", "halfDeck": "Drow", "marketCopyCount": 1, "effects": [] }]
                """)))
            {
                database.Load(stream);
            }

            Assert.ThrowsExactly<InvalidDataException>(() => database.LoadAdditionalFromJson("""
                [{ "id": "warlord_card", "aspect": "Order", "marketCopyCount": 1, "effects": [] }]
                """));

            // The original definition must survive the rejected merge attempt, unmodified.
            var cards = database.GetAllMarketCards();
            Assert.HasCount(1, cards);
            Assert.AreEqual(CardAspect.Warlord, cards[0].Aspect);
        }

        [TestMethod]
        public void LoadAdditionalFromJson_WithTwoEntriesSharingAnIdInTheSameBatch_ThrowsInsteadOfSilentlyPickingOne()
        {
            // Reviewer finding (2026-09-13): the original guard only checked each incoming
            // entry against the ALREADY-loaded catalog, never against the rest of the SAME
            // incoming batch - two fixture entries with the same id would both silently pass
            // and CardDatabase.GetCardById's FirstOrDefault would resolve to whichever happened
            // to be first, exactly the "silently shadow or duplicate" failure this method's own
            // doc comment claims to prevent.
            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
                [{ "id": "warlord_card", "aspect": "Warlord", "halfDeck": "Drow", "marketCopyCount": 1, "effects": [] }]
                """)))
            {
                database.Load(stream);
            }

            Assert.ThrowsExactly<InvalidDataException>(() => database.LoadAdditionalFromJson("""
                [
                  { "id": "duplicate_fixture", "aspect": "Order", "marketCopyCount": 1, "effects": [] },
                  { "id": "duplicate_fixture", "aspect": "Shadow", "marketCopyCount": 1, "effects": [] }
                ]
                """));

            // Nothing from the rejected batch should have been merged - not even the
            // originally-loaded catalog entry should be affected.
            var ids = database.GetAllMarketCards().Select(card => card.DefinitionId).ToHashSet();
            Assert.HasCount(1, ids);
            Assert.Contains("warlord_card", ids);
        }

        [TestMethod]
        public void LoadRealCardsJson_GetMarketCards_ReturnsOnlyTheSelectedHalfDecks()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using var stream = File.OpenRead(path);
            database.Load(stream);

            // Rulebook p.4's own first-game recommendation - also MarketDeckSelection.Default.
            var cards = database.GetMarketCards(new MarketDeckSelection(MarketHalfDeck.Drow, MarketHalfDeck.Dragons));
            var ids = cards.Select(card => card.DefinitionId).ToHashSet();

            Assert.Contains("advance_scout", ids, "A selected Drow half-deck card should enter the market deck.");
            Assert.Contains("black_dragon", ids, "A selected Dragons half-deck card should enter the market deck.");
            Assert.Contains("masters_of_sorcere", ids, "Every Drow half-deck card should enter the market deck, regardless of its Aspect.");
            Assert.DoesNotContain("demogorgon", ids, "A Demons half-deck card must not enter a Drow/Dragons market deck.");
            Assert.DoesNotContain("aboleth", ids, "An Aberrations half-deck card must not enter a Drow/Dragons market deck.");
            Assert.DoesNotContain("wight", ids, "An Undead half-deck card must not enter a Drow/Dragons market deck.");
            Assert.DoesNotContain("core_house_guard", ids, "House Guard belongs only to its fixed recruit pile, never the shuffled market deck.");
            Assert.DoesNotContain("core_priestess", ids, "Priestess belongs only to its fixed recruit pile, never the shuffled market deck.");
            Assert.DoesNotContain("insane_outcast", ids, "Insane Outcast redirects to its own supply pile, never the shuffled market deck.");
        }

        [TestMethod]
        public void LoadRealCardsJson_GetMarketCards_WorksForTheExpansionHalfDeckPairToo()
        {
            // Proves the mechanism generalizes beyond the Default/first-game combination -
            // Aberrations+Undead is the "Aberrations and Undead" expansion's own pair, which the
            // expansion's product info states "can be paired with any of the game's existing
            // half-decks" (i.e. the identical rule, not a special case).
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using var stream = File.OpenRead(path);
            database.Load(stream);

            var cards = database.GetMarketCards(new MarketDeckSelection(MarketHalfDeck.Aberrations, MarketHalfDeck.Undead));
            var ids = cards.Select(card => card.DefinitionId).ToHashSet();

            Assert.Contains("aboleth", ids, "A selected Aberrations half-deck card should enter the market deck.");
            Assert.Contains("wight", ids, "A selected Undead half-deck card should enter the market deck.");
            Assert.DoesNotContain("advance_scout", ids, "A Drow half-deck card must not enter an Aberrations/Undead market deck.");
            Assert.DoesNotContain("black_dragon", ids, "A Dragons half-deck card must not enter an Aberrations/Undead market deck.");
        }

        [TestMethod]
        public void LoadRealCardsJson_GetFixedRecruitPiles_CreatesTwoFiniteFifteenCardPiles()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using var stream = File.OpenRead(path);
            database.Load(stream);

            var piles = database.GetFixedRecruitPiles();

            Assert.HasCount(2, piles);
            CollectionAssert.AreEquivalent(new[] { "core_house_guard", "core_priestess" }, piles.Select(pile => pile.DefinitionId).ToArray());
            Assert.IsTrue(piles.All(pile => pile.Cards.Count == 15), "Each fixed recruit pile must contain its complete finite physical supply.");
            Assert.IsTrue(piles.SelectMany(pile => pile.Cards).All(card => card.Location == CardLocation.Market));
        }
        [TestMethod]
        public void LoadRealCardsJson_GetCompleteHalfDecks_ReportsOnlyDrowAndDragons()
        {
            // planning.txt TIER 1 item 14: Drow+Dragons are the only 2 half-decks with a real,
            // scan-verified 40-card count today - the other 4 are still mid-transcription
            // (TIER 4 item 28) and must not be reported as safe to select for a real match.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using var stream = File.OpenRead(path);
            database.Load(stream);

            var complete = database.GetCompleteHalfDecks();

            CollectionAssert.AreEquivalent(new[] { MarketHalfDeck.Drow, MarketHalfDeck.Dragons }, complete.ToArray());
        }

        [TestMethod]
        public void LoadRealCardsJson_EveryMarketCard_ResolvesNameAndDescriptionFromTheRealBundle()
        {
            // Regression test for the localization key indirection (planning.txt TIER 1,
            // 2026-09-01): every card in the real cards.json must have a matching
            // "{Id}_name"/"{Id}_description" entry in the real en_US.json bundle - a typo'd
            // or missing key would silently ship a "[MISSING:...]" card name/description
            // instead of failing a build, so assert it here instead.
            var cardsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            var localizationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/localization/en_US.json");
            if (!File.Exists(cardsPath)) Assert.Inconclusive("cards.json not found at " + cardsPath);
            if (!File.Exists(localizationPath)) Assert.Inconclusive("en_US.json not found at " + localizationPath);

            var localization = new LocalizationManager();
            using (var locStream = File.OpenRead(localizationPath))
            {
                localization.Load(locStream);
            }

            var database = new CardDatabase(localization);
            using (var stream = File.OpenRead(cardsPath))
            {
                database.Load(stream);
            }

            var marketCards = database.GetAllMarketCards();
            Assert.IsNotEmpty(marketCards, "Sanity check: cards.json should have produced at least one market card.");

            foreach (var card in marketCards)
            {
                Assert.DoesNotContain("[MISSING:", card.Name, $"{card.Id}: Name resolved to a missing-key placeholder - add the matching key to en_US.json.");
                Assert.DoesNotContain("[MISSING:", card.Description, $"{card.Id}: Description resolved to a missing-key placeholder - add the matching key to en_US.json.");
            }

            // RedirectsToSupplyOnDevourOrPromote cards (e.g. Insane Outcast) are excluded from
            // GetAllMarketCards - check those by id directly so they're not silently skipped.
            var supplyOnlyCard = database.GetCardById("insane_outcast");
            Assert.IsNotNull(supplyOnlyCard, "insane_outcast should exist in cards.json.");
            Assert.DoesNotContain("[MISSING:", supplyOnlyCard.Name);
            Assert.DoesNotContain("[MISSING:", supplyOnlyCard.Description);
        }

        [TestMethod]
        public void LoadRealCardsJson_EveryShuffledMarketCard_HasAValidHalfDeckTag()
        {
            // Regression test (planning.txt TIER 1 item 5): a card missing its HalfDeck tag (or
            // with a mistyped one) doesn't fail loudly today - it's simply invisible to EVERY
            // possible MarketDeckSelection, a silent "this card can never be drawn" bug. Assert
            // every card that's neither a fixed recruit pile (FixedRecruitPileSize>0) nor a
            // supply-redirect card (RedirectsToSupplyOnDevourOrPromote, e.g. Insane Outcast) nor
            // a test-only fixture (TestFixtureCards) has a HalfDeck value that parses as one of
            // the 6 real MarketHalfDeck values.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var missingOrInvalid = new List<string>();
            foreach (var card in document.RootElement.EnumerateArray())
            {
                string id = card.GetProperty("Id").GetString() ?? string.Empty;
                bool isFixedRecruitPile = card.TryGetProperty("FixedRecruitPileSize", out var pileSize) && pileSize.GetInt32() > 0;
                bool redirectsToSupply = card.TryGetProperty("RedirectsToSupplyOnDevourOrPromote", out var redirects) && redirects.GetBoolean();
                if (isFixedRecruitPile || redirectsToSupply) continue;

                bool hasValidHalfDeck = card.TryGetProperty("HalfDeck", out var halfDeckProperty)
                    && Enum.TryParse<MarketHalfDeck>(halfDeckProperty.GetString(), ignoreCase: true, out _);
                if (!hasValidHalfDeck) missingOrInvalid.Add(id);
            }

            Assert.IsEmpty(missingOrInvalid, $"Every shuffled market card must have a valid HalfDeck tag. Missing/invalid: {string.Join(", ", missingOrInvalid)}");
        }

        [TestMethod]
        public void LoadRealCardsJson_ContainsNoTestPrefixedFixtureCards()
        {
            // Regression test (planning.txt TIER 1 items 3 and 5): 5 single-primitive-shape
            // fixture cards (test_assassin/test_guard/test_infiltrator/test_blade_dancer/
            // test_displacer, 2026-09-13) plus core_noble (2026-09-14 - doesn't correspond to
            // any real card at all) used to ship in the real market data with no fixture flag,
            // meaning they could enter a real match's market whenever their aspect/half-deck was
            // selected. All 6 moved to a test-owned fixture
            // (ChaosWarlords.Tests.Source.Functional.TestFixtureCards, merged into
            // MatchScenario's CardDatabase via LoadAdditionalFromJson) - assert here that the
            // PRODUCTION catalog never regains any of them, by any name. Checks against
            // TestFixtureCards's own defined ids directly (not just the "test_" prefix pattern)
            // so this list and the fixture's own list can never silently drift apart - a future
            // fixture card added WITHOUT a "test_" prefix (exactly how core_noble slipped past
            // this check's original, narrower form) is still caught. Parses the raw JSON
            // directly (not through CardDatabase's GetAllMarketCards/GetFixedRecruitPiles) so
            // this also catches a future fixture-shaped card flagged FixedRecruitPileSize>0 or
            // RedirectsToSupplyOnDevourOrPromote - either of which would make it invisible to a
            // check built on those two accessors alone.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            using var fixtureDocument = System.Text.Json.JsonDocument.Parse(TestFixtureCards.CardsJson);
            var fixtureIds = fixtureDocument.RootElement.EnumerateArray()
                .Select(card => card.GetProperty("Id").GetString() ?? string.Empty)
                .ToHashSet();

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var leakedFixtureIds = document.RootElement.EnumerateArray()
                .Select(card => card.TryGetProperty("Id", out var idProperty) ? idProperty.GetString() ?? string.Empty : string.Empty)
                .Where(id => fixtureIds.Contains(id) || id.StartsWith("test_", StringComparison.Ordinal))
                .ToList();

            Assert.IsEmpty(leakedFixtureIds, $"Production cards.json must never ship a test-fixture card. Found: {string.Join(", ", leakedFixtureIds)}");
        }

        [TestMethod]
        public void LoadRealCardsJson_VerifyWight_HasSupplantSuccess()
        {
            // Arrange
            // Adjust path to point to Content relative to the executed DLL or project root
            // The previous test used "../../../../ChaosWarlords/" which implies running from bin/Debug/net10.0
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = File.OpenRead(path))
            {
                database.Load(stream);
            }

            // Act
            var card = database.GetCardById("wight");

            // Assert
            Assert.IsNotNull(card, "Wight card should exist");
            var devourEffect = card.Effects.FirstOrDefault(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect, "Wight should have Devour effect");
            Assert.IsNotNull(devourEffect.OnSuccess, "Wight Devour should have OnSuccess");
            Assert.AreEqual(EffectType.Supplant, devourEffect.OnSuccess.Type, "OnSuccess should be Supplant");

            // Verify Logic Predicate
            Assert.IsTrue(new ChaosWarlords.Source.Mechanics.Rules.Strategies.DevourStrategy().IsTargetingEffect, "Devour should be considered a Targeting Effect");
            var supplantStrategy = new ChaosWarlords.Source.Mechanics.Rules.Strategies.SupplantStrategy();
            Assert.IsTrue(supplantStrategy.IsTargetingEffect, "Supplant should be considered a Targeting Effect");
        }

        [TestMethod]
        public void LoadRealCardsJson_VerifyCarrionCrawler_HasPowerGainAndMarketDevour()
        {
            // Regression test: the real card is "+3 Power. Devour a card in the market and
            // replace it with this card." - the shipped JSON was missing the +3 Power effect
            // entirely (found by cross-checking against the real card image, see planning.txt
            // RESOLVED).
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase(new TestLocalizationService());
            using (var stream = File.OpenRead(path))
            {
                database.Load(stream);
            }

            var card = database.GetCardById("carrion_crawler");

            Assert.IsNotNull(card, "Carrion Crawler card should exist");

            var gainEffect = card.Effects.FirstOrDefault(e => e.Type == EffectType.GainResource);
            Assert.IsNotNull(gainEffect, "Carrion Crawler should have a GainResource effect");
            Assert.AreEqual(ResourceType.Power, gainEffect.TargetResource);
            Assert.AreEqual(3, gainEffect.Amount);

            var devourEffect = card.Effects.FirstOrDefault(e => e.Type == EffectType.Devour);
            Assert.IsNotNull(devourEffect, "Carrion Crawler should have a Devour effect");
            Assert.AreEqual(CardLocation.Market, devourEffect.TargetLocation);
            Assert.IsTrue(devourEffect.ReplaceWithSource, "Carrion Crawler devour should replace the market slot with itself, not the deck top.");
        }
    }
}
