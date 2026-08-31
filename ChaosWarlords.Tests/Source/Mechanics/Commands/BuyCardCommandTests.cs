using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Interfaces.Input;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    [TestClass]
    [TestCategory("Unit")]
    public class BuyCardCommandTests
    {
        [TestMethod]
        public void Execute_CallsTryBuyCardOnMarketManager()
        {
            // Arrange
            // 1. Setup TestGameplayState with basic mocks
            var stateFake = new TestGameplayState();

            var mockMarketManager = Substitute.For<IMarketManager>();
            var mockTurnManager = Substitute.For<ITurnManager>();
            var mockInputManager = Substitute.For<IInputManager>();
            var mockPlayer = TestData.Players.RedPlayer();
            var mockStateManager = Substitute.For<IPlayerStateManager>();

            // Setup Dependencies
            stateFake.MarketManager = mockMarketManager;
            stateFake.TurnManager = mockTurnManager;
            stateFake.InputManager = mockInputManager;

            mockTurnManager.ActivePlayer.Returns(mockPlayer);

            // 2. Setup MatchContext (because BuyCardCommand might access state.MatchContext.MarketManager)
            var context = new MatchContext(
                mockTurnManager,
                Substitute.For<IMapManager>(),
                mockMarketManager,
                Substitute.For<IActionSystem>(),
                Substitute.For<ICardDatabase>(),
                mockStateManager,
                null,
                Utilities.TestLogger.Instance
            );
            stateFake.MatchContext = context;

            var card = TestData.Cards.PowerCard();
            mockMarketManager.MarketRow.Returns(new List<ChaosWarlords.Source.Entities.Cards.Card> { card });
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(stateFake.MatchContext);

            // Assert
            // Verify the command delegation to the underlying manager
            mockMarketManager.Received(1).TryBuyCard(mockPlayer, card, mockStateManager);
        }
    }
}
