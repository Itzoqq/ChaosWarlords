using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;
using ChaosWarlords.Source.Utilities;

using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]

    [TestCategory("Integration")]
    public class MatchFactoryTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new MatchFactory(mockDb, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var replayManager = Substitute.For<IReplayManager>();
            var result = builder.Build(replayManager);

            Assert.IsNotNull(result.TurnManager.ActivePlayer);
            Assert.IsNotNull(result.MapManager);
        }
    }
}



