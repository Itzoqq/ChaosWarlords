using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]

    [TestCategory("Unit")]
    public class PlayCardCommandTests
    {
        [TestMethod]
        public void Execute_CallsPlayCardOnState()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var card = TestData.Cards.AssassinCard();
            var command = new PlayCardCommand(card);

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).PlayCard(card);
        }

        [TestMethod]
        public void Execute_WithBypass_CallsMatchManagerPlayCard()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            // Corrected Namespace: Services, not Logic
            var mockMatchManager = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IMatchManager>();
            mockState.MatchManager.Returns(mockMatchManager);
            
            var card = TestData.Cards.AssassinCard();
            var command = new PlayCardCommand(card, true);

            // Act
            command.Execute(mockState);

            // Assert
            mockMatchManager.Received(1).PlayCard(card);
            mockState.DidNotReceive().PlayCard(card); // Key distinction
        }
    }
}



