using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Engine-level coverage for CardEffectProcessor.ExpandChainedRepeat (CardEffect.
    /// ChainedRepeatCount) - not a specific shipped card (Graz'zt's own scenario tests live in
    /// GrazztScenarioTests.cs and additionally prove this works through the real cards.json/
    /// localization data, at the real ChainedRepeatCount=5). This file uses a hand-built Card
    /// (same pattern as ChooseCountChainTests.cs) to isolate and directly exercise the NEW
    /// primitive itself: "repeat an (optional return) -> (mandatory chained effect) PAIR N
    /// times, with each round's chained effect firing independently, scoped to THAT round's own
    /// site via ActionSystem.PendingSite" - distinct from CardEffect.ChooseCount (which repeats
    /// a CHOICE between 2 sibling effects, converging into one shared continuation only on the
    /// final round) and from IEffectStrategy.SupportsRepeat (which repeats the SAME single
    /// targeting step N times, converging into its OnSuccess only ONCE at the very end).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ChainedRepeatChainTests
    {
        private static Card BuildReturnSpySupplantChainCard(int rounds = 3)
        {
            var card = new Card("chained_repeat_test", "Chained Repeat Test", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.ReturnOwnSpy, 1)
            {
                IsOptional = true,
                ChainedRepeatCount = rounds,
                OnSuccess = new CardEffect(EffectType.Supplant, 1)
            });
            return card;
        }

        /// <summary>
        /// Places a Red spy at <paramref name="site"/> (the round's return target) and a Blue
        /// troop at one of its nodes (the round's Supplant target), PLUS a separate Red troop
        /// adjacent to that node so Presence survives the spy leaving (same setup
        /// CloakerScenarioTests.cs uses for the identical ReturnOwnSpy-&gt;Assassinate chain-link
        /// shape) - without this, returning the spy would remove the ONLY presence source before
        /// Supplant ever gets to check for one.
        /// </summary>
        private static MapNode SetupSpySiteWithSupplantableTroop(MatchScenario scenario, Player red, Site site)
        {
            var troopNode = site.NodesInternal[0];
            troopNode.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color);
            var presenceNode = troopNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            presenceNode.Occupant = red.Color;
            return troopNode;
        }

        [TestMethod]
        public void ChainedRepeat3_AcceptingAllRounds_ReturnsAndSupplantsIndependentlyAtEachSite()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0).Take(3).ToList();
            var siteA = sites[0];
            var siteB = sites[1];
            var siteC = sites[2];
            var troopA = SetupSpySiteWithSupplantableTroop(scenario, red, siteA);
            var troopB = SetupSpySiteWithSupplantableTroop(scenario, red, siteB);
            var troopC = SetupSpySiteWithSupplantableTroop(scenario, red, siteC);
            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Round 1's popup.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.AreEqual(ActionState.TargetingReturnOwnSpy, scenario.Context.ActionSystem.CurrentState, "Round 1 site-pick.");
            scenario.ClickTarget(null, siteA);
            Assert.AreEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Chains straight into Supplant.");
            scenario.ClickTarget(troopA, null);

            Assert.HasCount(2, scenario.Interactions, "Resolving round 1's Supplant should immediately raise round 2's popup.");
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, siteB);
            scenario.ClickTarget(troopB, null);

            Assert.HasCount(3, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, siteC);
            scenario.ClickTarget(troopC, null);

            Assert.AreEqual(red.Color, troopA.Occupant);
            Assert.AreEqual(red.Color, troopB.Occupant);
            Assert.AreEqual(red.Color, troopC.Occupant);
            Assert.DoesNotContain(red.Color, siteA.Spies);
            Assert.DoesNotContain(red.Color, siteB.Spies);
            Assert.DoesNotContain(red.Color, siteC.Spies);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The whole 3-round sequence must fully drain back to Normal.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChainedRepeat3_DecliningFirstRound_EndsImmediatelyWithNothingReturned()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            var troop = SetupSpySiteWithSupplantableTroop(scenario, red, site);
            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.Contains(red.Color, site.Spies, "Nothing should have been returned.");
            Assert.AreEqual(PlayerColor.Blue, troop.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(1, scenario.Interactions, "Declining round 1 must not raise a round 2 popup.");
        }

        [TestMethod]
        public void ChainedRepeat3_MixedAcceptAndDecline_StopsAtTheDeclinedRound()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0).Take(2).ToList();
            var siteA = sites[0];
            var siteB = sites[1];
            var troopA = SetupSpySiteWithSupplantableTroop(scenario, red, siteA);
            SetupSpySiteWithSupplantableTroop(scenario, red, siteB);
            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true); // Round 1: accept.
            scenario.ClickTarget(null, siteA);
            scenario.ClickTarget(troopA, null);

            Assert.HasCount(2, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false); // Round 2: decline - sequence ends here.

            Assert.Contains(red.Color, siteB.Spies, "Round 2's site must be untouched - never reached.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
            Assert.HasCount(2, scenario.Interactions, "No round 3 popup - declining round 2 ends the whole sequence.");
        }

        [TestMethod]
        public void ChainedRepeat3_WrongSiteSupplantClick_IsRejectedByPendingSiteGuard()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0).Take(2).ToList();
            var siteA = sites[0];
            var siteB = sites[1];
            var troopA = SetupSpySiteWithSupplantableTroop(scenario, red, siteA);
            var troopB = SetupSpySiteWithSupplantableTroop(scenario, red, siteB);
            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, siteA); // Round 1 returns from Site A.

            var rejected = scenario.ClickTarget(troopB, null); // Site B's troop is NOT where the spy was returned from.
            Assert.IsNull(rejected, "Supplanting at the wrong site must be rejected by the PendingSite guard.");
            Assert.AreEqual(PlayerColor.Blue, troopB.Occupant, "Wrong-site troop must survive.");

            scenario.ClickTarget(troopA, null); // Correct site.
            Assert.AreEqual(red.Color, troopA.Occupant);
        }

        [TestMethod]
        public void ChainedRepeat3_SupplantHasNoValidTargetBecauseTheSpyWasTheOnlyPresence_StillContinuesToNextRound()
        {
            // The critical bug this primitive's design has to avoid, mirroring ChooseCount's own
            // regression: a round whose chained step turns out to have no valid target must not
            // silently swallow every REMAINING round too. Here the gap is a real TOCTOU, not
            // contrived: this site's own spy is the ONLY presence source, board-wide, for ANY
            // Supplant target at all - so accepting round 1 flips CardRuleEngine.HasValidTargets
            // (Supplant) from true to false a split second before it's actually queried, purely
            // as a side effect of the spy leaving (no separate adjacent Red troop anywhere, and
            // no OTHER valid Supplant target anywhere else on the board either - both deliberate,
            // see below).
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.Name == "Crystal Cave");
            var troop = site.NodesInternal[0];
            troop.Occupant = PlayerColor.Blue;
            site.AddSpy(red.Color); // The ONLY presence source at this site - no adjacent troop.

            // A second spy at a completely separate, troop-free site - purely so round 2's OWN
            // ReturnOwnSpy still has a valid target to distinguish "the convergence trick pushed
            // round 2, which then legitimately offers its own prompt" from "round 2 was pushed
            // but ALSO immediately had nothing to return" (both would otherwise look identical:
            // chain ends at Normal after exactly 1 interaction).
            var secondSite = scenario.Context.MapManager.Sites.First(s => s.Name == "Obsidian Fortress");
            secondSite.AddSpy(red.Color);

            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Crystal Cave's troop IS currently reachable (via the not-yet-returned spy), so round 1's popup must still be offered.");
            scenario.RespondToLatestInteraction(accept: true);
            scenario.ClickTarget(null, site); // Return the spy - its own presence vanishes with it.

            Assert.DoesNotContain(red.Color, site.Spies, "The spy must still have been returned.");
            Assert.HasCount(2, scenario.Interactions, "Round 2's popup must still appear - Supplant finding no valid target anywhere must not have ended the sequence.");
            Assert.AreNotEqual(ActionState.TargetingSupplant, scenario.Context.ActionSystem.CurrentState, "Supplant must have been skipped, not left waiting for an impossible click.");
            Assert.AreEqual(PlayerColor.Blue, troop.Occupant, "Nothing to supplant once presence left with the spy.");

            scenario.RespondToLatestInteraction(accept: false); // End round 2 cleanly.
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChainedRepeat3_AfterThreeRounds_DoesNotSpuriouslyRestartTheSequence()
        {
            // Regression guard for the same class of bug ChooseCountChainTests guards against:
            // the LAST round's expanded copy must have ChainedRepeatCount forced to 0, or
            // PushEffectContext's entry guard would see the original count still intact and
            // re-expand the "final" round into a brand new 3-round sequence, looping forever.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var sites = scenario.Context.MapManager.Sites.Where(s => s.NodesInternal.Count > 0).Take(3).ToList();
            var troops = sites.Select(s => SetupSpySiteWithSupplantableTroop(scenario, red, s)).ToList();
            var card = BuildReturnSpySupplantChainCard(3);
            red.AddToHand(card);

            scenario.PlayCard(card);
            for (int i = 0; i < 3; i++)
            {
                scenario.RespondToLatestInteraction(accept: true);
                scenario.ClickTarget(null, sites[i]);
                scenario.ClickTarget(troops[i], null);
            }

            Assert.HasCount(3, scenario.Interactions, "Exactly 3 rounds' worth of popups - a 4th would indicate the sequence restarted.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
