using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Engine-level coverage for CardEffectProcessor.ExpandChoiceRepeat (CardEffect.ChooseCount) -
    /// not a specific shipped card (Weaponmaster's own scenario tests live in
    /// WeaponmasterScenarioTests.cs and additionally prove this works through the real
    /// cards.json/localization data). This file uses a hand-built Card (same pattern as
    /// CardEffectProcessorChainTests.cs) to isolate and directly exercise the NEW primitive
    /// itself: "choose N times, independently between 2 different effect types" - distinct from
    /// IEffectStrategy.SupportsRepeat (repeats the SAME effect type) and from a plain one-shot
    /// IsOptional+Alternative choice (ChooseCount &lt;= 1).
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ChooseCountChainTests
    {
        private static Card BuildChooseThreeTimesCard()
        {
            var card = new Card("choose_count_test", "Choose Count Test", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 1, ResourceType.Troops)
            {
                IsOptional = true,
                ChooseCount = 3,
                Alternative = new CardEffect(EffectType.Assassinate, 1) { TargetNeutralTroopOnly = true }
            });
            return card;
        }

        private static (Player red, MapNode t1, MapNode t2, MapNode t3) SetupRedWithThreeReachableNeutralTroops(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count >= 3);
            site.AddSpy(red.Color);
            var nodes = site.NodesInternal.Take(3).ToList();
            nodes[0].Occupant = PlayerColor.Neutral;
            nodes[1].Occupant = PlayerColor.Neutral;
            nodes[2].Occupant = PlayerColor.Neutral;
            return (red, nodes[0], nodes[1], nodes[2]);
        }

        [TestMethod]
        public void ChooseCount3_AcceptingAllThreeRounds_CreditsPendingFreeTroopsThreeTimesAndNeverAssassinates()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            int pendingBefore = red.PendingFreeTroops;
            var card = BuildChooseThreeTimesCard();
            red.AddToHand(card);

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Round 1's popup.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.HasCount(2, scenario.Interactions, "Accepting round 1 should immediately raise round 2's popup.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.HasCount(3, scenario.Interactions, "Round 3's popup.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(pendingBefore + 3, red.PendingFreeTroops, "All 3 rounds accepted - 3 troops credited.");
            Assert.AreEqual(0, red.TrophyHall);
            Assert.AreEqual(PlayerColor.Neutral, t1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t2.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The whole 3-round sequence must fully drain back to Normal.");
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChooseCount3_DecliningAllThreeRounds_AssassinatesAllThreeReachableNeutralTroops()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = BuildChooseThreeTimesCard();
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Round 1's decline branch.");
            scenario.ClickTarget(t1, null);
            Assert.AreEqual(1, red.TrophyHall);

            Assert.HasCount(2, scenario.Interactions, "Resolving round 1's Assassinate should immediately raise round 2's popup.");
            scenario.RespondToLatestInteraction(accept: false);
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(t2, null);
            Assert.AreEqual(2, red.TrophyHall);

            Assert.HasCount(3, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t3, null);
            Assert.AreEqual(3, red.TrophyHall);

            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.None, t2.Occupant);
            Assert.AreEqual(PlayerColor.None, t3.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChooseCount3_MixedChoices_AppliesEachRoundIndependently()
        {
            var scenario = MatchScenario.Build();
            var (red, t1, _, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            int pendingBefore = red.PendingFreeTroops;
            var card = BuildChooseThreeTimesCard();
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: true); // Round 1: Deploy.
            scenario.RespondToLatestInteraction(accept: false); // Round 2: Assassinate.
            scenario.ClickTarget(t1, null);
            scenario.RespondToLatestInteraction(accept: true); // Round 3: Deploy.

            Assert.AreEqual(pendingBefore + 2, red.PendingFreeTroops, "2 of the 3 rounds chose Deploy.");
            Assert.AreEqual(1, red.TrophyHall, "1 of the 3 rounds chose Assassinate.");
            Assert.AreEqual(PlayerColor.None, t1.Occupant);
            Assert.AreEqual(PlayerColor.Neutral, t3.Occupant, "Only 1 troop should have been assassinated.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChooseCount3_DeclineWhenNoValidAssassinateTargetExists_StillConsumesTheRoundAndContinues()
        {
            // The critical bug this primitive's design has to avoid: PushEffectContext's normal
            // "no valid target -> Alternative, full stop" behavior would otherwise silently
            // swallow every REMAINING round too, not just the one with no legal target.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            int pendingBefore = red.PendingFreeTroops;
            var card = BuildChooseThreeTimesCard();
            red.AddToHand(card);

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Deploy is always legal, so round 1's popup must still appear even with zero Neutral troops anywhere.");
            scenario.RespondToLatestInteraction(accept: false); // Decline into Assassinate, which has NO valid target at all.

            Assert.HasCount(2, scenario.Interactions, "Round 2's popup must still appear - the impossible decline must not have silently ended the whole sequence.");
            scenario.RespondToLatestInteraction(accept: true);
            Assert.HasCount(3, scenario.Interactions);
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(pendingBefore + 2, red.PendingFreeTroops, "Rounds 2 and 3 both accepted Deploy.");
            Assert.AreEqual(0, red.TrophyHall, "Round 1's decline never actually assassinated anything - there was nothing to hit.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }

        [TestMethod]
        public void ChooseCount3_AfterThreeRounds_DoesNotSpuriouslyRestartTheSequence()
        {
            // Regression guard for a real bug caught during design/mutation-testing: the LAST
            // round's expanded copy must have ChooseCount forced to 0, or PushEffectContext's
            // entry guard would see the original ChooseCount=3 still intact and re-expand the
            // "final" round into a brand new 3-round sequence, looping forever.
            var scenario = MatchScenario.Build();
            var (red, t1, t2, t3) = SetupRedWithThreeReachableNeutralTroops(scenario);
            var card = BuildChooseThreeTimesCard();
            red.AddToHand(card);

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t1, null);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t2, null);
            scenario.RespondToLatestInteraction(accept: false);
            scenario.ClickTarget(t3, null);

            Assert.HasCount(3, scenario.Interactions, "Exactly 3 rounds' worth of popups - a 4th would indicate the sequence restarted.");
            Assert.AreEqual(3, red.TrophyHall);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.IsEmpty(scenario.Context.ActionSystem.ExecutionStack);
        }
    }
}
