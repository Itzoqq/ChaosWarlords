using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for planning.txt TIER 1 item 8's transcribed cards that
    /// bank an EffectType.Promote end-of-turn credit (the same primitive Council Member/Cleric of
    /// Laogzed's shape already exercises - see TurnContext.AddPromotionCredit): Advocate
    /// ("Choose one: Gain 2 Influence. Or, at end of turn, promote another card played this
    /// turn." - the first card where the Promote credit itself is one branch of a choose-one,
    /// not an unconditional second effect), Wyrmspeaker ("Gain 1 Influence. At end of turn,
    /// promote another card played this turn." - two independent mandatory effects, same shape
    /// as Earth Elemental Myrmidon but with a deferred credit instead of an immediate Promote),
    /// Chosen of Lolth ("Return another player's troop or spy. At end of turn, promote another
    /// card played this turn." - ReturnUnitOrSpy(ReturnEnemyOnly) + Promote, the same combination
    /// High Priest of Myrkul's Return half already exercises), Drow Negotiator ("If there are 4
    /// or more cards in your inner circle, gain 3 Influence. At end of turn, promote another card
    /// played this turn." - the first card with a top-level, non-chained ConditionType.
    /// InnerCircleCount gate on a plain GainResource effect), and Cleric of Laogzed ("Move an
    /// enemy troop. At end of turn, promote another card played this turn." - a single mandatory
    /// MoveUnit, unlike Council Member's "up to 2" AllowPartialRepeat shape). Loads the REAL
    /// cards.json entries and dispatches every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class PromoteCreditDrowDragonsScenarioTests
    {
        private static (Player red, MapNode target) SetupRedWithOneAdjacentEnemyTroop(MatchScenario scenario)
        {
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var target = redNode.Neighbors.First(n => n.Occupant == PlayerColor.None);
            target.Occupant = PlayerColor.Blue;
            return (red, target);
        }

        // --- Advocate: choose-one, accept branch (Gain 2 Influence, no Promote credit). ---

        [TestMethod]
        public void PlayAdvocate_AcceptGainResource_GrantsInfluenceAndBanksNoPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "advocate");

            scenario.PlayCard(card);
            Assert.HasCount(1, scenario.Interactions, "Choose-one popup: Gain 2 Influence vs. bank a Promote credit.");
            scenario.RespondToLatestInteraction(accept: true);

            Assert.AreEqual(2, red.Influence);
            Assert.AreEqual(0, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The declined branch (the Promote credit) must not also apply.");
        }

        // --- Advocate: choose-one, decline branch (bank a Promote credit, no Influence). ---

        [TestMethod]
        public void PlayAdvocate_DeclineGainResource_BanksAPromotionCreditAndGrantsNoInfluence()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "advocate");

            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);

            Assert.AreEqual(0, red.Influence, "The declined GainResource branch must not also apply.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayAdvocateCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "advocate");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
            Assert.IsEmpty(scenario.Interactions);
        }

        [TestMethod]
        public void PlayAdvocateCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "advocate");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.HasCount(1, scenario.Interactions, "The Choose-one popup should have been raised exactly once, not twice.");
        }

        // --- Wyrmspeaker: two independent mandatory effects. ---

        [TestMethod]
        public void PlayWyrmspeaker_GrantsInfluenceAndBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "wyrmspeaker");

            scenario.PlayCard(card);

            Assert.AreEqual(1, red.Influence);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayWyrmspeakerCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "wyrmspeaker");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayWyrmspeakerCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "wyrmspeaker");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(1, red.Influence, "Should have applied exactly once, not twice.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must not have been banked twice.");
        }

        // --- Chosen of Lolth: ReturnUnitOrSpy(ReturnEnemyOnly) + Promote, both mandatory. ---

        [TestMethod]
        public void PlayChosenOfLolth_ReturnsAdjacentEnemyTroop_AndBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var blue = scenario.Player(PlayerColor.Blue);
            int blueTroopsBefore = blue.TroopsInBarracks;
            var card = scenario.GiveCard(PlayerColor.Red, "chosen_of_lolth");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(target, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(blueTroopsBefore + 1, blue.TroopsInBarracks);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayChosenOfLolth_OnlyOwnTroopOnBoard_ReturnSkipsButStillBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var redNode = scenario.Context.MapManager.Nodes.First(n => scenario.Context.MapManager.CanDeployAt(n, red.Color));
            scenario.Dispatch(new DeployTroopCommand(redNode.Id));
            var card = scenario.GiveCard(PlayerColor.Red, "chosen_of_lolth");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "No enemy troop/spy anywhere - Return must resolve as a clean no-op.");
            Assert.AreEqual(red.Color, redNode.Occupant, "Red's own troop must be untouched.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The independent Promote effect must still resolve.");
        }

        [TestMethod]
        public void PlayChosenOfLolthCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "chosen_of_lolth");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void ReturnUnitCommand_TargetingANonexistentNode_IsRejectedWhileChosenOfLolthEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "chosen_of_lolth");

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new ReturnTroopCommand(999999, card.Id), "A stale/nonexistent node id must be rejected.");
        }

        [TestMethod]
        public void PlayChosenOfLolthCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "chosen_of_lolth");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingReturnUnitOrSpy, scenario.Context.ActionSystem.CurrentState, "Should still be waiting for exactly the one click the first play triggered.");
        }

        // --- Drow Negotiator: top-level conditional GainResource + mandatory Promote. ---

        [TestMethod]
        public void PlayDrowNegotiator_FourCardsInInnerCircle_GrantsInfluenceAndBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            for (int i = 0; i < 4; i++)
            {
                scenario.PutCardInInnerCircle(PlayerColor.Red, "core_house_guard");
            }
            var card = scenario.GiveCard(PlayerColor.Red, "drow_negotiator");

            scenario.PlayCard(card);

            Assert.AreEqual(3, red.Influence, "4 cards in inner circle meets the threshold - the gated Influence gain should fire.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayDrowNegotiator_FewerThanFourCardsInInnerCircle_GrantsNoInfluenceButStillBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            for (int i = 0; i < 3; i++)
            {
                scenario.PutCardInInnerCircle(PlayerColor.Red, "core_house_guard");
            }
            var card = scenario.GiveCard(PlayerColor.Red, "drow_negotiator");

            scenario.PlayCard(card);

            Assert.AreEqual(0, red.Influence, "Below the threshold - the gated Influence gain must not fire.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The independent Promote effect is unconditional and must still resolve.");
        }

        [TestMethod]
        public void PlayDrowNegotiatorCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "drow_negotiator");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void PlayDrowNegotiatorCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            for (int i = 0; i < 4; i++)
            {
                scenario.PutCardInInnerCircle(PlayerColor.Red, "core_house_guard");
            }
            var card = scenario.GiveCard(PlayerColor.Red, "drow_negotiator");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(3, red.Influence, "Should have applied exactly once, not twice.");
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount, "The credit must not have been banked twice.");
        }

        // --- Cleric of Laogzed: single mandatory MoveUnit + Promote. ---

        [TestMethod]
        public void PlayClericOfLaogzed_MovesTheEnemyTroopAndBanksAPromotionCredit()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "cleric_of_laogzed");
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != target);

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(target, null);
            Assert.AreEqual(ActionState.TargetingMoveDestination, scenario.Context.ActionSystem.CurrentState);
            scenario.ClickTarget(destination, null);

            Assert.AreEqual(PlayerColor.None, target.Occupant);
            Assert.AreEqual(PlayerColor.Blue, destination.Occupant);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayClericOfLaogzed_NoEnemyTroopsAnywhere_SkipsMoveButStillBanksThePromotionCredit()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "cleric_of_laogzed");

            scenario.PlayCard(card);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void PlayClericOfLaogzedCommand_DispatchedByThePlayerWhoDoesNotHoldIt_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);
            var card = scenario.GiveCard(PlayerColor.Blue, "cleric_of_laogzed");

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.Contains(card, blue.Hand);
        }

        [TestMethod]
        public void MoveTroopCommand_TargetingANonexistentSourceNode_IsRejectedWhileClericOfLaogzedEffectIsPending()
        {
            var scenario = MatchScenario.Build();
            var (_, target) = SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "cleric_of_laogzed");
            var destination = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.None && n != target);

            scenario.PlayCard(card);
            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new MoveTroopCommand(999999, destination.Id, card.Id), "A stale/nonexistent source node id must be rejected.");
        }

        [TestMethod]
        public void PlayClericOfLaogzedCommand_DispatchedTwice_SecondDispatchIsRejected()
        {
            var scenario = MatchScenario.Build();
            SetupRedWithOneAdjacentEnemyTroop(scenario);
            var card = scenario.GiveCard(PlayerColor.Red, "cleric_of_laogzed");

            scenario.DispatchTwice(new PlayCardCommand(card));

            Assert.AreEqual(ActionState.TargetingMoveSource, scenario.Context.ActionSystem.CurrentState);
        }
    }
}
