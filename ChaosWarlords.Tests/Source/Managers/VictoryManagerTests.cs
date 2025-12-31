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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class VictoryManagerTests
    {
        private VictoryManager _victoryManager = null!;
        private MatchContext _context = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private Player _p1 = null!;
        private Player _p2 = null!;

        [TestInitialize]
        public void Setup()
        {
            var logger = Substitute.For<IGameLogger>();
            _victoryManager = new VictoryManager(logger);

            _p1 = new Player(PlayerColor.Red, System.Guid.NewGuid(), "Player 1");
            _p2 = new Player(PlayerColor.Blue, System.Guid.NewGuid(), "Player 2");

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            var actionSystem = Substitute.For<IActionSystem>();
            var cardDatabase = Substitute.For<ICardDatabase>();
            var playerState = new PlayerStateManager(logger);
            var mockRandom = Substitute.For<IGameRandom>();
            var turnManager = new TurnManager(new List<Player> { _p1, _p2 }, mockRandom, logger);

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                actionSystem,
                cardDatabase,
                playerState,
                logger
            );
        }

        [TestMethod]
        public void CheckEndGameConditions_ReturnsFalse_WhenGameInProgress()
        {
            // Arrange
            _p1.TroopsInBarracks = 5;
            _p2.TroopsInBarracks = 5;
            _marketManager.MarketRow.Returns(new List<Card> { TestData.Cards.CheapCard() });
            _marketManager.HasCardsInDeck().Returns(true);

            // Act
            bool result = _victoryManager.CheckEndGameConditions(_context, out _);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CheckEndGameConditions_ReturnsTrue_WhenPlayerHasNoTroops()
        {
            // Arrange
            _p1.TroopsInBarracks = 0; // Empty
            _p2.TroopsInBarracks = 5;
            _marketManager.MarketRow.Returns(new List<Card> { TestData.Cards.CheapCard() });
            _marketManager.HasCardsInDeck().Returns(true);

            // Act
            bool result = _victoryManager.CheckEndGameConditions(_context, out var reason);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckEndGameConditions_ReturnsTrue_WhenMarketEmpty()
        {
            // Arrange
            _p1.TroopsInBarracks = 5;
            _p2.TroopsInBarracks = 5;
            _marketManager.MarketRow.Returns(new List<Card>());
            _marketManager.HasCardsInDeck().Returns(false);

            // Act
            bool result = _victoryManager.CheckEndGameConditions(_context, out var reason);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CalculateFinalScore_SumsPointsCorrectly()
        {
            // Arrange
            _p1.VictoryPoints = 10; // Base VP Tokens
            _p1.TrophyHall = 3; // 3 VP

            // Setup Sites
            // Site 1: Controlled by P1 (1 VP)
            var site1 = new NonCitySite("Site 1", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            site1.EndGameVictoryPoints = 1;
            site1.Owner = _p1.Color;
            site1.HasTotalControl = false;

            // Site 2: Total Control by P1 (1 VP + 2 Bonus = 3 VP)
            var site2 = new NonCitySite("Site 2", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            site2.EndGameVictoryPoints = 1;
            site2.Owner = _p1.Color;
            site2.HasTotalControl = true;

            _mapManager.Sites.Returns(new List<Site> { site1, site2 });

            // Setup Deck
            // Card 1: DeckVP = 2
            var card1 = new Card("c1", "Card 1", 1, CardAspect.Neutral, 2, 5, 0);
            _p1.DeckManager.AddToTop(card1);

            // Setup Inner Circle
            // Card 2: InnerCircleVP = 5
            var card2 = new Card("c2", "Card 2", 1, CardAspect.Neutral, 2, 5, 0);
            _p1.InnerCircle.Add(card2);


            // Expected Score:
            // VP Tokens: 10
            // Trophy Hall: 3
            // Site 1: 1
            // Site 2: 3 (1+2)
            // Deck: 2
            // Inner Circle: 5
            // Total: 24

            // Act
            int score = _victoryManager.CalculateFinalScore(_p1, _context);

            // Assert
            Assert.AreEqual(24, score);
        }

        [TestMethod]
        public void DetermineWinner_ReturnsHighestScoringPlayer()
        {
            // Arrange
            // P1 Score: 20
            _p1.VictoryPoints = 20;

            // P2 Score: 10
            _p2.VictoryPoints = 10;
            
            _mapManager.Sites.Returns(new List<Site>());

            // Act
            var winner = _victoryManager.DetermineWinner(new List<Player> { _p1, _p2 }, _context);

            // Assert
            Assert.AreEqual(_p1, winner);
        }

        [TestMethod]
        public void ToVictoryDto_MapsCorrectly()
        {
            // Arrange
            _p1.VictoryPoints = 10;
            _p2.VictoryPoints = 5;
            _p1.SeatIndex = 0;
            _p2.SeatIndex = 1;

            // Mock end game to true
            // We can't mock VictoryManager since it's the class under test, 
            // but we are testing DTO Mapper + VictoryManager integration here essentially.
            // Let's force a condition that makes CheckEndGameConditions true.
            _marketManager.MarketRow.Returns(new List<Card>());
            _marketManager.HasCardsInDeck().Returns(false);

            // Act
            // We use the real VictoryManager here
            var dto = ChaosWarlords.Source.Core.Utilities.DtoMapper.ToVictoryDto(_context, _victoryManager);

            // Assert
            Assert.IsTrue(dto.IsGameOver);
            Assert.AreEqual("Market deck is empty!", dto.VictoryReason);
            Assert.AreEqual(0, dto.WinnerSeat);
            Assert.AreEqual("Player 1", dto.WinnerName);
            Assert.AreEqual(10, dto.FinalScores[0]);
            Assert.AreEqual(5, dto.FinalScores[1]);

            // Verify Breakdown
            Assert.IsTrue(dto.ScoreBreakdowns.ContainsKey(0));
            Assert.AreEqual(10, dto.ScoreBreakdowns[0].TotalScore);
            Assert.AreEqual(10, dto.ScoreBreakdowns[0].VPTokens);

            // Verify Colors
            Assert.IsTrue(dto.PlayerColors.ContainsKey(0));
            Assert.AreEqual("Red", dto.PlayerColors[0]);
            Assert.AreEqual("Blue", dto.PlayerColors[1]);
        }

        [TestMethod]
        public void GetScoreBreakdown_ReturnsCorrectComponents()
        {
            // Arrange
            _p1.VictoryPoints = 10;
            _p1.TrophyHall = 3;
            // Mock other sources as 0 for simplicity
            _mapManager.Sites.Returns(new List<Site>());

            // Act
            var breakdown = _victoryManager.GetScoreBreakdown(_p1, _context);

            // Assert
            Assert.AreEqual(13, breakdown.TotalScore);
            Assert.AreEqual(10, breakdown.VPTokens);
            Assert.AreEqual(3, breakdown.TrophyHallVP);
            Assert.AreEqual(0, breakdown.SiteControlVP);
            Assert.AreEqual(0, breakdown.DeckVP);
            Assert.AreEqual(0, breakdown.InnerCircleVP);
        }
    }
}
