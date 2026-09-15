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

        #region Empty-barracks return-then-place (rulebook p.12)

        [TestMethod]
        public void HandlePlaceSpy_EmptyBarracks_ClickOnSiteWithOwnSpy_ReturnsReturnSpyToPlaceCommand()
        {
            _activePlayer.SpiesInBarracks = 0;
            _site.Spies.Add(PlayerColor.Red);

            var cmd = _subsystem.HandlePlaceSpy(_site, null);

            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.ReturnSpyToPlaceCommand));
            var typed = (ChaosWarlords.Source.Commands.ReturnSpyToPlaceCommand)cmd!;
            Assert.AreEqual(_site.Id, typed.TargetSiteId);
        }

        [TestMethod]
        public void HandlePlaceSpy_EmptyBarracks_ClickOnSiteWithoutOwnSpy_ReturnsNull()
        {
            // Nothing to return here, and nothing to place either (barracks empty) - the click
            // is simply invalid, same as any other mistargeted click.
            _activePlayer.SpiesInBarracks = 0;

            var cmd = _subsystem.HandlePlaceSpy(_site, null);

            Assert.IsNull(cmd);
        }

        #endregion

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

        #region HandleReturnUnitOrSpySite (EffectType.ReturnUnitOrSpy - Intellect Devourer)

        [TestMethod]
        public void HandleReturnUnitOrSpySite_NullSite_ReturnsNull()
        {
            var cmd = _subsystem.HandleReturnUnitOrSpySite(null!, null);

            Assert.IsNull(cmd);
        }

        [TestMethod]
        public void HandleReturnUnitOrSpySite_NoEligibleSpies_NotifiesFailureAndReturnsNull()
        {
            _mapManager.GetAllSpiesAtSite(_site).Returns(new List<PlayerColor>());

            var cmd = _subsystem.HandleReturnUnitOrSpySite(_site, null);

            Assert.IsNull(cmd);
            _actionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void HandleReturnUnitOrSpySite_ExactlyOneEligibleSpy_ReturnsReturnAnySpyCommand()
        {
            _mapManager.GetAllSpiesAtSite(_site).Returns(new List<PlayerColor> { PlayerColor.Blue });
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);

            var cmd = _subsystem.HandleReturnUnitOrSpySite(_site, "intellect_devourer_abc");

            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.ReturnAnySpyCommand));
            var typed = (ChaosWarlords.Source.Commands.ReturnAnySpyCommand)cmd!;
            Assert.AreEqual(PlayerColor.Blue, typed.SpyColor);
            Assert.AreEqual("intellect_devourer_abc", typed.CardId);
        }

        [TestMethod]
        public void HandleReturnUnitOrSpySite_OneEligibleAndOneIneligibleSpy_ResolvesToTheEligibleOne()
        {
            // The site physically has 2 spies, but only Blue is actually returnable right now
            // (e.g. Red has no Presence to return the other enemy's) - not ambiguous.
            _mapManager.GetAllSpiesAtSite(_site).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Orange });
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Orange).Returns(false);

            var cmd = _subsystem.HandleReturnUnitOrSpySite(_site, null);

            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.ReturnAnySpyCommand));
            Assert.AreEqual(PlayerColor.Blue, ((ChaosWarlords.Source.Commands.ReturnAnySpyCommand)cmd!).SpyColor);
        }

        [TestMethod]
        public void HandleReturnUnitOrSpySite_TwoEligibleSpies_TransitionsToSpySelection()
        {
            // 2+ simultaneously eligible spies now disambiguate via the same
            // SelectingSpyToReturn sub-state the base "Return an enemy spy" action already has,
            // instead of silently reporting no valid target. See planning.txt TIER 1 item 16.
            _mapManager.GetAllSpiesAtSite(_site).Returns(new List<PlayerColor> { PlayerColor.Blue, PlayerColor.Orange });
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Blue).Returns(true);
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Orange).Returns(true);

            var cmd = _subsystem.HandleReturnUnitOrSpySite(_site, null);

            Assert.IsNull(cmd, "Should return null as it transitions state");
            _actionSystem.Received(1).TransitionToSpySelection(_site);
            _actionSystem.DidNotReceive().NotifyFailure(Arg.Any<string>());
        }

        #endregion

        #region FinalizeAnySpyReturn (EffectType.ReturnUnitOrSpy's disambiguation follow-up)

        [TestMethod]
        public void FinalizeAnySpyReturn_EligibleColor_ReturnsReturnAnySpyCommand_WithSelectedColorAndCardId()
        {
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Orange, false).Returns(true);

            var cmd = _subsystem.FinalizeAnySpyReturn(PlayerColor.Orange, _site, "intellect_devourer_abc");

            Assert.IsInstanceOfType(cmd, typeof(ChaosWarlords.Source.Commands.ReturnAnySpyCommand));
            var typed = (ChaosWarlords.Source.Commands.ReturnAnySpyCommand)cmd!;
            Assert.AreEqual(_site.Id, typed.TargetSiteId);
            Assert.AreEqual(PlayerColor.Orange, typed.SpyColor);
            Assert.AreEqual("intellect_devourer_abc", typed.CardId);
        }

        [TestMethod]
        public void FinalizeAnySpyReturn_NullPendingSite_ReturnsNull()
        {
            var cmd = _subsystem.FinalizeAnySpyReturn(PlayerColor.Orange, null!, null);

            Assert.IsNull(cmd);
        }

        [TestMethod]
        public void FinalizeAnySpyReturn_IneligibleColor_NotifiesFailureAndReturnsNull()
        {
            // Adversarial/misclick: the shared spy-selection UI can render a button for a color
            // that isn't actually eligible right now (e.g. the active player's own spy under a
            // ReturnEnemyOnly effect) - must fail loudly via NotifyFailure, not silently build a
            // command that would only fail later at ReturnAnySpyCommand.Validate() with no
            // player-visible feedback. See planning.txt TIER 1 item 16.
            _mapManager.CanReturnAnySpy(_site, _activePlayer, PlayerColor.Orange, false).Returns(false);

            var cmd = _subsystem.FinalizeAnySpyReturn(PlayerColor.Orange, _site, null);

            Assert.IsNull(cmd);
            _actionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        [TestMethod]
        public void FinalizeAnySpyReturn_IneligibleUnderReturnEnemyOnly_NotifiesFailureAndReturnsNull()
        {
            // The active player's own spy is never eligible under CardEffect.ReturnEnemyOnly
            // (High Priest of Myrkul) - re-derived from CurrentSourceEffect, not trusted from
            // the caller.
            _actionSystem.CurrentSourceEffect.Returns(new ChaosWarlords.Source.Entities.Cards.CardEffect(EffectType.ReturnUnitOrSpy, 1) { ReturnEnemyOnly = true });
            _mapManager.CanReturnAnySpy(_site, _activePlayer, _activePlayer.Color, true).Returns(false);

            var cmd = _subsystem.FinalizeAnySpyReturn(_activePlayer.Color, _site, "high_priest_of_myrkul_abc");

            Assert.IsNull(cmd);
            _actionSystem.Received(1).NotifyFailure(Arg.Any<string>());
        }

        #endregion
    }
}
