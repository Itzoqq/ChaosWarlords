using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Tests.Source.Core.Contexts
{
    [TestClass]
    [TestCategory("Unit")]
    public class MatchContextHashingTests
    {
        private MatchContext _context = null!;
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;
        private IPlayerStateManager _playerStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            
            // Setup Basic Map
            _mapManager.Nodes.Returns(new List<MapNode>());

            // Setup Basic Players
            _turnManager.Players.Returns(new List<Player>());
            
            // Setup Market
            _marketManager.MarketRow.Returns(new List<Card>());

            _context = new MatchContext(
                _turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase,
                _playerStateManager,
                null,
                TestLogger.Instance,
                12345
            );
        }

        [TestMethod]
        public void GetStateHash_ReturnsDeterministicResult()
        {
            // Arrange
            var hash1 = _context.GetStateHash();
            var hash2 = _context.GetStateHash();

            // Assert
            Assert.AreEqual(hash1, hash2, "Hash should be deterministic for identical states.");
        }

        [TestMethod]
        public void GetStateHash_ChangesWhenPhaseChanges()
        {
            // Arrange
            var hash1 = _context.GetStateHash();

            // Act
            _context.CurrentPhase = MatchPhase.Playing;
            var hash2 = _context.GetStateHash();

            // Assert
            Assert.AreNotEqual(hash1, hash2, "Hash should change when Phase changes.");
        }

        [TestMethod]
        public void GetStateHash_ChangesWhenSequenceUpdates()
        {
            // Arrange
            var hash1 = _context.GetStateHash();

            // Act
            _context.SequenceNumber++;
            var hash2 = _context.GetStateHash();

            // Assert
            Assert.AreNotEqual(hash1, hash2, "Hash should change when SequenceNumber increments.");
        }

        [TestMethod]
        public void GetStateHash_ChangesWhenPlayerResourcesChange()
        {
            // Arrange
            var player1 = new Player(PlayerColor.Red);
            // Default 0 power
            
            _turnManager.Players.Returns(new List<Player> { player1 });
            var hash1 = _context.GetStateHash();

            // Act
            // Simulate state change by returning a different player state
            // (Since we can't easily mutate the locked-down Player instance directly in a unit test without a real StateManager)
            var player2 = new Player(PlayerColor.Red);
            // We need to use reflection or a text-friend helper to set power if we can't use builder?
            // Actually, let's just use the fact that the hash checks Power.
            // If we can't set power, we can use a PlayerBuilder if available.
            // Let's assume we can use a simpler approach: 
            // The method checks: hash = hash * 31 + player.Power;
            // If both new Players have 0 power, hash won't change.
            // We need to change Power.
            
            // Allow Test to use reflection to set backing field for unit testing purposes
            var powerField = typeof(Player).GetField("_power", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (powerField == null) Assert.Fail("Could not find _power backing field");
            
            powerField.SetValue(player2, 100);

            _turnManager.Players.Returns(new List<Player> { player2 });
            var hash2 = _context.GetStateHash();

            // Assert
            Assert.AreNotEqual(hash1, hash2, "Hash should change when Player Power changes.");
        }

        [TestMethod]
        public void GetStateHash_ChangesWhenMapNodeOccupantChanges()
        {
            // Arrange
            var node = new MapNode(1, new ChaosWarlords.Source.Core.Data.LogicVector2(10 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, 10 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor));
            _mapManager.Nodes.Returns(new List<MapNode> { node });
            var hash1 = _context.GetStateHash();

            // Act
            node.Occupant = PlayerColor.Red;
            var hash2 = _context.GetStateHash();

            // Assert
            Assert.AreNotEqual(hash1, hash2, "Hash should change when MapNode occupant changes.");
        }
    }
}
