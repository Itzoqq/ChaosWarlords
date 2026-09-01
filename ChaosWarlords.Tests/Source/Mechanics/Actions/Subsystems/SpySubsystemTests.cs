using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Mechanics.Actions.Subsystems;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Mechanics.Actions.Subsystems
{
    [TestClass]
    [TestCategory("Unit")]
    public class SpySubsystemTests
    {
        private SpySubsystem _subsystem = null!;
        private IMapManager _mapManager = null!;
        private ITurnManager _turnManager = null!;
        private IActionSystem _actionSystem = null!;
        private IGameLogger _logger = null!;
        private IPlayerStateManager _playerStateManager = null!;

        private Player _activePlayer = null!;
        private Site _site = null!;

        [TestInitialize]
        public void Setup()
        {
            _mapManager = Substitute.For<IMapManager>();
            _turnManager = Substitute.For<ITurnManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _logger = Substitute.For<IGameLogger>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();

            _activePlayer = new Player(PlayerColor.Red) { SpiesInBarracks = 5 };
            _activePlayer.AddPower(10);
            _turnManager.ActivePlayer.Returns(_activePlayer);

            _site = TestData.Sites.NeutralSite();
            _site.Id = 1;

            _subsystem = new SpySubsystem(_mapManager, _turnManager, _actionSystem, _logger, _playerStateManager);
        }

        [TestMethod]
        public void HandlePlaceSpy_ReturnsCommand_IfValid()
        {
            // Act
            var cmd = _subsystem.HandlePlaceSpy(_site, null);

            // Assert
            Assert.IsNotNull(cmd);
            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.PlaceSpyCommand));
        }

        [TestMethod]
        public void HandlePlaceSpy_ReturnsNull_IfAlreadyPresent()
        {
            _site.Spies.Add(PlayerColor.Red);
            var cmd = _subsystem.HandlePlaceSpy(_site, null);
            Assert.IsNull(cmd);
        }

        [TestMethod]
        public void HandleReturnSpyInitialClick_CallsNotifyFailure_IfTargetInvalid()
        {
            // Arrange
            _mapManager.GetEnemySpiesAtSite(_site, _activePlayer).Returns(new List<PlayerColor>()); // No spies

            // Act
            var cmd = _subsystem.HandleReturnSpyInitialClick(_site, null);

            // Assert
            Assert.IsNull(cmd);
            _actionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleReturnSpyInitialClick_ReturnsCommand_ForSingleSpy()
        {
            // Arrange
            _mapManager.GetEnemySpiesAtSite(_site, _activePlayer).Returns(new List<PlayerColor> { PlayerColor.Blue });

            // Act
            var cmd = _subsystem.HandleReturnSpyInitialClick(_site, null);

            // Assert
            Assert.IsNotNull(cmd);
            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.ResolveSpyCommand));
        }

        [TestMethod]
        public void HandleReturnSpyInitialClick_TransitionsToSelection_ForMultipleSpies()
        {
            // Arrange
            _mapManager.GetEnemySpiesAtSite(_site, _activePlayer).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Neutral });

            // Act
            var cmd = _subsystem.HandleReturnSpyInitialClick(_site, null);

            // Assert
            Assert.IsNull(cmd, "Should return null as it transitions state");
            _actionSystem.Received(1).TransitionToSpySelection(_site);
        }

        #region PerformSpyReturn Tests

        [TestMethod]
        public void PerformSpyReturn_Success_WithPowerCost_UsesPlayerStateManager()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);

            // Act
            var result = _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: null);

            // Assert
            Assert.IsTrue(result, "Should return true on success");
            _playerStateManager.Received(1).TrySpendPower(_activePlayer, GameConstants.ReturnSpyPowerCost);
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformSpyReturn_Success_WithCardPayment_DoesNotSpendPower()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);

            // Act
            var result = _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: "some-card-id");

            // Assert
            Assert.IsTrue(result, "Should return true on success");
            _playerStateManager.DidNotReceive().TrySpendPower(Arg.Any<Player>(), Arg.Any<int>());
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformSpyReturn_MapManagerFailure_ReturnsFalse()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(false);

            // Act
            var result = _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: null);

            // Assert
            Assert.IsFalse(result, "Should return false when MapManager fails");
            _actionSystem.Received(1).NotifyFailure("Map Manager failed to return spy.");
            _actionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void PerformSpyReturn_Failure_DoesNotSpendPower()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(false);

            // Act
            var result = _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: null);

            // Assert
            Assert.IsFalse(result);
            _playerStateManager.DidNotReceive().TrySpendPower(Arg.Any<Player>(), Arg.Any<int>());
        }

        [TestMethod]
        public void PerformSpyReturn_CompletesAction_OnSuccess()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);

            // Act
            _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: "card123");

            // Assert
            _actionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void PerformSpyReturn_NotifiesFailure_WithCorrectMessage()
        {
            // Arrange
            _mapManager.ReturnSpecificSpy(_site, _activePlayer, PlayerColor.Blue).Returns(false);

            // Act
            _subsystem.PerformSpyReturn(_site, PlayerColor.Blue, cardId: null);

            // Assert
            _actionSystem.Received(1).NotifyFailure("Map Manager failed to return spy.");
        }

        #endregion
    }
}
