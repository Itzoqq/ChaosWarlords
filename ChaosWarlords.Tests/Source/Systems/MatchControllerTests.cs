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

            // Ensure TurnManager is created with valid players
            var turnManagerConcrete = new TurnManager(players);

            // Reconstruct the full context matching your constructor signature
            _context = new MatchContext(turnManagerConcrete, _mapManager, _marketManager, _actionSystem, _cardDatabase);

            _controller = new MatchController(_context);

            // Set active player for consistency
            _context.ActionSystem.SetCurrentPlayer(_p1);
        }

        #region Legacy Tests (Preserved)

        [TestMethod]
        public void CanEndTurn_ReturnsFalse_IfHandNotEmpty()
        {
            _p1.Hand.Add(new Card("c", "c", 0, CardAspect.Neutral, 0, 0, 0));

            bool result = _controller.CanEndTurn(out string reason);

            Assert.IsFalse(result);
            Assert.AreEqual("You must play all cards in your hand before ending your turn.", reason);
        }

        [TestMethod]
        public void CanEndTurn_ReturnsTrue_IfHandEmpty()
        {
            _p1.Hand.Clear();

            bool result = _controller.CanEndTurn(out string reason);

            Assert.IsTrue(result);
            Assert.AreEqual(string.Empty, reason);
        }

        [TestMethod]
        public void PlayCard_MovesCard_FromHand_ToPlayed()
        {
            var card = new Card("test", "Test Card", 0, CardAspect.Warlord, 0, 0, 0);
            _p1.Hand.Add(card);

            _controller.PlayCard(card);

            Assert.DoesNotContain(card, _p1.Hand);
            Assert.Contains(card, _p1.PlayedCards);
        }

        [TestMethod]
        public void PlayCard_ResolvesEffects_GainPower()
        {
            var card = new Card("power", "Power Card", 0, CardAspect.Warlord, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _p1.Hand.Add(card);
            _p1.Power = 0;

            _controller.PlayCard(card);

            Assert.AreEqual(3, _p1.Power);
        }

        [TestMethod]
        public void EndTurn_CyclesDeck_IncludingPlayedCards()
        {
            // Scenario: Deck is empty. We played 1 card. Hand is empty.
            // Result should be: Played card goes to Discard -> Discard shuffled to Deck -> Draw happens.

            _p1.Hand.Clear();
            _p1.Deck.Clear();
            _p1.DiscardPile.Clear();

            var playedCard = new Card("played", "Played", 0, CardAspect.Neutral, 0, 0, 0);
            _p1.PlayedCards.Add(playedCard);

            // Act
            _controller.EndTurn();

            // Assert
            // 1. Played card should have been moved to Discard
            // 2. Discard should have been reshuffled into Deck
            // 3. New hand should contain that card (since deck was size 1)
            Assert.HasCount(1, _p1.Hand, "Should have drawn the reshuffled card.");
            Assert.AreEqual(playedCard, _p1.Hand[0], "The played card should be back in hand.");
            Assert.IsEmpty(_p1.PlayedCards);
        }

        #endregion

        #region Focus Mechanic Tests (New)

        [TestMethod]
        public void PlayCard_WithoutFocus_EffectDoesNotTrigger()
        {
            // Setup: Create a card that requires Focus to give 5 Power
            var focusCard = new Card("focus_test", "Focus Card", 2, CardAspect.Shadow, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true; // Crucial: This effect needs Focus
            focusCard.AddEffect(effect);

            _p1.Power = 0;
            _p1.Hand.Add(focusCard);

            // Act: Play it as the ONLY card (No Focus condition met)
            _controller.PlayCard(focusCard);

            // Assert
            Assert.AreEqual(0, _p1.Power, "Effect triggered but Focus condition was NOT met!");
            Assert.Contains(focusCard, _p1.PlayedCards);
        }

        [TestMethod]
        public void PlayCard_WithFocus_FromPreviouslyPlayed_TriggersEffect()
        {
            // Setup: A setup card (Shadow) and the Finisher (Shadow + Focus Effect)
            var setupCard = new Card("shadow_bolt", "Shadow Bolt", 1, CardAspect.Shadow, 0, 0, 0);

            var focusCard = new Card("shadow_finisher", "Shadow Finisher", 2, CardAspect.Shadow, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true;
            focusCard.AddEffect(effect);

            _p1.Power = 0;
            _p1.Hand.Add(setupCard);
            _p1.Hand.Add(focusCard);

            // Act
            _controller.PlayCard(setupCard); // Play 1st Shadow card
            _controller.PlayCard(focusCard); // Play 2nd Shadow card (Should have Focus)

            // Assert
            Assert.AreEqual(5, _p1.Power, "Focus Effect did not trigger after playing a previous Shadow card!");
        }

        [TestMethod]
        public void PlayCard_WithFocus_FromHandReveal_TriggersEffect()
        {
            // Setup: We play the Focus card, but we HOLD another Shadow card in hand
            var revealCard = new Card("shadow_held", "Hidden Shadow", 1, CardAspect.Shadow, 0, 0, 0);

            var focusCard = new Card("shadow_finisher", "Shadow Finisher", 2, CardAspect.Shadow, 0, 0, 0);
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true;
            focusCard.AddEffect(effect);

            _p1.Power = 0;
            _p1.Hand.Add(focusCard);
            _p1.Hand.Add(revealCard); // This stays in hand

            // Act: Play the focus card
            _controller.PlayCard(focusCard);

            // Assert
            Assert.AreEqual(5, _p1.Power, "Focus Effect did not trigger using Hand Reveal!");

            // Verify state: Reveal card is still in hand, Focus card is played
            Assert.Contains(revealCard, _p1.Hand);
            Assert.DoesNotContain(revealCard, _p1.PlayedCards);
            Assert.Contains(focusCard, _p1.PlayedCards);
        }

        #endregion
    }
}