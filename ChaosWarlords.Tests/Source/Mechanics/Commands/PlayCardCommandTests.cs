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
    }
}



