using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Mechanics.Rules
{
    [TestClass]
    [TestCategory("Unit")]
    public class DevourStrategyFactoryTests
    {
        #region DevourFromHandStrategy Tests

        [TestMethod]
        public void DevourFromHandStrategy_WithEmptyHand_LogsWarningAndDoesNotCallActionSystem()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();
            player.Hand.Clear(); // Empty hand

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromHandStrategy();
            var sourceCard = TestData.Cards.PowerCard();
            var onComplete = Substitute.For<Action>();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, onComplete, false);

            // Assert
            mockLogger.Received(1).Log(
                Arg.Is<string>(s => s.Contains("Hand empty") && s.Contains("cannot Devour")),
                LogChannel.Warning);
            mockActionSystem.DidNotReceive().TryStartDevourHand(Arg.Any<Card>(), Arg.Any<Action>(), Arg.Any<bool>());
        }

        [TestMethod]
        public void DevourFromHandStrategy_WithNonEmptyHand_CallsTryStartDevourHand()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();
            player.Hand.Add(TestData.Cards.CheapCard());

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromHandStrategy();
            var sourceCard = TestData.Cards.PowerCard();
            var onComplete = Substitute.For<Action>();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, onComplete, false);

            // Assert
            mockActionSystem.Received(1).TryStartDevourHand(sourceCard, onComplete, false);
        }

        [TestMethod]
        public void DevourFromHandStrategy_WithDeferredExecution_PassesDeferFlagCorrectly()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();
            player.Hand.Add(TestData.Cards.CheapCard());

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromHandStrategy();
            var sourceCard = TestData.Cards.PowerCard();
            var onComplete = Substitute.For<Action>();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, onComplete, true);

            // Assert
            mockActionSystem.Received(1).TryStartDevourHand(sourceCard, onComplete, true);
        }

        [TestMethod]
        public void DevourFromHandStrategy_WithNullOnComplete_PassesNullCorrectly()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();
            player.Hand.Add(TestData.Cards.CheapCard());

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromHandStrategy();
            var sourceCard = TestData.Cards.PowerCard();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, null, false);

            // Assert
            mockActionSystem.Received(1).TryStartDevourHand(sourceCard, null, false);
        }

        #endregion

        #region DevourFromMarketStrategy Tests

        [TestMethod]
        public void DevourFromMarketStrategy_CallsTryStartDevourMarket()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromMarketStrategy();
            var sourceCard = TestData.Cards.PowerCard();
            var onComplete = Substitute.For<Action>();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, onComplete, false);

            // Assert
            mockActionSystem.Received(1).TryStartDevourMarket(sourceCard, onComplete, false);
        }

        [TestMethod]
        public void DevourFromMarketStrategy_WithDeferredExecution_PassesDeferFlagCorrectly()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromMarketStrategy();
            var sourceCard = TestData.Cards.PowerCard();
            var onComplete = Substitute.For<Action>();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, onComplete, true);

            // Assert
            mockActionSystem.Received(1).TryStartDevourMarket(sourceCard, onComplete, true);
        }

        [TestMethod]
        public void DevourFromMarketStrategy_WithNullOnComplete_PassesNullCorrectly()
        {
            // Arrange
            var mockLogger = Substitute.For<IGameLogger>();
            var mockActionSystem = Substitute.For<IActionSystem>();
            var player = new PlayerBuilder().Build();

            var context = CreateMockContext(mockActionSystem, player);
            var strategy = new DevourFromMarketStrategy();
            var sourceCard = TestData.Cards.PowerCard();

            // Act
            strategy.Execute(sourceCard, context, mockLogger, null, false);

            // Assert
            mockActionSystem.Received(1).TryStartDevourMarket(sourceCard, null, false);
        }

        #endregion

        #region DevourStrategyFactory Tests

        [TestMethod]
        public void GetStrategy_WithHandLocation_ReturnsHandStrategy()
        {
            // Act
            var strategy = DevourStrategyFactory.GetStrategy(CardLocation.Hand);

            // Assert
            Assert.IsInstanceOfType(strategy, typeof(DevourFromHandStrategy));
        }

        [TestMethod]
        public void GetStrategy_WithMarketLocation_ReturnsMarketStrategy()
        {
            // Act
            var strategy = DevourStrategyFactory.GetStrategy(CardLocation.Market);

            // Assert
            Assert.IsInstanceOfType(strategy, typeof(DevourFromMarketStrategy));
        }

        [TestMethod]
        public void GetStrategy_WithDeckLocation_ReturnsHandStrategyAsDefault()
        {
            // Act
            var strategy = DevourStrategyFactory.GetStrategy(CardLocation.Deck);

            // Assert
            Assert.IsInstanceOfType(strategy, typeof(DevourFromHandStrategy));
        }

        [TestMethod]
        public void GetStrategy_WithDiscardLocation_ReturnsHandStrategyAsDefault()
        {
            // Act
            var strategy = DevourStrategyFactory.GetStrategy(CardLocation.DiscardPile);

            // Assert
            Assert.IsInstanceOfType(strategy, typeof(DevourFromHandStrategy));
        }

        [TestMethod]
        public void GetStrategy_WithVoidLocation_ReturnsHandStrategyAsDefault()
        {
            // Act
            var strategy = DevourStrategyFactory.GetStrategy(CardLocation.Void);

            // Assert
            Assert.IsInstanceOfType(strategy, typeof(DevourFromHandStrategy));
        }

        [TestMethod]
        public void GetStrategy_ReturnsSingletonInstances()
        {
            // Act
            var strategy1 = DevourStrategyFactory.GetStrategy(CardLocation.Hand);
            var strategy2 = DevourStrategyFactory.GetStrategy(CardLocation.Hand);
            var strategy3 = DevourStrategyFactory.GetStrategy(CardLocation.Market);
            var strategy4 = DevourStrategyFactory.GetStrategy(CardLocation.Market);

            // Assert - Same instances should be returned
            Assert.AreSame(strategy1, strategy2);
            Assert.AreSame(strategy3, strategy4);
        }

        [TestMethod]
        public void GetStrategy_HandAndMarketStrategies_AreDifferentInstances()
        {
            // Act
            var handStrategy = DevourStrategyFactory.GetStrategy(CardLocation.Hand);
            var marketStrategy = DevourStrategyFactory.GetStrategy(CardLocation.Market);

            // Assert
            Assert.AreNotSame(handStrategy, marketStrategy);
        }

        #endregion

        #region Helper Methods

        private MatchContext CreateMockContext(IActionSystem actionSystem, Player player)
        {
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(player);
            
            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();
            var uiMediator = Substitute.For<IUIEventMediator>();
            var logger = Substitute.For<IGameLogger>();

            return new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                cardDb,
                playerState,
                uiMediator,
                logger,
                12345
            );
        }

        #endregion
    }
}
