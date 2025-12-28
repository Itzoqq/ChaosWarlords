using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class PlayCardCommandTests
    {
        [TestMethod]
        public void Execute_CallsPlayCardOnState()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var card = new Card("test", "Test Card", 3, CardAspect.Warlord, 1, 1, 0);
            var command = new PlayCardCommand(card);

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).PlayCard(card);
        }
    }
}



