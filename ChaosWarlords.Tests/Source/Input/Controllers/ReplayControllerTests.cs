using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data; // Added for ICardDatabase
using ChaosWarlords.Source.Managers; // Added for PlayerStateManager
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Core.Events;
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
        // private IGameplayState _stateMock = null!; // Removed
        private ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState _stateFake = null!; // Use Fake
        private IReplayManager _replayManagerMock = null!;
        private IInputManager _inputManagerMock = null!;
        private IGameLogger _loggerMock = null!; // Changed ILogger to IGameLogger
        private Action _onRestartMock = null!;
        private MatchContext _matchContext = null!;
        // Need to Mock ITurnManager to inject into Context
        private ITurnManager _turnManagerMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _inputManagerMock = Substitute.For<IInputManager>();
            _replayManagerMock = Substitute.For<IReplayManager>();
            _loggerMock = Substitute.For<IGameLogger>();
            _onRestartMock = Substitute.For<Action>();

            // Setup minimalistic MatchContext
            _turnManagerMock = Substitute.For<ITurnManager>();
            var mapManagerMsg = Substitute.For<IMapManager>();
            var marketManagerMsg = Substitute.For<IMarketManager>();
            var actionSystemMsg = Substitute.For<IActionSystem>();
            var cardDbMsg = Substitute.For<ICardDatabase>();
            var psMsg = new PlayerStateManager(_loggerMock);

            var p1 = TestData.Players.RedPlayer();
            _turnManagerMock.ActivePlayer.Returns(p1);
            
            _matchContext = new MatchContext(
                _turnManagerMock,
                mapManagerMsg,
                marketManagerMsg,
                actionSystemMsg,
                cardDbMsg,
                psMsg,
                _loggerMock
            );

             _stateFake = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState
            {
                MatchContext = _matchContext
            };

            // ReplayController(IGameplayState, IReplayManager, IInputManager, IGameLogger, Action)
            _controller = new ReplayController(_stateFake, _replayManagerMock, _inputManagerMock, _loggerMock, _onRestartMock);
        }

        [TestMethod]
        public void Update_F5Pressed_DuringPlayingPhase_SavesReplay()
        {
            // Arrange
            _matchContext.CurrentPhase = MatchPhase.Playing;
            _replayManagerMock.IsReplaying.Returns(false);
            _replayManagerMock.GetRecordingJson().Returns("{\"test\":\"data\"}");
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.F5);

            // Act
            _inputManagerMock.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_inputManagerMock, evt);

            // Assert
            _replayManagerMock.Received(1).GetRecordingJson();
            Assert.IsTrue(File.Exists("last_replay.json"));
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("saved")), LogChannel.Info);
        }

        [TestMethod]
        public void Update_F5Pressed_DuringReplay_DoesNotSave()
        {
            // Arrange
            _matchContext.CurrentPhase = MatchPhase.Playing;
            _replayManagerMock.IsReplaying.Returns(true);
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.F5);

            // Act
            _inputManagerMock.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_inputManagerMock, evt);

            // Assert
            _replayManagerMock.DidNotReceive().GetRecordingJson();
        }

        [TestMethod]
        public void Update_F6Pressed_DuringPlaying_LoadsReplay()
        {
            // Arrange
            File.WriteAllText("last_replay.json", "{}");
            _matchContext.CurrentPhase = MatchPhase.Playing;
            _replayManagerMock.IsReplaying.Returns(false);
            
            // Ensure no troops are placed so load is allowed
            _matchContext.MapManager.Nodes.Returns(new List<ChaosWarlords.Source.Entities.Map.MapNode>());

            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.F6);

            // Act
            _inputManagerMock.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_inputManagerMock, evt);

            // Assert
            _replayManagerMock.Received(1).StartReplay(Arg.Any<string>());
        }

        [TestMethod]
        public void Update_WhileReplaying_ExecutesCommandsOnTimer()
        {
            // Arrange
            _replayManagerMock.IsReplaying.Returns(true);
            var mockCommand = Substitute.For<IGameCommand>();
            _replayManagerMock.GetNextCommand(_stateFake.MatchContext).Returns(mockCommand); // Passed fake state
            var activePlayer = new Player(PlayerColor.Red);
            _turnManagerMock.ActivePlayer.Returns(activePlayer);
            
            // Act - First update (timer < 0.2s)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)));

            // Assert - Command not executed yet
            mockCommand.DidNotReceive().Execute(Arg.Any<ChaosWarlords.Source.Contexts.MatchContext>());

            // Act - Second update (timer >= 0.2s)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.15)));

            // Assert - Command executed
            mockCommand.Received(1).Execute(_matchContext); // Uses MatchContext from controller
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("Replay Executed")), LogChannel.Info);
        }

        [TestMethod]
        public void Update_ReplayComplete_LogsCompletionOnce()
        {
            // Arrange
            _replayManagerMock.IsReplaying.Returns(true);
            _replayManagerMock.GetNextCommand(_stateFake.MatchContext).Returns((IGameCommand?)null);

            // Act - First completion
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.3)));

            // Assert
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("REPLAY COMPLETE")), LogChannel.Info);

            // Act - Second update (should not log again)
            _controller.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.3)));

            // Assert - Still only one log
            _loggerMock.Received(1).Log(Arg.Is<string>(s => s.Contains("REPLAY COMPLETE")), LogChannel.Info);
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_NullInputManager_ThrowsException()
        {
            try
            {
                // Sig: (State, Replay, Input, Logger, Action)
                new ReplayController(_stateFake, _replayManagerMock, null!, _loggerMock, _onRestartMock);
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
                new ReplayController(_stateFake, null!, _inputManagerMock, _loggerMock, _onRestartMock);
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
