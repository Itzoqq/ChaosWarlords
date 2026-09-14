using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Contexts;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// End-to-end coverage for the Insane Outcast shared supply cap + clockwise tie-break
    /// (planning.txt TIER 1 item 11, rulebook p.13: "If the supply of... Insane Outcasts runs
    /// out, the game continues, but you'll no longer be able to recruit one of those cards. If
    /// multiple Insane Outcasts are recruited and would run out, they are recruited in clockwise
    /// order starting with the player whose turn it is.") - exercised through the REAL Ghoul/
    /// Demogorgon cards.json entries and a REAL CommandDispatcher, unlike
    /// InsaneOutcastMechanicsTests.cs's unit-level PlayerStateManager coverage of the counter
    /// itself. Every scenario here explicitly selects the Demons half-deck (MatchScenario.Build's
    /// own default, Drow+Dragons, would leave the supply at 0 - see GhoulScenarioTests/
    /// DemogorgonScenarioTests for why).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class InsaneOutcastSupplyScenarioTests
    {
        private static readonly MarketDeckSelection DemonsInclusiveSelection = new(MarketHalfDeck.Demons, MarketHalfDeck.Drow);

        [TestMethod]
        public void PlayGhoul_SupplyAlreadyExhausted_ForcesNoRecruitButStillGrantsInfluence()
        {
            var scenario = MatchScenario.Build(marketDeckSelection: DemonsInclusiveSelection);
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            scenario.Context.PlayerStateManager.InitializeInsaneOutcastSupply(0);
            int blueDiscardCountBefore = blue.DiscardPile.Count;

            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");
            scenario.PlayCard(ghoul);

            Assert.AreEqual(2, red.Influence, "The independent GainResource half must still apply.");
            Assert.HasCount(blueDiscardCountBefore, blue.DiscardPile, "No supply left - Blue must recruit nothing.");
            Assert.AreEqual(0, scenario.Context.PlayerStateManager.InsaneOutcastSupplyRemaining);
        }

        [TestMethod]
        public void PlayGhoul_ExactlyOneCopyRemaining_GivesItAndLeavesSupplyAtZero()
        {
            var scenario = MatchScenario.Build(marketDeckSelection: DemonsInclusiveSelection);
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            scenario.Context.PlayerStateManager.InitializeInsaneOutcastSupply(1);

            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");
            scenario.PlayCard(ghoul);

            Assert.IsTrue(blue.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
            Assert.AreEqual(0, scenario.Context.PlayerStateManager.InsaneOutcastSupplyRemaining);
        }

        [TestMethod]
        public void PlayDemogorgon_ThreePlayerMatch_InsufficientSupplyStopsInClockwiseOrder()
        {
            // Demogorgon: "Each opponent recruits 2 Insane Outcasts". 4 total requested (2 + 2);
            // only 3 remain in supply. TurnManager randomizes seat order per-seed (see
            // MatchScenario.Build's own doc comment) - don't assume Blue precedes Orange; instead
            // read the REAL clockwise order (TurnManager.GetOpponentsInSeatOrder, the same method
            // ForceRecruit itself uses) and assert against THAT. Whichever opponent is first gets
            // both of its 2; the second gets only the 1 that's left - not any other split.
            var scenario = MatchScenario.Build(playerColors: [PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange], marketDeckSelection: DemonsInclusiveSelection);
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var opponentsInClockwiseOrder = scenario.Context.TurnManager.GetOpponentsInSeatOrder(red).ToList();
            Assert.HasCount(2, opponentsInClockwiseOrder, "Setup check: exactly 2 opponents in a 3-player match.");
            var firstInLine = opponentsInClockwiseOrder[0];
            var secondInLine = opponentsInClockwiseOrder[1];
            scenario.Context.PlayerStateManager.InitializeInsaneOutcastSupply(3);

            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            scenario.PlayCard(demogorgon);
            // No Devour target in hand (only Demogorgon itself) - the optional Devour/Supplant
            // half fizzles cleanly, leaving ForceRecruit as the only effect to observe.
            Assert.IsEmpty(scenario.Interactions, "No valid Devour target - the optional-effect popup must never fire.");

            Assert.AreEqual(2, firstInLine.DiscardPile.Count(c => c.DefinitionId == "insane_outcast"), "First in clockwise order - fully satisfied.");
            Assert.AreEqual(1, secondInLine.DiscardPile.Count(c => c.DefinitionId == "insane_outcast"), "Second in line - only the 1 remaining copy, not the full 2 requested.");
            Assert.AreEqual(0, scenario.Context.PlayerStateManager.InsaneOutcastSupplyRemaining);
            Assert.AreEqual(0, red.DiscardPile.Count(c => c.DefinitionId == "insane_outcast"), "The active player is never their own opponent - they must not recruit anything from this effect.");
        }

        [TestMethod]
        public void PlayDemogorgon_NoSupplyRemaining_NoOpponentRecruitsAnything()
        {
            var scenario = MatchScenario.Build(playerColors: [PlayerColor.Red, PlayerColor.Blue, PlayerColor.Orange], marketDeckSelection: DemonsInclusiveSelection);
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var orange = scenario.Player(PlayerColor.Orange);
            scenario.Context.PlayerStateManager.InitializeInsaneOutcastSupply(0);

            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            scenario.PlayCard(demogorgon);

            Assert.IsFalse(blue.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
            Assert.IsFalse(orange.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
        }

        [TestMethod]
        public void PlayGhoulThenPlayDemogorgon_SupplySharedAcrossIndependentCards_SecondCardSeesTheFirstsConsumption()
        {
            // The supply is one shared, match-wide pool - not per-card, not per-effect. Ghoul
            // consumes 1 (2-player match, 1 opponent), leaving Demogorgon (2 per opponent) only
            // 1 more to hand out instead of the 2 it would normally give.
            var scenario = MatchScenario.Build(marketDeckSelection: DemonsInclusiveSelection);
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            scenario.Context.PlayerStateManager.InitializeInsaneOutcastSupply(2);

            var ghoul = scenario.GiveCard(PlayerColor.Red, "ghoul");
            scenario.PlayCard(ghoul);
            Assert.AreEqual(1, scenario.Context.PlayerStateManager.InsaneOutcastSupplyRemaining);

            var demogorgon = scenario.GiveCard(PlayerColor.Red, "demogorgon");
            scenario.PlayCard(demogorgon);

            Assert.AreEqual(1 + 1, blue.DiscardPile.Count(c => c.DefinitionId == "insane_outcast"), "1 from Ghoul, then only 1 more (not 2) from Demogorgon once the shared pool ran dry.");
            Assert.AreEqual(0, scenario.Context.PlayerStateManager.InsaneOutcastSupplyRemaining);
        }
    }
}
