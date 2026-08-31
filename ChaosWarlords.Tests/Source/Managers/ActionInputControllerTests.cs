using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Managers
{
    /// <summary>
    /// Direct tests for ActionInputController - previously covered only indirectly (through
    /// TargetingInputModeTests and various integration tests), which real code-coverage
    /// measurement (2026-08-31 architecture-review follow-up, see planning.txt) showed sat at
    /// 98% line coverage but only 74% BRANCH coverage: every one of the 7 targeting-state
    /// switch arms' "no target" null-check was only ever exercised on one side (the case
    /// where the click resolved to a valid node/site - never the defensive "routed here but
    /// targetNode/targetSite is null" case), HandleSupplant's CanAssassinate check was only
    /// ever exercised returning true, and HandleMoveDestination's PendingMoveSource == null
    /// guard was never exercised at all. This file closes those gaps directly rather than
    /// hoping some future indirect test happens to hit them.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ActionInputControllerTests
    {
        private IActionSystem _actionSystem = null!;
        private IMapManager _mapManager = null!;
        private ISpySubsystem _spySubsystem = null!;
        private ITurnManager _turnManager = null!;
        private Player _player = null!;
        private ActionInputController _controller = null!;

        [TestInitialize]
        public void Setup()
        {
            _actionSystem = Substitute.For<IActionSystem>();
            _mapManager = Substitute.For<IMapManager>();
            _spySubsystem = Substitute.For<ISpySubsystem>();
            _turnManager = Substitute.For<ITurnManager>();
            _player = new Player(PlayerColor.Red);
            _turnManager.ActivePlayer.Returns(_player);

            _controller = new ActionInputController(_actionSystem, _mapManager, _spySubsystem, _turnManager, TestLogger.Instance);
        }

        private static MapNode Node(int id = 1) => new MapNodeBuilder().WithId(id).Build();
        private static Site TestSite(int id = 1) => new NonCitySite("Test Site", ResourceType.Power, 0, ResourceType.Power, 0) { Id = id };

        // --- Default / unmapped state ---

        [TestMethod]
        public void HandleTargetClick_NormalState_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.Normal);

            var result = _controller.HandleTargetClick(Node(), TestSite());

            Assert.IsNull(result);
        }

        // --- TargetingAssassinate ---

        [TestMethod]
        public void HandleTargetClick_TargetingAssassinate_NullNode_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingAssassinate_InvalidTarget_RaisesActionFailedAndReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            var node = Node();
            _mapManager.CanAssassinate(node, _player).Returns(false);

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
            _actionSystem.Received(1).RaiseActionFailed(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleTargetClick_TargetingAssassinate_InsufficientPowerNoCard_NotifiesFailureAndReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            _actionSystem.PendingCard.Returns((ChaosWarlords.Source.Entities.Cards.Card?)null);
            var node = Node();
            _mapManager.CanAssassinate(node, _player).Returns(true);
            _player.AddPower(GameConstants.AssassinatePowerCost - 1);

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
            _actionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleTargetClick_TargetingAssassinate_ValidWithSufficientPower_ReturnsAssassinateCommand()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            _actionSystem.PendingCard.Returns((ChaosWarlords.Source.Entities.Cards.Card?)null);
            var node = Node(7);
            _mapManager.CanAssassinate(node, _player).Returns(true);
            _player.AddPower(GameConstants.AssassinatePowerCost);

            var result = _controller.HandleTargetClick(node, null);

            var cmd = Assert.IsInstanceOfType<AssassinateCommand>(result);
            Assert.AreEqual(7, cmd.TargetNodeId);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingAssassinate_PaidByCard_SkipsPowerCheck()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            _actionSystem.PendingCard.Returns(new ChaosWarlords.Source.Entities.Cards.Card("wight", "Wight", 0, CardAspect.Neutral, 0, 0, 0));
            var node = Node();
            _mapManager.CanAssassinate(node, _player).Returns(true);
            // Zero power - would fail the cost check if it ran, but a card is paying instead.

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsInstanceOfType<AssassinateCommand>(result);
            _actionSystem.DidNotReceive().NotifyFailure(Arg.Any<string>());
        }

        // --- TargetingReturn ---

        [TestMethod]
        public void HandleTargetClick_TargetingReturn_NullNode_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingReturn);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingReturn_CannotReturn_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingReturn);
            var node = Node();
            _mapManager.CanReturnTroop(node, _player).Returns(false);

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingReturn_Valid_ReturnsReturnTroopCommand()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingReturn);
            var node = Node(9);
            _mapManager.CanReturnTroop(node, _player).Returns(true);

            var result = _controller.HandleTargetClick(node, null);

            var cmd = Assert.IsInstanceOfType<ReturnTroopCommand>(result);
            Assert.AreEqual(9, cmd.TargetNodeId);
        }

        // --- TargetingSupplant ---

        [TestMethod]
        public void HandleTargetClick_TargetingSupplant_NullNode_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingSupplant);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingSupplant_CannotAssassinate_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingSupplant);
            var node = Node();
            _mapManager.CanAssassinate(node, _player).Returns(false);
            _player.TroopsInBarracks = 5;

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingSupplant_NoTroopsInBarracks_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingSupplant);
            var node = Node();
            _mapManager.CanAssassinate(node, _player).Returns(true);
            _player.TroopsInBarracks = 0;

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingSupplant_Valid_ReturnsSupplantCommand()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingSupplant);
            var node = Node(11);
            _mapManager.CanAssassinate(node, _player).Returns(true);
            _player.TroopsInBarracks = 3;

            var result = _controller.HandleTargetClick(node, null);

            var cmd = Assert.IsInstanceOfType<SupplantCommand>(result);
            Assert.AreEqual(11, cmd.TargetNodeId);
        }

        // --- TargetingPlaceSpy ---

        [TestMethod]
        public void HandleTargetClick_TargetingPlaceSpy_NullSite_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingPlaceSpy);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingPlaceSpy_ValidSite_DelegatesToSpySubsystem()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingPlaceSpy);
            var site = TestSite();
            var expectedCmd = Substitute.For<IGameCommand>();
            _spySubsystem.HandlePlaceSpy(site, Arg.Any<string?>()).Returns(expectedCmd);

            var result = _controller.HandleTargetClick(null, site);

            Assert.AreSame(expectedCmd, result);
        }

        // --- TargetingReturnSpy ---

        [TestMethod]
        public void HandleTargetClick_TargetingReturnSpy_NullSite_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingReturnSpy_ValidSite_DelegatesToSpySubsystem()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingReturnSpy);
            var site = TestSite();
            var expectedCmd = Substitute.For<IGameCommand>();
            _spySubsystem.HandleReturnSpyInitialClick(site, Arg.Any<string?>()).Returns(expectedCmd);

            var result = _controller.HandleTargetClick(null, site);

            Assert.AreSame(expectedCmd, result);
        }

        // --- TargetingMoveSource ---

        [TestMethod]
        public void HandleTargetClick_TargetingMoveSource_NullNode_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingMoveSource_Invalid_RaisesActionFailedAndReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);
            var node = Node();
            _mapManager.CanMoveSource(node, _player).Returns(false);

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
            _actionSystem.Received(1).RaiseActionFailed(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleTargetClick_TargetingMoveSource_Valid_SetsMoveSourceAndReturnsNull()
        {
            // Source selection is an intermediate step, not a command itself - it just
            // advances ActionSystem into the destination-targeting state.
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveSource);
            var node = Node();
            _mapManager.CanMoveSource(node, _player).Returns(true);

            var result = _controller.HandleTargetClick(node, null);

            Assert.IsNull(result);
            _actionSystem.Received(1).SetMoveSource(node);
        }

        // --- TargetingMoveDestination ---

        [TestMethod]
        public void HandleTargetClick_TargetingMoveDestination_NullNode_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveDestination);

            var result = _controller.HandleTargetClick(null, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingMoveDestination_NoPendingSource_ReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveDestination);
            _actionSystem.PendingMoveSource.Returns((MapNode?)null);
            var destination = Node();

            var result = _controller.HandleTargetClick(destination, null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleTargetClick_TargetingMoveDestination_InvalidDestination_RaisesActionFailedAndReturnsNull()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveDestination);
            var source = Node(1);
            _actionSystem.PendingMoveSource.Returns(source);
            var destination = Node(2);
            _mapManager.CanMoveDestination(destination).Returns(false);

            var result = _controller.HandleTargetClick(destination, null);

            Assert.IsNull(result);
            _actionSystem.Received(1).RaiseActionFailed(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleTargetClick_TargetingMoveDestination_Valid_ReturnsMoveTroopCommand()
        {
            _actionSystem.CurrentState.Returns(ActionState.TargetingMoveDestination);
            var source = Node(1);
            _actionSystem.PendingMoveSource.Returns(source);
            var destination = Node(2);
            _mapManager.CanMoveDestination(destination).Returns(true);

            var result = _controller.HandleTargetClick(destination, null);

            var cmd = Assert.IsInstanceOfType<MoveTroopCommand>(result);
            Assert.AreEqual(1, cmd.SourceNodeId);
            Assert.AreEqual(2, cmd.DestinationNodeId);
        }
    }
}
