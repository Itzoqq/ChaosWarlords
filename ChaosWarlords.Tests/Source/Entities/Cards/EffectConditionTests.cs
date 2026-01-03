using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Source.Entities.Cards
{
    [TestClass]
    public class EffectConditionTests
    {
        private MatchContext _context = null!;
        private IMapManager _mapManager = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            var turn = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            var market = Substitute.For<IMarketManager>();
            var action = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();
            var logger = Substitute.For<IGameLogger>();

            _context = new MatchContext(turn, _mapManager, market, action, cardDb, playerState, null, logger);
            _player = new Player(PlayerColor.Red);

            // Default empty map state
            _mapManager.Sites.Returns(new List<Site>());
            _mapManager.Nodes.Returns(new List<MapNode>());
        }

        [TestMethod]
        public void Evaluate_ConditionNone_ReturnsTrue()
        {
            var condition = new EffectCondition(ConditionType.None);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_ControlsSite_WhenControllingSite_ReturnsTrue()
        {
            // Arrange
            var node = new MapNode(0, new Microsoft.Xna.Framework.Vector2(0, 0)) { Occupant = PlayerColor.Red };
            // Constructor: name, controlRes, controlAmt, totalRes, totalAmt
            var site = new StartingSite("TestSite", ResourceType.Power, 1, ResourceType.VictoryPoints, 2);
            site.NodesInternal.Add(node); 
            
            var list = new List<Site> { site };
            _mapManager.Sites.Returns(list);

            var condition = new EffectCondition(ConditionType.ControlsSite);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasTroopsDeployed_WhenHasTroop_ReturnsTrue()
        {
            var node = new MapNode(0, new Microsoft.Xna.Framework.Vector2(0, 0)) { Occupant = PlayerColor.Red };
            _mapManager.Nodes.Returns(new List<MapNode> { node });

            var condition = new EffectCondition(ConditionType.HasTroopsDeployed);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasResourceAmount_Power_ReturnsTrueIfThresholdMet()
        {
            _player.Power = 5;
            var condition = new EffectCondition(ConditionType.HasResourceAmount, 5, ResourceType.Power);
            Assert.IsTrue(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_HasResourceAmount_Power_ReturnsFalseIfBelow()
        {
            _player.Power = 4;
            var condition = new EffectCondition(ConditionType.HasResourceAmount, 5, ResourceType.Power);
            Assert.IsFalse(condition.Evaluate(_context, _player));
        }

        [TestMethod]
        public void Evaluate_InnerCircleCount_ReturnsTrueIfMet()
        {
             // How to add to InnerCircle? It's a public property List<Card> usually?
             // Let's check Player.cs
             _player.InnerCircle.Add(new Card("1", "Test", 0, CardAspect.Neutral, 0, 0, 0));
             
             var condition = new EffectCondition(ConditionType.InnerCircleCount, 1);
             Assert.IsTrue(condition.Evaluate(_context, _player));
        }
    }
}
