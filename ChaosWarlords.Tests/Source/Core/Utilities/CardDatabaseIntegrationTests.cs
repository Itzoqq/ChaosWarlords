using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]
    [TestCategory("Integration")]
    public class CardDatabaseIntegrationTests
    {
        [TestMethod]
        public void LoadRealCardsJson_VerifyWight_HasSupplantSuccess()
        {
            // Arrange
            // Adjust path to point to Content relative to the executed DLL or project root
            // The previous test used "../../../../ChaosWarlords/" which implies running from bin/Debug/net10.0
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../ChaosWarlords/Content/data/cards.json");
            if (!File.Exists(path)) Assert.Inconclusive("cards.json not found at " + path);

            var database = new CardDatabase();
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

            var database = new CardDatabase();
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
