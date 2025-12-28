using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Input;
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
        public void Recalculate_AssignsOwner_Majority_TroopsOnly()
        {
            _node1.Occupant = _player1.Color;
            
            // Spy should NOT count for control
            _siteA.Spies.Add(_player2.Color); 
            
            _system.RecalculateSiteState(_siteA, _player1);
            
            // Red has 1 Troop, Blue has 0 Troops (1 Spy). Red should control.
            Assert.AreEqual(_player1.Color, _siteA.Owner);
        }

        [TestMethod]
        public void Recalculate_GrantsTotalControl_IfNoEnemyPresence()
        {
            _node1.Occupant = _player1.Color;
            // Node 2 is empty. Standard Tyrants rules: Total Control = Control + No Enemies.
            // Empty nodes do not prevent Total Control.

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.IsTrue(_siteA.HasTotalControl);
        }

        [TestMethod]
        public void Recalculate_BlocksTotalControl_IfEnemySpyPresent()
        {
            _node1.Occupant = _player1.Color;
            // Enemy Spy present
            _siteA.Spies.Add(_player2.Color);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(_player1.Color, _siteA.Owner); // Still Owner
            Assert.IsFalse(_siteA.HasTotalControl); // But No Total Control
        }

        [TestMethod]
        public void Recalculate_BlocksTotalControl_IfEnemyTroopPresent()
        {
            _node1.Occupant = _player1.Color;
            _node2.Occupant = _player2.Color; 
            // 1 vs 1 -> Tie -> No Owner usually.
            // Let's add another node for Red to ensure they own it but enemy exists.
            var node3 = new MapNode(3, Vector2.Zero);
            node3.Occupant = _player1.Color;
            _siteA.AddNode(node3);

            _system.RecalculateSiteState(_siteA, _player1);

            Assert.AreEqual(_player1.Color, _siteA.Owner); // 2 vs 1
            Assert.IsFalse(_siteA.HasTotalControl); // Enemy troop exists
        }

        [TestMethod]
        public void DistributeRewards_City_GivesIncome()
        {
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _player1.Power = 0;

            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            Assert.AreEqual(1, _player1.Power);
        }

        [TestMethod]
        public void DistributeRewards_NonCity_GivesZero()
        {
            _siteA.IsCity = false;
            _siteA.Owner = _player1.Color;
            _player1.Power = 0;

            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            Assert.AreEqual(0, _player1.Power);
        }

        [TestMethod]
        public void DistributeRewards_Additive_TotalControl()
        {
            _siteA.IsCity = true;
            _siteA.Owner = _player1.Color;
            _siteA.HasTotalControl = true;
            _player1.Power = 0;
            _player1.VictoryPoints = 0;

            // Control = 1 Power, Total = 5 VP
            var sites = new List<Site> { _siteA };
            _system.DistributeStartOfTurnRewards(sites, _player1);

            Assert.AreEqual(1, _player1.Power);
            Assert.AreEqual(5, _player1.VictoryPoints);
        }
    }
}


