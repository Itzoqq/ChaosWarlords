using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class TurnManagerTests
    {
        private Player _playerRed = null!;
        private Player _playerBlue = null!;
        private ITurnManager _turnManager = null!;

        // Cards for testing PlayCard/Focus logic
        private Card _shadowCard = null!;
        private Card _sorceryCard = null!;
        private Card _neutralCard = null!;

        [TestInitialize]
        public void Setup()
        {
            _playerRed = new Player(PlayerColor.Red);
            _playerBlue = new Player(PlayerColor.Blue);

            // Initialize TurnManager with two players
            _turnManager = new TurnManager(new List<Player> { _playerRed, _playerBlue });

            // Initialize test cards
            _shadowCard = new Card("shadow", "Shadow Card", 1, CardAspect.Shadow, 1, 1, 0);
            _sorceryCard = new Card("sorcery", "Sorcery Card", 1, CardAspect.Sorcery, 1, 1, 0);
            _neutralCard = new Card("neutral", "Neutral Card", 0, CardAspect.Neutral, 0, 0, 0);
        }

        [TestMethod]
        public void Constructor_InitializesCorrectly()
        {
            // Assert 1: Player list is correct
            Assert.HasCount(2, _turnManager.Players);
            Assert.AreEqual(_playerRed, _turnManager.Players[0]);
            Assert.AreEqual(_playerBlue, _turnManager.Players[1]);

            // Assert 2: Active player starts at index 0
            Assert.AreEqual(_playerRed, _turnManager.ActivePlayer, "Active player should be the first player in the list.");

            // Assert 3: Turn context is reset (empty dictionary)
            Assert.IsNotNull(_turnManager.PlayedAspectCounts);
            Assert.IsEmpty(_turnManager.PlayedAspectCounts, "PlayedAspectCounts should start empty.");
        }

        [TestMethod]
        public void EndTurn_SwitchesToNextPlayer()
        {
            // Act 1: End turn (from Red to Blue)
            _turnManager.EndTurn();
            Assert.AreEqual(_playerBlue, _turnManager.ActivePlayer, "Active player should switch from Red to Blue.");

            // Act 2: End turn (from Blue back to Red - testing the modulo wrap-around)
            _turnManager.EndTurn();
            Assert.AreEqual(_playerRed, _turnManager.ActivePlayer, "Active player should wrap around to Red.");
        }

        [TestMethod]
        public void EndTurn_ResetsPlayedAspectCounts()
        {
            // Arrange: Play a card to populate the dictionary
            _turnManager.PlayCard(_shadowCard);
            Assert.IsTrue(_turnManager.PlayedAspectCounts.ContainsKey(CardAspect.Shadow));

            // Act: End the turn
            _turnManager.EndTurn();

            // Assert: Dictionary should be empty for the new player (Blue)
            Assert.IsEmpty(_turnManager.PlayedAspectCounts, "PlayedAspectCounts should be reset after EndTurn.");
        }

        [TestMethod]
        public void PlayCard_AddsNewAspect()
        {
            // Act
            _turnManager.PlayCard(_shadowCard);

            // Assert
            Assert.HasCount(1, _turnManager.PlayedAspectCounts);
            Assert.AreEqual(1, _turnManager.PlayedAspectCounts[CardAspect.Shadow], "New aspect should start with a count of 1.");
        }

        [TestMethod]
        public void PlayCard_IncrementsExistingAspect()
        {
            // Arrange: Play one card
            _turnManager.PlayCard(_shadowCard);
            Assert.AreEqual(1, _turnManager.PlayedAspectCounts[CardAspect.Shadow]);

            // Act: Play a SECOND card of the same aspect (Distinct Instance)
            // BAD: _turnManager.PlayCard(_shadowCard);
            // GOOD: Create a fresh copy to simulate a second card in hand
            var secondShadowCard = new Card("shadow_2", "Shadow Card", 1, CardAspect.Shadow, 1, 1, 0);
            _turnManager.PlayCard(secondShadowCard);

            // Assert
            Assert.HasCount(1, _turnManager.PlayedAspectCounts, "Count of distinct aspects should remain 1.");
            Assert.AreEqual(2, _turnManager.PlayedAspectCounts[CardAspect.Shadow], "Aspect count should be incremented to 2.");
        }

        [TestMethod]
        public void PlayCard_TracksMultipleAspects()
        {
            // Act: Play cards of three distinct aspects
            _turnManager.PlayCard(_shadowCard);
            _turnManager.PlayCard(_sorceryCard);
            _turnManager.PlayCard(_neutralCard);

            // Act: Play Sorcery again (Distinct Instance)
            var secondSorceryCard = new Card("sorcery_2", "Sorcery Card", 1, CardAspect.Sorcery, 1, 1, 0);
            _turnManager.PlayCard(secondSorceryCard);

            // Assert
            Assert.HasCount(3, _turnManager.PlayedAspectCounts, "Should track 3 distinct aspects.");
            Assert.AreEqual(1, _turnManager.PlayedAspectCounts[CardAspect.Shadow]);
            Assert.AreEqual(2, _turnManager.PlayedAspectCounts[CardAspect.Sorcery], "Sorcery count should be 2.");
            Assert.AreEqual(1, _turnManager.PlayedAspectCounts[CardAspect.Neutral]);
        }
    }
}