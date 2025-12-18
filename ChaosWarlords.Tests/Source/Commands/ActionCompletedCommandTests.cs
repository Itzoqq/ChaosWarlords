using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class ActionCompletedCommandTests
    {
        // ------------------------------------------------------------------------
        // 2. UNIT TESTS
        // ------------------------------------------------------------------------

        [TestInitialize]
        public void Setup()
        {
            // Ensure logger is ready (prevents static crashes)
            GameLogger.Initialize();
        }

        [TestMethod]
        public void ActionCompleted_WithPendingCard_FinalizesCard_AndResetsState()
        {
            // 1. Arrange
            var mockAction = new MockActionSystem();
            var mockState = new MockGameplayState(mockAction);

            var card = new Card("test_id", "Test Card", 0, CardAspect.Neutral, 0, 0);
            mockAction.PendingCard = card;

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(mockState);

            // 3. Assert
            // It MUST resolve effects (gain power, etc.)
            Assert.IsTrue(mockState.ResolveCardEffectsCalled, "Should resolve card effects.");
            Assert.AreEqual(card, mockState.LastResolvedCard);

            // It MUST move card to discard pile
            Assert.IsTrue(mockState.MoveCardToPlayedCalled, "Should move card to played pile.");
            Assert.AreEqual(card, mockState.LastMovedCard);

            // It MUST reset the UI state
            Assert.IsTrue(mockAction.CancelTargetingCalled, "Should cancel targeting on the backend.");
            Assert.IsTrue(mockState.SwitchToNormalModeCalled, "Should switch Input Mode back to Normal.");
        }

        [TestMethod]
        public void ActionCompleted_NoPendingCard_JustResetsState()
        {
            // 1. Arrange
            var mockAction = new MockActionSystem();
            var mockState = new MockGameplayState(mockAction);

            // Case: Completing a pure map action (e.g. Return Spy) that didn't involve a card
            mockAction.PendingCard = null;

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(mockState);

            // 3. Assert
            Assert.IsFalse(mockState.ResolveCardEffectsCalled, "Should NOT resolve effects if no card pending.");
            Assert.IsFalse(mockState.MoveCardToPlayedCalled, "Should NOT move card if no card pending.");

            // Still needs to reset state
            Assert.IsTrue(mockAction.CancelTargetingCalled);
            Assert.IsTrue(mockState.SwitchToNormalModeCalled);
        }
    }
}