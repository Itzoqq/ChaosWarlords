using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Map;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
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
            var stateManager = new PlayerStateManager();
            _spyOps = new SpyOperations((site, player) => _siteRecalculated = true, stateManager);
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
            Assert.Contains(PlayerColor.Red, site.Spies);
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
            Assert.DoesNotContain(PlayerColor.Blue, site.Spies);
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
            Assert.Contains(PlayerColor.Red, site.Spies); // Still there
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
            Assert.HasCount(2, result);
            Assert.Contains(PlayerColor.Blue, result);
            Assert.Contains(PlayerColor.Orange, result);
            Assert.DoesNotContain(PlayerColor.Red, result);
        }
    }
}



