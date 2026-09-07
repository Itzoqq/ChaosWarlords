using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Contexts;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Rendering;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Events; // Fixed namespace
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class TargetingInputModeTests
    {
        private TargetingInputMode _inputMode = null!;
        private MockInputProvider _mockInput = null!;
        private IInputManager _inputManager = null!;

        // Concrete Fake
        private TestGameplayState _stateFake = null!;

        // Substitutes (Dependencies of State)
        private IMapManager _mapSub = null!;
        private IActionSystem _actionSub = null!;
        private IMarketManager _marketSub = null!;
        private IUIManager _mockUI = null!;
        private TurnManager _turnManager = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockInput = new MockInputProvider();
            _inputManager = new InputManager(_mockInput);

            _mapSub = Substitute.For<IMapManager>();
            // GetNodeAt/GetSiteAt are now extension methods over Nodes/Sites (see
            // MapHitTestExtensions.cs), not mockable interface members - back them with real
            // (empty, by default) collections so the real hit-test math runs safely.
            _mapSub.Nodes.Returns(new List<MapNode>());
            _mapSub.Sites.Returns(new List<Site>());
            _actionSub = Substitute.For<IActionSystem>();
            _marketSub = Substitute.For<IMarketManager>();
            _mockUI = Substitute.For<IUIManager>();
            _activePlayer = TestData.Players.RedPlayer();
            var mockRandom = Substitute.For<IGameRandom>();

            // Define p1 and p2 for the TurnManager instantiation
            var p1 = _activePlayer;
            var p2 = TestData.Players.BluePlayer();
            _turnManager = new TurnManager(new List<Player> { p1, p2 }, mockRandom, Utilities.TestLogger.Instance);

            // Initialize Fake State
            _stateFake = new TestGameplayState
            {
                MapManager = _mapSub,
                TurnManager = _turnManager,
                ActionSystem = _actionSub,
                MarketManager = _marketSub,
                MatchContext = new MatchContext(
                     Substitute.For<ITurnManager>(),
                     _mapSub,
                     _marketSub,
                     _actionSub,
                     Substitute.For<ICardDatabase>(),
                     new PlayerStateManager(Utilities.TestLogger.Instance),
                     Utilities.TestLogger.Instance
                )
            };

            _inputMode = new TargetingInputMode(
                _stateFake,
                _inputManager,
                _mockUI,
                _mapSub,
                _turnManager,
                _actionSub
            );
        }

        [TestMethod]
        public void HandleInteraction_SafetyCheck_IfActionStateIsNormal_ReturnsSwitchCommand()
        {
            _actionSub.CurrentState.Returns(ActionState.Normal);
            // Default event
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);
            
            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_RightClick_CancelsTargeting_AndReturnsSwitchCommand()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            var evt = new InputEventArgs(InputEventType.RightClick, new Vector2(100, 100));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_Escape_CancelsTargeting_AndReturnsSwitchCommand()
        {
            // Escape used to reach this mode's cancel logic only via a global, competing
            // handler that ran BEFORE this mode's own dispatch - now it's a direct synonym for
            // RightClick, handled by exactly the same code path. See planning.txt.
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Escape);

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_RightClick_AtARepeatOptionalEffectsEntryState_DeclinesInsteadOfCancelling()
        {
            // Council Member's "Move up to 2 enemy troops" - right-click at a genuine repeat
            // boundary (CurrentState == the pending effect's own entry state) must decline the
            // remaining repeats (keeping whatever already resolved), not fully CancelTargeting().
            var sourceCard = new ChaosWarlords.Source.Entities.Cards.Card(
                "council_member", "Council Member", 6, ChaosWarlords.Source.Utilities.CardAspect.Blasphemy, 3, 6, 0);
            var sourceEffect = new ChaosWarlords.Source.Entities.Cards.CardEffect(EffectType.MoveUnit, 2) { AllowPartialRepeat = true };
            var effectContext = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingMoveSource, sourceCard, requiresInput: true, "Effect: MoveUnit", onResolved: _ => { }, sourceEffect: sourceEffect);

            _actionSub.CurrentState.Returns(ActionState.TargetingMoveSource);
            _actionSub.CurrentEffect.Returns(effectContext);

            var evt = new InputEventArgs(InputEventType.RightClick, new Vector2(100, 100));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.DidNotReceive().CancelTargeting();
            Assert.IsInstanceOfType(result, typeof(DeclineRepeatCommand));
            Assert.AreEqual("council_member", ((DeclineRepeatCommand)result!).CardId);
        }

        [TestMethod]
        public void HandleInteraction_RightClick_MidwayThroughARepeatOptionalEffect_WhenStepBackApplies_ReturnsNullWithoutCancelling()
        {
            // Phase 2 design decision (planning.txt): mid a multi-click sub-pick for a repeat-
            // capable effect (MoveUnit's source chosen, destination not yet picked), right-
            // click steps back to the entry state instead of fully cancelling the whole card
            // play - ActionSystem.TryAbortInProgressRepeatSubStep is the real primitive doing
            // this (see ActionSystemTests.cs for its own real, non-mocked coverage); here it's
            // stubbed true to prove HandleCancellation correctly defers to it FIRST, before
            // ever reaching CancelTargeting().
            var sourceCard = new ChaosWarlords.Source.Entities.Cards.Card(
                "council_member", "Council Member", 6, ChaosWarlords.Source.Utilities.CardAspect.Blasphemy, 3, 6, 0);
            var sourceEffect = new ChaosWarlords.Source.Entities.Cards.CardEffect(EffectType.MoveUnit, 2) { AllowPartialRepeat = true };
            var effectContext = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingMoveSource, sourceCard, requiresInput: true, "Effect: MoveUnit", onResolved: _ => { }, sourceEffect: sourceEffect);

            _actionSub.CurrentState.Returns(ActionState.TargetingMoveDestination);
            _actionSub.CurrentEffect.Returns(effectContext);
            _actionSub.TryAbortInProgressRepeatSubStep().Returns(true);

            var evt = new InputEventArgs(InputEventType.RightClick, new Vector2(100, 100));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result, "A successful step-back is purely client-side - nothing to dispatch.");
            _actionSub.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleInteraction_RightClick_MidwayThroughARepeatOptionalEffect_WhenStepBackDoesNotApply_FallsBackToFullCancel()
        {
            // The genuinely-unrelated-cancel fallback (non-repeat effects, mid-chain Devour,
            // etc.) still needs to work when TryAbortInProgressRepeatSubStep has nothing to do
            // (stubbed false here, matching what the real ActionSystem returns whenever this
            // isn't a repeat-capable effect's own in-progress sub-pick).
            var sourceCard = new ChaosWarlords.Source.Entities.Cards.Card(
                "council_member", "Council Member", 6, ChaosWarlords.Source.Utilities.CardAspect.Blasphemy, 3, 6, 0);
            var sourceEffect = new ChaosWarlords.Source.Entities.Cards.CardEffect(EffectType.MoveUnit, 2) { AllowPartialRepeat = true };
            var effectContext = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingMoveSource, sourceCard, requiresInput: true, "Effect: MoveUnit", onResolved: _ => { }, sourceEffect: sourceEffect);

            _actionSub.CurrentState.Returns(ActionState.TargetingMoveDestination);
            _actionSub.CurrentEffect.Returns(effectContext);
            _actionSub.TryAbortInProgressRepeatSubStep().Returns(false);

            var evt = new InputEventArgs(InputEventType.RightClick, new Vector2(100, 100));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_UIBlocking_IfMarketHovered_DoesNothing()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingPlaceSpy);
            _mockUI.IsMarketHovered.Returns(true);

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var result = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
            _actionSub.DidNotReceive().HandleTargetClick(Arg.Any<MapNode>(), Arg.Any<Site>());
        }

        [TestMethod]
        public void HandleInteraction_ValidTargetClick_CallsSystemHandler()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingAssassinate);

            var node = TestData.MapNodes.Node1();
            var clickPos = new Vector2(200, 200);
            node.Position = clickPos.ToLogicVector2(); // so the real GetNodeAt hit-test math finds it
            _mapSub.Nodes.Returns(new List<MapNode> { node });

            var evt = new InputEventArgs(InputEventType.LeftClick, clickPos);

            _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).HandleTargetClick(node, null!);
        }

        [TestMethod]
        public void HandleInteraction_LeftClick_WhileSelectingSpyToReturn_NeverTouchesActionSystem()
        {
            // SelectingSpyToReturn (picking WHICH of 2+ enemy spies at a site to return) is
            // PlayerController's sole responsibility - it has the InteractionMapper-based hit-
            // testing this class doesn't, and it's a separate, independent subscriber on the
            // same InputManager.OnInputEvent stream (no shared "handled" flag - see
            // planning.txt), so this class must do nothing at all for this state, regardless of
            // where the click lands. The end-to-end test proving PlayerController actually
            // finalizes the return lives in GameplayStateTests.cs, since that's the only
            // harness wiring both real subscribers against one real InputManager (this class's
            // own test fixture doesn't construct PlayerController at all).
            _actionSub.CurrentState.Returns(ActionState.SelectingSpyToReturn);

            var site = TestData.Sites.NeutralSite();
            _actionSub.PendingSite.Returns(site);

            // Click anywhere - even a position that would have hit a genuinely valid spy
            // button under PlayerController's own hit-test - must be a no-op here.
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(400, 255));

            var command = _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(command);
            _actionSub.DidNotReceive().CancelTargeting();
            _actionSub.DidNotReceive().FinalizeSpyReturn(Arg.Any<PlayerColor>());
            _actionSub.DidNotReceive().HandleTargetClick(Arg.Any<MapNode>(), Arg.Any<Site>());
        }

        [TestMethod]
        public void HandleInteraction_ClickingSite_PassesSiteToActionSystem()
        {
            _actionSub.CurrentState.Returns(ActionState.TargetingPlaceSpy);

            var targetSite = TestData.Sites.NeutralSite();
            var clickPos = new Vector2(300, 300);
            // Real GetSiteAt hit-test needs the site's Bounds to actually contain clickPos.
            typeof(Site).GetProperty("Bounds")?.SetValue(targetSite, new LogicRectangle(
                250 * LogicVector2.ScaleFactor, 250 * LogicVector2.ScaleFactor,
                100 * LogicVector2.ScaleFactor, 100 * LogicVector2.ScaleFactor));
            _mapSub.Sites.Returns(new List<Site> { targetSite });
            // _mapSub.Nodes stays empty (Setup default) - real GetNodeAt hit-test finds nothing.

            var evt = new InputEventArgs(InputEventType.LeftClick, clickPos);

            _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            _actionSub.Received(1).HandleTargetClick(null!, targetSite);
        }
    }
}
