using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Interfaces;
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
