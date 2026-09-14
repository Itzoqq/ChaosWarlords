using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Setup phase (initial troop deployment) has no card-play step at all (rulebook p.4) -
    /// TIER 1 item 12 gave every player a real 5-card opening hand the instant MatchFactory.Build
    /// returns, which removed the only protection PlayCardCommand.Validate() used to have
    /// against this (an empty hand). See planning.txt TIER 1 item 12 and the
    /// PlayCardCommand.Validate() phase gate this exercises through the REAL CommandDispatcher
    /// path, not just a direct Validate() call (see PlayCardCommandTests.cs for that unit-level
    /// coverage).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class SetupPhaseScenarioTests
    {
        [TestMethod]
        public void PlayCard_DuringSetupPhase_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            // MatchScenario.Build defaults to Playing (see its own comment) - override back to
            // Setup, the actual phase every real match starts in.
            scenario.Context.CurrentPhase = MatchPhase.Setup;

            var card = scenario.GiveCard(scenario.Context.ActivePlayer.Color, "kobold");

            scenario.AssertRejected(new PlayCardCommand(card), "Playing a card during Setup must be rejected outright, not silently no-op.");
        }

        [TestMethod]
        public void PlayCard_AfterSetupTransitionsToPlaying_Succeeds()
        {
            // Regression guard for the fix above: once real play has started, playing a card
            // the active player actually holds must still work exactly as before.
            var scenario = MatchScenario.Build();
            scenario.Context.CurrentPhase = MatchPhase.Playing;

            var card = scenario.GiveCard(scenario.Context.ActivePlayer.Color, "kobold");
            long sequenceBefore = scenario.Context.SequenceNumber;

            scenario.PlayCard(card);

            Assert.IsGreaterThan(sequenceBefore, scenario.Context.SequenceNumber, "A legal PlayCard during Playing phase must actually dispatch.");
            CollectionAssert.DoesNotContain(scenario.Player(scenario.Context.ActivePlayer.Color).Hand.ToList(), card, "Card should have left the hand.");
        }
    }
}
