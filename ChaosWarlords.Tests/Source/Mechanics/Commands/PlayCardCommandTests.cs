using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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



