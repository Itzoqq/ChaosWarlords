using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Nalfeshnee ("Gain 3 Influence. Promote the top card of
    /// your deck.") - a second, simpler shipped use of EffectType.PromoteTopOfDeck (see
    /// HezrouScenarioTests.cs for the primitive's own writeup), with no targeting involved at
    /// all: both effects are automatic. Loads the REAL "nalfeshnee" entry out of the REAL
    /// cards.json and dispatches every command through a REAL CommandDispatcher, mirroring
    /// TrivialPrimitiveCardsScenarioTests.cs's style for a plain no-targeting card.
    ///
    /// Rows 3 (no-valid-target fallback) and 5 (stale target) don't apply - neither effect ever
    /// produces a targeting command. Row 6 (unmet resource precondition) doesn't apply either -
    /// playing a card from hand has no resource cost of its own to fail.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class NalfeshneeScenarioTests
    {
        [TestMethod]
        public void PlayNalfeshnee_GrantsThreeInfluence_AndPromotesTheTopOfDeck()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "nalfeshnee");
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.Influence);
            Assert.Contains(topOfDeck, red.InnerCircle, "The card that was on top of the deck should now be promoted.");
            Assert.DoesNotContain(topOfDeck, red.DeckManager.DrawPile);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- The new primitive's own genuine failure mode: nothing left to promote at all. ---

        [TestMethod]
        public void PlayNalfeshnee_WithEmptyDeckAndDiscard_StillGrantsInfluence_PromoteQuietlyNoOps()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "nalfeshnee");
            red.DeckManager.Clear();
            red.DeckManager.ClearDiscard();

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.Influence, "The Influence half must still apply independently of the Promote half.");
            Assert.IsEmpty(red.InnerCircle, "Nothing was available to promote - must fail quietly, not crash or fabricate a card.");
        }

        // --- Row 4: wrong-player dispatch. ---

        [TestMethod]
        public void PlayNalfeshneeCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "nalfeshnee");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.AreEqual(0, blue.Influence);
        }

        // --- Row 7: double-dispatch/replay. ---

        [TestMethod]
        public void PlayNalfeshneeCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "nalfeshnee");
            var topOfDeck = red.DeckManager.DrawPile.First();

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.Influence, "Should have applied exactly once, not twice.");
            Assert.Contains(topOfDeck, red.InnerCircle);
            Assert.HasCount(1, red.InnerCircle, "The Promote half must have applied exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlayCardCommand_DtoRoundTrip_StillPlaysNalfeshnee()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "nalfeshnee");
            var topOfDeck = red.DeckManager.DrawPile.First();

            var command = new PlayCardCommand(card);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PlayCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlayCardCommand.");

            scenario.Dispatch(hydrated);

            Assert.AreEqual(3, red.Influence);
            Assert.Contains(topOfDeck, red.InnerCircle);
        }
    }
}
