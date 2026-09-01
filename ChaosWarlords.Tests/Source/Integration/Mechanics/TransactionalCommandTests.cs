using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using ChaosWarlords.Source.Core.Interfaces.Data;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// Integration tests for transactional command execution with Devour→Supplant chains.
    /// Based on ActionSystemTransactionTests patterns but with real database loading.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class TransactionalCommandTests
    {
        private Player _player = null!;
        private IMapManager _mapManager = null!;
        private ITurnManager _turnManager = null!;
        private IMatchManager _matchManager = null!;
        private ActionSystem _actionSystem = null!;
        private MapNode _node1 = null!;

        [TestInitialize]
        public void Setup()
        {
            Utilities.TestLogger.Initialize();
            _player = TestData.Players.RedPlayer();

            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();
            _matchManager = Substitute.For<IMatchManager>();

            _turnManager.ActivePlayer.Returns(_player);

            _node1 = TestData.MapNodes.Node1();
            _mapManager.Nodes.Returns(new List<MapNode> { _node1 });

            _actionSystem = new ActionSystem(_turnManager, _mapManager, Utilities.TestLogger.Instance, Substitute.For<IPlayerStateManager>(), Substitute.For<IMarketManager>());
            _actionSystem.SetMatchManager(_matchManager);

            _player.ClearHand();
        }

        [TestMethod]
        public void DevourTargeting_DeferredExecution_BuffersCardWithoutExecuting()
        {
            // Arrange
            var sourceCard = TestData.Cards.CheapCard();
            var targetCard = TestData.Cards.CheapCard();
            _player.AddToHand(targetCard);

            // Act: Start deferred devour and selecttarget
            _actionSystem.TryStartDevourHand(sourceCard, null, deferExecution: true);
            var cmd = _actionSystem.HandleDevourSelection(targetCard);

            // Execute command to trigger deferral
            // Mock Context
            var context = new ChaosWarlords.Source.Contexts.MatchContext(
                 _turnManager,
                 _mapManager,
                 Substitute.For<IMarketManager>(),
                 _actionSystem,
                 Substitute.For<ICardDatabase>(),
                 Substitute.For<IPlayerStateManager>(),
                 Utilities.TestLogger.Instance);

            Assert.IsNotNull(cmd);
            cmd.Execute(context);

            // Assert: Card should be buffered, NOT executed
            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
            Assert.AreEqual(targetCard, _actionSystem.PendingDevourCard);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void DevourThenSupplant_BuffersDevourCard_IncludesInCommand()
        {
            // Arrange
            var sourceCard = TestData.Cards.SupplantCard();
            var targetCard = TestData.Cards.CheapCard();
            _player.AddToHand(targetCard);
            _player.TroopsInBarracks = 5;

            // Act 1: Start Devour with callback to Supplant
            _actionSystem.TryStartDevourHand(sourceCard, () =>
            {
                _actionSystem.StartTargeting(ActionState.TargetingSupplant, sourceCard);
            }, deferExecution: true);

            // Act 2: Select card to devour
            // Act 2: Select card to devour & execute deferral
            var cmd = _actionSystem.HandleDevourSelection(targetCard);
            var context = new ChaosWarlords.Source.Contexts.MatchContext(
                 _turnManager,
                 _mapManager,
                 Substitute.For<IMarketManager>(),
                 _actionSystem,
                 Substitute.For<ICardDatabase>(),
                 Substitute.For<IPlayerStateManager>(),
                 Utilities.TestLogger.Instance);

            Assert.IsNotNull(cmd);
            cmd.Execute(context);

            // Assert: Should be in Supplant targeting with buffered devour
            Assert.AreEqual(targetCard, _actionSystem.PendingDevourCard);
            Assert.AreEqual(ActionState.TargetingSupplant, _actionSystem.CurrentState);

            // Act 3: Complete Supplant targeting
            _mapManager.CanAssassinate(_node1, _player).Returns(true);
            var command = _actionSystem.HandleTargetClick(_node1, null);

            // Assert: Command should include devour card ID
            Assert.IsNotNull(command);
            var supplantCmd = command as ChaosWarlords.Source.Commands.SupplantCommand;
            Assert.IsNotNull(supplantCmd);
            Assert.AreEqual(targetCard.Id, supplantCmd.DevourCardId);
        }

        [TestMethod]
        public void CancelTargeting_AfterDeferredDevour_ClearsPendingCard()
        {
            // Arrange
            var sourceCard = TestData.Cards.CheapCard();
            var targetCard = TestData.Cards.CheapCard();
            _player.AddToHand(targetCard);

            _actionSystem.TryStartDevourHand(sourceCard, null, deferExecution: true);
            var cmd = _actionSystem.HandleDevourSelection(targetCard);

            var context = new ChaosWarlords.Source.Contexts.MatchContext(
                 _turnManager,
                 _mapManager,
                 Substitute.For<IMarketManager>(),
                 _actionSystem,
                 Substitute.For<ICardDatabase>(),
                 Substitute.For<IPlayerStateManager>(),
                 Utilities.TestLogger.Instance);

            Assert.IsNotNull(cmd);
            cmd.Execute(context);
            Assert.AreEqual(targetCard, _actionSystem.PendingDevourCard);

            // Act: Cancel
            _actionSystem.CancelTargeting();

            // Assert: Pending devour cleared
            Assert.IsNull(_actionSystem.PendingDevourCard);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void PreTargeting_SetAndRetrieve_WorksAcrossMultipleStates()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();
            var devourTarget = TestData.Cards.CheapCard();

            // Act: Set multiple pre-targets
            _actionSystem.SetPreTarget(card, ActionState.TargetingDevourHand, devourTarget);
            _actionSystem.SetPreTarget(card, ActionState.TargetingSupplant, _node1);

            // Assert: Retrieve pre-targets
            var retrieved1 = _actionSystem.GetAndClearPreTarget(card, ActionState.TargetingDevourHand);
            var retrieved2 = _actionSystem.GetAndClearPreTarget(card, ActionState.TargetingSupplant);

            Assert.AreEqual(devourTarget, retrieved1);
            Assert.AreEqual(_node1, retrieved2);

            // Verify cleared
            Assert.IsNull(_actionSystem.GetAndClearPreTarget(card, ActionState.TargetingDevourHand));
            Assert.IsNull(_actionSystem.GetAndClearPreTarget(card, ActionState.TargetingSupplant));
        }

        [TestMethod]
        public void SkippedTarget_CanBeSetAndRetrieved()
        {
            // Arrange
            var card = TestData.Cards.CheapCard();

            // Act
            _actionSystem.SetPreTarget(card, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            // Assert
            var retrieved = _actionSystem.GetAndClearPreTarget(card, ActionState.TargetingDevourHand);
            Assert.AreEqual(ActionSystem.SkippedTarget, retrieved);
        }
    }
}
