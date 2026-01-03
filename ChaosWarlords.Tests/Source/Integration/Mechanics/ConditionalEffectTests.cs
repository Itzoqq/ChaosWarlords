using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    [TestClass]
    [TestCategory("Integration")]
    public class ConditionalEffectTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private IGameLogger _logger = null!;
        
        [TestInitialize]
        public void Setup()
        {
            TestLogger.Initialize();
            _logger = TestLogger.Instance;
            
            // Setup generic mocks
            var turn = Substitute.For<ITurnManager>();
            var map = Substitute.For<IMapManager>();
            var market = Substitute.For<IMarketManager>();
            var action = Substitute.For<IActionSystem>();
            var cardDb = Substitute.For<ICardDatabase>();
            var playerState = Substitute.For<IPlayerStateManager>();

            // Setup real MatchContext which initializes CardRuleEngine internally
            _context = new MatchContext(turn, map, market, action, cardDb, playerState, _logger);
            
            _player = new Player(PlayerColor.Red);
            turn.ActivePlayer.Returns(_player);

            // Default Map Manager behavior (empty)
            map.Sites.Returns(new List<Site>());
            map.Nodes.Returns(new List<MapNode>());
        }

        [TestMethod]
        public void Puppeteer_ConditionMet_AppliesEffect()
        {
            // Arrange: Player controls a site
            var site = new StartingSite("TestSite", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            var node = new MapNode(1, new Microsoft.Xna.Framework.Vector2(0,0)) { Occupant = PlayerColor.Red };
            site.NodesInternal.Add(node);
            _context.MapManager.Sites.Returns(new List<Site> { site });

            // Create Conditional Card (Puppeteer: +2 Power if control site)
            var card = new Card("puppeteer", "Puppeteer", 3, CardAspect.Sorcery, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            effect.Condition = new EffectCondition(ConditionType.ControlsSite);
            card.AddEffect(effect);

            // Act: Resolve Effects
            CardEffectProcessor.ResolveEffects(card, _context, false, _logger);

            // Assert: Power was added
            _context.PlayerStateManager.Received(1).AddPower(_player, 2);
        }

        [TestMethod]
        public void Puppeteer_ConditionNotMet_SkippedEffect()
        {
            // Arrange: Player controls NO sites
            _context.MapManager.Sites.Returns(new List<Site>()); // Empty sites list

            // Create Conditional Card
            var card = new Card("puppeteer", "Puppeteer", 3, CardAspect.Sorcery, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 2, ResourceType.Power);
            effect.Condition = new EffectCondition(ConditionType.ControlsSite);
            card.AddEffect(effect);

            // Act: Resolve Effects
            CardEffectProcessor.ResolveEffects(card, _context, false, _logger);

            // Assert: Power was NOT added
            _context.PlayerStateManager.DidNotReceive().AddPower(_player, Arg.Any<int>());
        }
    }
}
