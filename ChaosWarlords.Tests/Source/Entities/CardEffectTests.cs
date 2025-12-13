using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Entities
{
    [TestClass]
    public class CardEffectTests
    {
        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly_ForResourceGain()
        {
            // Arrange & Act
            // Simulating a "House Guard" card effect: Gain 2 Power (Rulebook pg 15)
            var effect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);

            // Assert
            Assert.AreEqual(EffectType.GainResource, effect.Type);
            Assert.AreEqual(2, effect.Amount);
            Assert.AreEqual(ResourceType.Power, effect.TargetResource);
        }

        [TestMethod]
        public void Constructor_DefaultResource_IsVictoryPoints()
        {
            // Arrange & Act
            // Simulating a standard action like "Assassinate" which doesn't usually 
            // target a specific resource type in the constructor, defaulting to VP 
            // or irrelevant enum value.
            var effect = new CardEffect(EffectType.Assassinate, 1);

            // Assert
            Assert.AreEqual(EffectType.Assassinate, effect.Type);
            Assert.AreEqual(1, effect.Amount);
            // Verify the optional parameter default value from CardEffects.cs
            Assert.AreEqual(ResourceType.None, effect.TargetResource);
        }

        [TestMethod]
        public void Properties_CanBeModified()
        {
            // Arrange
            var effect = new CardEffect(EffectType.GainResource, 1, ResourceType.Influence);

            // Act - Simulating a modifier or upgrade (like the "Focus" mechanic in Elemental deck)
            effect.Amount = 3;
            effect.Type = EffectType.Promote;
            effect.TargetResource = ResourceType.VictoryPoints;

            // Assert
            Assert.AreEqual(3, effect.Amount);
            Assert.AreEqual(EffectType.Promote, effect.Type);
            Assert.AreEqual(ResourceType.VictoryPoints, effect.TargetResource);
        }
    }
}