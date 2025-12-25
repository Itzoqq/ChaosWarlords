using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class SiteControlSystemTests
    {
        private SiteControlSystem _system = null!;
        private Player _player1 = null!;
        private Player _player2 = null!;
        private Site _siteA = null!;
        private MapNode _node1 = null!, _node2 = null!;

        [TestInitialize]
        public void Setup()
        {
            _system = new SiteControlSystem();
            _player1 = new Player(PlayerColor.Red);
            _player2 = new Player(PlayerColor.Blue);

            // Setup Site with 2 nodes
            _node1 = new MapNode(1, Vector2.Zero);
            _node2 = new MapNode(2, Vector2.Zero);
            _siteA = new CitySite("TestCity", ResourceType.Power, 1, ResourceType.VictoryPoints, 5);
            _siteA.AddNode(_node1);
            _siteA.AddNode(_node2);
        }

        [TestMethod]
        public void Recalculate_AssignsOwner_Majority()
        {
            _node1.Occupant = _player1.Color;
            _system.RecalculateSiteState(_siteA, _player1);
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        [TestMethod]
        public void Recalculate_GrantsTotalControl_IfAllTroopsAndNoSpies()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player1.Color;

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.IsTrue(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void Recalculate_BlocksTotalControl_IfEnemySpyPresent()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player1.Color;
            _siteA.Spies.Add(_player2.Color);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.IsFalse(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void Recalculate_AwardsResources_OnTakingControl()
        {
            _node1.Occupant = _player1.Color;
            _player1.Power = 0;

            // Trigger calculation
            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(1, _player1.Power); // 1 Power from Site Control
        }

        [TestMethod]
        public void Recalculate_HandlesNullPlayer_Gracefully()
        {
            _node1.Occupant = _player1.Color;
            // This is the test case that was previously crashing
            try
            {
                _system.RecalculateSiteState(_siteA, null);
            }
            catch (System.NullReferenceException)
            {
                Assert.Fail("Should not throw NullReferenceException when player is null");
            }
        }

        [TestMethod]
        public void Recalculate_NoOwner_OnTie()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player2.Color;

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(PlayerColor.None, _siteA.Owner);
            Assert.IsFalse(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void Recalculate_NoOwner_OnTieWithNeutral()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = PlayerColor.Neutral;

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(PlayerColor.None, _siteA.Owner);
        }
    }
}