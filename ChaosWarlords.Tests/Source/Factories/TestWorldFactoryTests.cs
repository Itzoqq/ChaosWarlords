using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities;
using NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class TestWorldFactoryTests
    {
        [TestMethod]
        public void Build_CreatesValidWorldState_Headless()
        {
            var mockDb = Substitute.For<ICardDatabase>();
            mockDb.GetAllMarketCards().Returns(new List<Card>());

            var builder = new TestWorldFactory(mockDb, "dummy_map.json");
            var world = builder.Build();

            Assert.IsNotNull(world.TurnManager.ActivePlayer);
            Assert.IsNotNull(world.MapManager);
        }
    }
}
