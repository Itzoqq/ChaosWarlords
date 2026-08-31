using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Tests.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests
{
    [TestClass]
    [TestCategory("Unit")]
    public class MatchContextBuilderTests
    {
        [TestMethod]
        public void Build_WithNoOverrides_ProducesUsableContextWithDefaults()
        {
            var context = new MatchContextBuilder().Build();

            Assert.IsNotNull(context.TurnManager);
            Assert.IsNotNull(context.MapManager);
            Assert.IsNotNull(context.MarketManager);
            Assert.IsNotNull(context.ActionSystem);
            Assert.IsNotNull(context.CardDatabase);
            Assert.IsNotNull(context.PlayerStateManager);
            Assert.IsNotNull(context.Logger);
            Assert.AreEqual(12345, context.Seed);
        }

        [TestMethod]
        public void WithTurnManager_OverridesDefault()
        {
            var turnManager = Substitute.For<ITurnManager>();

            var context = new MatchContextBuilder().WithTurnManager(turnManager).Build();

            Assert.AreSame(turnManager, context.TurnManager);
        }

        [TestMethod]
        public void WithMapManager_OverridesDefault()
        {
            var mapManager = Substitute.For<IMapManager>();

            var context = new MatchContextBuilder().WithMapManager(mapManager).Build();

            Assert.AreSame(mapManager, context.MapManager);
        }

        [TestMethod]
        public void WithMarketManager_OverridesDefault()
        {
            var marketManager = Substitute.For<IMarketManager>();

            var context = new MatchContextBuilder().WithMarketManager(marketManager).Build();

            Assert.AreSame(marketManager, context.MarketManager);
        }

        [TestMethod]
        public void WithActionSystem_OverridesDefault()
        {
            var actionSystem = Substitute.For<IActionSystem>();

            var context = new MatchContextBuilder().WithActionSystem(actionSystem).Build();

            Assert.AreSame(actionSystem, context.ActionSystem);
        }

        [TestMethod]
        public void WithCardDatabase_OverridesDefault()
        {
            var cardDatabase = Substitute.For<ICardDatabase>();

            var context = new MatchContextBuilder().WithCardDatabase(cardDatabase).Build();

            Assert.AreSame(cardDatabase, context.CardDatabase);
        }

        [TestMethod]
        public void WithPlayerStateManager_OverridesDefault()
        {
            var playerStateManager = Substitute.For<IPlayerStateManager>();

            var context = new MatchContextBuilder().WithPlayerStateManager(playerStateManager).Build();

            Assert.AreSame(playerStateManager, context.PlayerStateManager);
        }

        [TestMethod]
        public void WithLogger_OverridesDefault()
        {
            var logger = Substitute.For<IGameLogger>();

            var context = new MatchContextBuilder().WithLogger(logger).Build();

            Assert.AreSame(logger, context.Logger);
        }

        [TestMethod]
        public void WithSeed_OverridesDefault()
        {
            var context = new MatchContextBuilder().WithSeed(999).Build();

            Assert.AreEqual(999, context.Seed);
        }

        [TestMethod]
        public void Build_ChainedOverrides_AllApply()
        {
            var turnManager = Substitute.For<ITurnManager>();
            var logger = Substitute.For<IGameLogger>();

            var context = new MatchContextBuilder()
                .WithTurnManager(turnManager)
                .WithLogger(logger)
                .WithSeed(42)
                .Build();

            Assert.AreSame(turnManager, context.TurnManager);
            Assert.AreSame(logger, context.Logger);
            Assert.AreEqual(42, context.Seed);
        }
    }
}
