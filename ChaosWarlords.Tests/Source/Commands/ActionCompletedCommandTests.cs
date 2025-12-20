using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class ActionCompletedCommandTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Ensure logger is ready (prevents static crashes)
            GameLogger.Initialize();
        }

        [TestMethod]
        public void ActionCompleted_WithPendingCard_FinalizesCard_AndResetsState()
        {
            // 1. Arrange: Create dynamic mocks (no manual classes needed)
            var actionSub = Substitute.For<IActionSystem>();
            var stateSub = Substitute.For<IGameplayState>();

            // Link the systems: When the Command asks State for the ActionSystem, return our mock
            stateSub.ActionSystem.Returns(actionSub);

            // Setup Data: Define what 'PendingCard' returns
            var card = new Card("test_id", "Test Card", 0, CardAspect.Neutral, 0, 0, 0);
            actionSub.PendingCard.Returns(card);

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert: Verify interactions using .Received()

            // It MUST resolve effects with the specific card we set up
            stateSub.Received(1).ResolveCardEffects(card);

            // It MUST move card to discard pile
            stateSub.Received(1).MoveCardToPlayed(card);

            // It MUST reset the UI state
            actionSub.Received(1).CancelTargeting();
            stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void ActionCompleted_NoPendingCard_JustResetsState()
        {
            // 1. Arrange
            var actionSub = Substitute.For<IActionSystem>();
            var stateSub = Substitute.For<IGameplayState>();

            stateSub.ActionSystem.Returns(actionSub);

            // Case: No card is pending (return null)
            actionSub.PendingCard.Returns((Card?)null);

            var command = new ActionCompletedCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert

            // Verify specific methods were NOT called
            // We use Arg.Any<Card>() to say "It shouldn't be called with ANY card"
            stateSub.DidNotReceive().ResolveCardEffects(Arg.Any<Card>());
            stateSub.DidNotReceive().MoveCardToPlayed(Arg.Any<Card>());

            // Still needs to reset state
            actionSub.Received(1).CancelTargeting();
            stateSub.Received(1).SwitchToNormalMode();
        }
    }
}