using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;

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
            _state = Substitute.For<IGameplayState>();
            _command = Substitute.For<IGameCommand>();

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
            _state.MatchContext.Returns(matchContext);

            // Act
            _dispatcher.Dispatch(_command, _state);

            // Assert
            // 1. Verifies Recording
            _replayManager.Received(1).RecordCommand(_command, player, Arg.Any<int>());
            
            // 2. Verifies Execution
            _command.Received(1).Execute(_state);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Dispatch_WhenReplaying_ExecutesButDoesNotRecord()
        {
            // Arrange
            _replayManager.IsReplaying.Returns(true);

            // Act
            _dispatcher.Dispatch(_command, _state);

            // Assert
            _replayManager.DidNotReceive().RecordCommand(Arg.Any<IGameCommand>(), Arg.Any<Player>(), Arg.Any<int>());
            _command.Received(1).Execute(_state);
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
            _state.MatchContext.Returns(matchContext);

            // Act
            _dispatcher.Dispatch(_command, _state); // seq 1
            _dispatcher.Dispatch(_command, _state); // seq 2

            // Assert
            _replayManager.Received().RecordCommand(_command, player, 1);
            _replayManager.Received().RecordCommand(_command, player, 2);
        }
    }
}
