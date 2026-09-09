using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Ghoul ("Gain 2 Influence. Each opponent recruits an
    /// Insane Outcast.") - the first card to need CardEffect.AppliesToEachOpponent, a new
    /// EffectType.ForceRecruit mode that loops TurnManager.GetOpponentsInSeatOrder instead of
    /// resolving against a single SelectOpponent-chosen recipient (Gibbering Mouther). Both
    /// effects are fully automatic/non-targeting - no popup, no click, everything resolves in
    /// the single PlayCardCommand dispatch. Loads the REAL "ghoul" entry out of the REAL
    /// cards.json and dispatches through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class GhoulScenarioTests
    {
        [TestMethod]
        public void PlayGhoul_TwoPlayerMatch_GainsInfluenceAndForcesTheSoleOpponentToRecruit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            int influenceBefore = red.Influence;
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");
            scenario.PlayCard(ghoul);

            Assert.AreEqual(influenceBefore + 2, red.Influence);
            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile, "Blue should have recruited exactly 1 card.");
            Assert.IsTrue(blue.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"), "The forced recruit must specifically be an Insane Outcast.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Everything is automatic - the whole card settles in one dispatch.");
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "AppliesToEachOpponent never sets ForcedActingPlayer - unlike SelectOpponent, there's no single chosen actor.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void PlayGhoul_ThreePlayerMatch_ForcesEveryOpponent_NotJustOne()
        {
            var scenario = MatchScenario.Build(playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange });
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var orange = scenario.Player(PlayerColor.Orange);
            int blueDiscardCountBefore = blue.DiscardPile.Count;
            int orangeDiscardCountBefore = orange.DiscardPile.Count;
            int redDiscardCountBefore = red.DiscardPile.Count;

            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");
            scenario.PlayCard(ghoul);

            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile, "Every opponent, not just one, must recruit.");
            Assert.HasCount(orangeDiscardCountBefore + 1, orange.DiscardPile);
            Assert.IsTrue(blue.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
            Assert.IsTrue(orange.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
            Assert.HasCount(redDiscardCountBefore, red.DiscardPile, "The real card-playing player is never their own opponent - they must not recruit anything from this effect.");
        }

        // --- Row 4: wrong-player dispatch ---

        [TestMethod]
        public void PlayGhoulCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejectedWithNoStateChange()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var ghoul = scenario.GiveCard(PlayerColor.Blue, "ghoul");

            scenario.AssertRejected(new PlayCardCommand(ghoul));

            Assert.Contains(ghoul, blue.Hand, "Ghoul should still be in Blue's hand - the command must not have executed.");
        }

        // --- Row 7: double-dispatch/replay ---

        [TestMethod]
        public void PlayGhoulCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            int influenceBefore = red.Influence;
            int blueDiscardCountBefore = blue.DiscardPile.Count;
            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");

            scenario.DispatchTwice(new PlayCardCommand(ghoul));

            Assert.AreEqual(influenceBefore + 2, red.Influence, "Influence should have been granted exactly once.");
            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile, "Blue should have recruited exactly once, not twice.");
        }

        // --- Row 9: DTO round-trip ---

        [TestMethod]
        public void PlayCardCommand_DtoRoundTrip_StillPlaysGhoulAndForcesTheRecruit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            int influenceBefore = red.Influence;
            int blueDiscardCountBefore = blue.DiscardPile.Count;
            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");

            var command = new PlayCardCommand(ghoul);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as PlayCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a PlayCardCommand.");

            scenario.Dispatch(hydrated);

            Assert.AreEqual(influenceBefore + 2, red.Influence);
            Assert.HasCount(blueDiscardCountBefore + 1, blue.DiscardPile);
        }
    }
}
