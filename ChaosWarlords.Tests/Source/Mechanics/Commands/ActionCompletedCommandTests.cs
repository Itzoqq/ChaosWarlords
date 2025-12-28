using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class ActionCompletedCommandTests
    {
        private IGameplayState _stateSub = null!;
        private IActionSystem _actionSub = null!;
        private IMatchManager _matchManagerSub = null!; // New Mock

        [TestInitialize]
        public void Setup()
        {
            GameLogger.Initialize();

            // 1. Create Mocks
            _stateSub = Substitute.For<IGameplayState>();
            _actionSub = Substitute.For<IActionSystem>();
            _matchManagerSub = Substitute.For<IMatchManager>();

            // 2. Wire Mocks together
            _stateSub.ActionSystem.Returns(_actionSub);
            _stateSub.MatchManager.Returns(_matchManagerSub); // Important: Hook up the controller
        }

        [TestMethod]
        public void Execute_WithPendingCard_DelegatesToMatchManager()
        {
            // Arrange
            var card = new Card("test", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            _actionSub.PendingCard.Returns(card);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            // 1. Verify logic was delegated to the Controller (which handles effects + movement)
            _matchManagerSub.Received(1).PlayCard(card);

            // 2. Verify cleanup
            _actionSub.Received(1).CancelTargeting();
            _stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void Execute_NoPendingCard_SkipsControllerCall_ButResetsState()
        {
            // Arrange
            _actionSub.PendingCard.Returns((Card)null!);

            var command = new ActionCompletedCommand();

            // Act
            command.Execute(_stateSub);

            // Assert
            // 1. Verify Controller was NOT called
            _matchManagerSub.DidNotReceive().PlayCard(Arg.Any<Card>());

            // 2. Verify cleanup still happens
            _actionSub.Received(1).CancelTargeting();
            _stateSub.Received(1).SwitchToNormalMode();
        }
    }
}


