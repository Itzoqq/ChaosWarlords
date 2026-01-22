using NSubstitute;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Contexts; // Needed for EffectContext
using ChaosWarlords.Source.Core.Interfaces.Services;
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
            Utilities.TestLogger.Initialize();
            _logger = Utilities.TestLogger.Instance;

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
            _player.AddToHand(card);

            // Simulate card being moved to PlayedCards during targeting
            _player.RemoveFromHand(card);
            _player.AddToPlayed(card);
            card.Location = CardLocation.Played;

            // Set up ActionSystem state as if targeting was initiated
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            CollectionAssert.Contains(_player.Hand.ToList(), card, "Card should be returned to Hand");
            CollectionAssert.DoesNotContain(_player.PlayedCards.ToList(), card, "Card should be removed from PlayedCards");
            Assert.AreEqual(CardLocation.Hand, card.Location, "Card location should be Hand");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "ActionSystem should return to Normal state");
        }

        [TestMethod]
        public void CancelTargeting_WithPendingCardInHand_DoesNotDuplicate()
        {
            // Arrange
            var card = new Card("test_card", "Test Card", 0, CardAspect.Neutral, 0, 0, 0);
            _player.AddToHand(card);
            card.Location = CardLocation.Hand;

            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, card);
            int initialHandCount = _player.Hand.Count;

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.HasCount(initialHandCount, _player.Hand, "Hand count should not change");
            CollectionAssert.Contains(_player.Hand.ToList(), card, "Card should still be in Hand");
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

        [TestMethod]
        public void CancelTargeting_ClearsExecutionStack()
        {
            // Arrange
            var card = new Card("spy_master", "Spy Master", 0, CardAspect.Neutral, 0, 0, 0);
            var effect = new CardEffect(EffectType.PlaceSpy, 1);

            // Push an effect to the stack (simulating CardEffectProcessor behavior)
            var context = new EffectContext(
                ActionState.TargetingPlaceSpy,
                card,
                true, // Requires Input
                "Place Spy",
                (bool success) => { }, // Dummy callback with explicit type
                effect
            );
            _actionSystem.PushEffect(context);

            // Process stack to enter targeting state (and consume the item - wait, ProcessStack Peeks, doesn't Pop until Resolved)
            _actionSystem.ProcessStack();

            Assert.HasCount(1, _actionSystem.ExecutionStack, "Stack should have 1 item before cancellation");
            Assert.AreEqual(ActionState.TargetingPlaceSpy, _actionSystem.CurrentState, "State should be Targeting");

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.IsEmpty(_actionSystem.ExecutionStack, "Stack should be empty after cancellation");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState, "State should return to Normal");
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
        public void CancelTargeting_WithMultipleEffects_ClearsAllEffectsForCard()
        {
            // Arrange
            // Create a card with two effects: 
            // 1. Assassinate (Blocking/Targeting)
            // 2. Gain Resource (Automatic/Focus)
            var card = new Card("shadow_blade", "Shadow Blade", 0, CardAspect.Neutral, 0, 0, 0);

            // Blocking Effect (Top of Stack)
            var effect1 = new CardEffect(EffectType.Assassinate, 1);
            var context1 = new EffectContext(
                ActionState.TargetingAssassinate,
                card,
                true, // Requires Input
                "Assassinate",
                (bool s) => { },
                effect1
            );

            // Automatic Effect (Bottom of Stack)
            var effect2 = new CardEffect(EffectType.GainResource, 1);
            var context2 = new EffectContext(
                ActionState.Normal,
                card,
                false, // No Input
                "Gain Power",
                (bool s) => { if (s) Assert.Fail("Zombie Effect Executed Successfully! This should have been cancelled/skipped."); },
                effect2
            );

            // Push in reverse order (as CardEffectProcessor does)
            _actionSystem.PushEffect(context2); // Bottom
            _actionSystem.PushEffect(context1); // Top

            // Validate Stack State
            Assert.HasCount(2, _actionSystem.ExecutionStack);
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);

            // Start Processing (Will stop at Assassinate)
            _actionSystem.ProcessStack();

            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
            Assert.HasCount(2, _actionSystem.ExecutionStack, "Both effects should be on stack");

            // Act
            _actionSystem.CancelTargeting();

            // Assert
            Assert.IsEmpty(_actionSystem.ExecutionStack, "Stack should be empty after cancellation. All card effects should be cleared.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
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
