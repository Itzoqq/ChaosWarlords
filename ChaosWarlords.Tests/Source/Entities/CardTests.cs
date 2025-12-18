using ChaosWarlords.Source.Entities;
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
            // Create a standard card for testing
            // Constructor: Id, Name, Cost, Aspect, VP, Influence
            _card = new Card("test_id", "Test Card", 3, CardAspect.Sorcery, 1, 2);
        }

        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly()
        {
            Assert.AreEqual("test_id", _card.Id);
            Assert.AreEqual("Test Card", _card.Name);
            Assert.AreEqual(3, _card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, _card.Aspect);

            // The current model only tracks one VP value
            Assert.AreEqual(1, _card.VictoryPoints);
            // Assert.AreEqual(2, _card.InnerCircleVP); // Removed for now

            Assert.AreEqual(CardLocation.None, _card.Location); // Default location is None/0 until set
            Assert.IsNotNull(_card.Effects);
            Assert.IsEmpty(_card.Effects);
        }

        [TestMethod]
        public void AddEffect_AddsEffectToList()
        {
            // Fixed Constructor: (Type, Amount, Resource)
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
            // Since Card is just a data container now, we verify we can write to the property
            _card.Location = CardLocation.Hand;
            Assert.AreEqual(CardLocation.Hand, _card.Location);

            _card.Location = CardLocation.DiscardPile;
            Assert.AreEqual(CardLocation.DiscardPile, _card.Location);
        }
    }
}