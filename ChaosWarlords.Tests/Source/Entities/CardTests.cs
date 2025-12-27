using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Entities
{
    [TestClass]
    public class CardTests
    {
        private Card _card = null!;

        [TestInitialize]
        public void Setup()
        {
            // Constructor now requires (Id, Name, Cost, Aspect, DeckVP, InnerVP, Influence)
            // We pass 1 for DeckVP, 2 for InnerCircleVP, and 0 for Influence
            _card = new Card("test_id", "Test Card", 3, CardAspect.Sorcery, 1, 2, 0);
        }

        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly()
        {
            Assert.AreEqual("test_id", _card.Id);
            Assert.AreEqual("Test Card", _card.Name);
            Assert.AreEqual(3, _card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, _card.Aspect);

            // Check specific VP types now instead of generic VictoryPoints
            Assert.AreEqual(1, _card.DeckVP);
            Assert.AreEqual(2, _card.InnerCircleVP);
            Assert.AreEqual(0, _card.InfluenceValue);

            Assert.AreEqual(CardLocation.None, _card.Location);
            Assert.IsNotNull(_card.Effects);
            // Replaced Assert.IsEmpty with standard check or CollectionAssert
            Assert.IsEmpty(_card.Effects);
        }

        [TestMethod]
        public void AddEffect_AddsEffectToList()
        {
            var effect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);

            _card.AddEffect(effect);

            Assert.HasCount(1, _card.Effects);
            Assert.AreEqual(EffectType.GainResource, _card.Effects[0].Type);
            Assert.AreEqual(2, _card.Effects[0].Amount);
            Assert.AreEqual(ResourceType.Power, _card.Effects[0].TargetResource);
        }

        [TestMethod]
        public void Location_CanBeUpdated()
        {
            _card.Location = CardLocation.Hand;
            Assert.AreEqual(CardLocation.Hand, _card.Location);

            _card.Location = CardLocation.DiscardPile;
            Assert.AreEqual(CardLocation.DiscardPile, _card.Location);
        }

        [TestMethod]
        public void Clone_CopiesAllNewProperties()
        {
            // Arrange
            _card.Description = "Original Description";
            // REMOVED: _card.IsHovered = true; (Moved to ViewModel)
            _card.Location = CardLocation.Hand;
            _card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Act
            var clone = _card.Clone();

            // Assert
            Assert.AreNotSame(_card, clone, "Clone should be a new instance.");
            Assert.AreEqual(_card.Id, clone.Id);

            // Critical checks for new properties
            Assert.AreEqual(_card.DeckVP, clone.DeckVP, "Clone must preserve DeckVP.");
            Assert.AreEqual(_card.InnerCircleVP, clone.InnerCircleVP, "Clone must preserve InnerCircleVP.");
            Assert.AreEqual(_card.InfluenceValue, clone.InfluenceValue, "Clone must preserve InfluenceValue.");

            // Standard checks
            Assert.AreEqual(_card.Description, clone.Description);
            Assert.AreEqual(_card.Location, clone.Location);
            Assert.HasCount(1, clone.Effects);
        }

        [TestMethod]
        public void Constructor_AllowsNegativeValues()
        {
            // Scenario: A "Cursed" card that subtracts VP
            var cursedCard = new Card("curse", "Cursed Item", 0, CardAspect.Shadow, -5, -2, -10);

            Assert.AreEqual(-5, cursedCard.DeckVP);
            Assert.AreEqual(-2, cursedCard.InnerCircleVP);
            Assert.AreEqual(-10, cursedCard.InfluenceValue);
        }
    }
}


