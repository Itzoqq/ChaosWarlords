using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;

namespace ChaosWarlords.Tests.Source.Input.Controllers
{
    [TestClass]
    [TestCategory("Unit")]
    public class ReplayControllerTests
    {
        private ReplayController _controller = null!;
        private IGameplayState _stateMock = null!;
        private IReplayManager _replayManagerMock = null!;
        private IInputManager _inputManagerMock = null!;
        private IGameLogger _loggerMock = null!;
        private Action _onRestartMock = null!;
        private MatchContext _matchContext = null!;
        private IMapManager _mapManagerMock = null!;
        private ITurnManager _turnManagerMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _stateMock = Substitute.For<IGameplayState>();
            _replayManagerMock = Substitute.For<IReplayManager>();
            _inputManagerMock = Substitute.For<IInputManager>();
            _loggerMock = Substitute.For<IGameLogger>();
            _onRestartMock = Substitute.For<Action>();
            _mapManagerMock = Substitute.For<IMapManager>();
            _turnManagerMock = Substitute.For<ITurnManager>();

            // Create real MatchContext with mocked dependencies
            _matchContext = new MatchContext(
                _turnManagerMock,
                _mapManagerMock,
                Substitute.For<IMarketManager>(),
                Substitute.For<IActionSystem>(),
                Substitute.For<ChaosWarlords.Source.Core.Interfaces.Data.ICardDatabase>(),
                Substitute.For<IPlayerStateManager>(),
                _loggerMock,
                123
            );
            _matchContext.CurrentPhase = MatchPhase.Playing;

            // Setup default state mocks
            _stateMock.MatchContext.Returns(_matchContext);
            _stateMock.MapManager.Returns(_mapManagerMock);
            _stateMock.TurnManager.Returns(_turnManagerMock);
            _mapManagerMock.Nodes.Returns(new List<MapNode>());

            _controller = new ReplayController(_stateMock, _replayManagerMock, _inputManagerMock, _loggerMock, _onRestartMock);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test file if created
            if (File.Exists("last_replay.json"))
                File.Delete("last_replay.json");
        }

        #region Save Tests

        [TestMethod]
        public void Update_F5Pressed_DuringPlayingPhase_SavesReplay()
        {
            // Arrange
            _inputManagerMock.IsKeyJustPressed(Keys.F5).Returns(true);
            _matchContext.CurrentPhase = MatchPhase.Playing;
            _replayManagerMock.IsReplaying.Returns(false);
            _replayManagerMock.GetRecordingJson().Returns("{\"test\":\"data\"}");

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.Received(1).GetRecordingJson();
            Assert.IsTrue(File.Exists("last_replay.json"));
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("saved")), LogChannel.Info);
        }

        [TestMethod]
        public void Update_F5Pressed_DuringSetupPhase_LogsWarning()
        {
            // Arrange
            _inputManagerMock.IsKeyJustPressed(Keys.F5).Returns(true);
            _matchContext.CurrentPhase = MatchPhase.Setup;

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.DidNotReceive().GetRecordingJson();
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("Cannot save replay during setup")), LogChannel.Warning);
        }

        [TestMethod]
        public void Update_F5Pressed_WhileReplaying_DoesNotSave()
        {
            // Arrange
            _inputManagerMock.IsKeyJustPressed(Keys.F5).Returns(true);
            _matchContext.CurrentPhase = MatchPhase.Playing;
            _replayManagerMock.IsReplaying.Returns(true);

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.DidNotReceive().GetRecordingJson();
        }

        #endregion

        #region Load Tests

        [TestMethod]
        public void Update_F6Pressed_WithNoTroops_StartsReplay()
        {
            // Arrange
            File.WriteAllText("last_replay.json", "{\"seed\":123}");
            _inputManagerMock.IsKeyJustPressed(Keys.F6).Returns(true);
            _mapManagerMock.Nodes.Returns(new List<MapNode> { new MapNode(1, Vector2.Zero) }); // Empty node
            _replayManagerMock.Seed.Returns(123);

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.Received(1).StartReplay(Arg.Any<string>());
            _onRestartMock.Received(1).Invoke();
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("Replay started")), LogChannel.Info);
        }

        [TestMethod]
        public void Update_F6Pressed_WithTroopsPlaced_LogsWarning()
        {
            // Arrange
            _inputManagerMock.IsKeyJustPressed(Keys.F6).Returns(true);
            var occupiedNode = new MapNode(1, Vector2.Zero) { Occupant = PlayerColor.Red };
            _mapManagerMock.Nodes.Returns(new List<MapNode> { occupiedNode });

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.DidNotReceive().StartReplay(Arg.Any<string>());
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("Cannot start replay after troops")), LogChannel.Warning);
        }

        [TestMethod]
        public void Update_F6Pressed_NoReplayFile_LogsWarning()
        {
            // Arrange
            _inputManagerMock.IsKeyJustPressed(Keys.F6).Returns(true);
            _mapManagerMock.Nodes.Returns(new List<MapNode>());

            // Act
            _controller.Update(new GameTime());

            // Assert
            _replayManagerMock.DidNotReceive().StartReplay(Arg.Any<string>());
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("No replay file found")), LogChannel.Warning);
        }

        #endregion

        #region Playback Tests

        [TestMethod]
        public void Update_WhileReplaying_ExecutesCommandsOnTimer()
        {
            // Arrange
            _replayManagerMock.IsReplaying.Returns(true);
            var mockCommand = Substitute.For<IGameCommand>();
            _replayManagerMock.GetNextCommand(_stateMock).Returns(mockCommand);
            var activePlayer = new Player(PlayerColor.Red);
            _turnManagerMock.ActivePlayer.Returns(activePlayer);

            // Act - First update (timer < 0.2s)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)));
            
            // Assert - Command not executed yet
            mockCommand.DidNotReceive().Execute(Arg.Any<IGameplayState>());

            // Act - Second update (timer >= 0.2s)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.15)));

            // Assert - Command executed
            mockCommand.Received(1).Execute(_stateMock);
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("Replay Executed")), LogChannel.Info);
        }

        [TestMethod]
        public void Update_ReplayComplete_LogsCompletionOnce()
        {
            // Arrange
            _replayManagerMock.IsReplaying.Returns(true);
            _replayManagerMock.GetNextCommand(_stateMock).Returns((IGameCommand?)null);

            // Act - First completion
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.3)));

            // Assert
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("REPLAY COMPLETE")), LogChannel.Info);

            // Act - Second update (should not log again)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.3)));

            // Assert - Still only one log
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("REPLAY COMPLETE")), LogChannel.Info);
        }

        #endregion

        #region Constructor Tests


        [TestMethod]
        public void Constructor_NullGameState_ThrowsException()
        {
            try
            {
                new ReplayController(null!, _replayManagerMock, _inputManagerMock, _loggerMock, _onRestartMock);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Constructor_NullReplayManager_ThrowsException()
        {
            try
            {
                new ReplayController(_stateMock, null!, _inputManagerMock, _loggerMock, _onRestartMock);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        #endregion
    }
}
