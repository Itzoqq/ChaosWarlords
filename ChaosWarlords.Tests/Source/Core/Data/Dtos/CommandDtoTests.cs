using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Data.Dtos;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Source.Core.Data.Dtos
{
    [TestClass]
    [TestCategory("Unit")]
    public class CommandDtoTests
    {
        [TestMethod]
        public void ReplayDataDto_Properties_AreSetCorrectly()
        {
            var dto = new ReplayDataDto
            {
                Seed = 12345,
                Commands = new List<GameCommandDto>()
            };

            Assert.AreEqual(12345, dto.Seed);
            Assert.IsNotNull(dto.Commands);
        }

        [TestMethod]
        public void PlayCardCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new PlayCardCommandDto { CardId = "c1", HandIdx = 2, Seq = 1, Seat = 0 };
            Assert.AreEqual("c1", dto.CardId);
            Assert.AreEqual(2, dto.HandIdx);
            Assert.AreEqual(1, dto.Seq);
            Assert.AreEqual(0, dto.Seat);
        }

        [TestMethod]
        public void BuyCardCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new BuyCardCommandDto { CardId = "c2", Seq = 2 };
            Assert.AreEqual("c2", dto.CardId);
            Assert.AreEqual(2, dto.Seq);
        }

        [TestMethod]
        public void DeployTroopCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new DeployTroopCommandDto { NodeId = 10, Seq = 3 };
            Assert.AreEqual(10, dto.NodeId);
            Assert.AreEqual(3, dto.Seq);
        }

        [TestMethod]
        public void DevourCardCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new DevourCardCommandDto { CardId = "c3", HandIdx = 0, Seq = 4 };
            Assert.AreEqual("c3", dto.CardId);
            Assert.AreEqual(0, dto.HandIdx);
            Assert.AreEqual(4, dto.Seq);
        }

        [TestMethod]
        public void EndTurnCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new EndTurnCommandDto { Seq = 5 };
            Assert.AreEqual(5, dto.Seq);
        }

        [TestMethod]
        public void ResolveSpyCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new ResolveSpyCommandDto { SiteId = 5, Color = "Red", CardId = "spy_card" };
            Assert.AreEqual(5, dto.SiteId);
            Assert.AreEqual("Red", dto.Color);
            Assert.AreEqual("spy_card", dto.CardId);
        }

        [TestMethod]
        public void AssassinateCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new AssassinateCommandDto { NodeId = 99, CardId = "killer" };
            Assert.AreEqual(99, dto.NodeId);
            Assert.AreEqual("killer", dto.CardId);
        }

        [TestMethod]
        public void ReturnTroopCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new ReturnTroopCommandDto { NodeId = 1, CardId = "bouncer" };
            Assert.AreEqual(1, dto.NodeId);
            Assert.AreEqual("bouncer", dto.CardId);
        }

        [TestMethod]
        public void SupplantCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new SupplantCommandDto { NodeId = 2, CardId = "replacer" };
            Assert.AreEqual(2, dto.NodeId);
            Assert.AreEqual("replacer", dto.CardId);
        }

        [TestMethod]
        public void PlaceSpyCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new PlaceSpyCommandDto { SiteId = 3, CardId = "spy" };
            Assert.AreEqual(3, dto.SiteId);
            Assert.AreEqual("spy", dto.CardId);
        }

        [TestMethod]
        public void MoveTroopCommandDto_Properties_AreSetCorrectly()
        {
            var dto = new MoveTroopCommandDto { SrcId = 1, DestId = 2, CardId = "mover" };
            Assert.AreEqual(1, dto.SrcId);
            Assert.AreEqual(2, dto.DestId);
            Assert.AreEqual("mover", dto.CardId);
        }

        [TestMethod]
        public void OtherCommands_Properties_AreSetCorrectly()
        {
            Assert.IsNotNull(new CancelActionCommandDto());
            Assert.IsNotNull(new ToggleMarketCommandDto());
            Assert.IsNotNull(new SwitchModeCommandDto());
            Assert.IsNotNull(new StartAssassinateCommandDto());
            Assert.IsNotNull(new StartReturnSpyCommandDto());
            Assert.IsNotNull(new ActionCompletedCommandDto());
        }
    }
}
