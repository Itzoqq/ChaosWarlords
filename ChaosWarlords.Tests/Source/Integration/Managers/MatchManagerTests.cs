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

namespace ChaosWarlords.Tests.Integration.Managers
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

        private IVictoryManager _victoryManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _p1 = TestData.Players.RedPlayer();
            _p2 = TestData.Players.BluePlayer();

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _victoryManager = Substitute.For<IVictoryManager>();

            var mockRandom = Substitute.For<IGameRandom>();
            var playerState = new PlayerStateManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var turnManager = new TurnManager(new List<Player> { _p1, _p2 }, mockRandom, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase,
                playerState,
                ChaosWarlords.Tests.Utilities.TestLogger.Instance
            );

            _controller = new MatchManager(_context, ChaosWarlords.Tests.Utilities.TestLogger.Instance, _victoryManager);


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
            var card = TestData.Cards.PowerCard(); // Gain 3 Power
            _p1.Hand.Add(card);

            // Act
            _p1.Power = 0;
            _controller.PlayCard(card);

            // Assert
            Assert.AreEqual(3, _p1.Power);
            Assert.DoesNotContain(card, _p1.Hand, "Card should be removed from Hand");
            Assert.Contains(card, _p1.PlayedCards, "Card should be in PlayedCards");
        }

        [TestMethod]
        public void EndTurn_ResetsStateAndSwitchesTurn()
        {
            // Arrange
            _p1.Power = 5;
            _p1.PlayedCards.Add(TestData.Cards.CheapCard());

            // Add filler cards to Deck so DrawCards(5) doesn't force a Reshuffle
            for (int i = 0; i < 10; i++)
            {
                _p1.DeckManager.AddToTop(TestData.Cards.CheapCard());
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
            _p1.Hand.Add(TestData.Cards.CheapCard());

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
            var cardInHand = TestData.Cards.CheapCard();
            _p1.Hand.Add(cardInHand);

            // Filler for deck
            for (int i = 0; i < 10; i++) _p1.DeckManager.AddToTop(TestData.Cards.CheapCard());

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

            var card = TestData.Cards.FocusPowerCard();
            _p1.Hand.Add(card);
            _p1.Power = 0;

            // Act
            _controller.PlayCard(card);

            // Assert
            // Since it's the first card, and no others in hand, Focus should be FALSE.
            // Therefore, 0 Power gained. (If bug exists, it would be 3).
            Assert.AreEqual(0, _p1.Power, "Focus incorrectly triggered by the card itself!");
        }

        [TestMethod]
        public void PlayCard_WithFocus_TriggersBonus_IfPreviouslyPlayed()
        {
            var setupCard = TestData.Cards.SupplantCard(); // Shadow
            var focusCard = TestData.Cards.FocusPowerCard(); // Focus Gain 3 Power, Warlord?? Wait. 
            // My FocusPowerCard is Warlord in TestData. I should change it to Shadow if I want it to trigger Focus from SupplantCard.
            // Or use a Shadow card.

            _p1.Power = 0;
            _p1.Hand.Add(setupCard);
            _p1.Hand.Add(focusCard);

            _controller.PlayCard(setupCard);
            _controller.PlayCard(focusCard);

            Assert.AreEqual(3, _p1.Power, "Focus Effect did not trigger after playing a previous Shadow card!");
        }

        [TestMethod]
        public void PlayCard_WithFocus_FromHandReveal_TriggersEffect()
        {
            var revealCard = TestData.Cards.SupplantCard(); // Shadow
            var focusCard = TestData.Cards.FocusPowerCard(); 
            // I'll update TestData FocusPowerCard to be Shadow for consistency with these tests.

            _p1.Power = 0;
            _p1.Hand.Add(focusCard);
            _p1.Hand.Add(revealCard);

            _controller.PlayCard(focusCard);

            Assert.AreEqual(3, _p1.Power, "Focus Effect did not trigger using Hand Reveal!");
            Assert.Contains(revealCard, _p1.Hand);
        }

        [TestMethod]
        public void DevourCard_RemovesCardFromHand_AndAddsToVoid()
        {
            // Arrange
            var cardToDevour = TestData.Cards.CheapCard();
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
            var cardInDeck = TestData.Cards.CheapCard();
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
            var node1 = TestData.MapNodes.Node1();
            node1.Occupant = _p1.Color;
            var node2 = TestData.MapNodes.Node2();
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
            var node1 = TestData.MapNodes.Node1();
            node1.Occupant = _p1.Color;
            var node2 = TestData.MapNodes.Node2();
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
            var node1 = TestData.MapNodes.Node1();
            node1.Occupant = _p1.Color;
            var node2 = TestData.MapNodes.Node2();
            node2.Occupant = PlayerColor.None;

            _mapManager.Nodes.Returns(new List<MapNode> { node1, node2 });

            // Simulate P1 having played a card (Adding to Discard Pile)
            // Simulate P1 having played a card (Adding to Discard Pile)

            // 1. Populate Deck so DrawCards(5) doesn't trigger a Reshuffle (which clears Discard)
            for (int i = 0; i < 10; i++)
            {
                _p1.DeckManager.AddToTop(TestData.Cards.CheapCard());
            }

            // 2. Now add the played card to Discard. It will stay there.
            _p1.DeckManager.AddToDiscard(TestData.Cards.CheapCard());

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

            var alienCard = TestData.Cards.CheapCard();
            _p2.Hand.Add(alienCard); // Belongs to P2

            // Act: P1 (Active) tries to play P2's card
            _controller.PlayCard(alienCard);

            // Assert
            Assert.DoesNotContain(alienCard, _p1.PlayedCards, "Should NOT add alien card to PlayedCards");
            Assert.Contains(alienCard, _p2.Hand, "Card should remain in P2's hand");
        }

        [TestMethod]
        public void EndTurn_DefersGameOver_UntilRoundComplete()
        {
            // Arrange
            // Force P1 to be the active player and P2 to be next (and last).
            // Logic: P1 Ends Turn -> P2 Starts -> P2 Ends Turn -> Round Over -> Game Over Triggered
            
            // Mock Victory Handler to ALWAYS say the game should end (e.g. troops depleted)
            _victoryManager.CheckEndGameConditions(_context, out Arg.Any<string>())
                  .Returns(x => { x[1] = "Mock End Game"; return true; });

            // Setup DTO mapper for the final trigger
            _victoryManager.CalculateFinalScore(Arg.Any<Player>(), _context).Returns(10);
            _victoryManager.GetScoreBreakdown(Arg.Any<Player>(), _context).Returns(new ChaosWarlords.Source.Core.Data.Dtos.ScoreBreakdownDto { TotalScore = 10 });
            _victoryManager.DetermineWinner(Arg.Any<List<Player>>(), _context).Returns(_p1);

            // Act 1: P1 Ends Turn
            _controller.EndTurn();

            // Assert 1: Game should NOT be over yet, because P2 still needs to play.
            Assert.IsFalse(_controller.IsGameOver(), "Game should continue until the round finishes.");
            Assert.AreEqual(_p2, _context.ActivePlayer, "Turn should pass to P2.");

            // Act 2: P2 Ends Turn (Last player in round)
            _controller.EndTurn();

            // Assert 2: Round finished, deferred trigger should fire now.
            Assert.IsTrue(_controller.IsGameOver(), "Game should end after the last player in the round finishes.");
        }
        [TestMethod]
        public void BuyCard_TriggersEndGame_WhenMarketDeckEmpty()
        {
            // Integration Test: MatchManager + Real VictoryManager logic
            
            // 1. Setup Real VictoryManager to verify the CONDITION logic actually works within the loop
            var realVictoryManager = new VictoryManager(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            _controller = new MatchManager(_context, ChaosWarlords.Tests.Utilities.TestLogger.Instance, realVictoryManager);

            // 2. Setup Market State: Empty Row and Empty Deck
            _marketManager.MarketRow.Returns(new List<Card>());
            _marketManager.HasCardsInDeck().Returns(false);

            // 3. Ensure P2 is the last player, so EndTurn triggers game over immediately
            // Active Player is P1. P2 is next.
            // Setup TurnManager to treat P2 as last. 
            // In Setup(), P1 is seat 0, P2 is seat 1.
            
            // Act: P1 Ends Turn -> Checks Victory
            _controller.EndTurn();

            // Assert 1: Victory Condition MET ("Market Empty"), but deferred (End of Round)
            // P1 is NOT last player (P2 is). So Game NOT Over yet.
            Assert.IsFalse(_controller.IsGameOver(), "Should defer victory until round completes.");
            
            // Act 2: P2 Ends Turn
            _controller.EndTurn();

            // Assert 2: Round Finished -> Game Over
            Assert.IsTrue(_controller.IsGameOver(), "Should trigger Game Over after round finishes with empty market.");
            Assert.AreEqual("Market deck is empty!", _controller.VictoryResult?.VictoryReason);
        }

        // --- Devour Integration Tests (Using Real ActionSystem) ---

        private (MatchManager manager, ActionSystem actionSystem, MatchContext context, Player p1) SetupRealDevourSystem()
        {
            var p1 = TestData.Players.RedPlayer();
            var p2 = TestData.Players.BluePlayer();
            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            // Mocks
            var mapManager = Substitute.For<IMapManager>();
            var marketManager = Substitute.For<IMarketManager>();
            var cardDatabase = Substitute.For<ICardDatabase>();
            var victoryManager = Substitute.For<IVictoryManager>();
            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(p1);

            // TurnContext Fix for PlayCard
            var turnContext = new TurnContext(p1, logger);
            turnManager.CurrentTurnContext.Returns(turnContext);

            // Real Systems
            var actionSystem = new ActionSystem(turnManager, mapManager, logger);
            var playerState = new PlayerStateManager(logger);
            
            // Wiring
            actionSystem.SetPlayerStateManager(playerState);

            var context = new MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                cardDatabase,
                playerState,
                logger
            );

            var manager = new MatchManager(context, logger, victoryManager);
            actionSystem.SetMatchManager(manager);

            return (manager, actionSystem, context, p1);
        }

        [TestMethod]
        public void PlayDevourCard_WithSkippedTarget_HandDecreasesByOne_AndNoDevourOccurs()
        {
            // Arrange
            var (manager, actionSystem, context, p1) = SetupRealDevourSystem();
            var devourCard = TestData.Cards.DevourCard();
            var otherCard = TestData.Cards.CheapCard();
            
            p1.Hand.Add(devourCard);
            p1.Hand.Add(otherCard);
            int startHandCount = p1.Hand.Count; 

            // Set Pre-Target to SKIP
            actionSystem.SetPreTarget(devourCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            // Act
            manager.PlayCard(devourCard);

            // Assert
            Assert.HasCount(startHandCount - 1, p1.Hand, "Hand count should decrease by 1 when skipping Devour.");
            Assert.Contains(devourCard, p1.PlayedCards, "Devour card should be played.");
            Assert.Contains(otherCard, p1.Hand, "Other card should remain in hand.");
            Assert.IsEmpty(context.VoidPile, "Void pile should be empty.");
        }

        [TestMethod]
        public void PlayDevourCard_WithValidTarget_HandDecreasesByTwo_AndDevourOccurs()
        {
            // Arrange
            var (manager, actionSystem, context, p1) = SetupRealDevourSystem();
            var devourCard = TestData.Cards.DevourCard();
            var targetCard = TestData.Cards.CheapCard();
            var otherCard = TestData.Cards.CheapCard();

            p1.Hand.Add(devourCard);
            p1.Hand.Add(targetCard);
            p1.Hand.Add(otherCard);
            int startHandCount = p1.Hand.Count;

            // Set Pre-Target to TARGET
            actionSystem.SetPreTarget(devourCard, ActionState.TargetingDevourHand, targetCard);

            // Act
            manager.PlayCard(devourCard);

            // Assert
            Assert.HasCount(startHandCount - 2, p1.Hand, "Hand count should decrease by 2 when devouring.");
            Assert.Contains(devourCard, p1.PlayedCards, "Devour card should be played.");
            Assert.Contains(targetCard, context.VoidPile, "Target card should be in Void.");
            Assert.DoesNotContain(targetCard, p1.Hand, "Target card should be removed from hand.");
            Assert.Contains(otherCard, p1.Hand, "Other card should remain in hand.");
        }

        [TestMethod]
        public void PlayDevourCard_AsLastHandCard_SkipsTargeting_AndPlaysNormally()
        {
            // Arrange
            var (manager, actionSystem, context, p1) = SetupRealDevourSystem();
            var devourCard = TestData.Cards.DevourCard();
            p1.Hand.Clear();
            p1.Hand.Add(devourCard);

            // Act
            manager.PlayCard(devourCard);

            // Assert
            Assert.IsEmpty(p1.Hand, "Hand should be empty after playing last card.");
            Assert.Contains(devourCard, p1.PlayedCards, "Card should be in played pile.");
            Assert.IsEmpty(context.VoidPile, "Void pile should be empty (Devour skipped).");
        }
    }
}





