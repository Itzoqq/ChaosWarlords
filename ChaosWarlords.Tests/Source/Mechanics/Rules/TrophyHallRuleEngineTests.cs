using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Systems
{
    // Unit coverage for TrophyHallRuleEngine - EffectType.DeployFromTrophyHall's eligibility
    // check (Mummy Lord: "take a white troop from any trophy hall").
    [TestClass]
    [TestCategory("Unit")]
    public class TrophyHallRuleEngineTests
    {
        private Player _red = null!;
        private Player _blue = null!;

        [TestInitialize]
        public void Setup()
        {
            _red = TestData.Players.RedPlayer();
            _blue = TestData.Players.BluePlayer();
        }

        [TestMethod]
        public void TryGetSoleEligibleSource_NoPlayerHasTheRequiredColor_ReturnsFalse()
        {
            bool result = TrophyHallRuleEngine.TryGetSoleEligibleSource(
                new[] { _red, _blue }, requireNeutralOnly: true, out var sourceColor);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, sourceColor);
        }

        [TestMethod]
        public void TryGetSoleEligibleSource_ExactlyOnePlayerEligible_ReturnsTrueWithThatPlayersColor()
        {
            _blue.SetTrophyHall(1, PlayerColor.Neutral);

            bool result = TrophyHallRuleEngine.TryGetSoleEligibleSource(
                new[] { _red, _blue }, requireNeutralOnly: true, out var sourceColor);

            Assert.IsTrue(result);
            Assert.AreEqual(PlayerColor.Blue, sourceColor);
        }

        [TestMethod]
        public void TryGetSoleEligibleSource_TwoPlayersEligible_ReturnsFalse()
        {
            // Ambiguous - no click-based way to ask "which player's trophy hall?" today (see
            // TrophyHallRuleEngine's own doc comment), so this deliberately counts as
            // unresolvable rather than guessing.
            _red.SetTrophyHall(1, PlayerColor.Neutral);
            _blue.SetTrophyHall(1, PlayerColor.Neutral);

            bool result = TrophyHallRuleEngine.TryGetSoleEligibleSource(
                new[] { _red, _blue }, requireNeutralOnly: true, out var sourceColor);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, sourceColor);
        }

        [TestMethod]
        public void TryGetSoleEligibleSource_EligiblePlayerHasOnlyANonNeutralColor_IsNotCountedEligible()
        {
            // A trophy hall composed entirely of, say, Blue's own captured Red troops must not
            // satisfy a "white troop" requirement.
            _blue.SetTrophyHall(1, PlayerColor.Red);

            bool result = TrophyHallRuleEngine.TryGetSoleEligibleSource(
                new[] { _red, _blue }, requireNeutralOnly: true, out _);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryGetSoleEligibleSource_RequireNeutralOnlyFalse_ReturnsFalse()
        {
            // "Any color" sourcing (Lich/Orcus's shape) isn't built yet - see this class's own
            // doc comment. No shipped card reaches this branch, but it must fail safely rather
            // than silently resolving something incorrect.
            _blue.SetTrophyHall(1, PlayerColor.Neutral);

            bool result = TrophyHallRuleEngine.TryGetSoleEligibleSource(
                new[] { _red, _blue }, requireNeutralOnly: false, out var sourceColor);

            Assert.IsFalse(result);
            Assert.AreEqual(PlayerColor.None, sourceColor);
        }

        [TestMethod]
        public void HasEligibleSource_MirrorsTryGetSoleEligibleSource_TrueCase()
        {
            _blue.SetTrophyHall(1, PlayerColor.Neutral);

            Assert.IsTrue(TrophyHallRuleEngine.HasEligibleSource(new[] { _red, _blue }, requireNeutralOnly: true));
        }

        [TestMethod]
        public void HasEligibleSource_MirrorsTryGetSoleEligibleSource_AmbiguousCaseIsFalse()
        {
            _red.SetTrophyHall(1, PlayerColor.Neutral);
            _blue.SetTrophyHall(1, PlayerColor.Neutral);

            Assert.IsFalse(TrophyHallRuleEngine.HasEligibleSource(new[] { _red, _blue }, requireNeutralOnly: true));
        }
    }
}
