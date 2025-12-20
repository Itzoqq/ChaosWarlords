using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities; // Added for PlayerColor/CardAspect visibility

namespace ChaosWarlords.Tests.Contexts
{
    [TestClass]
    public class TurnContextTests
    {
        private TurnContext _turnContext = null!;
        private Player _dummyPlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create a dummy player to satisfy the constructor requirement
            _dummyPlayer = new Player(PlayerColor.Red);
            _turnContext = new TurnContext(_dummyPlayer);
        }

        [TestMethod]
        public void Constructor_StartsEmpty()
        {
            Assert.IsNotNull(_turnContext.PlayedAspectCounts);
            Assert.IsEmpty(_turnContext.PlayedAspectCounts);
            Assert.AreEqual(_dummyPlayer, _turnContext.ActivePlayer);
        }

        [TestMethod]
        public void RecordPlayedCard_IncrementsCount()
        {
            // Act
            _turnContext.RecordPlayedCard(CardAspect.Shadow);

            // Assert
            Assert.IsTrue(_turnContext.PlayedAspectCounts.ContainsKey(CardAspect.Shadow));
            Assert.AreEqual(1, _turnContext.PlayedAspectCounts[CardAspect.Shadow]);
        }

        [TestMethod]
        public void RecordPlayedCard_IncrementsExistingCount()
        {
            // Arrange
            _turnContext.RecordPlayedCard(CardAspect.Shadow);

            // Act
            _turnContext.RecordPlayedCard(CardAspect.Shadow);

            // Assert
            Assert.AreEqual(2, _turnContext.PlayedAspectCounts[CardAspect.Shadow]);
        }

        // REMOVED: Reset_ClearsAllCounts
        // Reason: Your TurnContext class does not have a Reset() method. 
        // The TurnManager handles "Resetting" by throwing away the old context and making a new one.
    }
}