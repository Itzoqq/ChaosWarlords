using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    public class CommandDispatcherTests
    {
        private IReplayManager _replayManager = null!;
        private IGameLogger _logger = null!;
        private CommandDispatcher _dispatcher = null!;
        private IGameplayState _state = null!;
        private IGameCommand _command = null!;

        [TestInitialize]
        public void Setup()
        {
            _replayManager = Substitute.For<IReplayManager>();
            _logger = Substitute.For<IGameLogger>();
            // Use Fake State
            // _state = Substitute.For<IGameplayState>();
            // We'll init the fake in each test or setup if possible, but the fake is lightweight.
            // Let's create a field for it but we need to reset it for each test if we shared it.
            // Better to instantiate in logic, but test fields are fine if re-init in setup.

            // Actually, let's keep the field type as IGameplayState interface for the dispatcher signature,
            // but assign the concrete fake.
            _state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();

            _command = Substitute.For<IGameCommand>();
            _command.Validate(Arg.Any<MatchContext>()).Returns(true);

            _dispatcher = new CommandDispatcher(_replayManager, _logger);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenNotReplaying_RecordsAndExecutesCommand()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);
            var matchContext = new MatchContext(
                turnManager,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                null,
                _logger,
                123);

            // Act
            _dispatcher.Dispatch(_command, matchContext);

            // Assert
            // 1. Verifies Recording (Sequence Number starts at 0, increments to 1)
            _replayManager.Received(1).RecordCommand(_command, player, 1);

            // 2. Verifies Execution
            _command.Received(1).Execute(matchContext);

            // 3. Verify Context Sequence Updated
            Assert.AreEqual(1, matchContext.SequenceNumber);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenReplaying_ExecutesButDoesNotRecord()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(true);
            var matchContext = new MatchContext(
                Substitute.For<ITurnManager>(),
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                null,
                _logger,
                123);

            // Act
            _dispatcher.Dispatch(_command, matchContext);

            // Assert
            _replayManager.DidNotReceive().RecordCommand(Arg.Any<IGameCommand>(), Arg.Any<Player>(), Arg.Any<int>());
            _command.Received(1).Execute(matchContext);
            Assert.AreEqual(1, matchContext.SequenceNumber); // Still increments sequence logic
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_IncrementsSequenceCounter()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(false);
            var player = new Player(PlayerColor.Red);
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);
            var matchContext = new MatchContext(
                turnManager,
                Substitute.For<IMapManager>(),
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                null,
                _logger,
                123);


            // Act
            _dispatcher.Dispatch(_command, matchContext); // seq 1
            _dispatcher.Dispatch(_command, matchContext); // seq 2

            // Assert
            _replayManager.Received().RecordCommand(_command, player, 1);
            _replayManager.Received().RecordCommand(_command, player, 2);
            Assert.AreEqual(2, matchContext.SequenceNumber);
        }
    }
}
