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
        private IGameplayState _stateSub = null!;
        private IActionSystem _actionSub = null!;

        [TestInitialize]
        public void Setup()
        {
            GameLogger.Initialize();
            _stateSub = Substitute.For<IGameplayState>();
            _actionSub = Substitute.For<IActionSystem>();
            _stateSub.ActionSystem.Returns(_actionSub);
        }

        [TestMethod]
        public void Execute_WithPendingCard_ResolvesAndMovesCard()
        {
            // Arrange
            var card = new Card("test", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            _actionSub.PendingCard.Returns(card);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            // 1. Verify card effects are resolved
            _stateSub.Received(1).ResolveCardEffects(card);
            // 2. Verify card is moved to played pile
            _stateSub.Received(1).MoveCardToPlayed(card);
            // 3. Verify cleanup
            _actionSub.Received(1).CancelTargeting();
            _stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void Execute_NoPendingCard_SkipsCardLogic_ButResetsState()
        {
            // Arrange
            // Use (Card)null! to suppress CS8600.
            // We tell the compiler "Treat this null as a valid Card object" (even though it's null).
            // This allows NSubstitute to return null at runtime without the compiler warning.
            _actionSub.PendingCard.Returns((Card)null!);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            // 1. Ensure card methods were NOT called
            _stateSub.DidNotReceive().ResolveCardEffects(Arg.Any<Card>());
            _stateSub.DidNotReceive().MoveCardToPlayed(Arg.Any<Card>());

            // 2. Verify cleanup still happens
            _actionSub.Received(1).CancelTargeting();
            _stateSub.Received(1).SwitchToNormalMode();
        }
    }
}