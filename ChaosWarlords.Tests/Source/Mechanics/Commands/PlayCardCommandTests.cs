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
            var card = new CardBuilder().WithName("test").WithCost(3).WithAspect(CardAspect.Warlord).WithPower(1).WithInfluence(1).Build();
            var command = new PlayCardCommand(card);

            // Act
            command.Execute(mockState);

            // Assert
            mockState.Received(1).PlayCard(card);
        }
    }
}



