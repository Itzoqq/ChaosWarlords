using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]

    [TestCategory("Unit")]
    public class SpyOperationsTests
    {
        private SpyOperations _spyOps = null!;
        private bool _siteRecalculated = false;
        private PlayerStateManager _playerState = null!;

        [TestInitialize]
        public void Setup()
        {
            Utilities.TestLogger.Initialize();
            _siteRecalculated = false;
            _playerState = new PlayerStateManager(Utilities.TestLogger.Instance);
            _spyOps = new SpyOperations(
                (site, player) => { _siteRecalculated = true; },
                _playerState,
                Utilities.TestLogger.Instance
            );
        }

        [TestMethod]
        public void ExecutePlaceSpy_PlacesSpyAndDecreasesBarracks()
        {
            // Arrange
            var site = TestData.Sites.NeutralSite();
            var player = TestData.Players.RedPlayer();
            player.SpiesInBarracks = 3;

            // Act
            _spyOps.ExecutePlaceSpy(site, player);

            // Assert
            Assert.Contains(PlayerColor.Red, site.Spies);
            Assert.AreEqual(2, player.SpiesInBarracks);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecutePlaceSpy_WhenAlreadyHasSpy_DoesNotPlaceAgain()
        {
            // Arrange
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Red);
            var player = TestData.Players.RedPlayer();
            player.SpiesInBarracks = 3;

            // Act
            _spyOps.ExecutePlaceSpy(site, player);

            // Assert
            Assert.AreEqual(1, site.Spies.Count(s => s == PlayerColor.Red));
            Assert.AreEqual(3, player.SpiesInBarracks); // Unchanged
        }

        [TestMethod]
        public void ExecuteReturnSpy_RemovesEnemySpy()
        {
            // Arrange
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Blue);
            var player = TestData.Players.RedPlayer();

            // Act
            var result = _spyOps.ExecuteReturnSpy(site, player, PlayerColor.Blue);

            // Assert
            Assert.IsTrue(result);
            Assert.DoesNotContain(PlayerColor.Blue, site.Spies);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteReturnSpy_CannotReturnOwnSpy()
        {
            // Arrange
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Red);
            var player = TestData.Players.RedPlayer();

            // Act
            var result = _spyOps.ExecuteReturnSpy(site, player, PlayerColor.Red);

            // Assert
            Assert.IsFalse(result);
            Assert.Contains(PlayerColor.Red, site.Spies); // Still there
        }

        [TestMethod]
        public void ExecuteReturnOwnSpy_RemovesSpyAndReplenishesBarracks()
        {
            // A returned spy must go back to the player's own barracks so it's re-placeable
            // later, matching CombatResolver.ExecuteReturnTroop's equivalent AddTroops call for
            // a returned troop.
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Red);
            var player = TestData.Players.RedPlayer();
            player.SpiesInBarracks = 2;

            var result = _spyOps.ExecuteReturnOwnSpy(site, player);

            Assert.IsTrue(result);
            Assert.DoesNotContain(PlayerColor.Red, site.Spies);
            Assert.AreEqual(3, player.SpiesInBarracks, "The returned spy must be added back to the player's own barracks.");
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteReturnOwnSpy_WhenNoSpyAtSite_ReturnsFalseAndDoesNotChangeBarracks()
        {
            var site = TestData.Sites.NeutralSite();
            var player = TestData.Players.RedPlayer();
            player.SpiesInBarracks = 2;

            var result = _spyOps.ExecuteReturnOwnSpy(site, player);

            Assert.IsFalse(result);
            Assert.AreEqual(2, player.SpiesInBarracks);
            Assert.IsFalse(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteReturnOwnSpy_CannotReturnAnEnemysSpy()
        {
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Blue);
            var player = TestData.Players.RedPlayer();
            player.SpiesInBarracks = 2;

            var result = _spyOps.ExecuteReturnOwnSpy(site, player);

            Assert.IsFalse(result);
            Assert.Contains(PlayerColor.Blue, site.Spies);
            Assert.AreEqual(2, player.SpiesInBarracks);
        }

        [TestMethod]
        public void GetEnemySpiesAtSite_ReturnsOnlyEnemySpies()
        {
            // Arrange
            var site = TestData.Sites.NeutralSite();
            site.Spies.Add(PlayerColor.Red); // Own spy
            site.Spies.Add(PlayerColor.Blue); // Enemy spy
            site.Spies.Add(PlayerColor.Orange); // Enemy spy
            var player = TestData.Players.RedPlayer();

            // Act
            var result = SpyOperations.GetEnemySpiesAtSite(site, player);

            // Assert
            Assert.HasCount(2, result);
            Assert.Contains(PlayerColor.Blue, result);
            Assert.Contains(PlayerColor.Orange, result);
            Assert.DoesNotContain(PlayerColor.Red, result);
        }
    }
}



