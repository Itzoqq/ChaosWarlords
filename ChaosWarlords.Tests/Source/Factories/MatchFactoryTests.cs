using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;

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
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new MatchFactory(mockDb);
            var result = builder.Build();

            Assert.IsNotNull(result.TurnManager.ActivePlayer);
            Assert.IsNotNull(result.MapManager);
        }
    }
}



