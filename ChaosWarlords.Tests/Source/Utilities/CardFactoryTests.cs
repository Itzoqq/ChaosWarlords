using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

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
            Assert.AreEqual("priestess_of_lolth", card.Id);
            Assert.AreEqual("Priestess of Lolth", card.Name);
            Assert.AreEqual(2, card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, card.Aspect);
            Assert.AreEqual(1, card.VictoryPoints); // Maps DeckVP to VP
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
            Assert.AreEqual(ResourceType.None, effect1.TargetResource); // No resource specified

            var effect2 = card.Effects[1];
            Assert.AreEqual(EffectType.GainResource, effect2.Type);
            Assert.AreEqual(2, effect2.Amount);
            Assert.AreEqual(ResourceType.Power, effect2.TargetResource);
        }

        [TestMethod]
        public void CreateSoldier_CreatesCorrectCard()
        {
            var card = CardFactory.CreateSoldier();
            Assert.AreEqual("soldier", card.Id);
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Power, card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void CreateNoble_CreatesCorrectCard()
        {
            var card = CardFactory.CreateNoble();
            Assert.AreEqual("noble", card.Id);
            Assert.AreEqual(EffectType.GainResource, card.Effects[0].Type);
            Assert.AreEqual(ResourceType.Influence, card.Effects[0].TargetResource);
        }
    }
}