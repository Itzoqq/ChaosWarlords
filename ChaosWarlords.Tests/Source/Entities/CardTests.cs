using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using System.Reflection;

namespace ChaosWarlords.Tests.Source.Entities
{
    [TestClass]
    [TestCategory("Unit")]
    public class CardTests
    {
        private Card _card = null!;

        [TestInitialize]
        public void Setup()
        {
            _card = TestData.Cards.CheapCard();
        }

        [TestMethod]
        public void Constructor_SetsPropertiesCorrectly()
        {
            Assert.AreEqual("cheap", _card.Id);
            Assert.AreEqual("Test Description", _card.Name);
            Assert.AreEqual(2, _card.Cost);
            Assert.AreEqual(CardAspect.Neutral, _card.Aspect);

            // Check specific VP types
            Assert.AreEqual(0, _card.DeckVP);
            Assert.AreEqual(0, _card.InnerCircleVP);
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
            Assert.AreEqual(0, clone.DeckVP, "Clone must preserve DeckVP.");
            Assert.AreEqual(0, clone.InnerCircleVP, "Clone must preserve InnerCircleVP.");
            Assert.AreEqual(0, clone.InfluenceValue, "Clone must preserve InfluenceValue.");

            // Standard checks
            Assert.AreEqual(_card.Description, clone.Description);
            Assert.AreEqual(_card.Location, clone.Location);
            Assert.HasCount(1, clone.Effects);
        }

        [TestMethod]
        public void Clone_EffectHasTargetNeutralTroopOnly_PreservesTargetNeutralTroopOnly()
        {
            // Arrange
            _card.AddEffect(new CardEffect(EffectType.Assassinate, 1) { TargetNeutralTroopOnly = true });

            // Act
            var clone = _card.Clone();

            // Assert
            Assert.AreNotSame(_card, clone, "Clone should be a new instance.");
            Assert.HasCount(1, clone.Effects);
            Assert.IsTrue(clone.Effects[0].TargetNeutralTroopOnly, "Clone must preserve TargetNeutralTroopOnly.");
        }

        [TestMethod]
        public void Clone_EffectHasIgnoresPresenceRequirement_PreservesIgnoresPresenceRequirement()
        {
            // Arrange
            _card.AddEffect(new CardEffect(EffectType.Supplant, 1) { IgnoresPresenceRequirement = true });

            // Act
            var clone = _card.Clone();

            // Assert
            Assert.AreNotSame(_card, clone, "Clone should be a new instance.");
            Assert.HasCount(1, clone.Effects);
            Assert.IsTrue(clone.Effects[0].IgnoresPresenceRequirement, "Clone must preserve IgnoresPresenceRequirement.");
        }

        [TestMethod]
        public void Clone_EffectHasDynamicAmountSource_PreservesDynamicAmountSourceAndDivisor()
        {
            // Arrange
            _card.AddEffect(new CardEffect(EffectType.GainResource, 0, ResourceType.VictoryPoints)
            {
                DynamicAmountSource = DynamicAmountSource.SitesControlled,
                DynamicAmountDivisor = 2
            });

            // Act
            var clone = _card.Clone();

            // Assert
            Assert.AreNotSame(_card, clone, "Clone should be a new instance.");
            Assert.HasCount(1, clone.Effects);
            Assert.AreEqual(DynamicAmountSource.SitesControlled, clone.Effects[0].DynamicAmountSource, "Clone must preserve DynamicAmountSource.");
            Assert.AreEqual(2, clone.Effects[0].DynamicAmountDivisor, "Clone must preserve DynamicAmountDivisor.");
        }

        /// <summary>
        /// Reflection-based completeness check (docs/coding-guidelines.md Rule #24) - covers
        /// every CURRENT and FUTURE CardEffect property automatically, so a new field forgotten
        /// in Card.CloneEffect's object initializer fails THIS test immediately instead of
        /// silently reverting to its default the first time a card carrying it is cloned (e.g.
        /// on ActionSystem.CancelTargeting's snapshot restore). This is the exact shape that
        /// shipped as a real bug for IgnoresPresenceRequirement (see the tyrants-rules skill's
        /// bug-log.md) - a hand-written per-field test (like the three above) only protects a
        /// field someone remembered to write one for; this protects the next one nobody does.
        /// </summary>
        [TestMethod]
        public void Clone_EveryCardEffectProperty_IsPreservedAutomatically()
        {
            var effect = new CardEffect(EffectType.Assassinate, 1);
            _card.AddEffect(effect);

            var properties = typeof(CardEffect).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                // OnSuccess/Alternative are self-referential chain nodes, not a flat data field
                // this shallow-equality check can compare directly (the clone is a structurally
                // equal but reference-different deep copy). No separate nested-node test is
                // needed for THIS check's purpose though: Card.CloneEffect recurses into
                // OnSuccess/Alternative via this exact same method, at every depth - so the
                // completeness guarantee below already applies to a nested node's own
                // properties too, for free, the next time this test runs against one.
                .Where(p => p.Name != nameof(CardEffect.OnSuccess) && p.Name != nameof(CardEffect.Alternative))
                .ToList();

            Assert.IsNotEmpty(properties, "Sanity check: CardEffect should have cloneable properties to test.");

            var sentinels = new Dictionary<PropertyInfo, object?>();
            foreach (var prop in properties)
            {
                object? sentinel = BuildSentinel(prop, prop.GetValue(effect));
                sentinels[prop] = sentinel;
                prop.SetValue(effect, sentinel);
            }

            var clone = _card.Clone();
            var clonedEffect = clone.Effects[0];

            foreach (var (prop, sentinel) in sentinels)
            {
                Assert.AreEqual(sentinel, prop.GetValue(clonedEffect),
                    $"Card.CloneEffect must copy CardEffect.{prop.Name} - it came back as its default " +
                    "instead of the sentinel value this test set, meaning it's missing from CloneEffect's " +
                    "object initializer (see docs/coding-guidelines.md Rule #24).");
            }
        }

        /// <summary>
        /// Builds a value guaranteed to differ from <paramref name="currentValue"/> for
        /// <paramref name="prop"/>'s type, so the completeness test above can tell "copied"
        /// apart from "left at its default" regardless of what that default actually is.
        /// Throws for any type it hasn't been taught yet, rather than silently skipping it - a
        /// new CardEffect property of an unrecognized type should fail loudly here until this
        /// method is extended, not slip through untested.
        /// </summary>
        private static object? BuildSentinel(PropertyInfo prop, object? currentValue)
        {
            var type = prop.PropertyType;

            if (type == typeof(bool)) return !(bool)(currentValue ?? false);
            if (type == typeof(int)) return (int)(currentValue ?? 0) + 1;
            if (type.IsEnum)
            {
                object? candidate = Enum.GetValues(type).Cast<object>().FirstOrDefault(v => !v.Equals(currentValue));
                if (candidate == null)
                {
                    throw new NotSupportedException($"Enum {type.Name} has no value distinct from its current default - extend it or this helper.");
                }
                return candidate;
            }
            if (type == typeof(EffectCondition)) return new EffectCondition(ConditionType.HandSize, 4);

            throw new NotSupportedException(
                $"CardTests.BuildSentinel doesn't know how to build a sentinel value for CardEffect.{prop.Name} " +
                $"(type {type.Name}) - extend this method so Clone_EveryCardEffectProperty_IsPreservedAutomatically " +
                "can actually exercise the new field.");
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


