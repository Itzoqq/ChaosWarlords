using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]
    public class CardFactoryTests
    {
        [TestMethod]
        public void CreateFromData_ParsesPropertiesCorrectly()
        {
            // ARRANGE
            var cardData = new CardData
            {
                Id = "priestess_of_lolth",
                Name = "Priestess of Lolth",
                Cost = 2,
                Aspect = "Sorcery",
                DeckVP = 1,
                InnerCircleVP = 0,
                Description = "A test card"
            };

            // ACT
            var card = CardFactory.CreateFromData(cardData);

            // ASSERT
            Assert.IsNotNull(card);
            StringAssert.StartsWith(card.Id, "priestess_of_lolth");
            Assert.AreEqual("Priestess of Lolth", card.Name);
            Assert.AreEqual(2, card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, card.Aspect);

            Assert.AreEqual(1, card.DeckVP);
            Assert.AreEqual("A test card", card.Description);
        }

        [TestMethod]
        public void CreateFromData_ParsesEffectsCorrectly()
        {
            // ARRANGE
            var cardData = new CardData
            {
                Id = "test_card",
                Name = "Test Card",
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Type = "Promote", Amount = 1 },
                    new CardEffectData { Type = "GainResource", Amount = 2, TargetResource = "Power" }
                }
            };

            // ACT
            var card = CardFactory.CreateFromData(cardData);

            // ASSERT
            Assert.HasCount(2, card.Effects);

            var effect1 = card.Effects[0];
            Assert.AreEqual(EffectType.Promote, effect1.Type);
            Assert.AreEqual(1, effect1.Amount);
            Assert.AreEqual(ResourceType.None, effect1.TargetResource);

            var effect2 = card.Effects[1];
            Assert.AreEqual(EffectType.GainResource, effect2.Type);
            Assert.AreEqual(2, effect2.Amount);
            Assert.AreEqual(ResourceType.Power, effect2.TargetResource);
        }

        [TestMethod]
        public void CreateSoldier_CreatesCorrectCard()
        {
            var card = CardFactory.CreateSoldier();
            StringAssert.StartsWith(card.Id, "soldier");
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Power, card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void CreateNoble_CreatesCorrectCard()
        {
            var card = CardFactory.CreateNoble();
            StringAssert.StartsWith(card.Id, "noble");
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Influence, card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void CreateFromData_DefaultsMissingVPsToZero()
        {
            // Scenario: Loading old JSON data where DeckVP/InnerCircleVP properties don't exist
            // C# object initializer leaves them as default (0)
            var cardData = new CardData
            {
                Id = "old_card",
                Name = "Old Card",
                Cost = 1,
                Aspect = "Neutral"
            };

            var card = CardFactory.CreateFromData(cardData);

            Assert.AreEqual(0, card.DeckVP, "Missing DeckVP should default to 0");
            Assert.AreEqual(0, card.InnerCircleVP, "Missing InnerCircleVP should default to 0");
        }

        [TestMethod]
        public void CreateFromData_SetsDefaultInfluenceToZero()
        {
            // Current Factory implementation hardcodes Influence to 0
            // This test ensures that stays true until we explicitly update CardData
            var cardData = new CardData
            {
                Id = "inf_test",
                Name = "Influence Test",
                Cost = 1
            };

            var card = CardFactory.CreateFromData(cardData);

            Assert.AreEqual(0, card.InfluenceValue);
        }
    }
}

