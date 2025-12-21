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
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;
        private Player _p1 = null!;
        private Player _p2 = null!;

        [TestInitialize]
        public void Setup()
        {
            _p1 = new Player(PlayerColor.Red);
            _p2 = new Player(PlayerColor.Blue);
            var players = new List<Player> { _p1, _p2 };

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();

            var turnManagerConcrete = new TurnManager(players);

            _context = new MatchContext(
                turnManagerConcrete,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase
            );

            _controller = new MatchController(_context);
        }

        [TestMethod]
        public void PlayCard_MovesCardToPlayed_AndAppliesEffects()
        {
            // Arrange
            var card = new Card("test", "Test Minion", 3, CardAspect.Warlord, 1, 2, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 2, ResourceType.Power));
            _p1.Hand.Add(card);

            // Act
            _controller.PlayCard(card);

            // Assert
            Assert.AreEqual(2, _p1.Power);
            Assert.DoesNotContain(card, _p1.Hand, "Card should be removed from Hand");
            Assert.Contains(card, _p1.PlayedCards, "Card should be in PlayedCards");
        }

        [TestMethod]
        public void EndTurn_ResetsStateAndSwitchesTurn()
        {
            // Arrange
            _p1.Power = 5;
            _p1.PlayedCards.Add(new Card("c1", "c1", 0, 0, 0, 0, 0));

            // FIX: Add filler cards to Deck so DrawCards(5) doesn't force a Reshuffle
            // If the deck is empty, DrawCards will pull from Discard, emptying it again.
            for (int i = 0; i < 10; i++)
            {
                _p1.Deck.Add(new Card("filler", "Filler", 0, 0, 0, 0, 0));
            }

            // Act
            _controller.EndTurn();

            // Assert
            Assert.AreEqual(0, _p1.Power, "Resources should be cleared"); // CleanUpTurn clears resources? (Verify logic if needed)
            Assert.IsEmpty(_p1.PlayedCards, "Played cards should be moved to discard");
            Assert.HasCount(1, _p1.DiscardPile, "Discard pile should contain the cleaned up card");
            Assert.HasCount(5, _p1.Hand, "Should draw new hand");

            // Verify Turn Manager Switched
            Assert.AreEqual(_p2, _context.TurnManager.ActivePlayer);
        }

        [TestMethod]
        public void PlayCard_WithFocus_TriggersBonus_IfPreviouslyPlayed()
        {
            var setupCard = new Card("shadow1", "Shadow Init", 1, CardAspect.Shadow, 0, 0, 0);
            var focusCard = new Card("shadow2", "Shadow Focus", 1, CardAspect.Shadow, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true;
            focusCard.AddEffect(effect);

            _p1.Power = 0;
            _p1.Hand.Add(setupCard);
            _p1.Hand.Add(focusCard);

            _controller.PlayCard(setupCard);
            _controller.PlayCard(focusCard);

            Assert.AreEqual(5, _p1.Power, "Focus Effect did not trigger after playing a previous Shadow card!");
        }

        [TestMethod]
        public void PlayCard_WithFocus_FromHandReveal_TriggersEffect()
        {
            var revealCard = new Card("shadow_held", "Hidden Shadow", 1, CardAspect.Shadow, 0, 0, 0);
            var focusCard = new Card("shadow_finisher", "Shadow Finisher", 2, CardAspect.Shadow, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true;
            focusCard.AddEffect(effect);

            _p1.Power = 0;
            _p1.Hand.Add(focusCard);
            _p1.Hand.Add(revealCard);

            _controller.PlayCard(focusCard);

            Assert.AreEqual(5, _p1.Power, "Focus Effect did not trigger using Hand Reveal!");
            Assert.Contains(revealCard, _p1.Hand);
        }
    }
}