using NSubstitute;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

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

            // Add filler cards to Deck so DrawCards(5) doesn't force a Reshuffle
            for (int i = 0; i < 10; i++)
            {
                _p1.Deck.Add(new Card("filler", "Filler", 0, 0, 0, 0, 0));
            }

            // Act
            _controller.EndTurn();

            // Assert
            Assert.AreEqual(0, _p1.Power, "Resources should be cleared");
            Assert.IsEmpty(_p1.PlayedCards, "Played cards should be moved to discard");
            Assert.HasCount(1, _p1.DiscardPile, "Discard pile should contain the cleaned up card");
            Assert.HasCount(5, _p1.Hand, "Should draw new hand");

            // Verify Turn Manager Switched
            Assert.AreEqual(_p2, _context.TurnManager.ActivePlayer);
            
            // Verify Map Rewards Distributed
            _mapManager.Received(1).DistributeControlRewards(_p1);
        }

        [TestMethod]
        public void CanEndTurn_ReturnsTrue_EvenIfHandNotEmpty()
        {
            // Arrange
            _p1.Hand.Add(new Card("rem", "Remaining", 0, 0, 0, 0, 0));

            // Act
            bool result = _controller.CanEndTurn(out string reason);

            // Assert
            Assert.IsTrue(result, "Should allow ending turn with cards in hand");
            Assert.AreEqual(string.Empty, reason);
        }

        [TestMethod]
        public void EndTurn_DiscardsRemainingHand()
        {
            // Arrange
            var cardInHand = new Card("h1", "HandCard", 0, 0, 0, 0, 0);
            _p1.Hand.Add(cardInHand);

            // Filler for deck
            for (int i = 0; i < 10; i++) _p1.Deck.Add(new Card("f", "f", 0, 0, 0, 0, 0));

            // Act
            _controller.EndTurn();

            // Assert
            Assert.IsEmpty(_p1.Hand.Where(c => c == cardInHand), "Old hand should be cleared");
            Assert.Contains(cardInHand, _p1.DiscardPile, "Remaining hand card should be in discard pile");
        }

        [TestMethod]
        public void PlayCard_FirstCardOfAspect_DoesNotTriggerFocus_SelfReferenceFix()
        {
            // This tests the "Snapshot" fix. 
            // If the card counts itself after moving to played, Focus would be TRUE.
            // If it snapshots before moving, Focus is FALSE.

            var card = new Card("focus_self", "Self Check", 1, CardAspect.Shadow, 0, 0, 0);
            // Effect: Gain 5 Power ONLY if Focus.
            var effect = new CardEffect(EffectType.GainResource, 5, ResourceType.Power);
            effect.RequiresFocus = true;
            card.AddEffect(effect);

            _p1.Hand.Add(card);
            _p1.Power = 0;

            // Act
            _controller.PlayCard(card);

            // Assert
            // Since it's the first card, and no others in hand, Focus should be FALSE.
            // Therefore, 0 Power gained. (If bug exists, it would be 5).
            Assert.AreEqual(0, _p1.Power, "Focus incorrectly triggered by the card itself!");
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

        [TestMethod]
        public void DevourCard_RemovesCardFromHand_AndAddsToVoid()
        {
            // Arrange
            var cardToDevour = new Card("weak_minion", "Weak Minion", 0, CardAspect.Neutral, 0, 0, 0);
            cardToDevour.Location = CardLocation.Hand;
            _p1.Hand.Add(cardToDevour);

            // Act
            _controller.DevourCard(cardToDevour);

            // Assert
            Assert.DoesNotContain(cardToDevour, _p1.Hand, "Card should be removed from Hand.");
            Assert.Contains(cardToDevour, _context.VoidPile, "Card should be added to Void Pile.");
            Assert.AreEqual(CardLocation.Void, cardToDevour.Location, "Card Location property should be updated to Void.");
        }

        [TestMethod]
        public void DevourCard_DoesNotCrash_IfCardNotInHand()
        {
            // Arrange
            var cardInDeck = new Card("deck_card", "Deck Card", 0, CardAspect.Neutral, 0, 0, 0);
            _p1.Deck.Add(cardInDeck);

            // Act
            _controller.DevourCard(cardInDeck);

            // Assert
            Assert.DoesNotContain(cardInDeck, _context.VoidPile, "Should not move card if it wasn't in the expected source (Hand).");
            Assert.IsEmpty(_p1.Hand);
        }
    }
}