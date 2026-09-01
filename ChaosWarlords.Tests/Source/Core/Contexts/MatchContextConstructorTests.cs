using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Core.Contexts
{
    // Coverage for MatchContext's own constructor guards - flagged in planning.txt TIER 1
    // (risk-hotspot remediation, 2026-09-01) as a coverage gap: 7 "?? throw
    // ArgumentNullException" checks, none of which any existing test actually triggered. This
    // is the composition root every match depends on, so a silently-broken guard here (e.g. a
    // future refactor that drops one by accident) deserves the same explicit coverage every
    // other precondition in this codebase gets.
    [TestClass]
    [TestCategory("Unit")]
    public class MatchContextConstructorTests
    {
        private ITurnManager _turnManager = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IActionSystem _actionSystem = null!;
        private ICardDatabase _cardDatabase = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private IGameLogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _turnManager = Substitute.For<ITurnManager>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
            _playerStateManager = Substitute.For<IPlayerStateManager>();
            _logger = Substitute.For<IGameLogger>();
        }

        private MatchContext Build() => new(
            _turnManager, _mapManager, _marketManager, _actionSystem,
            _cardDatabase, _playerStateManager, _logger);

        [TestMethod]
        public void Constructor_WithNullTurnManager_ThrowsArgumentNullException()
        {
            _turnManager = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullMapManager_ThrowsArgumentNullException()
        {
            _mapManager = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullMarketManager_ThrowsArgumentNullException()
        {
            _marketManager = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullActionSystem_ThrowsArgumentNullException()
        {
            _actionSystem = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullCardDatabase_ThrowsArgumentNullException()
        {
            _cardDatabase = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullPlayerStateManager_ThrowsArgumentNullException()
        {
            _playerStateManager = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            _logger = null!;
            Assert.ThrowsExactly<ArgumentNullException>(() => Build());
        }

        [TestMethod]
        public void Constructor_WithNoSeed_DefaultsToEnvironmentTickCountBasedSeed()
        {
            // Seed isn't null-guarded (it's an int?, defaults via ?? Environment.TickCount) -
            // covered here for completeness alongside the guard tests above, not because it's
            // part of the Crap-score gap itself.
            var context = Build();

            Assert.IsNotNull(context.Random);
        }

        [TestMethod]
        public void Constructor_WithExplicitSeed_UsesItDirectly()
        {
            var context = new MatchContext(
                _turnManager, _mapManager, _marketManager, _actionSystem,
                _cardDatabase, _playerStateManager, _logger, seed: 42);

            Assert.AreEqual(42, context.Seed);
        }
    }
}
