using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.Map
{
    [TestClass]
    public class SpyOperationsTests
    {
        private SpyOperations _spyOps = null!;
        private bool _siteRecalculated = false;

        [TestInitialize]
        public void Setup()
        {
            _siteRecalculated = false;
            _spyOps = new SpyOperations((site, player) => _siteRecalculated = true);
        }

        [TestMethod]
        public void ExecutePlaceSpy_PlacesSpyAndDecreasesBarracks()
        {
            // Arrange
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            var player = new Player(PlayerColor.Red);
            player.SpiesInBarracks = 3;

            // Act
            _spyOps.ExecutePlaceSpy(site, player);

            // Assert
            Assert.IsTrue(site.Spies.Contains(PlayerColor.Red));
            Assert.AreEqual(2, player.SpiesInBarracks);
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecutePlaceSpy_WhenAlreadyHasSpy_DoesNotPlaceAgain()
        {
            // Arrange
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.Spies.Add(PlayerColor.Red);
            var player = new Player(PlayerColor.Red);
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
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.Spies.Add(PlayerColor.Blue);
            var player = new Player(PlayerColor.Red);

            // Act
            var result = _spyOps.ExecuteReturnSpy(site, player, PlayerColor.Blue);

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(site.Spies.Contains(PlayerColor.Blue));
            Assert.IsTrue(_siteRecalculated);
        }

        [TestMethod]
        public void ExecuteReturnSpy_CannotReturnOwnSpy()
        {
            // Arrange
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.Spies.Add(PlayerColor.Red);
            var player = new Player(PlayerColor.Red);

            // Act
            var result = _spyOps.ExecuteReturnSpy(site, player, PlayerColor.Red);

            // Assert
            Assert.IsFalse(result);
            Assert.IsTrue(site.Spies.Contains(PlayerColor.Red)); // Still there
        }

        [TestMethod]
        public void GetEnemySpiesAtSite_ReturnsOnlyEnemySpies()
        {
            // Arrange
            var site = new NonCitySite("TestSite", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.Spies.Add(PlayerColor.Red); // Own spy
            site.Spies.Add(PlayerColor.Blue); // Enemy spy
            site.Spies.Add(PlayerColor.Orange); // Enemy spy
            var player = new Player(PlayerColor.Red);

            // Act
            var result = _spyOps.GetEnemySpiesAtSite(site, player);

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(PlayerColor.Blue));
            Assert.IsTrue(result.Contains(PlayerColor.Orange));
            Assert.IsFalse(result.Contains(PlayerColor.Red));
        }
    }
}
