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
using ChaosWarlords.Source.Contexts;

using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
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
            
            var mockStateManager = Substitute.For<IPlayerStateManager>();
            
            // Mock MatchContext returning the StateManager
            // Since MatchContext is not an interface, we can mock it by putting it in the state.
            // But IGameplayState.MatchContext returns the Concrete class MatchContext.
            // We need to construct a real MatchContext context with Mocks, or Mock the StateManager property getter if it's virtual (it's not).
            // Actually, we are using NSubstitute on the INTERFACE IGameplayState. 
            // The interface IGameplayState has 'MatchContext MatchContext { get; }' -> returning the class.
            
            // We can just create a dummy context with our mock state manager.
            var context = new MatchContext(
                mockTurnManager,
                Substitute.For<IMapManager>(),
                mockMarketManager,
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                mockStateManager
            );
            
            mockState.MatchContext.Returns(context);
            
            var card = new Card("test", "Test Card", 3, CardAspect.Warlord, 1, 1, 0);
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(mockState);

            // Assert
            mockMarketManager.Received(1).TryBuyCard(mockPlayer, card, mockStateManager);
        }
    }
}



