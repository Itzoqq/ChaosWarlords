using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Standing test-matrix coverage for Ambassador ("At end of turn, promote another card
    /// played this turn. If an opponent causes you to discard this, you may promote it
    /// instead.") - closes planning.txt TIER 1 item 5's last open piece (REACTIVE TRIGGERS).
    ///
    /// Ambassador's primary ability needs no new mechanism at all - it's the exact same plain,
    /// unfiltered EffectType.Promote credit core_noble already ships (see
    /// PlayAmbassador_GrantsOnePromotionCredit below).
    ///
    /// Its SECOND ability is the new piece: EffectType.PromoteInsteadOfDiscard, the first
    /// Card.ReactiveDiscardEffect shape that REPLACES the discard (rather than adding a side
    /// effect on top of it, like Grimlock/Umber Hulk) and the first that's a genuine player
    /// choice - DiscardCardCommand.PromoteInsteadOfDiscard is the field carrying that choice,
    /// set by the player via the popup DiscardInputModeTests.cs covers at the input layer. Only
    /// legal when the discard is genuinely opponent-caused (TurnManager.ForcedActingPlayer ==
    /// the discarding player) - mirrors GrimlockScenarioTests.cs/UmberHulkScenarioTests.cs's own
    /// structure, loading the REAL "ambassador" entry out of the REAL cards.json and dispatching
    /// every command through a REAL CommandDispatcher.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class AmbassadorScenarioTests
    {
        // --- Row 1: positive/happy path for the PLAYED effect - the same plain Promote credit
        // core_noble already ships, unrelated to the reactive trigger. ---

        [TestMethod]
        public void PlayAmbassador_GrantsOnePromotionCredit()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "ambassador");

            scenario.PlayCard(card);

            Assert.AreEqual(1, scenario.Context.TurnManager.CurrentTurnContext.PendingPromotionsCount);
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
        }

        // --- The core new behavior: an opponent-caused discard of Ambassador can be promoted
        // instead, replacing the discard entirely. ---

        [TestMethod]
        public void DiscardCommand_AmbassadorForcedByNeogi_PromoteInsteadAccepted_PromotesInsteadOfDiscarding()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");
            Assert.AreEqual(blue, scenario.Context.ActivePlayer);

            scenario.Dispatch(new DiscardCardCommand(blue.Color, ambassador.Id, promoteInsteadOfDiscard: true));

            Assert.DoesNotContain(ambassador, blue.Hand);
            Assert.DoesNotContain(ambassador, blue.DiscardPile, "Must not have been discarded at all - promote-instead replaces the discard.");
            Assert.Contains(ambassador, blue.InnerCircle);
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "The forced-discard sequence should have completed normally.");
        }

        [TestMethod]
        public void DiscardCommand_AmbassadorForcedByNeogi_PromoteInsteadDeclined_DiscardsNormally()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, ambassador.Id, promoteInsteadOfDiscard: false));

            Assert.DoesNotContain(ambassador, blue.Hand);
            Assert.Contains(ambassador, blue.DiscardPile, "Declining must discard normally, exactly like any other card.");
            Assert.DoesNotContain(ambassador, blue.InnerCircle);
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
        }

        [TestMethod]
        public void DiscardCommand_AmbassadorForcedByCraniumRats_PromoteInsteadAccepted_AlsoPromotesInstead()
        {
            // Regression, mirroring GrimlockScenarioTests/UmberHulkScenarioTests: Neogi's
            // MatchManager._pendingDiscardQueue is NOT the only way a shipped card forces an
            // opponent to discard - Cranium Rats forces one via SelectOpponent -> OnSuccess:
            // DiscardCard, entirely on ActionSystem's ExecutionStack. The promote-instead choice
            // must work for this path too, not just Neogi's.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var craniumRats = scenario.GiveCard(PlayerColor.Red, "cranium_rats");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");

            scenario.PlayCard(craniumRats);
            scenario.Dispatch(new SelectOpponentCommand(blue.Color));
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Setup check: this is the ExecutionStack-based chain, not Neogi's queue.");

            scenario.Dispatch(new DiscardCardCommand(blue.Color, ambassador.Id, promoteInsteadOfDiscard: true));

            Assert.Contains(ambassador, blue.InnerCircle);
            Assert.DoesNotContain(ambassador, blue.DiscardPile);
            Assert.IsNull(scenario.Context.TurnManager.ForcedActingPlayer, "The forced-actor override should still be fully released once the chain completes.");
        }

        [TestMethod]
        public void DiscardCommand_AmbassadorDiscardedViaOwnEffect_PromoteInsteadRequested_IsRejected()
        {
            // Insane Outcast's own "discard a card from your hand" cost - the discarding
            // player's OWN choice, not an opponent's effect. Validate() must reject
            // PromoteInsteadOfDiscard here even though the card carries the reactive effect.
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);

            var insaneOutcast = scenario.GiveCard(PlayerColor.Red, "insane_outcast");
            var ambassador = scenario.GiveCard(PlayerColor.Red, "ambassador");
            scenario.PlayCard(insaneOutcast);
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState);

            scenario.AssertRejected(new DiscardCardCommand(red.Color, ambassador.Id, promoteInsteadOfDiscard: true));

            Assert.Contains(ambassador, red.Hand, "The rejected command must not have executed - Ambassador should still be in hand.");
        }

        // --- Row 6: unmet precondition - promote-instead requested on a card that doesn't carry
        // the PromoteInsteadOfDiscard reactive effect at all. ---

        [TestMethod]
        public void DiscardCommand_PromoteInsteadRequested_OnACardWithoutTheReactiveEffect_IsRejected()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var plainCard = scenario.GiveCard(PlayerColor.Blue, "core_house_guard");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(ActionState.TargetingDiscard, scenario.Context.ActionSystem.CurrentState, "Setup check: Blue is now forced to discard.");

            scenario.AssertRejected(new DiscardCardCommand(blue.Color, plainCard.Id, promoteInsteadOfDiscard: true));

            Assert.Contains(plainCard, blue.Hand, "The rejected command must not have executed.");
            Assert.IsTrue(scenario.Context.MatchManager.IsResolvingOpponentDiscard, "Still mid-sequence, waiting for Blue's real discard.");
        }

        // --- Row 4: wrong-player dispatch during the forced-discard window. ---

        [TestMethod]
        public void DiscardCommand_DuringAmbassadorsForcedDiscard_DispatchedByTheWrongPlayer_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");
            scenario.Dispatch(new EndTurnCommand());
            Assert.AreEqual(blue, scenario.Context.ActivePlayer, "Setup check: Blue is the one forced to discard, not Red.");

            scenario.AssertRejected(new DiscardCardCommand(red.Color, ambassador.Id, promoteInsteadOfDiscard: true));

            Assert.Contains(ambassador, blue.Hand, "Ambassador must still be in Blue's hand - the rejected command must not have executed.");
        }

        // --- Row 7: double-dispatch/replay - the promotion must not double-fire either. ---

        [TestMethod]
        public void DiscardCommand_AmbassadorPromoteInsteadDispatchedTwice_SecondDispatchIsRejected_PromotesOnlyOnce()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");
            scenario.Dispatch(new EndTurnCommand());

            scenario.DispatchTwice(new DiscardCardCommand(blue.Color, ambassador.Id, promoteInsteadOfDiscard: true));

            Assert.HasCount(1, blue.InnerCircle.Where(c => c == ambassador), "Ambassador must have been promoted exactly once, not twice.");
            Assert.IsFalse(scenario.Context.MatchManager.IsResolvingOpponentDiscard);
        }

        // --- Row 9: DTO round-trip. ---

        [TestMethod]
        public void DiscardCardCommand_DtoRoundTrip_StillPromotesAmbassadorInstead()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var blue = scenario.Player(PlayerColor.Blue);

            var neogi = scenario.GiveCard(PlayerColor.Red, "neogi");
            scenario.PlayCard(neogi);
            var ambassador = scenario.GiveCard(PlayerColor.Blue, "ambassador");
            scenario.Dispatch(new EndTurnCommand());

            var command = new DiscardCardCommand(blue.Color, ambassador.Id, promoteInsteadOfDiscard: true);
            var dto = command.ToDto();
            var hydrated = DtoMapper.HydrateCommand(dto, scenario.Context) as DiscardCardCommand;

            Assert.IsNotNull(hydrated, "The DTO should round-trip back into a DiscardCardCommand.");
            Assert.IsTrue(hydrated!.PromoteInsteadOfDiscard, "PromoteInsteadOfDiscard must survive the DTO round-trip.");

            scenario.Dispatch(hydrated);

            Assert.Contains(ambassador, blue.InnerCircle);
            Assert.DoesNotContain(ambassador, blue.DiscardPile);
        }
    }
}
