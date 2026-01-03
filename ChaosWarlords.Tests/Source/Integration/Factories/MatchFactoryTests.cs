using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Factories;
using NSubstitute;
using ChaosWarlords.Source.Utilities;

using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;
using ChaosWarlords.Source.Entities.Actors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Integration.Factories
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

        [TestMethod]
        public void Verify_SeatIndex_IsStable_Deterministic()
        {
             var mockDb = Substitute.For<ICardDatabase>();
             mockDb.GetAllMarketCards(Arg.Any<IGameRandom>()).Returns(new List<Card>());
             mockDb.GetAllMarketCards(null).Returns(new List<Card>()); // Handle optional argument
             
             var factory = new MatchFactory(mockDb, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
             var replayManagerMock = Substitute.For<IReplayManager>();
             
             // Run 1
             var world1 = factory.Build(replayManagerMock, 555);
             var p1_red = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p1_blue = world1.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             // Run 2
             var world2 = factory.Build(replayManagerMock, 555);
             var p2_red = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
             var p2_blue = world2.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
             
             Assert.AreEqual(p1_red.SeatIndex, p2_red.SeatIndex);
             Assert.AreEqual(p1_blue.SeatIndex, p2_blue.SeatIndex);
             
             Assert.AreNotEqual(p1_red.SeatIndex, p1_blue.SeatIndex);
        }
    }
}



