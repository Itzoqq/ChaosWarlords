using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using System;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class ReplayManagerTests
    {
        private ReplayManager _manager = null!;
        private IGameLogger _loggerMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = Substitute.For<IGameLogger>();
            _manager = new ReplayManager(_loggerMock);
        }

        #region Recording Tests

        [TestMethod]
        public void InitializeRecording_SetsSeedAndClearsRecording()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            var command = new PlayCardCommand(card);

            // Record something first
            _manager.InitializeRecording(100);
            _manager.RecordCommand(command, player, 1);

            // Act - Re-initialize with new seed
            _manager.InitializeRecording(200);

            // Assert
            Assert.AreEqual(200, _manager.Seed);
            var json = _manager.GetRecordingJson();
            Assert.Contains("\"Commands\":[]", json, "Recording should be cleared");
        }

        [TestMethod]
        public void RecordCommand_AddsCommandToRecording()
        {
            // Arrange
            _manager.InitializeRecording(123);
            var player = new Player(PlayerColor.Red);
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);
            var command = new PlayCardCommand(card);

            // Act
            _manager.RecordCommand(command, player, 1);
            string json = _manager.GetRecordingJson();

            // Assert
            Assert.Contains("\"Seq\":1", json);
            Assert.Contains("test_id", json);
        }

        [TestMethod]
        public void RecordCommand_WhileReplaying_DoesNotRecord()
        {
            // Arrange
            _manager.InitializeRecording(123);
            var player = new Player(PlayerColor.Red);
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);
            var command = new PlayCardCommand(card);

            // Start replay mode
            var replayJson = "{\"Seed\":123,\"Commands\":[]}";
            _manager.StartReplay(replayJson);

            // Act - Try to record while replaying
            _manager.RecordCommand(command, player, 1);
            string json = _manager.GetRecordingJson();

            // Assert
            Assert.IsTrue(_manager.IsReplaying);
            Assert.Contains("\"Commands\":[]", json, "Should not record during replay");
        }

        [TestMethod]
        public void RecordCommand_WithNullCommand_DoesNotCrash()
        {
            // Arrange
            _manager.InitializeRecording(123);
            var player = new Player(PlayerColor.Red);

            // Act
            _manager.RecordCommand(null!, player, 1);

            // Assert - No exception thrown
            Assert.IsNotNull(_manager.GetRecordingJson());
        }

        #endregion

        #region Playback Tests

        [TestMethod]
        public void StartReplay_WithValidJson_EntersReplayMode()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);
            var command = new PlayCardCommand(card);

            _manager.InitializeRecording(123);
            _manager.RecordCommand(command, player, 1);
            string json = _manager.GetRecordingJson();

            // Act
            var newManager = new ReplayManager(_loggerMock);
            newManager.StartReplay(json);

            // Assert
            Assert.IsTrue(newManager.IsReplaying);
            Assert.AreEqual(123, newManager.Seed);
        }

        [TestMethod]
        public void StartReplay_WithInvalidJson_DoesNotEnterReplayMode()
        {
            // Act
            _manager.StartReplay("INVALID JSON");

            // Assert
            Assert.IsFalse(_manager.IsReplaying);
        }

        [TestMethod]
        public void GetNextCommand_WhenNotReplaying_ReturnsNull()
        {
            // Arrange
            var stateMock = Substitute.For<IGameplayState>();

            // Act
            var result = _manager.GetNextCommand(stateMock);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetNextCommand_WithEmptyQueue_StopsReplayAndReturnsNull()
        {
            // Arrange
            var replayJson = "{\"Seed\":123,\"Commands\":[]}";
            _manager.StartReplay(replayJson);
            var stateMock = Substitute.For<IGameplayState>();

            // Act
            var result = _manager.GetNextCommand(stateMock);

            // Assert
            Assert.IsNull(result);
            Assert.IsFalse(_manager.IsReplaying, "Should auto-stop when queue is empty");
        }

        [TestMethod]
        public void GetNextCommand_WithValidCommand_ReturnsHydratedCommand()
        {
            // Arrange
            var player = new Player(PlayerColor.Red) { SeatIndex = 0 };
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);

            _manager.InitializeRecording(123);
            _manager.RecordCommand(new PlayCardCommand(card), player, 1);
            string json = _manager.GetRecordingJson();

            var newManager = new ReplayManager(_loggerMock);
            newManager.StartReplay(json);

            var stateMock = Substitute.For<IGameplayState>();
            var turnManagerMock = Substitute.For<ITurnManager>();
            turnManagerMock.Players.Returns(new System.Collections.Generic.List<Player> { player });
            stateMock.TurnManager.Returns(turnManagerMock);
            stateMock.Logger.Returns(_loggerMock);

            // Act
            var result = newManager.GetNextCommand(stateMock);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
        }

        [TestMethod]
        public void StopReplay_ExitsReplayMode()
        {
            // Arrange
            var replayJson = "{\"Seed\":123,\"Commands\":[]}";
            _manager.StartReplay(replayJson);
            Assert.IsTrue(_manager.IsReplaying);

            // Act
            _manager.StopReplay();

            // Assert
            Assert.IsFalse(_manager.IsReplaying);
        }

        #endregion

        #region Serialization Tests

        [TestMethod]
        public void GetRecordingJson_ReturnsValidJson()
        {
            // Arrange
            _manager.InitializeRecording(456);

            // Act
            string json = _manager.GetRecordingJson();

            // Assert
            Assert.IsNotNull(json);
            Assert.Contains("\"Seed\":456", json);
            Assert.Contains("\"Commands\"", json);
        }

        [TestMethod]
        public void RecordAndSerialize_RoundTrip_PreservesData()
        {
            // Arrange
            var player = new Player(PlayerColor.Red);
            var card = new Card("test_id", "Test", 1, CardAspect.Neutral, 0, 0, 0);
            player.Hand.Add(card);
            var command = new PlayCardCommand(card);

            // Act
            _manager.InitializeRecording(789);
            _manager.RecordCommand(command, player, 1);
            string json = _manager.GetRecordingJson();

            // Simulate new session
            var newManager = new ReplayManager(_loggerMock);
            newManager.StartReplay(json);
            var serializedBack = newManager.GetRecordingJson();

            // Assert
            Assert.AreEqual(json, serializedBack);
            Assert.AreEqual(789, newManager.Seed);
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsException()
        {
            try
            {
                new ReplayManager(null!);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        #endregion

        #region State Management Tests

        [TestMethod]
        public void IsReplaying_InitiallyFalse()
        {
            // Assert
            Assert.IsFalse(_manager.IsReplaying);
        }

        [TestMethod]
        public void Seed_DefaultsToZero()
        {
            // Assert
            Assert.AreEqual(0, _manager.Seed);
        }

        [TestMethod]
        public void Seed_UpdatedByInitializeRecording()
        {
            // Act
            _manager.InitializeRecording(999);

            // Assert
            Assert.AreEqual(999, _manager.Seed);
        }

        [TestMethod]
        public void Seed_UpdatedByStartReplay()
        {
            // Arrange
            var replayJson = "{\"Seed\":555,\"Commands\":[]}";

            // Act
            _manager.StartReplay(replayJson);

            // Assert
            Assert.AreEqual(555, _manager.Seed);
        }

        #endregion
    }
}
