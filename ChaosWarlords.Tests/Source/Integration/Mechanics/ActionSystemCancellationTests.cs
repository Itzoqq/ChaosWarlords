using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    /// <summary>
    /// Integration tests for ActionSystem cancellation and validation behaviors.
    /// Tests ensure:
    /// 1. Cards return to hand when targeting is cancelled (right-click)
    /// 2. Return Spy validates targets before starting targeting mode
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class ActionSystemCancellationTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private ActionSystem _actionSystem = null!;
        private IMarketManager _marketManager = null!;
        private IMapManager _mapManager = null!;
        private IMatchManager _matchManager = null!;
        private IGameLogger _logger = null!;
        private PlayerStateManager _playerStateManager = null!;
        private IMarketStateManager _marketStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(_player);

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _matchManager = Substitute.For<IMatchManager>();
            _marketStateManager = Substitute.For<IMarketStateManager>();
            _playerStateManager = new PlayerStateManager(_logger);

            // Create ActionSystem first (before MatchContext)
            _actionSystem = new ActionSystem(turnManager, _mapManager, _logger);
            _actionSystem.SetMatchManager(_matchManager);
            _actionSystem.SetMarketManager(_marketManager);
            _actionSystem.SetMarketStateManager(_marketStateManager);
            _actionSystem.SetPlayerStateManager(_playerStateManager);

            var cardDb = Substitute.For<ICardDatabase>();
            var uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                cardDb,
                _playerStateManager,
                uiMediator,
                _logger
            );
        }

        #region Targeting Cancellation Tests

        [TestMethod]
        public void CancelTargeting_WithPendingCardInPlayedPile_ReturnsCardToHand()
        {
            // Arrange
            var card = new Card("test_card", "Test Card", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);
            
            // Simulate card being moved to PlayedCards during targeting
            _player.Hand.Remove(card);
            _player.PlayedCards.Add(card);
            card.Location = CardLocation.Played;
            
            // Set up ActionSystem state as if targeting was initiated
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.Contains(card, _player.Hand, "Card should be returned to Hand");
            Assert.DoesNotContain(card, _player.PlayedCards, "Card should be removed from PlayedCards");
            Assert.AreEqual(CardLocation.Hand, card.Location, "Card location should be Hand");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "ActionSystem should return to Normal state");
        }

        [TestMethod]
        public void CancelTargeting_WithPendingCardInHand_DoesNotDuplicate()
        {
            // Arrange
            var card = new Card("test_card", "Test Card", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);
            card.Location = CardLocation.Hand;
            
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);
            int initialHandCount = _player.Hand.Count;

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.HasCount(initialHandCount, _player.Hand, "Hand count should not change");
            Assert.Contains(card, _player.Hand, "Card should still be in Hand");
        }

        [TestMethod]
        public void CancelTargeting_WithNoPendingCard_DoesNotThrow()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingReturnSpy);

            // Act & Assert - should not throw
            _actionSystem.CancelTargeting();
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        #endregion

        #region Return Spy Validation Tests

        [TestMethod]
        public void TryStartReturnSpy_WithNoValidTargets_DoesNotStartTargeting()
        {
            // Arrange
            _player.Power = 10; // Sufficient power
            _mapManager.HasValidReturnSpyTarget(_player).Returns(false);

            // Act
            _actionSystem.TryStartReturnSpy();

            // Assert
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, 
                "Should not enter targeting state when no valid targets exist");
        }

        [TestMethod]
        public void TryStartReturnSpy_WithValidTargets_StartsTargeting()
        {
            // Arrange
            _player.Power = 10; // Sufficient power
            _mapManager.HasValidReturnSpyTarget(_player).Returns(true);

            // Act
            _actionSystem.TryStartReturnSpy();

            // Assert
            Assert.AreEqual(ActionState.TargetingReturnSpy, _actionSystem.CurrentState,
                "Should enter targeting state when valid targets exist");
        }

        [TestMethod]
        public void TryStartReturnSpy_WithInsufficientPower_DoesNotStartTargeting()
        {
            // Arrange
            _player.Power = 0; // Insufficient power
            _mapManager.HasValidReturnSpyTarget(_player).Returns(true);

            // Act
            _actionSystem.TryStartReturnSpy();

            // Assert
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState,
                "Should not enter targeting state when power is insufficient");
        }

        #endregion
    }
}
