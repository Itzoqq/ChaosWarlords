using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]
    [TestCategory("Integration")]
    public class CardDatabaseIntegrationTests
    {
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
