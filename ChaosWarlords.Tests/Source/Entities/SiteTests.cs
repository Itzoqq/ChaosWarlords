using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    public class SiteTests
    {
        private MapNode _node1 = null!, _node2 = null!, _node3 = null!;
        private Site _site = null!;

        [TestInitialize]
        public void Setup()
        {
            _node1 = new MapNode(1, new(0, 0), null);
            _node2 = new MapNode(2, new(0, 0), null);
            _node3 = new MapNode(3, new(0, 0), null);
            _site = new Site("Test Site", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            _site.AddNode(_node1);
            _site.AddNode(_node2);
            _site.AddNode(_node3);
        }

        [TestMethod]
        public void GetTroopCount_CountsCorrectly()
        {
            _node1.Occupant = PlayerColor.Red;
            _node2.Occupant = PlayerColor.Red;
            _node3.Occupant = PlayerColor.Blue;

            Assert.AreEqual(2, _site.GetTroopCount(PlayerColor.Red));
            Assert.AreEqual(1, _site.GetTroopCount(PlayerColor.Blue));
            Assert.AreEqual(0, _site.GetTroopCount(PlayerColor.Black));
        }

        [TestMethod]
        public void GetControllingPlayer_ReturnsPlayerWithMajority()
        {
            _node1.Occupant = PlayerColor.Red;
            _node2.Occupant = PlayerColor.Red;
            _node3.Occupant = PlayerColor.Blue;

            Assert.AreEqual(PlayerColor.Red, _site.GetControllingPlayer());
        }

        [TestMethod]
        public void GetControllingPlayer_ReturnsNoneOnTie()
        {
            _node1.Occupant = PlayerColor.Red;
            _node2.Occupant = PlayerColor.Blue;

            Assert.AreEqual(PlayerColor.None, _site.GetControllingPlayer());
        }

        [TestMethod]
        public void GetControllingPlayer_IgnoresNeutralTroopsForControl()
        {
            _node1.Occupant = PlayerColor.Red;
            _node2.Occupant = PlayerColor.Neutral;
            _node3.Occupant = PlayerColor.Neutral;

            Assert.AreEqual(PlayerColor.Red, _site.GetControllingPlayer(), "Player with most non-neutral troops should win.");
        }

        [TestMethod]
        public void SpyFunctionality_AddsAndRemovesSpiesCorrectly()
        {
            Assert.IsFalse(_site.HasSpy(PlayerColor.Blue));
            _site.AddSpy(PlayerColor.Blue);
            Assert.IsTrue(_site.HasSpy(PlayerColor.Blue));
            Assert.IsTrue(_site.RemoveSpy(PlayerColor.Blue));
            Assert.IsFalse(_site.HasSpy(PlayerColor.Blue));
            Assert.IsFalse(_site.RemoveSpy(PlayerColor.Blue), "Removing a non-existent spy should return false.");
        }
    }
}