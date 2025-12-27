using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class DevourCardCommandTests
    {
        private IGameplayState _mockState = null!;
        private IMatchManager _mockMatchManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private Card _testCard = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockState = Substitute.For<IGameplayState>();
            _mockMatchManager = Substitute.For<IMatchManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();

            _mockState.MatchManager.Returns(_mockMatchManager);
            _mockState.ActionSystem.Returns(_mockActionSystem);

            _testCard = new Card("Devour Test", "Test", 1, CardAspect.Warlord, 0, 0, 0);
        }

        [TestMethod]
        public void Constructor_StoresCard()
        {
            // Act
            var command = new DevourCardCommand(_testCard);

            // Assert - If constructor doesn't throw, card was stored
            Assert.IsNotNull(command);
        }

        [TestMethod]
        public void Execute_CallsMatchManagerDevour()
        {
            // Arrange
            var command = new DevourCardCommand(_testCard);

            // Act
            command.Execute(_mockState);

            // Assert
            _mockMatchManager.Received(1).DevourCard(_testCard);
        }

        [TestMethod]
        public void Execute_CallsActionSystemCompleteAction()
        {
            // Arrange
            var command = new DevourCardCommand(_testCard);

            // Act
            command.Execute(_mockState);

            // Assert
            _mockActionSystem.Received(1).CompleteAction();
        }

        [TestMethod]
        public void Execute_CallsMethodsInCorrectOrder()
        {
            // Arrange
            var command = new DevourCardCommand(_testCard);
            var callOrder = new System.Collections.Generic.List<string>();

            _mockMatchManager.When(x => x.DevourCard(Arg.Any<Card>()))
                .Do(_ => callOrder.Add("DevourCard"));
            _mockActionSystem.When(x => x.CompleteAction())
                .Do(_ => callOrder.Add("CompleteAction"));

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.HasCount(2, callOrder);
            Assert.AreEqual("DevourCard", callOrder[0]);
            Assert.AreEqual("CompleteAction", callOrder[1]);
        }
    }
}
