using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Systems
{
    [TestClass]
    public class MatchControllerTests
    {
        private MatchContext _context = null!;
        private MatchController _controller = null!;

        // Removed unused _turnManager field
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;

        private Player _p1 = null!;
        private Player _p2 = null!;

        [TestInitialize]
        public void Setup()
        {
            // 1. Create Players
            _p1 = new Player(PlayerColor.Red);
            _p2 = new Player(PlayerColor.Blue);
            var players = new List<Player> { _p1, _p2 };

            // 2. Create Mocks
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();

            // 3. Use real TurnManager for logic
            var turnManagerConcrete = new TurnManager(players);

            // 4. Create Context
            _context = new MatchContext(turnManagerConcrete, _mapManager, _marketManager, _actionSystem, _cardDatabase);

            // 5. Create Controller
            _controller = new MatchController(_context);
            _context.ActionSystem.SetCurrentPlayer(_p1);
        }

        [TestMethod]
        public void PlayCard_MovesCard_FromHand_ToPlayed()
        {
            // Arrange
            var card = new Card("test", "Test Card", 0, CardAspect.Warlord, 0, 0, 0);
            _p1.Hand.Add(card);

            // Act
            _controller.PlayCard(card);

            // Assert
            Assert.DoesNotContain(card, _p1.Hand);
            Assert.Contains(card, _p1.PlayedCards);
        }

        [TestMethod]
        public void PlayCard_ResolvesEffects_GainPower()
        {
            // Arrange
            var card = new Card("power", "Power Card", 0, CardAspect.Warlord, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _p1.Hand.Add(card);
            _p1.Power = 0;

            // Act
            _controller.PlayCard(card);

            // Assert
            Assert.AreEqual(3, _p1.Power);
        }

        [TestMethod]
        public void ResolveCardEffects_SkipsFocus_IfConditionNotMet()
        {
            // Arrange
            var card = new Card("focus", "Focus Card", 0, CardAspect.Sorcery, 0, 0, 0);
            // Effect requires Focus (played another Sorcery card)
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Influence);
            effect.RequiresFocus = true;
            card.AddEffect(effect);

            _p1.Hand.Add(card);
            _p1.Influence = 0;

            // Act
            _controller.PlayCard(card);

            // Assert
            Assert.AreEqual(0, _p1.Influence, "Should not gain influence because Focus condition was not met (no other Sorcery cards played).");
        }

        [TestMethod]
        public void ResolveCardEffects_AppliesFocus_IfConditionMet()
        {
            // Arrange
            // 1. Put a Sorcery card in played pile already
            var prevCard = new Card("prev", "Prev", 0, CardAspect.Sorcery, 0, 0, 0);
            _p1.PlayedCards.Add(prevCard);

            // 2. Play the Focus card
            var card = new Card("focus", "Focus Card", 0, CardAspect.Sorcery, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Influence);
            effect.RequiresFocus = true;
            card.AddEffect(effect);
            _p1.Hand.Add(card);

            // Act
            _controller.PlayCard(card);

            // Assert
            Assert.AreEqual(5, _p1.Influence, "Should gain influence because Focus condition WAS met.");
        }

        [TestMethod]
        public void EndTurn_PerformsCleanupAndSwitch()
        {
            // Arrange
            _p1.Power = 5;
            _p1.PlayedCards.Add(new Card("c1", "c1", 0, CardAspect.Neutral, 0, 0, 0));
            _p1.Hand.Add(new Card("c2", "c2", 0, CardAspect.Neutral, 0, 0, 0));

            // Fill deck so draw works
            for (int i = 0; i < 10; i++) _p1.Deck.Add(new Card("d", "d", 0, CardAspect.Neutral, 0, 0, 0));

            // Act
            _controller.EndTurn();

            // Assert
            // 1. Map Rewards Distributed
            _mapManager.Received(1).DistributeControlRewards(_p1);

            // 2. Resources Reset
            Assert.AreEqual(0, _p1.Power);

            // 3. Hand/Played moved to Discard
            Assert.IsEmpty(_p1.PlayedCards);
            Assert.HasCount(5, _p1.Hand); // Drew new hand
            Assert.IsGreaterThanOrEqualTo(2, _p1.DiscardPile.Count); // The played card + remaining hand

            // 4. Turn Switched
            Assert.AreEqual(_p2, _context.ActivePlayer);
        }
    }
}