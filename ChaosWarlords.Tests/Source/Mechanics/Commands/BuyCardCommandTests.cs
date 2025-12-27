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

using ChaosWarlords.Source.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    public class BuyCardCommandTests
    {
        [TestMethod]
        public void Execute_CallsTryBuyCardOnMarketManager()
        {
            // Arrange
            var mockState = Substitute.For<IGameplayState>();
            var mockMarketManager = Substitute.For<IMarketManager>();
            var mockTurnManager = Substitute.For<ITurnManager>();
            var mockPlayer = new Player(PlayerColor.Red);
            
            mockState.MarketManager.Returns(mockMarketManager);
            mockState.TurnManager.Returns(mockTurnManager);
            mockTurnManager.ActivePlayer.Returns(mockPlayer);
            
            var card = new Card("test", "Test Card", 3, CardAspect.Warlord, 1, 1, 0);
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(mockState);

            // Assert
            mockMarketManager.Received(1).TryBuyCard(mockPlayer, card);
        }
    }
}



