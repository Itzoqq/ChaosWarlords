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
    }
}
