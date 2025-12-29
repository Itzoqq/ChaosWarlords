using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using NSubstitute;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Systems
{
    [TestClass]

    [TestCategory("Integration")]
    public class MatchManagerTests
    {
        private MatchContext _context = null!;
        private MatchManager _controller = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;
        private Player _p1 = null!;
        private Player _p2 = null!;

        [TestInitialize]
        public void Setup()
        {
            _p1 = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
            _p2 = new PlayerBuilder().WithColor(PlayerColor.Blue).Build();

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();

            var mockRandom = Substitute.For<IGameRandom>();
            var playerState = new PlayerStateManager();
            var turnManagerConcrete = new TurnManager(new List<Player> { _p1, _p2 }, mockRandom);

            _context = new MatchContext(
                turnManagerConcrete,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase,
                playerState
            );

            _controller = new MatchManager(_context);

            // Ensure _p1 always refers to the Active Player for test consistency.
            if (_context.ActivePlayer != _p1)
            {
                var temp = _p1;
                _p1 = _p2;
                _p2 = temp;
            }
        }

        [TestMethod]
        public void PlayCard_MovesCardToPlayed_AndAppliesEffects()
        {
            // Arrange
            var card = new CardBuilder()
                .WithName("test")
                .WithCost(3)
                .WithAspect(CardAspect.Warlord)
                .WithPower(1)
                .WithInfluence(2)
                .WithEffect(EffectType.GainResource, 2, ResourceType.Power)
                .Build();
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
            _p1.PlayedCards.Add(new CardBuilder().WithName("c1").WithCost(0).Build());

            // Add filler cards to Deck so DrawCards(5) doesn't force a Reshuffle
            for (int i = 0; i < 10; i++)
            {
                _p1.DeckManager.AddToTop(new CardBuilder().WithName("filler").WithCost(0).Build());
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

            // Verify Map Rewards Distributed to NEW player (Start of Turn)
            _mapManager.Received(1).DistributeStartOfTurnRewards(_p2);
        }

        [TestMethod]
        public void CanEndTurn_ReturnsTrue_EvenIfHandNotEmpty()
        {
            // Arrange
            _p1.Hand.Add(new CardBuilder().WithName("rem").WithCost(0).Build());

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
            var cardInHand = new CardBuilder().WithName("h1").WithCost(0).Build();
            _p1.Hand.Add(cardInHand);

            // Filler for deck
            for (int i = 0; i < 10; i++) _p1.DeckManager.AddToTop(new CardBuilder().WithName("f").WithCost(0).Build());

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

            var card = new CardBuilder().WithName("focus_self").WithCost(1).WithAspect(CardAspect.Shadow).WithPower(0).WithInfluence(0).WithVP(0).Build();
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
            var setupCard = new CardBuilder().WithName("shadow1").WithCost(1).WithAspect(CardAspect.Shadow).WithPower(0).WithInfluence(0).WithVP(0).Build();
            var focusCard = new CardBuilder().WithName("shadow2").WithCost(1).WithAspect(CardAspect.Shadow).WithPower(0).WithInfluence(0).WithVP(0).Build();
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
            var revealCard = new CardBuilder().WithName("shadow_held").WithCost(1).WithAspect(CardAspect.Shadow).WithPower(0).WithInfluence(0).WithVP(0).Build();
            var focusCard = new CardBuilder().WithName("shadow_finisher").WithCost(2).WithAspect(CardAspect.Shadow).WithPower(0).WithInfluence(0).WithVP(0).Build();
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
            var cardToDevour = new CardBuilder().WithName("weak_minion").WithCost(0).WithAspect(CardAspect.Neutral).WithPower(0).WithInfluence(0).WithVP(0).Build();
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
            var cardInDeck = new CardBuilder().WithName("deck_card").WithCost(0).WithAspect(CardAspect.Neutral).WithPower(0).WithInfluence(0).WithVP(0).Build();
            _p1.DeckManager.AddToTop(cardInDeck);

            // Act
            _controller.DevourCard(cardInDeck);

            // Assert
            Assert.DoesNotContain(cardInDeck, _context.VoidPile, "Should not move card if it wasn't in the expected source (Hand).");
            Assert.IsEmpty(_p1.Hand);
        }

        [TestMethod]
        public void EndTurn_StaysInSetup_IfPlayerHasNoTroops()
        {
            // Arrange
            _context.CurrentPhase = MatchPhase.Setup;
            // MapManager Mock: P1 has 1 troop, P2 has 0 troops
            var node1 = new MapNodeBuilder().WithId(1).Build();
            node1.Occupant = _p1.Color;
            var node2 = new MapNodeBuilder().WithId(2).Build();
            node2.Occupant = PlayerColor.None;

            _mapManager.Nodes.Returns(new List<MapNode> { node1, node2 });

            // Act
            _controller.EndTurn();

            // Assert
            Assert.AreEqual(MatchPhase.Setup, _context.CurrentPhase, "Should stay in Setup if not all players deployed.");
            _mapManager.DidNotReceive().SetPhase(MatchPhase.Playing);
        }

        [TestMethod]
        public void EndTurn_TransitionsToPlaying_IfAllPlayersHaveTroops()
        {
            // Arrange
            _context.CurrentPhase = MatchPhase.Setup;
            // MapManager Mock: P1 has 1 troop, P2 has 1 troop
            var node1 = new MapNodeBuilder().WithId(1).Build();
            node1.Occupant = _p1.Color;
            var node2 = new MapNodeBuilder().WithId(2).Build();
            node2.Occupant = _p2.Color;

            _mapManager.Nodes.Returns(new List<MapNode> { node1, node2 });

            // Act
            _controller.EndTurn();

            // Assert
            Assert.AreEqual(MatchPhase.Playing, _context.CurrentPhase, "Should transition to Playing if all players deployed.");
            _mapManager.Received(1).SetPhase(MatchPhase.Playing);
        }

        [TestMethod]
        public void EndTurn_TransitionsToPlaying_IfPlayerHasPlayedCards_EvenIfDeploymentIncomplete()
        {
            // Arrange
            _context.CurrentPhase = MatchPhase.Setup;
            // MapManager Mock: P1 has 1 troop, P2 has 0 (Failed to deploy)
            var node1 = new MapNodeBuilder().WithId(1).Build();
            node1.Occupant = _p1.Color;
            var node2 = new MapNodeBuilder().WithId(2).Build();
            node2.Occupant = PlayerColor.None;

            _mapManager.Nodes.Returns(new List<MapNode> { node1, node2 });

            // Simulate P1 having played a card (Adding to Discard Pile)
            // Simulate P1 having played a card (Adding to Discard Pile)

            // 1. Populate Deck so DrawCards(5) doesn't trigger a Reshuffle (which clears Discard)
            for (int i = 0; i < 10; i++)
            {
                _p1.DeckManager.AddToTop(new CardBuilder().WithName("filler").WithCost(0).Build());
            }

            // 2. Now add the played card to Discard. It will stay there.
            _p1.DeckManager.AddToDiscard(new CardBuilder().WithName("test").WithCost(0).Build());

            // Act
            _controller.EndTurn();

            // Assert
            Assert.AreEqual(MatchPhase.Playing, _context.CurrentPhase, "Should transition to Playing if game has progressed (Discard Pile not empty).");
            _mapManager.Received(1).SetPhase(MatchPhase.Playing);
        }
        [TestMethod]
        public void PlayCard_Fails_IfCardNotOwnedByPlayer()
        {
            // Scenario: UI mistakenly sends a card commanded by the player but physically located in another player's hand/deck.
            // This happens if references are leaked or UI targeting is loose.

            var alienCard = new CardBuilder().WithName("alien").WithCost(0).WithAspect(CardAspect.Neutral).WithPower(0).WithInfluence(0).WithVP(0).Build();
            _p2.Hand.Add(alienCard); // Belongs to P2

            // Act: P1 (Active) tries to play P2's card
            _controller.PlayCard(alienCard);

            // Assert
            Assert.DoesNotContain(alienCard, _p1.PlayedCards, "Should NOT add alien card to PlayedCards");
            Assert.Contains(alienCard, _p2.Hand, "Card should remain in P2's hand");
        }
    }
}


