using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Entities
{
    [TestClass]
    [TestCategory("Unit")]
    public class SiteTests
    {
        private MapNode _node1 = null!, _node2 = null!, _node3 = null!;
        private Site _site = null!;

        [TestInitialize]
        public void Setup()
        {
            // Removed null texture arg
            _node1 = TestData.MapNodes.Node1();
            _node2 = TestData.MapNodes.Node2();
            _node3 = TestData.MapNodes.Node3();
            _site = TestData.Sites.NeutralSite();
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

        [TestMethod]
        public void HasTroop_ReturnsTrue_IfAnyNodeOccupiedByColor()
        {
            // Arrange
            _node1.Occupant = PlayerColor.None;
            _node2.Occupant = PlayerColor.Red; // One node occupied
            _node3.Occupant = PlayerColor.Blue;

            // Act & Assert
            Assert.IsTrue(_site.HasTroop(PlayerColor.Red), "Should find Red troop.");
            Assert.IsTrue(_site.HasTroop(PlayerColor.Blue), "Should find Blue troop.");
            Assert.IsFalse(_site.HasTroop(PlayerColor.Orange), "Should NOT find Orange troop.");
        }
    }
}


