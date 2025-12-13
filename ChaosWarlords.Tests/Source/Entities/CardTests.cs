using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

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
            _card = new Card("test_id", "Test Card", 3, CardAspect.Sorcery, 1, 2);
        }

        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly()
        {
            Assert.AreEqual("test_id", _card.Id);
            Assert.AreEqual("Test Card", _card.Name);
            Assert.AreEqual(3, _card.Cost);
            Assert.AreEqual(CardAspect.Sorcery, _card.Aspect);
            Assert.AreEqual(1, _card.DeckVP);
            Assert.AreEqual(2, _card.InnerCircleVP);
            Assert.AreEqual(CardLocation.Market, _card.Location); // Default location
            Assert.IsNotNull(_card.Effects);
            Assert.IsEmpty(_card.Effects);
        }

        [TestMethod]
        public void AddEffect_AddsEffectToList()
        {
            var effect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);

            _card.AddEffect(effect);

            Assert.HasCount(1, _card.Effects);
            Assert.AreEqual(EffectType.GainResource, _card.Effects[0].Type);
        }

        [TestMethod]
        public void Update_DetectsHover_WhenMouseIsInsideBounds()
        {
            // Arrange
            _card.Position = new Vector2(100, 100);

            // Mouse is at (110, 110) - Inside the card (100,100) to (250,300)
            // Note: Card.Width = 150, Height = 200
            var mouseState = new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            // Act
            _card.Update(new GameTime(), mouseState);

            // Assert
            Assert.IsTrue(_card.IsHovered, "Card should be hovered when mouse is inside bounds.");
        }

        [TestMethod]
        public void Update_DetectsNoHover_WhenMouseIsOutsideBounds()
        {
            // Arrange
            _card.Position = new Vector2(100, 100);

            // Mouse is at (0, 0) - Way outside
            var mouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            // Act
            _card.Update(new GameTime(), mouseState);

            // Assert
            Assert.IsFalse(_card.IsHovered, "Card should NOT be hovered when mouse is outside.");
        }

        [TestMethod]
        public void Update_UpdatesBoundsDynamically_WhenPositionChanges()
        {
            // Arrange
            // 1. Place card at 100,100
            _card.Position = new Vector2(100, 100);
            // 2. Mouse is at 100,100 (Hovering)
            var mouseState = new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

            _card.Update(new GameTime(), mouseState);
            Assert.IsTrue(_card.IsHovered, "Sanity check failed: Card should be hovered.");

            // Act: Move card away to 500,500
            _card.Position = new Vector2(500, 500);

            // Update again with SAME mouse position (100,100)
            _card.Update(new GameTime(), mouseState);

            // Assert: Should no longer be hovered because the bounds moved!
            Assert.IsFalse(_card.IsHovered, "Card bounds did not update after changing Position.");
        }

        [TestMethod]
        public void Properties_SettersWorkCorrectly()
        {
            // Testing the setters that might not be used in constructor
            _card.Description = "New Desc";
            _card.Location = CardLocation.Hand;

            Assert.AreEqual("New Desc", _card.Description);
            Assert.AreEqual(CardLocation.Hand, _card.Location);
        }

        [TestMethod]
        public void BuildEffectText_GeneratesCorrectString_ForResourceGain()
        {
            // Arrange
            var card = new Card("id", "Test", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));

            // Act
            // We can call this because of [InternalsVisibleTo]!
            string text = card.BuildEffectText();

            // Assert
            StringAssert.Contains(text, "+2 Power");
        }

        [TestMethod]
        public void BuildEffectText_UsesStaticDescription_IfAvailable()
        {
            // Arrange
            var card = new Card("id", "Test", 0, CardAspect.Neutral, 0, 0);
            card.Description = "Static Description Override";
            card.AddEffect(new CardEffect(EffectType.GainResource, 5, ResourceType.Influence));

            // Act
            string text = card.BuildEffectText();

            // Assert
            Assert.AreEqual("Static Description Override", text);
        }

        [TestMethod]
        public void BuildEffectText_HandlesMultipleEffects()
        {
            // Arrange
            var card = new Card("id", "Test", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Influence));
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Act
            string text = card.BuildEffectText();

            // Assert
            StringAssert.Contains(text, "+1 Influence");
            StringAssert.Contains(text, "Assassinate");
        }
    }
}