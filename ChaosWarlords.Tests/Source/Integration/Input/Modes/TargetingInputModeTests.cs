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
        public void HandleInteraction_RightClick_MidwayThroughARepeatOptionalEffect_StillCancelsNormally()
        {
            // Same repeat-optional effect as above, but CurrentState has moved on to MoveUnit's
            // OWN 2nd sub-step (source picked, destination not yet chosen) - not a genuine
            // repeat boundary, so right-click must fall back to the normal full cancel.
            var sourceCard = new ChaosWarlords.Source.Entities.Cards.Card(
                "council_member", "Council Member", 6, ChaosWarlords.Source.Utilities.CardAspect.Blasphemy, 3, 6, 0);
            var sourceEffect = new ChaosWarlords.Source.Entities.Cards.CardEffect(EffectType.MoveUnit, 2) { AllowPartialRepeat = true };
            var effectContext = new ChaosWarlords.Source.Core.Contexts.EffectContext(
                ActionState.TargetingMoveSource, sourceCard, requiresInput: true, "Effect: MoveUnit", onResolved: _ => { }, sourceEffect: sourceEffect);

            _actionSub.CurrentState.Returns(ActionState.TargetingMoveDestination);
            _actionSub.CurrentEffect.Returns(effectContext);

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
        public void HandleInteraction_ClickingOutsideSpySelection_CancelsTargeting()
        {
            // 1. Arrange
            _actionSub.CurrentState.Returns(ActionState.SelectingSpyToReturn);

            var site = TestData.Sites.NeutralSite();
            // Use Reflection to set bounds if needed, or rely on defaults
            typeof(Site).GetProperty("Bounds")?.SetValue(site, new LogicRectangle(
                100 * LogicVector2.ScaleFactor, 100 * LogicVector2.ScaleFactor,
                100 * LogicVector2.ScaleFactor, 100 * LogicVector2.ScaleFactor));

            _actionSub.PendingSite.Returns(site);

            // Use method call instead of property
            _mapSub.GetEnemySpiesAtSite(site, _activePlayer).Returns(new List<PlayerColor> { PlayerColor.Blue });

            // Click FAR AWAY at (800, 600)
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(800, 600));

            // 2. Act
            _inputMode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // 3. Assert
            _actionSub.Received(1).CancelTargeting();
            _actionSub.DidNotReceive().FinalizeSpyReturn(Arg.Any<PlayerColor>());
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
