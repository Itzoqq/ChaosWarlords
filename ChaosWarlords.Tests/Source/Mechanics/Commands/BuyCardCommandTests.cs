using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Utilities;

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
            var context = new MatchContextBuilder()
                .WithTurnManager(mockTurnManager)
                .WithMarketManager(mockMarketManager)
                .WithPlayerStateManager(mockStateManager)
                .Build();
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

        // Regression coverage for planning.txt TIER 1 (test hardening audit, 2026-09-01):
        // BuyCardCommand.Validate() used to be the ONE resource-gated command that didn't
        // check its own cost precondition - every sibling (AssassinateCommand's Power,
        // PlaceSpyCommand's SpiesInBarracks, SupplantCommand's TroopsInBarracks) enforces
        // its own requirement directly in Validate(). An insufficient-funds purchase used
        // to pass Validate() (advancing SequenceNumber, getting recorded) and rely entirely
        // on MarketManager.TryBuyCard's internal guard to silently no-op it.
        [TestMethod]
        public void Validate_WithSufficientInfluence_ReturnsTrue()
        {
            var player = TestData.Players.RedPlayer();
            player.AddInfluence(5);

            var marketManager = Substitute.For<IMarketManager>();
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.PowerCard(); // Cost 2
            marketManager.MarketRow.Returns(new List<ChaosWarlords.Source.Entities.Cards.Card> { card });

            var context = new MatchContextBuilder()
                .WithTurnManager(turnManager)
                .WithMarketManager(marketManager)
                .Build();

            var command = new BuyCardCommand(card);

            Assert.IsTrue(command.Validate(context));
        }

        [TestMethod]
        public void Validate_WithInsufficientInfluence_ReturnsFalse()
        {
            var player = new PlayerBuilder().WithColor(PlayerColor.Red).WithInfluence(1).Build();

            var marketManager = Substitute.For<IMarketManager>();
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);

            var card = TestData.Cards.PowerCard(); // Cost 2 - player only has 1.
            marketManager.MarketRow.Returns(new List<ChaosWarlords.Source.Entities.Cards.Card> { card });

            var context = new MatchContextBuilder()
                .WithTurnManager(turnManager)
                .WithMarketManager(marketManager)
                .Build();

            var command = new BuyCardCommand(card);

            Assert.IsFalse(command.Validate(context), "Insufficient Influence must be rejected by Validate(), not just silently no-op inside TryBuyCard.");
        }

        [TestMethod]
        public void Validate_CardNotInMarket_ReturnsFalse()
        {
            var player = TestData.Players.RedPlayer();
            player.AddInfluence(100); // Plenty of funds - the rejection must be about the target, not the cost.

            var marketManager = Substitute.For<IMarketManager>();
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);
            marketManager.MarketRow.Returns(new List<ChaosWarlords.Source.Entities.Cards.Card>()); // Empty market.

            var card = TestData.Cards.PowerCard();

            var context = new MatchContextBuilder()
                .WithTurnManager(turnManager)
                .WithMarketManager(marketManager)
                .Build();

            var command = new BuyCardCommand(card);

            Assert.IsFalse(command.Validate(context));
        }

        [TestMethod]
        public void Validate_WithInfluenceExactlyEqualToCost_ReturnsTrue()
        {
            // Boundary case: >= not >.
            var player = TestData.Players.RedPlayer();
            var card = TestData.Cards.PowerCard(); // Cost 2
            player.AddInfluence(card.Cost);

            var marketManager = Substitute.For<IMarketManager>();
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);
            marketManager.MarketRow.Returns(new List<ChaosWarlords.Source.Entities.Cards.Card> { card });

            var context = new MatchContextBuilder()
                .WithTurnManager(turnManager)
                .WithMarketManager(marketManager)
                .Build();

            var command = new BuyCardCommand(card);

            Assert.IsTrue(command.Validate(context));
        }
    }
}
