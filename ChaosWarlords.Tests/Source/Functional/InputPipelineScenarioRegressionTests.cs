using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Utilities;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Pins the 4 real input-dispatch-layer bugs that motivated building InputPipelineScenario
    /// (see that class's own doc comment and planning.txt's TEST INFRASTRUCTURE section) as
    /// regression tests through the harness - proof this harness would have caught them, not
    /// just a plausible-sounding claim. Each test's own comment names the RESOLVED.txt commit
    /// that fixed the bug it pins.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class InputPipelineScenarioRegressionTests
    {
        [TestMethod]
        public void RightClick_WhileTargetingAssassinate_CancelsAndResyncsInputModeToNormal()
        {
            // Regression for RESOLVED.txt [375f118]: ActionSystem.CancelTargeting()'s real
            // snapshot-restore branch used to skip raising OnStateChanged, leaving
            // GameplayInputCoordinator (the client's only owner of which IInputMode is active)
            // permanently stuck showing TargetingInputMode even though ActionSystem itself was
            // correctly back to Normal. A test with a MOCKED ActionSystem cannot reproduce
            // this - CancelTargeting() on a mock is an inert stub with no restore logic to
            // contain the bug at all (see GameplayInputCoordinatorTests' own pre-existing test,
            // which used exactly that shape and passed even with the bug present).
            var scenario = InputPipelineScenario.Build();
            scenario.Context.CurrentPhase = MatchPhase.Playing;

            // ActionSystem.TryStartAssassinate only gates on Power (see its own implementation) -
            // no enemy troop/Presence setup is needed to REACH TargetingAssassinate, only to
            // complete an actual assassination, which this test never does.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.AddPower(10);

            scenario.ClickAssassinateButton();
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Setup failed to enter targeting.");
            Assert.IsInstanceOfType(scenario.CurrentMode, typeof(TargetingInputMode), "Setup failed to switch to TargetingInputMode.");

            scenario.RightClick(0, 0);

            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "Sanity: CancelTargeting should revert CurrentState.");
            Assert.IsInstanceOfType(scenario.CurrentMode, typeof(NormalPlayInputMode),
                "The coordinator's input mode must resync back to NormalPlayInputMode after a real cancel - otherwise the player is stuck in TargetingInputMode despite ActionSystem correctly being back in Normal.");
        }

        [TestMethod]
        public void ReturnSpy_WithTwoDifferentlyColoredEnemySpies_ReturnsOnlyTheClickedColor()
        {
            // Regression for RESOLVED.txt [243d922]: Return Spy was completely non-functional
            // whenever 2+ differently-colored enemy spies occupied the targeted site.
            // TargetingInputMode's own click handling used to unconditionally call
            // CancelTargeting() for ANY click during SelectingSpyToReturn, racing with
            // PlayerController's own (correct) InteractionMapper-based handling of the SAME
            // click - reverting the whole action before PlayerController's handler ever ran.
            // Neither PlayerControllerTests.cs (never constructs a real TargetingInputMode)
            // nor TargetingInputModeTests.cs (never constructs a real PlayerController) alone
            // could catch this - only a harness wiring both real classes against the SAME real
            // InputManager, in production's own subscription order, can.
            var scenario = InputPipelineScenario.Build(
                playerColors: new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Black });
            scenario.Context.CurrentPhase = MatchPhase.Playing;

            var site = scenario.Context.MapManager.Sites.First(s => s.NodesInternal.Count > 0);
            site.AddSpy(PlayerColor.Blue);
            site.AddSpy(PlayerColor.Black);

            var red = scenario.AsActivePlayer(PlayerColor.Red);
            scenario.Dispatch(new DeployTroopCommand(site.NodesInternal[0]));
            red.AddPower(10);

            scenario.ClickReturnSpyButton();
            Assert.AreEqual(ActionState.TargetingReturnSpy, scenario.Context.ActionSystem.CurrentState, "Setup failed to enter ReturnSpy targeting.");

            scenario.ClickSite(site);
            Assert.AreEqual(ActionState.SelectingSpyToReturn, scenario.Context.ActionSystem.CurrentState,
                "Setup failed to reach the color-selection sub-state (site has 2+ differently-colored spies).");

            scenario.ClickSpyReturnButton(PlayerColor.Blue, site);

            Assert.DoesNotContain(PlayerColor.Blue, site.Spies, "Blue's spy should have been returned.");
            Assert.Contains(PlayerColor.Black, site.Spies, "Black's spy must be untouched.");
            Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState, "The action should have fully resolved.");
            Assert.IsInstanceOfType(scenario.CurrentMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void ClickEndTurnButton_WhileTargeting_NeverFiresTheRequest_NoStateChange()
        {
            // Regression for RESOLVED.txt [f1c31bf]: the Main Game UI buttons (Market/
            // Assassinate/ReturnSpy/EndTurn) used to stay clickable throughout an unrelated
            // in-progress targeting sequence, able to permanently desync ActionSystem.
            // CurrentState with no error and no revert. UIManager.IsTargeting (synced each
            // frame by UIEventMediator.Update()) now gates these buttons' IsActive() - proven
            // here via a REAL UIManager reacting to a REAL targeting ActionState, not a mocked
            // IUIManager.IsTargeting flag set directly by the test. Asserts the UIManager-level
            // OnEndTurnRequest event itself never fires (not just that a deeper layer like
            // MatchManager.CanEndTurn/EndTurnCommand.Validate() also happens to reject it -
            // confirmed by mutation that removing ONLY UIManager's own IsTargeting gate leaves
            // this specific assertion failing even though those other layers keep the test's
            // end state assertions passing, i.e. defense-in-depth would otherwise mask this
            // layer being broken).
            var scenario = InputPipelineScenario.Build();
            scenario.Context.CurrentPhase = MatchPhase.Playing;

            // ActionSystem.TryStartAssassinate only gates on Power - see the sibling RightClick
            // test's own comment on why no enemy troop/Presence setup is needed here either.
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            red.AddPower(10);

            scenario.ClickAssassinateButton();
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState, "Setup failed to enter targeting.");

            int endTurnRequestCount = 0;
            scenario.UIManager.OnEndTurnRequest += (_, _) => endTurnRequestCount++;

            scenario.ClickEndTurnButton();

            Assert.AreEqual(0, endTurnRequestCount, "The End Turn button must be inactive (IsTargeting) - the click must never even raise OnEndTurnRequest.");
            Assert.AreEqual(ActionState.TargetingAssassinate, scenario.Context.ActionSystem.CurrentState,
                "Clicking End Turn mid-targeting must be rejected outright, not silently end the turn or desync state.");
            Assert.AreEqual(PlayerColor.Red, scenario.Context.ActivePlayer.Color, "The turn must not have advanced.");
        }

        [TestMethod]
        public void PlayHandCard_ThroughRealClick_DispatchesPlayCardCommand_AndResolvesTheEffect()
        {
            // Positive/ordinary-use smoke test (not a regression pin) - proves the harness's
            // basic click-to-command path works end to end for the common case, not just the
            // 3 adversarial scenarios above: a real hand-card click resolves through
            // NormalPlayInputMode -> PlayCardCommand -> the real CommandDispatcher, exactly
            // like MatchScenario.PlayCard's own direct-dispatch helper, but reached via an
            // actual simulated click instead.
            var scenario = InputPipelineScenario.Build();
            scenario.Context.CurrentPhase = MatchPhase.Playing;
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var card = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
            int powerBefore = red.Power;

            scenario.ClickHandCard(card);

            Assert.DoesNotContain(card, red.Hand, "The card should have left Hand.");
            Assert.Contains(card, red.PlayedCards, "The card should be in PlayedCards.");
            Assert.AreEqual(powerBefore + 2, red.Power, "core_house_guard grants +2 Power.");
            Assert.IsInstanceOfType(scenario.CurrentMode, typeof(NormalPlayInputMode), "No targeting effect - mode should stay Normal.");
        }
    }
}
